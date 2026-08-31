using Atlas.Core.Models;
using Atlas.Server.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Controllers;

[ApiController]
[Route("api/v1/cache")]
public sealed class CacheController : ControllerBase
{
    private readonly ServerCacheRepository _cacheRepo;
    private readonly ILogger<CacheController> _logger;

    public CacheController(ServerCacheRepository cacheRepo, ILogger<CacheController> logger)
    {
        _cacheRepo = cacheRepo;
        _logger = logger;
    }

    [HttpPost("synthesis/check")]
    public ActionResult<SynthesisCheckResponse> CheckSynthesis([FromBody] SynthesisCheckRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CommitSha))
        {
            return BadRequest(new SynthesisCheckResponse { IsExactMatch = false });
        }

        var result = _cacheRepo.CheckSynthesis(request.RepoName, request.CommitSha, request.FileShas);
        return Ok(result);
    }

    [HttpPost("files/query")]
    public ActionResult<FileSummaryQueryResponse> QueryFiles([FromBody] FileSummaryQueryRequest request)
    {
        if (request?.BlobShas == null || request.BlobShas.Count == 0)
        {
            return Ok(new FileSummaryQueryResponse());
        }

        var summaries = _cacheRepo.QueryFileSummaries(request.BlobShas);
        var dict = summaries.Where(s => !string.IsNullOrEmpty(s.GitBlobSha)).ToDictionary(s => s.GitBlobSha!, s => s);
        return Ok(new FileSummaryQueryResponse { Summaries = dict });
    }

    [HttpPost("files/store")]
    public IActionResult StoreFiles([FromBody] FileSummaryStoreRequest request)
    {
        if (request?.Summaries != null && request.Summaries.Count > 0)
        {
            _cacheRepo.StoreFileSummaries(request.Summaries);
        }
        return Ok(new { success = true, count = request?.Summaries?.Count ?? 0 });
    }
}
