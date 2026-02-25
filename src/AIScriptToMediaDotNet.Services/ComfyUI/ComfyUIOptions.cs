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

    /// <summary>
    /// Gets or sets the path to the workflow JSON file.
    /// </summary>
    public string WorkflowPath { get; set; } = "ComfyUiWorkflows/ComfyUIWorkflow.json";

    /// <summary>
    /// Gets or sets the node mapping configuration for the workflow.
    /// If not specified, nodes will be auto-discovered.
    /// </summary>
    public WorkflowNodeMapping? NodeMapping { get; set; }
}
