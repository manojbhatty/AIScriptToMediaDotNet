namespace AIScriptToMediaDotNet.App;

/// <summary>
/// Configuration options for the pipeline.
/// </summary>
public class PipelineOptions
{
    /// <summary>
    /// Gets or sets the default maximum retries per stage.
    /// </summary>
    public int MaxRetriesPerStage { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum retries per agent (overrides default).
    /// </summary>
    public Dictionary<string, int> MaxRetriesPerAgent { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to use the best attempt on verification failure (instead of failing the pipeline).
    /// </summary>
    public bool UseBestAttemptOnFailure { get; set; } = false;

    /// <summary>
    /// Gets or sets per-agent settings for using best attempt on failure.
    /// </summary>
    public Dictionary<string, bool> UseBestAttemptOnFailurePerAgent { get; set; } = new();

    /// <summary>
    /// Gets the maximum retries for a specific agent.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <returns>The maximum retries for the agent, or the default if not specified.</returns>
    public int GetMaxRetriesForAgent(string agentName)
    {
        if (string.IsNullOrEmpty(agentName))
            return MaxRetriesPerStage;

        // Try exact match first
        if (MaxRetriesPerAgent.TryGetValue(agentName, out var retries))
            return retries;

        // Try case-insensitive match
        var agentNameLower = agentName.ToLowerInvariant();
        foreach (var kvp in MaxRetriesPerAgent)
        {
            if (kvp.Key.ToLowerInvariant() == agentNameLower)
                return kvp.Value;
        }

        // Return default if not found
        return MaxRetriesPerStage;
    }

    /// <summary>
    /// Gets whether to use best attempt on failure for a specific agent.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <returns>True if best attempt should be used, false otherwise.</returns>
    public bool ShouldUseBestAttemptOnFailure(string agentName)
    {
        // Check per-agent setting first
        if (!string.IsNullOrEmpty(agentName))
        {
            if (UseBestAttemptOnFailurePerAgent.TryGetValue(agentName, out var useBest))
                return useBest;

            // Try case-insensitive match
            var agentNameLower = agentName.ToLowerInvariant();
            foreach (var kvp in UseBestAttemptOnFailurePerAgent)
            {
                if (kvp.Key.ToLowerInvariant() == agentNameLower)
                    return kvp.Value;
            }
        }

        // Fall back to global setting
        return UseBestAttemptOnFailure;
    }
}
