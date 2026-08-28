using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Caching;

public sealed class FileSummary
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("gitBlobSha")]
    public string GitBlobSha { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("primaryAbstractions")]
    public List<string> PrimaryAbstractions { get; set; } = new();

    [JsonPropertyName("keyDependencies")]
    public List<string> KeyDependencies { get; set; } = new();

    [JsonPropertyName("endpointsOrRoutes")]
    public List<string> EndpointsOrRoutes { get; set; } = new();

    [JsonPropertyName("configsOrEnvVars")]
    public List<string> ConfigsOrEnvVars { get; set; } = new();

    [JsonPropertyName("businessLogicAndInvariants")]
    public List<string> BusinessLogicAndInvariants { get; set; } = new();

    [JsonPropertyName("inputOutputContracts")]
    public List<string> InputOutputContracts { get; set; } = new();

    [JsonPropertyName("errorAndExceptionHandling")]
    public List<string> ErrorAndExceptionHandling { get; set; } = new();

    [JsonPropertyName("stateMutationsAndEvents")]
    public List<string> StateMutationsAndEvents { get; set; } = new();

    [JsonPropertyName("securityAndQualityNotes")]
    public List<string> SecurityAndQualityNotes { get; set; } = new();

    [JsonPropertyName("summarizedAt")]
    public DateTime SummarizedAt { get; set; } = DateTime.UtcNow;
}

public sealed class FileSummaryCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FileSummaryCache> _logger;
    private readonly bool _disabled;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }

    public FileSummaryCache(string repoRoot, ILogger<FileSummaryCache> logger, bool disabled = false, string? customCacheDir = null)
    {
        _logger = logger;
        _disabled = disabled;
        _cacheDirectory = customCacheDir ?? Path.Combine(repoRoot, ".atlas", "cache", "files");

        if (!_disabled && !Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public FileSummary? TryGet(string gitBlobSha)
    {
        if (_disabled || string.IsNullOrWhiteSpace(gitBlobSha)) return null;

        var filePath = GetCacheFilePath(gitBlobSha);
        if (!File.Exists(filePath))
        {
            CacheMisses++;
            _logger.LogTrace("[FileSummaryCache] Cache MISS for Git Blob SHA: {Sha}", gitBlobSha);
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var summary = JsonSerializer.Deserialize<FileSummary>(json, JsonOptions);
            if (summary != null)
            {
                CacheHits++;
                _logger.LogTrace("[FileSummaryCache] Cache HIT for {Path} (Git SHA: {Sha})", summary.RelativePath, gitBlobSha);
                return summary;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileSummaryCache] Failed to read cached summary for SHA: {Sha}", gitBlobSha);
        }

        CacheMisses++;
        return null;
    }

    public void Store(FileSummary summary)
    {
        if (_disabled || string.IsNullOrWhiteSpace(summary.GitBlobSha)) return;

        try
        {
            var filePath = GetCacheFilePath(summary.GitBlobSha);
            var json = JsonSerializer.Serialize(summary, JsonOptions);
            File.WriteAllText(filePath, json);
            _logger.LogTrace("[FileSummaryCache] Cached summary for {Path} at {CacheFile}", summary.RelativePath, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileSummaryCache] Failed to write cache for {Path}", summary.RelativePath);
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
                _logger.LogInformation("[FileSummaryCache] Cleared cache directory: {Dir}", _cacheDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileSummaryCache] Failed to clear cache directory: {Dir}", _cacheDirectory);
        }
    }

    private string GetCacheFilePath(string gitBlobSha) => Path.Combine(_cacheDirectory, $"{gitBlobSha}.json");
}
