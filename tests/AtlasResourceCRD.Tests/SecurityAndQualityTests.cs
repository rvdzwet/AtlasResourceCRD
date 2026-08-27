using System.Collections.Generic;
using AtlasResourceCRD.Core.Html;
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Validation;
using FluentAssertions;
using Xunit;

namespace AtlasResourceCRD.Tests;

public class SecurityAndQualityTests
{
    [Fact]
    public void Validate_ShouldPassForValidSecurityAndQualitySpecs()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "secure-service"
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Name = "secure-service",
                    Tier = "Backend"
                },
                Architecture = new ArchitectureSpec
                {
                    ComponentDiagram = "flowchart TD\n  API --> Service"
                },
                Security = new SecurityScanSpec
                {
                    OverallRating = "A+",
                    SecurityScore = 95,
                    OwaspCompliance = new List<OwaspComplianceItem>
                    {
                        new() { Category = "A01:2021-Broken Access Control", Status = "Compliant", Evidence = "Endpoint auth enforced" }
                    },
                    Findings = new List<SecurityFinding>
                    {
                        new() { Title = "TLS Enforcement", Severity = "Low", Description = "Enforce HTTPS", Mitigation = "Redirect HTTP to HTTPS" }
                    }
                },
                Quality = new QualityVerdictSpec
                {
                    SigStars = 4.5,
                    MaintainabilityLevel = "High",
                    Dimensions = new List<SigDimensionScore>
                    {
                        new() { Dimension = "ComponentIndependence", Stars = 5, Evaluation = "Excellent modular decoupling" }
                    }
                }
            }
        };

        var result = CrdValidator.Validate(resource);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ShouldRenderOwaspAndSigScorecardsInHtml()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata { Name = "test-service" },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview { Name = "test-service", Tier = "Backend" },
                Architecture = new ArchitectureSpec { ComponentDiagram = "flowchart TD\n  A --> B" },
                Security = new SecurityScanSpec
                {
                    OverallRating = "A",
                    SecurityScore = 92,
                    OwaspCompliance = new List<OwaspComplianceItem>
                    {
                        new() { Category = "A01 Broken Access Control", Status = "Compliant", Evidence = "RBAC enabled" }
                    }
                },
                Quality = new QualityVerdictSpec
                {
                    SigStars = 4.5,
                    MaintainabilityLevel = "High",
                    Dimensions = new List<SigDimensionScore>
                    {
                        new() { Dimension = "UnitComplexity", Stars = 4, Evaluation = "Low cyclomatic complexity" }
                    }
                }
            }
        };

        var html = HtmlVisualizerGenerator.Generate(resource);
        html.Should().Contain("OWASP Security Posture");
        html.Should().Contain("SIG Maintainability & Quality");
        html.Should().Contain("Grade: A");
        html.Should().Contain("4.5 / 5.0");
        html.Should().Contain("UnitComplexity");
    }
}
