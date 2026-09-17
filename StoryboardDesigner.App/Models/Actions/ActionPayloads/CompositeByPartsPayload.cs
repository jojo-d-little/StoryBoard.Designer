using System;
using System.Collections.Generic;
using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Models;

public sealed record CompositeByPartsPayload(
    Guid? CompositeTargetObjectId,
    Guid? CompositeRecipeId,
    IReadOnlyList<Guid> CompositeRequiredPartObjectIds,
    bool? CompositeStrictPartCountEnforcement,
    int? CompositeMinimumRequiredPartCount,
    string CompositeMatchMode,
    string CompositeAmbiguityPolicy,
    string CompositePartConsumptionMode,
    string CompositeResolvedTargetOutputTemplate) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.BuildCompositeByParts;

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
