using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Caching;
using Atlas.Core.Gemini;
using Atlas.Core.Html;
using Atlas.Core.Models;
using Atlas.Core.Scanner;
using Atlas.Core.Serialization;
using Atlas.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Agents;

public sealed class AtlasAgentPipelineOptions
{
    public int Concurrency { get; set; } = 16;
    public bool DisableCache { get; set; }
    public bool ForceSynth { get; set; }
    public string? CustomCacheDir { get; set; }
    public string? ServerUrl { get; set; }
    public Atlas.Core.Client.AtlasServerClient? ServerClient { get; set; }
    public int MaxValidationRepairAttempts { get; set; } = 2;
    public int MaxDiagramRepairAttempts { get; set; } = 3;
}

public sealed class AtlasAgentPipeline
{
    private readonly ILlmClient _llmClient;
    private readonly FileSummaryAgent _summaryAgent;
    private readonly ILogger<AtlasAgentPipeline> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AtlasAgentPipeline(
        ILlmClient llmClient,
        FileSummaryAgent summaryAgent,
        ILogger<AtlasAgentPipeline> logger,
        ILoggerFactory loggerFactory)
    {
        _llmClient = llmClient;
        _summaryAgent = summaryAgent;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<AtlasResource> ExecuteAsync(
        CodebaseSkeleton skeleton,
        AtlasAgentPipelineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AtlasAgentPipelineOptions();
        _logger.LogInformation("[AtlasAgentPipeline] Starting Map-Reduce Analysis for: {RepoName}", skeleton.RepoName);

        // Compute current file SHAs
        var currentFileShas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in skeleton.HighValueFiles)
        {
            var sha = GitBlobShaCalculator.ComputeBlobShaForText(file.Content);
            currentFileShas[file.RelativePath] = sha;
        }

        var gitCommit = skeleton.Git?.CommitSha ?? "unknown";
        var hasRemoteServer = !string.IsNullOrWhiteSpace(options.ServerUrl) && options.ServerClient != null;

        // =========================================================================
        // 0. CHECK SYNTHESIS CACHE (100% INSTANT HIT / ZERO TOKENS)
        // =========================================================================
        if (!options.DisableCache && !options.ForceSynth)
        {
            if (hasRemoteServer)
            {
                var remoteCheck = await options.ServerClient!.CheckSynthesisAsync(
                    options.ServerUrl!,
                    skeleton.RepoName,
                    gitCommit,
                    currentFileShas,
                    cancellationToken);

                if (remoteCheck != null && remoteCheck.IsExactMatch && remoteCheck.CachedResource != null)
                {
                    _logger.LogInformation("[AtlasAgentPipeline] ⚡ 100% Instant Remote Synthesis Cache HIT from Atlas Server for commit {Commit} (0 LLM tokens, 100% idempotent).",
                        gitCommit);
                    return remoteCheck.CachedResource;
                }
            }
        }

        // =========================================================================
        // 1. MAP & REDUCE SYNTHESIS
        // =========================================================================
        AtlasResourceSpec extractedSpec;
        var mapStopwatch = Stopwatch.StartNew();
        var reduceStopwatch = new Stopwatch();

        _logger.LogInformation("[AtlasAgentPipeline] --- Phase 1: MAP (Summarizing {Count} key files via Remote Cache & LLM) ---",
            skeleton.HighValueFiles.Count);

        var (allSummaries, newlyComputedSummaries) = await SummarizeFilesWithRemoteCacheAsync(
            skeleton.HighValueFiles,
            options,
            cancellationToken);

        mapStopwatch.Stop();
        _logger.LogInformation("[AtlasAgentPipeline] Map Phase completed in {ElapsedMs} ms ({Count} files summarized, {NewCount} newly analyzed).",
            mapStopwatch.ElapsedMilliseconds, allSummaries.Count, newlyComputedSummaries.Count);

        // Store new summaries remotely
        if (hasRemoteServer && newlyComputedSummaries.Count > 0 && !options.DisableCache)
        {
            await options.ServerClient!.StoreFileSummariesAsync(options.ServerUrl!, newlyComputedSummaries, cancellationToken);
        }

        // Multi-Pass Synthesis Phase
        reduceStopwatch.Start();
        var synthEngine = new MultiPassSynthesisEngine(_llmClient, _loggerFactory.CreateLogger<MultiPassSynthesisEngine>());
        extractedSpec = await synthEngine.ExecuteMultiPassSynthesisAsync(skeleton, allSummaries, cancellationToken);
        reduceStopwatch.Stop();
        _logger.LogInformation("[AtlasAgentPipeline] Multi-Pass Synthesis completed in {ElapsedMs} ms.", reduceStopwatch.ElapsedMilliseconds);

        // =========================================================================
        // 3. ITERATIVE DIAGRAM VALIDATION & AUTO-REPAIR LOOP
        // =========================================================================
        await ValidateAndRepairDiagramsAsync(extractedSpec, skeleton, options.MaxDiagramRepairAttempts, cancellationToken);

        // =========================================================================
        // 4. BUILD INITIAL CRD INSTANCE & SCHEMA REPAIR
        // =========================================================================
        var crd = AssembleCrd(extractedSpec, skeleton);

        // =========================================================================
        // 4.1 GENERATE CYCLONEDX 1.5 SBOM & OSV.DEV VULNERABILITY AUDITS
        // =========================================================================
        try
        {
            var sbomGen = new CycloneDxGenerator();
            var (sbom, vulnAudit) = await sbomGen.GenerateAsync(skeleton, cancellationToken);
            crd.Spec.Dependencies.Sbom = sbom;
            crd.Spec.Dependencies.VulnerabilityAudit = vulnAudit;
            _logger.LogInformation("[AtlasAgentPipeline] Generated CycloneDX 1.5 SBOM ({Components} components, {Vulns} vulnerabilities)",
                sbom.Components.Count, vulnAudit.TotalVulnerabilities);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AtlasAgentPipeline] Could not generate CycloneDX SBOM or OSV audit");
        }

        crd = await ValidateAndRepairCrdAsync(crd, skeleton, options.MaxValidationRepairAttempts, cancellationToken);

        // =========================================================================
        // 5. INGEST INTO ATLAS SERVER
        // =========================================================================
        if (hasRemoteServer && !options.DisableCache)
        {
            var ingestResult = await options.ServerClient!.IngestCatalogItemAsync(
                options.ServerUrl!,
                crd,
                gitCommit,
                currentFileShas,
                cancellationToken);

            if (ingestResult != null && ingestResult.Success)
            {
                _logger.LogInformation("[AtlasAgentPipeline] Successfully ingested catalog item '{Name}' into Atlas Server (Graph updated: {Graph})",
                    crd.Metadata.Name, ingestResult.GraphUpdated);
            }
        }

        _logger.LogInformation("[AtlasAgentPipeline] Pipeline finished successfully! Total time: {TotalMs} ms [Component: {Name}, Tier: {Tier}]",
            mapStopwatch.ElapsedMilliseconds + reduceStopwatch.ElapsedMilliseconds, crd.Metadata.Name, crd.Spec.ComponentOverview.Tier);

        return crd;
    }

    private async Task<(List<FileSummary> allSummaries, List<FileSummary> newSummaries)> SummarizeFilesWithRemoteCacheAsync(
        List<ScannedSourceFile> files,
        AtlasAgentPipelineOptions options,
        CancellationToken cancellationToken)
    {
        var summaries = new ConcurrentBag<FileSummary>();
        var newSummaries = new ConcurrentBag<FileSummary>();
        var uncachedFiles = new List<(ScannedSourceFile file, string sha)>();
        var hasRemoteServer = !string.IsNullOrWhiteSpace(options.ServerUrl) && options.ServerClient != null;

        // 1. Build file-to-sha mapping
        var fileShaList = files.Select(f => (file: f, sha: GitBlobShaCalculator.ComputeBlobShaForText(f.Content))).ToList();

        // 2. Query remote cache if available
        Dictionary<string, FileSummary> remoteSummaries = new();
        if (hasRemoteServer && !options.DisableCache)
        {
            var allBlobShas = fileShaList.Select(x => x.sha).Distinct().ToList();
            remoteSummaries = await options.ServerClient!.QueryFileSummariesAsync(options.ServerUrl!, allBlobShas, cancellationToken);
        }

        foreach (var (file, sha) in fileShaList)
        {
            if (remoteSummaries.TryGetValue(sha, out var cachedSummary))
            {
                summaries.Add(cachedSummary);
            }
            else
            {
                uncachedFiles.Add((file, sha));
            }
        }

        // 3. Summarize remaining uncached files with Gemini
        if (uncachedFiles.Count > 0)
        {
            var totalUncached = uncachedFiles.Count;
            var processedCount = 0;
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("[AtlasAgentPipeline] [Map Phase] Summarizing {Count} uncached files with Gemini (Concurrency: {Concurrency})...",
                totalUncached, options.Concurrency);

            // Periodic 10-second Progress Reporting Task
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressTask = Task.Run(async () =>
            {
                while (!progressCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(10000, progressCts.Token);
                        var current = Volatile.Read(ref processedCount);
                        var elapsed = stopwatch.Elapsed;
                        var pct = (double)current / totalUncached * 100.0;
                        var rate = current > 0 ? current / elapsed.TotalSeconds : 0;
                        var remainingFiles = totalUncached - current;
                        var eta = rate > 0 ? TimeSpan.FromSeconds(remainingFiles / rate) : TimeSpan.Zero;

                        _logger.LogInformation("[AtlasAgentPipeline] ⏱️ [Map Phase Progress] {Current}/{Total} files ({Pct:F1}%) | Rate: {Rate:F1} files/sec | Elapsed: {Elapsed:mm\\:ss} | ETA: {Eta:mm\\:ss}",
                            current, totalUncached, pct, rate, elapsed, eta);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, progressCts.Token);

            using var semaphore = new SemaphoreSlim(options.Concurrency, options.Concurrency);
            var tasks = uncachedFiles.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var summary = await _summaryAgent.SummarizeAsync(item.file, item.sha, cancellationToken);
                    summaries.Add(summary);
                    newSummaries.Add(summary);
                }
                finally
                {
                    Interlocked.Increment(ref processedCount);
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            progressCts.Cancel();
            try { await progressTask; } catch { }

            _logger.LogInformation("[AtlasAgentPipeline] 🟢 [Map Phase Completed] Summarized {Count} files in {ElapsedMs} ms ({Rate:F1} files/sec).",
                totalUncached, stopwatch.ElapsedMilliseconds, totalUncached / (stopwatch.Elapsed.TotalSeconds > 0 ? stopwatch.Elapsed.TotalSeconds : 1));
        }

        return (summaries.ToList(), newSummaries.ToList());
    }

    private async Task<AtlasResourceSpec> ExecuteIncrementalPatchAsync(
        AtlasResourceSpec baselineSpec,
        FileDiffResult diff,
        List<FileSummary> changedSummaries,
        CodebaseSkeleton skeleton,
        CancellationToken cancellationToken)
    {
        var addedSb = new StringBuilder();
        var modifiedSb = new StringBuilder();

        var summariesByPath = changedSummaries.ToDictionary(s => s.RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var added in diff.AddedFiles)
        {
            if (summariesByPath.TryGetValue(added, out var s))
            {
                addedSb.AppendLine($"- `{s.RelativePath}`: {s.Purpose}");
            }
            else
            {
                addedSb.AppendLine($"- `{added}`");
            }
        }

        foreach (var mod in diff.ModifiedFiles)
        {
            if (summariesByPath.TryGetValue(mod, out var s))
            {
                modifiedSb.AppendLine($"- `{s.RelativePath}`: {s.Purpose}");
            }
            else
            {
                modifiedSb.AppendLine($"- `{mod}`");
            }
        }

        var deletedSb = new StringBuilder();
        foreach (var del in diff.DeletedFiles)
        {
            deletedSb.AppendLine($"- `{del}`");
        }

        var patchPrompt = Prompts.IncrementalPatchPromptTemplate
            .Replace("{BASELINE_SPEC_JSON}", CrdYamlSerializer.SerializeJson(baselineSpec))
            .Replace("{GIT_COMMIT}", skeleton.Git?.CommitShaShort ?? "unknown")
            .Replace("{ADDED_COUNT}", diff.AddedFiles.Count.ToString())
            .Replace("{ADDED_FILES_SUMMARY}", addedSb.Length > 0 ? addedSb.ToString() : "(None)")
            .Replace("{MODIFIED_COUNT}", diff.ModifiedFiles.Count.ToString())
            .Replace("{MODIFIED_FILES_SUMMARY}", modifiedSb.Length > 0 ? modifiedSb.ToString() : "(None)")
            .Replace("{DELETED_COUNT}", diff.DeletedFiles.Count.ToString())
            .Replace("{DELETED_FILES_SUMMARY}", deletedSb.Length > 0 ? deletedSb.ToString() : "(None)");

        return await _llmClient.GenerateStructuredAsync<AtlasResourceSpec>(
            patchPrompt,
            Prompts.SystemInstruction,
            cancellationToken);
    }

    private async Task ValidateAndRepairDiagramsAsync(
        AtlasResourceSpec spec,
        CodebaseSkeleton skeleton,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var compNames = spec.Architecture.Components.Select(c => c.Name).ToList();

        // 1. Context Diagram
        spec.Architecture.ContextDiagram = await RepairSingleDiagramAsync(
            "C4 Level 1 System Context Diagram (contextDiagram)",
            spec.Architecture.ContextDiagram ?? spec.Architecture.MermaidDiagram,
            compNames,
            "TD",
            maxAttempts,
            cancellationToken);

        // 2. Component Diagram
        spec.Architecture.ComponentDiagram = await RepairSingleDiagramAsync(
            "C4 Level 2 Component Architecture Diagram (componentDiagram)",
            spec.Architecture.ComponentDiagram ?? spec.Architecture.MermaidDiagram,
            compNames,
            "TD",
            maxAttempts,
            cancellationToken);

        // 3. Data Flow Diagram
        spec.Architecture.DataFlowDiagram = await RepairSingleDiagramAsync(
            "Data & Event Ingestion Lifecycle Diagram (dataFlowDiagram)",
            spec.Architecture.DataFlowDiagram ?? spec.Architecture.MermaidDiagram,
            compNames,
            "LR",
            maxAttempts,
            cancellationToken);

        spec.Architecture.MermaidDiagram = spec.Architecture.ComponentDiagram;
    }

    private async Task<string> RepairSingleDiagramAsync(
        string diagramName,
        string rawDiagram,
        List<string> componentNames,
        string fallbackOrientation,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var current = MermaidValidator.Sanitize(rawDiagram);
        var validation = MermaidValidator.Validate(current);

        if (validation.IsValid)
        {
            _logger.LogInformation("[DiagramValidator] {Diagram} is VALID (100% compliant syntax).", diagramName);
            return validation.SanitizedDiagram;
        }

        _logger.LogWarning("[DiagramValidator] {Diagram} syntax validation failed with {Count} errors. Entering iterative repair loop...",
            diagramName, validation.Errors.Count);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogInformation("[DiagramValidator] --- Diagram Repair Attempt {Attempt}/{Max} for {Name} ---",
                attempt, maxAttempts, diagramName);

            var prompt = Prompts.DiagramRepairPromptTemplate
                .Replace("{DIAGRAM_NAME}", diagramName)
                .Replace("{SYNTAX_ERRORS}", string.Join("\n", validation.Errors.Select(e => $"- {e}")))
                .Replace("{BROKEN_DIAGRAM}", current);

            try
            {
                var repaired = await _llmClient.GenerateContentAsync(
                    prompt,
                    "You are a Mermaid syntax repair assistant. Output ONLY valid Mermaid syntax.",
                    enforceJson: false,
                    cancellationToken);

                current = MermaidValidator.Sanitize(repaired);
                validation = MermaidValidator.Validate(current);

                if (validation.IsValid)
                {
                    _logger.LogInformation("[DiagramValidator] Diagram repair SUCCESSFUL on attempt {Attempt} for {Name}!",
                        attempt, diagramName);
                    return validation.SanitizedDiagram;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DiagramValidator] Exception during diagram repair attempt {Attempt}.", attempt);
            }
        }

        _logger.LogError("[DiagramValidator] Failed to repair {Name} after {Max} attempts. Applying clean fallback diagram.",
            diagramName, maxAttempts);

        return MermaidValidator.GenerateFallbackDiagram(diagramName, componentNames, fallbackOrientation);
    }

    private async Task<AtlasResource> ValidateAndRepairCrdAsync(
        AtlasResource crd,
        CodebaseSkeleton skeleton,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var validation = CrdValidator.Validate(crd);
        if (validation.IsValid)
        {
            _logger.LogInformation("[AtlasAgentPipeline] Schema validation PASSED (100% compliant).");
            return crd;
        }

        _logger.LogWarning("[AtlasAgentPipeline] Initial schema validation found {Count} violations. Entering iterative auto-repair loop...",
            validation.Errors.Count);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogInformation("[AtlasAgentPipeline] --- Self-Healing Loop: Attempt {Attempt}/{MaxAttempts} ---", attempt, maxAttempts);
            foreach (var err in validation.Errors)
            {
                _logger.LogWarning("  [Violation] {Error}", err);
            }

            var repairPrompt = Prompts.SchemaRepairPromptTemplate
                .Replace("{VALIDATION_ERRORS}", string.Join("\n", validation.Errors.Select(e => $"- {e}")))
                .Replace("{PREVIOUS_OUTPUT}", CrdYamlSerializer.SerializeJson(crd));

            try
            {
                var repairedSpec = await _llmClient.GenerateStructuredAsync<AtlasResourceSpec>(
                    repairPrompt,
                    Prompts.SystemInstruction,
                    cancellationToken);

                crd = AssembleCrd(repairedSpec, skeleton);
                validation = CrdValidator.Validate(crd);

                if (validation.IsValid)
                {
                    _logger.LogInformation("[AtlasAgentPipeline] Auto-repair SUCCESSFUL on attempt {Attempt}! CRD is now 100% compliant.", attempt);
                    return crd;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AtlasAgentPipeline] Error during repair attempt {Attempt}.", attempt);
            }
        }

        _logger.LogWarning("[AtlasAgentPipeline] Auto-repair completed with remaining non-blocking notices.");
        return crd;
    }

    private static AtlasResource AssembleCrd(AtlasResourceSpec rawSpec, CodebaseSkeleton skeleton)
    {
        var finalSpec = SynthesizeSpec(rawSpec, skeleton);
        var resourceName = NormalizeK8sName(
            skeleton.LocalConfig?.Name
            ?? finalSpec.ComponentOverview.Name
            ?? skeleton.RepoName);

        var crd = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = resourceName,
                Namespace = skeleton.LocalConfig?.Namespace ?? "default",
                Labels = new Dictionary<string, string>
                {
                    ["app.kubernetes.io/name"] = resourceName,
                    ["app.kubernetes.io/part-of"] = (finalSpec.ComponentOverview.Tier ?? "backend").ToLowerInvariant(),
                    ["app.kubernetes.io/managed-by"] = "atlas",
                    ["atlas.io/language"] = (finalSpec.TechStack.PrimaryLanguage ?? "unknown").ToLowerInvariant()
                },
                Annotations = new Dictionary<string, string>
                {
                    ["atlas.io/scanned-at"] = DateTime.UtcNow.ToString("o"),
                    ["atlas.io/generator"] = "Atlas CLI"
                }
            },
            Spec = finalSpec
        };

        if (skeleton.Git != null)
        {
            if (!string.IsNullOrEmpty(skeleton.Git.CommitSha))
                crd.Metadata.Annotations["atlas.io/git-commit"] = skeleton.Git.CommitSha;

            if (!string.IsNullOrEmpty(skeleton.Git.CommitShaShort))
                crd.Metadata.Annotations["atlas.io/git-commit-short"] = skeleton.Git.CommitShaShort;

            if (!string.IsNullOrEmpty(skeleton.Git.Branch))
                crd.Metadata.Annotations["atlas.io/git-branch"] = skeleton.Git.Branch;

            if (!string.IsNullOrEmpty(skeleton.Git.RemoteUrl))
                crd.Metadata.Annotations["atlas.io/git-remote"] = skeleton.Git.RemoteUrl;

            if (!string.IsNullOrEmpty(skeleton.Git.Author))
                crd.Metadata.Annotations["atlas.io/git-author"] = skeleton.Git.Author;
        }

        if (skeleton.LocalConfig?.Labels != null)
        {
            foreach (var (k, v) in skeleton.LocalConfig.Labels)
            {
                crd.Metadata.Labels[k] = v;
            }
        }

        if (skeleton.LocalConfig?.Annotations != null)
        {
            foreach (var (k, v) in skeleton.LocalConfig.Annotations)
            {
                crd.Metadata.Annotations[k] = v;
            }
        }

        return crd;
    }

    private static string BuildReducePrompt(CodebaseSkeleton skeleton, List<FileSummary> summaries)
    {
        var extSummary = string.Join(", ", skeleton.ExtensionCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ({kv.Value})"));

        var manifestsSb = new StringBuilder();
        foreach (var m in skeleton.Manifests)
        {
            manifestsSb.AppendLine($"- {m.ManifestType} at `{m.RelativePath}` (Runtime: {m.TargetRuntime ?? "N/A"})");
            if (m.ExtractedPackages.Count > 0)
            {
                manifestsSb.AppendLine($"  Packages: {string.Join(", ", m.ExtractedPackages.Select(p => $"{p.Name} {p.Version}".Trim()))}");
            }
            if (m.ExposedPorts.Count > 0)
            {
                manifestsSb.AppendLine($"  Exposed Ports: {string.Join(", ", m.ExposedPorts)}");
            }
            if (m.EnvironmentVariables.Count > 0)
            {
                manifestsSb.AppendLine($"  Env Vars: {string.Join(", ", m.EnvironmentVariables)}");
            }
        }

        var summariesSb = new StringBuilder();

        if (summaries.Count <= 100)
        {
            foreach (var s in summaries)
            {
                AppendSingleFileSummary(summariesSb, s);
            }
        }
        else
        {
            // Large codebase hierarchical aggregation by domain / directory module
            var groups = summaries
                .GroupBy(s =>
                {
                    var normalized = s.RelativePath.Replace('\\', '/');
                    var slashIdx = normalized.IndexOf('/');
                    return slashIdx > 0 ? normalized.Substring(0, slashIdx) : "root";
                })
                .OrderByDescending(g => g.Count())
                .ToList();

            summariesSb.AppendLine($"## Hierarchical Domain & Subsystem Decomposition ({summaries.Count} files across {groups.Count} modules)");
            summariesSb.AppendLine();

            foreach (var group in groups)
            {
                var moduleFiles = group.ToList();
                var categories = moduleFiles.GroupBy(f => f.Category).Select(c => $"{c.Key}: {c.Count()}").ToList();
                var distinctEndpoints = moduleFiles.SelectMany(f => f.EndpointsOrRoutes).Distinct().Take(25).ToList();
                var distinctAbstractions = moduleFiles.SelectMany(f => f.PrimaryAbstractions).Distinct().Take(30).ToList();
                var distinctDependencies = moduleFiles.SelectMany(f => f.KeyDependencies).Distinct().Take(25).ToList();
                var distinctRules = moduleFiles.SelectMany(f => f.BusinessLogicAndInvariants).Distinct().Take(12).ToList();
                var distinctMutations = moduleFiles.SelectMany(f => f.StateMutationsAndEvents).Distinct().Take(12).ToList();
                var distinctSmells = moduleFiles.SelectMany(f => f.SecurityAndQualityNotes).Distinct().Take(12).ToList();

                summariesSb.AppendLine($"### Module / Domain: `{group.Key}` ({moduleFiles.Count} files)");
                summariesSb.AppendLine($"- **Component Categories**: {string.Join(", ", categories)}");
                if (distinctAbstractions.Count > 0)
                    summariesSb.AppendLine($"- **Primary Abstractions & Schemas**: {string.Join(", ", distinctAbstractions)}");
                if (distinctEndpoints.Count > 0)
                    summariesSb.AppendLine($"- **Endpoints / Routes / Entrypoints**: {string.Join(", ", distinctEndpoints)}");
                if (distinctDependencies.Count > 0)
                    summariesSb.AppendLine($"- **Key Dependencies & Data Stores**: {string.Join(", ", distinctDependencies)}");
                if (distinctRules.Count > 0)
                    summariesSb.AppendLine($"- **Key Business Invariants**: {string.Join("; ", distinctRules)}");
                if (distinctMutations.Count > 0)
                    summariesSb.AppendLine($"- **State Mutations & Events**: {string.Join("; ", distinctMutations)}");
                if (distinctSmells.Count > 0)
                    summariesSb.AppendLine($"- **Security & Code Smells**: {string.Join("; ", distinctSmells)}");

                // Sample top 12 representative files per module (e.g. highest complexity / entry points)
                var topFiles = moduleFiles
                    .OrderByDescending(f => f.PrimaryAbstractions.Count + f.EndpointsOrRoutes.Count + f.BusinessLogicAndInvariants.Count)
                    .Take(12)
                    .ToList();

                summariesSb.AppendLine($"- **Representative Sample Procedures / Files in `{group.Key}`**:");
                foreach (var top in topFiles)
                {
                    summariesSb.AppendLine($"  * `{top.RelativePath}` ({top.Category}): {top.Purpose}");
                }
                summariesSb.AppendLine();
            }
        }

        var readmeSnippet = !string.IsNullOrWhiteSpace(skeleton.ReadmeContent)
            ? (skeleton.ReadmeContent.Length > 15000 ? skeleton.ReadmeContent.Substring(0, 15000) + "\n...[truncated]..." : skeleton.ReadmeContent)
            : "(No README.md found)";

        return Prompts.ExtractionPromptTemplate
            .Replace("{REPO_NAME}", skeleton.RepoName)
            .Replace("{TOTAL_FILES}", skeleton.TotalFiles.ToString())
            .Replace("{EXTENSIONS_SUMMARY}", string.IsNullOrWhiteSpace(extSummary) ? "None" : extSummary)
            .Replace("{GIT_BRANCH}", skeleton.Git?.Branch ?? "unknown")
            .Replace("{GIT_COMMIT}", skeleton.Git?.CommitShaShort ?? "unknown")
            .Replace("{GIT_REMOTE}", skeleton.Git?.RemoteUrl ?? "unknown")
            .Replace("{MANIFESTS_SUMMARY}", manifestsSb.Length > 0 ? manifestsSb.ToString() : "(No manifests discovered)")
            .Replace("{README_SNIPPET}", readmeSnippet)
            .Replace("{SOURCE_FILES_SNIPPET}", summariesSb.Length > 0 ? summariesSb.ToString() : "(No file summaries available)");
    }

    private static void AppendSingleFileSummary(StringBuilder sb, FileSummary s)
    {
        sb.AppendLine($"### `{s.RelativePath}` ({s.Category})");
        sb.AppendLine($"- **Purpose**: {s.Purpose}");
        if (s.PrimaryAbstractions.Count > 0)
            sb.AppendLine($"- **Abstractions**: {string.Join(", ", s.PrimaryAbstractions)}");
        if (s.EndpointsOrRoutes.Count > 0)
            sb.AppendLine($"- **Endpoints/Routes**: {string.Join(", ", s.EndpointsOrRoutes)}");
        if (s.KeyDependencies.Count > 0)
            sb.AppendLine($"- **Dependencies**: {string.Join(", ", s.KeyDependencies)}");
        if (s.ConfigsOrEnvVars.Count > 0)
            sb.AppendLine($"- **Configs/Env**: {string.Join(", ", s.ConfigsOrEnvVars)}");
        if (s.BusinessLogicAndInvariants.Count > 0)
            sb.AppendLine($"- **Business Logic & Rules**: {string.Join("; ", s.BusinessLogicAndInvariants)}");
        if (s.InputOutputContracts.Count > 0)
            sb.AppendLine($"- **Data Contracts & Schemas**: {string.Join("; ", s.InputOutputContracts)}");
        if (s.ErrorAndExceptionHandling.Count > 0)
            sb.AppendLine($"- **Exception Handling & Fallbacks**: {string.Join("; ", s.ErrorAndExceptionHandling)}");
        if (s.StateMutationsAndEvents.Count > 0)
            sb.AppendLine($"- **State Mutations & Events**: {string.Join("; ", s.StateMutationsAndEvents)}");
        if (s.SecurityAndQualityNotes.Count > 0)
            sb.AppendLine($"- **Security & Code Smells**: {string.Join("; ", s.SecurityAndQualityNotes)}");
        sb.AppendLine();
    }

    private static AtlasResourceSpec SynthesizeSpec(AtlasResourceSpec spec, CodebaseSkeleton skeleton)
    {
        if (string.IsNullOrWhiteSpace(spec.ComponentOverview.RepositoryUrl) && !string.IsNullOrWhiteSpace(skeleton.Git?.RemoteUrl))
        {
            spec.ComponentOverview.RepositoryUrl = skeleton.Git.RemoteUrl;
        }

        if (string.IsNullOrWhiteSpace(spec.ComponentOverview.Name))
        {
            spec.ComponentOverview.Name = skeleton.RepoName;
        }

        var existingPackageNames = new HashSet<string>(
            spec.Dependencies.KeyPackages.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var m in skeleton.Manifests)
        {
            foreach (var pkg in m.ExtractedPackages)
            {
                if (!existingPackageNames.Contains(pkg.Name))
                {
                    spec.Dependencies.KeyPackages.Add(pkg);
                    existingPackageNames.Add(pkg.Name);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(skeleton.LocalConfig?.Tier))
        {
            spec.ComponentOverview.Tier = skeleton.LocalConfig.Tier;
        }
        if (!string.IsNullOrWhiteSpace(skeleton.LocalConfig?.Owner))
        {
            spec.ComponentOverview.Owner = skeleton.LocalConfig.Owner;
        }

        return spec;
    }

    public static string NormalizeK8sName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "atlas-component";

        var clean = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9\-\.]", "-");
        clean = Regex.Replace(clean, @"\-+", "-").Trim('-');

        if (clean.Length > 63)
        {
            clean = clean.Substring(0, 63).TrimEnd('-');
        }

        return string.IsNullOrEmpty(clean) ? "atlas-component" : clean;
    }
}
