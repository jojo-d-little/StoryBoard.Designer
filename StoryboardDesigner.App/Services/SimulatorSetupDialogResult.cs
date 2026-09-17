namespace StoryboardDesigner.App.Services;

public sealed class SimulatorSetupDialogResult
{
    public bool AppliedChanges { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string ReplayFilePath { get; init; } = string.Empty;
    public double? ReplaySpeed { get; init; }
    public string SimulatorExecutablePath { get; init; } = string.Empty;
}
