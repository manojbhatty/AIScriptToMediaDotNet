namespace AIScriptToMediaDotNet.Core.Options;

/// <summary>
/// Configuration options for AI model inference.
/// </summary>
public class ModelOptions
{
    /// <summary>
    /// The model name to use (e.g., "qwen3:latestt", "mistral", "gpt-4").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens to generate in the response.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for sampling (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Top-p sampling threshold (nucleus sampling).
    /// </summary>
    public double TopP { get; set; } = 0.9;

    /// <summary>
    /// Seed for reproducible generation (-1 for random).
    /// </summary>
    public int? Seed { get; set; } = -1;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Creates a copy of these options.
    /// </summary>
    public ModelOptions Clone()
    {
        return new ModelOptions
        {
            Model = this.Model,
            MaxTokens = this.MaxTokens,
            Temperature = this.Temperature,
            TopP = this.TopP,
            Seed = this.Seed,
            TimeoutSeconds = this.TimeoutSeconds
        };
    }
}
