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

    [Fact]
    public void Generate_ShouldRenderCodeReviewDashboardInHtml()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata { Name = "reviewed-service" },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview { Name = "reviewed-service", Tier = "Backend" },
                Architecture = new ArchitectureSpec { ComponentDiagram = "flowchart TD\n  A --> B" },
                CodeReview = new CodeReviewSpec
                {
                    ReviewGrade = "A",
                    ReviewScore = 94,
                    Summary = "Exemplary clean architecture with strong domain boundary separation.",
                    Strengths = new List<string> { "Consistent dependency injection", "Idiomatic asynchronous patterns" },
                    CodeSmells = new List<CodeSmellItem>
                    {
                        new() { SmellType = "Long Parameter List", Description = "DeviceController constructor has 9 parameters", AffectedComponentOrFile = "DeviceController.cs" }
                    },
                    Findings = new List<CodeReviewFinding>
                    {
                        new()
                        {
                            Title = "Extract Parameter Object",
                            Category = "Maintainability",
                            Severity = "Minor",
                            File = "src/Controllers/DeviceController.cs",
                            Symbol = "DeviceController(..)",
                            Description = "Constructor has too many parameters.",
                            Recommendation = "Group parameters into a DeviceControllerOptions object."
                        }
                    }
                }
            }
        };

        var html = HtmlVisualizerGenerator.Generate(resource);
        html.Should().Contain("Automated Code Review & Architectural Insights");
        html.Should().Contain("Grade: A (94/100)");
        html.Should().Contain("Consistent dependency injection");
        html.Should().Contain("Long Parameter List");
        html.Should().Contain("DeviceController.cs");
        html.Should().Contain("Extract Parameter Object");
    }

    [Fact]
    public void Generate_ShouldRenderExecutiveRiskAndThreatModelSections()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata { Name = "restricted-service" },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview { Name = "restricted-service", Tier = "Backend" },
                Architecture = new ArchitectureSpec { ComponentDiagram = "flowchart TD\n  A --> B" },
                RiskSummary = new RiskSummarySpec
                {
                    OverallRiskLevel = "High",
                    ProductionReadiness = "Conditional",
                    ExecutiveSummary = "Restricted environment deployment requires air-gap network isolation and mTLS enforcement.",
                    BlastRadiusEvaluation = "Process crash isolated to IoT daemon; database remains protected via WAL journal mode.",
                    RestrictedEnvironmentCompliance = "Complies with zero-trust local network rules; external cloud telemetry disabled.",
                    Risks = new List<RiskItem>
                    {
                        new()
                        {
                            RiskTitle = "Unauthenticated Local MQTT Broker",
                            RiskLevel = "High",
                            Impact = "Unauthorized command injection on local subnet.",
                            Likelihood = "Medium",
                            TriggerScenario = "Attacker on same VLAN connects to port 1883.",
                            RequiredMitigation = "Enforce mTLS certificate validation and user ACLs on MQTT broker."
                        }
                    }
                },
                ThreatModel = new ThreatModelSpec
                {
                    Methodology = "STRIDE",
                    AttackSurfaceSummary = "Exposed ports include HTTP 5000 and MQTT 8883 across LAN boundary.",
                    TrustBoundaries = new List<TrustBoundary>
                    {
                        new()
                        {
                            Name = "LAN vs In-Process Memory",
                            Description = "Separates external subnet from local daemon memory.",
                            AssetsInside = new List<string> { "In-Memory Device State", "Encryption Keys" }
                        }
                    },
                    Threats = new List<ThreatVector>
                    {
                        new()
                        {
                            Id = "T-01",
                            StrideCategory = "Tampering",
                            TargetAsset = "Device Telemetry Stream",
                            ThreatScenario = "Packet modification in transit without TLS.",
                            Severity = "High",
                            MitigationControl = "Mandate TLS 1.3 encryption on all telemetry sockets.",
                            ResidualRisk = "Low"
                        }
                    }
                }
            }
        };

        var html = HtmlVisualizerGenerator.Generate(resource);
        html.Should().Contain("Executive Risk & Blast Radius Assessment");
        html.Should().Contain("Production Readiness: Conditional");
        html.Should().Contain("Overall Risk: High");
        html.Should().Contain("Unauthenticated Local MQTT Broker");
        html.Should().Contain("STRIDE Threat Model & Attack Surface");
        html.Should().Contain("LAN vs In-Process Memory");
        html.Should().Contain("Device Telemetry Stream");
        html.Should().Contain("Mandate TLS 1.3 encryption");
    }

    [Fact]
    public void Generate_ShouldRenderLivingDocumentationAndC4Legend()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata { Name = "living-doc-service" },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview { Name = "living-doc-service", Tier = "Backend" },
                Architecture = new ArchitectureSpec { ComponentDiagram = "flowchart TD\n  A --> B" },
                FunctionalSpecs = new FunctionalSpecs
                {
                    Capabilities = new List<CapabilityItem>
                    {
                        new()
                        {
                            Name = "Adaptive Comfort Balancing",
                            Description = "Dynamically adjusts climate setpoints based on occupant presence and real-time solar yield.",
                            BusinessOutcome = "Reduces grid energy consumption by 24%."
                        }
                    },
                    UseCases = new List<BusinessUseCase>
                    {
                        new()
                        {
                            Id = "UC-01",
                            Title = "Dynamic Airco Setpoint Adjustment",
                            Capability = "Adaptive Comfort Balancing",
                            PrimaryActor = "Home Automation Daemon",
                            BusinessValue = "Avoids compressor cycling during peak energy tariffs.",
                            Trigger = "Solar yield drops below 1.5 kW threshold.",
                            Preconditions = new List<string> { "Airco integration online", "Enphase solar gateway responding" },
                            MainFlow = new List<string> { "Read current solar power", "Evaluate adaptive comfort target", "Dispatch HTTP command to Daikin unit" },
                            BusinessRules = new List<string> { "Maintain minimum compressor runtime of 3 minutes" },
                            AcceptanceScenarios = new List<BddScenario>
                            {
                                new()
                                {
                                    ScenarioTitle = "Solar yield drop triggers eco mode",
                                    Given = "Solar production is 800W and target temperature is 21C",
                                    When = "RuleSupervisor evaluates tariff schedule",
                                    Then = "Airco setpoint is raised to 23C"
                                }
                            },
                            AssociatedComponents = new List<string> { "ClimateBrainService", "DaikinClient" }
                        }
                    }
                }
            }
        };

        var html = HtmlVisualizerGenerator.Generate(resource);
        html.Should().Contain("Living Documentation & Functional Specifications");
        html.Should().Contain("C4 Model Legend");
        html.Should().Contain("Adaptive Comfort Balancing");
        html.Should().Contain("UC-01");
        html.Should().Contain("Dynamic Airco Setpoint Adjustment");
        html.Should().Contain("Actor: Home Automation Daemon");
        html.Should().Contain("Given");
        html.Should().Contain("When");
        html.Should().Contain("Then");
        html.Should().Contain("ClimateBrainService");
    }
}
