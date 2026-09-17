namespace StoryboardDesigner.App.Models;

public sealed class EventBindingTargetDefinition
{
    public string ActionName { get; set; } = string.Empty;

    public string OnMissingAction { get; set; } = "DiagnosticOnly";

    public bool StopChainOnFailure { get; set; } = true;
}
