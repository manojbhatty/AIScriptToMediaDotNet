using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Agents.Base;

/// <summary>
/// Base class for creator agents that generate content from input.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type to create.</typeparam>
public abstract class CreatorAgent<TInput, TOutput> : BaseAgent, IAgent<TInput, TOutput>
{
    /// <summary>
    /// Initializes a new instance of the CreatorAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    protected CreatorAgent(IAIProvider aiProvider, ILogger<CreatorAgent<TInput, TOutput>> logger)
        : base(aiProvider, logger)
    {
    }

    /// <summary>
    /// Processes the input and creates the output.
    /// </summary>
    /// <param name="input">The input to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent result containing the created output.</returns>
    public abstract Task<AgentResult<TOutput>> ProcessAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the output from the input. Override this method in derived classes.
    /// </summary>
    /// <param name="input">The input to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created output.</returns>
    protected abstract Task<TOutput> CreateAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the prompt for creation. Override this method in derived classes.
    /// </summary>
    /// <param name="input">The input to process.</param>
    /// <returns>The prompt to send to the AI.</returns>
    protected abstract string BuildPrompt(TInput input);

    /// <summary>
    /// Parses the AI response into the output type. Override this method in derived classes.
    /// </summary>
    /// <param name="response">The AI response text.</param>
    /// <returns>The parsed output.</returns>
    protected abstract TOutput ParseResponse(string response);
}
