namespace StoryboardDesigner.App.Services;

public sealed class RunExternalSimulatorResult
{
    public bool Success { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string? LaunchedExecutablePath { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}
