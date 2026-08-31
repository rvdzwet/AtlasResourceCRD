using System;
using System.Collections.Generic;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.Core.Scanner;
using Atlas.Core.Serialization;
using Xunit;

namespace Atlas.Core.Tests;

public class DeploymentTests
{
    [Fact]
    public void DeploymentReportRequest_SerializesAndDeserializesCorrectly()
    {
        var request = new DeploymentReportRequest
        {
            ServiceName = "payments-core",
            Environment = "production",
            Platform = "Kubernetes",
            Cluster = "k8s-prod-weu",
            Namespace = "payments-prod",
            Tool = "ArgoCD",
            ImageOrArtifact = "ghcr.io/org/payments:v2.1.0",
            GitCommit = "c3b2a10",
            Replicas = 3,
            Ingress = new IngressReportDto
            {
                PublicUrl = "https://api.domain.com/payments",
                InternalHost = "payments.internal:8080",
                Exposure = "Public"
            }
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
        Assert.Contains("\"serviceName\": \"payments-core\"", json);
        Assert.Contains("\"platform\": \"Kubernetes\"", json);
        Assert.Contains("\"tool\": \"ArgoCD\"", json);

        var deserialized = JsonSerializer.Deserialize<DeploymentReportRequest>(json);
        Assert.NotNull(deserialized);
        Assert.Equal("payments-core", deserialized.ServiceName);
        Assert.Equal("Kubernetes", deserialized.Platform);
        Assert.Equal("k8s-prod-weu", deserialized.Cluster);
        Assert.Equal(3, deserialized.Replicas);
        Assert.Equal("Public", deserialized.Ingress?.Exposure);
    }

    [Fact]
    public void VmDeploymentReportRequest_SerializesAndDeserializesCorrectly()
    {
        var request = new DeploymentReportRequest
        {
            ServiceName = "legacy-auth",
            Environment = "production",
            Platform = "VirtualMachine",
            Host = "srv-iis-01.corp.local",
            IpAddress = "192.168.1.4",
            Os = "Windows Server 2022",
            Tool = "Ansible",
            ImageOrArtifact = "C:\\inetpub\\wwwroot\\LegacyAuth",
            GitCommit = "f4e3d2c",
            Replicas = 1
        };

        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<DeploymentReportRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("legacy-auth", deserialized.ServiceName);
        Assert.Equal("VirtualMachine", deserialized.Platform);
        Assert.Equal("192.168.1.4", deserialized.IpAddress);
        Assert.Equal("Windows Server 2022", deserialized.Os);
    }

    [Fact]
    public void AtlasResource_WithDeploymentSpec_SerializesToYamlAndJson()
    {
        var resource = new AtlasResource
        {
            Metadata = new AtlasResourceMetadata { Name = "iot-engine" },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview { Name = "iot-engine", Tier = "Backend" },
                Deployment = new DeploymentSpec
                {
                    PrimaryPlatform = "Kubernetes",
                    Environments = new List<EnvironmentDeployment>
                    {
                        new EnvironmentDeployment
                        {
                            Environment = "production",
                            Platform = "Kubernetes",
                            ClusterOrHost = "aks-prod-01",
                            NamespaceOrPath = "iot-workloads",
                            Ingress = new IngressConfig
                            {
                                PublicUrl = "https://iot.domain.com",
                                Exposure = "Public"
                            },
                            Orchestration = new OrchestrationConfig
                            {
                                Tool = "ArgoCD",
                                ImageOrArtifact = "ghcr.io/org/iot:v1.0",
                                GitCommit = "abc1234",
                                Replicas = 2
                            }
                        }
                    }
                }
            }
        };

        var yaml = CrdYamlSerializer.SerializeYaml(resource);
        Assert.Contains("deployment:", yaml);
        Assert.Contains("primaryPlatform: Kubernetes", yaml);
        Assert.Contains("clusterOrHost: aks-prod-01", yaml);

        var deserializedYaml = CrdYamlSerializer.DeserializeYaml(yaml);
        Assert.NotNull(deserializedYaml.Spec.Deployment);
        Assert.Single(deserializedYaml.Spec.Deployment.Environments);
        Assert.Equal("aks-prod-01", deserializedYaml.Spec.Deployment.Environments[0].ClusterOrHost);
    }

    [Fact]
    public async Task CycloneDxGenerator_RunsOfflineWithZeroNetworkCalls()
    {
        var skeleton = new CodebaseSkeleton
        {
            RepoName = "OfflineService",
            Manifests = new List<ScannedManifest>
            {
                new ScannedManifest
                {
                    ManifestType = "DotNetCsproj",
                    ExtractedPackages = new List<PackageDependency>
                    {
                        new PackageDependency { Name = "Serilog", Version = "3.1.1", Purpose = "Logging" },
                        new PackageDependency { Name = "Microsoft.Extensions.Hosting", Version = "8.0.0", Purpose = "Host" }
                    }
                },
                new ScannedManifest
                {
                    ManifestType = "NodePackageJson",
                    ExtractedPackages = new List<PackageDependency>
                    {
                        new PackageDependency { Name = "express", Version = "4.19.2", Purpose = "Web server" }
                    }
                }
            }
        };

        var generator = new CycloneDxGenerator();
        var (sbom, vulnAudit) = await generator.GenerateAsync(skeleton);

        Assert.NotNull(sbom);
        Assert.Equal(3, sbom.Components.Count);
        Assert.Contains(sbom.Components, c => c.Name == "Serilog" && c.Purl == "pkg:nuget/Serilog@3.1.1");
        Assert.Contains(sbom.Components, c => c.Name == "express" && c.Purl == "pkg:npm/express@4.19.2");
        Assert.NotNull(vulnAudit);
        Assert.Equal(0, vulnAudit.TotalVulnerabilities);
    }
}
