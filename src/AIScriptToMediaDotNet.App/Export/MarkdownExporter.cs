using AIScriptToMediaDotNet.Core.Context;

namespace AIScriptToMediaDotNet.App.Export;

/// <summary>
/// Exports pipeline context to markdown files.
/// </summary>
public static class MarkdownExporter
{
    /// <summary>
    /// Exports all context data to markdown files in the output directory.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <returns>Path to the created output folder.</returns>
    public static string Export(ScriptToMediaContext context, string outputPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var folderName = $"{SanitizeFileName(context.Title)}_{timestamp}";
        var outputFolder = Path.Combine(outputPath, folderName);

        Directory.CreateDirectory(outputFolder);

        // Export script
        var scriptPath = Path.Combine(outputFolder, "script.md");
        File.WriteAllText(scriptPath, $"# {context.Title}\n\n{context.OriginalScript}");

        // Export scenes
        var scenesPath = Path.Combine(outputFolder, "scenes.md");
        File.WriteAllText(scenesPath, ExportScenes(context.Scenes));

        // Export agent log
        var logPath = Path.Combine(outputFolder, "agent-log.md");
        File.WriteAllText(logPath, ExportAgentLog(context));

        return outputFolder;
    }

    /// <summary>
    /// Exports scenes to markdown format.
    /// </summary>
    /// <param name="scenes">The list of scenes.</param>
    /// <returns>Markdown formatted scenes.</returns>
    private static string ExportScenes(List<Scene> scenes)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine("# Scenes\n\n");

        foreach (var scene in scenes)
        {
            md.AppendLine($"## {scene.Id}: {scene.Title}\n");
            md.AppendLine($"**Location:** {scene.Location}\n");
            md.AppendLine($"**Time:** {scene.Time}\n");
            md.AppendLine($"**Characters:** {string.Join(", ", scene.Characters)}\n");
            md.AppendLine($"**Description:**\n\n{scene.Description}\n");

            if (!string.IsNullOrEmpty(scene.Notes))
            {
                md.AppendLine($"**Notes:** {scene.Notes}\n");
            }

            md.AppendLine("---\n");
        }

        return md.ToString();
    }

    /// <summary>
    /// Exports agent execution log to markdown.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>Markdown formatted agent log.</returns>
    private static string ExportAgentLog(ScriptToMediaContext context)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine($"# Agent Execution Log\n\n");
        md.AppendLine($"**Title:** {context.Title}\n");
        md.AppendLine($"**Context ID:** {context.Id}\n");
        md.AppendLine($"**Created:** {context.CreatedAt:yyyy-MM-dd HH:mm:ss}\n");
        md.AppendLine($"**Updated:** {context.UpdatedAt:yyyy-MM-dd HH:mm:ss}\n\n");

        md.AppendLine("## Pipeline Status\n\n");
        md.AppendLine($"- **Current Stage:** {context.CurrentStage}\n");
        md.AppendLine($"- **Is Complete:** {context.IsComplete}\n");
        md.AppendLine($"- **Has Failed:** {context.HasFailed}\n");
        md.AppendLine($"- **Total Retries:** {context.TotalRetryCount}\n\n");

        md.AppendLine("## Stage Details\n\n");

        foreach (var stageState in context.StageStates)
        {
            var state = stageState.Value;
            var icon = state.IsComplete ? "✅" : state.HasFailed ? "❌" : "⏳";

            md.AppendLine($"### {icon} {state.StageName}\n");
            md.AppendLine($"- **Status:** {(state.IsComplete ? "Complete" : state.HasFailed ? "Failed" : "In Progress")}\n");
            md.AppendLine($"- **Retries:** {state.RetryCount}/{state.MaxRetries}\n");

            if (state.StartedAt.HasValue)
            {
                md.AppendLine($"- **Started:** {state.StartedAt:yyyy-MM-dd HH:mm:ss}\n");
            }

            if (state.CompletedAt.HasValue)
            {
                md.AppendLine($"- **Completed:** {state.CompletedAt:yyyy-MM-dd HH:mm:ss}\n");
            }

            if (state.ExecutionTime.HasValue)
            {
                md.AppendLine($"- **Execution Time:** {state.ExecutionTime.Value.TotalSeconds:F2}s\n");
            }

            if (state.ValidationErrors.Any())
            {
                md.AppendLine($"- **Errors:**\n");
                foreach (var error in state.ValidationErrors)
                {
                    md.AppendLine($"  - {error}\n");
                }
            }

            if (!string.IsNullOrEmpty(state.Feedback))
            {
                md.AppendLine($"- **Feedback:** {state.Feedback}\n");
            }

            md.AppendLine("---\n");
        }

        md.AppendLine("## Content Summary\n\n");
        md.AppendLine($"- **Original Script:** {context.OriginalScript.Length} characters\n");
        md.AppendLine($"- **Scenes:** {context.Scenes.Count}\n");
        md.AppendLine($"- **Photo Prompts:** {context.PhotoPrompts.Count}\n");
        md.AppendLine($"- **Video Prompts:** {context.VideoPrompts.Count}\n");

        return md.ToString();
    }

    /// <summary>
    /// Sanitizes a filename by removing invalid characters.
    /// </summary>
    /// <param name="name">The name to sanitize.</param>
    /// <returns>A sanitized filename.</returns>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
