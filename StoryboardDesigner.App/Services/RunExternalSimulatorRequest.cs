namespace StoryboardDesigner.App.Services;

/// <summary>Describes a request to launch Simulator with a runtime export.</summary>
public sealed class RunExternalSimulatorRequest
{
    /// <summary>Gets or initializes the authoring project file path.</summary>
    public string ProjectFilePath { get; init; } = string.Empty;
    /// <summary>Gets or initializes the successfully exported runtime project path.</summary>
    public string? RuntimeProjectPath { get; init; }
    /// <summary>Gets or initializes an optional replay file path.</summary>
    public string? ReplayFilePath { get; init; }
    /// <summary>Gets or initializes the optional replay speed.</summary>
    public double? ReplaySpeed { get; init; }
}
