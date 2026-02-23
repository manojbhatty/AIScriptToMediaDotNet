namespace AIScriptToMediaDotNet.Core.Agents;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets feedback for correction when validation fails.
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Creates a passing validation result.
    /// </summary>
    /// <returns>A passing validation result.</returns>
    public static ValidationResult Pass() => new() { IsValid = true };

    /// <summary>
    /// Creates a failing validation result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="feedback">Optional feedback for correction.</param>
    /// <returns>A failing validation result.</returns>
    public static ValidationResult Fail(string error, string? feedback = null) => new()
    {
        IsValid = false,
        Errors = { error },
        Feedback = feedback
    };

    /// <summary>
    /// Creates a failing validation result with multiple errors.
    /// </summary>
    /// <param name="errors">The list of error messages.</param>
    /// <param name="feedback">Optional feedback for correction.</param>
    /// <returns>A failing validation result.</returns>
    public static ValidationResult Fail(IEnumerable<string> errors, string? feedback = null) => new()
    {
        IsValid = false,
        Errors = errors.ToList(),
        Feedback = feedback
    };
}
