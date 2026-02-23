using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Agents.Base;

/// <summary>
/// Base class for all agents providing common functionality like logging and timing.
/// </summary>
public abstract class BaseAgent : IAgent
{
    private readonly ILogger<BaseAgent> _logger;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public virtual string Description => $"Agent that processes {GetType().Name}";

    /// <summary>
    /// Gets the AI provider used by this agent.
    /// </summary>
    protected readonly IAIProvider AIProvider;

    /// <summary>
    /// Gets the model name configured for this agent.
    /// </summary>
    protected readonly string ModelName;

    /// <summary>
    /// Initializes a new instance of the BaseAgent class.
    /// </summary>
    /// <param name="aiProvider">The AI provider to use.</param>
    /// <param name="logger">The logger instance.</param>
    protected BaseAgent(IAIProvider aiProvider, ILogger<BaseAgent> logger)
    {
        AIProvider = aiProvider ?? throw new ArgumentNullException(nameof(aiProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ModelName = aiProvider.GetModelForAgent(GetType().Name);
    }

    /// <summary>
    /// Invokes the AI provider with the given prompt.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI response text.</returns>
    protected async Task<string> InvokeAIAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var options = new Core.Options.ModelOptions
        {
            Model = ModelName,
            MaxTokens = 4096,
            Temperature = 0.7
        };

        _logger.LogDebug("Invoking AI with model '{Model}' for agent '{Agent}'", ModelName, Name);

        var response = await AIProvider.GenerateResponseAsync(prompt, options, cancellationToken);

        _logger.LogDebug("AI response received ({Length} chars)", response?.Length ?? 0);

        return response ?? throw new InvalidOperationException("AI provider returned null response");
    }

    /// <summary>
    /// Invokes the AI provider with custom options.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="options">Custom model options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI response text.</returns>
    protected async Task<string> InvokeAIAsync(string prompt, Core.Options.ModelOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Model))
        {
            options.Model = ModelName;
        }

        _logger.LogDebug("Invoking AI with model '{Model}' for agent '{Agent}'", options.Model, Name);

        var response = await AIProvider.GenerateResponseAsync(prompt, options, cancellationToken);

        _logger.LogDebug("AI response received ({Length} chars)", response?.Length ?? 0);

        return response ?? throw new InvalidOperationException("AI provider returned null response");
    }

    /// <summary>
    /// Creates a successful agent result.
    /// </summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="data">The result data.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <returns>A successful agent result.</returns>
    protected static AgentResult<T> CreateSuccessResult<T>(T data, TimeSpan? executionTime = null)
    {
        var result = AgentResult<T>.Ok(data);
        if (executionTime.HasValue)
        {
            result.ExecutionTime = executionTime.Value;
        }
        return result;
    }

    /// <summary>
    /// Creates a failed agent result.
    /// </summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="error">The error message.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <returns>A failed agent result.</returns>
    protected static AgentResult<T> CreateFailureResult<T>(string error, TimeSpan? executionTime = null)
    {
        var result = AgentResult<T>.Fail(error);
        if (executionTime.HasValue)
        {
            result.ExecutionTime = executionTime.Value;
        }
        return result;
    }

    /// <summary>
    /// Measures the execution time of an async operation.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="result">The resulting agent result with timing set.</param>
    protected static async Task MeasureExecutionTime<T>(
        Func<Task<AgentResult<T>>> operation,
        Action<AgentResult<T>> result)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var agentResult = await operation();
        stopwatch.Stop();
        agentResult.ExecutionTime = stopwatch.Elapsed;
        result(agentResult);
    }
}
