namespace AIScriptToMediaDotNet.Core.Options;

/// <summary>
/// Configuration options for Ollama provider.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// The Ollama API endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default model to use when not specified.
    /// </summary>
    public string DefaultModel { get; set; } = "llama3.1";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Number of retries for failed requests.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 2;
}
