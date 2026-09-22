namespace StoryboardDesigner.App.Services;

/// <summary>
/// Minimal process lifecycle boundary owned by the Designer launch workflow.
/// </summary>
public interface IManagedProcess
{
    /// <summary>
    /// Gets the operating-system process identifier when available.
    /// </summary>
    int? ProcessId { get; }

    /// <summary>
    /// Gets whether the child process has exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Terminates the child process and its process tree when supported.
    /// </summary>
    void Kill();
}
