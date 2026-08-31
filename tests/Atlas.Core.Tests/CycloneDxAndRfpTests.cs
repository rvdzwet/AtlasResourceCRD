using System.Text.Json;
using Atlas.Core.Models;
using Atlas.Core.Scanner;
using Atlas.Core.Security;
using Xunit;

namespace Atlas.Core.Tests;

public class CycloneDxAndRfpTests
{
    [Fact]
    public async Task CycloneDxGenerator_BuildsValid15Sbom()
    {
        var skeleton = new CodebaseSkeleton
        {
            RepoName = "TestMortgageCore",
            Manifests = new List<ScannedManifest>
            {
                new ScannedManifest
                {
                    ManifestType = "DotNetCsproj",
                    ExtractedPackages = new List<PackageDependency>
                    {
                        new PackageDependency { Name = "Newtonsoft.Json", Version = "13.0.1", Purpose = "JSON Serialization" },
                        new PackageDependency { Name = "Serilog", Version = "3.1.1", Purpose = "Structured Logging" }
                    }
                }
            }
        };

        var generator = new CycloneDxGenerator();
        var (sbom, vulnAudit) = await generator.GenerateAsync(skeleton);

        Assert.NotNull(sbom);
        Assert.Equal("CycloneDX", sbom.BomFormat);
        Assert.Equal("1.5", sbom.SpecVersion);
        Assert.Equal(2, sbom.Components.Count);

        var newtonsoft = sbom.Components.FirstOrDefault(c => c.Name == "Newtonsoft.Json");
        Assert.NotNull(newtonsoft);
        Assert.Equal("pkg:nuget/Newtonsoft.Json@13.0.1", newtonsoft.Purl);

        Assert.NotNull(vulnAudit);
    }

    [Fact]
    public void CycloneDxSbom_SerializesValidJson()
    {
        var sbom = new CycloneDxSbom
        {
            BomFormat = "CycloneDX",
            SpecVersion = "1.5",
            Components = new List<CycloneDxComponent>
            {
                new CycloneDxComponent
                {
                    Name = "Microsoft.Extensions.Logging",
                    Version = "8.0.0",
                    Purl = "pkg:nuget/Microsoft.Extensions.Logging@8.0.0",
                    Type = "library"
                }
            },
            Vulnerabilities = new List<CycloneDxVulnerability>
            {
                new CycloneDxVulnerability
                {
                    Id = "CVE-2024-12345",
                    Description = "Sample CVE",
                    Ratings = new List<CycloneDxVulnerabilityRating>
                    {
                        new CycloneDxVulnerabilityRating { Score = 8.5, Severity = "High", Method = "CVSSv31" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(sbom, new JsonSerializerOptions { WriteIndented = true });

        Assert.Contains("\"bomFormat\": \"CycloneDX\"", json);
        Assert.Contains("\"specVersion\": \"1.5\"", json);
        Assert.Contains("\"Microsoft.Extensions.Logging\"", json);
        Assert.Contains("\"CVE-2024-12345\"", json);
    }

    [Fact]
    public void FunctionalSpecs_SupportsRebuildBlueprintFields()
    {
        var crd = new AtlasResource
        {
            Metadata = new AtlasResourceMetadata { Name = "LoanOrigination" },
            Spec = new AtlasResourceSpec
            {
                FunctionalSpecs = new FunctionalSpecs
                {
                    Capabilities = new List<CapabilityItem>
                    {
                        new CapabilityItem { Name = "Underwriting", Description = "Automated risk assessment" }
                    },
                    UseCases = new List<BusinessUseCase>
                    {
                        new BusinessUseCase
                        {
                            Id = "UC-01",
                            Title = "Validate Loan Application",
                            PrimaryActor = "Loan Officer",
                            BusinessValue = "Ensures KYC and debt-to-income limits",
                            MainFlow = new List<string> { "Receive application", "Calculate DTI", "Store application" },
                            AcceptanceScenarios = new List<BddScenario>
                            {
                                new BddScenario
                                {
                                    ScenarioTitle = "Applicant meets DTI threshold",
                                    Given = "A credit score > 700 and DTI < 43%",
                                    When = "The validation is executed",
                                    Then = "Return APPROVED status"
                                }
                            }
                        }
                    },
                    BusinessRulesAndFormulas = new List<BusinessRuleSpecification>
                    {
                        new BusinessRuleSpecification
                        {
                            RuleId = "BR-01",
                            RuleTitle = "Max DTI Ratio",
                            FormalLogicOrFormula = "DTI = (MonthlyDebt / MonthlyIncome) * 100 <= 43.0",
                            Category = "Calculation"
                        }
                    },
                    StateMachines = new List<DomainStateMachine>
                    {
                        new DomainStateMachine
                        {
                            EntityName = "Loan",
                            InitialState = "Draft",
                            States = new List<string> { "Draft", "Submitted", "Approved", "Declined" },
                            Transitions = new List<StateTransition>
                            {
                                new StateTransition { FromState = "Draft", TriggerEvent = "Submit", ToState = "Submitted" }
                            }
                        }
                    },
                    DataDictionary = new List<DataDictionaryEntity>
                    {
                        new DataDictionaryEntity
                        {
                            EntityName = "LoanApplication",
                            Description = "Core loan record",
                            Fields = new List<DataDictionaryField>
                            {
                                new DataDictionaryField { Name = "id", DataType = "uuid", Required = true },
                                new DataDictionaryField { Name = "principalAmount", DataType = "decimal", Required = true }
                            }
                        }
                    },
                    Invariants = new List<SystemInvariant>
                    {
                        new SystemInvariant
                        {
                            Id = "INV-01",
                            Description = "Principal must be strictly positive",
                            EnforcementMechanism = "Transactional Boundary",
                            ConcurrencyRequirement = "Serializable"
                        }
                    }
                }
            }
        };

        Assert.NotNull(crd.Spec.FunctionalSpecs.BusinessRulesAndFormulas);
        Assert.Single(crd.Spec.FunctionalSpecs.BusinessRulesAndFormulas);
        Assert.Single(crd.Spec.FunctionalSpecs.StateMachines);
        Assert.Single(crd.Spec.FunctionalSpecs.DataDictionary);
        Assert.Single(crd.Spec.FunctionalSpecs.Invariants);
        Assert.Single(crd.Spec.FunctionalSpecs.UseCases[0].AcceptanceScenarios);
    }

    [Fact]
    public void OsvVulnerabilityClient_EnrichesVerificationProperties()
    {
        var comp = new CycloneDxComponent
        {
            Name = "Serilog",
            Version = "4.3.1",
            Purl = "pkg:nuget/Serilog@4.3.1"
        };

        var emptyVulns = new List<CycloneDxVulnerability>();
        OsvVulnerabilityClient.EnrichComponentAuditProperties(comp, emptyVulns, "NuGet");

        Assert.Contains(comp.Properties, p => p.Name == "atlas:osv:status" && p.Value == "CLEAN");
        Assert.Contains(comp.Properties, p => p.Name == "atlas:osv:cveCount" && p.Value == "0");
        Assert.Contains(comp.Properties, p => p.Name == "atlas:osv:url" && p.Value.Contains("osv.dev"));
        Assert.Contains(comp.Properties, p => p.Name == "atlas:osv:lastAudited");
    }

    [Fact]
    public async Task DepsDevClient_EnrichesVerifiedSpdxLicenseAndLifecycle()
    {
        var client = new DepsDevClient();
        var comp = new CycloneDxComponent
        {
            Name = "Serilog",
            Version = "4.3.1",
            Purl = "pkg:nuget/Serilog@4.3.1"
        };

        await client.EnrichComponentAsync(comp, "NuGet");

        Assert.NotEmpty(comp.Licenses);
        var lic = comp.Licenses.First().License?.Name;
        Assert.Equal("Apache-2.0", lic);
        Assert.Contains(comp.Properties, p => p.Name == "atlas:license:verifiedBy" && p.Value == "Google deps.dev");
        Assert.Contains(comp.Properties, p => p.Name == "atlas:lifecycle:latestVersion");
    }

    [Fact]
    public async Task CycloneDxGenerator_GeneratesCorrectPurlsForNpmAndNuGet()
    {
        var skeleton = new CodebaseSkeleton
        {
            RepoName = "MultiEcoService",
            Manifests = new List<ScannedManifest>
            {
                new ScannedManifest
                {
                    ManifestType = "DotNetCsproj",
                    ExtractedPackages = new List<PackageDependency>
                    {
                        new PackageDependency { Name = "Serilog", Version = "4.3.1" }
                    }
                },
                new ScannedManifest
                {
                    ManifestType = "NodePackageJson",
                    ExtractedPackages = new List<PackageDependency>
                    {
                        new PackageDependency { Name = "@angular/animations", Version = "19.0.0" },
                        new PackageDependency { Name = "rxjs", Version = "7.8.1" }
                    }
                }
            }
        };

        var generator = new CycloneDxGenerator();
        var (sbom, _) = await generator.GenerateAsync(skeleton);

        var ngAnim = sbom.Components.FirstOrDefault(c => c.Name == "@angular/animations");
        Assert.NotNull(ngAnim);
        Assert.Equal("pkg:npm/@angular/animations@19.0.0", ngAnim.Purl);

        var rxjs = sbom.Components.FirstOrDefault(c => c.Name == "rxjs");
        Assert.NotNull(rxjs);
        Assert.Equal("pkg:npm/rxjs@7.8.1", rxjs.Purl);

        var serilog = sbom.Components.FirstOrDefault(c => c.Name == "Serilog");
        Assert.NotNull(serilog);
        Assert.Equal("pkg:nuget/Serilog@4.3.1", serilog.Purl);
    }

    [Fact]
    public async Task OsvVulnerabilityClient_QueryByPurl_FindsKnownVulnerabilities()
    {
        var client = new OsvVulnerabilityClient();
        // Query known vulnerable Angular version via PURL
        var vulns = await client.QueryByPurlAsync("pkg:npm/@angular/core@17.0.0", forceRefresh: true);
        Assert.NotEmpty(vulns);
        Assert.Contains(vulns, v => v.Id.StartsWith("GHSA-") || v.Id.StartsWith("CVE-"));
    }
}
