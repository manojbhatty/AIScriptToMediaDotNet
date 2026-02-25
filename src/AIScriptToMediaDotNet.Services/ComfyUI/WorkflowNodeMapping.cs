namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Maps semantic node roles to node IDs in a ComfyUI workflow.
/// This allows the workflow builder to work with any workflow JSON structure.
/// </summary>
public class WorkflowNodeMapping
{
    /// <summary>
    /// Gets or sets the node ID for the positive prompt text encoder.
    /// </summary>
    public string? PositivePromptNodeId { get; set; }

    /// <summary>
    /// Gets or sets the node ID for the negative prompt text encoder.
    /// </summary>
    public string? NegativePromptNodeId { get; set; }

    /// <summary>
    /// Gets or sets the node ID for the sampler (KSampler, KSamplerAdvanced, etc.).
    /// </summary>
    public string? SamplerNodeId { get; set; }

    /// <summary>
    /// Gets or sets the node ID for the save image node.
    /// </summary>
    public string? SaveImageNodeId { get; set; }

    /// <summary>
    /// Gets or sets the node ID for the latent image generator.
    /// </summary>
    public string? LatentImageNodeId { get; set; }

    /// <summary>
    /// Gets or sets the class types to search for when auto-discovering nodes.
    /// </summary>
    public NodeClassTypes ClassTypes { get; set; } = new();
}

/// <summary>
/// Default class types for common node roles.
/// </summary>
public class NodeClassTypes
{
    /// <summary>
    /// Gets or sets the class types for text encoders (positive/negative prompts).
    /// </summary>
    public List<string> TextEncoder { get; set; } = new() { "CLIPTextEncode" };

    /// <summary>
    /// Gets or sets the class types for samplers.
    /// </summary>
    public List<string> Sampler { get; set; } = new() { "KSampler", "KSamplerAdvanced", "KSamplerSelect" };

    /// <summary>
    /// Gets or sets the class types for save image nodes.
    /// </summary>
    public List<string> SaveImage { get; set; } = new() { "SaveImage", "PreviewImage" };

    /// <summary>
    /// Gets or sets the class types for latent image generators.
    /// </summary>
    public List<string> LatentImage { get; set; } = new() { "EmptyLatentImage", "EmptySD3LatentImage" };
}
