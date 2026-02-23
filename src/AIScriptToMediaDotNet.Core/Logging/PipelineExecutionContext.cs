namespace AIScriptToMediaDotNet.Core.Logging;

/// <summary>
/// Represents a single log entry for agent execution.
/// </summary>
public class AgentLogEntry
{
    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public string Level { get; set; } = "INFO";

    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event type (Start, Complete, Retry, Error, etc.).
    /// </summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input summary (truncated for large inputs).
    /// </summary>
    public string? InputSummary { get; set; }

    /// <summary>
    /// Gets or sets the input data as JSON for detailed debugging.
    /// </summary>
    public string? InputData { get; set; }

    /// <summary>
    /// Gets or sets the output summary (truncated for large outputs).
    /// </summary>
    public string? OutputSummary { get; set; }

    /// <summary>
    /// Gets or sets the output data as JSON for detailed debugging.
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Gets or sets the error details including stack trace.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Gets or sets the retry count.
    /// </summary>
    public int? RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, string?> Metadata { get; set; } = new();
}

/// <summary>
/// Captures detailed information about a pipeline run for debugging.
/// </summary>
public class PipelineExecutionContext
{
    /// <summary>
    /// Gets or sets the unique execution ID.
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the overall status (Success, Failed, Cancelled).
    /// </summary>
    public string Status { get; set; } = "Running";

    /// <summary>
    /// Gets or sets the script title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input script (truncated for logging).
    /// </summary>
    public string ScriptSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full input script for error reproduction.
    /// </summary>
    public string FullScript { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configuration snapshot (models, endpoints, etc.).
    /// </summary>
    public Dictionary<string, string> ConfigurationSnapshot { get; set; } = new();

    /// <summary>
    /// Gets the log entries for this execution.
    /// </summary>
    public List<AgentLogEntry> LogEntries { get; set; } = new();

    /// <summary>
    /// Gets or sets the final error if the pipeline failed.
    /// </summary>
    public string? FinalError { get; set; }

    /// <summary>
    /// Gets or sets the stack trace if the pipeline failed.
    /// </summary>
    public string? FinalStackTrace { get; set; }

    /// <summary>
    /// Adds a log entry.
    /// </summary>
    /// <param name="entry">The log entry to add.</param>
    public void AddLogEntry(AgentLogEntry entry)
    {
        LogEntries.Add(entry);
    }

    /// <summary>
    /// Creates a log entry for an agent starting.
    /// </summary>
    /// <param name="agent">The agent name.</param>
    /// <param name="stage">The stage name.</param>
    /// <param name="inputSummary">Summary of the input.</param>
    /// <param name="inputData">The actual input data serialized to JSON.</param>
    /// <returns>The created log entry.</returns>
    public AgentLogEntry LogAgentStart(string agent, string stage, string inputSummary, string? inputData = null)
    {
        var entry = new AgentLogEntry
        {
            Level = "INFO",
            Agent = agent,
            Stage = stage,
            Event = "Start",
            Message = $"Agent {agent} started stage {stage}",
            InputSummary = Truncate(inputSummary, 500),
            InputData = inputData
        };
        AddLogEntry(entry);
        return entry;
    }

    /// <summary>
    /// Creates a log entry for an agent completing successfully.
    /// </summary>
    /// <param name="agent">The agent name.</param>
    /// <param name="stage">The stage name.</param>
    /// <param name="outputSummary">Summary of the output.</param>
    /// <param name="outputData">The actual output data serialized to JSON.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <returns>The created log entry.</returns>
    public AgentLogEntry LogAgentComplete(string agent, string stage, string outputSummary, string? outputData, long executionTimeMs)
    {
        var entry = new AgentLogEntry
        {
            Level = "INFO",
            Agent = agent,
            Stage = stage,
            Event = "Complete",
            Message = $"Agent {agent} completed stage {stage} successfully",
            OutputSummary = Truncate(outputSummary, 500),
            OutputData = outputData,
            ExecutionTimeMs = executionTimeMs
        };
        AddLogEntry(entry);
        return entry;
    }

    /// <summary>
    /// Creates a log entry for an agent retry.
    /// </summary>
    /// <param name="agent">The agent name.</param>
    /// <param name="stage">The stage name.</param>
    /// <param name="retryCount">The retry attempt number.</param>
    /// <param name="reason">The reason for retry.</param>
    /// <param name="feedback">Feedback from verifier if applicable.</param>
    /// <returns>The created log entry.</returns>
    public AgentLogEntry LogAgentRetry(string agent, string stage, int retryCount, string reason, string? feedback = null)
    {
        var entry = new AgentLogEntry
        {
            Level = "WARN",
            Agent = agent,
            Stage = stage,
            Event = "Retry",
            Message = $"Agent {agent} retrying stage {stage} (attempt {retryCount})",
            RetryCount = retryCount,
            ErrorDetails = reason,
            Metadata = { { "Feedback", feedback } }
        };
        AddLogEntry(entry);
        return entry;
    }

    /// <summary>
    /// Creates a log entry for an agent error.
    /// </summary>
    /// <param name="agent">The agent name.</param>
    /// <param name="stage">The stage name.</param>
    /// <param name="error">The error message.</param>
    /// <param name="stackTrace">The stack trace.</param>
    /// <param name="inputSummary">Summary of the input that caused the error.</param>
    /// <param name="inputData">Full JSON data of the input.</param>
    /// <param name="outputData">Full JSON data of the output (if available).</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <returns>The created log entry.</returns>
    public AgentLogEntry LogAgentError(string agent, string stage, string error, string? stackTrace, string? inputSummary = null, string? inputData = null, string? outputData = null, long executionTimeMs = 0)
    {
        var entry = new AgentLogEntry
        {
            Level = "ERROR",
            Agent = agent,
            Stage = stage,
            Event = "Error",
            Message = $"Agent {agent} failed stage {stage}",
            ErrorDetails = error,
            InputSummary = inputSummary != null ? Truncate(inputSummary, 500) : null,
            InputData = inputData,
            OutputData = outputData,
            ExecutionTimeMs = executionTimeMs > 0 ? executionTimeMs : null,
            Metadata = { { "StackTrace", stackTrace } }
        };
        AddLogEntry(entry);
        return entry;
    }

    /// <summary>
    /// Marks the pipeline as complete.
    /// </summary>
    /// <param name="status">The final status.</param>
    public void Complete(string status)
    {
        EndTime = DateTime.UtcNow;
        Status = status;
    }

    /// <summary>
    /// Marks the pipeline as failed.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="stackTrace">The stack trace.</param>
    public void Fail(string error, string? stackTrace = null)
    {
        EndTime = DateTime.UtcNow;
        Status = "Failed";
        FinalError = error;
        FinalStackTrace = stackTrace;
    }

    /// <summary>
    /// Truncates a string to the specified length.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns>The truncated text.</returns>
    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + $"... ({text.Length - maxLength} more chars)";
    }

    /// <summary>
    /// Serializes an object to JSON for logging.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="maxLength">Maximum length of the serialized string.</param>
    /// <returns>JSON string or null if object is null.</returns>
    public static string? SerializeToJson(object? obj, int maxLength = 5000)
    {
        if (obj == null) return null;
        
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                MaxDepth = 10
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(obj, options);
            
            if (json.Length > maxLength)
            {
                return json.Substring(0, maxLength) + $"\n... ({json.Length - maxLength} more chars)";
            }
            
            return json;
        }
        catch
        {
            return obj?.ToString();
        }
    }
}
