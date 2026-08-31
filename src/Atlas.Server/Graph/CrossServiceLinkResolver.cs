using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Graph;

public sealed class CrossServiceLinkResolver
{
    private readonly Neo4jGraphService _graphService;
    private readonly ILogger<CrossServiceLinkResolver> _logger;

    public CrossServiceLinkResolver(Neo4jGraphService graphService, ILogger<CrossServiceLinkResolver> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public async Task ResolveCrossServiceLinksAsync()
    {
        _logger.LogInformation("[CrossServiceLinkResolver] Resolving cross-service dependencies in Neo4j graph...");

        // 1. Resolve Service-to-Service links via Event Topics (Publisher -> Topic <- Consumer)
        var eventLinkCypher = """
            MATCH (producer:Service)-[:PRODUCES]->(t:EventTopic)<-[:CONSUMES]-(consumer:Service)
            WHERE producer <> consumer
            MERGE (consumer)-[r:DEPENDS_ON { type: 'EventStream', topic: t.topicOrQueue }]->(producer)
            RETURN count(r) AS eventEdges
            """;

        // 2. Resolve Service-to-Service links via Shared Databases
        var dbLinkCypher = """
            MATCH (s1:Service)-[:READS_WRITES]->(db:Database)<-[:READS_WRITES]-(s2:Service)
            WHERE s1 <> s2
            MERGE (s1)-[r:SHARES_DATASTORE { database: db.name }]->(s2)
            RETURN count(r) AS dbEdges
            """;

        // 3. Resolve Direct Service Calls based on matching endpoint paths or service name references
        var apiLinkCypher = """
            MATCH (caller:Service), (provider:Service)-[:EXPOSES]->(e:Endpoint)
            WHERE caller <> provider AND (caller.dependencies CONTAINS provider.name OR caller.dependencies CONTAINS e.path)
            MERGE (caller)-[r:CALLS_SERVICE { endpoint: e.path }]->(provider)
            RETURN count(r) AS apiEdges
            """;

        try
        {
            await _graphService.ExecuteCypherAsync(eventLinkCypher);
            await _graphService.ExecuteCypherAsync(dbLinkCypher);
            await _graphService.ExecuteCypherAsync(apiLinkCypher);
            _logger.LogInformation("[CrossServiceLinkResolver] Cross-service graph relationships resolved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CrossServiceLinkResolver] Error resolving cross-service linkages in Neo4j.");
        }
    }
}
