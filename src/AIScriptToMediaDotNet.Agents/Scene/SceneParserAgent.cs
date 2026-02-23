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
/// Agent that parses script text into discrete scenes.
/// </summary>
public class SceneParserAgent : CreatorAgent<string, List<SceneModel>>
{
    private readonly AgentPrompts _prompts;

    /// <inheritdoc />
    public override string Name => "SceneParser";

    /// <inheritdoc />
    public override string Description => "Parses script text into discrete scenes with structured data";

    /// <summary>
    /// Initializes a new instance of the SceneParserAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="prompts">The prompt templates configuration.</param>
    public SceneParserAgent(IAIProvider aiProvider, ILogger<SceneParserAgent> logger, AgentPrompts? prompts = null)
        : base(aiProvider, logger)
    {
        _prompts = prompts ?? new AgentPrompts();
    }

    /// <inheritdoc />
    public override async Task<AgentResult<List<SceneModel>>> ProcessAsync(string script, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting scene parsing for script ({Length} chars)", script.Length);

            // Build the prompt
            var prompt = BuildPrompt(script);
            _logger.LogInformation("Generated prompt ({Length} chars)", prompt.Length);
            _logger.LogDebug("Prompt preview: {Preview}", prompt.Length > 500 ? prompt.Substring(0, 500) + "..." : prompt);

            // Invoke AI
            var response = await InvokeAIAsync(prompt, cancellationToken);
            _logger.LogInformation("Received AI response ({Length} chars)", response.Length);

            // Parse the response
            var scenes = ParseResponse(response);
            _logger.LogInformation("Parsed {SceneCount} scenes", scenes.Count);

            stopwatch.Stop();
            return CreateSuccessResult(scenes, stopwatch.Elapsed);
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to parse AI response as JSON");
            return CreateFailureResult<List<SceneModel>>($"JSON parsing failed: {ex.Message}", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Scene parsing failed");
            return CreateFailureResult<List<SceneModel>>($"Parsing failed: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Creates the list of scenes from the script.
    /// </summary>
    /// <param name="script">The script text to parse.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed list of scenes.</returns>
    protected override async Task<List<SceneModel>> CreateAsync(string script, CancellationToken cancellationToken = default)
    {
        // Build the prompt
        var prompt = BuildPrompt(script);

        // Invoke AI
        var response = await InvokeAIAsync(prompt, cancellationToken);

        // Parse the response
        return ParseResponse(response);
    }

    /// <summary>
    /// Builds the prompt for scene parsing.
    /// </summary>
    /// <param name="script">The script text to parse.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected override string BuildPrompt(string script)
    {
        // Replace {0} placeholder with the script text
        return _prompts.SceneParserPrompt.Replace("{0}", script, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the AI response into a list of scenes.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The parsed list of scenes.</returns>
    protected override List<SceneModel> ParseResponse(string response)
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

            var scenes = JsonSerializer.Deserialize<List<SceneModel>>(json, options)
                ?? throw new JsonException("Failed to deserialize scenes from JSON - null result");

            // Validate and assign default IDs if missing
            for (int i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                if (string.IsNullOrEmpty(scene.Id))
                {
                    scene.Id = $"SCENE-{(i + 1):D3}";
                }
                if (string.IsNullOrEmpty(scene.Time))
                {
                    scene.Time = "DAY"; // Default
                }
            }

            _logger.LogDebug("Successfully parsed {Count} scenes", scenes.Count);
            return scenes;
        }
        catch (JsonException ex)
        {
            // Log the problematic JSON for debugging
            _logger.LogError(ex, "JSON parsing failed. Problematic JSON:\n{Json}", 
                json.Length > 2000 ? json.Substring(0, 2000) + $"... ({json.Length - 2000} more chars)" : json);
            throw;
        }
    }

    /// <summary>
    /// Extracts JSON array from markdown code blocks or thinking model output.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The extracted JSON string.</returns>
    private static string ExtractJsonFromMarkdown(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            throw new JsonException("Empty response from AI");
        }

        // First, try to find JSON in markdown code blocks (```json or ```)
        var startIndex = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
        {
            startIndex = response.IndexOf("```", StringComparison.OrdinalIgnoreCase);
        }
        
        if (startIndex >= 0)
        {
            // Find the opening bracket after the code block marker
            var bracketStart = response.IndexOf('[', startIndex);
            if (bracketStart >= 0)
            {
                // Find the matching closing bracket
                var bracketEnd = response.LastIndexOf(']');
                if (bracketEnd > bracketStart)
                {
                    return response.Substring(bracketStart, bracketEnd - bracketStart + 1);
                }
            }
        }
        
        // If no markdown blocks, look for JSON array anywhere in the response
        // This handles thinking models that output reasoning before JSON
        var arrayStart = response.IndexOf('[');
        if (arrayStart >= 0)
        {
            var arrayEnd = response.LastIndexOf(']');
            if (arrayEnd > arrayStart)
            {
                return response.Substring(arrayStart, arrayEnd - arrayStart + 1);
            }
        }
        
        // If still no JSON found, return the whole response and let deserialization fail with clear error
        return response.Trim();
    }

    /// <summary>
    /// Extracts a JSON array from the AI response text.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The extracted JSON array string.</returns>
    private static string ExtractJsonArray(string response)
    {
        // Find the first '[' and last ']'
        var startIndex = response.IndexOf('[');
        var endIndex = response.LastIndexOf(']');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return response.Substring(startIndex, endIndex - startIndex + 1);
        }

        // If no array found, return the whole response and let deserialization fail
        return response.Trim();
    }
}
