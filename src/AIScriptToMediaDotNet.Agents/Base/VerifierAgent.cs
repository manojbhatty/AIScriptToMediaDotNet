using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Agents.Base;

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

/// <summary>
/// Base class for verifier agents that validate content.
/// </summary>
/// <typeparam name="TInput">The input type to validate.</typeparam>
public abstract class VerifierAgent<TInput> : BaseAgent, IAgent<TInput, ValidationResult>
{
    /// <summary>
    /// Initializes a new instance of the VerifierAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    protected VerifierAgent(IAIProvider aiProvider, ILogger<VerifierAgent<TInput>> logger)
        : base(aiProvider, logger)
    {
    }

    /// <summary>
    /// Processes the input and validates it.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent result containing the validation result.</returns>
    public abstract Task<AgentResult<ValidationResult>> ProcessAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the input. Override this method in derived classes.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    protected abstract Task<ValidationResult> ValidateAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the prompt for validation. Override this method in derived classes.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected abstract string BuildPrompt(TInput input);

    /// <summary>
    /// Parses the AI response into a validation result. Override this method in derived classes.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The parsed validation result.</returns>
    protected abstract ValidationResult ParseResponse(string response);
}
