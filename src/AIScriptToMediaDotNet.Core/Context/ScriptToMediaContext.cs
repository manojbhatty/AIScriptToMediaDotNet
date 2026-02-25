using System.Text.Json.Serialization;

namespace AIScriptToMediaDotNet.Core.Context;

/// <summary>
/// Represents the validation state for a pipeline stage.
/// </summary>
public class StageState
{
    /// <summary>
    /// Gets or sets the stage name (e.g., "SceneParsing", "PhotoPromptCreation").
    /// </summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the stage is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the stage has failed.
    /// </summary>
    public bool HasFailed { get; set; }

    /// <summary>
    /// Gets or sets the current retry count for this stage.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum retries allowed for this stage.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the list of validation errors for this stage.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of warnings for this stage.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets feedback from the verifier for retry attempts.
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the stage started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the stage completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the execution time for this stage.
    /// </summary>
    public TimeSpan? ExecutionTime { get; set; }

    /// <summary>
    /// Resets the stage state for a retry.
    /// </summary>
    public void ResetForRetry()
    {
        HasFailed = false;
        ValidationErrors.Clear();
        Feedback = null;
    }

    /// <summary>
    /// Marks the stage as failed.
    /// </summary>
    /// <param name="errors">The list of errors.</param>
    /// <param name="feedback">Optional feedback for correction.</param>
    public void MarkFailed(IEnumerable<string> errors, string? feedback = null)
    {
        HasFailed = true;
        IsComplete = false;
        ValidationErrors = errors.ToList();
        Feedback = feedback;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the stage as complete.
    /// </summary>
    /// <param name="executionTime">Optional execution time.</param>
    public void MarkComplete(TimeSpan? executionTime = null)
    {
        IsComplete = true;
        HasFailed = false;
        ValidationErrors.Clear();
        Feedback = null;
        CompletedAt = DateTime.UtcNow;
        ExecutionTime = executionTime;
    }

    /// <summary>
    /// Checks if retries are still available.
    /// </summary>
    /// <returns>True if retries are available.</returns>
    public bool CanRetry() => RetryCount < MaxRetries;

    /// <summary>
    /// Increments the retry count.
    /// </summary>
    public void IncrementRetry()
    {
        RetryCount++;
        StartedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents the shared context for the script-to-media pipeline.
/// This object is passed between all agents and tracks the state of each stage.
/// </summary>
public class ScriptToMediaContext
{
    private readonly Dictionary<string, StageState> _stageStates = new();

    /// <summary>
    /// Gets or sets the unique identifier for this context/run.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Gets or sets the title of the script (used for output folder naming).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original script text.
    /// </summary>
    public string OriginalScript { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of parsed scenes.
    /// </summary>
    public List<Scene> Scenes { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of photo prompts.
    /// </summary>
    public List<PhotoPrompt> PhotoPrompts { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of video prompts.
    /// </summary>
    public List<VideoPrompt> VideoPrompts { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of generated images.
    /// </summary>
    public List<GeneratedImage> GeneratedImages { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of all errors that occurred during pipeline execution.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to generate images from photo prompts.
    /// </summary>
    public bool GenerateImages { get; set; }

    /// <summary>
    /// Gets or sets the output directory path.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the maximum retries allowed per stage.
    /// </summary>
    public int MaxRetriesPerStage { get; set; } = 3;

    /// <summary>
    /// Gets or sets the created timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the stage states for tracking progress.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, StageState> StageStates => _stageStates.AsReadOnly();

    #region Stage State Management

    /// <summary>
    /// Gets or creates the state for a specific stage.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    /// <returns>The stage state.</returns>
    public StageState GetOrCreateStageState(string stageName)
    {
        if (!_stageStates.TryGetValue(stageName, out var state))
        {
            state = new StageState
            {
                StageName = stageName,
                MaxRetries = MaxRetriesPerStage,
                StartedAt = DateTime.UtcNow
            };
            _stageStates[stageName] = state;
        }
        return state;
    }

    /// <summary>
    /// Gets the state for a specific stage.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    /// <returns>The stage state, or null if not found.</returns>
    public StageState? GetStageState(string stageName)
    {
        _stageStates.TryGetValue(stageName, out var state);
        return state;
    }

    /// <summary>
    /// Marks a stage as started.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    public void MarkStageStarted(string stageName)
    {
        var state = GetOrCreateStageState(stageName);
        state.StartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks a stage as complete.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    /// <param name="executionTime">Optional execution time.</param>
    public void MarkStageComplete(string stageName, TimeSpan? executionTime = null)
    {
        var state = GetOrCreateStageState(stageName);
        state.MarkComplete(executionTime);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks a stage as failed.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    /// <param name="errors">The list of errors.</param>
    /// <param name="feedback">Optional feedback for correction.</param>
    public void MarkStageFailed(string stageName, IEnumerable<string> errors, string? feedback = null)
    {
        var state = GetOrCreateStageState(stageName);
        state.MarkFailed(errors, feedback);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments the retry count for a stage.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    public void IncrementStageRetry(string stageName)
    {
        var state = GetOrCreateStageState(stageName);
        state.IncrementRetry();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if a stage can be retried.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    /// <returns>True if retries are available.</returns>
    public bool CanRetryStage(string stageName)
    {
        var state = GetStageState(stageName);
        return state?.CanRetry() ?? true;
    }

    /// <summary>
    /// Gets the feedback for a stage.
    /// </summary>
    /// <param name="stageName">The stage name.</param>
    /// <returns>The feedback, or null if not found.</returns>
    public string? GetStageFeedback(string stageName)
    {
        var state = GetStageState(stageName);
        return state?.Feedback;
    }

    #endregion

    #region Pipeline Status

    /// <summary>
    /// Gets a value indicating whether all pipeline stages are complete.
    /// </summary>
    [JsonIgnore]
    public bool IsComplete => _stageStates.Values.All(s => s.IsComplete);

    /// <summary>
    /// Gets a value indicating whether any pipeline stage has failed.
    /// </summary>
    [JsonIgnore]
    public bool HasFailed => _stageStates.Values.Any(s => s.HasFailed && !s.CanRetry());

    /// <summary>
    /// Gets the current pipeline stage.
    /// </summary>
    [JsonIgnore]
    public string CurrentStage => _stageStates.Values.FirstOrDefault(s => !s.IsComplete && !s.HasFailed)?.StageName
        ?? "Completed";

    /// <summary>
    /// Gets the total retry count across all stages.
    /// </summary>
    [JsonIgnore]
    public int TotalRetryCount => _stageStates.Values.Sum(s => s.RetryCount);

    #endregion

    /// <summary>
    /// Creates a summary of the context for logging.
    /// </summary>
    /// <returns>A summary string.</returns>
    public string GetSummary()
    {
        return $"""
            Context: {Id} - "{Title}"
            Created: {CreatedAt:yyyy-MM-dd HH:mm:ss}
            Updated: {UpdatedAt:yyyy-MM-dd HH:mm:ss}

            Content:
              - Original Script: {OriginalScript.Length} chars
              - Scenes: {Scenes.Count}
              - Photo Prompts: {PhotoPrompts.Count}
              - Video Prompts: {VideoPrompts.Count}
              - Generated Images: {GeneratedImages.Count}

            Pipeline Status: {CurrentStage}
            Total Retries: {TotalRetryCount}

            Stage States:
            {string.Join(Environment.NewLine, _stageStates.Values.Select(s => $"  - {s.StageName}: {(s.IsComplete ? "✓ Complete" : s.HasFailed ? "✗ Failed" : "In Progress")} (Retries: {s.RetryCount})"))}
            """;
    }
}
