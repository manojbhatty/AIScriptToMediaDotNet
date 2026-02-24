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
    /// <param name="outputPath">The output directory path (already created by ScriptToMediaService).</param>
    /// <returns>Path to the created output folder.</returns>
    public static string Export(ScriptToMediaContext context, string outputPath)
    {
        // Use the output folder from context if available, otherwise create one
        var outputFolder = !string.IsNullOrEmpty(context.OutputPath) && Directory.Exists(context.OutputPath)
            ? context.OutputPath
            : CreateOutputFolder(context, outputPath);

        // Export script
        var scriptPath = Path.Combine(outputFolder, "script.md");
        File.WriteAllText(scriptPath, $"# {context.Title}\n\n{context.OriginalScript}");

        // Export scenes
        var scenesPath = Path.Combine(outputFolder, "scenes.md");
        File.WriteAllText(scenesPath, ExportScenes(context.Scenes));

        // Export photo prompts
        var photoPromptsPath = Path.Combine(outputFolder, "photo-prompts.md");
        File.WriteAllText(photoPromptsPath, ExportPhotoPrompts(context.PhotoPrompts));

        // Export video prompts
        var videoPromptsPath = Path.Combine(outputFolder, "video-prompts.md");
        File.WriteAllText(videoPromptsPath, ExportVideoPrompts(context.VideoPrompts));

        // Export generated images
        if (context.GeneratedImages.Any())
        {
            var imagesPath = Path.Combine(outputFolder, "generated-images.md");
            File.WriteAllText(imagesPath, ExportGeneratedImages(context.GeneratedImages));
        }

        // Export agent log
        var logPath = Path.Combine(outputFolder, "agent-log.md");
        File.WriteAllText(logPath, ExportAgentLog(context));

        return outputFolder;
    }

    /// <summary>
    /// Creates a new output folder with timestamp.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="outputPath">The base output path.</param>
    /// <returns>The path to the created folder.</returns>
    private static string CreateOutputFolder(ScriptToMediaContext context, string outputPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var folderName = $"{SanitizeFileName(context.Title)}_{timestamp}";
        var outputFolder = Path.Combine(outputPath, folderName);
        Directory.CreateDirectory(outputFolder);
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
    /// Exports photo prompts to markdown format.
    /// </summary>
    /// <param name="photoPrompts">The list of photo prompts.</param>
    /// <returns>Markdown formatted photo prompts.</returns>
    private static string ExportPhotoPrompts(List<PhotoPrompt> photoPrompts)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine("# Photo Prompts\n\n");
        md.AppendLine($"**Total Prompts:** {photoPrompts.Count}\n\n");

        // Group by scene
        var promptsByScene = photoPrompts.GroupBy(p => p.SceneId).OrderBy(g => g.Key);

        foreach (var sceneGroup in promptsByScene)
        {
            md.AppendLine($"## Scene {sceneGroup.Key}\n\n");

            foreach (var prompt in sceneGroup)
            {
                md.AppendLine($"### {prompt.Id}\n\n");
                md.AppendLine($"**Full Prompt:**\n\n{prompt.Prompt}\n\n");
                
                md.AppendLine("**Details:**\n\n");
                md.AppendLine($"- **Subject:** {prompt.Subject}\n");
                md.AppendLine($"- **Style:** {prompt.Style}\n");
                md.AppendLine($"- **Lighting:** {prompt.Lighting}\n");
                md.AppendLine($"- **Composition:** {prompt.Composition}\n");
                md.AppendLine($"- **Mood:** {prompt.Mood}\n");
                md.AppendLine($"- **Camera:** {prompt.Camera}\n");

                if (!string.IsNullOrEmpty(prompt.ScriptExcerpt))
                {
                    md.AppendLine($"- **Script Excerpt:** {prompt.ScriptExcerpt}\n");
                }

                if (!string.IsNullOrEmpty(prompt.NegativePrompt))
                {
                    md.AppendLine($"- **Negative Prompt:** {prompt.NegativePrompt}\n");
                }

                md.AppendLine("---\n\n");
            }
        }

        return md.ToString();
    }

    /// <summary>
    /// Exports video prompts to markdown format.
    /// </summary>
    /// <param name="videoPrompts">The list of video prompts.</param>
    /// <returns>Markdown formatted video prompts.</returns>
    private static string ExportVideoPrompts(List<VideoPrompt> videoPrompts)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine("# Video Prompts\n\n");
        md.AppendLine($"**Total Prompts:** {videoPrompts.Count}\n\n");
        md.AppendLine("**Note:** Video prompts are for reference and planning in v1. No actual video generation.\n\n");

        // Group by scene
        var promptsByScene = videoPrompts.GroupBy(p => p.SceneId).OrderBy(g => g.Key);

        foreach (var sceneGroup in promptsByScene)
        {
            md.AppendLine($"## Scene {sceneGroup.Key}\n\n");

            foreach (var prompt in sceneGroup)
            {
                md.AppendLine($"### {prompt.Id}\n\n");
                md.AppendLine($"**Full Prompt:**\n\n{prompt.Prompt}\n\n");
                
                md.AppendLine("**Details:**\n\n");
                md.AppendLine($"- **Motion:** {prompt.Motion}\n");
                md.AppendLine($"- **Camera Movement:** {prompt.CameraMovement}\n");
                md.AppendLine($"- **Duration:** {prompt.DurationSeconds} seconds\n");

                if (!string.IsNullOrEmpty(prompt.Transition))
                {
                    md.AppendLine($"- **Transition:** {prompt.Transition}\n");
                }

                if (!string.IsNullOrEmpty(prompt.ScriptExcerpt))
                {
                    md.AppendLine($"- **Script Excerpt:** {prompt.ScriptExcerpt}\n");
                }

                md.AppendLine("---\n\n");
            }
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
        md.AppendLine($"- **Generated Images:** {context.GeneratedImages.Count}\n");

        return md.ToString();
    }

    /// <summary>
    /// Exports generated images to markdown format.
    /// </summary>
    /// <param name="images">The list of generated images.</param>
    /// <returns>Markdown string.</returns>
    private static string ExportGeneratedImages(List<GeneratedImage> images)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine("# Generated Images\n\n");
        md.AppendLine($"**Total Images:** {images.Count}\n\n");

        var successful = images.Where(i => i.Success).ToList();
        var failed = images.Where(i => !i.Success).ToList();

        if (successful.Any())
        {
            md.AppendLine("## Successful Generations\n\n");
            foreach (var image in successful)
            {
                md.AppendLine($"### {image.PromptId} (Scene {image.SceneId})\n\n");
                md.AppendLine($"- **File:** `{image.FilePath}`\n");
                md.AppendLine($"- **Generated:** {image.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n\n");
            }
        }

        if (failed.Any())
        {
            md.AppendLine("## Failed Generations\n\n");
            foreach (var image in failed)
            {
                md.AppendLine($"### {image.PromptId} (Scene {image.SceneId})\n\n");
                md.AppendLine($"- **Error:** {image.ErrorMessage}\n\n");
            }
        }

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
