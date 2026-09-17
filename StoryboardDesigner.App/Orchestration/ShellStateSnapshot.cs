namespace StoryboardDesigner.App.Orchestration;

public sealed record ShellStateSnapshot(
    string WindowTitle,
    string StatusMessage,
    string ProjectSummary,
    bool IsBusy,
    string? ActiveWorkflow,
    DateTimeOffset CapturedAtUtc)
{
    public static ShellStateSnapshot CreateDefault()
    {
        return new ShellStateSnapshot(
            WindowTitle: "Storyboard Designer - No Project Loaded",
            StatusMessage: "Ready",
            ProjectSummary: "No project loaded",
            IsBusy: false,
            ActiveWorkflow: null,
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }
}