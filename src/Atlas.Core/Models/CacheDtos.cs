using System.Collections.Generic;
using System.Text.Json.Serialization;
using Atlas.Core.Caching;

namespace Atlas.Core.Models;

public sealed class SynthesisCheckRequest
{
    [JsonPropertyName("repoName")]
    public string RepoName { get; set; } = string.Empty;

    [JsonPropertyName("commitSha")]
    public string CommitSha { get; set; } = string.Empty;

    [JsonPropertyName("fileShas")]
    public Dictionary<string, string> FileShas { get; set; } = new();
}

public sealed class SynthesisCheckResponse
{
    [JsonPropertyName("isExactMatch")]
    public bool IsExactMatch { get; set; }

    [JsonPropertyName("cachedResource")]
    public AtlasResource? CachedResource { get; set; }

    [JsonPropertyName("missingBlobShas")]
    public List<string> MissingBlobShas { get; set; } = new();
}

public sealed class FileSummaryQueryRequest
{
    [JsonPropertyName("blobShas")]
    public List<string> BlobShas { get; set; } = new();
}

public sealed class FileSummaryQueryResponse
{
    [JsonPropertyName("summaries")]
    public Dictionary<string, FileSummary> Summaries { get; set; } = new();
}

public sealed class FileSummaryStoreRequest
{
    [JsonPropertyName("summaries")]
    public List<FileSummary> Summaries { get; set; } = new();
}

public sealed class CatalogIngestRequest
{
    [JsonPropertyName("resource")]
    public AtlasResource Resource { get; set; } = new();

    [JsonPropertyName("commitSha")]
    public string CommitSha { get; set; } = string.Empty;

    [JsonPropertyName("fileShas")]
    public Dictionary<string, string> FileShas { get; set; } = new();
}

public sealed class CatalogIngestResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("graphUpdated")]
    public bool GraphUpdated { get; set; }

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;
}
