using HomeLab.Cli.Models.AI;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for LLM provider interactions.
/// Supports multiple backends (Anthropic, Ollama, OpenAI).
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Sends a prompt to the LLM and returns the response.
    /// </summary>
    Task<LlmResponse> SendMessageAsync(string systemPrompt, string userMessage, int maxTokens = 1024);

    /// <summary>
    /// Checks if the LLM service is configured and reachable.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Gets the name of the configured provider.
    /// </summary>
    string ProviderName { get; }
}
