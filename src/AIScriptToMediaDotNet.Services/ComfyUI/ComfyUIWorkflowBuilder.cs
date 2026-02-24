using System.Text.Json;
using System.Text.Json.Nodes;
using AIScriptToMediaDotNet.Core.Context;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Builds ComfyUI workflow JSON from photo prompts.
/// </summary>
public class ComfyUIWorkflowBuilder
{
    private readonly ILogger<ComfyUIWorkflowBuilder> _logger;
    private readonly JsonNode _baseWorkflow;
    private readonly string _workflowPath;

    /// <summary>
    /// Initializes a new instance of the ComfyUIWorkflowBuilder class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workflowPath">Path to the base workflow JSON file.</param>
    public ComfyUIWorkflowBuilder(
        ILogger<ComfyUIWorkflowBuilder> logger,
        string workflowPath = "ComfyUiWorkflows/ComfyUI_SDXL_Image_Generation.json")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workflowPath = workflowPath;

        // Load the base workflow
        if (File.Exists(workflowPath))
        {
            var workflowJson = File.ReadAllText(workflowPath);
            _baseWorkflow = JsonNode.Parse(workflowJson) 
                ?? throw new InvalidOperationException($"Failed to parse workflow JSON from {workflowPath}");
            _logger.LogDebug("Loaded base workflow from {WorkflowPath}", workflowPath);
        }
        else
        {
            _logger.LogWarning("Base workflow not found at {WorkflowPath}, using default", workflowPath);
            _baseWorkflow = CreateDefaultWorkflow();
        }
    }

    /// <summary>
    /// Builds a ComfyUI prompt from a photo prompt.
    /// </summary>
    /// <param name="photoPrompt">The photo prompt.</param>
    /// <param name="seed">Random seed for generation (optional).</param>
    /// <param name="outputPath">Optional output path to save the workflow JSON for debugging.</param>
    /// <returns>The ComfyUI prompt object.</returns>
    public ComfyuiPrompt BuildPrompt(PhotoPrompt photoPrompt, int? seed = null, string? outputPath = null)
    {
        _logger.LogDebug("Building ComfyUI prompt for {PromptId}", photoPrompt.Id);

        // Clone the base workflow
        var workflow = _baseWorkflow.DeepClone();

        // Build the full prompt text by combining all photo prompt fields
        var fullPrompt = BuildFullPromptText(photoPrompt);
        _logger.LogDebug("Built full prompt text ({Length} characters)", fullPrompt.Length);

        // Update the positive prompt (node 67 in the workflow)
        if (workflow["67"] != null)
        {
            workflow["67"]["inputs"]!["text"] = fullPrompt;
            _logger.LogDebug("Updated node 67 (positive prompt) with {Length} characters", fullPrompt.Length);
        }
        else
        {
            _logger.LogWarning("Node 67 (positive prompt) not found in workflow");
        }

        // Update negative prompt if available (node 71)
        if (!string.IsNullOrEmpty(photoPrompt.NegativePrompt) && workflow["71"] != null)
        {
            workflow["71"]["inputs"]!["text"] = photoPrompt.NegativePrompt;
        }
        else if (workflow["71"] != null)
        {
            // Default negative prompt
            workflow["71"]["inputs"]!["text"] = "blurry, distorted, low quality, watermark, text, signature, bad anatomy, deformed, disfigured";
        }
        else
        {
            _logger.LogWarning("Node 71 (negative prompt) not found in workflow");
        }

        // Update seed if provided (node 69) - ensure positive value
        if (seed.HasValue && workflow["69"] != null)
        {
            // Ensure seed is always positive (ComfyUI requires seed >= 0)
            var positiveSeed = Math.Abs(seed.Value);
            workflow["69"]["inputs"]!["seed"] = positiveSeed;
            _logger.LogDebug("Updated node 69 (KSampler) with seed {Seed}", positiveSeed);
        }
        else if (workflow["69"] != null)
        {
            // Use a random seed if none provided
            var randomSeed = Random.Shared.Next(0, int.MaxValue);
            workflow["69"]["inputs"]!["seed"] = randomSeed;
            _logger.LogDebug("Updated node 69 (KSampler) with random seed {Seed}", randomSeed);
        }
        else
        {
            _logger.LogWarning("Node 69 (KSampler) not found in workflow");
        }

        // Update filename prefix to include scene and prompt IDs (node 9)
        if (workflow["9"] != null)
        {
            var filenamePrefix = $"scene-{photoPrompt.SceneId}-prompt-{photoPrompt.Id}";
            workflow["9"]["inputs"]!["filename_prefix"] = filenamePrefix;
            _logger.LogDebug("Updated node 9 (SaveImage) with filename prefix {Prefix}", filenamePrefix);
        }
        else
        {
            _logger.LogWarning("Node 9 (SaveImage) not found in workflow");
        }

        // Convert to dictionary for the prompt object
        var promptDict = workflow.AsObject().ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value!);
        
        // Log the complete workflow JSON for debugging
        var workflowJson = JsonSerializer.Serialize(promptDict, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogDebug("Complete workflow JSON:\n{Json}", workflowJson);
        
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
    /// <param name="photoPrompt">The photo prompt.</param>
    /// <returns>The combined prompt text.</returns>
    private static string BuildFullPromptText(PhotoPrompt photoPrompt)
    {
        var parts = new List<string>();

        // Start with the main prompt
        if (!string.IsNullOrEmpty(photoPrompt.Prompt))
        {
            parts.Add(photoPrompt.Prompt);
        }

        // Add subject description
        if (!string.IsNullOrEmpty(photoPrompt.Subject))
        {
            parts.Add($"Subject: {photoPrompt.Subject}");
        }

        // Add style
        if (!string.IsNullOrEmpty(photoPrompt.Style))
        {
            parts.Add($"Style: {photoPrompt.Style}");
        }

        // Add lighting
        if (!string.IsNullOrEmpty(photoPrompt.Lighting))
        {
            parts.Add($"Lighting: {photoPrompt.Lighting}");
        }

        // Add composition
        if (!string.IsNullOrEmpty(photoPrompt.Composition))
        {
            parts.Add($"Composition: {photoPrompt.Composition}");
        }

        // Add mood
        if (!string.IsNullOrEmpty(photoPrompt.Mood))
        {
            parts.Add($"Mood: {photoPrompt.Mood}");
        }

        // Add camera details
        if (!string.IsNullOrEmpty(photoPrompt.Camera))
        {
            parts.Add($"Camera: {photoPrompt.Camera}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Creates a default SDXL workflow if the file is not found.
    /// </summary>
    /// <returns>The default workflow JsonNode.</returns>
    private static JsonNode CreateDefaultWorkflow()
    {
        var workflow = new JsonObject
        {
            ["67"] = new JsonObject
            {
                ["inputs"] = new JsonObject
                {
                    ["text"] = "",
                    ["clip"] = new JsonArray { "62", 0 }
                },
                ["class_type"] = "CLIPTextEncode"
            },
            ["71"] = new JsonObject
            {
                ["inputs"] = new JsonObject
                {
                    ["text"] = "blurry, distorted, low quality",
                    ["clip"] = new JsonArray { "62", 0 }
                },
                ["class_type"] = "CLIPTextEncode"
            },
            ["69"] = new JsonObject
            {
                ["inputs"] = new JsonObject
                {
                    ["seed"] = 0,
                    ["steps"] = 25,
                    ["cfg"] = 4,
                    ["sampler_name"] = "res_multistep",
                    ["scheduler"] = "simple",
                    ["denoise"] = 1,
                    ["model"] = new JsonArray { "70", 0 },
                    ["positive"] = new JsonArray { "67", 0 },
                    ["negative"] = new JsonArray { "71", 0 },
                    ["latent_image"] = new JsonArray { "68", 0 }
                },
                ["class_type"] = "KSampler"
            },
            ["68"] = new JsonObject
            {
                ["inputs"] = new JsonObject
                {
                    ["width"] = 1280,
                    ["height"] = 720,
                    ["batch_size"] = 1
                },
                ["class_type"] = "EmptySD3LatentImage"
            },
            ["9"] = new JsonObject
            {
                ["inputs"] = new JsonObject
                {
                    ["filename_prefix"] = "z-image",
                    ["images"] = new JsonArray { "65", 0 }
                },
                ["class_type"] = "SaveImage"
            }
        };

        return workflow;
    }
}
