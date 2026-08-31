using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Atlas.Core.Caching;
using Atlas.Core.Gemini;
using Atlas.Core.Models;
using Atlas.Server.Graph;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Storage;

/// <summary>
/// 100% Stateless Neo4j-backed Remote Cache Repository.
/// All Git blob file summaries and synthesis snapshots are persisted directly as nodes in Neo4j (zero disk files).
/// </summary>
public sealed class ServerCacheRepository
{
    private readonly Neo4jGraphService _graphService;
    private readonly ILogger<ServerCacheRepository> _logger;

    public ServerCacheRepository(Neo4jGraphService graphService, ILogger<ServerCacheRepository> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public SynthesisCheckResponse CheckSynthesis(string repoName, string commitSha, Dictionary<string, string> fileShas)
    {
        if (string.IsNullOrWhiteSpace(commitSha) || fileShas == null || fileShas.Count == 0)
        {
            return new SynthesisCheckResponse { IsExactMatch = false };
        }

        try
        {
            var cypher = """
                MATCH (c:SynthesisCache { repo: $repoName, commitSha: $commitSha })
                RETURN c.rawJson AS rawJson
                LIMIT 1
                """;

            var records = _graphService.QueryAsync(cypher, new { repoName, commitSha }).GetAwaiter().GetResult();
            if (records.Count > 0 && records[0].TryGetValue("rawJson", out var rawJsonObj) && rawJsonObj is string rawJson && !string.IsNullOrWhiteSpace(rawJson))
            {
                var entry = JsonSerializer.Deserialize<SynthesisCacheEntry>(rawJson, GeminiClient.JsonOptions);
                if (entry != null && entry.FileShaMap != null)
                {
                    bool allMatch = fileShas.Count == entry.FileShaMap.Count;
                    if (allMatch)
                    {
                        foreach (var kvp in fileShas)
                        {
                            if (!entry.FileShaMap.TryGetValue(kvp.Key, out var cachedSha) ||
                                !string.Equals(kvp.Value, cachedSha, StringComparison.OrdinalIgnoreCase))
                            {
                                allMatch = false;
                                break;
                            }
                        }

                        if (allMatch)
                        {
                            _logger.LogInformation("[ServerCacheRepository] ⚡ Exact synthesis cache hit in Neo4j for {Repo} at commit {Commit}", repoName, commitSha);
                            return new SynthesisCheckResponse
                            {
                                IsExactMatch = true,
                                CachedResource = entry.Resource
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ServerCacheRepository] Error checking synthesis cache in Neo4j for {Repo}", repoName);
        }

        var missingShas = GetMissingShas(fileShas.Values);
        return new SynthesisCheckResponse
        {
            IsExactMatch = false,
            MissingBlobShas = missingShas
        };
    }

    public List<FileSummary> GetFileSummaries(IEnumerable<string> blobShas)
    {
        var results = new List<FileSummary>();
        var shaList = blobShas.Distinct().ToList();
        if (shaList.Count == 0) return results;

        try
        {
            var cypher = """
                MATCH (f:FileSummaryCache)
                WHERE f.sha IN $shaList
                RETURN f.sha AS sha, f.json AS json
                """;

            var records = _graphService.QueryAsync(cypher, new { shaList }).GetAwaiter().GetResult();
            foreach (var r in records)
            {
                if (r.TryGetValue("json", out var jsonObj) && jsonObj is string json && !string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var summary = JsonSerializer.Deserialize<FileSummary>(json, GeminiClient.JsonOptions);
                        if (summary != null) results.Add(summary);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerCacheRepository] Error retrieving file summaries from Neo4j");
        }

        return results;
    }

    public List<FileSummary> QueryFileSummaries(IEnumerable<string> blobShas) => GetFileSummaries(blobShas);

    public void StoreFileSummaries(IEnumerable<FileSummary> summaries)
    {
        var list = summaries.ToList();
        if (list.Count == 0) return;

        try
        {
            var items = list.Select(s => new
            {
                sha = s.GitBlobSha ?? "",
                relativePath = s.RelativePath ?? "",
                category = s.Category ?? "Source",
                json = JsonSerializer.Serialize(s, GeminiClient.JsonOptions)
            }).Where(x => !string.IsNullOrWhiteSpace(x.sha)).ToList();

            if (items.Count == 0) return;

            var cypher = """
                UNWIND $items AS item
                MERGE (f:FileSummaryCache { sha: item.sha })
                SET f.relativePath = item.relativePath,
                    f.category = item.category,
                    f.json = item.json,
                    f.updatedAt = datetime()
                """;

            _graphService.ExecuteCypherAsync(cypher, new { items }).GetAwaiter().GetResult();
            _logger.LogInformation("[ServerCacheRepository] Stored {Count} file summaries in Neo4j FileSummaryCache", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerCacheRepository] Error storing file summaries in Neo4j");
        }
    }

    public void StoreSynthesis(string repoName, string commitSha, Dictionary<string, string> fileShas, AtlasResource resource)
    {
        if (string.IsNullOrWhiteSpace(commitSha)) return;

        try
        {
            var entry = new SynthesisCacheEntry
            {
                GitCommit = commitSha,
                FileShaMap = fileShas,
                Resource = resource,
                GeneratedAt = DateTime.UtcNow
            };

            var rawJson = JsonSerializer.Serialize(entry, GeminiClient.JsonOptions);
            var cypher = """
                MERGE (c:SynthesisCache { repo: $repoName, commitSha: $commitSha })
                SET c.rawJson = $rawJson,
                    c.updatedAt = datetime()
                """;

            _graphService.ExecuteCypherAsync(cypher, new { repoName, commitSha, rawJson }).GetAwaiter().GetResult();
            _logger.LogInformation("[ServerCacheRepository] Stored synthesis snapshot in Neo4j for {Repo} ({Commit})", repoName, commitSha);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerCacheRepository] Error storing synthesis cache in Neo4j for {Repo}", repoName);
        }
    }

    public List<string> GetMissingShas(IEnumerable<string> candidateShas)
    {
        var unique = candidateShas.Distinct().ToList();
        if (unique.Count == 0) return new List<string>();

        try
        {
            var cypher = """
                MATCH (f:FileSummaryCache)
                WHERE f.sha IN $unique
                RETURN f.sha AS sha
                """;

            var records = _graphService.QueryAsync(cypher, new { unique }).GetAwaiter().GetResult();
            var existing = new HashSet<string>(records.Select(r => r.GetValueOrDefault("sha")?.ToString() ?? ""), StringComparer.OrdinalIgnoreCase);

            return unique.Where(sha => !existing.Contains(sha)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerCacheRepository] Error checking missing SHAs in Neo4j");
            return unique;
        }
    }

    public void DeleteServiceCache(string serviceName)
    {
        try
        {
            var cypher = "MATCH (c:SynthesisCache { repo: $serviceName }) DETACH DELETE c";
            _graphService.ExecuteCypherAsync(cypher, new { serviceName }).GetAwaiter().GetResult();
            _logger.LogInformation("[ServerCacheRepository] Deleted synthesis cache in Neo4j for service '{Service}'", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerCacheRepository] Error deleting synthesis cache for '{Service}' in Neo4j", serviceName);
        }
    }

    public void Clear()
    {
        try
        {
            _graphService.ExecuteCypherAsync("MATCH (f:FileSummaryCache) DETACH DELETE f").GetAwaiter().GetResult();
            _graphService.ExecuteCypherAsync("MATCH (c:SynthesisCache) DETACH DELETE c").GetAwaiter().GetResult();
            _logger.LogInformation("[ServerCacheRepository] Cleared all Neo4j cache nodes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServerCacheRepository] Error clearing Neo4j cache nodes");
        }
    }
}
