using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.Core.Orchestration;

/// <summary>
/// Orchestrates the execution of the script-to-media pipeline.
/// Manages agent execution, retry logic, and progress tracking.
/// </summary>
public class PipelineOrchestrator
{
    private readonly ILogger<PipelineOrchestrator> _logger;
    private readonly int _maxRetriesPerStage;

    /// <summary>
    /// Initializes a new instance of the PipelineOrchestrator class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="maxRetriesPerStage">Maximum retry attempts per stage (default: 3).</param>
    public PipelineOrchestrator(
        ILogger<PipelineOrchestrator> logger,
        int maxRetriesPerStage = 3)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxRetriesPerStage = maxRetriesPerStage;
    }

    /// <summary>
    /// Executes a single pipeline stage with retry logic.
    /// </summary>
    /// <typeparam name="TInput">The input type for the agent.</typeparam>
    /// <typeparam name="TOutput">The output type for the agent.</typeparam>
    /// <param name="context">The shared context object.</param>
    /// <param name="stageName">The name of the stage.</param>
    /// <param name="agent">The agent to execute.</param>
    /// <param name="inputProvider">Function to provide input from context.</param>
    /// <param name="outputConsumer">Action to consume output and update context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the stage completed successfully, false otherwise.</returns>
    public async Task<bool> ExecuteStageAsync<TInput, TOutput>(
        ScriptToMediaContext context,
        string stageName,
        IAgent<TInput, TOutput> agent,
        Func<ScriptToMediaContext, TInput> inputProvider,
        Action<ScriptToMediaContext, TOutput> outputConsumer,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting stage: {StageName} (Agent: {AgentName})", stageName, agent.Name);
        context.MarkStageStarted(stageName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;
        string? feedback = null;

        while (attempt < _maxRetriesPerStage)
        {
            attempt++;
            context.IncrementStageRetry(stageName);

            _logger.LogDebug("Attempt {Attempt}/{MaxRetries} for stage {StageName}",
                attempt, _maxRetriesPerStage, stageName);

            try
            {
                // Get input from context
                var input = inputProvider(context);

                // If we have feedback from previous attempt, log it
                if (!string.IsNullOrEmpty(feedback))
                {
                    _logger.LogDebug("Using feedback from previous attempt: {Feedback}", feedback);
                }

                // Execute the agent
                var result = await agent.ProcessAsync(input, cancellationToken);
                stopwatch.Stop();

                if (result.Success)
                {
                    // Consume the output and update context
                    outputConsumer(context, result.Data!);
                    context.MarkStageComplete(stageName, result.ExecutionTime);

                    _logger.LogInformation(
                        "Stage {StageName} completed successfully in {ElapsedMs}ms (Attempt {Attempt})",
                        stageName, stopwatch.ElapsedMilliseconds, attempt);

                    return true;
                }

                // Stage failed - store errors and feedback for retry
                feedback = result.Metadata.TryGetValue("Feedback", out var fb) ? fb?.ToString() : null;
                context.MarkStageFailed(stageName, result.Errors, feedback);

                _logger.LogWarning(
                    "Stage {StageName} failed (Attempt {Attempt}/{MaxRetries}): {Errors}",
                    stageName, attempt, _maxRetriesPerStage, string.Join("; ", result.Errors));

                if (!string.IsNullOrEmpty(feedback))
                {
                    _logger.LogDebug("Feedback for retry: {Feedback}", feedback);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogInformation("Stage {StageName} cancelled", stageName);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                context.MarkStageFailed(stageName, new[] { ex.Message }, null);

                _logger.LogError(ex, "Stage {StageName} threw exception (Attempt {Attempt}/{MaxRetries})",
                    stageName, attempt, _maxRetriesPerStage);
            }

            // Check if we can retry
            if (attempt >= _maxRetriesPerStage)
            {
                _logger.LogError(
                    "Stage {StageName} failed after {MaxRetries} attempts",
                    stageName, _maxRetriesPerStage);
                break;
            }

            // Small delay before retry
            await Task.Delay(1000 * attempt, cancellationToken);
        }

        // All retries exhausted
        context.MarkStageFailed(stageName,
            new[] { $"Stage failed after {_maxRetriesPerStage} attempts" },
            null);

        return false;
    }

    /// <summary>
    /// Executes a verification stage with retry logic.
    /// </summary>
    /// <typeparam name="TInput">The input type to validate.</typeparam>
    /// <param name="context">The shared context object.</param>
    /// <param name="stageName">The name of the stage.</param>
    /// <param name="verifierAgent">The verifier agent.</param>
    /// <param name="inputProvider">Function to provide input from context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    public async Task<ValidationResult> ExecuteVerificationStageAsync<TInput>(
        ScriptToMediaContext context,
        string stageName,
        IAgent<TInput, ValidationResult> verifierAgent,
        Func<ScriptToMediaContext, TInput> inputProvider,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting verification stage: {StageName} (Agent: {AgentName})",
            stageName, verifierAgent.Name);
        context.MarkStageStarted(stageName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;

        while (attempt < _maxRetriesPerStage)
        {
            attempt++;
            context.IncrementStageRetry(stageName);

            try
            {
                var input = inputProvider(context);
                var result = await verifierAgent.ProcessAsync(input, cancellationToken);
                stopwatch.Stop();

                if (result.Success && result.Data != null)
                {
                    var validationResult = result.Data;

                    if (validationResult.IsValid)
                    {
                        context.MarkStageComplete(stageName, result.ExecutionTime);
                        _logger.LogInformation(
                            "Verification {StageName} passed in {ElapsedMs}ms",
                            stageName, stopwatch.ElapsedMilliseconds);

                        return validationResult;
                    }

                    // Validation failed - store errors and feedback
                    context.MarkStageFailed(stageName, validationResult.Errors, validationResult.Feedback);

                    _logger.LogWarning(
                        "Verification {StageName} failed (Attempt {Attempt}/{MaxRetries}): {Errors}",
                        stageName, attempt, _maxRetriesPerStage,
                        string.Join("; ", validationResult.Errors));

                    if (!string.IsNullOrEmpty(validationResult.Feedback))
                    {
                        _logger.LogDebug("Feedback for creator: {Feedback}", validationResult.Feedback);
                    }
                }
                else
                {
                    context.MarkStageFailed(stageName, result.Errors, null);
                    _logger.LogWarning("Verification {StageName} returned failure result", stageName);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogInformation("Verification {StageName} cancelled", stageName);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                context.MarkStageFailed(stageName, new[] { ex.Message }, null);
                _logger.LogError(ex, "Verification {StageName} threw exception", stageName);
            }

            if (attempt >= _maxRetriesPerStage)
            {
                break;
            }

            await Task.Delay(1000 * attempt, cancellationToken);
        }

        // Return failed validation result
        return ValidationResult.Fail(
            new[] { $"Verification failed after {_maxRetriesPerStage} attempts" },
            null);
    }

    /// <summary>
    /// Gets the current pipeline status from the context.
    /// </summary>
    /// <param name="context">The context object.</param>
    /// <returns>A status string.</returns>
    public string GetPipelineStatus(ScriptToMediaContext context)
    {
        var status = new System.Text.StringBuilder();
        status.AppendLine($"Pipeline Status for: {context.Title} (ID: {context.Id})");
        status.AppendLine($"Current Stage: {context.CurrentStage}");
        status.AppendLine($"Total Retries: {context.TotalRetryCount}");
        status.AppendLine();
        status.AppendLine("Stage Progress:");

        foreach (var stageState in context.StageStates)
        {
            var state = stageState.Value;
            var icon = state.IsComplete ? "✓" : state.HasFailed ? "✗" : "→";
            status.AppendLine($"  {icon} {state.StageName}: Retries={state.RetryCount}, Errors={state.ValidationErrors.Count}");
        }

        return status.ToString();
    }
}
