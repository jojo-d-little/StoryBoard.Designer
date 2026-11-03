namespace StoryboardDesigner.App.Models;

public sealed record AddObjectPresentationCuePayload(Guid? ObjectId, string EffectKey, Storyboard.Shared.DesignerContracts.ProjectObjectSetReferenceDto? ObjectSetReference = null) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.AddObjectPresentationCue;
    public IReadOnlyList<string> GetReferenceTokens() => StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record RemoveObjectPresentationCuePayload(Guid? ObjectId, string EffectKey, Storyboard.Shared.DesignerContracts.ProjectObjectSetReferenceDto? ObjectSetReference = null) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.RemoveObjectPresentationCue;
    public IReadOnlyList<string> GetReferenceTokens() => StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record ClearObjectPresentationCuesPayload(Guid? ObjectId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.ClearObjectPresentationCues;
    public IReadOnlyList<string> GetReferenceTokens() => StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record CaptureObjectsOnObjectFootprintPayload(string ObjectSetKey, Guid? TargetObjectId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CaptureObjectsOnObjectFootprint;
    public IReadOnlyList<string> GetReferenceTokens() => StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record CheckObjectSetContainsObjectPayload(string ObjectSetKey, string Target) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.CheckObjectSetContainsObject;
    public IReadOnlyList<string> GetReferenceTokens() => StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record NavigateObjectSetToAdjacentPayload(string ObjectSetKey, string Direction) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.NavigateObjectSetToAdjacent;
    public IReadOnlyList<string> GetReferenceTokens() => StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}
