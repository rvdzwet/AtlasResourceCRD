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
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Scanner;
using AtlasResourceCRD.Core.Serialization;
using AtlasResourceCRD.Core.Validation;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Agents;

public sealed class AtlasAgentPipelineOptions
{
    public int Concurrency { get; set; } = 8;
    public bool DisableCache { get; set; }
    public string? CustomCacheDir { get; set; }
    public int MaxValidationRepairAttempts { get; set; } = 2;
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

        var cache = new FileSummaryCache(
            skeleton.RootPath,
            _loggerFactory.CreateLogger<FileSummaryCache>(),
            disabled: options.DisableCache,
            customCacheDir: options.CustomCacheDir);

        // ==========================================
        // 1. MAP PHASE: Parallel File Summarization
        // ==========================================
        var mapStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[AtlasAgentPipeline] --- Phase 1: MAP (Summarizing {Count} key files with Git Blob SHA caching) ---",
            skeleton.HighValueFiles.Count);

        var fileSummaries = new ConcurrentBag<FileSummary>();
        var uncachedFiles = new List<(ScannedSourceFile file, string sha)>();

        foreach (var file in skeleton.HighValueFiles)
        {
            var sha = GitBlobShaCalculator.ComputeBlobShaForText(file.Content);
            var cached = cache.TryGet(sha);
            if (cached != null)
            {
                fileSummaries.Add(cached);
            }
            else
            {
                uncachedFiles.Add((file, sha));
            }
        }

        _logger.LogInformation("[AtlasAgentPipeline] Cache status: {Hits} Hits, {Misses} Misses (Processing {Uncached} files via LLM)",
            cache.CacheHits, cache.CacheMisses, uncachedFiles.Count);

        if (uncachedFiles.Count > 0)
        {
            using var semaphore = new SemaphoreSlim(options.Concurrency, options.Concurrency);
            var tasks = uncachedFiles.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var summary = await _summaryAgent.SummarizeAsync(item.file, item.sha, cancellationToken);
                    cache.Store(summary);
                    fileSummaries.Add(summary);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        mapStopwatch.Stop();
        _logger.LogInformation("[AtlasAgentPipeline] Map Phase completed in {ElapsedMs} ms. (Cache Hit Ratio: {Ratio:P0})",
            mapStopwatch.ElapsedMilliseconds,
            skeleton.HighValueFiles.Count > 0 ? (double)cache.CacheHits / skeleton.HighValueFiles.Count : 1.0);

        // ==========================================
        // 2. REDUCE PHASE: Global Architect Synthesis
        // ==========================================
        var reduceStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[AtlasAgentPipeline] --- Phase 2: REDUCE (Synthesizing Multi-Diagram Architecture with High Thinking) ---");

        var prompt = BuildReducePrompt(skeleton, fileSummaries.OrderBy(f => f.RelativePath).ToList());
        _logger.LogDebug("[AtlasAgentPipeline] Reduce prompt constructed ({Length} chars). Invoking Gemini...", prompt.Length);

        var extractedSpec = await _geminiClient.GenerateStructuredAsync<AtlasResourceSpec>(
            prompt,
            Prompts.SystemInstruction,
            cancellationToken);

        reduceStopwatch.Stop();
        _logger.LogInformation("[AtlasAgentPipeline] Reduce Phase completed in {ElapsedMs} ms.", reduceStopwatch.ElapsedMilliseconds);

        // ==========================================
        // 3. BUILD INITIAL CRD INSTANCE
        // ==========================================
        var crd = AssembleCrd(extractedSpec, skeleton);

        // ==========================================
        // 4. ITERATIVE SCHEMA VALIDATION & AUTO-REPAIR
        // ==========================================
        crd = await ValidateAndRepairCrdAsync(crd, skeleton, options.MaxValidationRepairAttempts, cancellationToken);

        _logger.LogInformation("[AtlasAgentPipeline] Pipeline finished successfully! Total time: {TotalMs} ms [Component: {Name}, Tier: {Tier}]",
            mapStopwatch.ElapsedMilliseconds + reduceStopwatch.ElapsedMilliseconds, crd.Metadata.Name, crd.Spec.ComponentOverview.Tier);

        return crd;
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
                manifestsSb.AppendLine($"  Packages: {string.Join(", ", m.ExtractedPackages.Take(25).Select(p => $"{p.Name} {p.Version}".Trim()))}");
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
            ? (skeleton.ReadmeContent.Length > 3500 ? skeleton.ReadmeContent.Substring(0, 3500) + "\n...[truncated]..." : skeleton.ReadmeContent)
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
