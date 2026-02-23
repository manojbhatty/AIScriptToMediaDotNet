namespace AIScriptToMediaDotNet.Core.Agents;

/// <summary>
/// Represents the result of an agent's processing operation.
/// </summary>
public class AgentResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of error messages.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of warning messages.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a successful agent result.
    /// </summary>
    /// <returns>A successful agent result.</returns>
    public static AgentResult Ok() => new() { Success = true };

    /// <summary>
    /// Creates a failed agent result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed agent result.</returns>
    public static AgentResult Fail(string error) => new() { Success = false, Errors = { error } };

    /// <summary>
    /// Creates a failed agent result with multiple errors.
    /// </summary>
    /// <param name="errors">The list of error messages.</param>
    /// <returns>A failed agent result.</returns>
    public static AgentResult Fail(IEnumerable<string> errors) => new() { Success = false, Errors = errors.ToList() };
}

/// <summary>
/// Represents the result of an agent's processing operation with typed data.
/// </summary>
/// <typeparam name="T">The type of the data in the result.</typeparam>
public class AgentResult<T> : AgentResult
{
    /// <summary>
    /// Gets or sets the data produced by the agent.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Creates a successful agent result with data.
    /// </summary>
    /// <param name="data">The data to include in the result.</param>
    /// <returns>A successful agent result with data.</returns>
    public static AgentResult<T> Ok(T data) => new() { Success = true, Data = data };

    /// <summary>
    /// Creates a failed agent result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed agent result.</returns>
    public new static AgentResult<T> Fail(string error) => new() { Success = false, Errors = { error } };

    /// <summary>
    /// Creates a failed agent result with multiple errors.
    /// </summary>
    /// <param name="errors">The list of error messages.</param>
    /// <returns>A failed agent result.</returns>
    public new static AgentResult<T> Fail(IEnumerable<string> errors) => new() { Success = false, Errors = errors.ToList() };
}
