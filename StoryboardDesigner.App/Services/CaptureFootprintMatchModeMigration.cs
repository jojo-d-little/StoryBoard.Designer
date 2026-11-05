using System.Text.Json;
using System.Text.Json.Nodes;

namespace StoryboardDesigner.App.Services;

internal static class CaptureFootprintMatchModeMigration
{
    internal sealed record MigrationResult(string Json, int UpdatedPayloadCount);

    public static MigrationResult AddExplicitAnyOverlap(string json)
    {
        var root = JsonNode.Parse(json) ?? throw new JsonException("The project JSON root is empty.");
        var updated = 0;
        Visit(root, ref updated);
        return new MigrationResult(
            updated == 0 ? json : root.ToJsonString(new JsonSerializerOptions { WriteIndented = json.Contains('\n') }),
            updated);
    }

    private static void Visit(JsonNode node, ref int updated)
    {
        if (node is JsonObject obj)
        {
            var discriminator = obj.FirstOrDefault(static pair => string.Equals(pair.Key, "$payloadType", StringComparison.OrdinalIgnoreCase)).Value;
            var isCapturePayload = discriminator is JsonValue discriminatorValue
                && discriminatorValue.TryGetValue<string>(out var discriminatorText)
                && string.Equals(discriminatorText, "captureObjectsOnObjectFootprint", StringComparison.OrdinalIgnoreCase);
            var hasMatchMode = obj.Any(static pair => string.Equals(pair.Key, "footprintMatchMode", StringComparison.OrdinalIgnoreCase));
            if (isCapturePayload && !hasMatchMode)
            {
                obj["footprintMatchMode"] = "AnyOverlap";
                updated++;
            }

            foreach (var child in obj.ToList())
            {
                if (child.Value is not null) Visit(child.Value, ref updated);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null) Visit(child, ref updated);
            }
        }
    }
}
