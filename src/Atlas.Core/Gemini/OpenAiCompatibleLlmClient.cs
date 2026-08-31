using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Gemini;

/// <summary>
/// Universal OpenAI-compatible LLM Client for local inference backends (Ollama, LM Studio, vLLM, LocalAI, llama.cpp, Qwen 2.5 Coder, Gemma 2, DeepSeek-R1).
/// </summary>
public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmProfileConfig _config;
    private readonly ILogger _logger;

    public string ProviderName => "OpenAICompatible";
    public string ModelName => _config.Model;
    public int ContextWindowTokens => _config.ContextWindow > 0 ? _config.ContextWindow : 32768;

    public OpenAiCompatibleLlmClient(HttpClient httpClient, LlmProfileConfig config, ILogger logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateContentAsync(
        string userPrompt,
        string? systemInstruction = null,
        bool enforceJson = true,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = (_config.BaseUrl ?? "http://localhost:11434/v1").TrimEnd('/');
        var endpoint = $"{baseUrl}/chat/completions";

        var messages = new List<OpenAiChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            messages.Add(new OpenAiChatMessage { Role = "system", Content = systemInstruction });
        }

        messages.Add(new OpenAiChatMessage { Role = "user", Content = userPrompt });

        var requestBody = new OpenAiChatRequest
        {
            Model = _config.Model,
            Messages = messages,
            Temperature = _config.Temperature
        };

        if (enforceJson)
        {
            requestBody.ResponseFormat = new OpenAiResponseFormat { Type = "json_object" };
        }

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        });

        _logger.LogInformation("[OpenAiCompatibleLlmClient] Outbound POST -> {Endpoint} (Model: {Model}, ContextWindow: {ContextWindow}, JSON Mode: {JsonMode}, PromptLength: {Length} chars)",
            endpoint, _config.Model, ContextWindowTokens, enforceJson, userPrompt.Length);

        var stopwatch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var apiKey = !string.IsNullOrWhiteSpace(_config.ApiKey) ? _config.ApiKey : "local-no-key";
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAiCompatibleLlmClient] HTTP connection error contacting OpenAI-compatible endpoint at {Endpoint}", endpoint);
            throw;
        }

        stopwatch.Stop();
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[OpenAiCompatibleLlmClient] Inbound HTTP {StatusCode} from {Endpoint} in {ElapsedMs}ms. Response: {Body}",
                (int)response.StatusCode, endpoint, stopwatch.ElapsedMilliseconds, responseContent);
            throw new HttpRequestException($"OpenAI-compatible endpoint returned HTTP {(int)response.StatusCode}: {responseContent}");
        }

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentElem))
                {
                    var rawContent = contentElem.GetString() ?? "";

                    _logger.LogInformation("[OpenAiCompatibleLlmClient] Received {Length} chars in {ElapsedMs}ms from {Model}",
                        rawContent.Length, stopwatch.ElapsedMilliseconds, _config.Model);

                    return enforceJson ? ExtractJsonPayload(rawContent) : rawContent;
                }
            }

            throw new InvalidOperationException($"Unexpected OpenAI-compatible response format: {responseContent}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[OpenAiCompatibleLlmClient] Failed to parse response JSON: {Content}", responseContent);
            throw;
        }
    }

    private static string ExtractJsonPayload(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var trimmed = text.Trim();

        // 1. Markdown codeblock extraction
        var match = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // 2. Locate outermost JSON braces or brackets
        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        int firstBracket = trimmed.IndexOf('[');
        int lastBracket = trimmed.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
        {
            return trimmed.Substring(firstBracket, lastBracket - firstBracket + 1);
        }

        return trimmed;
    }

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAiChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.1;

        [JsonPropertyName("response_format")]
        public OpenAiResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }
}
