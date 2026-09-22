namespace StoryboardDesigner.App.Services;

/// <summary>
/// Describes the exported project that should be launched by a development GameHost.
/// </summary>
public sealed class DevelopmentGameHostLaunchRequest
{
    /// <summary>
    /// Gets or initializes the saved Designer project file path.
    /// </summary>
    public string ProjectFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the absolute runtime export path.
    /// </summary>
    public string RuntimeProjectPath { get; init; } = string.Empty;
}
