using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Caching;
using Atlas.Core.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Client;

public sealed class AtlasServerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AtlasServerClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AtlasServerClient(HttpClient httpClient, ILogger<AtlasServerClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckHealthAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        try
        {
            var response = await _httpClient.GetAsync(new Uri(baseUri, "api/v1/health"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "[AtlasServerClient] Health check failed for {ServerUrl}", serverUrl);
            return false;
        }
    }

    public async Task<SynthesisCheckResponse?> CheckSynthesisAsync(
        string serverUrl,
        string repoName,
        string commitSha,
        Dictionary<string, string> fileShas,
        CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        var targetUri = new Uri(baseUri, "api/v1/cache/synthesis/check");

        var request = new SynthesisCheckRequest
        {
            RepoName = repoName,
            CommitSha = commitSha,
            FileShas = fileShas
        };

        try
        {
            _logger.LogDebug("[AtlasServerClient] Checking remote synthesis cache at {Uri} for commit {Sha}", targetUri, commitSha);
            var response = await _httpClient.PostAsJsonAsync(targetUri, request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[AtlasServerClient] Remote cache check returned status code: {Code}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SynthesisCheckResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AtlasServerClient] Failed to check remote synthesis cache at {ServerUrl}", serverUrl);
            return null;
        }
    }

    public async Task<Dictionary<string, FileSummary>> QueryFileSummariesAsync(
        string serverUrl,
        List<string> blobShas,
        CancellationToken cancellationToken = default)
    {
        if (blobShas == null || blobShas.Count == 0)
        {
            return new Dictionary<string, FileSummary>();
        }

        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        var targetUri = new Uri(baseUri, "api/v1/cache/files/query");

        var request = new FileSummaryQueryRequest
        {
            BlobShas = blobShas
        };

        try
        {
            _logger.LogDebug("[AtlasServerClient] Querying remote file cache for {Count} Git Blob SHAs", blobShas.Count);
            var response = await _httpClient.PostAsJsonAsync(targetUri, request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[AtlasServerClient] Remote file query returned status: {Code}", response.StatusCode);
                return new Dictionary<string, FileSummary>();
            }

            var result = await response.Content.ReadFromJsonAsync<FileSummaryQueryResponse>(JsonOptions, cancellationToken);
            return result?.Summaries ?? new Dictionary<string, FileSummary>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AtlasServerClient] Failed to query remote file summaries from {ServerUrl}", serverUrl);
            return new Dictionary<string, FileSummary>();
        }
    }

    public async Task<bool> StoreFileSummariesAsync(
        string serverUrl,
        List<FileSummary> summaries,
        CancellationToken cancellationToken = default)
    {
        if (summaries == null || summaries.Count == 0) return true;

        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        var targetUri = new Uri(baseUri, "api/v1/cache/files/store");

        var request = new FileSummaryStoreRequest
        {
            Summaries = summaries
        };

        try
        {
            _logger.LogDebug("[AtlasServerClient] Storing {Count} new file summaries into remote cache at {Uri}", summaries.Count, targetUri);
            var response = await _httpClient.PostAsJsonAsync(targetUri, request, JsonOptions, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AtlasServerClient] Failed to store remote file summaries to {ServerUrl}", serverUrl);
            return false;
        }
    }

    public async Task<CatalogIngestResponse?> IngestCatalogItemAsync(
        string serverUrl,
        AtlasResource resource,
        string commitSha,
        Dictionary<string, string> fileShas,
        CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        var targetUri = new Uri(baseUri, "api/v1/catalog/ingest");

        var request = new CatalogIngestRequest
        {
            Resource = resource,
            CommitSha = commitSha,
            FileShas = fileShas
        };

        try
        {
            _logger.LogInformation("[AtlasServerClient] Ingesting catalog item '{Name}' into Atlas Server at {Uri}",
                resource.Metadata?.Name ?? "unknown", targetUri);

            var response = await _httpClient.PostAsJsonAsync(targetUri, request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[AtlasServerClient] Ingestion failed with status {Code}: {Error}", response.StatusCode, errorBody);
                return new CatalogIngestResponse
                {
                    Success = false,
                    Message = $"Ingestion failed: HTTP {response.StatusCode} - {errorBody}"
                };
            }

            return await response.Content.ReadFromJsonAsync<CatalogIngestResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AtlasServerClient] Error connecting to Atlas Server for ingestion at {ServerUrl}", serverUrl);
            return new CatalogIngestResponse
            {
                Success = false,
                Message = $"Network error connecting to Atlas Server at {serverUrl}: {ex.Message}"
            };
        }
    }
    public async Task<bool> DeleteServiceAsync(
        string serverUrl,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        var targetUri = new Uri(baseUri, $"api/v1/catalog/resources/{Uri.EscapeDataString(serviceName)}");

        try
        {
            _logger.LogInformation("[AtlasServerClient] Sending DELETE request for service '{Name}' to {Uri}", serviceName, targetUri);
            var response = await _httpClient.DeleteAsync(targetUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[AtlasServerClient] Service '{Name}' successfully deleted from Atlas Server", serviceName);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("[AtlasServerClient] Delete failed with status {Code}: {Error}", response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AtlasServerClient] Error deleting service '{Name}' from Atlas Server at {ServerUrl}", serviceName, serverUrl);
            return false;
        }
    }
}
