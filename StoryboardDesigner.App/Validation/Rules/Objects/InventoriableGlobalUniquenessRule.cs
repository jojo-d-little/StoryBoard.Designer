using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class InventoriableGlobalUniquenessRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-002",
        Title: "Inventoriable Global Uniqueness",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var inventoriableLocations = BuildInventoriableLocations(context.Project);
        foreach (var duplicate in inventoriableLocations.Where(static pair => pair.Value.Count > 1))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project",
                $"inventoriable object name '{duplicate.Key}' must be globally unique. Locations: {string.Join("; ", duplicate.Value)}.");
        }
    }

    private static Dictionary<string, List<string>> BuildInventoriableLocations(ProjectModel project)
    {
        var inventoriableLocations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void TrackObject(GameObject gameObject, string parentPath)
        {
            var objectName = gameObject.Name?.Trim() ?? string.Empty;
            var isLinkedRoomInstance = gameObject.IsRoomInstance;
            if (isLinkedRoomInstance)
            {
                // Linked room instances represent the same authored object across placements.
                // Exclude the entire linked subtree from global uniqueness counting.
                return;
            }

            if (gameObject.IsInventoriable && !string.IsNullOrWhiteSpace(objectName))
            {
                if (!inventoriableLocations.TryGetValue(objectName, out var entries))
                {
                    entries = new List<string>();
                    inventoriableLocations.Add(objectName, entries);
                }

                entries.Add($"{parentPath} / {objectName}");
            }

            foreach (var child in gameObject.ContainedObjects)
            {
                TrackObject(child, $"{parentPath} / {objectName}");
            }
        }

        foreach (var globalObject in project.GlobalScope.GameObjects)
        {
            TrackObject(globalObject, "Global");
        }

        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        var roomPath = $"{planet.Name} / {country.Name} / {area.Name} / {room.Name}";
                        foreach (var roomObject in room.GameObjects)
                        {
                            TrackObject(roomObject, roomPath);
                        }
                    }
                }
            }
        }

        return inventoriableLocations;
    }
}
