using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Models;

public sealed record AddObjectPresentationCuePayload(Guid? ObjectId, string EffectKey) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.AddObjectPresentationCue;
    public IReadOnlyList<string> GetReferenceTokens() => RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record RemoveObjectPresentationCuePayload(Guid? ObjectId, string EffectKey) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.RemoveObjectPresentationCue;
    public IReadOnlyList<string> GetReferenceTokens() => RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}

public sealed record ClearObjectPresentationCuesPayload(Guid? ObjectId) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.ClearObjectPresentationCues;
    public IReadOnlyList<string> GetReferenceTokens() => RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields() => [];
}
