using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core.Models;
using Atlas.Core.Serialization;
using Atlas.Server.Graph;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Storage;

/// <summary>
/// 100% Stateless Neo4j-backed Repository.
/// All catalog documents, raw YAML/JSON specifications, service summaries, and historical timelines
/// are persisted and queried directly in Neo4j (zero in-memory caching, zero local disk files).
/// </summary>
public sealed class SpecDocumentRepository
{
    private readonly Neo4jGraphService _graphService;
    private readonly Neo4jGraphMapper _graphMapper;
    private readonly ILogger<SpecDocumentRepository> _logger;

    public SpecDocumentRepository(
        Neo4jGraphService graphService,
        Neo4jGraphMapper graphMapper,
        ILogger<SpecDocumentRepository> logger)
    {
        _graphService = graphService;
        _graphMapper = graphMapper;
        _logger = logger;
    }

    public int TotalServicesCount
    {
        get
        {
            try
            {
                var query = "MATCH (s:Service) RETURN count(s) AS total";
                var records = _graphService.QueryAsync(query).GetAwaiter().GetResult();
                if (records.Count > 0 && records[0].TryGetValue("total", out var val))
                {
                    return Convert.ToInt32(val);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SpecDocumentRepository] Error querying TotalServicesCount from Neo4j");
            }
            return 0;
        }
    }

    public void Save(AtlasResource resource, string? commitSha = null)
    {
        var name = resource.Metadata?.Name ?? "unknown-service";
        _logger.LogInformation("[SpecDocumentRepository] Persisting '{Name}' directly to Neo4j graph", name);
        _graphMapper.MapResourceToGraphAsync(resource).GetAwaiter().GetResult();
    }

    public async Task SaveAsync(AtlasResource resource, string? commitSha = null)
    {
        var name = resource.Metadata?.Name ?? "unknown-service";
        _logger.LogInformation("[SpecDocumentRepository] Persisting '{Name}' asynchronously to Neo4j graph", name);
        await _graphMapper.MapResourceToGraphAsync(resource);
    }

    public List<AtlasResource> GetAll()
    {
        var list = new List<AtlasResource>();
        try
        {
            var query = "MATCH (s:Service) WHERE s.rawYaml IS NOT NULL RETURN s.rawYaml AS rawYaml, s.name AS name";
            var records = _graphService.QueryAsync(query).GetAwaiter().GetResult();

            foreach (var r in records)
            {
                if (r.TryGetValue("rawYaml", out var rawYamlObj) && rawYamlObj is string yaml && !string.IsNullOrWhiteSpace(yaml))
                {
                    try
                    {
                        var res = CrdYamlSerializer.DeserializeYaml(yaml);
                        if (res != null) list.Add(res);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[SpecDocumentRepository] Could not deserialize YAML for {Name}", r.GetValueOrDefault("name"));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error querying all resources from Neo4j");
        }
        return list;
    }

    public AtlasResource? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var query = "MATCH (s:Service { name: $name }) WHERE s.rawYaml IS NOT NULL RETURN s.rawYaml AS rawYaml";
            var records = _graphService.QueryAsync(query, new { name }).GetAwaiter().GetResult();

            if (records.Count > 0 && records[0].TryGetValue("rawYaml", out var rawYamlObj) && rawYamlObj is string yaml && !string.IsNullOrWhiteSpace(yaml))
            {
                return CrdYamlSerializer.DeserializeYaml(yaml);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error querying resource '{Name}' from Neo4j", name);
        }
        return null;
    }

    public string? GetYaml(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var query = "MATCH (s:Service { name: $name }) WHERE s.rawYaml IS NOT NULL RETURN s.rawYaml AS rawYaml";
            var records = _graphService.QueryAsync(query, new { name }).GetAwaiter().GetResult();

            if (records.Count > 0 && records[0].TryGetValue("rawYaml", out var rawYamlObj) && rawYamlObj is string yaml)
            {
                return yaml;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error querying YAML for '{Name}' from Neo4j", name);
        }
        return null;
    }

    public string? GetRawYaml(string name) => GetYaml(name);

    public ServiceSummaryRecord? GetSummary(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var query = """
                MATCH (s:Service { name: $name })
                OPTIONAL MATCH (s)-[:EXPOSES]->(e:Endpoint)
                OPTIONAL MATCH (s)-[:PERSISTS_TO]->(db:Database)
                OPTIONAL MATCH (s)-[:CALLS]->(ext:ExternalApi)
                OPTIONAL MATCH (s)-[:HAS_THREAT]->(th:Threat)
                WITH s, count(DISTINCT e) AS endpointsCount, count(DISTINCT db) AS databasesCount, count(DISTINCT ext) AS externalApisCount, count(DISTINCT th) AS threatCount
                RETURN s.name AS name, s.tier AS tier, s.domain AS domain, s.sigStars AS sigStars,
                       s.reviewGrade AS reviewGrade, s.riskLevel AS riskLevel, s.repoUrl AS repoUrl, s.description AS description,
                       endpointsCount, databasesCount, externalApisCount, threatCount
                LIMIT 1
                """;

            var records = _graphService.QueryAsync(query, new { name }).GetAwaiter().GetResult();
            if (records.Count > 0)
            {
                return MapRecordToSummary(records[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error fetching summary for '{Name}' from Neo4j", name);
        }
        return null;
    }

    public List<ServiceSummaryRecord> GetAllSummaries()
    {
        var list = new List<ServiceSummaryRecord>();
        try
        {
            var query = """
                MATCH (s:Service)
                OPTIONAL MATCH (s)-[:EXPOSES]->(e:Endpoint)
                OPTIONAL MATCH (s)-[:PERSISTS_TO]->(db:Database)
                OPTIONAL MATCH (s)-[:CALLS]->(ext:ExternalApi)
                OPTIONAL MATCH (s)-[:HAS_THREAT]->(th:Threat)
                WITH s, count(DISTINCT e) AS endpointsCount, count(DISTINCT db) AS databasesCount, count(DISTINCT ext) AS externalApisCount, count(DISTINCT th) AS threatCount
                RETURN s.name AS name, s.tier AS tier, s.domain AS domain, s.sigStars AS sigStars,
                       s.reviewGrade AS reviewGrade, s.riskLevel AS riskLevel, s.repoUrl AS repoUrl, s.description AS description,
                       endpointsCount, databasesCount, externalApisCount, threatCount
                ORDER BY s.name ASC
                """;

            var records = _graphService.QueryAsync(query).GetAwaiter().GetResult();
            foreach (var r in records)
            {
                list.Add(MapRecordToSummary(r));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error fetching all summaries from Neo4j");
        }
        return list;
    }

    public PagedResult<ServiceSummaryRecord> GetPagedSummaries(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? sortBy = "name",
        bool descending = false,
        string? domain = null,
        string? tier = null,
        double? minStars = null,
        double? maxStars = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 500);
        int skip = (page - 1) * pageSize;

        var items = new List<ServiceSummaryRecord>();
        int totalItems = 0;

        try
        {
            var whereClauses = new List<string>();
            var parameters = new Dictionary<string, object>
            {
                ["skip"] = skip,
                ["limit"] = pageSize
            };

            if (!string.IsNullOrWhiteSpace(domain) && domain != "All")
            {
                whereClauses.Add("s.domain = $domain");
                parameters["domain"] = domain;
            }

            if (!string.IsNullOrWhiteSpace(tier) && tier != "All")
            {
                whereClauses.Add("s.tier = $tier");
                parameters["tier"] = tier;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                whereClauses.Add("(toLower(s.name) CONTAINS toLower($search) OR toLower(coalesce(s.description, '')) CONTAINS toLower($search) OR toLower(coalesce(s.domain, '')) CONTAINS toLower($search))");
                parameters["search"] = search;
            }

            if (minStars.HasValue)
            {
                whereClauses.Add("s.sigStars >= $minStars");
                parameters["minStars"] = minStars.Value;
            }

            if (maxStars.HasValue)
            {
                whereClauses.Add("s.sigStars <= $maxStars");
                parameters["maxStars"] = maxStars.Value;
            }

            var whereString = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

            var countQuery = $"MATCH (s:Service) {whereString} RETURN count(s) AS total";
            var countRecords = _graphService.QueryAsync(countQuery, parameters).GetAwaiter().GetResult();
            if (countRecords.Count > 0 && countRecords[0].TryGetValue("total", out var totalVal))
            {
                totalItems = Convert.ToInt32(totalVal);
            }

            var orderBy = sortBy?.ToLowerInvariant() switch
            {
                "stars_desc" => "s.sigStars DESC, s.name ASC",
                "stars_asc" => "s.sigStars ASC, s.name ASC",
                "endpoints_desc" => "endpointsCount DESC, s.name ASC",
                "threats_desc" => "threatCount DESC, s.name ASC",
                _ => "s.name ASC"
            };

            var query = $"""
                MATCH (s:Service)
                {whereString}
                OPTIONAL MATCH (s)-[:EXPOSES]->(e:Endpoint)
                OPTIONAL MATCH (s)-[:PERSISTS_TO]->(db:Database)
                OPTIONAL MATCH (s)-[:CALLS]->(ext:ExternalApi)
                OPTIONAL MATCH (s)-[:HAS_THREAT]->(th:Threat)
                WITH s, count(DISTINCT e) AS endpointsCount, count(DISTINCT db) AS databasesCount, count(DISTINCT ext) AS externalApisCount, count(DISTINCT th) AS threatCount
                RETURN s.name AS name, s.tier AS tier, s.domain AS domain, s.sigStars AS sigStars,
                       s.reviewGrade AS reviewGrade, s.riskLevel AS riskLevel, s.repoUrl AS repoUrl, s.description AS description,
                       endpointsCount, databasesCount, externalApisCount, threatCount
                ORDER BY {orderBy}
                SKIP $skip LIMIT $limit
                """;

            var records = _graphService.QueryAsync(query, parameters).GetAwaiter().GetResult();
            foreach (var r in records)
            {
                items.Add(MapRecordToSummary(r));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error querying paginated summaries from Neo4j");
        }

        return new PagedResult<ServiceSummaryRecord>(items, totalItems, page, pageSize);
    }

    public List<ServiceSummaryRecord> SearchSummaries(string query, int maxResults = 15)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return GetAllSummaries().Take(maxResults).ToList();
        }

        var results = new List<ServiceSummaryRecord>();
        try
        {
            var cypher = """
                MATCH (s:Service)
                WHERE toLower(s.name) CONTAINS toLower($query)
                   OR toLower(coalesce(s.description, '')) CONTAINS toLower($query)
                   OR toLower(coalesce(s.domain, '')) CONTAINS toLower($query)
                OPTIONAL MATCH (s)-[:EXPOSES]->(e:Endpoint)
                OPTIONAL MATCH (s)-[:PERSISTS_TO]->(db:Database)
                OPTIONAL MATCH (s)-[:CALLS]->(ext:ExternalApi)
                OPTIONAL MATCH (s)-[:HAS_THREAT]->(th:Threat)
                WITH s, count(DISTINCT e) AS endpointsCount, count(DISTINCT db) AS databasesCount, count(DISTINCT ext) AS externalApisCount, count(DISTINCT th) AS threatCount
                RETURN s.name AS name, s.tier AS tier, s.domain AS domain, s.sigStars AS sigStars,
                       s.reviewGrade AS reviewGrade, s.riskLevel AS riskLevel, s.repoUrl AS repoUrl, s.description AS description,
                       endpointsCount, databasesCount, externalApisCount, threatCount
                ORDER BY s.name ASC
                LIMIT $limit
                """;

            var records = _graphService.QueryAsync(cypher, new { query, limit = maxResults }).GetAwaiter().GetResult();
            foreach (var r in records)
            {
                results.Add(MapRecordToSummary(r));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error performing prefix search in Neo4j for '{Query}'", query);
        }
        return results;
    }

    public List<ServiceSnapshotRecord> GetHistoricalSnapshots(string serviceName)
    {
        var snapshots = new List<ServiceSnapshotRecord>();
        if (string.IsNullOrWhiteSpace(serviceName)) return snapshots;

        try
        {
            var cypher = """
                MATCH (s:Service { name: $serviceName })-[:HAS_HISTORICAL_SNAPSHOT]->(snap:ServiceSnapshot)
                RETURN snap.snapshotId AS snapshotId, snap.commit AS commit, snap.sigStars AS sigStars,
                       snap.techDebtRatio AS techDebtRatio, snap.maintainabilityIndex AS maintainabilityIndex,
                       snap.threatCount AS threatCount, snap.codeSmellCount AS codeSmellCount,
                       snap.totalLinesOfCode AS totalLinesOfCode, snap.timestamp AS timestamp
                ORDER BY snap.timestamp DESC
                """;

            var records = _graphService.QueryAsync(cypher, new { serviceName }).GetAwaiter().GetResult();
            foreach (var r in records)
            {
                snapshots.Add(new ServiceSnapshotRecord
                {
                    SnapshotId = r.GetValueOrDefault("snapshotId")?.ToString() ?? Guid.NewGuid().ToString("N"),
                    ServiceName = serviceName,
                    GitCommitSha = r.GetValueOrDefault("commit")?.ToString(),
                    SigStars = Convert.ToDouble(r.GetValueOrDefault("sigStars") ?? 4.0),
                    TechDebtRatio = Convert.ToDouble(r.GetValueOrDefault("techDebtRatio") ?? 0.05),
                    MaintainabilityIndex = Convert.ToDouble(r.GetValueOrDefault("maintainabilityIndex") ?? 85.0),
                    ThreatCount = Convert.ToInt32(r.GetValueOrDefault("threatCount") ?? 0),
                    CodeSmellCount = Convert.ToInt32(r.GetValueOrDefault("codeSmellCount") ?? 0),
                    TotalLinesOfCode = Convert.ToInt32(r.GetValueOrDefault("totalLinesOfCode") ?? 0)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error querying historical snapshots for '{Service}' from Neo4j", serviceName);
        }
        return snapshots;
    }

    public ServiceHistorySummary GetHistory(string serviceName)
    {
        var snapshots = GetHistoricalSnapshots(serviceName);
        return new ServiceHistorySummary
        {
            ServiceName = serviceName,
            Snapshots = snapshots
        };
    }

    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        try
        {
            var cypher = "MATCH (s:Service { name: $name }) DETACH DELETE s";
            _graphService.ExecuteCypherAsync(cypher, new { name }).GetAwaiter().GetResult();
            _logger.LogInformation("[SpecDocumentRepository] Deleted service '{Name}' from Neo4j graph", name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpecDocumentRepository] Error deleting service '{Name}' from Neo4j", name);
            return false;
        }
    }

    private static ServiceSummaryRecord MapRecordToSummary(Dictionary<string, object> r)
    {
        var name = r.GetValueOrDefault("name")?.ToString() ?? "unknown";
        var tier = r.GetValueOrDefault("tier")?.ToString() ?? "Backend";
        var domain = r.GetValueOrDefault("domain")?.ToString();
        if (string.IsNullOrWhiteSpace(domain))
        {
            domain = name.Contains("hypotheken", StringComparison.OrdinalIgnoreCase) ? "Dutch Mortgage & Lending Core" : "Smart Home & IoT Automation";
        }

        return new ServiceSummaryRecord
        {
            Name = name,
            Tier = tier,
            Domain = domain,
            SigStars = Convert.ToDouble(r.GetValueOrDefault("sigStars") ?? 4.0),
            ReviewGrade = r.GetValueOrDefault("reviewGrade")?.ToString() ?? "A",
            OverallRiskLevel = r.GetValueOrDefault("riskLevel")?.ToString() ?? "Low",
            RepositoryUrl = r.GetValueOrDefault("repoUrl")?.ToString() ?? "",
            Description = r.GetValueOrDefault("description")?.ToString() ?? "",
            EndpointsCount = Convert.ToInt32(r.GetValueOrDefault("endpointsCount") ?? 0),
            DatabasesCount = Convert.ToInt32(r.GetValueOrDefault("databasesCount") ?? 0),
            ExternalApisCount = Convert.ToInt32(r.GetValueOrDefault("externalApisCount") ?? 0),
            ThreatCount = Convert.ToInt32(r.GetValueOrDefault("threatCount") ?? 0)
        };
    }
}
