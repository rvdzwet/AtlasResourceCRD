using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Core.Gemini;

/// <summary>
/// Universal LLM Client interface supporting native Google Gemini and OpenAI-compatible inference backends
/// (such as Ollama, LM Studio, vLLM, llama.cpp, LocalAI, Qwen 2.5 Coder, Gemma 2, DeepSeek-R1).
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Identifier of the provider (e.g. "Gemini", "OpenAICompatible", "Ollama", "Local").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Model name or checkpoint (e.g. "gemini-3.7-flash", "qwen2.5-coder:32b", "gemma2:27b").
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Configured context window token budget (e.g. 131072 for Gemini, 32768 for Qwen, 8192 for Gemma).
    /// </summary>
    int ContextWindowTokens { get; }

    /// <summary>
    /// Generates structured or raw text response from the model.
    /// </summary>
    Task<string> GenerateContentAsync(
        string userPrompt,
        string? systemInstruction = null,
        bool enforceJson = true,
        CancellationToken cancellationToken = default);
}
