using System;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Core.Models;

public sealed class ServiceSnapshotRecord
{
    public string SnapshotId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? GitCommitSha { get; set; }
    public string? GitBranch { get; set; }
    public string? GitCommitMessage { get; set; }

    // Quality Metrics
    public double SigStars { get; set; } = 4.0;
    public double TechDebtRatio { get; set; } = 0.0;
    public double MaintainabilityIndex { get; set; } = 80.0;
    public int CodeSmellCount { get; set; } = 0;
    public int ArchitecturalViolationsCount { get; set; } = 0;

    // Security Metrics
    public int ThreatCount { get; set; } = 0;
    public int CriticalThreatsCount { get; set; } = 0;
    public int HighThreatsCount { get; set; } = 0;
    public int VulnerabilityCount { get; set; } = 0;

    // Architecture & Volume Metrics
    public int ComponentCount { get; set; } = 0;
    public int ApiEndpointCount { get; set; } = 0;
    public int DatastoreCount { get; set; } = 0;
    public int PackageDependencyCount { get; set; } = 0;
    public int TotalLinesOfCode { get; set; } = 0;
    public int TotalFilesCount { get; set; } = 0;

    // Delta flags compared to previous snapshot
    public double DeltaSigStars { get; set; }
    public int DeltaThreats { get; set; }
    public int DeltaCodeSmells { get; set; }
    public double DeltaTechDebtRatio { get; set; }
    public int DeltaLinesOfCode { get; set; }

    public static ServiceSnapshotRecord FromResource(AtlasResource resource, string? commitSha = null, DateTimeOffset? timestamp = null)
    {
        var spec = resource.Spec;
        var meta = resource.Metadata;
        var qual = spec?.Quality;
        var review = spec?.CodeReview;
        var threat = spec?.ThreatModel;
        var arch = spec?.Architecture;
        var deps = spec?.Dependencies;

        var rec = new ServiceSnapshotRecord
        {
            SnapshotId = (timestamp ?? DateTimeOffset.UtcNow).ToString("yyyyMMddHHmmss") + (string.IsNullOrWhiteSpace(commitSha) ? "" : $"_{commitSha[..Math.Min(7, commitSha.Length)]}"),
            ServiceName = meta?.Name ?? "unknown",
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            GitCommitSha = commitSha ?? (meta?.Annotations != null && meta.Annotations.TryGetValue("atlas.io/git-commit", out var c) ? c : null),
            GitBranch = meta?.Annotations != null && meta.Annotations.TryGetValue("atlas.io/git-branch", out var b) ? b : null,
            
            SigStars = qual?.SigStars ?? 4.0,
            TechDebtRatio = qual?.TechDebtItems != null && qual.TechDebtItems.Count > 0 ? qual.TechDebtItems.Count * 2.5 : 0.0,
            MaintainabilityIndex = (qual?.SigStars ?? 4.0) * 20.0,
            CodeSmellCount = review?.CodeSmells?.Count ?? 0,
            ArchitecturalViolationsCount = review?.Findings?.Count ?? 0,

            ThreatCount = threat?.Threats?.Count ?? 0,
            CriticalThreatsCount = threat?.Threats?.Count(t => string.Equals(t.Severity, "Critical", StringComparison.OrdinalIgnoreCase)) ?? 0,
            HighThreatsCount = threat?.Threats?.Count(t => string.Equals(t.Severity, "High", StringComparison.OrdinalIgnoreCase)) ?? 0,
            VulnerabilityCount = deps?.VulnerabilityAudit?.TotalVulnerabilities ?? 0,

            ComponentCount = arch?.Components?.Count ?? 0,
            ApiEndpointCount = spec?.ApiContracts?.Endpoints?.Count ?? 0,
            DatastoreCount = spec?.DataStores?.Databases?.Count ?? 0,
            PackageDependencyCount = deps?.KeyPackages?.Count ?? 0,
            TotalLinesOfCode = 0,
            TotalFilesCount = 0
        };

        return rec;
    }
}

public sealed class ServiceHistorySummary
{
    public string ServiceName { get; set; } = string.Empty;
    public List<ServiceSnapshotRecord> Snapshots { get; set; } = new();
    public ServiceSnapshotRecord? LatestSnapshot => Snapshots.LastOrDefault();
    public ServiceSnapshotRecord? PreviousSnapshot => Snapshots.Count > 1 ? Snapshots[^2] : null;

    public string TrendDirection
    {
        get
        {
            if (Snapshots.Count < 2) return "stable";
            var latest = Snapshots[^1];
            var prev = Snapshots[^2];
            var diff = latest.SigStars - prev.SigStars;
            if (diff > 0.1 || (latest.ThreatCount < prev.ThreatCount && diff >= -0.05) || (latest.CodeSmellCount < prev.CodeSmellCount && diff >= -0.05)) 
                return "improving";
            if (diff < -0.1 || latest.ThreatCount > prev.ThreatCount || latest.CodeSmellCount > prev.CodeSmellCount) 
                return "degrading";
            return "stable";
        }
    }
}
