using System.Text.Json;
using AIScriptToMediaDotNet.Agents.Base;
using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Prompts;
using Microsoft.Extensions.Logging;

using Scene = AIScriptToMediaDotNet.Core.Context.Scene;

namespace AIScriptToMediaDotNet.Agents.Video;

/// <summary>
/// Input for video prompt verification containing both scenes and prompts.
/// </summary>
public class VideoPromptVerificationInput
{
    public List<AIScriptToMediaDotNet.Core.Context.Scene> Scenes { get; set; } = new();
    public List<VideoPrompt> VideoPrompts { get; set; } = new();
}

/// <summary>
/// Agent that validates video prompts for completeness and quality.
/// </summary>
public class VideoPromptVerifierAgent : VerifierAgent<VideoPromptVerificationInput>
{
    private readonly AgentPrompts _prompts;

    /// <inheritdoc />
    public override string Name => "VideoPromptVerifier";

    /// <inheritdoc />
    public override string Description => "Validates video prompts for completeness, quality, and technical feasibility";

    /// <summary>
    /// Initializes a new instance of the VideoPromptVerifierAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="prompts">The prompt templates configuration.</param>
    public VideoPromptVerifierAgent(IAIProvider aiProvider, ILogger<VideoPromptVerifierAgent> logger, AgentPrompts? prompts = null)
        : base(aiProvider, logger)
    {
        _prompts = prompts ?? new AgentPrompts();
    }

    /// <inheritdoc />
    public override async Task<AgentResult<ValidationResult>> ProcessAsync(VideoPromptVerificationInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting video prompt verification for {PromptCount} prompts", input.VideoPrompts.Count);

            // Invoke AI and parse response
            var validationResult = await ValidateAsync(input, cancellationToken);
            _logger.LogInformation("Verification complete: {IsValid}", validationResult.IsValid);

            stopwatch.Stop();
            
            // Always return the validation result, even if invalid, so best-attempt logic can use it
            if (validationResult.IsValid)
            {
                return CreateSuccessResult(validationResult, stopwatch.Elapsed);
            }
            else
            {
                return new AgentResult<ValidationResult>
                {
                    Success = false,
                    Data = validationResult,
                    Errors = validationResult.Errors.ToList(),
                    ExecutionTime = stopwatch.Elapsed
                };
            }
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to parse AI response as JSON");
            return CreateFailureResult<ValidationResult>($"JSON parsing failed: {ex.Message}", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Video prompt verification failed");
            return CreateFailureResult<ValidationResult>($"Verification failed: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Validates the video prompts.
    /// </summary>
    /// <param name="input">The verification input containing scenes and prompts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    protected override async Task<ValidationResult> ValidateAsync(VideoPromptVerificationInput input, CancellationToken cancellationToken = default)
    {
        // Build the prompt with both scenes and prompts
        var prompt = BuildPrompt(input);

        // Invoke AI
        var response = await InvokeAIAsync(prompt, cancellationToken);

        // Parse the response
        return ParseResponse(response);
    }

    /// <summary>
    /// Builds the prompt for video prompt verification.
    /// </summary>
    /// <param name="input">The verification input containing scenes and prompts.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected override string BuildPrompt(VideoPromptVerificationInput input)
    {
        var scenesJson = JsonSerializer.Serialize(input.Scenes, new JsonSerializerOptions { WriteIndented = true });
        var promptsJson = JsonSerializer.Serialize(input.VideoPrompts, new JsonSerializerOptions { WriteIndented = true });

        // Replace {0} with scenes and {1} with video prompts
        var prompt = _prompts.VideoPromptVerifierPrompt.Replace("{0}", scenesJson, StringComparison.OrdinalIgnoreCase);
        prompt = prompt.Replace("{1}", promptsJson, StringComparison.OrdinalIgnoreCase);

        return prompt;
    }

    /// <summary>
    /// Parses the AI response into a validation result.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The validation result.</returns>
    protected override ValidationResult ParseResponse(string response)
    {
        // Extract JSON from markdown code blocks if present
        var json = ExtractJsonFromMarkdown(response);

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var validationResult = JsonSerializer.Deserialize<ValidationResultDto>(json, options)
                ?? throw new JsonException("Failed to deserialize validation result from JSON - null result");

            // Convert DTO to domain model
            var result = new ValidationResult
            {
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors ?? new List<string>(),
                Warnings = validationResult.Warnings ?? new List<string>(),
                Feedback = validationResult.Feedback
            };

            // If there are significant warnings about missing content, mark as invalid
            if (result.Warnings.Any())
            {
                // Check if warnings indicate missing content
                var hasMissingContentWarning = result.Warnings.Any(w => 
                    w.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("incomplete", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("not fully captured", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("should include", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("add", StringComparison.OrdinalIgnoreCase));
                
                if (hasMissingContentWarning)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(result.Warnings.Select(w => $"Missing content: {w}"));
                    _logger.LogWarning("Video prompt verification downgraded to INVALID due to missing content warnings");
                }
            }

            return result;
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
            var bracketStart = response.IndexOf('{', startIndex);
            if (bracketStart >= 0)
            {
                var bracketEnd = response.LastIndexOf('}');
                if (bracketEnd > bracketStart)
                {
                    return response.Substring(bracketStart, bracketEnd - bracketStart + 1);
                }
            }
        }

        // If no markdown blocks, look for JSON object anywhere
        var objectStart = response.IndexOf('{');
        if (objectStart >= 0)
        {
            var objectEnd = response.LastIndexOf('}');
            if (objectEnd > objectStart)
            {
                return response.Substring(objectStart, objectEnd - objectStart + 1);
            }
        }

        return response.Trim();
    }

    /// <summary>
    /// DTO for deserializing validation result from AI.
    /// </summary>
    private class ValidationResultDto
    {
        public bool IsValid { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? Warnings { get; set; }
        public string? Feedback { get; set; }
    }
}
