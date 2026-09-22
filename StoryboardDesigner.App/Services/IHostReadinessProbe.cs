namespace StoryboardDesigner.App.Services;

/// <summary>
/// Polls the existing GameHost health endpoint without introducing a shared contract.
/// </summary>
public interface IHostReadinessProbe
{
    /// <summary>
    /// Waits for a host to report a ready runtime registration source.
    /// </summary>
    /// <param name="hostUri">The host base URI.</param>
    /// <param name="isProcessExited">Returns whether the child host has exited.</param>
    /// <param name="timeout">The maximum readiness wait.</param>
    /// <param name="cancellationToken">Cancels the readiness wait.</param>
    /// <returns>The readiness result.</returns>
    Task<HostReadinessResult> WaitUntilReadyAsync(
        Uri hostUri,
        Func<bool> isProcessExited,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
