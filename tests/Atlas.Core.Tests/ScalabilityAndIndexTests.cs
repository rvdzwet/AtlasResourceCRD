using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Atlas.Core.Models;
using Xunit;

namespace Atlas.Core.Tests;

public sealed class ScalabilityAndIndexTests
{
    [Fact]
    public void ServiceSummaryRecord_MapsCorrectlyFromAtlasResource()
    {
        var resource = new AtlasResource
        {
            Metadata = new AtlasResourceMetadata
            {
                Name = "hypotheken-hypotheekofferte-service"
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Tier = "Backend",
                    Purpose = "Dutch Mortgage Application Generation",
                    Description = "High-throughput mortgage offer generator",
                    Owner = "Lending Core Guild",
                    Lifecycle = "Active"
                },
                TechStack = new TechStack
                {
                    PrimaryLanguage = "C# / .NET 10",
                    Frameworks = new List<TechItem>
                    {
                        new() { Name = "ASP.NET Core" },
                        new() { Name = "MassTransit" }
                    }
                },
                Quality = new QualityVerdictSpec
                {
                    SigStars = 4.8,
                    MaintainabilityLevel = "Very High"
                },
                CodeReview = new CodeReviewSpec
                {
                    ReviewGrade = "A+"
                },
                ThreatModel = new ThreatModelSpec
                {
                    Threats = new List<ThreatVector>
                    {
                        new() { Severity = "Critical", ThreatScenario = "SQL Injection in Quote API" },
                        new() { Severity = "High", ThreatScenario = "Token spoofing" }
                    }
                },
                RiskSummary = new RiskSummarySpec
                {
                    OverallRiskLevel = "Medium",
                    ProductionReadiness = "Approved"
                }
            }
        };

        var summary = ServiceSummaryRecord.FromResource(resource);

        Assert.Equal("hypotheken-hypotheekofferte-service", summary.Name);
        Assert.Equal("Dutch Mortgage & Lending Core", summary.Domain);
        Assert.Equal("Backend", summary.Tier);
        Assert.Equal(4.8, summary.SigStars);
        Assert.Equal("A+", summary.ReviewGrade);
        Assert.Equal(2, summary.ThreatCount);
        Assert.Equal(1, summary.CriticalThreatCount);
        Assert.Equal(1, summary.HighThreatCount);
        Assert.Equal("Approved", summary.ProductionReadiness);
        Assert.Contains("ASP.NET Core", summary.Frameworks);
    }

    [Fact]
    public void HighScale_25000_ServiceSummaries_MemoryAndQueryPerformance()
    {
        // 1. Generate 25,000 simulated service summaries
        var summaries = new List<ServiceSummaryRecord>(25000);
        var random = new Random(42);

        var tiers = new[] { "Backend", "Frontend", "Worker", "Gateway", "DataService" };
        var domains = new[] { "Dutch Mortgage & Lending Core", "Smart Home & IoT Automation", "Payments & Financial Ledger", "Identity & Security Services" };
        var langs = new[] { "C# / .NET 10", "TypeScript / React", "Go", "Python", "Kotlin" };

        for (int i = 1; i <= 25000; i++)
        {
            summaries.Add(new ServiceSummaryRecord
            {
                Name = $"service-node-{i:D5}",
                Tier = tiers[i % tiers.Length],
                Domain = domains[i % domains.Length],
                PrimaryLanguage = langs[i % langs.Length],
                SigStars = 3.0 + (random.NextDouble() * 2.0),
                ReviewGrade = (i % 3 == 0) ? "A+" : (i % 3 == 1 ? "A" : "B"),
                ThreatCount = random.Next(0, 5),
                CriticalCveCount = (i % 20 == 0) ? 1 : 0,
                ProductionReadiness = (i % 10 == 0) ? "Pending Review" : "Approved"
            });
        }

        Assert.Equal(25000, summaries.Count);

        // 2. Measure Paging & Search Performance across 25,000 items
        var sw = Stopwatch.StartNew();

        var query = summaries.AsEnumerable();
        var search = "node-12";
        var filtered = query
            .Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.SigStars)
            .Skip(0)
            .Take(25)
            .ToList();

        sw.Stop();

        Assert.NotEmpty(filtered);
        Assert.True(sw.ElapsedMilliseconds < 50, $"Expected filter across 25,000 items to take <50ms, took {sw.ElapsedMilliseconds}ms");
    }
}
