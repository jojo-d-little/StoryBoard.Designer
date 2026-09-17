using System;
using System.Linq;

namespace StoryboardDesigner.App.Models;

public static class ActionPayloadSchemaHelpers
{
    public static bool OwnsField(CommandActionType actionType, string fieldName)
    {
        return ActionPayloadSchemaMap.Get(actionType)
            .OwnedFields
            .Contains(fieldName, StringComparer.Ordinal);
    }

    public static bool OwnsAllFields(CommandActionType actionType, params string[] fieldNames)
    {
        return fieldNames.All(field => OwnsField(actionType, field));
    }
}
