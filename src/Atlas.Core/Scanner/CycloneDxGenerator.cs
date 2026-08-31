using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Scanner;

/// <summary>
/// 100% Offline Software Bill of Materials (SBOM) Generator.
/// Extracts standard CycloneDX 1.5 components and PURLs directly from local codebase manifests
/// with zero outbound network calls (offloading cloud license & vulnerability enrichment to Atlas Server).
/// </summary>
public sealed class CycloneDxGenerator
{
    private readonly ILogger<CycloneDxGenerator>? _logger;

    public CycloneDxGenerator(ILogger<CycloneDxGenerator>? logger = null)
    {
        _logger = logger;
    }

    public Task<(CycloneDxSbom Sbom, VulnerabilityAuditSummary VulnAudit)> GenerateAsync(
        CodebaseSkeleton skeleton,
        CancellationToken cancellationToken = default)
    {
        var sbom = new CycloneDxSbom
        {
            BomFormat = "CycloneDX",
            SpecVersion = "1.5",
            SerialNumber = $"urn:uuid:{Guid.NewGuid()}",
            Version = 1,
            Metadata = new CycloneDxMetadata
            {
                Timestamp = DateTime.UtcNow,
                Component = new CycloneDxComponent
                {
                    Type = "application",
                    Name = skeleton.RepoName,
                    Version = skeleton.Git?.CommitShaShort ?? "1.0.0",
                    Description = $"Application component synthesized by Atlas for {skeleton.RepoName}"
                }
            }
        };

        var discoveredPackages = new Dictionary<string, (string Name, string Version, string Ecosystem, string? Purpose)>(StringComparer.OrdinalIgnoreCase);

        // Extract packages from local manifests
        foreach (var m in skeleton.Manifests)
        {
            foreach (var p in m.ExtractedPackages)
            {
                var ecosystem = InferEcosystem(m.ManifestType, p.Name);
                var key = $"{ecosystem}:{p.Name}";
                if (!discoveredPackages.ContainsKey(key) || string.IsNullOrEmpty(discoveredPackages[key].Version))
                {
                    discoveredPackages[key] = (p.Name, p.Version ?? "1.0.0", ecosystem, p.Purpose);
                }
            }
        }

        var components = new List<CycloneDxComponent>();

        foreach (var pkg in discoveredPackages.Values)
        {
            var (name, version, ecosystem, purpose) = pkg;
            var cleanVersion = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.TrimStart('v', '^', '~', '=');
            var purl = $"pkg:{ecosystem.ToLowerInvariant()}/{name}@{cleanVersion}";
            var licenseName = GuessLicense(name);

            var comp = new CycloneDxComponent
            {
                Type = "library",
                BomRef = purl,
                Name = name,
                Version = cleanVersion,
                Purl = purl,
                Description = purpose ?? $"{name} dependency for {skeleton.RepoName}",
                Scope = "required",
                Licenses = new List<CycloneDxLicenseChoice>
                {
                    new CycloneDxLicenseChoice
                    {
                        License = new CycloneDxLicense
                        {
                            Id = licenseName,
                            Name = licenseName
                        }
                    }
                },
                Properties = new List<CycloneDxProperty>
                {
                    new CycloneDxProperty { Name = "atlas:ecosystem", Value = ecosystem },
                    new CycloneDxProperty { Name = "atlas:audit_status", Value = "PENDING_SERVER_ENRICHMENT" }
                }
            };

            components.Add(comp);
        }

        sbom.Components = components.OrderBy(c => c.Name).ToList();
        sbom.Vulnerabilities = new List<CycloneDxVulnerability>();

        var vulnAudit = new VulnerabilityAuditSummary
        {
            TotalVulnerabilities = 0,
            CriticalCount = 0,
            HighCount = 0,
            MediumCount = 0,
            LowCount = 0,
            AuditTimestamp = DateTime.UtcNow,
            Vulnerabilities = new List<CycloneDxVulnerability>()
        };

        return Task.FromResult((sbom, vulnAudit));
    }

    public static string InferEcosystem(string manifestType, string packageName)
    {
        var lower = (manifestType ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("nodepackagejson") || lower.Contains("package") || lower.Contains("npm") || lower.Contains("node")) return "npm";
        if (lower.Contains("python") || lower.Contains("pypi") || lower.Contains("requirements") || lower.Contains("poetry")) return "PyPI";
        if (lower.Contains("go") || lower.Contains("golang")) return "Go";
        if (lower.Contains("maven") || lower.Contains("pom") || lower.Contains("gradle") || lower.Contains("java")) return "Maven";
        if (lower.Contains("cargo") || lower.Contains("rust") || lower.Contains("crates")) return "crates.io";
        if (lower.Contains("dotnet") || lower.Contains("csproj") || lower.Contains("nuget")) return "NuGet";

        // Fallback checks on package name
        if (packageName.StartsWith("@") ||
            packageName.StartsWith("ngx-", StringComparison.OrdinalIgnoreCase) ||
            packageName.StartsWith("ng2-", StringComparison.OrdinalIgnoreCase) ||
            packageName.StartsWith("chartjs-", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("typescript", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("rxjs", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("zone.js", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("tslib", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("date-fns", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("chart.js", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("vite", StringComparison.OrdinalIgnoreCase) ||
            packageName.Equals("webpack", StringComparison.OrdinalIgnoreCase))
        {
            return "npm";
        }

        return "NuGet";
    }

    public static string GuessLicense(string pkgName)
    {
        var lower = pkgName.ToLowerInvariant();
        if (lower.Contains("microsoft.") || lower.Contains("system.") || lower.Contains("azure.") || lower.Contains("google."))
            return "MIT";
        if (lower.Contains("apache") || lower.Contains("serilog") || lower.Contains("nlog"))
            return "Apache-2.0";
        if (lower.Contains("gpl") || lower.Contains("agpl"))
            return "GPL-3.0-only";
        if (lower.Contains("bsd"))
            return "BSD-3-Clause";
        return "MIT";
    }
}
