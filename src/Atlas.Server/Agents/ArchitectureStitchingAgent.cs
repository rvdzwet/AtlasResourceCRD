using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Gemini;
using Atlas.Core.Models;
using Atlas.Server.Graph;
using Atlas.Server.Storage;
using Microsoft.Extensions.Logging;

namespace Atlas.Server.Agents;

public sealed class ArchitectureStitchingAgent
{
    private readonly ILlmClient? _llmClient;
    private readonly Neo4jGraphService _graphService;
    private readonly SpecDocumentRepository _specRepo;
    private readonly ILogger<ArchitectureStitchingAgent> _logger;

    public ArchitectureStitchingAgent(
        ILlmClient? llmClient,
        Neo4jGraphService graphService,
        SpecDocumentRepository specRepo,
        ILogger<ArchitectureStitchingAgent> logger)
    {
        _llmClient = llmClient;
        _graphService = graphService;
        _specRepo = specRepo;
        _logger = logger;
    }

    public async Task<StitchingResult> StitchServiceAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("[ArchitectureStitchingAgent] 🧵 Starting architecture stitching for service: {Service}", serviceName);
        var targetRes = _specRepo.GetByName(serviceName);
        var allResources = _specRepo.GetAll();
        var resultEdges = new List<StitchedEdgeDto>();

        if (targetRes == null)
        {
            _logger.LogWarning("[ArchitectureStitchingAgent] Service '{Service}' not found in catalog. Skipping stitching.", serviceName);
            return new StitchingResult { ServiceName = serviceName, ReconciledEdges = resultEdges };
        }

        // =========================================================================
        // STAGE 1: Deterministic Graph Matching (Exact contract, event & DB matches)
        // =========================================================================
        var targetSpec = targetRes.Spec;
        if (targetSpec != null)
        {
            // 1a. Inbound & Outbound Internal Service Calls
            if (targetSpec.Dependencies?.InternalServices != null)
            {
                foreach (var dep in targetSpec.Dependencies.InternalServices)
                {
                    var matchedSvc = allResources.FirstOrDefault(r => string.Equals(r.Metadata.Name, dep.Name, StringComparison.OrdinalIgnoreCase));
                    if (matchedSvc != null)
                    {
                        resultEdges.Add(new StitchedEdgeDto
                        {
                            SourceServiceName = targetRes.Metadata.Name,
                            TargetServiceName = matchedSvc.Metadata.Name,
                            EdgeType = "CALLS",
                            EndpointOrTopic = dep.Purpose,
                            Protocol = dep.ProtocolOrHost ?? "gRPC/HTTP",
                            Confidence = 1.0
                        });
                    }
                }
            }

            // Find other services calling THIS target service
            foreach (var other in allResources.Where(r => !string.Equals(r.Metadata.Name, targetRes.Metadata.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var callsTarget = other.Spec?.Dependencies?.InternalServices?.FirstOrDefault(s => string.Equals(s.Name, targetRes.Metadata.Name, StringComparison.OrdinalIgnoreCase));
                if (callsTarget != null)
                {
                    resultEdges.Add(new StitchedEdgeDto
                    {
                        SourceServiceName = other.Metadata.Name,
                        TargetServiceName = targetRes.Metadata.Name,
                        EdgeType = "CALLS",
                        EndpointOrTopic = callsTarget.Purpose,
                        Protocol = callsTarget.ProtocolOrHost ?? "HTTP",
                        Confidence = 1.0
                    });
                }
            }

            // 1b. Event Topic Matching (Producers & Consumers)
            if (targetSpec.ApiContracts?.Events != null)
            {
                foreach (var ev in targetSpec.ApiContracts.Events)
                {
                    if (!string.IsNullOrWhiteSpace(ev.TopicOrQueue))
                    {
                        resultEdges.Add(new StitchedEdgeDto
                        {
                            SourceServiceName = targetRes.Metadata.Name,
                            TargetServiceName = ev.TopicOrQueue,
                            EdgeType = "EVENT",
                            EndpointOrTopic = ev.TopicOrQueue,
                            Action = ev.Action ?? "PUB",
                            Confidence = 1.0
                        });
                    }
                }
            }
        }

        // =========================================================================
        // STAGE 2: AI Semantic Resolution & Duplicate Asset Detection
        // =========================================================================
        if (_llmClient != null)
        {
            try
            {
                var candidates = allResources
                    .Where(r => !string.Equals(r.Metadata.Name, targetRes.Metadata.Name, StringComparison.OrdinalIgnoreCase))
                    .Take(15)
                    .Select(r => new {
                        Name = r.Metadata.Name,
                        Tier = r.Spec?.ComponentOverview?.Tier,
                        Description = r.Spec?.ComponentOverview?.Description,
                        Databases = r.Spec?.DataStores?.Databases?.Select(d => d.Name).ToList()
                    }).ToList();

                var prompt = $@"
Analyze the target microservice against candidate services in the enterprise graph.

Target Service:
- Name: {targetRes.Metadata.Name}
- Tier: {targetSpec?.ComponentOverview?.Tier}
- Description: {targetSpec?.ComponentOverview?.Description}
- Databases: {string.Join(", ", targetSpec?.DataStores?.Databases?.Select(d => d.Name) ?? Array.Empty<string>())}

Candidate Enterprise Services:
{JsonSerializer.Serialize(candidates, new JsonSerializerOptions { WriteIndented = true })}

TASK:
1. Identify if Target Service is a POTENTIAL DUPLICATE or overlapping microservice with any candidate.
2. Identify any unlinked semantic dependencies (where Target Service likely calls candidate services).

Return strictly JSON matching this structure:
{{
  ""duplicates"": [
    {{
      ""targetServiceName"": ""candidate_name"",
      ""reason"": ""Overlapping domain capability or database"",
      ""confidence"": 0.85
    }}
  ],
  ""inferredCalls"": [
    {{
      ""targetServiceName"": ""candidate_name"",
      ""protocol"": ""HTTP/REST"",
      ""reason"": ""Candidate exposes business capability consumed by Target"",
      ""confidence"": 0.90
    }}
  ]
}}
";

                var response = await _llmClient.GenerateContentAsync(prompt, "You are an enterprise architecture entity resolution agent.", enforceJson: true, cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    var cleanJson = LlmClientExtensions.ExtractJson(response);
                    var aiAnalysis = JsonSerializer.Deserialize<StitchingAiDto>(cleanJson, GeminiClient.JsonOptions);
                    if (aiAnalysis != null)
                    {
                        if (aiAnalysis.Duplicates != null)
                        {
                            foreach (var dup in aiAnalysis.Duplicates.Where(d => d.Confidence >= 0.75))
                            {
                                resultEdges.Add(new StitchedEdgeDto
                                {
                                    SourceServiceName = targetRes.Metadata.Name,
                                    TargetServiceName = dup.TargetServiceName,
                                    EdgeType = "DUPLICATE",
                                    Reason = dup.Reason,
                                    Confidence = dup.Confidence
                                });
                            }
                        }

                        if (aiAnalysis.InferredCalls != null)
                        {
                            foreach (var call in aiAnalysis.InferredCalls.Where(c => c.Confidence >= 0.85))
                            {
                                resultEdges.Add(new StitchedEdgeDto
                                {
                                    SourceServiceName = targetRes.Metadata.Name,
                                    TargetServiceName = call.TargetServiceName,
                                    EdgeType = "CALLS",
                                    Protocol = call.Protocol ?? "HTTP",
                                    EndpointOrTopic = call.Reason,
                                    Confidence = call.Confidence
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ArchitectureStitchingAgent] Stage 2 AI semantic analysis encountered an error for {Service}. Deterministic edges will be used.", serviceName);
            }
        }

        // =========================================================================
        // STAGE 3: Declarative ACID Reconciliation in Neo4j
        // =========================================================================
        await _graphService.ReconcileStitchedEdgesAsync(targetRes.Metadata.Name, resultEdges);

        return new StitchingResult
        {
            ServiceName = serviceName,
            ReconciledEdges = resultEdges
        };
    }

    public sealed class StitchingResult
    {
        public string ServiceName { get; set; } = string.Empty;
        public List<StitchedEdgeDto> ReconciledEdges { get; set; } = new();
    }

    private sealed class StitchingAiDto
    {
        [JsonPropertyName("duplicates")]
        public List<DuplicateCandidateDto>? Duplicates { get; set; }

        [JsonPropertyName("inferredCalls")]
        public List<InferredCallDto>? InferredCalls { get; set; }
    }

    private sealed class DuplicateCandidateDto
    {
        [JsonPropertyName("targetServiceName")]
        public string TargetServiceName { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 0.8;
    }

    private sealed class InferredCallDto
    {
        [JsonPropertyName("targetServiceName")]
        public string TargetServiceName { get; set; } = string.Empty;

        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 0.8;
    }
}
