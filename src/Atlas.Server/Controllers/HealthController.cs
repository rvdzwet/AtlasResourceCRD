using System;
using System.Threading.Tasks;
using Atlas.Server.Graph;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Server.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly Neo4jGraphService _graphService;

    public HealthController(Neo4jGraphService graphService)
    {
        _graphService = graphService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var isNeo4jOnline = await _graphService.TestConnectionAsync();
        return Ok(new
        {
            status = "Healthy",
            server = "Atlas Enterprise Hub v1.7.0",
            neo4j = isNeo4jOnline ? "Connected" : "Offline / Fallback",
            timestamp = DateTime.UtcNow
        });
    }
}
