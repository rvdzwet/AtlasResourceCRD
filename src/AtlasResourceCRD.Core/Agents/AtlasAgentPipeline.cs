using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AtlasResourceCRD.Core.Caching;
using AtlasResourceCRD.Core.Gemini;
using AtlasResourceCRD.Core.Html;
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Scanner;
using AtlasResourceCRD.Core.Serialization;
using AtlasResourceCRD.Core.Validation;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Agents;

public sealed class AtlasAgentPipelineOptions
{
    public int Concurrency { get; set; } = 16;
    public bool DisableCache { get; set; }
    public bool ForceSynth { get; set; }
    public string? CustomCacheDir { get; set; }
    public int MaxValidationRepairAttempts { get; set; } = 2;
    public int MaxDiagramRepairAttempts { get; set; } = 3;
}

public sealed class AtlasAgentPipeline
{
    private readonly GeminiClient _geminiClient;
    private readonly FileSummaryAgent _summaryAgent;
    private readonly ILogger<AtlasAgentPipeline> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AtlasAgentPipeline(
        GeminiClient geminiClient,
        FileSummaryAgent summaryAgent,
        ILogger<AtlasAgentPipeline> logger,
        ILoggerFactory loggerFactory)
    {
        _geminiClient = geminiClient;
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

        var fileCache = new FileSummaryCache(
            skeleton.RootPath,
            _loggerFactory.CreateLogger<FileSummaryCache>(),
            disabled: options.DisableCache,
            customCacheDir: options.CustomCacheDir);

        var synthCache = new SynthesisCache(
            skeleton.RootPath,
            _loggerFactory.CreateLogger<SynthesisCache>(),
            disabled: options.DisableCache,
            customCacheDir: options.CustomCacheDir);

        // Compute current file SHAs
        var currentFileShas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in skeleton.HighValueFiles)
        {
            var sha = GitBlobShaCalculator.ComputeBlobShaForText(file.Content);
            currentFileShas[file.RelativePath] = sha;
        }

        var gitCommit = skeleton.Git?.CommitSha ?? "unknown";

        // =========================================================================
        // 0. CHECK SYNTHESIS CACHE (100% INSTANT HIT / ZERO TOKENS)
        // =========================================================================
        if (!options.DisableCache && !options.ForceSynth)
        {
            var exactMatch = synthCache.TryGetExactMatch(gitCommit, currentFileShas);
            if (exactMatch != null)
            {
                _logger.LogInformation("[AtlasAgentPipeline] ⚡ 100% Instant Synthesis Cache HIT for commit {Commit} (0 LLM tokens, 100% idempotent).",
                    gitCommit);
                return exactMatch.Resource;
            }
        }

        // =========================================================================
        // 1. CHECK FOR INCREMENTAL DIFF PATCHING vs FULL SYNTHESIS
        // =========================================================================
        var previousEntry = (!options.DisableCache && !options.ForceSynth) ? synthCache.GetLatest() : null;
        AtlasResourceSpec extractedSpec;
        var mapStopwatch = Stopwatch.StartNew();
        var reduceStopwatch = new Stopwatch();

        if (previousEntry != null && previousEntry.FileShaMap.Count > 0)
        {
            var diff = SynthesisCache.ComputeDiff(currentFileShas, previousEntry.FileShaMap);
            _logger.LogInformation("[AtlasAgentPipeline] Incremental Diff Detected: {Added} Added, {Modified} Modified, {Deleted} Deleted, {Unchanged} Unchanged.",
                diff.AddedFiles.Count, diff.ModifiedFiles.Count, diff.DeletedFiles.Count, diff.UnchangedFiles.Count);

            // Phase 1: Map changed files only
            var changedFilesMap = skeleton.HighValueFiles
                .Where(f => diff.AddedFiles.Contains(f.RelativePath, StringComparer.OrdinalIgnoreCase) ||
                            diff.ModifiedFiles.Contains(f.RelativePath, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var changedSummaries = await SummarizeFilesAsync(changedFilesMap, fileCache, options.Concurrency, cancellationToken);
            mapStopwatch.Stop();
            _logger.LogInformation("[AtlasAgentPipeline] Incremental Map completed in {ElapsedMs} ms ({Count} changed files analyzed).",
                mapStopwatch.ElapsedMilliseconds, changedFilesMap.Count);

            // Phase 2: Incremental Patch Prompt
            reduceStopwatch.Start();
            _logger.LogInformation("[AtlasAgentPipeline] --- Phase 2: INCREMENTAL PATCH (Applying stable delta patch to previous specification) ---");

            extractedSpec = await ExecuteIncrementalPatchAsync(
                previousEntry.Resource.Spec,
                diff,
                changedSummaries,
                skeleton,
                cancellationToken);

            reduceStopwatch.Stop();
            _logger.LogInformation("[AtlasAgentPipeline] Incremental Patch Phase completed in {ElapsedMs} ms.", reduceStopwatch.ElapsedMilliseconds);
        }
        else
        {
            // Full Map Phase
            _logger.LogInformation("[AtlasAgentPipeline] --- Phase 1: FULL MAP (Summarizing {Count} key files with Git Blob SHA caching) ---",
                skeleton.HighValueFiles.Count);

            var allSummaries = await SummarizeFilesAsync(skeleton.HighValueFiles, fileCache, options.Concurrency, cancellationToken);
            mapStopwatch.Stop();
            _logger.LogInformation("[AtlasAgentPipeline] Full Map Phase completed in {ElapsedMs} ms. (Cache Hit Ratio: {Ratio:P0})",
                mapStopwatch.ElapsedMilliseconds,
                skeleton.HighValueFiles.Count > 0 ? (double)fileCache.CacheHits / skeleton.HighValueFiles.Count : 1.0);

            // Full Reduce Phase
            reduceStopwatch.Start();
            _logger.LogInformation("[AtlasAgentPipeline] --- Phase 2: FULL REDUCE (Synthesizing Multi-Diagram Architecture with High Thinking) ---");

            var prompt = BuildReducePrompt(skeleton, allSummaries.OrderBy(f => f.RelativePath).ToList());
            _logger.LogDebug("[AtlasAgentPipeline] Reduce prompt constructed ({Length} chars). Invoking Gemini...", prompt.Length);

            extractedSpec = await _geminiClient.GenerateStructuredAsync<AtlasResourceSpec>(
                prompt,
                Prompts.SystemInstruction,
                cancellationToken);

            reduceStopwatch.Stop();
            _logger.LogInformation("[AtlasAgentPipeline] Full Reduce Phase completed in {ElapsedMs} ms.", reduceStopwatch.ElapsedMilliseconds);
        }

        // =========================================================================
        // 3. ITERATIVE DIAGRAM VALIDATION & AUTO-REPAIR LOOP
        // =========================================================================
        await ValidateAndRepairDiagramsAsync(extractedSpec, skeleton, options.MaxDiagramRepairAttempts, cancellationToken);

        // =========================================================================
        // 4. BUILD INITIAL CRD INSTANCE & SCHEMA REPAIR
        // =========================================================================
        var crd = AssembleCrd(extractedSpec, skeleton);
        crd = await ValidateAndRepairCrdAsync(crd, skeleton, options.MaxValidationRepairAttempts, cancellationToken);

        // =========================================================================
        // 5. CACHE SYNTHESIS & PRE-RENDERED HTML ARTIFACTS
        // =========================================================================
        if (!options.DisableCache)
        {
            var renderedHtml = HtmlVisualizerGenerator.Generate(crd);
            synthCache.Store(new SynthesisCacheEntry
            {
                GitCommit = gitCommit,
                GitBranch = skeleton.Git?.Branch ?? "unknown",
                FileShaMap = currentFileShas,
                Resource = crd,
                CachedHtml = renderedHtml,
                GeneratedAt = DateTime.UtcNow
            });
        }

        _logger.LogInformation("[AtlasAgentPipeline] Pipeline finished successfully! Total time: {TotalMs} ms [Component: {Name}, Tier: {Tier}]",
            mapStopwatch.ElapsedMilliseconds + reduceStopwatch.ElapsedMilliseconds, crd.Metadata.Name, crd.Spec.ComponentOverview.Tier);

        return crd;
    }

    private async Task<List<FileSummary>> SummarizeFilesAsync(
        List<ScannedSourceFile> files,
        FileSummaryCache cache,
        int concurrency,
        CancellationToken cancellationToken)
    {
        var summaries = new ConcurrentBag<FileSummary>();
        var uncachedFiles = new List<(ScannedSourceFile file, string sha)>();

        foreach (var file in files)
        {
            var sha = GitBlobShaCalculator.ComputeBlobShaForText(file.Content);
            var cached = cache.TryGet(sha);
            if (cached != null)
            {
                summaries.Add(cached);
            }
            else
            {
                uncachedFiles.Add((file, sha));
            }
        }

        if (uncachedFiles.Count > 0)
        {
            using var semaphore = new SemaphoreSlim(concurrency, concurrency);
            var tasks = uncachedFiles.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var summary = await _summaryAgent.SummarizeAsync(item.file, item.sha, cancellationToken);
                    cache.Store(summary);
                    summaries.Add(summary);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        return summaries.ToList();
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

        return await _geminiClient.GenerateStructuredAsync<AtlasResourceSpec>(
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
                var repaired = await _geminiClient.GenerateContentAsync(
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
                var repairedSpec = await _geminiClient.GenerateStructuredAsync<AtlasResourceSpec>(
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
                    ["atlas.io/generator"] = "AtlasResourceCRD CLI"
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
        foreach (var s in summaries)
        {
            summariesSb.AppendLine($"### `{s.RelativePath}` ({s.Category})");
            summariesSb.AppendLine($"- **Purpose**: {s.Purpose}");
            if (s.PrimaryAbstractions.Count > 0)
                summariesSb.AppendLine($"- **Abstractions**: {string.Join(", ", s.PrimaryAbstractions)}");
            if (s.EndpointsOrRoutes.Count > 0)
                summariesSb.AppendLine($"- **Endpoints/Routes**: {string.Join(", ", s.EndpointsOrRoutes)}");
            if (s.KeyDependencies.Count > 0)
                summariesSb.AppendLine($"- **Dependencies**: {string.Join(", ", s.KeyDependencies)}");
            if (s.ConfigsOrEnvVars.Count > 0)
                summariesSb.AppendLine($"- **Configs/Env**: {string.Join(", ", s.ConfigsOrEnvVars)}");
            summariesSb.AppendLine();
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
