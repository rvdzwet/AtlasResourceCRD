using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Core.Gemini;

public static class LlmClientExtensions
{
    public static async Task<T> GenerateStructuredAsync<T>(
        this ILlmClient client,
        string userPrompt,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default)
    {
        var rawJson = await client.GenerateContentAsync(userPrompt, systemInstruction, enforceJson: true, cancellationToken);
        var cleanJson = ExtractJson(rawJson);

        try
        {
            var result = JsonSerializer.Deserialize<T>(cleanJson, GeminiClient.JsonOptions);
            if (result != null) return result;
        }
        catch (JsonException)
        {
            try
            {
                var repairPrompt = $"""
                    Fix the JSON syntax and structural error in the following JSON text.
                    Ensure all braces/brackets match, strings are properly escaped, and output ONLY valid JSON without markdown code fences or conversational text.
                    
                    Malformed JSON:
                    {cleanJson}
                    """;

                var repairedRaw = await client.GenerateContentAsync(
                    repairPrompt,
                    "You are a strict JSON syntax repair parser. Output ONLY 100% valid JSON adhering to the input data.",
                    enforceJson: false,
                    cancellationToken);

                var repairedClean = ExtractJson(repairedRaw);
                var repairedResult = JsonSerializer.Deserialize<T>(repairedClean, GeminiClient.JsonOptions);
                if (repairedResult != null)
                {
                    return repairedResult;
                }
            }
            catch { }

            // Final fallback: try lenient fallback parsing
            throw;
        }

        throw new InvalidOperationException($"Could not deserialize JSON response into {typeof(T).Name}");
    }

    public static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var trimmed = text.Trim();

        var match = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        int firstBracket = trimmed.IndexOf('[');
        int lastBracket = trimmed.LastIndexOf(']');

        if (firstBracket >= 0 && lastBracket > firstBracket && (firstBrace < 0 || firstBracket < firstBrace))
        {
            return trimmed.Substring(firstBracket, lastBracket - firstBracket + 1);
        }

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return trimmed;
    }
}
