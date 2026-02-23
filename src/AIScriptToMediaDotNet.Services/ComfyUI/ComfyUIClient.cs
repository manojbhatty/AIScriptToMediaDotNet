using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIScriptToMediaDotNet.Services.ComfyUI;

/// <summary>
/// Client for interacting with the ComfyUI API.
/// </summary>
public class ComfyUIClient
{
    private readonly HttpClient _httpClient;
    private readonly ComfyUIOptions _options;
    private readonly ILogger<ComfyUIClient> _logger;
    private readonly string _clientId;

    /// <summary>
    /// Initializes a new instance of the ComfyUIClient class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="options">The ComfyUI options.</param>
    /// <param name="logger">The logger instance.</param>
    public ComfyUIClient(
        HttpClient httpClient,
        IOptions<ComfyUIOptions> options,
        ILogger<ComfyUIClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Tests the connection to ComfyUI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is successful.</returns>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing ComfyUI connection to {Endpoint}", _options.Endpoint);
            var response = await _httpClient.GetAsync("system_stats", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to ComfyUI");
            return false;
        }
    }

    /// <summary>
    /// Queues a prompt for generation.
    /// </summary>
    /// <param name="prompt">The workflow prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The queue response with prompt ID.</returns>
    public async Task<ComfyuiQueueResponse> QueuePromptAsync(ComfyuiPrompt prompt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Queueing prompt to ComfyUI");

        var requestBody = new
        {
            prompt = prompt.Prompt,
            client_id = _clientId
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("prompt", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var queueResponse = JsonSerializer.Deserialize<ComfyuiQueueResponse>(responseJson)
            ?? throw new InvalidOperationException("Failed to deserialize queue response");

        _logger.LogInformation("Prompt queued with ID: {PromptId}", queueResponse.PromptId);
        return queueResponse;
    }

    /// <summary>
    /// Gets the status of a generation task.
    /// </summary>
    /// <param name="promptId">The prompt ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task status.</returns>
    public async Task<ComfyuiStatus> GetTaskStatusAsync(string promptId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting status for prompt {PromptId}", promptId);

        var response = await _httpClient.GetAsync($"history/{promptId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Parse the history response
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty(promptId, out var promptHistory))
        {
            return new ComfyuiStatus
            {
                StatusType = "pending",
                Completed = false
            };
        }

        var status = new ComfyuiStatus();
        
        if (promptHistory.TryGetProperty("status", out var statusElement))
        {
            if (statusElement.TryGetProperty("status_str", out var statusStr))
            {
                status.StatusType = statusStr.GetString() ?? "unknown";
            }
            
            status.Completed = statusStr.GetString() == "success";
        }

        if (promptHistory.TryGetProperty("messages", out var messagesElement))
        {
            foreach (var message in messagesElement.EnumerateArray())
            {
                if (message.ValueKind == System.Text.Json.JsonValueKind.Array && message.GetArrayLength() > 1)
                {
                    var messageData = message[1];
                    if (messageData.TryGetProperty("text", out var text))
                    {
                        status.Messages.Add(text.GetString() ?? "");
                    }
                }
            }
        }

        return status;
    }

    /// <summary>
    /// Waits for a generation task to complete.
    /// </summary>
    /// <param name="promptId">The prompt ID.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="pollIntervalMs">Polling interval in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final status.</returns>
    public async Task<ComfyuiStatus> WaitForCompletionAsync(
        string promptId,
        int timeoutSeconds = 300,
        int pollIntervalMs = 1000,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Waiting for prompt {PromptId} to complete (timeout: {Timeout}s)", 
            promptId, timeoutSeconds);

        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
        {
            var status = await GetTaskStatusAsync(promptId, cancellationToken);

            if (status.Completed)
            {
                _logger.LogInformation("Prompt {PromptId} completed successfully", promptId);
                return status;
            }

            if (status.StatusType == "error")
            {
                _logger.LogError("Prompt {PromptId} failed with errors: {Errors}", 
                    promptId, string.Join("; ", status.Messages));
                return status;
            }

            await Task.Delay(pollIntervalMs, cancellationToken);
        }

        _logger.LogWarning("Timeout waiting for prompt {PromptId} to complete", promptId);
        return new ComfyuiStatus
        {
            StatusType = "timeout",
            Completed = false,
            Messages = { "Timeout waiting for generation to complete" }
        };
    }

    /// <summary>
    /// Downloads a generated image.
    /// </summary>
    /// <param name="image">The image metadata.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the downloaded image file.</returns>
    public async Task<string> DownloadImageAsync(
        ComfyuiImage image,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading image {Filename} to {OutputPath}", 
            image.Filename, outputPath);

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(outputPath);

        // Build the download URL
        var downloadUrl = $"view?filename={image.Filename}&subfolder={image.Subfolder}&type={image.Type}";
        
        var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Save the image
        var outputPathFull = Path.Combine(outputPath, image.Filename);
        await using var fileStream = new FileStream(outputPathFull, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream, cancellationToken);

        _logger.LogInformation("Image downloaded to {OutputPath}", outputPathFull);
        return outputPathFull;
    }

    /// <summary>
    /// Gets all images from a completed generation task.
    /// </summary>
    /// <param name="promptId">The prompt ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of generated images.</returns>
    public async Task<List<ComfyuiImage>> GetGeneratedImagesAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting images for prompt {PromptId}", promptId);

        var response = await _httpClient.GetAsync($"history/{promptId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var images = new List<ComfyuiImage>();

        if (!root.TryGetProperty(promptId, out var promptHistory))
        {
            return images;
        }

        if (promptHistory.TryGetProperty("outputs", out var outputsElement))
        {
            foreach (var nodeOutputs in outputsElement.EnumerateObject())
            {
                var outputs = nodeOutputs.Value;
                
                if (outputs.TryGetProperty("images", out var imagesElement))
                {
                    foreach (var imageElement in imagesElement.EnumerateArray())
                    {
                        var image = new ComfyuiImage();
                        
                        if (imageElement.TryGetProperty("filename", out var filename))
                        {
                            image.Filename = filename.GetString() ?? "";
                        }
                        
                        if (imageElement.TryGetProperty("subfolder", out var subfolder))
                        {
                            image.Subfolder = subfolder.GetString() ?? "";
                        }
                        
                        if (imageElement.TryGetProperty("type", out var type))
                        {
                            image.Type = type.GetString() ?? "output";
                        }

                        if (!string.IsNullOrEmpty(image.Filename))
                        {
                            images.Add(image);
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Found {ImageCount} images for prompt {PromptId}", images.Count, promptId);
        return images;
    }

    /// <summary>
    /// Generates an image from a prompt and waits for completion.
    /// </summary>
    /// <param name="prompt">The workflow prompt.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of generated image file paths.</returns>
    public async Task<List<string>> GenerateImageAsync(
        ComfyuiPrompt prompt,
        string outputPath,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting image generation");

        // Queue the prompt
        var queueResponse = await QueuePromptAsync(prompt, cancellationToken);

        // Wait for completion
        var status = await WaitForCompletionAsync(
            queueResponse.PromptId, 
            timeoutSeconds, 
            1000, 
            cancellationToken);

        if (!status.Completed)
        {
            throw new InvalidOperationException($"Generation failed: {string.Join("; ", status.Messages)}");
        }

        // Get generated images
        var images = await GetGeneratedImagesAsync(queueResponse.PromptId, cancellationToken);

        // Download all images
        var downloadedPaths = new List<string>();
        foreach (var image in images)
        {
            var path = await DownloadImageAsync(image, outputPath, cancellationToken);
            downloadedPaths.Add(path);
        }

        _logger.LogInformation("Generated {ImageCount} images", downloadedPaths.Count);
        return downloadedPaths;
    }
}
