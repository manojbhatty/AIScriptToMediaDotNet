using System.Text.Json;
using AIScriptToMediaDotNet.Agents.Base;
using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Interfaces;
using AIScriptToMediaDotNet.Core.Prompts;
using Microsoft.Extensions.Logging;

using SceneModel = AIScriptToMediaDotNet.Core.Context.Scene;

namespace AIScriptToMediaDotNet.Agents.Scene;

/// <summary>
/// Agent that validates parsed scenes against the original script.
/// </summary>
public class SceneVerifierAgent : VerifierAgent<List<SceneModel>>
{
    private readonly AgentPrompts _prompts;

    /// <inheritdoc />
    public override string Name => "SceneVerifier";

    /// <inheritdoc />
    public override string Description => "Validates parsed scenes against the original script for completeness and accuracy";

    /// <summary>
    /// Initializes a new instance of the SceneVerifierAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="prompts">The prompt templates configuration.</param>
    public SceneVerifierAgent(IAIProvider aiProvider, ILogger<SceneVerifierAgent> logger, AgentPrompts? prompts = null)
        : base(aiProvider, logger)
    {
        _prompts = prompts ?? new AgentPrompts();
    }

    /// <inheritdoc />
    public override async Task<AgentResult<ValidationResult>> ProcessAsync(List<SceneModel> scenes, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting scene verification for {SceneCount} scenes", scenes.Count);

            // Invoke AI and parse response
            var validationResult = await ValidateAsync(scenes, cancellationToken);
            _logger.LogInformation("Verification complete: {IsValid}", validationResult.IsValid);

            stopwatch.Stop();
            return validationResult.IsValid
                ? CreateSuccessResult(validationResult, stopwatch.Elapsed)
                : CreateFailureResult<ValidationResult>(string.Join("; ", validationResult.Errors), stopwatch.Elapsed);
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
            _logger.LogError(ex, "Scene verification failed");
            return CreateFailureResult<ValidationResult>($"Verification failed: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Validates the scenes against the original script.
    /// </summary>
    /// <param name="scenes">The parsed scenes to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    protected override async Task<ValidationResult> ValidateAsync(List<SceneModel> scenes, CancellationToken cancellationToken = default)
    {
        // Build the prompt
        var prompt = BuildPrompt(scenes);

        // Invoke AI
        var response = await InvokeAIAsync(prompt, cancellationToken);

        // Parse the response
        return ParseResponse(response);
    }

    /// <summary>
    /// Builds the prompt for scene verification.
    /// </summary>
    /// <param name="scenes">The parsed scenes to validate.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected override string BuildPrompt(List<SceneModel> scenes)
    {
        var scenesJson = JsonSerializer.Serialize(scenes, new JsonSerializerOptions { WriteIndented = true });
        
        return string.Format(_prompts.SceneVerifierPrompt, scenesJson);
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

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var validationResult = JsonSerializer.Deserialize<ValidationResultDto>(json, options)
            ?? throw new JsonException("Failed to deserialize validation result from JSON");

        // Convert DTO to domain model
        return new ValidationResult
        {
            IsValid = validationResult.IsValid,
            Errors = validationResult.Errors ?? new List<string>(),
            Warnings = validationResult.Warnings ?? new List<string>(),
            Feedback = validationResult.Feedback
        };
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
