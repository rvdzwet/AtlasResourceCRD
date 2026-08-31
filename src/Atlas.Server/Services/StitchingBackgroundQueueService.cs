using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Server.Agents;
using Atlas.Server.Graph;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Services;

public sealed class StitchingBackgroundQueueService : BackgroundService
{
    private readonly Neo4jGraphService _graphService;
    private readonly ArchitectureStitchingAgent _stitchingAgent;
    private readonly ILogger<StitchingBackgroundQueueService> _logger;
    private readonly string _podId;

    public StitchingBackgroundQueueService(
        Neo4jGraphService graphService,
        ArchitectureStitchingAgent stitchingAgent,
        ILogger<StitchingBackgroundQueueService> logger)
    {
        _graphService = graphService;
        _stitchingAgent = stitchingAgent;
        _logger = logger;
        _podId = Environment.GetEnvironmentVariable("HOSTNAME") ?? $"atlas-pod-{Guid.NewGuid().ToString("N")[..8]}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[StitchingBackgroundQueue] 🚀 Starting distributed stitching worker (Pod: {PodId})", _podId);
        
        // Wait briefly for server startup & schema readiness
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        await _graphService.EnsureSchemaConstraintsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_graphService.IsConnected)
                {
                    // Attempt atomic lease claim across horizontal pods
                    var serviceToStitch = await _graphService.ClaimNextStitchingJobAsync(_podId, TimeSpan.FromMinutes(2));
                    if (!string.IsNullOrWhiteSpace(serviceToStitch))
                    {
                        _logger.LogInformation("[StitchingBackgroundQueue] 🔒 Pod {PodId} claimed stitching job for: {Service}", _podId, serviceToStitch);
                        
                        try
                        {
                            await _stitchingAgent.StitchServiceAsync(serviceToStitch, stoppingToken);
                            await _graphService.CompleteStitchingJobAsync(serviceToStitch);
                            _logger.LogInformation("[StitchingBackgroundQueue] ✅ Completed stitching job for: {Service}", serviceToStitch);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[StitchingBackgroundQueue] ❌ Error executing stitching for: {Service}. Job lease will expire.", serviceToStitch);
                        }

                        // Check immediately for next job in queue
                        continue;
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[StitchingBackgroundQueue] Error in background worker loop.");
            }

            // Debounce / poll interval (2.5 seconds)
            await Task.Delay(TimeSpan.FromMilliseconds(2500), stoppingToken);
        }
    }
}
