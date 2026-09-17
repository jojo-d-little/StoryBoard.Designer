namespace StoryboardDesigner.App.Services;

public sealed class ProjectCreationPreferences
{
    public string? StarterProjectsRootOverride { get; set; }

    public string LastStarterBrowseFolder { get; set; } = string.Empty;

    public string LastCreateProjectParentFolder { get; set; } = string.Empty;

    public string SimulatorExecutablePath { get; set; } = string.Empty;
}
