using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Core.Models;
using Atlas.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Graph;

public sealed class Neo4jGraphMapper
{
    private readonly Neo4jGraphService _graphService;
    private readonly ILogger<Neo4jGraphMapper> _logger;

    public Neo4jGraphMapper(Neo4jGraphService graphService, ILogger<Neo4jGraphMapper> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<bool> MapResourceToGraphAsync(AtlasResource resource)
    {
        if (resource?.Metadata == null || resource.Spec == null) return false;

        var name = resource.Metadata.Name;
        var tier = resource.Spec.ComponentOverview?.Tier ?? "Backend";
        var domain = resource.Metadata.Annotations != null && resource.Metadata.Annotations.TryGetValue("atlas.io/domain", out var dom) 
            ? dom 
            : (name.Contains("hypotheken", StringComparison.OrdinalIgnoreCase) ? "Dutch Mortgage & Lending Core" : "Smart Home & IoT Automation");
        var version = resource.Metadata.Annotations != null && resource.Metadata.Annotations.TryGetValue("atlas.io/version", out var v) ? v : "1.0.0";
        var sigStars = resource.Spec.Quality?.SigStars ?? 4.0;
        var grade = resource.Spec.CodeReview?.ReviewGrade ?? "A";
        var riskLevel = resource.Spec.RiskSummary?.OverallRiskLevel ?? "Low";
        var repoUrl = resource.Metadata.Annotations != null && resource.Metadata.Annotations.TryGetValue("atlas.io/git-remote", out var r) ? r : "";
        var description = resource.Spec.ComponentOverview?.Description ?? "";

        var rawYaml = CrdYamlSerializer.SerializeYaml(resource);
        var rawJson = CrdYamlSerializer.SerializeJson(resource);

        _logger.LogInformation("[Neo4jGraphMapper] Mapping service '{Name}' (Tier: {Tier}, Domain: {Domain}) to Neo4j graph", name, tier, domain);

        try
        {
            // 1. Upsert Service Node with rawYaml and full property payload
            var serviceCypher = """
                MERGE (s:Service { name: $name })
                SET s.tier = $tier,
                    s.domain = $domain,
                    s.version = $version,
                    s.sigStars = $sigStars,
                    s.reviewGrade = $grade,
                    s.riskLevel = $riskLevel,
                    s.repoUrl = $repoUrl,
                    s.description = $description,
                    s.rawYaml = $rawYaml,
                    s.rawJson = $rawJson,
                    s.updatedAt = datetime()
                RETURN s
                """;

            await _graphService.ExecuteCypherAsync(serviceCypher, new
            {
                name,
                tier,
                domain,
                version,
                sigStars,
                grade,
                riskLevel,
                repoUrl,
                description,
                rawYaml,
                rawJson
            });

            // 2. Map Endpoints
            if (resource.Spec.ApiContracts?.Endpoints != null)
            {
                foreach (var ep in resource.Spec.ApiContracts.Endpoints)
                {
                    if (string.IsNullOrWhiteSpace(ep.Path)) continue;

                    var epCypher = """
                        MATCH (s:Service { name: $serviceName })
                        MERGE (e:Endpoint { path: $path, method: $method })
                        SET e.description = $description,
                            e.authRequired = $authRequired,
                            e.responseType = $responseType
                        MERGE (s)-[:EXPOSES]->(e)
                        """;

                    await _graphService.ExecuteCypherAsync(epCypher, new
                    {
                        serviceName = name,
                        path = ep.Path,
                        method = ep.Method ?? "GET",
                        description = ep.Description ?? "",
                        authRequired = ep.AuthRequired,
                        responseType = ep.ResponseType ?? ""
                    });
                }
            }

            // 3. Map External and Internal Dependencies
            if (resource.Spec.Dependencies?.ExternalApis != null)
            {
                foreach (var ext in resource.Spec.Dependencies.ExternalApis)
                {
                    if (string.IsNullOrWhiteSpace(ext.Name)) continue;

                    var extCypher = """
                        MATCH (s:Service { name: $serviceName })
                        MERGE (d:ExternalApi { name: $name })
                        SET d.protocolOrHost = $protocol,
                            d.purpose = $purpose,
                            d.criticality = $criticality
                        MERGE (s)-[:CALLS { criticality: $criticality }]->(d)
                        """;

                    await _graphService.ExecuteCypherAsync(extCypher, new
                    {
                        serviceName = name,
                        name = ext.Name,
                        protocol = ext.ProtocolOrHost ?? "",
                        purpose = ext.Purpose ?? "",
                        criticality = ext.Criticality ?? "Medium"
                    });
                }
            }

            if (resource.Spec.Dependencies?.InternalServices != null)
            {
                foreach (var dep in resource.Spec.Dependencies.InternalServices)
                {
                    if (string.IsNullOrWhiteSpace(dep.Name)) continue;

                    var depCypher = """
                        MATCH (s:Service { name: $serviceName })
                        MERGE (target:Service { name: $targetName })
                        MERGE (s)-[:DEPENDS_ON { criticality: $criticality, purpose: $purpose }]->(target)
                        """;

                    await _graphService.ExecuteCypherAsync(depCypher, new
                    {
                        serviceName = name,
                        targetName = dep.Name,
                        purpose = dep.Purpose ?? "",
                        criticality = dep.Criticality ?? "High"
                    });
                }
            }

            // 4. Map Event Topics / Queues
            if (resource.Spec.ApiContracts?.Events != null)
            {
                foreach (var ev in resource.Spec.ApiContracts.Events)
                {
                    if (string.IsNullOrWhiteSpace(ev.TopicOrQueue)) continue;

                    var isPub = string.Equals(ev.Action, "Publish", StringComparison.OrdinalIgnoreCase);
                    var evCypher = isPub
                        ? """
                          MATCH (s:Service { name: $serviceName })
                          MERGE (t:EventTopic { topicOrQueue: $topic })
                          SET t.payloadType = $payloadType
                          MERGE (s)-[:PRODUCES]->(t)
                          """
                        : """
                          MATCH (s:Service { name: $serviceName })
                          MERGE (t:EventTopic { topicOrQueue: $topic })
                          SET t.payloadType = $payloadType
                          MERGE (s)-[:CONSUMES]->(t)
                          """;

                    await _graphService.ExecuteCypherAsync(evCypher, new
                    {
                        serviceName = name,
                        topic = ev.TopicOrQueue,
                        payloadType = ev.PayloadType ?? ""
                    });
                }
            }

            // 5. Map Capabilities & Use Cases
            if (resource.Spec.FunctionalSpecs?.Capabilities != null)
            {
                foreach (var cap in resource.Spec.FunctionalSpecs.Capabilities)
                {
                    if (string.IsNullOrWhiteSpace(cap.Name)) continue;

                    var capCypher = """
                        MATCH (s:Service { name: $serviceName })
                        MERGE (c:Capability { name: $name })
                        SET c.description = $description
                        MERGE (s)-[:IMPLEMENTS]->(c)
                        """;

                    await _graphService.ExecuteCypherAsync(capCypher, new
                    {
                        serviceName = name,
                        name = cap.Name,
                        description = cap.Description ?? ""
                    });
                }
            }

            // 6. Map Threats
            if (resource.Spec.ThreatModel?.Threats != null)
            {
                foreach (var t in resource.Spec.ThreatModel.Threats)
                {
                    if (string.IsNullOrWhiteSpace(t.Id)) continue;

                    var threatCypher = """
                        MATCH (s:Service { name: $serviceName })
                        MERGE (th:Threat { id: $id, serviceName: $serviceName })
                        SET th.strideCategory = $category,
                            th.targetAsset = $targetAsset,
                            th.severity = $severity,
                            th.mitigationControl = $mitigation
                        MERGE (s)-[:HAS_THREAT]->(th)
                        """;

                    await _graphService.ExecuteCypherAsync(threatCypher, new
                    {
                        serviceName = name,
                        id = t.Id,
                        category = t.StrideCategory ?? "General",
                        targetAsset = t.TargetAsset ?? "",
                        severity = t.Severity ?? "Medium",
                        mitigation = t.MitigationControl ?? ""
                    });
                }
            }

            // 8. Map Historical Snapshot Node
            var snapshotRecord = ServiceSnapshotRecord.FromResource(resource);
            var snapshotCypher = """
                MATCH (s:Service { name: $serviceName })
                MERGE (snap:ServiceSnapshot { snapshotId: $snapshotId, serviceName: $serviceName })
                SET snap.timestamp = datetime(),
                    snap.commit = $commit,
                    snap.sigStars = $sigStars,
                    snap.techDebtRatio = $techDebtRatio,
                    snap.maintainabilityIndex = $maintainabilityIndex,
                    snap.threatCount = $threatCount,
                    snap.codeSmellCount = $codeSmellCount,
                    snap.totalLinesOfCode = $totalLinesOfCode
                MERGE (s)-[:HAS_HISTORICAL_SNAPSHOT]->(snap)
                """;

            await _graphService.ExecuteCypherAsync(snapshotCypher, new
            {
                serviceName = name,
                snapshotId = snapshotRecord.SnapshotId,
                commit = snapshotRecord.GitCommitSha ?? "head",
                sigStars = snapshotRecord.SigStars,
                techDebtRatio = snapshotRecord.TechDebtRatio,
                maintainabilityIndex = snapshotRecord.MaintainabilityIndex,
                threatCount = snapshotRecord.ThreatCount,
                codeSmellCount = snapshotRecord.CodeSmellCount,
                totalLinesOfCode = snapshotRecord.TotalLinesOfCode
            });

            _logger.LogInformation("[Neo4jGraphMapper] Successfully mapped '{Name}' into Neo4j graph nodes and relationships", name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphMapper] Error mapping resource '{Name}' to Neo4j", name);
            return false;
        }
    }

    /// <summary>
    /// Completely removes a service and all its exclusively-owned graph nodes (endpoints, threats)
    /// and relationships from Neo4j. Shared nodes (ExternalApi, EventTopic, Capability) are
    /// preserved if still referenced by other services.
    /// </summary>
    public async Task<bool> DeleteServiceFromGraphAsync(string serviceName)
    {
        _logger.LogInformation("[Neo4jGraphMapper] Deleting service '{Name}' and all owned nodes from Neo4j graph", serviceName);

        try
        {
            // 1. Delete exclusively-owned Threat nodes (case-insensitive)
            await _graphService.ExecuteCypherAsync(
                "MATCH (th:Threat) WHERE toLower(th.serviceName) = toLower($name) DETACH DELETE th",
                new { name = serviceName });

            // 2. Delete Endpoint nodes exclusively exposed by this service
            await _graphService.ExecuteCypherAsync("""
                MATCH (s:Service)-[:EXPOSES]->(e:Endpoint)
                WHERE toLower(s.name) = toLower($name)
                AND NOT EXISTS { MATCH (other:Service)-[:EXPOSES]->(e) WHERE toLower(other.name) <> toLower($name) }
                DETACH DELETE e
                """, new { name = serviceName });

            // 3. Delete ExternalApi nodes exclusively called by this service
            await _graphService.ExecuteCypherAsync("""
                MATCH (s:Service)-[:CALLS]->(d:ExternalApi)
                WHERE toLower(s.name) = toLower($name)
                AND NOT EXISTS { MATCH (other:Service)-[:CALLS]->(d) WHERE toLower(other.name) <> toLower($name) }
                DETACH DELETE d
                """, new { name = serviceName });

            // 4. Delete EventTopic nodes exclusively used by this service
            await _graphService.ExecuteCypherAsync("""
                MATCH (s:Service)-[:PRODUCES|CONSUMES]->(t:EventTopic)
                WHERE toLower(s.name) = toLower($name)
                AND NOT EXISTS { MATCH (other:Service)-[:PRODUCES|CONSUMES]->(t) WHERE toLower(other.name) <> toLower($name) }
                DETACH DELETE t
                """, new { name = serviceName });

            // 5. Delete Capability nodes exclusively implemented by this service
            await _graphService.ExecuteCypherAsync("""
                MATCH (s:Service)-[:IMPLEMENTS]->(c:Capability)
                WHERE toLower(s.name) = toLower($name)
                AND NOT EXISTS { MATCH (other:Service)-[:IMPLEMENTS]->(c) WHERE toLower(other.name) <> toLower($name) }
                DETACH DELETE c
                """, new { name = serviceName });

            // 6. Detach and delete the Service node itself
            await _graphService.ExecuteCypherAsync(
                "MATCH (s:Service) WHERE toLower(s.name) = toLower($name) DETACH DELETE s",
                new { name = serviceName });

            _logger.LogInformation("[Neo4jGraphMapper] Successfully deleted service '{Name}' and orphaned dependent nodes from Neo4j graph", serviceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphMapper] Error deleting service '{Name}' from Neo4j", serviceName);
            return false;
        }
    }

    public async Task<bool> MapDeploymentReportAsync(DeploymentReportRequest report)
    {
        if (report == null || string.IsNullOrWhiteSpace(report.ServiceName)) return false;

        var svcName = report.ServiceName;
        var env = string.IsNullOrWhiteSpace(report.Environment) ? "production" : report.Environment.ToLowerInvariant();
        var platform = string.IsNullOrWhiteSpace(report.Platform) ? "Kubernetes" : report.Platform;
        var tool = report.Tool ?? "ArgoCD";
        var commit = report.GitCommit ?? "";
        var image = report.ImageOrArtifact ?? "";
        var exposure = report.Ingress?.Exposure ?? "InternalOnly";
        var publicUrl = report.Ingress?.PublicUrl ?? "";
        var internalHost = report.Ingress?.InternalHost ?? "";
        var deployedBy = report.DeployedBy ?? "automated-pipeline";
        var timestamp = (report.Timestamp ?? DateTime.UtcNow).ToString("o");

        _logger.LogInformation("[Neo4jGraphMapper] Ingesting deployment report for '{Service}' -> {Env} ({Platform} via {Tool})",
            svcName, env, platform, tool);

        try
        {
            // 1. Ensure Service and Environment nodes exist
            await _graphService.ExecuteCypherAsync("""
                MERGE (s:Service { name: $svcName })
                MERGE (e:Environment { name: $env })
                """, new { svcName, env });

            if (platform.Equals("Kubernetes", StringComparison.OrdinalIgnoreCase))
            {
                var cluster = string.IsNullOrWhiteSpace(report.Cluster) ? "k8s-cluster-default" : report.Cluster;
                var ns = string.IsNullOrWhiteSpace(report.Namespace) ? "default" : report.Namespace;

                var k8sCypher = """
                    MATCH (s:Service { name: $svcName }), (e:Environment { name: $env })
                    MERGE (cl:Cluster { name: $cluster })
                    SET cl.region = $region
                    MERGE (cl)-[:IN_ENVIRONMENT]->(e)
                    MERGE (n:Namespace { name: $ns, cluster: $cluster })
                    MERGE (n)-[:IN_CLUSTER]->(cl)
                    MERGE (s)-[d:DEPLOYED_TO { environment: $env }]->(n)
                    SET d.platform = 'Kubernetes',
                        d.tool = $tool,
                        d.image = $image,
                        d.commit = $commit,
                        d.replicas = $replicas,
                        d.exposure = $exposure,
                        d.publicUrl = $publicUrl,
                        d.internalHost = $internalHost,
                        d.deployedBy = $deployedBy,
                        d.deployedAt = datetime($timestamp)
                    """;

                await _graphService.ExecuteCypherAsync(k8sCypher, new
                {
                    svcName,
                    env,
                    cluster,
                    ns,
                    region = report.Region ?? "westeurope",
                    tool,
                    image,
                    commit,
                    replicas = report.Replicas,
                    exposure,
                    publicUrl,
                    internalHost,
                    deployedBy,
                    timestamp
                });
            }
            else
            {
                // Virtual Machine, IIS, Bare-Metal, Docker Compose
                var host = !string.IsNullOrWhiteSpace(report.Host) 
                    ? report.Host 
                    : (!string.IsNullOrWhiteSpace(report.IpAddress) ? report.IpAddress : $"vm-{svcName}-01");
                var ip = report.IpAddress ?? "";
                var os = report.Os ?? (platform.Equals("IIS", StringComparison.OrdinalIgnoreCase) ? "Windows Server 2022" : "Ubuntu 22.04 LTS");

                var vmCypher = """
                    MATCH (s:Service { name: $svcName }), (e:Environment { name: $env })
                    MERGE (h:Host { name: $host })
                    SET h.ip = $ip,
                        h.os = $os,
                        h.platform = $platform
                    MERGE (h)-[:IN_ENVIRONMENT]->(e)
                    MERGE (s)-[d:HOSTED_ON { environment: $env }]->(h)
                    SET d.platform = $platform,
                        d.tool = $tool,
                        d.artifact = $image,
                        d.commit = $commit,
                        d.exposure = $exposure,
                        d.publicUrl = $publicUrl,
                        d.internalHost = $internalHost,
                        d.deployedBy = $deployedBy,
                        d.deployedAt = datetime($timestamp)
                    """;

                await _graphService.ExecuteCypherAsync(vmCypher, new
                {
                    svcName,
                    env,
                    host,
                    ip,
                    os,
                    platform,
                    tool,
                    image,
                    commit,
                    exposure,
                    publicUrl,
                    internalHost,
                    deployedBy,
                    timestamp
                });
            }

            _logger.LogInformation("🟢 [Neo4jGraphMapper] Successfully mapped deployment topology for '{Service}' ({Env}) in Neo4j", svcName, env);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphMapper] Error mapping deployment report for '{Service}'", svcName);
            return false;
        }
    }
}
