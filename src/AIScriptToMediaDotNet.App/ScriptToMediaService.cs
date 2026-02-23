using AIScriptToMediaDotNet.Agents.Scene;
using AIScriptToMediaDotNet.Core.Context;
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
    private readonly ILogger<ScriptToMediaService> _logger;

    /// <summary>
    /// Initializes a new instance of the ScriptToMediaService class.
    /// </summary>
    /// <param name="orchestrator">The pipeline orchestrator.</param>
    /// <param name="sceneParser">The scene parser agent.</param>
    /// <param name="logger">The logger instance.</param>
    public ScriptToMediaService(
        PipelineOrchestrator orchestrator,
        SceneParserAgent sceneParser,
        ILogger<ScriptToMediaService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _sceneParser = sceneParser ?? throw new ArgumentNullException(nameof(sceneParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        // Initialize context
        var context = new ScriptToMediaContext
        {
            Title = title,
            OriginalScript = script,
            MaxRetriesPerStage = 3
        };

        try
        {
            // Stage 1: Scene Parsing
            _logger.LogInformation("Stage 1: Parsing scenes...");
            var parsed = await _orchestrator.ExecuteStageAsync(
                context,
                "SceneParsing",
                _sceneParser,
                ctx => ctx.OriginalScript,
                (ctx, scenes) => ctx.Scenes = scenes,
                cancellationToken);

            if (!parsed)
            {
                _logger.LogError("Scene parsing failed after all retries");
                return context;
            }

            _logger.LogInformation("Successfully parsed {SceneCount} scenes", context.Scenes.Count);

            // Log pipeline status
            var status = _orchestrator.GetPipelineStatus(context);
            _logger.LogInformation("Pipeline Status:\n{Status}", status);

            // Export results
            var outputFolder = Export.MarkdownExporter.Export(context, outputPath);
            _logger.LogInformation("Output exported to: {OutputFolder}", outputFolder);

            return context;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed with error");
            throw;
        }
    }
}
