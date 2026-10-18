using System;
using System.Collections.Generic;
namespace StoryboardDesigner.App.Models;

public sealed record BreakCompositePayload(
    Guid? CompositeTargetObjectId,
    Guid? CompositeRecipeId,
    IReadOnlyList<Guid> CompositeRequiredPartObjectIds,
    bool? CompositeStrictPartCountEnforcement,
    int? CompositeMinimumRequiredPartCount,
    string CompositePartConsumptionMode) : IActionPayload
{
    public CommandActionType ActionType => CommandActionType.BreakCompositeItem;

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return StoryboardDesigner.App.Services.ActionManifestCatalog.GetOutputVariableTokens(ActionType);
    }

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
