using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Core.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Security;

/// <summary>
/// Intelligence client using Google's Open Source Insights (deps.dev) API.
/// Fetches verified SPDX licenses, deprecation advisories, version drift, and publication metadata.
/// </summary>
public sealed class DepsDevClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DepsDevClient> _logger;
    private static readonly ConcurrentDictionary<string, DepsDevVersionInfo?> _versionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string?> _latestVersionCache = new(StringComparer.OrdinalIgnoreCase);

    public DepsDevClient(HttpClient? httpClient = null, ILogger<DepsDevClient>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri("https://api.deps.dev/v3/"), Timeout = TimeSpan.FromSeconds(10) };
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DepsDevClient>();
    }

    public void ClearCache()
    {
        _versionCache.Clear();
        _latestVersionCache.Clear();
        _logger.LogInformation("[deps.dev] In-memory cache cleared");
    }

    /// <summary>
    /// Enriches a CycloneDX component with verified SPDX licenses, deprecation status, and version lifecycle metadata.
    /// </summary>
    public async Task EnrichComponentAsync(
        CycloneDxComponent component,
        string ecosystem = "NuGet",
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (component == null || string.IsNullOrWhiteSpace(component.Name) || string.IsNullOrWhiteSpace(component.Version))
            return;

        var system = NormalizeSystem(ecosystem);
        var pkgName = component.Name;
        var version = component.Version.TrimStart('v', '^', '~', '=');

        try
        {
            var versionInfo = await GetVersionInfoAsync(system, pkgName, version, forceRefresh, cancellationToken);
            if (versionInfo != null)
            {
                // 1. Enrich SPDX Licenses if verified from deps.dev
                if (versionInfo.Licenses != null && versionInfo.Licenses.Count > 0)
                {
                    component.Licenses.Clear();
                    foreach (var lic in versionInfo.Licenses)
                    {
                        component.Licenses.Add(new CycloneDxLicenseChoice
                        {
                            License = new CycloneDxLicense
                            {
                                Id = lic,
                                Name = lic
                            }
                        });
                    }
                }

                // 2. Attach Standard CycloneDX Properties
                SetProperty(component, "atlas:license:verifiedBy", "Google deps.dev");
                if (versionInfo.PublishedAt.HasValue)
                {
                    SetProperty(component, "atlas:lifecycle:publishedAt", versionInfo.PublishedAt.Value.ToString("o"));
                }

                if (versionInfo.IsDeprecated)
                {
                    SetProperty(component, "atlas:lifecycle:isDeprecated", "true");
                    if (!string.IsNullOrWhiteSpace(versionInfo.DeprecatedReason))
                    {
                        SetProperty(component, "atlas:lifecycle:deprecatedReason", versionInfo.DeprecatedReason);
                    }
                }
                else
                {
                    SetProperty(component, "atlas:lifecycle:isDeprecated", "false");
                }

                // 3. Attach External References (Source repo / Homepage)
                if (versionInfo.Links != null)
                {
                    foreach (var link in versionInfo.Links)
                    {
                        var type = link.Label switch
                        {
                            "SOURCE_REPO" => "vcs",
                            "HOMEPAGE" => "website",
                            "ISSUE_TRACKER" => "issue-tracker",
                            _ => "other"
                        };

                        if (!component.ExternalReferences.Any(r => r.Url.Equals(link.Url, StringComparison.OrdinalIgnoreCase)))
                        {
                            component.ExternalReferences.Add(new CycloneDxExternalReference
                            {
                                Type = type,
                                Url = link.Url
                            });
                        }
                    }
                }
            }

            // 4. Query Latest Version
            var latestVer = await GetLatestVersionAsync(system, pkgName, forceRefresh, cancellationToken);
            if (!string.IsNullOrWhiteSpace(latestVer))
            {
                SetProperty(component, "atlas:lifecycle:latestVersion", latestVer);
                var isOutdated = !string.Equals(version, latestVer, StringComparison.OrdinalIgnoreCase);
                SetProperty(component, "atlas:lifecycle:isOutdated", isOutdated ? "true" : "false");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[deps.dev] Failed to enrich component {Package}@{Version}", pkgName, version);
        }
    }

    public async Task<DepsDevVersionInfo?> GetVersionInfoAsync(
        string system,
        string packageName,
        string version,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{system}:{packageName}@{version}";
        if (!forceRefresh && _versionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var encodedPkg = Uri.EscapeDataString(packageName);
            var encodedVer = Uri.EscapeDataString(version);
            var url = $"systems/{system}/packages/{encodedPkg}/versions/{encodedVer}";

            _logger.LogInformation("[deps.dev] Outbound HTTP GET -> https://api.deps.dev/v3/{Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("[deps.dev] Query for {System}:{Package}@{Version} returned HTTP {Status}", system, packageName, version, response.StatusCode);
                _versionCache[cacheKey] = null;
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<DepsDevVersionResponse>(cancellationToken: cancellationToken);
            if (dto == null)
            {
                _versionCache[cacheKey] = null;
                return null;
            }

            var info = new DepsDevVersionInfo
            {
                Licenses = dto.Licenses ?? new List<string>(),
                IsDeprecated = dto.IsDeprecated,
                DeprecatedReason = dto.DeprecatedReason,
                PublishedAt = dto.PublishedAt,
                Links = dto.Links ?? new List<DepsDevLink>()
            };

            _logger.LogInformation("[deps.dev] Inbound HTTP 200 for {Package}@{Version} -> Licenses: [{Licenses}], Deprecated: {IsDep}",
                packageName, version, string.Join(", ", info.Licenses), info.IsDeprecated);

            _versionCache[cacheKey] = info;
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[deps.dev] Error querying version info for {Package}@{Version}", packageName, version);
            _versionCache[cacheKey] = null;
            return null;
        }
    }

    public async Task<string?> GetLatestVersionAsync(
        string system,
        string packageName,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{system}:{packageName}";
        if (!forceRefresh && _latestVersionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var encodedPkg = Uri.EscapeDataString(packageName);
            var url = $"systems/{system}/packages/{encodedPkg}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _latestVersionCache[cacheKey] = null;
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<DepsDevPackageResponse>(cancellationToken: cancellationToken);
            var latestVer = dto?.Versions?.FirstOrDefault(v => v.IsDefault)?.VersionKey?.Version
                         ?? dto?.Versions?.LastOrDefault()?.VersionKey?.Version;

            _latestVersionCache[cacheKey] = latestVer;
            return latestVer;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[deps.dev] Failed fetching package summary for {Package}", packageName);
            _latestVersionCache[cacheKey] = null;
            return null;
        }
    }

    private static void SetProperty(CycloneDxComponent comp, string name, string value)
    {
        var existing = comp.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            comp.Properties.Add(new CycloneDxProperty { Name = name, Value = value });
        }
    }

    private static string NormalizeSystem(string ecosystem)
    {
        return ecosystem.ToLowerInvariant() switch
        {
            "nuget" or ".net" or "dotnet" or "c#" => "nuget",
            "npm" or "node" or "javascript" or "typescript" => "npm",
            "pypi" or "python" => "pypi",
            "go" or "golang" => "go",
            "maven" or "java" => "maven",
            "cargo" or "rust" or "crates.io" => "cargo",
            _ => "nuget"
        };
    }
}

public sealed class DepsDevVersionInfo
{
    public List<string> Licenses { get; set; } = new();
    public bool IsDeprecated { get; set; }
    public string? DeprecatedReason { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<DepsDevLink> Links { get; set; } = new();
}

internal sealed class DepsDevVersionResponse
{
    [JsonPropertyName("licenses")]
    public List<string>? Licenses { get; set; }

    [JsonPropertyName("isDeprecated")]
    public bool IsDeprecated { get; set; }

    [JsonPropertyName("deprecatedReason")]
    public string? DeprecatedReason { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("links")]
    public List<DepsDevLink>? Links { get; set; }
}

public sealed class DepsDevLink
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

internal sealed class DepsDevPackageResponse
{
    [JsonPropertyName("versions")]
    public List<DepsDevVersionEntry>? Versions { get; set; }
}

internal sealed class DepsDevVersionEntry
{
    [JsonPropertyName("versionKey")]
    public DepsDevVersionKey? VersionKey { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}

internal sealed class DepsDevVersionKey
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
