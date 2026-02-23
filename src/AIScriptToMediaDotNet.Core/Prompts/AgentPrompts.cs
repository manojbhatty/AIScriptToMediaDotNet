namespace AIScriptToMediaDotNet.Core.Prompts;

/// <summary>
/// Configuration for agent prompt templates.
/// </summary>
public class AgentPrompts
{
    /// <summary>
    /// Gets or sets the prompt template for scene parsing.
    /// </summary>
    public string SceneParserPrompt { get; set; } = DefaultSceneParserPrompt;

    /// <summary>
    /// Gets the default prompt template for scene parsing.
    /// Use {0} as placeholder for the script text.
    /// </summary>
    public const string DefaultSceneParserPrompt = @"You are a professional script analyzer. Your task is to parse a screenplay/script into discrete scenes.

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
]";
}
