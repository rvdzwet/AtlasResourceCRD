using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Caching;
using Atlas.Core.Gemini;
using Atlas.Core.Scanner;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Agents;

public sealed class FileSummaryAgent
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<FileSummaryAgent> _logger;

    public FileSummaryAgent(ILlmClient llmClient, ILogger<FileSummaryAgent> logger)
    {
        _llmClient = llmClient;
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
  "primaryAbstractions": ["Main classes, interfaces, records, structs, procedures, or key functions defined in this file"],
  "keyDependencies": ["External packages, internal service dependencies, database tables, or remote APIs used"],
  "endpointsOrRoutes": ["Any HTTP/API routes (e.g. 'POST /api/commands'), RPC procedures, MQTT topics, WebSocket hubs, or event subscriptions"],
  "configsOrEnvVars": ["Configuration settings, connection strings, or environment variables referenced"],
  "businessLogicAndInvariants": ["Key business calculations, mathematical equations, invariant rules, thresholds, or domain policies executed in this code"],
  "inputOutputContracts": ["Method signatures, parameters, payload schemas, return types, or data structures accepted/produced"],
  "errorAndExceptionHandling": ["Exception handling blocks, timeout configurations, retry policies, or fallback mechanisms"],
  "stateMutationsAndEvents": ["Database writes/queries, cache updates, state changes, or CloudEvents/domain events published"],
  "securityAndQualityNotes": ["Line-level security observations, missing authentication, anti-patterns, sync-over-async blocking, or code smells"]
}

--- FILE: {{file.RelativePath}} (Category: {{file.Category}}) ---
{{(file.Content.Length > 12000 ? file.Content.Substring(0, 12000) + "\n...[truncated]..." : file.Content)}}
""";

        const int maxAttempts = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var currentPrompt = prompt;
                if (attempt > 1 && lastException != null)
                {
                    currentPrompt += $"\n\nIMPORTANT: Previous response attempt {attempt - 1} failed JSON parsing with: '{lastException.Message}'. Return ONLY valid, well-formed JSON matching the schema.";
                }

                var rawJson = await _llmClient.GenerateContentAsync(
                    currentPrompt,
                    "You are an expert deep code analyzer and reverse engineer. Output strictly raw JSON.",
                    enforceJson: true,
                    cancellationToken: cancellationToken);

                var cleanJson = LlmClientExtensions.ExtractJson(rawJson);
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                var purpose = root.TryGetProperty("purpose", out var pProp) && pProp.ValueKind == JsonValueKind.String
                    ? pProp.GetString() ?? $"Source file for {file.Category}"
                    : $"Source file for {file.Category}";

                return new FileSummary
                {
                    RelativePath = file.RelativePath,
                    GitBlobSha = gitBlobSha,
                    Category = file.Category,
                    Purpose = purpose,
                    PrimaryAbstractions = ExtractStringList(root, "primaryAbstractions"),
                    KeyDependencies = ExtractStringList(root, "keyDependencies"),
                    EndpointsOrRoutes = ExtractStringList(root, "endpointsOrRoutes"),
                    ConfigsOrEnvVars = ExtractStringList(root, "configsOrEnvVars"),
                    BusinessLogicAndInvariants = ExtractStringList(root, "businessLogicAndInvariants"),
                    InputOutputContracts = ExtractStringList(root, "inputOutputContracts"),
                    ErrorAndExceptionHandling = ExtractStringList(root, "errorAndExceptionHandling"),
                    StateMutationsAndEvents = ExtractStringList(root, "stateMutationsAndEvents"),
                    SecurityAndQualityNotes = ExtractStringList(root, "securityAndQualityNotes")
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < maxAttempts)
                {
                    _logger.LogWarning("[FileSummaryAgent] Parse/LLM attempt {Attempt}/{Max} failed for {Path}: {Msg}. Retrying...",
                        attempt, maxAttempts, file.RelativePath, ex.Message);
                    await Task.Delay(250 * attempt, cancellationToken);
                }
            }
        }

        _logger.LogWarning(lastException, "[FileSummaryAgent] Failed to summarize {Path} via LLM after {Max} attempts, creating heuristic fallback summary.", file.RelativePath, maxAttempts);
        return new FileSummary
        {
            RelativePath = file.RelativePath,
            GitBlobSha = gitBlobSha,
            Category = file.Category,
            Purpose = $"Source file for {file.Category}",
            PrimaryAbstractions = new List<string> { System.IO.Path.GetFileNameWithoutExtension(file.RelativePath) }
        };
    }

    private static List<string> ExtractStringList(JsonElement root, string propertyName)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(propertyName, out var prop)) return list;

        if (prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
                else if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Number || item.ValueKind == JsonValueKind.True || item.ValueKind == JsonValueKind.False)
                {
                    list.Add(item.ToString());
                }
            }
        }
        else if (prop.ValueKind == JsonValueKind.String)
        {
            var s = prop.GetString();
            if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
        }

        return list;
    }
}
