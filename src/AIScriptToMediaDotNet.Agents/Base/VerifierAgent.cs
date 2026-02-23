using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Agents.Base;

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
