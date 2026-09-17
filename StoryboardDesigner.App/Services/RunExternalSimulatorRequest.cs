namespace StoryboardDesigner.App.Services;

public sealed class RunExternalSimulatorRequest
{
    public string ProjectFilePath { get; init; } = string.Empty;
    public string? ReplayFilePath { get; init; }
    public double? ReplaySpeed { get; init; }
}
