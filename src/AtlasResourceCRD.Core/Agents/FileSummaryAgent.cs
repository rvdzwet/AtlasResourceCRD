using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AtlasResourceCRD.Core.Caching;
using AtlasResourceCRD.Core.Gemini;
using AtlasResourceCRD.Core.Scanner;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Agents;

public sealed class FileSummaryAgent
{
    private readonly GeminiClient _geminiClient;
    private readonly ILogger<FileSummaryAgent> _logger;

    public FileSummaryAgent(GeminiClient geminiClient, ILogger<FileSummaryAgent> logger)
    {
        _geminiClient = geminiClient;
        _logger = logger;
    }

    public async Task<FileSummary> SummarizeAsync(
        ScannedSourceFile file,
        string gitBlobSha,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("[FileSummaryAgent] Summarizing file: {Path} (Category: {Category})", file.RelativePath, file.Category);

        var prompt = $$"""
Analyze this source code file deeply and extract structured functional, architectural, and security facts matching this JSON schema:

{
  "purpose": "Precise sentence explaining the core purpose and domain role of this file",
  "primaryAbstractions": ["Main classes, interfaces, records, structs, or key functions defined in this file"],
  "keyDependencies": ["External packages, internal service dependencies, or remote APIs used"],
  "endpointsOrRoutes": ["Any HTTP/API routes (e.g. 'POST /api/commands'), MQTT topics, WebSocket hubs, or event subscriptions"],
  "configsOrEnvVars": ["Configuration settings, connection strings, or environment variables referenced"],
  "businessLogicAndInvariants": ["Key business calculations, mathematical equations, invariant rules, thresholds, or domain policies executed in this code"],
  "inputOutputContracts": ["Method signatures, parameter types, payload schemas, return types, or data structures accepted/produced"],
  "errorAndExceptionHandling": ["Exception handling blocks, timeout configurations, retry policies, or fallback mechanisms"],
  "stateMutationsAndEvents": ["Database writes/queries, cache updates, state changes, or CloudEvents/domain events published"],
  "securityAndQualityNotes": ["Line-level security observations, missing authentication, anti-patterns, sync-over-async blocking, or code smells"]
}

--- FILE: {{file.RelativePath}} (Category: {{file.Category}}) ---
{{(file.Content.Length > 12000 ? file.Content.Substring(0, 12000) + "\n...[truncated]..." : file.Content)}}
""";

        try
        {
            var rawJson = await _geminiClient.GenerateContentAsync(
                prompt,
                "You are an expert deep code analyzer and reverse engineer. Output strictly JSON.",
                enforceJson: true,
                cancellationToken: cancellationToken);

            var cleanJson = GeminiClient.ExtractJson(rawJson);
            var parsed = JsonSerializer.Deserialize<FileSummaryDto>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new FileSummary
            {
                RelativePath = file.RelativePath,
                GitBlobSha = gitBlobSha,
                Category = file.Category,
                Purpose = parsed?.Purpose ?? "Source file component",
                PrimaryAbstractions = parsed?.PrimaryAbstractions ?? new List<string>(),
                KeyDependencies = parsed?.KeyDependencies ?? new List<string>(),
                EndpointsOrRoutes = parsed?.EndpointsOrRoutes ?? new List<string>(),
                ConfigsOrEnvVars = parsed?.ConfigsOrEnvVars ?? new List<string>(),
                BusinessLogicAndInvariants = parsed?.BusinessLogicAndInvariants ?? new List<string>(),
                InputOutputContracts = parsed?.InputOutputContracts ?? new List<string>(),
                ErrorAndExceptionHandling = parsed?.ErrorAndExceptionHandling ?? new List<string>(),
                StateMutationsAndEvents = parsed?.StateMutationsAndEvents ?? new List<string>(),
                SecurityAndQualityNotes = parsed?.SecurityAndQualityNotes ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileSummaryAgent] Failed to summarize {Path} via LLM, creating heuristic fallback summary.", file.RelativePath);
            return new FileSummary
            {
                RelativePath = file.RelativePath,
                GitBlobSha = gitBlobSha,
                Category = file.Category,
                Purpose = $"Source file for {file.Category}",
                PrimaryAbstractions = new List<string> { System.IO.Path.GetFileNameWithoutExtension(file.RelativePath) }
            };
        }
    }

    private sealed class FileSummaryDto
    {
        [JsonPropertyName("purpose")]
        public string? Purpose { get; set; }

        [JsonPropertyName("primaryAbstractions")]
        public List<string>? PrimaryAbstractions { get; set; }

        [JsonPropertyName("keyDependencies")]
        public List<string>? KeyDependencies { get; set; }

        [JsonPropertyName("endpointsOrRoutes")]
        public List<string>? EndpointsOrRoutes { get; set; }

        [JsonPropertyName("configsOrEnvVars")]
        public List<string>? ConfigsOrEnvVars { get; set; }

        [JsonPropertyName("businessLogicAndInvariants")]
        public List<string>? BusinessLogicAndInvariants { get; set; }

        [JsonPropertyName("inputOutputContracts")]
        public List<string>? InputOutputContracts { get; set; }

        [JsonPropertyName("errorAndExceptionHandling")]
        public List<string>? ErrorAndExceptionHandling { get; set; }

        [JsonPropertyName("stateMutationsAndEvents")]
        public List<string>? StateMutationsAndEvents { get; set; }

        [JsonPropertyName("securityAndQualityNotes")]
        public List<string>? SecurityAndQualityNotes { get; set; }
    }
}
