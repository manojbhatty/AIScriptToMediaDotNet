using AIScriptToMediaDotNet.Core.Agents;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Logging;
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
    /// <param name="executionContext">Optional execution context for detailed logging.</param>
    /// <returns>True if the stage completed successfully, false otherwise.</returns>
    public async Task<bool> ExecuteStageAsync<TInput, TOutput>(
        ScriptToMediaContext context,
        string stageName,
        IAgent<TInput, TOutput> agent,
        Func<ScriptToMediaContext, TInput> inputProvider,
        Action<ScriptToMediaContext, TOutput> outputConsumer,
        CancellationToken cancellationToken = default,
        PipelineExecutionContext? executionContext = null)
    {
        _logger.LogInformation("Starting stage: {StageName} (Agent: {AgentName})", stageName, agent.Name);
        context.MarkStageStarted(stageName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;
        string? feedback = null;
        TInput? lastInput = default(TInput);

        while (attempt < _maxRetriesPerStage)
        {
            attempt++;
            context.IncrementStageRetry(stageName);

            _logger.LogDebug("Attempt {Attempt}/{MaxRetries} for stage {StageName}",
                attempt, _maxRetriesPerStage, stageName);

            try
            {
                // Get input from context (or modify existing input to include feedback)
                if (lastInput == null)
                {
                    lastInput = inputProvider(context);
                    
                    // Log the start with input data (on first attempt)
                    var inputJson = PipelineExecutionContext.SerializeToJson(lastInput);
                    var inputSummary = lastInput?.ToString() ?? "null";
                    executionContext?.LogAgentStart(agent.Name, stageName, inputSummary, inputJson);
                }
                else
                {
                    // For types that support feedback (like SceneParserInput), update the feedback property
                    var inputType = lastInput!.GetType();
                    var feedbackProperty = inputType.GetProperty("Feedback");
                    if (feedbackProperty != null && !string.IsNullOrEmpty(feedback))
                    {
                        feedbackProperty.SetValue(lastInput, feedback);
                        _logger.LogDebug("Updated input with feedback: {Feedback}", feedback);
                    }
                }

                // If we have feedback from previous attempt, log it
                if (!string.IsNullOrEmpty(feedback))
                {
                    _logger.LogDebug("Using feedback from previous attempt: {Feedback}", feedback);
                    executionContext?.LogAgentRetry(agent.Name, stageName, attempt, feedback);
                }

                // Execute the agent
                var result = await agent.ProcessAsync(lastInput!, cancellationToken);
                stopwatch.Stop();

                if (result.Success)
                {
                    // Consume the output and update context
                    outputConsumer(context, result.Data!);
                    context.MarkStageComplete(stageName, result.ExecutionTime);
                    
                    // Serialize output data for logging
                    var outputJson = PipelineExecutionContext.SerializeToJson(result.Data);
                    var outputSummary = result.Data?.ToString() ?? "Success";
                    
                    executionContext?.LogAgentComplete(agent.Name, stageName,
                        outputSummary, outputJson, (long)result.ExecutionTime.TotalMilliseconds);

                    _logger.LogInformation(
                        "Stage {StageName} completed successfully in {ElapsedMs}ms (Attempt {Attempt})",
                        stageName, stopwatch.ElapsedMilliseconds, attempt);

                    return true;
                }

                // Stage failed - store errors and feedback for retry
                feedback = result.Metadata.TryGetValue("Feedback", out var fb) ? fb?.ToString() : null;

                // If no feedback in metadata, use errors as feedback
                if (string.IsNullOrEmpty(feedback) && result.Errors.Any())
                {
                    feedback = string.Join("; ", result.Errors);
                }

                context.MarkStageFailed(stageName, result.Errors, feedback);
                
                // Serialize input and output for error logging
                var errorInputJson = PipelineExecutionContext.SerializeToJson(lastInput);
                var errorOutputJson = result.Data != null ? PipelineExecutionContext.SerializeToJson(result.Data) : null;
                
                executionContext?.LogAgentError(
                    agent.Name, 
                    stageName,
                    string.Join("; ", result.Errors), 
                    null, 
                    lastInput?.ToString(),
                    errorInputJson,
                    errorOutputJson,
                    (long)stopwatch.ElapsedMilliseconds);

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
                executionContext?.LogAgentError(agent.Name, stageName, "Cancelled", null);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                context.MarkStageFailed(stageName, new[] { ex.Message }, null);
                executionContext?.LogAgentError(agent.Name, stageName, ex.Message, ex.StackTrace);

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
        executionContext?.LogAgentError(agent.Name, stageName,
            $"Stage failed after {_maxRetriesPerStage} attempts", null);

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

    /// <summary>
    /// Executes a single pipeline stage with custom retry logic and best-attempt fallback.
    /// </summary>
    /// <typeparam name="TInput">The input type for the agent.</typeparam>
    /// <typeparam name="TOutput">The output type for the agent.</typeparam>
    /// <param name="context">The shared context object.</param>
    /// <param name="stageName">The name of the stage.</param>
    /// <param name="agent">The agent to execute.</param>
    /// <param name="inputProvider">Function to provide input from context.</param>
    /// <param name="outputConsumer">Action to consume output and update context.</param>
    /// <param name="maxRetries">Maximum retry attempts for this stage.</param>
    /// <param name="useBestAttemptOnFailure">If true, picks the best attempt on failure instead of failing the stage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="executionContext">Optional execution context for detailed logging.</param>
    /// <returns>True if the stage completed successfully, false otherwise.</returns>
    public async Task<bool> ExecuteStageAsync<TInput, TOutput>(
        ScriptToMediaContext context,
        string stageName,
        IAgent<TInput, TOutput> agent,
        Func<ScriptToMediaContext, TInput> inputProvider,
        Action<ScriptToMediaContext, TOutput> outputConsumer,
        int maxRetries,
        bool useBestAttemptOnFailure,
        CancellationToken cancellationToken = default,
        PipelineExecutionContext? executionContext = null)
    {
        _logger.LogInformation("Starting stage: {StageName} (Agent: {AgentName}, MaxRetries: {MaxRetries}, UseBestAttempt: {UseBest})", 
            stageName, agent.Name, maxRetries, useBestAttemptOnFailure);
        context.MarkStageStarted(stageName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;
        string? feedback = null;
        TInput? lastInput = default(TInput);
        
        // Track all attempts for best-attempt selection
        var allAttempts = new List<(int AttemptNumber, AgentResult<TOutput> Result, TOutput? Data, string? Feedback)>();

        while (attempt < maxRetries)
        {
            attempt++;
            context.IncrementStageRetry(stageName);

            _logger.LogDebug("Attempt {Attempt}/{MaxRetries} for stage {StageName}",
                attempt, maxRetries, stageName);

            try
            {
                // Get input from context (or modify existing input to include feedback)
                if (lastInput == null)
                {
                    lastInput = inputProvider(context);

                    // Log the start with input data (on first attempt)
                    var inputJson = PipelineExecutionContext.SerializeToJson(lastInput);
                    var inputSummary = lastInput?.ToString() ?? "null";
                    executionContext?.LogAgentStart(agent.Name, stageName, inputSummary, inputJson);
                }
                else
                {
                    // For types that support feedback (like SceneParserInput), update the feedback property
                    var inputType = lastInput!.GetType();
                    var feedbackProperty = inputType.GetProperty("Feedback");
                    if (feedbackProperty != null && !string.IsNullOrEmpty(feedback))
                    {
                        feedbackProperty.SetValue(lastInput, feedback);
                        _logger.LogDebug("Updated input with feedback: {Feedback}", feedback);
                    }
                }

                // If we have feedback from previous attempt, log it
                if (!string.IsNullOrEmpty(feedback))
                {
                    _logger.LogDebug("Using feedback from previous attempt: {Feedback}", feedback);
                    executionContext?.LogAgentRetry(agent.Name, stageName, attempt, feedback);
                }

                // Execute the agent
                var result = await agent.ProcessAsync(lastInput!, cancellationToken);
                stopwatch.Stop();
                
                // Track this attempt
                allAttempts.Add((attempt, result, result.Data, feedback));

                if (result.Success)
                {
                    // Consume the output and update context
                    outputConsumer(context, result.Data!);
                    context.MarkStageComplete(stageName, result.ExecutionTime);

                    // Serialize output data for logging
                    var outputJson = PipelineExecutionContext.SerializeToJson(result.Data);
                    var outputSummary = result.Data?.ToString() ?? "Success";

                    executionContext?.LogAgentComplete(agent.Name, stageName,
                        outputSummary, outputJson, (long)result.ExecutionTime.TotalMilliseconds);

                    _logger.LogInformation(
                        "Stage {StageName} completed successfully in {ElapsedMs}ms (Attempt {Attempt})",
                        stageName, stopwatch.ElapsedMilliseconds, attempt);

                    return true;
                }

                // Stage failed - store errors and feedback for retry
                feedback = result.Metadata.TryGetValue("Feedback", out var fb) ? fb?.ToString() : null;

                // If no feedback in metadata, use errors as feedback
                if (string.IsNullOrEmpty(feedback) && result.Errors.Any())
                {
                    feedback = string.Join("; ", result.Errors);
                }

                context.MarkStageFailed(stageName, result.Errors, feedback);

                // Serialize input and output for error logging
                var errorInputJson = PipelineExecutionContext.SerializeToJson(lastInput);
                var errorOutputJson = result.Data != null ? PipelineExecutionContext.SerializeToJson(result.Data) : null;

                executionContext?.LogAgentError(
                    agent.Name,
                    stageName,
                    string.Join("; ", result.Errors),
                    null,
                    lastInput?.ToString(),
                    errorInputJson,
                    errorOutputJson,
                    (long)stopwatch.ElapsedMilliseconds);

                _logger.LogWarning(
                    "Stage {StageName} failed (Attempt {Attempt}/{MaxRetries}): {Errors}",
                    stageName, attempt, maxRetries, string.Join("; ", result.Errors));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Stage {StageName} threw exception (Attempt {Attempt}/{MaxRetries})",
                    stageName, attempt, maxRetries);

                executionContext?.LogAgentError(
                    agent.Name,
                    stageName,
                    ex.Message,
                    ex.StackTrace,
                    lastInput?.ToString(),
                    PipelineExecutionContext.SerializeToJson(lastInput),
                    null,
                    (long)stopwatch.ElapsedMilliseconds);

                feedback = ex.Message;
                context.MarkStageFailed(stageName, new[] { ex.Message }, ex.StackTrace);
            }

            if (attempt >= maxRetries)
            {
                _logger.LogError(
                    "Stage {StageName} failed after {MaxRetries} attempts",
                    stageName, maxRetries);

                // If useBestAttemptOnFailure is enabled, pick the best attempt
                if (useBestAttemptOnFailure && allAttempts.Any())
                {
                    _logger.LogInformation("Selecting best attempt from {AttemptCount} failed attempts", allAttempts.Count);
                    
                    // Pick the attempt with fewest errors (or last attempt as tie-breaker)
                    var bestAttempt = allAttempts
                        .OrderBy(a => a.Result.Errors.Count)
                        .ThenByDescending(a => a.AttemptNumber)
                        .FirstOrDefault();
                    
                    // For types that have data, use the best one with data
                    // For validation types, use the best attempt even if it indicates failure
                    var hasData = bestAttempt.Data != null;
                    
                    if (hasData)
                    {
                        _logger.LogInformation("Using best attempt (Attempt {Attempt}) with {ErrorCount} errors",
                            bestAttempt.AttemptNumber, bestAttempt.Result.Errors.Count);
                        
                        // Consume the best output
                        outputConsumer(context, bestAttempt.Data);
                        context.MarkStageComplete(stageName, TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds));
                        
                        // Log the best attempt
                        var outputJson = PipelineExecutionContext.SerializeToJson(bestAttempt.Data);
                        executionContext?.LogAgentComplete(agent.Name, stageName,
                            $"Best of {allAttempts.Count} attempts (had {bestAttempt.Result.Errors.Count} errors)",
                            outputJson, (long)stopwatch.ElapsedMilliseconds);
                        
                        _logger.LogWarning(
                            "Stage {StageName} completed with best attempt (had {ErrorCount} errors): {Errors}",
                            stageName, bestAttempt.Result.Errors.Count, string.Join("; ", bestAttempt.Result.Errors));
                        
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Best attempt selection found no data in attempts");
                    }
                }

                return false;
            }

            // Delay before retry (exponential backoff)
            await Task.Delay(1000 * attempt, cancellationToken);
        }

        // Return false if we exhausted all retries
        context.MarkStageFailed(stageName,
            new[] { $"Stage failed after {maxRetries} attempts" },
            $"Stage failed after {maxRetries} attempts");

        return false;
    }
}
