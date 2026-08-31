using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Core.Models;
using Atlas.Core.Serialization;
using Atlas.Server.Agents;
using Atlas.Server.Graph;
using Atlas.Server.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Controllers;

[ApiController]
[Route("api/v1/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly SpecDocumentRepository _specRepo;
    private readonly ServerCacheRepository _cacheRepo;
    private readonly Neo4jGraphMapper _graphMapper;
    private readonly Neo4jGraphService _graphService;
    private readonly Services.VulnerabilityBackgroundSyncService _vulnSyncService;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        SpecDocumentRepository specRepo,
        ServerCacheRepository cacheRepo,
        Neo4jGraphMapper graphMapper,
        Neo4jGraphService graphService,
        Services.VulnerabilityBackgroundSyncService vulnSyncService,
        ILogger<CatalogController> logger)
    {
        _specRepo = specRepo;
        _cacheRepo = cacheRepo;
        _graphMapper = graphMapper;
        _graphService = graphService;
        _vulnSyncService = vulnSyncService;
        _logger = logger;
    }

    [HttpPost("ingest")]
    public async Task<ActionResult<CatalogIngestResponse>> Ingest([FromBody] CatalogIngestRequest request)
    {
        if (request?.Resource?.Metadata == null)
        {
            return BadRequest(new CatalogIngestResponse
            {
                Success = false,
                Message = "Invalid request: missing Resource or Metadata"
            });
        }

        var name = request.Resource.Metadata.Name;
        _logger.LogInformation("[CatalogController] Received ingestion request for service '{Name}' (Commit: {Commit})",
            name, request.CommitSha);

        try
        {
            // 1. Persist raw manifest to disk repository
            _specRepo.Save(request.Resource, request.CommitSha);

            // 2. Store synthesis snapshot in remote cache
            if (!string.IsNullOrWhiteSpace(request.CommitSha) && request.FileShas != null)
            {
                _cacheRepo.StoreSynthesis(name, request.CommitSha, request.FileShas, request.Resource);
            }

            // 3. Map into Neo4j graph nodes and relationships
            var graphUpdated = await _graphMapper.MapResourceToGraphAsync(request.Resource);

            // 4. Enqueue distributed post-ingestion stitching job in Neo4j
            await _graphService.EnqueueStitchingJobAsync(name);

            // 5. Trigger asynchronous cloud vulnerability & license enrichment in the background
            _ = Task.Run(() => _vulnSyncService.EnrichServiceAsync(name));

            return Ok(new CatalogIngestResponse
            {
                Success = true,
                Message = $"Catalog item '{name}' ingested successfully. Stitching and vulnerability enrichment enqueued.",
                GraphUpdated = graphUpdated,
                ResourceName = name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CatalogController] Ingestion error for '{Name}'", name);
            return StatusCode(500, new CatalogIngestResponse
            {
                Success = false,
                Message = $"Internal error ingesting '{name}': {ex.Message}",
                ResourceName = name
            });
        }
    }

    [HttpPost("sync-graph")]
    public async Task<IActionResult> SyncAllToGraph()
    {
        var resources = _specRepo.GetAll();
        int mappedCount = 0;
        foreach (var r in resources)
        {
            var ok = await _graphMapper.MapResourceToGraphAsync(r);
            if (ok)
            {
                mappedCount++;
                await _graphService.EnqueueStitchingJobAsync(r.Metadata.Name);
            }
        }

        return Ok(new
        {
            success = true,
            totalResources = resources.Count,
            mappedToGraph = mappedCount,
            message = "All resources synced and enqueued for agentic stitching"
        });
    }

    [HttpPost("vulnerabilities/sync")]
    public async Task<ActionResult<Services.VulnerabilitySyncReport>> TriggerVulnerabilitySync(System.Threading.CancellationToken cancellationToken)
    {
        _logger.LogInformation("[CatalogController] Manual trigger for fleet-wide OSV.dev vulnerability sync received");
        var report = await _vulnSyncService.SyncAllServicesAsync(cancellationToken);
        return Ok(report);
    }

    [HttpGet("resources")]
    public ActionResult<List<AtlasResource>> GetAll()
    {
        return Ok(_specRepo.GetAll());
    }

    [HttpGet("resources/{name}")]
    public ActionResult<AtlasResource> GetByName(string name)
    {
        var item = _specRepo.GetByName(name);
        return item != null ? Ok(item) : NotFound(new { error = $"Service '{name}' not found" });
    }

    [HttpGet("resources/{name}/yaml")]
    public IActionResult GetYaml(string name)
    {
        var yaml = _specRepo.GetRawYaml(name);
        return yaml != null ? Content(yaml, "application/x-yaml") : NotFound();
    }

    /// <summary>
    /// Completely removes a service from the Atlas catalog, Neo4j graph, and synthesis cache.
    /// This is a destructive, irreversible operation.
    /// </summary>
    [HttpDelete("resources/{name}")]
    public async Task<IActionResult> Delete(string name)
    {
        _logger.LogWarning("[CatalogController] ⚠️ DELETE request received for service '{Name}'", name);

        var existed = _specRepo.GetByName(name) != null;
        if (!existed)
        {
            return NotFound(new { success = false, message = $"Service '{name}' not found in the catalog" });
        }

        try
        {
            // 1. Remove from disk & in-memory index
            _specRepo.Delete(name);

            // 2. Purge synthesis cache
            _cacheRepo.DeleteServiceCache(name);

            // 3. Remove from Neo4j graph and clean up any stitching jobs
            var graphDeleted = await _graphMapper.DeleteServiceFromGraphAsync(name);
            await _graphService.ExecuteCypherAsync("MATCH (j:StitchingJob { serviceName: $name }) DELETE j", new { name });

            _logger.LogInformation("[CatalogController] Service '{Name}' fully deleted from catalog, cache, and graph (GraphDeleted: {GraphDeleted})",
                name, graphDeleted);

            return Ok(new
            {
                success = true,
                message = $"Service '{name}' has been permanently deleted",
                deletedFrom = new
                {
                    specRepository = true,
                    synthesisCache = true,
                    neo4jGraph = graphDeleted
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CatalogController] Error deleting service '{Name}'", name);
            return StatusCode(500, new { success = false, message = $"Error deleting '{name}': {ex.Message}" });
        }
    }
}
