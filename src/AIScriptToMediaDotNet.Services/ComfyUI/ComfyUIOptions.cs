namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Configuration options for ComfyUI client.
/// </summary>
public class ComfyUIOptions
{
    /// <summary>
    /// Gets or sets the ComfyUI API endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:8188";

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the polling interval in milliseconds.
    /// </summary>
    public int PollIntervalMs { get; set; } = 1000;
}
