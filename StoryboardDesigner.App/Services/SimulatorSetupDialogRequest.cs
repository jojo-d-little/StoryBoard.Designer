namespace StoryboardDesigner.App.Services;

public sealed class SimulatorSetupDialogRequest
{
    public string? ProjectFilePath { get; init; }
    public string ReplayFilePath { get; init; } = string.Empty;
    public double? ReplaySpeed { get; init; }
}
