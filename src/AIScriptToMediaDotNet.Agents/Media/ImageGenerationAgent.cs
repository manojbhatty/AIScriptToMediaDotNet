using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Services.ComfyUI;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Agents.Media;

/// <summary>
/// Input for image generation containing photo prompts and output path.
/// </summary>
public class ImageGenerationInput
{
    /// <summary>
    /// Gets or sets the list of photo prompts to generate images for.
    /// </summary>
    public List<PhotoPrompt> PhotoPrompts { get; set; } = new();

    /// <summary>
    /// Gets or sets the output directory path.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
}

/// <summary>
/// Result of image generation containing generated images and errors.
/// </summary>
public class ImageGenerationResult
{
    /// <summary>
    /// Gets or sets the list of successfully generated images.
    /// </summary>
    public List<GeneratedImage> Images { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of errors for failed generations.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether all images were generated successfully.
    /// </summary>
    public bool AllSuccess => Errors.Count == 0;

    /// <summary>
    /// Gets or sets a value indicating whether at least some images were generated.
    /// </summary>
    public bool HasSuccess => Images.Any(i => i.Success);
}

/// <summary>
/// Agent that generates images from photo prompts using ComfyUI.
/// </summary>
public class ImageGenerationAgent : IAgent<ImageGenerationInput, ImageGenerationResult>
{
    private readonly ComfyUIClient _comfyUIClient;
    private readonly ComfyUIWorkflowBuilder _workflowBuilder;
    private readonly ILogger<ImageGenerationAgent> _logger;

    /// <inheritdoc />
    public string Name => "ImageGenerator";

    /// <inheritdoc />
    public string Description => "Generates images from photo prompts using ComfyUI";

    /// <summary>
    /// Initializes a new instance of the ImageGenerationAgent class.
    /// </summary>
    /// <param name="comfyUIClient">The ComfyUI client.</param>
    /// <param name="workflowBuilder">The workflow builder.</param>
    /// <param name="logger">The logger instance.</param>
    public ImageGenerationAgent(
        ComfyUIClient comfyUIClient,
        ComfyUIWorkflowBuilder workflowBuilder,
        ILogger<ImageGenerationAgent> logger)
    {
        _comfyUIClient = comfyUIClient ?? throw new ArgumentNullException(nameof(comfyUIClient));
        _workflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentResult<ImageGenerationResult>> ProcessAsync(ImageGenerationInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting image generation for {PromptCount} photo prompts", input.PhotoPrompts.Count);

            // Test ComfyUI connection
            _logger.LogInformation("Testing ComfyUI connection...");
            var isConnected = await _comfyUIClient.TestConnectionAsync(cancellationToken);
            if (!isConnected)
            {
                _logger.LogWarning("ComfyUI is not available. Skipping image generation.");
                stopwatch.Stop();
                return AgentResult<ImageGenerationResult>.Ok(new ImageGenerationResult
                {
                    Errors = { "ComfyUI is not available. Skipping image generation." }
                });
            }

            _logger.LogInformation("ComfyUI connection successful");

            // Generate images one at a time
            var result = await GenerateImagesAsync(input, cancellationToken);

            _logger.LogInformation("Image generation complete: {SuccessCount}/{TotalCount} successful", 
                result.Images.Count(i => i.Success), input.PhotoPrompts.Count);

            stopwatch.Stop();
            return result.Errors.Any() && !result.HasSuccess
                ? AgentResult<ImageGenerationResult>.Fail(string.Join("; ", result.Errors))
                : AgentResult<ImageGenerationResult>.Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Image generation failed");
            return AgentResult<ImageGenerationResult>.Fail($"Generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates images one at a time, logging errors for failures.
    /// </summary>
    /// <param name="input">The input containing photo prompts and output path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The image generation result.</returns>
    private async Task<ImageGenerationResult> GenerateImagesAsync(ImageGenerationInput input, CancellationToken cancellationToken = default)
    {
        var result = new ImageGenerationResult();
        // Use the provided output path or default to ./output
        var outputPath = string.IsNullOrEmpty(input.OutputPath) ? "./output" : input.OutputPath;

        _logger.LogInformation("Image generation will save to: {OutputPath}", outputPath);

        // Ensure output directory exists
        Directory.CreateDirectory(outputPath);

        // Process images ONE AT A TIME - await each generation before starting next
        foreach (var photoPrompt in input.PhotoPrompts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Image generation cancelled by user");
                break;
            }

            try
            {
                _logger.LogInformation("=== Starting image {Current}/{Total} for {PromptId} (Scene {SceneId}) ===",
                    input.PhotoPrompts.IndexOf(photoPrompt) + 1, input.PhotoPrompts.Count,
                    photoPrompt.Id, photoPrompt.SceneId);

                // Build ComfyUI workflow with a positive random seed
                // Use uint range to ensure always positive (0 to int.MaxValue)
                var seed = Random.Shared.Next(0, int.MaxValue);
                var comfyPrompt = await _workflowBuilder.BuildPromptAsync(photoPrompt, seed, outputPath, cancellationToken);

                _logger.LogInformation("Workflow built, now queueing to ComfyUI...");

                // Generate image - this awaits until COMPLETE before continuing
                var imagePaths = await _comfyUIClient.GenerateImageAsync(
                    comfyPrompt,
                    outputPath,
                    timeoutSeconds: 300,
                    cancellationToken);

                _logger.LogInformation("Image generation complete for {PromptId}, got {Count} images",
                    photoPrompt.Id, imagePaths.Count);

                // Create generated image records
                foreach (var imagePath in imagePaths)
                {
                    var generatedImage = new GeneratedImage
                    {
                        PromptId = photoPrompt.Id,
                        SceneId = photoPrompt.SceneId,
                        PhotoPromptId = photoPrompt.Id,
                        FilePath = imagePath,
                        Success = true,
                        GeneratedAt = DateTime.UtcNow
                    };

                    result.Images.Add(generatedImage);
                    _logger.LogInformation("✓ Image generated successfully: {FilePath}", imagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate image for {PromptId} (Scene {SceneId})",
                    photoPrompt.Id, photoPrompt.SceneId);

                // Log error but continue with next prompt
                var errorMessage = $"Failed to generate image for {photoPrompt.Id} (Scene {photoPrompt.SceneId}): {ex.Message}";
                result.Errors.Add(errorMessage);

                // Add a failed record for tracking
                result.Images.Add(new GeneratedImage
                {
                    PromptId = photoPrompt.Id,
                    SceneId = photoPrompt.SceneId,
                    PhotoPromptId = photoPrompt.Id,
                    FilePath = string.Empty,
                    Success = false,
                    ErrorMessage = ex.Message,
                    GeneratedAt = DateTime.UtcNow
                });
            }
        }

        _logger.LogInformation("=== All image generations complete ===");
        return result;
    }
}
