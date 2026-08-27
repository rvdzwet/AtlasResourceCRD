using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtlasResourceCRD.Core.Models;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Caching;

public sealed class SynthesisCacheEntry
{
    [JsonPropertyName("gitCommit")]
    public string GitCommit { get; set; } = string.Empty;

    [JsonPropertyName("gitBranch")]
    public string GitBranch { get; set; } = string.Empty;

    [JsonPropertyName("fileShaMap")]
    public Dictionary<string, string> FileShaMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("resource")]
    public AtlasResource Resource { get; set; } = new();

    [JsonPropertyName("cachedHtml")]
    public string CachedHtml { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class FileDiffResult
{
    public List<string> AddedFiles { get; set; } = new();
    public List<string> ModifiedFiles { get; set; } = new();
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> UnchangedFiles { get; set; } = new();

    public bool HasChanges => AddedFiles.Count > 0 || ModifiedFiles.Count > 0 || DeletedFiles.Count > 0;
    public int TotalChangedCount => AddedFiles.Count + ModifiedFiles.Count + DeletedFiles.Count;
}

public sealed class SynthesisCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<SynthesisCache> _logger;
    private readonly bool _disabled;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SynthesisCache(string repoRoot, ILogger<SynthesisCache> logger, bool disabled = false, string? customCacheDir = null)
    {
        _logger = logger;
        _disabled = disabled;
        _cacheDirectory = customCacheDir ?? Path.Combine(repoRoot, ".atlas", "cache", "synth");

        if (!_disabled && !Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public SynthesisCacheEntry? TryGetExactMatch(string gitCommit, Dictionary<string, string> currentFileShas)
    {
        if (_disabled) return null;

        var latest = GetLatest();
        if (latest == null) return null;

        // Check if git commit matches
        var commitMatches = !string.IsNullOrWhiteSpace(gitCommit) &&
                            string.Equals(latest.GitCommit, gitCommit, StringComparison.OrdinalIgnoreCase);

        // Check if all file SHAs match exactly
        var diff = ComputeDiff(currentFileShas, latest.FileShaMap);
        if (!diff.HasChanges && (commitMatches || string.IsNullOrWhiteSpace(gitCommit)))
        {
            _logger.LogInformation("[SynthesisCache] 100% Exact Cache HIT for commit {Commit} ({Count} files identical).",
                latest.GitCommit, currentFileShas.Count);
            return latest;
        }

        return null;
    }

    public SynthesisCacheEntry? GetLatest()
    {
        if (_disabled) return null;

        var latestFile = Path.Combine(_cacheDirectory, "latest.json");
        if (!File.Exists(latestFile)) return null;

        try
        {
            var json = File.ReadAllText(latestFile);
            return JsonSerializer.Deserialize<SynthesisCacheEntry>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SynthesisCache] Failed to read latest synthesis cache entry.");
            return null;
        }
    }

    public static FileDiffResult ComputeDiff(Dictionary<string, string> currentShas, Dictionary<string, string> cachedShas)
    {
        var result = new FileDiffResult();

        // 1. Check added and modified
        foreach (var (path, currentSha) in currentShas)
        {
            if (!cachedShas.TryGetValue(path, out var cachedSha))
            {
                result.AddedFiles.Add(path);
            }
            else if (!string.Equals(currentSha, cachedSha, StringComparison.OrdinalIgnoreCase))
            {
                result.ModifiedFiles.Add(path);
            }
            else
            {
                result.UnchangedFiles.Add(path);
            }
        }

        // 2. Check deleted
        foreach (var (path, _) in cachedShas)
        {
            if (!currentShas.ContainsKey(path))
            {
                result.DeletedFiles.Add(path);
            }
        }

        return result;
    }

    public void Store(SynthesisCacheEntry entry)
    {
        if (_disabled) return;

        try
        {
            var json = JsonSerializer.Serialize(entry, JsonOptions);

            // Store latest.json
            var latestFile = Path.Combine(_cacheDirectory, "latest.json");
            File.WriteAllText(latestFile, json);

            // Store commit-specific file if commit is known
            if (!string.IsNullOrWhiteSpace(entry.GitCommit) && !entry.GitCommit.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                var commitFile = Path.Combine(_cacheDirectory, $"{entry.GitCommit}.json");
                File.WriteAllText(commitFile, json);
            }

            _logger.LogInformation("[SynthesisCache] Successfully cached synthesis state for commit {Commit} ({Count} files).",
                entry.GitCommit, entry.FileShaMap.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SynthesisCache] Failed to store synthesis cache.");
        }
    }

    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, true);
                Directory.CreateDirectory(_cacheDirectory);
                _logger.LogInformation("[SynthesisCache] Cleared synthesis cache directory: {Dir}", _cacheDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SynthesisCache] Failed to clear synthesis cache directory.");
        }
    }
}
