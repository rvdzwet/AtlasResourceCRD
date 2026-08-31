using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Gemini;

/// <summary>
/// Factory for creating ILlmClient instances from profile configurations or ad-hoc settings.
/// </summary>
public static class LlmClientFactory
{
    public static ILlmClient Create(
        LlmProfileConfig config,
        ILoggerFactory loggerFactory,
        HttpClient? httpClient = null)
    {
        var http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var provider = config.Provider?.ToLowerInvariant() ?? "gemini";

        if (provider is "openaicompatible" or "openai" or "ollama" or "local" or "vllm" or "lmstudio")
        {
            var logger = loggerFactory.CreateLogger<OpenAiCompatibleLlmClient>();
            return new OpenAiCompatibleLlmClient(http, config, logger);
        }

        // Default to Google Gemini native client
        var geminiOptions = new GeminiClientOptions
        {
            ApiKey = config.ApiKey ?? "",
            Model = !string.IsNullOrWhiteSpace(config.Model) ? config.Model : "gemini-3.7-flash",
            ContextWindow = config.ContextWindow > 0 ? config.ContextWindow : 131072,
            Temperature = config.Temperature
        };

        if (!string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            geminiOptions.BaseUrl = config.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(config.ThinkingLevel))
        {
            geminiOptions.ApplyThinkingLevel(config.ThinkingLevel);
        }

        var geminiLogger = loggerFactory.CreateLogger<GeminiClient>();
        return new GeminiClient(http, geminiOptions, geminiLogger);
    }
}
