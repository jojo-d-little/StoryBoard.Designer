using System.Text.Json;
using System.Text.Json.Nodes;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Services;

internal static class CaptureFootprintMatchModeJsonNormalizer
{
    public static string MarkMissingOrUnsupportedAsInvalid(string json)
    {
        if (!json.Contains("captureObjectsOnObjectFootprint", StringComparison.OrdinalIgnoreCase))
        {
            return json;
        }

        var root = JsonNode.Parse(json);
        if (root is null) return json;
        var changed = false;
        Visit(root, ref changed);
        return changed ? root.ToJsonString() : json;
    }

    private static void Visit(JsonNode node, ref bool changed)
    {
        if (node is JsonObject obj)
        {
            var payloadType = obj.FirstOrDefault(static pair => string.Equals(pair.Key, "$payloadType", StringComparison.OrdinalIgnoreCase)).Value;
            if (payloadType is JsonValue typeValue
                && typeValue.TryGetValue<string>(out var typeName)
                && string.Equals(typeName, "captureObjectsOnObjectFootprint", StringComparison.OrdinalIgnoreCase))
            {
                var modeProperty = obj.FirstOrDefault(static pair => string.Equals(pair.Key, "footprintMatchMode", StringComparison.OrdinalIgnoreCase));
                if (modeProperty.Key is null || !IsSupportedMode(modeProperty.Value))
                {
                    obj[modeProperty.Key ?? "footprintMatchMode"] = -1;
                    changed = true;
                }
            }

            foreach (var child in obj.ToList())
            {
                if (child.Value is not null) Visit(child.Value, ref changed);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null) Visit(child, ref changed);
            }
        }
    }

    private static bool IsSupportedMode(JsonNode? node)
    {
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<string>(out var name))
        {
            return Enum.TryParse<FootprintMatchMode>(name, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed);
        }

        if (value.TryGetValue<int>(out var numeric))
        {
            return Enum.IsDefined((FootprintMatchMode)numeric);
        }

        return false;
    }
}
