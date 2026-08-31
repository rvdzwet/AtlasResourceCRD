using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Atlas.Core.Models;

/// <summary>
/// Deployment report payload posted by ArgoCD webhooks, CI/CD pipelines (GitHub Actions, GitLab CI, Azure DevOps),
/// or VM provisioning scripts (PowerShell, Ansible, curl) to report where an application is hosted.
/// </summary>
public sealed class DeploymentReportRequest
{
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "production"; // production | staging | test | dev

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "Kubernetes"; // Kubernetes | VirtualMachine | IIS | DockerCompose | BareMetal | Serverless

    [JsonPropertyName("cluster")]
    public string? Cluster { get; set; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("ingress")]
    public IngressReportDto? Ingress { get; set; }

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "ArgoCD"; // ArgoCD | GitHubActions | GitLabCI | Ansible | OctopusDeploy | Manual

    [JsonPropertyName("imageOrArtifact")]
    public string? ImageOrArtifact { get; set; }

    [JsonPropertyName("gitCommit")]
    public string? GitCommit { get; set; }

    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 1;

    [JsonPropertyName("deployedBy")]
    public string? DeployedBy { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class IngressReportDto
{
    [JsonPropertyName("publicUrl")]
    public string? PublicUrl { get; set; }

    [JsonPropertyName("internalHost")]
    public string? InternalHost { get; set; }

    [JsonPropertyName("exposure")]
    public string Exposure { get; set; } = "InternalOnly"; // Public | InternalOnly | DMZ

    [JsonPropertyName("tlsTermination")]
    public string? TlsTermination { get; set; }
}

public sealed class DeploymentReportResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("hostingTarget")]
    public string HostingTarget { get; set; } = string.Empty;

    [JsonPropertyName("graphUpdated")]
    public bool GraphUpdated { get; set; }
}

public sealed class ServiceDeploymentSummary
{
    public string ServiceName { get; set; } = string.Empty;
    public string PrimaryPlatform { get; set; } = "Kubernetes";
    public List<EnvironmentDeployment> Deployments { get; set; } = new();
    public List<CoLocatedServiceSummary> CoLocatedServices { get; set; } = new();
}

public sealed class CoLocatedServiceSummary
{
    public string ServiceName { get; set; } = string.Empty;
    public string Tier { get; set; } = "Backend";
    public string SharedResource { get; set; } = string.Empty; // e.g. "Cluster: prod-aks" or "VM: 192.168.1.4"
    public string Environment { get; set; } = "production";
}

public sealed class InfrastructureTopologySummary
{
    public List<ClusterNodeDto> Clusters { get; set; } = new();
    public List<HostNodeDto> Hosts { get; set; } = new();
    public List<EnvironmentSummaryDto> Environments { get; set; } = new();
}

public sealed class ClusterNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public List<string> Namespaces { get; set; } = new();
    public List<string> HostedServices { get; set; } = new();
}

public sealed class HostNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Os { get; set; }
    public string Platform { get; set; } = "VirtualMachine";
    public List<string> HostedServices { get; set; } = new();
}

public sealed class EnvironmentSummaryDto
{
    public string Name { get; set; } = "production";
    public int ServiceCount { get; set; }
    public int ClusterCount { get; set; }
    public int HostCount { get; set; }
}
