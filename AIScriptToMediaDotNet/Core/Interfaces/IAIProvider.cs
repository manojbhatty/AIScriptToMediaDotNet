using AIScriptToMediaDotNet.Core.Options;

namespace AIScriptToMediaDotNet.Core.Interfaces;

/// <summary>
/// Provides AI inference capabilities for agents.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Gets the name of the provider (e.g., "Ollama", "OpenAI", "Anthropic").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Generates a response from the AI model.
    /// </summary>
    /// <param name="prompt">The prompt to send to the model.</param>
    /// <param name="options">Model configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response text.</returns>
    Task<string> GenerateResponseAsync(
        string prompt,
        ModelOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the provider is available and responding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the provider is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
