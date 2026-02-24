using System.Text.Json;
using AIScriptToMediaDotNet.Agents.Base;
using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Prompts;
using Microsoft.Extensions.Logging;

using Scene = AIScriptToMediaDotNet.Core.Context.Scene;

namespace AIScriptToMediaDotNet.Agents.Photo;

/// <summary>
/// Input for photo prompt creation, optionally including feedback from previous attempts.
/// </summary>
public class PhotoPromptCreatorInput
{
    public List<AIScriptToMediaDotNet.Core.Context.Scene> Scenes { get; set; } = new();
    public string? Feedback { get; set; }
}

/// <summary>
/// Agent that creates detailed photo prompts from verified scenes.
/// </summary>
public class PhotoPromptCreatorAgent : CreatorAgent<PhotoPromptCreatorInput, List<PhotoPrompt>>
{
    private readonly AgentPrompts _prompts;

    /// <inheritdoc />
    public override string Name => "PhotoPromptCreator";

    /// <inheritdoc />
    public override string Description => "Creates detailed image generation prompts from verified scenes";

    /// <summary>
    /// Initializes a new instance of the PhotoPromptCreatorAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="prompts">The prompt templates configuration.</param>
    public PhotoPromptCreatorAgent(IAIProvider aiProvider, ILogger<PhotoPromptCreatorAgent> logger, AgentPrompts? prompts = null)
        : base(aiProvider, logger)
    {
        _prompts = prompts ?? new AgentPrompts();
    }

    /// <inheritdoc />
    public override async Task<AgentResult<List<PhotoPrompt>>> ProcessAsync(PhotoPromptCreatorInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting photo prompt creation for {SceneCount} scenes", input.Scenes.Count);
            
            if (!string.IsNullOrEmpty(input.Feedback))
            {
                _logger.LogInformation("Using feedback from previous attempt: {Feedback}", input.Feedback);
            }

            // Build the prompt (includes feedback if this is a retry)
            var prompt = BuildPrompt(input);
            _logger.LogInformation("Generated prompt ({Length} chars)", prompt.Length);
            _logger.LogDebug("Prompt preview: {Preview}", prompt.Length > 500 ? prompt.Substring(0, 500) + "..." : prompt);

            // Invoke AI
            var response = await InvokeAIAsync(prompt, cancellationToken);
            _logger.LogInformation("Received AI response ({Length} chars)", response.Length);

            // Parse the response
            var photoPrompts = ParseResponse(response);
            _logger.LogInformation("Parsed {PromptCount} photo prompts", photoPrompts.Count);

            stopwatch.Stop();
            return CreateSuccessResult(photoPrompts, stopwatch.Elapsed);
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to parse AI response as JSON");
            return CreateFailureResult<List<PhotoPrompt>>($"JSON parsing failed: {ex.Message}", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Photo prompt creation failed");
            return CreateFailureResult<List<PhotoPrompt>>($"Prompt creation failed: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Creates photo prompts from scenes.
    /// </summary>
    /// <param name="input">The input containing scenes and optional feedback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of photo prompts.</returns>
    protected override async Task<List<PhotoPrompt>> CreateAsync(PhotoPromptCreatorInput input, CancellationToken cancellationToken = default)
    {
        // Build the prompt (includes feedback if this is a retry)
        var prompt = BuildPrompt(input);

        // Invoke AI
        var response = await InvokeAIAsync(prompt, cancellationToken);

        // Parse the response
        return ParseResponse(response);
    }

    /// <summary>
    /// Builds the prompt for photo prompt creation.
    /// </summary>
    /// <param name="input">The input containing scenes and optional feedback.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected override string BuildPrompt(PhotoPromptCreatorInput input)
    {
        var scenesJson = JsonSerializer.Serialize(input.Scenes, new JsonSerializerOptions { WriteIndented = true });
        var prompt = _prompts.PhotoPromptCreatorPrompt.Replace("{0}", scenesJson, StringComparison.OrdinalIgnoreCase);

        // If we have feedback from a previous verification attempt, append it
        if (!string.IsNullOrEmpty(input.Feedback))
        {
            prompt += $"\n\nIMPORTANT FEEDBACK FROM PREVIOUS REVIEW:\n{input.Feedback}\n\nPlease revise your photo prompts to address this feedback. Ensure all scenes have detailed prompts with subject, style, lighting, composition, mood, and camera details.";
        }

        return prompt;
    }

    /// <summary>
    /// Parses the AI response into a list of photo prompts.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The parsed list of photo prompts.</returns>
    protected override List<PhotoPrompt> ParseResponse(string response)
    {
        // Extract JSON from markdown code blocks if present
        var json = ExtractJsonFromMarkdown(response);
        
        // Fix common JSON issues from AI models
        // Replace Python-style None with JSON null
        json = json.Replace(": None,", ": null,", StringComparison.OrdinalIgnoreCase)
                   .Replace(": None}", ": null}", StringComparison.OrdinalIgnoreCase)
                   .Replace(": None\r\n", ": null\r\n", StringComparison.OrdinalIgnoreCase)
                   .Replace(": None\n", ": null\n", StringComparison.OrdinalIgnoreCase);

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var photoPrompts = JsonSerializer.Deserialize<List<PhotoPrompt>>(json, options)
                ?? throw new JsonException("Failed to deserialize photo prompts from JSON - null result");

            // Validate and assign default IDs if missing
            for (int i = 0; i < photoPrompts.Count; i++)
            {
                var prompt = photoPrompts[i];
                if (string.IsNullOrEmpty(prompt.Id))
                {
                    prompt.Id = $"PROMPT-{(i + 1):D3}";
                }
                if (string.IsNullOrEmpty(prompt.SceneId))
                {
                    prompt.SceneId = $"SCENE-{(i + 1):D3}";
                }
            }

            _logger.LogDebug("Successfully parsed {Count} photo prompts", photoPrompts.Count);
            return photoPrompts;
        }
        catch (JsonException ex)
        {
            // Log the problematic JSON for debugging
            var jsonPreview = json.Length > 2000 ? json.Substring(0, 2000) + $"... ({json.Length - 2000} more chars)" : json;
            _logger.LogError(ex, "JSON parsing failed. Problematic JSON:\n{Json}", jsonPreview);

            // Re-throw with JSON context for error logging
            throw new JsonException($"JSON parsing failed. Problematic JSON preview: {jsonPreview}", ex);
        }
    }

    /// <summary>
    /// Extracts JSON from markdown code blocks or plain text.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The extracted JSON string.</returns>
    private static string ExtractJsonFromMarkdown(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            throw new JsonException("Empty response from AI");
        }

        // Try to find JSON in markdown code blocks
        var startIndex = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
        {
            startIndex = response.IndexOf("```", StringComparison.OrdinalIgnoreCase);
        }

        if (startIndex >= 0)
        {
            var bracketStart = response.IndexOf('[', startIndex);
            if (bracketStart >= 0)
            {
                var bracketEnd = response.LastIndexOf(']');
                if (bracketEnd > bracketStart)
                {
                    return response.Substring(bracketStart, bracketEnd - bracketStart + 1);
                }
            }
        }

        // If no markdown blocks, look for JSON array anywhere
        var arrayStart = response.IndexOf('[');
        if (arrayStart >= 0)
        {
            var arrayEnd = response.LastIndexOf(']');
            if (arrayEnd > arrayStart)
            {
                return response.Substring(arrayStart, arrayEnd - arrayStart + 1);
            }
        }

        return response.Trim();
    }
}
