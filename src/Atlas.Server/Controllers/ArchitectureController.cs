using System.Threading.Tasks;
using Atlas.Server.Agents;
using Atlas.Server.Graph;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Server.Controllers;

[ApiController]
[Route("api/v1/architecture")]
public sealed class ArchitectureController : ControllerBase
{
    private readonly ArchitectureStitchingAgent _stitchingAgent;
    private readonly Neo4jGraphService _graphService;

    public ArchitectureController(
        ArchitectureStitchingAgent stitchingAgent,
        Neo4jGraphService graphService)
    {
        _stitchingAgent = stitchingAgent;
        _graphService = graphService;
    }

    [HttpPost("stitch/{serviceName}")]
    public async Task<IActionResult> StitchService(string serviceName)
    {
        var result = await _stitchingAgent.StitchServiceAsync(serviceName);
        return Ok(new
        {
            success = true,
            service = serviceName,
            reconciledEdges = result.ReconciledEdges
        });
    }

    [HttpGet("jobs/claim")]
    public async Task<IActionResult> ClaimJob([FromQuery] string podId = "manual-worker")
    {
        var service = await _graphService.ClaimNextStitchingJobAsync(podId, System.TimeSpan.FromMinutes(2));
        return Ok(new { claimed = !string.IsNullOrWhiteSpace(service), service });
    }
}
