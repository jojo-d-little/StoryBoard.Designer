namespace StoryboardDesigner.App.Services;

public sealed class ValidationRunDialogResult
{
    public ValidationRunScopeOption Scope { get; set; } = ValidationRunScopeOption.FromHere;

    public ValidationCompletionMode Completion { get; set; } = ValidationCompletionMode.FullReport;
}

