using System.Text;

namespace AIScriptToMediaDotNet.Core.Logging;

/// <summary>
/// Exports pipeline execution context to detailed markdown log.
/// </summary>
public static class ExecutionLogExporter
{
    /// <summary>
    /// Exports the execution context to a markdown file.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="outputPath">The output file path.</param>
    public static void Export(PipelineExecutionContext context, string outputPath)
    {
        var md = new StringBuilder();

        // Header
        md.AppendLine($"# Pipeline Execution Log\n\n");
        md.AppendLine($"**Execution ID:** {context.ExecutionId}\n\n");

        // Summary
        md.AppendLine("## Summary\n\n");
        md.AppendLine($"- **Title:** {context.Title}\n");
        md.AppendLine($"- **Status:** {GetStatusEmoji(context.Status)} {context.Status}\n");
        md.AppendLine($"- **Started:** {context.StartTime:yyyy-MM-dd HH:mm:ss}\n");
        if (context.EndTime.HasValue)
        {
            md.AppendLine($"- **Ended:** {context.EndTime.Value:yyyy-MM-dd HH:mm:ss}\n");
            md.AppendLine($"- **Duration:** {(context.EndTime.Value - context.StartTime).TotalSeconds:F2}s\n");
        }
        md.AppendLine($"- **Total Log Entries:** {context.LogEntries.Count}\n");
        md.AppendLine($"- **Total Retries:** {context.LogEntries.Count(e => e.Event == "Retry")}\n\n");

        // Configuration
        if (context.ConfigurationSnapshot.Any())
        {
            md.AppendLine("## Configuration\n\n");
            foreach (var config in context.ConfigurationSnapshot)
            {
                md.AppendLine($"- **{config.Key}:** {config.Value}\n");
            }
            md.AppendLine();
        }

        // Input Script
        md.AppendLine("## Input Script\n\n");
        md.AppendLine($"**Length:** {context.FullScript.Length} characters\n\n");
        md.AppendLine("```");
        md.AppendLine(context.ScriptSummary);
        md.AppendLine("```\n\n");

        // Final Error (if failed)
        if (context.Status == "Failed" && !string.IsNullOrEmpty(context.FinalError))
        {
            md.AppendLine("## ❌ Final Error\n\n");
            md.AppendLine($"**Error:** {context.FinalError}\n\n");
            if (!string.IsNullOrEmpty(context.FinalStackTrace))
            {
                md.AppendLine("**Stack Trace:**\n\n");
                md.AppendLine("```\n");
                md.AppendLine(context.FinalStackTrace);
                md.AppendLine("```\n\n");
            }
        }

        // Timeline
        md.AppendLine("## Execution Timeline\n\n");

        var groupedLogs = context.LogEntries.GroupBy(e => e.Stage).ToList();
        foreach (var stageGroup in groupedLogs)
        {
            md.AppendLine($"### {GetStageEmoji(stageGroup.Key)} {stageGroup.Key}\n\n");

            foreach (var entry in stageGroup.OrderBy(e => e.Timestamp))
            {
                var emoji = GetEventEmoji(entry.Event);
                var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");

                md.AppendLine($"#### {emoji} [{timestamp}] {entry.Event}\n\n");
                md.AppendLine($"**Agent:** {entry.Agent}\n\n");
                md.AppendLine($"**Level:** {entry.Level}\n\n");
                md.AppendLine($"{entry.Message}\n\n");

                if (!string.IsNullOrEmpty(entry.InputSummary))
                {
                    md.AppendLine("**Input:**\n\n");
                    md.AppendLine("```\n");
                    md.AppendLine(entry.InputSummary);
                    md.AppendLine("```\n\n");
                }

                if (!string.IsNullOrEmpty(entry.OutputSummary))
                {
                    md.AppendLine("**Output:**\n\n");
                    md.AppendLine("```\n");
                    md.AppendLine(entry.OutputSummary);
                    md.AppendLine("```\n\n");
                }

                if (!string.IsNullOrEmpty(entry.ErrorDetails))
                {
                    md.AppendLine("**Error:**\n\n");
                    md.AppendLine("```\n");
                    md.AppendLine(entry.ErrorDetails);
                    md.AppendLine("```\n\n");
                }

                if (entry.RetryCount.HasValue)
                {
                    md.AppendLine($"**Retry Count:** {entry.RetryCount}\n\n");
                }

                if (entry.ExecutionTimeMs.HasValue)
                {
                    md.AppendLine($"**Execution Time:** {entry.ExecutionTimeMs}ms\n\n");
                }

                if (entry.Metadata.Any())
                {
                    md.AppendLine("**Metadata:**\n\n");
                    foreach (var meta in entry.Metadata.Where(m => !string.IsNullOrEmpty(m.Value)))
                    {
                        md.AppendLine($"- **{meta.Key}:** {Truncate(meta.Value, 500)}\n");
                    }
                    md.AppendLine();
                }

                md.AppendLine("---\n\n");
            }
        }

        // Statistics
        md.AppendLine("## Statistics\n\n");

        var successCount = context.LogEntries.Count(e => e.Event == "Complete");
        var errorCount = context.LogEntries.Count(e => e.Event == "Error");
        var retryCount = context.LogEntries.Count(e => e.Event == "Retry");

        md.AppendLine($"- **Successful Stages:** {successCount}\n");
        md.AppendLine($"- **Failed Stages:** {errorCount}\n");
        md.AppendLine($"- **Retry Attempts:** {retryCount}\n");

        if (context.LogEntries.Any(e => e.ExecutionTimeMs.HasValue))
        {
            var avgTime = context.LogEntries
                .Where(e => e.ExecutionTimeMs.HasValue)
                .Average(e => e.ExecutionTimeMs!.Value);
            var maxTime = context.LogEntries
                .Where(e => e.ExecutionTimeMs.HasValue)
                .Max(e => e.ExecutionTimeMs!.Value);
            var minTime = context.LogEntries
                .Where(e => e.ExecutionTimeMs.HasValue)
                .Min(e => e.ExecutionTimeMs!.Value);

            md.AppendLine($"- **Avg Execution Time:** {avgTime:F0}ms\n");
            md.AppendLine($"- **Max Execution Time:** {maxTime}ms\n");
            md.AppendLine($"- **Min Execution Time:** {minTime}ms\n");
        }

        File.WriteAllText(outputPath, md.ToString());
    }

    private static string GetStatusEmoji(string status)
    {
        return status switch
        {
            "Success" => "✅",
            "Failed" => "❌",
            "Cancelled" => "⛔",
            _ => "⏳"
        };
    }

    private static string GetStageEmoji(string stage)
    {
        if (stage.Contains("Scene", StringComparison.OrdinalIgnoreCase)) return "🎬";
        if (stage.Contains("Photo", StringComparison.OrdinalIgnoreCase)) return "📸";
        if (stage.Contains("Video", StringComparison.OrdinalIgnoreCase)) return "🎥";
        if (stage.Contains("Export", StringComparison.OrdinalIgnoreCase)) return "📤";
        return "⚙️";
    }

    private static string GetEventEmoji(string eventType)
    {
        return eventType switch
        {
            "Start" => "▶️",
            "Complete" => "✅",
            "Retry" => "🔄",
            "Error" => "❌",
            _ => "ℹ️"
        };
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + $"... ({text.Length - maxLength} more chars)";
    }
}
