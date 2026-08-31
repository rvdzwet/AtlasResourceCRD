using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core.Models;
using Atlas.Server.Graph;
using Atlas.Server.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Controllers;

/// <summary>
/// REST API for receiving runtime deployment events and querying infrastructure hosting topology.
/// Accepts direct HTTP payloads from ArgoCD sync hooks, CI/CD release pipelines (GitHub Actions, GitLab CI, Azure DevOps),
/// and legacy VM provisioning scripts (PowerShell, Ansible, curl).
/// </summary>
[ApiController]
[Route("api/v1/deployment")]
public sealed class DeploymentController : ControllerBase
{
    private readonly Neo4jGraphMapper _graphMapper;
    private readonly Neo4jGraphService _graphService;
    private readonly SpecDocumentRepository _specRepo;
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(
        Neo4jGraphMapper graphMapper,
        Neo4jGraphService graphService,
        SpecDocumentRepository specRepo,
        ILogger<DeploymentController> logger)
    {
        _graphMapper = graphMapper;
        _graphService = graphService;
        _specRepo = specRepo;
        _logger = logger;
    }

    /// <summary>
    /// Ingests a runtime deployment event from ArgoCD, GitOps, or VM curl/Ansible script.
    /// </summary>
    [HttpPost("report")]
    public async Task<ActionResult<DeploymentReportResponse>> ReportDeployment([FromBody] DeploymentReportRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return BadRequest(new DeploymentReportResponse
            {
                Success = false,
                Message = "Invalid deployment report: 'serviceName' is required."
            });
        }

        var svcName = request.ServiceName.Trim();
        var env = string.IsNullOrWhiteSpace(request.Environment) ? "production" : request.Environment.ToLowerInvariant();
        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "Kubernetes" : request.Platform;

        _logger.LogInformation("[DeploymentController] Received deployment report: {Service} -> {Env} ({Platform} via {Tool})",
            svcName, env, platform, request.Tool);

        try
        {
            // 1. Map into Neo4j graph nodes and relationships
            var graphUpdated = await _graphMapper.MapDeploymentReportAsync(request);

            // 2. Synchronize DeploymentSpec on stored CRD manifest in Neo4j (if service is registered)
            var existingResource = _specRepo.GetByName(svcName);
            if (existingResource != null)
            {
                existingResource.Spec ??= new AtlasResourceSpec();
                existingResource.Spec.Deployment ??= new DeploymentSpec
                {
                    PrimaryPlatform = platform,
                    Environments = new List<EnvironmentDeployment>()
                };

                existingResource.Spec.Deployment.PrimaryPlatform = platform;
                var envDeploy = existingResource.Spec.Deployment.Environments.FirstOrDefault(e => string.Equals(e.Environment, env, StringComparison.OrdinalIgnoreCase));
                if (envDeploy == null)
                {
                    envDeploy = new EnvironmentDeployment { Environment = env };
                    existingResource.Spec.Deployment.Environments.Add(envDeploy);
                }

                envDeploy.Platform = platform;
                envDeploy.ClusterOrHost = platform.Equals("Kubernetes", StringComparison.OrdinalIgnoreCase)
                    ? (request.Cluster ?? "default-cluster")
                    : (request.Host ?? request.IpAddress ?? "default-host");
                envDeploy.NamespaceOrPath = request.Namespace ?? "";
                envDeploy.IpAddress = request.IpAddress;
                envDeploy.Os = request.Os;
                envDeploy.DeployedBy = request.DeployedBy ?? "automated-pipeline";
                envDeploy.LastDeployedAt = request.Timestamp ?? DateTime.UtcNow;

                if (request.Ingress != null)
                {
                    envDeploy.Ingress = new IngressConfig
                    {
                        Exposure = request.Ingress.Exposure ?? "InternalOnly",
                        PublicUrl = request.Ingress.PublicUrl,
                        InternalHost = request.Ingress.InternalHost,
                        TlsTermination = request.Ingress.TlsTermination
                    };
                }

                envDeploy.Orchestration = new OrchestrationConfig
                {
                    Tool = request.Tool ?? "ArgoCD",
                    ImageOrArtifact = request.ImageOrArtifact,
                    GitCommit = request.GitCommit,
                    Replicas = request.Replicas
                };

                _specRepo.Save(existingResource);
            }

            var targetDesc = platform.Equals("Kubernetes", StringComparison.OrdinalIgnoreCase)
                ? $"K8s Namespace: {request.Namespace ?? "default"} ({request.Cluster ?? "default-cluster"})"
                : $"VM Host: {request.Host ?? request.IpAddress ?? "unknown"}";

            return Ok(new DeploymentReportResponse
            {
                Success = true,
                Message = $"Deployment reported successfully for '{svcName}' to {env} on {targetDesc}",
                ServiceName = svcName,
                Environment = env,
                Platform = platform,
                HostingTarget = targetDesc,
                GraphUpdated = graphUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DeploymentController] Error processing deployment report for '{Service}'", svcName);
            return StatusCode(500, new DeploymentReportResponse
            {
                Success = false,
                Message = $"Internal error processing deployment report: {ex.Message}",
                ServiceName = svcName,
                Environment = env,
                Platform = platform
            });
        }
    }

    /// <summary>
    /// Retrieves live deployment coordinates and co-located microservices for a given service.
    /// </summary>
    [HttpGet("service/{name}")]
    public async Task<ActionResult<ServiceDeploymentSummary>> GetServiceDeployment(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Service name is required.");

        var deployments = await _graphService.GetDeploymentsForServiceAsync(name);
        var coLocated = await _graphService.GetCoLocatedServicesAsync(name);

        var primaryPlatform = deployments.FirstOrDefault()?.Platform ?? "Kubernetes";

        return Ok(new ServiceDeploymentSummary
        {
            ServiceName = name,
            PrimaryPlatform = primaryPlatform,
            Deployments = deployments,
            CoLocatedServices = coLocated
        });
    }

    /// <summary>
    /// Retrieves overall infrastructure topology (clusters, namespaces, hosts, environments).
    /// </summary>
    [HttpGet("infrastructure")]
    public async Task<ActionResult<InfrastructureTopologySummary>> GetInfrastructureTopology()
    {
        var summary = new InfrastructureTopologySummary();

        try
        {
            // 1. Clusters & Namespaces
            var clusterCypher = """
                MATCH (cl:Cluster)
                OPTIONAL MATCH (ns:Namespace)-[:IN_CLUSTER]->(cl)
                OPTIONAL MATCH (s:Service)-[:DEPLOYED_TO]->(ns)
                RETURN cl.name AS cluster, cl.region AS region, collect(DISTINCT ns.name) AS namespaces, collect(DISTINCT s.name) AS services
                """;

            var clusterRecords = await _graphService.QueryAsync(clusterCypher);
            foreach (var r in clusterRecords)
            {
                var nsList = (r.GetValueOrDefault("namespaces") as IEnumerable<object>)?.Select(x => x.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList() ?? new List<string>();
                var svcList = (r.GetValueOrDefault("services") as IEnumerable<object>)?.Select(x => x.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList() ?? new List<string>();

                summary.Clusters.Add(new ClusterNodeDto
                {
                    Name = r.GetValueOrDefault("cluster")?.ToString() ?? "unknown",
                    Region = r.GetValueOrDefault("region")?.ToString() ?? "westeurope",
                    Namespaces = nsList,
                    HostedServices = svcList
                });
            }

            // 2. Hosts / VMs
            var hostCypher = """
                MATCH (h:Host)
                OPTIONAL MATCH (s:Service)-[:HOSTED_ON]->(h)
                RETURN h.name AS host, h.ip AS ip, h.os AS os, h.platform AS platform, collect(DISTINCT s.name) AS services
                """;

            var hostRecords = await _graphService.QueryAsync(hostCypher);
            foreach (var r in hostRecords)
            {
                var svcList = (r.GetValueOrDefault("services") as IEnumerable<object>)?.Select(x => x.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList() ?? new List<string>();

                summary.Hosts.Add(new HostNodeDto
                {
                    Name = r.GetValueOrDefault("host")?.ToString() ?? "unknown",
                    IpAddress = r.GetValueOrDefault("ip")?.ToString(),
                    Os = r.GetValueOrDefault("os")?.ToString(),
                    Platform = r.GetValueOrDefault("platform")?.ToString() ?? "VirtualMachine",
                    HostedServices = svcList
                });
            }

            // 3. Environments
            var envCypher = """
                MATCH (e:Environment)
                OPTIONAL MATCH (cl:Cluster)-[:IN_ENVIRONMENT]->(e)
                OPTIONAL MATCH (h:Host)-[:IN_ENVIRONMENT]->(e)
                OPTIONAL MATCH (s:Service)-[d:DEPLOYED_TO|HOSTED_ON { environment: e.name }]->()
                RETURN e.name AS env, count(DISTINCT cl) AS clusterCount, count(DISTINCT h) AS hostCount, count(DISTINCT s) AS serviceCount
                """;

            var envRecords = await _graphService.QueryAsync(envCypher);
            foreach (var r in envRecords)
            {
                summary.Environments.Add(new EnvironmentSummaryDto
                {
                    Name = r.GetValueOrDefault("env")?.ToString() ?? "production",
                    ClusterCount = Convert.ToInt32(r.GetValueOrDefault("clusterCount") ?? 0),
                    HostCount = Convert.ToInt32(r.GetValueOrDefault("hostCount") ?? 0),
                    ServiceCount = Convert.ToInt32(r.GetValueOrDefault("serviceCount") ?? 0)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DeploymentController] Error querying infrastructure topology");
        }

        return Ok(summary);
    }
}
