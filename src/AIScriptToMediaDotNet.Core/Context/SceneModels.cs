using System.Text.Json.Serialization;

namespace AIScriptToMediaDotNet.Core.Context;

/// <summary>
/// Represents a single scene parsed from a script.
/// </summary>
public class Scene
{
    /// <summary>
    /// Gets or sets the scene identifier (e.g., "SCENE-001").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scene title or slug line.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scene description (action, setting, mood).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the location of the scene.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time of day (e.g., "DAY", "NIGHT", "DAWN").
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of characters in the scene.
    /// </summary>
    public List<string> Characters { get; set; } = new();

    /// <summary>
    /// Gets or sets optional notes about the scene.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Creates a string representation of the scene.
    /// </summary>
    /// <returns>A formatted scene string.</returns>
    public override string ToString()
    {
        return $"[{Id}] {Title} - {Location} ({Time})";
    }
}

/// <summary>
/// Represents a photo (image) generation prompt for a scene.
/// </summary>
public class PhotoPrompt
{
    /// <summary>
    /// Gets or sets the prompt identifier (e.g., "PROMPT-001").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scene ID this prompt is for.
    /// </summary>
    public string SceneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed image generation prompt.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject description.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the style description (e.g., "cinematic", "photorealistic").
    /// </summary>
    public string Style { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lighting description.
    /// </summary>
    public string Lighting { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the composition details.
    /// </summary>
    public string Composition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the mood/atmosphere.
    /// </summary>
    public string Mood { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets camera details (angle, lens, etc.).
    /// </summary>
    public string Camera { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the script excerpt this prompt is based on.
    /// </summary>
    public string? ScriptExcerpt { get; set; }

    /// <summary>
    /// Gets or sets negative prompts (what to avoid).
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Creates a string representation of the photo prompt.
    /// </summary>
    /// <returns>A formatted prompt string.</returns>
    public override string ToString()
    {
        return $"[{Id}] Scene {SceneId}: {Prompt[..Math.Min(50, Prompt.Length)]}...";
    }
}

/// <summary>
/// Represents a video generation prompt for a scene.
/// </summary>
public class VideoPrompt
{
    /// <summary>
    /// Gets or sets the prompt identifier (e.g., "VPROMPT-001").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scene ID this prompt is for.
    /// </summary>
    public string SceneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed video generation prompt.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the motion description.
    /// </summary>
    public string Motion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the camera movement details.
    /// </summary>
    public string CameraMovement { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets transition details.
    /// </summary>
    public string? Transition { get; set; }

    /// <summary>
    /// Gets or sets the script excerpt this prompt is based on.
    /// </summary>
    public string? ScriptExcerpt { get; set; }

    /// <summary>
    /// Creates a string representation of the video prompt.
    /// </summary>
    /// <returns>A formatted prompt string.</returns>
    public override string ToString()
    {
        return $"[{Id}] Scene {SceneId}: {Prompt[..Math.Min(50, Prompt.Length)]}...";
    }
}

/// <summary>
/// Represents a generated image from ComfyUI.
/// </summary>
public class GeneratedImage
{
    /// <summary>
    /// Gets or sets the prompt ID this image was generated from.
    /// </summary>
    public string PromptId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scene ID this image is for.
    /// </summary>
    public string SceneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the photo prompt ID.
    /// </summary>
    public string PhotoPromptId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path to the generated image.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a string representation of the generated image.
    /// </summary>
    /// <returns>A formatted image string.</returns>
    public override string ToString()
    {
        return $"[{PromptId}] Scene {SceneId}: {FilePath} - {(Success ? "Success" : $"Failed: {ErrorMessage}")}";
    }
}
