namespace StoryboardDesigner.App.Services;

public sealed class ValidationRunDialogRequest
{
    public string ContextNodeLabel { get; set; } = string.Empty;

    public ValidationRunScopeOption InitialScope { get; set; } = ValidationRunScopeOption.FromHere;

    public ValidationCompletionMode InitialCompletion { get; set; } = ValidationCompletionMode.FullReport;
}

