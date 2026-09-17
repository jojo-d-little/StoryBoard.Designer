namespace StoryboardDesigner.App.Services;

public sealed class AutoSaveBeforeGameStateInitResponse
{
    public bool Confirmed { get; init; }
    public bool ShouldAutoSave { get; init; }
    public bool DoNotAskAgainThisRun { get; init; }
}
