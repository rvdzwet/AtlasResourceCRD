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
Analyze this source code file and return a concise semantic summary matching this JSON schema:

{
  "purpose": "Brief sentence explaining what this file accomplishes",
  "primaryAbstractions": ["Main classes, interfaces, records, or functions defined in this file"],
  "keyDependencies": ["Key external libraries, APIs, or internal packages used"],
  "endpointsOrRoutes": ["Any HTTP/API routes or event topics defined in this file (e.g. GET /api/items), or empty list"],
  "configsOrEnvVars": ["Any configuration keys or environment variables read in this file, or empty list"]
}

--- FILE: {{file.RelativePath}} (Category: {{file.Category}}) ---
{{(file.Content.Length > 4000 ? file.Content.Substring(0, 4000) + "\n...[truncated]..." : file.Content)}}
""";

        try
        {
            var rawJson = await _geminiClient.GenerateContentAsync(
                prompt,
                "You are an expert code summarizer. Output strictly JSON.",
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
                ConfigsOrEnvVars = parsed?.ConfigsOrEnvVars ?? new List<string>()
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
    }
}
