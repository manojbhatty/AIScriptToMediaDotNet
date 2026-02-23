namespace AIScriptToMediaDotNet.Core.Prompts;

/// <summary>
/// Configuration for agent prompt templates.
/// These templates are loaded from appsettings.json and can be customized without code changes.
/// </summary>
public class AgentPrompts
{
    /// <summary>
    /// Gets or sets the prompt template for scene parsing.
    /// Use {0} as placeholder for the script text.
    /// </summary>
    public string SceneParserPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt template for scene verification.
    /// Use {0} as placeholder for the scenes JSON.
    /// </summary>
    public string SceneVerifierPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt template for photo prompt creation.
    /// Use {0} as placeholder for the scenes JSON.
    /// </summary>
    public string PhotoPromptCreatorPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt template for photo prompt verification.
    /// Use {0} as placeholder for scenes JSON and {1} for photo prompts JSON.
    /// </summary>
    public string PhotoPromptVerifierPrompt { get; set; } = string.Empty;
}
