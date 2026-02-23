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
/// Input for scene verification containing both script and scenes.
/// </summary>
public class SceneVerificationInput
{
    public string OriginalScript { get; set; } = string.Empty;
    public List<SceneModel> Scenes { get; set; } = new();
}

/// <summary>
/// Agent that validates parsed scenes against the original script.
/// </summary>
public class SceneVerifierAgent : VerifierAgent<SceneVerificationInput>
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
    public override async Task<AgentResult<ValidationResult>> ProcessAsync(SceneVerificationInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting scene verification for {SceneCount} scenes", input.Scenes.Count);

            // Invoke AI and parse response
            var validationResult = await ValidateAsync(input, cancellationToken);
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
    /// <param name="input">The verification input containing script and scenes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    protected override async Task<ValidationResult> ValidateAsync(SceneVerificationInput input, CancellationToken cancellationToken = default)
    {
        // Build the prompt with both script and scenes
        var prompt = BuildPrompt(input);

        // Invoke AI
        var response = await InvokeAIAsync(prompt, cancellationToken);

        // Parse the response
        return ParseResponse(response);
    }

    /// <summary>
    /// Builds the prompt for scene verification.
    /// </summary>
    /// <param name="input">The verification input containing script and scenes.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected override string BuildPrompt(SceneVerificationInput input)
    {
        var scenesJson = JsonSerializer.Serialize(input.Scenes, new JsonSerializerOptions { WriteIndented = true });
        
        // Replace {{0}} with script and {{1}} with scenes JSON
        var prompt = _prompts.SceneVerifierPrompt.Replace("{{0}}", input.OriginalScript, StringComparison.OrdinalIgnoreCase);
        prompt = prompt.Replace("{{1}}", scenesJson, StringComparison.OrdinalIgnoreCase);
        
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

            // If there are significant warnings about missing story elements, mark as invalid
            // This ensures the SceneParser gets another chance to create more complete scenes
            if (result.Warnings.Any())
            {
                // Check if warnings indicate missing story beats
                var hasMissingContentWarning = result.Warnings.Any(w => 
                    w.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("not fully captured", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("incomplete", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("should be created", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("split", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("separate scenes", StringComparison.OrdinalIgnoreCase));
                
                if (hasMissingContentWarning)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(result.Warnings.Select(w => $"Missing content: {w}"));
                    _logger.LogWarning("Scene verification downgraded to INVALID due to missing content warnings");
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
