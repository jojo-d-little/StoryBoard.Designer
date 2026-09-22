namespace StoryboardDesigner.App.Services;

public sealed class ProjectCreationPreferences
{
    public string? StarterProjectsRootOverride { get; set; }

    public string LastStarterBrowseFolder { get; set; } = string.Empty;

    public string LastCreateProjectParentFolder { get; set; } = string.Empty;

    public string SimulatorExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional machine-local GameHost executable path for development launches.
    /// </summary>
    public string GameHostExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional machine-local WebPortal distribution folder for development launches.
    /// </summary>
    public string WebPortalRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine-local development username supplied to the WebPortal bootstrap flow.
    /// </summary>
    public string DevelopmentUsername { get; set; } = "dev";
}
