namespace StoryboardDesigner.App.Models;

public sealed record LinkedActionTargetChoice(
    CommandAction Action,
    string ScopeName,
    LinkedActionTargetGroup Group,
    Guid? TargetObjectId = null);
