namespace StoryboardDesigner.App.Services;

/// <summary>
/// Owns Designer-side creation, fresh launch, readiness, and cleanup for a development GameHost.
/// </summary>
public interface IDevelopmentGameHostWorkflowService
{
    /// <summary>
    /// Stops any prior Designer-owned host and starts a fresh development GameHost for the exported project.
    /// </summary>
    /// <param name="request">The saved project and runtime export paths.</param>
    /// <param name="cancellationToken">Cancels the launch and readiness wait.</param>
    /// <returns>The launch result and downstream browser/identity context.</returns>
    Task<DevelopmentGameHostLaunchResult> LaunchAsync(
        DevelopmentGameHostLaunchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the active development host and removes its temporary registration file.
    /// </summary>
    void Stop();
}
