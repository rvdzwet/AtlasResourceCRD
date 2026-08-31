using System.Text.Json;
using Atlas.Core.Models;
using Atlas.Server.Services;
using Atlas.Server.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Server.Controllers;

[ApiController]
[Route("api/v1/sbom")]
public sealed class SbomController : ControllerBase
{
    private readonly SpecDocumentRepository _specRepo;
    private readonly VulnerabilityBackgroundSyncService _syncService;
    private readonly ILogger<SbomController> _logger;

    public SbomController(
        SpecDocumentRepository specRepo,
        VulnerabilityBackgroundSyncService syncService,
        ILogger<SbomController> logger)
    {
        _specRepo = specRepo;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the CycloneDX 1.5 Software Bill of Materials (SBOM) for a specific service.
    /// </summary>
    [HttpGet("{serviceName}")]
    [Produces("application/json")]
    public IActionResult GetSbom(string serviceName)
    {
        var crd = _specRepo.GetByName(serviceName);
        if (crd == null)
        {
            return NotFound(new { error = $"Service '{serviceName}' not found." });
        }

        var sbom = crd.Spec?.Dependencies?.Sbom;
        if (sbom == null)
        {
            return NotFound(new { error = $"No SBOM recorded for service '{serviceName}'." });
        }

        return Ok(sbom);
    }

    /// <summary>
    /// Triggers an immediate fleet-wide OSV.dev CVE re-audit across all registered service SBOMs.
    /// </summary>
    [HttpPost("sync-vulnerabilities")]
    public async Task<IActionResult> SyncVulnerabilities(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SbomController] Received fleet-wide vulnerability synchronization request");
        var report = await _syncService.SyncAllServicesAsync(cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Uploads an external CycloneDX 1.5 SBOM and attaches it to a service catalog item.
    /// </summary>
    [HttpPost("upload/{serviceName}")]
    [Consumes("application/json")]
    public IActionResult UploadSbom(string serviceName, [FromBody] CycloneDxSbom sbom)
    {
        if (sbom == null || sbom.Components == null)
        {
            return BadRequest(new { error = "Invalid CycloneDX SBOM payload." });
        }

        var crd = _specRepo.GetByName(serviceName);
        if (crd == null)
        {
            return NotFound(new { error = $"Service '{serviceName}' not found." });
        }

        crd.Spec.Dependencies ??= new DependenciesSpec();
        crd.Spec.Dependencies.Sbom = sbom;

        _specRepo.Save(crd);
        _logger.LogInformation("Attached external CycloneDX SBOM ({Count} components) to service '{ServiceName}'",
            sbom.Components.Count, serviceName);

        return Ok(new { success = true, componentsCount = sbom.Components.Count });
    }
}
