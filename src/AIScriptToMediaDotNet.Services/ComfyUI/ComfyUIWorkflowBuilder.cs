using System.Text.Json;
using System.Text.Json.Nodes;
using AIScriptToMediaDotNet.Core.Context;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Builds ComfyUI workflow JSON from photo prompts.
/// Works with any ComfyUI workflow JSON by discovering nodes dynamically.
/// </summary>
public class ComfyUIWorkflowBuilder
{
    private readonly ILogger<ComfyUIWorkflowBuilder> _logger;
    private readonly IWorkflowTemplateProvider _templateProvider;
    private readonly WorkflowNodeMapping _nodeMapping;

    /// <summary>
    /// Initializes a new instance of the ComfyUIWorkflowBuilder class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="templateProvider">The workflow template provider.</param>
    /// <param name="nodeMapping">Optional node mapping configuration. If null, auto-discovery will be used.</param>
    public ComfyUIWorkflowBuilder(
        ILogger<ComfyUIWorkflowBuilder> logger,
        IWorkflowTemplateProvider templateProvider,
        WorkflowNodeMapping? nodeMapping = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
        _nodeMapping = nodeMapping ?? new WorkflowNodeMapping();
        _logger.LogDebug("Initialized with workflow: {WorkflowName}", templateProvider.GetWorkflowName());
    }

    /// <summary>
    /// Builds a ComfyUI prompt from a photo prompt.
    /// </summary>
    /// <param name="photoPrompt">The photo prompt.</param>
    /// <param name="seed">Random seed for generation (optional).</param>
    /// <param name="outputPath">Optional output path to save the workflow JSON for debugging.</param>
    /// <returns>The ComfyUI prompt object.</returns>
    public async Task<ComfyuiPrompt> BuildPromptAsync(PhotoPrompt photoPrompt, int? seed = null, string? outputPath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Building ComfyUI prompt for {PromptId}", photoPrompt.Id);

        // Load and clone the base workflow
        var template = await _templateProvider.GetWorkflowTemplateAsync(cancellationToken);
        var workflow = template.DeepClone();

        // Auto-discover nodes if not configured
        var mapping = _nodeMapping;
        if (ShouldAutoDiscover(mapping))
        {
            mapping = AutoDiscoverNodes(workflow);
            _logger.LogDebug("Auto-discovered node mapping: Positive={Positive}, Negative={Negative}, Sampler={Sampler}, SaveImage={SaveImage}",
                mapping.PositivePromptNodeId, mapping.NegativePromptNodeId, mapping.SamplerNodeId, mapping.SaveImageNodeId);
        }

        // Build the full prompt text
        var fullPrompt = BuildFullPromptText(photoPrompt);
        _logger.LogDebug("Built full prompt text ({Length} characters)", fullPrompt.Length);

        // Update positive prompt
        if (!string.IsNullOrEmpty(mapping.PositivePromptNodeId) && workflow[mapping.PositivePromptNodeId] != null)
        {
            var node = workflow[mapping.PositivePromptNodeId]!;
            if (node["inputs"] != null)
            {
                node["inputs"]["text"] = fullPrompt;
                _logger.LogDebug("Updated positive prompt node {NodeId} with {Length} characters",
                    mapping.PositivePromptNodeId, fullPrompt.Length);
            }
        }
        else
        {
            // Try to find any CLIPTextEncode node as fallback
            var fallbackNode = FindNodeByClassType(workflow, "CLIPTextEncode");
            if (fallbackNode.HasValue)
            {
                fallbackNode.Value.node["inputs"]!["text"] = fullPrompt;
                _logger.LogDebug("Updated fallback positive prompt node {NodeId}", fallbackNode.Value.nodeId);
            }
            else
            {
                _logger.LogWarning("No positive prompt node found in workflow");
            }
        }

        // Update negative prompt
        if (!string.IsNullOrEmpty(mapping.NegativePromptNodeId) && workflow[mapping.NegativePromptNodeId] != null)
        {
            var node = workflow[mapping.NegativePromptNodeId]!;
            if (node["inputs"] != null)
            {
                node["inputs"]["text"] = !string.IsNullOrEmpty(photoPrompt.NegativePrompt)
                    ? photoPrompt.NegativePrompt
                    : "blurry, distorted, low quality, watermark, text, signature, bad anatomy, deformed, disfigured";
                _logger.LogDebug("Updated negative prompt node {NodeId}", mapping.NegativePromptNodeId);
            }
        }
        else
        {
            // Try to find second CLIPTextEncode node as fallback
            var positiveNodeId = mapping.PositivePromptNodeId ?? FindNodeByClassType(workflow, "CLIPTextEncode")?.nodeId;
            var fallbackNode = FindSecondNodeByClassType(workflow, "CLIPTextEncode", positiveNodeId);
            if (fallbackNode.HasValue)
            {
                fallbackNode.Value.node["inputs"]!["text"] = "blurry, distorted, low quality, watermark, text, signature, bad anatomy, deformed, disfigured";
                _logger.LogDebug("Updated fallback negative prompt node {NodeId}", fallbackNode.Value.nodeId);
            }
        }

        // Update seed in sampler node
        var seedToUse = seed ?? Random.Shared.Next(0, int.MaxValue);
        if (!string.IsNullOrEmpty(mapping.SamplerNodeId) && workflow[mapping.SamplerNodeId] != null)
        {
            var node = workflow[mapping.SamplerNodeId]!;
            if (node["inputs"] != null)
            {
                // Try "seed" first, then "noise_seed" for KSamplerAdvanced
                if (node["inputs"]["seed"] != null)
                {
                    node["inputs"]["seed"] = Math.Abs(seedToUse);
                    _logger.LogDebug("Updated sampler node {NodeId} with seed {Seed}", mapping.SamplerNodeId, Math.Abs(seedToUse));
                }
                else if (node["inputs"]["noise_seed"] != null)
                {
                    node["inputs"]["noise_seed"] = Math.Abs(seedToUse);
                    _logger.LogDebug("Updated sampler node {NodeId} with noise_seed {Seed}", mapping.SamplerNodeId, Math.Abs(seedToUse));
                }
            }
        }
        else
        {
            // Try to find any KSampler node as fallback
            var fallbackNode = FindNodeByClassTypes(workflow, new[] { "KSampler", "KSamplerAdvanced" });
            if (fallbackNode.HasValue)
            {
                if (fallbackNode.Value.node["inputs"]!["seed"] != null)
                {
                    fallbackNode.Value.node["inputs"]!["seed"] = Math.Abs(seedToUse);
                    _logger.LogDebug("Updated fallback sampler node {NodeId} with seed {Seed}", fallbackNode.Value.nodeId, Math.Abs(seedToUse));
                }
                else if (fallbackNode.Value.node["inputs"]!["noise_seed"] != null)
                {
                    fallbackNode.Value.node["inputs"]!["noise_seed"] = Math.Abs(seedToUse);
                    _logger.LogDebug("Updated fallback sampler node {NodeId} with noise_seed {Seed}", fallbackNode.Value.nodeId, Math.Abs(seedToUse));
                }
            }
        }

        // Update filename prefix in save image node
        if (!string.IsNullOrEmpty(mapping.SaveImageNodeId) && workflow[mapping.SaveImageNodeId] != null)
        {
            var node = workflow[mapping.SaveImageNodeId]!;
            if (node["inputs"] != null)
            {
                var filenamePrefix = $"scene-{photoPrompt.SceneId}-prompt-{photoPrompt.Id}";
                node["inputs"]["filename_prefix"] = filenamePrefix;
                _logger.LogDebug("Updated save image node {NodeId} with filename prefix {Prefix}",
                    mapping.SaveImageNodeId, filenamePrefix);
            }
        }
        else
        {
            // Try to find any SaveImage node as fallback
            var fallbackNode = FindNodeByClassTypes(workflow, new[] { "SaveImage", "PreviewImage" });
            if (fallbackNode.HasValue)
            {
                var filenamePrefix = $"scene-{photoPrompt.SceneId}-prompt-{photoPrompt.Id}";
                fallbackNode.Value.node["inputs"]!["filename_prefix"] = filenamePrefix;
                _logger.LogDebug("Updated fallback save image node {NodeId} with filename prefix {Prefix}",
                    fallbackNode.Value.nodeId, filenamePrefix);
            }
        }

        // Convert to dictionary
        var promptDict = workflow.AsObject().ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value!);

        // Log the workflow JSON for debugging
        var workflowJson = JsonSerializer.Serialize(promptDict, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogDebug("Complete workflow JSON generated");

        // Save the workflow JSON to the output folder if specified
        if (!string.IsNullOrEmpty(outputPath))
        {
            try
            {
                var workflowsFolder = Path.Combine(outputPath, "workflows");
                Directory.CreateDirectory(workflowsFolder);
                var workflowFilePath = Path.Combine(workflowsFolder, $"workflow_{photoPrompt.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(workflowFilePath, workflowJson);
                _logger.LogInformation("Workflow JSON saved to: {FilePath}", workflowFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save workflow JSON to output folder");
            }
        }

        return new ComfyuiPrompt
        {
            Prompt = promptDict,
            ClientId = Guid.NewGuid().ToString("N")
        };
    }

    /// <summary>
    /// Builds the full prompt text by combining all photo prompt fields.
    /// </summary>
    private static string BuildFullPromptText(PhotoPrompt photoPrompt)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(photoPrompt.Prompt))
            parts.Add(photoPrompt.Prompt);

        if (!string.IsNullOrEmpty(photoPrompt.Subject))
            parts.Add($"Subject: {photoPrompt.Subject}");

        if (!string.IsNullOrEmpty(photoPrompt.Style))
            parts.Add($"Style: {photoPrompt.Style}");

        if (!string.IsNullOrEmpty(photoPrompt.Lighting))
            parts.Add($"Lighting: {photoPrompt.Lighting}");

        if (!string.IsNullOrEmpty(photoPrompt.Composition))
            parts.Add($"Composition: {photoPrompt.Composition}");

        if (!string.IsNullOrEmpty(photoPrompt.Mood))
            parts.Add($"Mood: {photoPrompt.Mood}");

        if (!string.IsNullOrEmpty(photoPrompt.Camera))
            parts.Add($"Camera: {photoPrompt.Camera}");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Determines if auto-discovery should be used.
    /// </summary>
    private static bool ShouldAutoDiscover(WorkflowNodeMapping mapping)
    {
        return string.IsNullOrEmpty(mapping.PositivePromptNodeId) ||
               string.IsNullOrEmpty(mapping.NegativePromptNodeId) ||
               string.IsNullOrEmpty(mapping.SamplerNodeId) ||
               string.IsNullOrEmpty(mapping.SaveImageNodeId);
    }

    /// <summary>
    /// Auto-discovers node mappings from the workflow.
    /// </summary>
    private WorkflowNodeMapping AutoDiscoverNodes(JsonNode workflow)
    {
        var mapping = new WorkflowNodeMapping();
        var workflowObject = workflow.AsObject();

        // Find CLIPTextEncode nodes
        var clipTextEncodeNodes = new List<string>();
        foreach (var prop in workflowObject)
        {
            var node = prop.Value;
            if (node?["class_type"]?.GetValue<string>() == "CLIPTextEncode")
            {
                clipTextEncodeNodes.Add(prop.Key);
            }
        }

        // First CLIPTextEncode is typically positive, second is negative
        if (clipTextEncodeNodes.Count > 0)
            mapping.PositivePromptNodeId = clipTextEncodeNodes[0];
        if (clipTextEncodeNodes.Count > 1)
            mapping.NegativePromptNodeId = clipTextEncodeNodes[1];

        // Find sampler node
        var samplerNode = FindNodeByClassTypes(workflow, new[] { "KSampler", "KSamplerAdvanced" });
        mapping.SamplerNodeId = samplerNode?.nodeId;

        // Find save image node
        var saveImageNode = FindNodeByClassTypes(workflow, new[] { "SaveImage", "PreviewImage" });
        mapping.SaveImageNodeId = saveImageNode?.nodeId;

        // Find latent image node
        var latentNode = FindNodeByClassTypes(workflow, new[] { "EmptyLatentImage", "EmptySD3LatentImage" });
        mapping.LatentImageNodeId = latentNode?.nodeId;

        return mapping;
    }

    /// <summary>
    /// Finds a node by its class type.
    /// </summary>
    private static (string nodeId, JsonNode node)? FindNodeByClassType(JsonNode workflow, string classType)
    {
        var workflowObject = workflow.AsObject();
        foreach (var prop in workflowObject)
        {
            var node = prop.Value;
            if (node?["class_type"]?.GetValue<string>() == classType)
            {
                return (prop.Key, node);
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a node by multiple possible class types.
    /// </summary>
    private static (string nodeId, JsonNode node)? FindNodeByClassTypes(JsonNode workflow, string[] classTypes)
    {
        var workflowObject = workflow.AsObject();
        foreach (var prop in workflowObject)
        {
            var node = prop.Value;
            var nodeClassType = node?["class_type"]?.GetValue<string>();
            if (nodeClassType != null && classTypes.Contains(nodeClassType))
            {
                return (prop.Key, node);
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the second node by class type, excluding a specific node ID.
    /// </summary>
    private static (string nodeId, JsonNode node)? FindSecondNodeByClassType(JsonNode workflow, string classType, string? excludeNodeId)
    {
        var workflowObject = workflow.AsObject();
        foreach (var prop in workflowObject)
        {
            if (prop.Key == excludeNodeId)
                continue;

            var node = prop.Value;
            if (node?["class_type"]?.GetValue<string>() == classType)
            {
                return (prop.Key, node);
            }
        }
        return null;
    }
}
