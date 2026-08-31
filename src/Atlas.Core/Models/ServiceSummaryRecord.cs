using System;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Core.Models;

/// <summary>
/// Lightweight in-memory representation (~200 bytes) of a registered microservice.
/// Enables lightning-fast indexing, filtering, and aggregation across >20,000 services without loading full documents.
/// </summary>
public sealed class ServiceSummaryRecord
{
    public string Name { get; set; } = string.Empty;
    public string Tier { get; set; } = "Backend";
    public string Domain { get; set; } = "Core Banking & Enterprise";
    public string Description { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string PrimaryLanguage { get; set; } = "C# / .NET";
    public List<string> Frameworks { get; set; } = new();
    public string Owner { get; set; } = "Architecture Guild";
    public string Lifecycle { get; set; } = "Active";
    public double SigStars { get; set; } = 4.0;
    public string ReviewGrade { get; set; } = "A";
    public string MaintainabilityLevel { get; set; } = "High";
    public int ThreatCount { get; set; }
    public int CriticalThreatCount { get; set; }
    public int HighThreatCount { get; set; }
    public int SbomComponentCount { get; set; }
    public int CriticalCveCount { get; set; }
    public int HighCveCount { get; set; }
    public int OutdatedComponentCount { get; set; }
    public int DeprecatedComponentCount { get; set; }
    public string OverallRiskLevel { get; set; } = "Low";
    public string ProductionReadiness { get; set; } = "Approved";
    public int EndpointsCount { get; set; }
    public int DatabasesCount { get; set; }
    public int ExternalApisCount { get; set; }
    public int UseCasesCount { get; set; }
    public int TechDebtCount { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public string RepositoryUrl { get; set; } = string.Empty;

    public static ServiceSummaryRecord FromResource(AtlasResource resource, DateTimeOffset? timestamp = null)
    {
        var meta = resource.Metadata ?? new AtlasResourceMetadata();
        var spec = resource.Spec ?? new AtlasResourceSpec();
        var overview = spec.ComponentOverview ?? new ComponentOverview();
        var tech = spec.TechStack ?? new TechStack();
        var quality = spec.Quality ?? new QualityVerdictSpec();
        var review = spec.CodeReview ?? new CodeReviewSpec();
        var threatModel = spec.ThreatModel ?? new ThreatModelSpec();
        var sbom = spec.Dependencies?.Sbom;
        var risk = spec.RiskSummary ?? new RiskSummarySpec();
        var fn = spec.FunctionalSpecs ?? new FunctionalSpecs();

        var threats = threatModel.Threats ?? new List<ThreatVector>();
        var components = sbom?.Components ?? new List<CycloneDxComponent>();
        var vulns = sbom?.Vulnerabilities ?? new List<CycloneDxVulnerability>();

        // Extract Domain
        var domain = "Core Platform & Infrastructure";
        if (meta.Name.Contains("hypotheken", StringComparison.OrdinalIgnoreCase) || meta.Name.Contains("mortgage", StringComparison.OrdinalIgnoreCase))
            domain = "Dutch Mortgage & Lending Core";
        else if (meta.Name.Contains("iot", StringComparison.OrdinalIgnoreCase) || meta.Name.Contains("smart", StringComparison.OrdinalIgnoreCase))
            domain = "Smart Home & IoT Automation";
        else if (meta.Name.Contains("payment", StringComparison.OrdinalIgnoreCase) || meta.Name.Contains("ledger", StringComparison.OrdinalIgnoreCase))
            domain = "Payments & Financial Ledger";
        else if (meta.Name.Contains("security", StringComparison.OrdinalIgnoreCase) || meta.Name.Contains("auth", StringComparison.OrdinalIgnoreCase))
            domain = "Identity & Security Services";

        return new ServiceSummaryRecord
        {
            Name = meta.Name,
            Tier = string.IsNullOrWhiteSpace(overview.Tier) ? "Backend" : overview.Tier,
            Domain = domain,
            Description = overview.Description ?? "",
            Purpose = overview.Purpose ?? "",
            PrimaryLanguage = string.IsNullOrWhiteSpace(tech.PrimaryLanguage) ? "C# / .NET" : tech.PrimaryLanguage,
            Frameworks = tech.Frameworks?.Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>(),
            Owner = string.IsNullOrWhiteSpace(overview.Owner) ? "Enterprise Architecture" : overview.Owner,
            Lifecycle = string.IsNullOrWhiteSpace(overview.Lifecycle) ? "Active" : overview.Lifecycle,
            SigStars = quality.SigStars > 0 ? quality.SigStars : 4.0,
            ReviewGrade = string.IsNullOrWhiteSpace(review.ReviewGrade) ? "A" : review.ReviewGrade,
            MaintainabilityLevel = string.IsNullOrWhiteSpace(quality.MaintainabilityLevel) ? "High" : quality.MaintainabilityLevel,
            ThreatCount = threats.Count,
            CriticalThreatCount = threats.Count(t => string.Equals(t.Severity, "Critical", StringComparison.OrdinalIgnoreCase)),
            HighThreatCount = threats.Count(t => string.Equals(t.Severity, "High", StringComparison.OrdinalIgnoreCase)),
            SbomComponentCount = components.Count,
            CriticalCveCount = vulns.Count(v => v.Ratings != null && v.Ratings.Any(r => string.Equals(r.Severity, "Critical", StringComparison.OrdinalIgnoreCase))),
            HighCveCount = vulns.Count(v => v.Ratings != null && v.Ratings.Any(r => string.Equals(r.Severity, "High", StringComparison.OrdinalIgnoreCase))),
            OutdatedComponentCount = components.Count(c => c.Properties.Any(p => p.Name == "atlas:lifecycle:isOutdated" && p.Value == "true")),
            DeprecatedComponentCount = components.Count(c => c.Properties.Any(p => p.Name == "atlas:lifecycle:isDeprecated" && p.Value == "true")),
            OverallRiskLevel = string.IsNullOrWhiteSpace(risk.OverallRiskLevel) ? "Low" : risk.OverallRiskLevel,
            ProductionReadiness = string.IsNullOrWhiteSpace(risk.ProductionReadiness) ? "Approved" : risk.ProductionReadiness,
            EndpointsCount = spec.ApiContracts?.Endpoints?.Count ?? 0,
            DatabasesCount = spec.DataStores?.Databases?.Count ?? 0,
            ExternalApisCount = spec.Dependencies?.ExternalApis?.Count ?? 0,
            UseCasesCount = fn.UseCases?.Count ?? 0,
            TechDebtCount = quality.TechDebtItems?.Count ?? 0,
            LastUpdated = timestamp ?? DateTimeOffset.UtcNow,
            RepositoryUrl = overview.RepositoryUrl ?? ""
        };
    }
}
