using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Gemini;

public sealed class GeminiClientOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.7-flash"; // Supports gemini-3.7-flash, gemini-2.5-flash, etc.
    public int? ThinkingBudget { get; set; } = 24576; // High thinking budget by default (24k tokens)
    public double Temperature { get; set; } = 0.1;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public void ApplyThinkingLevel(string level)
    {
        ThinkingBudget = level.ToLowerInvariant() switch
        {
            "high" => 24576,
            "max" or "maximum" => 65536,
            "dynamic" or "auto" => -1,
            "medium" => 8192,
            "low" => 2048,
            "off" or "none" => 0,
            _ => int.TryParse(level, out var custom) ? custom : 24576
        };
    }
}

public sealed class GeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiClientOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public GeminiClient(HttpClient httpClient, GeminiClientOptions options, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<string> GenerateContentAsync(
        string userPrompt,
        string? systemInstruction = null,
        bool enforceJson = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured. Set GEMINI_API_KEY environment variable or pass --api-key.");
        }

        var endpoint = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        var requestBody = new GeminiRequest
        {
            Contents =
            {
                new GeminiContent
                {
                    Role = "user",
                    Parts = { new GeminiPart { Text = userPrompt } }
                }
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _options.Temperature,
                ResponseMimeType = enforceJson ? "application/json" : null
            }
        };

        if (_options.ThinkingBudget.HasValue && _options.ThinkingBudget.Value != 0)
        {
            requestBody.GenerationConfig.ThinkingConfig = new GeminiThinkingConfig
            {
                ThinkingBudget = _options.ThinkingBudget.Value
            };
        }

        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            requestBody.SystemInstruction = new GeminiContent
            {
                Parts = { new GeminiPart { Text = systemInstruction } }
            };
        }

        var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

        _logger.LogInformation("[GeminiClient] Sending request to Gemini API (Model: {Model}, ThinkingBudget: {ThinkingBudget}, PromptLength: {Length} chars)",
            _options.Model, _options.ThinkingBudget, userPrompt.Length);
        _logger.LogDebug("[GeminiClient] Target endpoint: {BaseUrl}/models/{Model}:generateContent", _options.BaseUrl, _options.Model);
        _logger.LogTrace("[GeminiClient] Full Request Payload:\n{RequestJson}", requestJson);

        var stopwatch = Stopwatch.StartNew();
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GeminiClient] HTTP network exception while connecting to Gemini API.");
            throw;
        }

        stopwatch.Stop();
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("[GeminiClient] Received HTTP {StatusCode} in {ElapsedMs} ms ({Length} bytes)",
            (int)response.StatusCode, stopwatch.ElapsedMilliseconds, rawResponse.Length);
        _logger.LogTrace("[GeminiClient] Full Raw Response Payload:\n{RawResponse}", rawResponse);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[GeminiClient] API Error HTTP {StatusCode}: {Response}", response.StatusCode, rawResponse);
            throw new HttpRequestException($"Gemini API error (HTTP {response.StatusCode}): {rawResponse}");
        }

        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(rawResponse, JsonOptions);

        if (geminiResponse?.Error != null)
        {
            _logger.LogError("[GeminiClient] Gemini API returned error payload: {Message} (Code: {Code})",
                geminiResponse.Error.Message, geminiResponse.Error.Code);
            throw new InvalidOperationException($"Gemini error: {geminiResponse.Error.Message}");
        }

        if (geminiResponse?.UsageMetadata != null)
        {
            _logger.LogInformation("[GeminiClient] Token Usage: Prompt={PromptTokens}, Candidates={CandidateTokens}, Total={TotalTokens}, Thinking={ThinkingTokens}",
                geminiResponse.UsageMetadata.PromptTokenCount,
                geminiResponse.UsageMetadata.CandidatesTokenCount,
                geminiResponse.UsageMetadata.TotalTokenCount,
                geminiResponse.UsageMetadata.ThinkingTokenCount);
        }

        var candidate = geminiResponse?.Candidates?[0];
        if (candidate?.Content?.Parts == null || candidate.Content.Parts.Count == 0)
        {
            throw new InvalidOperationException("Gemini returned an empty candidate list or no text parts.");
        }

        var textBuilder = new StringBuilder();
        foreach (var part in candidate.Content.Parts)
        {
            if (part.Thought == true)
            {
                _logger.LogTrace("[GeminiClient] Thinking Trace:\n{Thought}", part.Text);
            }
            else if (!string.IsNullOrEmpty(part.Text))
            {
                textBuilder.Append(part.Text);
            }
        }

        var resultText = textBuilder.ToString();
        _logger.LogDebug("[GeminiClient] Extracted content length: {Length} characters (FinishReason: {FinishReason})",
            resultText.Length, candidate.FinishReason);

        return resultText;
    }

    public async Task<T> GenerateStructuredAsync<T>(
        string userPrompt,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default)
    {
        var rawJson = await GenerateContentAsync(userPrompt, systemInstruction, enforceJson: true, cancellationToken);
        var cleanJson = ExtractJson(rawJson);

        try
        {
            var result = JsonSerializer.Deserialize<T>(cleanJson, JsonOptions);
            if (result == null)
            {
                throw new JsonException($"Deserialization produced null for type {typeof(T).Name}");
            }
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[GeminiClient] Failed to deserialize JSON into {Type}. Cleaned JSON was:\n{CleanJson}",
                typeof(T).Name, cleanJson);
            throw;
        }
    }

    public static string ExtractJson(string responseText)
    {
        var trimmed = responseText.Trim();

        // Check if wrapped in ```json ... ``` or ``` ... ```
        var match = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Check if starts with { or [
        var firstBrace = trimmed.IndexOfAny(new[] { '{', '[' });
        var lastBrace = trimmed.LastIndexOfAny(new[] { '}', ']' });

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return trimmed;
    }
}
