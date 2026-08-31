using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Atlas.Server.Graph;

public sealed class Neo4jGraphService : IAsyncDisposable
{
    private readonly IDriver? _driver;
    private readonly ILogger<Neo4jGraphService> _logger;
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public Neo4jGraphService(IConfiguration config, ILogger<Neo4jGraphService> logger)
    {
        _logger = logger;
        var uri = config["Neo4j:Uri"] ?? Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        var user = config["Neo4j:User"] ?? Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        var password = config["Neo4j:Password"] ?? Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "";

        try
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
            _logger.LogInformation("[Neo4jGraphService] Initialized Neo4j driver configured for {Uri} (User: {User})", uri, user);
            // Run background initial check
            _ = Task.Run(async () => await TestConnectionAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Neo4jGraphService] Could not create Neo4j driver for {Uri}", uri);
            _isConnected = false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_driver == null)
        {
            _isConnected = false;
            return false;
        }

        try
        {
            await _driver.VerifyConnectivityAsync();
            _isConnected = true;
            _logger.LogInformation("[Neo4jGraphService] 🟢 Verified live connection to Neo4j database.");
            _ = Task.Run(async () => await InitializeSchemaConstraintsAndIndexesAsync());
            return true;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogWarning(ex, "[Neo4jGraphService] Connection verification to Neo4j failed.");
            return false;
        }
    }

    public async Task InitializeSchemaConstraintsAndIndexesAsync()
    {
        if (!_isConnected) return;
        var queries = new[]
        {
            "CREATE CONSTRAINT service_name_unique IF NOT EXISTS FOR (s:Service) REQUIRE s.name IS UNIQUE",
            "CREATE INDEX service_tier_idx IF NOT EXISTS FOR (s:Service) ON (s.tier)",
            "CREATE INDEX endpoint_path_method_idx IF NOT EXISTS FOR (e:Endpoint) ON (e.path, e.method)",
            "CREATE INDEX database_name_idx IF NOT EXISTS FOR (d:Database) ON (d.name)",
            "CREATE INDEX topic_name_idx IF NOT EXISTS FOR (t:Topic) ON (t.name)"
        };

        foreach (var q in queries)
        {
            try
            {
                await ExecuteCypherAsync(q);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Neo4jGraphService] Error applying schema constraint/index: {Query}", q);
            }
        }
        _logger.LogInformation("[Neo4jGraphService] 🚀 Schema constraints & graph indexes initialized for high-scale fleet operations.");
    }

    public async Task<IResultCursor?> ExecuteCypherAsync(string cypher, object? parameters = null)
    {
        if (_driver == null) return null;
        try
        {
            await using var session = _driver.AsyncSession();
            return parameters != null
                ? await session.RunAsync(cypher, parameters)
                : await session.RunAsync(cypher);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error executing Cypher query: {Cypher}", cypher);
            return null;
        }
    }

    public async Task<List<Dictionary<string, object>>> QueryAsync(string cypher, object? parameters = null)
    {
        var records = new List<Dictionary<string, object>>();
        if (_driver == null) return records;

        try
        {
            await using var session = _driver.AsyncSession();
            var cursor = parameters != null
                ? await session.RunAsync(cypher, parameters)
                : await session.RunAsync(cypher);

            while (await cursor.FetchAsync())
            {
                var dict = new Dictionary<string, object>();
                foreach (var key in cursor.Current.Keys)
                {
                    dict[key] = cursor.Current[key];
                }
                records.Add(dict);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error running query: {Cypher}", cypher);
        }

        return records;
    }

    public async Task EnsureSchemaConstraintsAsync()
    {
        if (_driver == null) return;
        try
        {
            var statements = new[]
            {
                "CREATE CONSTRAINT service_name_unique IF NOT EXISTS FOR (s:Service) REQUIRE s.name IS UNIQUE",
                "CREATE INDEX stitching_job_status IF NOT EXISTS FOR (j:StitchingJob) ON (j.status, j.requestedAt)",
                "CREATE INDEX file_cache_sha IF NOT EXISTS FOR (f:FileSummaryCache) ON (f.sha)",
                "CREATE INDEX synth_cache_lookup IF NOT EXISTS FOR (c:SynthesisCache) ON (c.repo, c.commitSha)"
            };

            foreach (var stmt in statements)
            {
                await using var session = _driver.AsyncSession();
                await session.RunAsync(stmt);
            }
            _logger.LogInformation("[Neo4jGraphService] ✅ Schema constraints & indexes verified.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Neo4jGraphService] Could not ensure schema constraints (Neo4j might be starting or read-only).");
        }
    }

    public async Task EnqueueStitchingJobAsync(string serviceName)
    {
        if (_driver == null) return;
        try
        {
            await using var session = _driver.AsyncSession();
            await session.RunAsync(
                "MERGE (j:StitchingJob { serviceName: $serviceName }) " +
                "ON CREATE SET j.status = 'PENDING', j.requestedAt = datetime() " +
                "ON MATCH SET j.status = 'PENDING', j.requestedAt = datetime(), j.leaseExpiry = null",
                new { serviceName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error enqueuing stitching job for {Service}", serviceName);
        }
    }

    public async Task<string?> ClaimNextStitchingJobAsync(string podId, TimeSpan leaseDuration)
    {
        if (_driver == null) return null;
        try
        {
            await using var session = _driver.AsyncSession();
            var result = await session.RunAsync(
                "MATCH (j:StitchingJob) " +
                "WHERE (j.status = 'PENDING' AND j.requestedAt <= datetime() - duration({seconds: 2})) " +
                "   OR (j.status = 'PROCESSING' AND j.leaseExpiry < datetime()) " +
                "WITH j ORDER BY j.requestedAt ASC LIMIT 1 " +
                "SET j.status = 'PROCESSING', j.claimedByPod = $podId, j.leaseExpiry = datetime() + duration({seconds: $leaseSeconds}) " +
                "RETURN j.serviceName AS serviceName",
                new { podId, leaseSeconds = (int)leaseDuration.TotalSeconds });

            if (await result.FetchAsync())
            {
                return result.Current["serviceName"].As<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Neo4jGraphService] Error claiming stitching job by pod {PodId}", podId);
        }
        return null;
    }

    public async Task CompleteStitchingJobAsync(string serviceName)
    {
        if (_driver == null) return;
        try
        {
            await using var session = _driver.AsyncSession();
            await session.RunAsync(
                "MATCH (j:StitchingJob { serviceName: $serviceName }) " +
                "SET j.status = 'COMPLETED', j.completedAt = datetime(), j.leaseExpiry = null",
                new { serviceName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error completing stitching job for {Service}", serviceName);
        }
    }

    public async Task ReconcileStitchedEdgesAsync(string serviceName, List<StitchedEdgeDto> edges)
    {
        if (_driver == null) return;
        try
        {
            await using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                // 1. Atomically delete all existing dynamically inferred edges owned by this service
                await tx.RunAsync(
                    "MATCH (s:Service { name: $serviceName }) " +
                    "OPTIONAL MATCH (s)-[r:STITCHED_CALLS|STITCHED_EVENT|POTENTIAL_DUPLICATE_OF { discoveredFromService: $serviceName }]-() " +
                    "DELETE r",
                    new { serviceName });

                // 2. Atomically create newly verified stitched edges
                foreach (var edge in edges)
                {
                    if (edge.EdgeType == "CALLS")
                    {
                        await tx.RunAsync(
                            "MATCH (src:Service { name: $srcName }), (tgt:Service { name: $tgtName }) " +
                            "MERGE (src)-[r:STITCHED_CALLS { endpoint: $endpoint, discoveredFromService: $discoveredFrom }]->(tgt) " +
                            "SET r.confidence = $confidence, r.protocol = $protocol, r.verifiedAt = datetime()",
                            new { 
                                srcName = edge.SourceServiceName, 
                                tgtName = edge.TargetServiceName, 
                                endpoint = edge.EndpointOrTopic ?? "", 
                                discoveredFrom = serviceName,
                                confidence = edge.Confidence,
                                protocol = edge.Protocol ?? "HTTP"
                            });
                    }
                    else if (edge.EdgeType == "EVENT")
                    {
                        await tx.RunAsync(
                            "MATCH (src:Service { name: $srcName }) " +
                            "MERGE (ev:EventTopic { name: $topic }) " +
                            "MERGE (src)-[r:STITCHED_EVENT { action: $action, discoveredFromService: $discoveredFrom }]->(ev) " +
                            "SET r.confidence = $confidence, r.verifiedAt = datetime()",
                            new { 
                                srcName = edge.SourceServiceName, 
                                topic = edge.EndpointOrTopic ?? "unknown-event", 
                                action = edge.Action ?? "PUB",
                                discoveredFrom = serviceName,
                                confidence = edge.Confidence
                            });
                    }
                    else if (edge.EdgeType == "DUPLICATE")
                    {
                        await tx.RunAsync(
                            "MATCH (src:Service { name: $srcName }), (tgt:Service { name: $tgtName }) " +
                            "MERGE (src)-[r:POTENTIAL_DUPLICATE_OF { discoveredFromService: $discoveredFrom }]->(tgt) " +
                            "SET r.reason = $reason, r.confidence = $confidence, r.verifiedAt = datetime()",
                            new { 
                                srcName = edge.SourceServiceName, 
                                tgtName = edge.TargetServiceName, 
                                reason = edge.Reason ?? "Shared responsibilities",
                                discoveredFrom = serviceName,
                                confidence = edge.Confidence
                            });
                    }
                }
            });
            _logger.LogInformation("[Neo4jGraphService] ⚡ Declaratively reconciled {Count} stitched edges for service {Service}", edges.Count, serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error reconciling stitched edges for {Service}", serviceName);
        }
    }

    public async Task<List<EnvironmentDeployment>> GetDeploymentsForServiceAsync(string serviceName)
    {
        var list = new List<EnvironmentDeployment>();
        if (string.IsNullOrWhiteSpace(serviceName)) return list;

        try
        {
            // 1. K8s deployments
            var k8sQuery = """
                MATCH (s:Service { name: $serviceName })-[d:DEPLOYED_TO]->(ns:Namespace)-[:IN_CLUSTER]->(cl:Cluster)
                RETURN d.environment AS environment, d.platform AS platform, cl.name AS cluster, ns.name AS namespace,
                       d.tool AS tool, d.image AS image, d.commit AS commit, d.exposure AS exposure,
                       d.publicUrl AS publicUrl, d.internalHost AS internalHost, d.replicas AS replicas,
                       d.deployedBy AS deployedBy, toString(d.deployedAt) AS deployedAt
                """;

            var k8sRecords = await QueryAsync(k8sQuery, new { serviceName });
            foreach (var r in k8sRecords)
            {
                list.Add(new EnvironmentDeployment
                {
                    Environment = r.GetValueOrDefault("environment")?.ToString() ?? "production",
                    Platform = "Kubernetes",
                    ClusterOrHost = r.GetValueOrDefault("cluster")?.ToString() ?? "",
                    NamespaceOrPath = r.GetValueOrDefault("namespace")?.ToString() ?? "",
                    DeployedBy = r.GetValueOrDefault("deployedBy")?.ToString(),
                    LastDeployedAt = DateTime.TryParse(r.GetValueOrDefault("deployedAt")?.ToString(), out var dt) ? dt : null,
                    Ingress = new IngressConfig
                    {
                        Exposure = r.GetValueOrDefault("exposure")?.ToString() ?? "InternalOnly",
                        PublicUrl = r.GetValueOrDefault("publicUrl")?.ToString(),
                        InternalHost = r.GetValueOrDefault("internalHost")?.ToString()
                    },
                    Orchestration = new OrchestrationConfig
                    {
                        Tool = r.GetValueOrDefault("tool")?.ToString() ?? "ArgoCD",
                        ImageOrArtifact = r.GetValueOrDefault("image")?.ToString(),
                        GitCommit = r.GetValueOrDefault("commit")?.ToString(),
                        Replicas = Convert.ToInt32(r.GetValueOrDefault("replicas") ?? 1)
                    }
                });
            }

            // 2. VM / Host deployments
            var vmQuery = """
                MATCH (s:Service { name: $serviceName })-[d:HOSTED_ON]->(h:Host)
                RETURN d.environment AS environment, d.platform AS platform, h.name AS host, h.ip AS ip, h.os AS os,
                       d.tool AS tool, d.artifact AS artifact, d.commit AS commit, d.exposure AS exposure,
                       d.publicUrl AS publicUrl, d.internalHost AS internalHost,
                       d.deployedBy AS deployedBy, toString(d.deployedAt) AS deployedAt
                """;

            var vmRecords = await QueryAsync(vmQuery, new { serviceName });
            foreach (var r in vmRecords)
            {
                list.Add(new EnvironmentDeployment
                {
                    Environment = r.GetValueOrDefault("environment")?.ToString() ?? "production",
                    Platform = r.GetValueOrDefault("platform")?.ToString() ?? "VirtualMachine",
                    ClusterOrHost = r.GetValueOrDefault("host")?.ToString() ?? "",
                    IpAddress = r.GetValueOrDefault("ip")?.ToString(),
                    Os = r.GetValueOrDefault("os")?.ToString(),
                    DeployedBy = r.GetValueOrDefault("deployedBy")?.ToString(),
                    LastDeployedAt = DateTime.TryParse(r.GetValueOrDefault("deployedAt")?.ToString(), out var dt) ? dt : null,
                    Ingress = new IngressConfig
                    {
                        Exposure = r.GetValueOrDefault("exposure")?.ToString() ?? "InternalOnly",
                        PublicUrl = r.GetValueOrDefault("publicUrl")?.ToString(),
                        InternalHost = r.GetValueOrDefault("internalHost")?.ToString()
                    },
                    Orchestration = new OrchestrationConfig
                    {
                        Tool = r.GetValueOrDefault("tool")?.ToString() ?? "Ansible",
                        ImageOrArtifact = r.GetValueOrDefault("artifact")?.ToString(),
                        GitCommit = r.GetValueOrDefault("commit")?.ToString(),
                        Replicas = 1
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error querying deployments for '{Service}'", serviceName);
        }

        return list;
    }

    public async Task<List<CoLocatedServiceSummary>> GetCoLocatedServicesAsync(string serviceName)
    {
        var list = new List<CoLocatedServiceSummary>();
        if (string.IsNullOrWhiteSpace(serviceName)) return list;

        try
        {
            // 1. Co-located on same VM Host
            var vmQuery = """
                MATCH (s:Service { name: $serviceName })-[:HOSTED_ON]->(h:Host)<-[:HOSTED_ON]-(other:Service)
                WHERE other.name <> $serviceName
                RETURN other.name AS serviceName, other.tier AS tier, h.name AS host, h.ip AS ip
                """;

            var vmRecords = await QueryAsync(vmQuery, new { serviceName });
            foreach (var r in vmRecords)
            {
                var ip = r.GetValueOrDefault("ip")?.ToString();
                var hostDesc = !string.IsNullOrWhiteSpace(ip)
                    ? $"VM: {r.GetValueOrDefault("host")} ({ip})"
                    : $"VM: {r.GetValueOrDefault("host")}";

                list.Add(new CoLocatedServiceSummary
                {
                    ServiceName = r.GetValueOrDefault("serviceName")?.ToString() ?? "",
                    Tier = r.GetValueOrDefault("tier")?.ToString() ?? "Backend",
                    SharedResource = hostDesc
                });
            }

            // 2. Co-located in same K8s Cluster & Namespace
            var k8sQuery = """
                MATCH (s:Service { name: $serviceName })-[:DEPLOYED_TO]->(ns:Namespace)<-[:DEPLOYED_TO]-(other:Service)
                WHERE other.name <> $serviceName
                RETURN other.name AS serviceName, other.tier AS tier, ns.name AS namespace, ns.cluster AS cluster
                """;

            var k8sRecords = await QueryAsync(k8sQuery, new { serviceName });
            foreach (var r in k8sRecords)
            {
                list.Add(new CoLocatedServiceSummary
                {
                    ServiceName = r.GetValueOrDefault("serviceName")?.ToString() ?? "",
                    Tier = r.GetValueOrDefault("tier")?.ToString() ?? "Backend",
                    SharedResource = $"K8s Namespace: {r.GetValueOrDefault("namespace")} ({r.GetValueOrDefault("cluster")})"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Neo4jGraphService] Error querying co-located services for '{Service}'", serviceName);
        }

        return list;
    }

    public async ValueTask DisposeAsync()
    {
        if (_driver != null)
        {
            await _driver.DisposeAsync();
        }
    }
}

public sealed class StitchedEdgeDto
{
    public string SourceServiceName { get; set; } = string.Empty;
    public string TargetServiceName { get; set; } = string.Empty;
    public string EdgeType { get; set; } = "CALLS"; // "CALLS", "EVENT", "DUPLICATE"
    public string? EndpointOrTopic { get; set; }
    public string? Protocol { get; set; } = "HTTP";
    public string? Action { get; set; } = "PUB";
    public string? Reason { get; set; }
    public double Confidence { get; set; } = 1.0;
}
