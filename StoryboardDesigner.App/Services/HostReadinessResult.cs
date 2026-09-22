namespace StoryboardDesigner.App.Services;

/// <summary>
/// Reports whether a GameHost health endpoint has become ready for a development launch.
/// </summary>
public sealed class HostReadinessResult
{
    /// <summary>
    /// Gets whether the host returned HTTP success and a ready registration source.
    /// </summary>
    public bool IsReady { get; init; }

    /// <summary>
    /// Gets the last actionable readiness detail.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
