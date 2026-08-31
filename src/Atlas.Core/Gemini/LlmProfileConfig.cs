using System;
using System.Collections.Generic;

namespace Atlas.Core.Gemini;

/// <summary>
/// Configuration for a named LLM profile.
/// </summary>
public sealed class LlmProfileConfig
{
    /// <summary>
    /// Provider type: "Gemini" | "OpenAICompatible" | "Ollama" | "Local"
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>
    /// Model name or checkpoint (e.g. "gemini-3.7-flash", "qwen2.5-coder:32b", "gemma2:27b", "deepseek-r1").
    /// </summary>
    public string Model { get; set; } = "gemini-3.7-flash";

    /// <summary>
    /// Base URL for the LLM endpoint.
    /// Default for Gemini: https://generativelanguage.googleapis.com/v1beta
    /// Default for Local Ollama: http://localhost:11434/v1
    /// Default for LM Studio: http://localhost:1234/v1
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// API Key (required for Gemini/cloud, optional for local inference).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Maximum context window tokens (default: 131072 for Gemini, 32768 for Qwen, 8192 for Gemma).
    /// </summary>
    public int ContextWindow { get; set; } = 131072;

    /// <summary>
    /// Thinking level / budget for Gemini 3.7 ("high", "medium", "low", "off", or integer tokens).
    /// </summary>
    public string? ThinkingLevel { get; set; } = "high";

    /// <summary>
    /// Temperature for inference (default 0.1 for high precision structured synthesis).
    /// </summary>
    public double Temperature { get; set; } = 0.1;
}

/// <summary>
/// Top-level settings container matching appsettings.json "LLM" section.
/// </summary>
public sealed class LlmSettings
{
    public string DefaultProfile { get; set; } = "gemini";
    public Dictionary<string, LlmProfileConfig> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public LlmProfileConfig GetActiveProfile(string? profileName = null)
    {
        var targetName = string.IsNullOrWhiteSpace(profileName) ? DefaultProfile : profileName;
        if (Profiles.TryGetValue(targetName, out var config))
        {
            return config;
        }

        // Return sensible defaults if profile name is known
        if (targetName.Contains("qwen", StringComparison.OrdinalIgnoreCase))
        {
            return new LlmProfileConfig
            {
                Provider = "OpenAICompatible",
                Model = "qwen2.5-coder:32b",
                BaseUrl = "http://localhost:11434/v1",
                ContextWindow = 32768
            };
        }

        if (targetName.Contains("gemma", StringComparison.OrdinalIgnoreCase))
        {
            return new LlmProfileConfig
            {
                Provider = "OpenAICompatible",
                Model = "gemma2:27b",
                BaseUrl = "http://localhost:11434/v1",
                ContextWindow = 8192
            };
        }

        // Default Gemini profile
        return new LlmProfileConfig
        {
            Provider = "Gemini",
            Model = "gemini-3.7-flash",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
            ContextWindow = 131072
        };
    }
}
