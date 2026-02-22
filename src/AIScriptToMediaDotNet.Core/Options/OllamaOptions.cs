namespace AIScriptToMediaDotNet.Core.Options;

/// <summary>
/// Configuration options for Ollama provider.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// The Ollama API endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default model to use when not specified.
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Number of retries for failed requests.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Model options for specific agents/tasks.
    /// </summary>
    public AgentModelsOptions AgentModels { get; set; } = new();
}

/// <summary>
/// Model configuration per agent type.
/// </summary>
public class AgentModelsOptions
{
    /// <summary>
    /// Default model to use when agent-specific model is not configured.
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// Model for scene parsing agent.
    /// </summary>
    public string SceneParser { get; set; } = string.Empty;

    /// <summary>
    /// Model for scene verifier agent.
    /// </summary>
    public string SceneVerifier { get; set; } = string.Empty;

    /// <summary>
    /// Model for photo prompt creator agent.
    /// </summary>
    public string PhotoPromptCreator { get; set; } = string.Empty;

    /// <summary>
    /// Model for photo prompt verifier agent.
    /// </summary>
    public string PhotoPromptVerifier { get; set; } = string.Empty;

    /// <summary>
    /// Model for video prompt creator agent.
    /// </summary>
    public string VideoPromptCreator { get; set; } = string.Empty;

    /// <summary>
    /// Model for video prompt verifier agent.
    /// </summary>
    public string VideoPromptVerifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets the model name for a specific agent.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <returns>The configured model name.</returns>
    public string GetModelForAgent(string agentName)
    {
        return agentName.ToLower() switch
        {
            "sceneparser" => SceneParser,
            "sceneverifier" => SceneVerifier,
            "photopromptcreator" => PhotoPromptCreator,
            "photopromptverifier" => PhotoPromptVerifier,
            "videopromptcreator" => VideoPromptCreator,
            "videopromptverifier" => VideoPromptVerifier,
            _ => DefaultModel
        };
    }
}
