using AIScriptToMediaDotNet.Agents.Scene;
using AIScriptToMediaDotNet.Core.Context;
using AIScriptToMediaDotNet.Core.Logging;
using AIScriptToMediaDotNet.Core.Orchestration;
using AIScriptToMediaDotNet.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace AIScriptToMediaDotNet.App;

/// <summary>
/// Service that orchestrates the script-to-media pipeline.
/// </summary>
public class ScriptToMediaService
{
    private readonly PipelineOrchestrator _orchestrator;
    private readonly SceneParserAgent _sceneParser;
    private readonly SceneVerifierAgent _sceneVerifier;
    private readonly ILogger<ScriptToMediaService> _logger;
    private readonly PipelineExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the ScriptToMediaService class.
    /// </summary>
    /// <param name="orchestrator">The pipeline orchestrator.</param>
    /// <param name="sceneParser">The scene parser agent.</param>
    /// <param name="sceneVerifier">The scene verifier agent.</param>
    /// <param name="logger">The logger instance.</param>
    public ScriptToMediaService(
        PipelineOrchestrator orchestrator,
        SceneParserAgent sceneParser,
        SceneVerifierAgent sceneVerifier,
        ILogger<ScriptToMediaService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _sceneParser = sceneParser ?? throw new ArgumentNullException(nameof(sceneParser));
        _sceneVerifier = sceneVerifier ?? throw new ArgumentNullException(nameof(sceneVerifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionContext = new PipelineExecutionContext();
    }

    /// <summary>
    /// Processes a script and generates all outputs.
    /// </summary>
    /// <param name="title">The script title.</param>
    /// <param name="script">The script text.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline context with all generated data.</returns>
    public async Task<ScriptToMediaContext> ProcessScriptAsync(
        string title,
        string script,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting pipeline for: {Title}", title);

        // Initialize execution context for detailed logging
        _executionContext.Title = title;
        _executionContext.FullScript = script;
        _executionContext.ScriptSummary = script.Length > 1000 
            ? script.Substring(0, 1000) + $"... ({script.Length - 1000} more chars)" 
            : script;
        _executionContext.ConfigurationSnapshot["Ollama.Endpoint"] = "http://localhost:11434";
        _executionContext.ConfigurationSnapshot["Ollama.DefaultModel"] = "qwen2.5-coder:latest";
        _executionContext.ConfigurationSnapshot["MaxRetries"] = "3";

        // Initialize context
        var context = new ScriptToMediaContext
        {
            Title = title,
            OriginalScript = script,
            MaxRetriesPerStage = 3
        };

        try
        {
            // Log pipeline start
            _executionContext.LogAgentStart("Pipeline", "Initialization", $"Title: {title}, Script Length: {script.Length}");

            // Stage 1: Scene Parsing
            _logger.LogInformation("Stage 1: Parsing scenes...");
            var parsed = await _orchestrator.ExecuteStageAsync(
                context,
                "SceneParsing",
                _sceneParser,
                ctx => ctx.OriginalScript,
                (ctx, scenes) => ctx.Scenes = scenes,
                cancellationToken,
                _executionContext);

            if (!parsed)
            {
                _logger.LogError("Scene parsing failed after all retries");
                _executionContext.Fail("Scene parsing failed after all retries");
                
                // Export error log even on failure
                var errorLogPath = Path.Combine(outputPath, $"error-{_executionContext.ExecutionId}.md");
                ExportErrorLog(_executionContext, errorLogPath);
                _logger.LogInformation("Error log exported to: {ErrorLogPath}", errorLogPath);
                
                return context;
            }

            _logger.LogInformation("Successfully parsed {SceneCount} scenes", context.Scenes.Count);

            // Stage 2: Scene Verification
            _logger.LogInformation("Stage 2: Verifying scenes...");
            var verified = await _orchestrator.ExecuteStageAsync(
                context,
                "SceneVerification",
                _sceneVerifier,
                ctx => ctx.Scenes,
                (ctx, validationResult) => { /* Validation result stored in context if needed */ },
                cancellationToken,
                _executionContext);

            if (!verified)
            {
                _logger.LogError("Scene verification failed after all retries");
                _executionContext.Fail("Scene verification failed after all retries");
                
                // Export error log even on failure
                var errorLogPath = Path.Combine(outputPath, $"error-{_executionContext.ExecutionId}.md");
                ExportErrorLog(_executionContext, errorLogPath);
                _logger.LogInformation("Error log exported to: {ErrorLogPath}", errorLogPath);
                
                return context;
            }

            _logger.LogInformation("Scenes verified successfully");
            _executionContext.LogAgentComplete("SceneParser", "SceneParsing", $"{context.Scenes.Count} scenes parsed", 0);

            // Log pipeline status
            var status = _orchestrator.GetPipelineStatus(context);
            _logger.LogInformation("Pipeline Status:\n{Status}", status);

            // Export results
            var outputFolder = Export.MarkdownExporter.Export(context, outputPath);
            _logger.LogInformation("Output exported to: {OutputFolder}", outputFolder);

            // Export detailed execution log
            var executionLogPath = Path.Combine(outputFolder, "execution-log.md");
            _executionContext.Complete("Success");
            ExecutionLogExporter.Export(_executionContext, executionLogPath);
            _logger.LogInformation("Execution log exported to: {ExecutionLogPath}", executionLogPath);

            // Export error log if there were any errors (for quick reference)
            if (_executionContext.LogEntries.Any(e => e.Level == "ERROR"))
            {
                var errorLogPath = Path.Combine(outputFolder, "error-log.md");
                ExportErrorLog(_executionContext, errorLogPath);
                _logger.LogInformation("Error log exported to: {ErrorLogPath}", errorLogPath);
            }

            return context;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled by user");
            _executionContext.Fail("Pipeline cancelled by user");
            
            // Export error log on cancellation
            var errorLogPath = Path.Combine(outputPath, $"error-{_executionContext.ExecutionId}.md");
            ExportErrorLog(_executionContext, errorLogPath);
            _logger.LogInformation("Error log exported to: {ErrorLogPath}", errorLogPath);
            
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed with error");
            _executionContext.Fail(ex.Message, ex.StackTrace);
            
            // Export error log on exception
            var errorLogPath = Path.Combine(outputPath, $"error-{_executionContext.ExecutionId}.md");
            ExportErrorLog(_executionContext, errorLogPath);
            _logger.LogInformation("Error log exported to: {ErrorLogPath}", errorLogPath);
            
            throw;
        }
    }

    /// <summary>
    /// Gets the execution context for this pipeline run.
    /// </summary>
    public PipelineExecutionContext GetExecutionContext() => _executionContext;

    /// <summary>
    /// Exports a concise error log for quick reference.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="outputPath">The output file path.</param>
    private static void ExportErrorLog(PipelineExecutionContext context, string outputPath)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine($"# Error Log\n\n");
        md.AppendLine($"**Execution ID:** {context.ExecutionId}\n\n");
        md.AppendLine($"**Title:** {context.Title}\n\n");
        md.AppendLine($"**Status:** {context.Status}\n\n");
        md.AppendLine($"**Time:** {context.StartTime:yyyy-MM-dd HH:mm:ss}\n\n");

        var errors = context.LogEntries.Where(e => e.Level == "ERROR").ToList();
        if (errors.Any())
        {
            md.AppendLine($"## Errors ({errors.Count})\n\n");
            foreach (var error in errors)
            {
                md.AppendLine($"### {error.Agent} - {error.Stage}\n\n");
                md.AppendLine($"**Time:** {error.Timestamp:yyyy-MM-dd HH:mm:ss}\n\n");
                md.AppendLine($"**Event:** {error.Event}\n\n");
                md.AppendLine($"**Message:** {error.Message}\n\n");
                if (!string.IsNullOrEmpty(error.ErrorDetails))
                {
                    md.AppendLine("**Error Details:**\n\n");
                    md.AppendLine("```\n");
                    md.AppendLine(error.ErrorDetails);
                    md.AppendLine("```\n\n");
                }
                if (!string.IsNullOrEmpty(error.InputSummary))
                {
                    md.AppendLine("**Input:**\n\n");
                    md.AppendLine("```\n");
                    md.AppendLine(error.InputSummary);
                    md.AppendLine("```\n\n");
                }
                if (error.RetryCount.HasValue)
                {
                    md.AppendLine($"**Retry Count:** {error.RetryCount}\n\n");
                }
                md.AppendLine("---\n\n");
            }
        }

        if (!string.IsNullOrEmpty(context.FinalError))
        {
            md.AppendLine("## Final Pipeline Error\n\n");
            md.AppendLine($"**Error:** {context.FinalError}\n\n");
            if (!string.IsNullOrEmpty(context.FinalStackTrace))
            {
                md.AppendLine("**Stack Trace:**\n\n");
                md.AppendLine("```\n");
                md.AppendLine(context.FinalStackTrace);
                md.AppendLine("```\n\n");
            }
        }

        md.AppendLine("## Summary\n\n");
        md.AppendLine($"- **Total Errors:** {errors.Count}\n");
        md.AppendLine($"- **Total Retries:** {context.LogEntries.Count(e => e.Event == "Retry")}\n");
        md.AppendLine($"- **Failed Stages:** {errors.Select(e => e.Stage).Distinct().Count()}\n");

        File.WriteAllText(outputPath, md.ToString());
    }
}
