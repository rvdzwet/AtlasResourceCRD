using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Gemini;

public sealed class GeminiClientOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.7-flash"; // Supports gemini-3.7-flash, gemini-2.5-flash, etc.
    public int? ThinkingBudget { get; set; } = 24576; // High thinking budget by default (24k tokens)
    public double Temperature { get; set; } = 0.1;
    public int ContextWindow { get; set; } = 131072;
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

public sealed class GeminiClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiClientOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public string ProviderName => "Gemini";
    public string ModelName => _options.Model;
    public int ContextWindowTokens => _options.ContextWindow > 0 ? _options.ContextWindow : 131072;

    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
        options.Converters.Add(new FlexibleStringJsonConverter());
        options.Converters.Add(new FlexibleListStringJsonConverter());
        options.Converters.Add(new Models.FlexibleDoubleJsonConverter());
        options.Converters.Add(new Models.FlexibleIntJsonConverter());
        options.Converters.Add(new Models.TechItemJsonConverter());
        options.Converters.Add(new Models.DependencyItemJsonConverter());
        options.Converters.Add(new Models.PackageDependencyJsonConverter());
        options.Converters.Add(new Models.ArchComponentJsonConverter());
        options.Converters.Add(new Models.CodeSmellItemJsonConverter());
        options.Converters.Add(new Models.CodeReviewFindingJsonConverter());
        options.Converters.Add(new Models.HealthCheckSpecJsonConverter());
        options.Converters.Add(new Models.OwaspComplianceItemJsonConverter());
        options.Converters.Add(new Models.SecurityFindingJsonConverter());
        options.Converters.Add(new Models.RiskItemJsonConverter());
        options.Converters.Add(new Models.TrustBoundaryJsonConverter());
        options.Converters.Add(new Models.ThreatVectorJsonConverter());
        options.Converters.Add(new Models.BddScenarioJsonConverter());
        options.Converters.Add(new Models.SystemInvariantJsonConverter());
        options.Converters.Add(new Models.CapabilityItemJsonConverter());
        return options;
    }

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

        if (_options.ThinkingBudget.HasValue)
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

        var maxRetries = 4;
        HttpResponseMessage? response = null;
        string rawResponse = string.Empty;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            try
            {
                response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "[GeminiClient] Network exception on attempt {Attempt}/{MaxRetries}. Retrying in 3s...", attempt, maxRetries);
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            stopwatch.Stop();
            rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("[GeminiClient] Received HTTP {StatusCode} in {ElapsedMs} ms ({Length} bytes)",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds, rawResponse.Length);

            if (response.IsSuccessStatusCode)
            {
                break;
            }

            if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503 || (int)response.StatusCode == 504)
            {
                if (attempt < maxRetries)
                {
                    int delaySeconds = attempt * 5; // default 5s, 10s, 15s

                    // Parse retryDelay from Gemini response if present (e.g. "25s" or "25.3s")
                    try
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(rawResponse, @"(?:retryDelay|retry in)\D+(\d+(?:\.\d+)?)s?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out double parsedSec))
                        {
                            delaySeconds = (int)Math.Ceiling(parsedSec) + 2;
                        }
                    }
                    catch { }

                    _logger.LogWarning("[GeminiClient] Rate limit or transient error (HTTP {StatusCode}) on attempt {Attempt}/{MaxRetries}. Backing off for {Seconds}s before retry...",
                        response.StatusCode, attempt, maxRetries, delaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    continue;
                }
            }

            _logger.LogError("[GeminiClient] API Error HTTP {StatusCode}: {Response}", response.StatusCode, rawResponse);
            throw new HttpRequestException($"Gemini API error (HTTP {response.StatusCode}): {rawResponse}");
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API request failed after {maxRetries} attempts: {rawResponse}");
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
            if (result != null) return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("[GeminiClient] Initial JSON deserialization failed for {Type}: {Message}. Invoking rapid self-healing...",
                typeof(T).Name, ex.Message);

            try
            {
                var repairPrompt = $"""
                    Fix the JSON syntax and structural error in the following JSON text.
                    Ensure all braces/brackets match, strings are properly escaped, and output ONLY valid JSON without markdown code fences or conversational text.
                    
                    Error Details: {ex.Message}
                    
                    Malformed JSON:
                    {cleanJson}
                    """;

                var repairedRaw = await GenerateContentAsync(
                    repairPrompt,
                    "You are a strict JSON syntax repair parser. Output ONLY 100% valid JSON adhering to the input data.",
                    enforceJson: false,
                    cancellationToken);

                var repairedClean = ExtractJson(repairedRaw);
                var repairedResult = JsonSerializer.Deserialize<T>(repairedClean, JsonOptions);
                if (repairedResult != null)
                {
                    _logger.LogInformation("🟢 [GeminiClient] Self-healing repair SUCCESSFUL for {Type}!", typeof(T).Name);
                    return repairedResult;
                }
            }
            catch (Exception repairEx)
            {
                _logger.LogError(repairEx, "[GeminiClient] Secondary self-healing repair failed for {Type}.", typeof(T).Name);
            }

            _logger.LogError(ex, "[GeminiClient] Failed to deserialize JSON into {Type}. Cleaned JSON was:\n{CleanJson}",
                typeof(T).Name, cleanJson);
            throw;
        }

        throw new JsonException($"Deserialization produced null for type {typeof(T).Name}");
    }

    public static string ExtractJson(string responseText)
    {
        var trimmed = responseText.Trim();

        // 1. Check for ```json ... ``` code fence
        var jsonFenceMatch = Regex.Match(trimmed, @"```json\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (jsonFenceMatch.Success)
        {
            trimmed = jsonFenceMatch.Groups[1].Value.Trim();
        }
        else
        {
            var generalFenceMatch = Regex.Match(trimmed, @"```\s*([\s\S]*?)\s*```");
            if (generalFenceMatch.Success)
            {
                var content = generalFenceMatch.Groups[1].Value.Trim();
                if (content.StartsWith("{") || content.StartsWith("["))
                {
                    trimmed = content;
                }
            }
        }

        // 2. Extract between outer matching braces
        var firstBrace = trimmed.IndexOf('{');
        var firstBracket = trimmed.IndexOf('[');
        int startIdx = -1;
        char closeChar = '}';

        if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
        {
            startIdx = firstBrace;
            closeChar = '}';
        }
        else if (firstBracket >= 0)
        {
            startIdx = firstBracket;
            closeChar = ']';
        }

        if (startIdx >= 0)
        {
            var lastIdx = trimmed.LastIndexOf(closeChar);
            if (lastIdx > startIdx)
            {
                return trimmed.Substring(startIdx, lastIdx - startIdx + 1);
            }
        }

        return trimmed;
    }
}

public sealed class FlexibleStringJsonConverter : System.Text.Json.Serialization.JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : (reader.TryGetDouble(out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => string.Empty,
            JsonTokenType.StartObject => ReadRawJson(ref reader),
            JsonTokenType.StartArray => ReadRawJson(ref reader),
            _ => string.Empty
        };
    }

    private static string ReadRawJson(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public sealed class FlexibleListStringJsonConverter : System.Text.Json.Serialization.JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<string>();

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (!string.IsNullOrWhiteSpace(str)) list.Add(str);
            return list;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                list.Add($"{prop.Name}: {prop.Value.GetRawText()}");
            }
            return list;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list;

                if (reader.TokenType == JsonTokenType.String)
                {
                    list.Add(reader.GetString() ?? string.Empty);
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    list.Add(reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
                {
                    list.Add(reader.GetBoolean().ToString());
                }
                else if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    using var itemDoc = JsonDocument.ParseValue(ref reader);
                    list.Add(itemDoc.RootElement.GetRawText());
                }
            }
            return list;
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}
