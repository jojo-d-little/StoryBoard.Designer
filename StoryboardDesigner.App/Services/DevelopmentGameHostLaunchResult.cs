namespace StoryboardDesigner.App.Services;

/// <summary>
/// Reports the result and downstream context of a Designer development GameHost launch.
/// </summary>
public sealed class DevelopmentGameHostLaunchResult
{
    /// <summary>
    /// Gets whether the host reached readiness.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the concise user-facing result message.
    /// </summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets the diagnostics collected while creating or starting the host.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the exact non-secret process launch details emitted to the Designer output console.
    /// </summary>
    public IReadOnlyList<string> LaunchDiagnostics { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the host base URI after readiness succeeds.
    /// </summary>
    public Uri? HostUri { get; init; }

    /// <summary>
    /// Gets the browser URI for the host-served WebPortal bundle.
    /// </summary>
    public Uri? BrowserUri { get; init; }

    /// <summary>
    /// Gets the development username encoded into the WebPortal bootstrap URI.
    /// </summary>
    public string? DevelopmentUsername { get; init; }

    /// <summary>
    /// Gets the invocation-scoped registration file path.
    /// </summary>
    public string? RegistrationFilePath { get; init; }

    /// <summary>
    /// Gets the deterministic development game identifier.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Gets the deterministic development game key.
    /// </summary>
    public string? GameKey { get; init; }

}
