using System.Diagnostics;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Starts child processes for Designer-owned workflows.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Starts a process from the supplied launch information.
    /// </summary>
    /// <param name="startInfo">The process executable, arguments, environment, and working directory.</param>
    /// <returns>A managed child-process handle.</returns>
    IManagedProcess Start(ProcessStartInfo startInfo);
}
