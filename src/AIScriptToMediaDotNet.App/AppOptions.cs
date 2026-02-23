namespace AIScriptToMediaDotNet.App;

/// <summary>
/// Command-line options for the application.
/// </summary>
public class AppOptions
{
    /// <summary>
    /// Gets or sets the script title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the path to the input script file.
    /// </summary>
    public string? InputFile { get; set; }

    /// <summary>
    /// Gets or sets the script text directly.
    /// </summary>
    public string? ScriptText { get; set; }

    /// <summary>
    /// Gets or sets the output directory path.
    /// </summary>
    public string OutputPath { get; set; } = "./output";

    /// <summary>
    /// Gets or sets a value indicating whether to run in interactive mode.
    /// </summary>
    public bool Interactive { get; set; } = true;
}
