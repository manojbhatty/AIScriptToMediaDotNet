namespace AIScriptToMediaDotNet.Core.Agents;

/// <summary>
/// Represents an agent that processes input and produces output.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the name of the agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the agent's purpose.
    /// </summary>
    string Description { get; }
}

/// <summary>
/// Represents a generic agent that processes input of type TInput and produces output of type TOutput.
/// </summary>
/// <typeparam name="TInput">The input type for the agent.</typeparam>
/// <typeparam name="TOutput">The output type for the agent.</typeparam>
public interface IAgent<in TInput, TOutput> : IAgent
{
    /// <summary>
    /// Processes the input and produces an agent result containing the output.
    /// </summary>
    /// <param name="input">The input to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent result containing the output or error information.</returns>
    Task<AgentResult<TOutput>> ProcessAsync(TInput input, CancellationToken cancellationToken = default);
}
