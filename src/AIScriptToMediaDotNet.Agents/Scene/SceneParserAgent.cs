using System.Text.Json;
using AIScriptToMediaDotNet.Agents.Base;
using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Interfaces;
using Microsoft.Extensions.Logging;

using SceneModel = AIScriptToMediaDotNet.Core.Context.Scene;

namespace AIScriptToMediaDotNet.Agents.Scene;

/// <summary>
/// Agent that parses script text into discrete scenes.
/// </summary>
public class SceneParserAgent : CreatorAgent<string, List<SceneModel>>
{
    /// <inheritdoc />
    public override string Name => "SceneParser";

    /// <inheritdoc />
    public override string Description => "Parses script text into discrete scenes with structured data";

    /// <summary>
    /// Initializes a new instance of the SceneParserAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    public SceneParserAgent(IAIProvider aiProvider, ILogger<SceneParserAgent> logger)
        : base(aiProvider, logger)
    {
    }

    /// <inheritdoc />
    public override async Task<AgentResult<List<SceneModel>>> ProcessAsync(string script, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting scene parsing for script ({Length} chars)", script.Length);

            // Invoke AI and parse response
            var scenes = await CreateAsync(script, cancellationToken);
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
        _logger.LogDebug("Generated prompt ({Length} chars)", prompt.Length);

        // Invoke AI
        var response = await InvokeAIAsync(prompt, cancellationToken);
        _logger.LogDebug("Received AI response ({Length} chars)", response.Length);

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
        return string.Format(@"You are a professional script analyzer. Your task is to parse a screenplay/script into discrete scenes.

Analyze the following script and break it down into individual scenes. For each scene, extract:
- id: A unique identifier (e.g., ""SCENE-001"", ""SCENE-002"")
- title: The scene heading/slug line (e.g., ""INT. COFFEE SHOP - DAY"")
- description: A brief description of the action/events in the scene
- location: The location name (e.g., ""Coffee Shop"", ""John's Apartment"")
- time: Time of day (DAY, NIGHT, DAWN, DUSK, etc.)
- characters: List of character names that appear in the scene
- notes: Any important notes about mood, tone, or special requirements

Return your response as a valid JSON array of scene objects. Do not include any text outside the JSON.

SCRIPT TO ANALYZE:
{0}

Respond with ONLY a JSON array in this exact format:
[
  {{
    ""id"": ""SCENE-001"",
    ""title"": ""INT. LOCATION - DAY"",
    ""description"": ""Brief description of what happens"",
    ""location"": ""Location Name"",
    ""time"": ""DAY"",
    ""characters"": [""Character1"", ""Character2""],
    ""notes"": ""Optional notes""
  }}
]", script);
    }

    /// <summary>
    /// Parses the AI response into a list of scenes.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The parsed list of scenes.</returns>
    protected override List<SceneModel> ParseResponse(string response)
    {
        // Try to extract JSON array from response
        var json = ExtractJsonArray(response);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var scenes = JsonSerializer.Deserialize<List<SceneModel>>(json, options)
            ?? throw new JsonException("Failed to deserialize scenes from JSON");

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
