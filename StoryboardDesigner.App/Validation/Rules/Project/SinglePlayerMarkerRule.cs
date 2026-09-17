using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SinglePlayerMarkerRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-004",
        Title: "Single Player Marker",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var markedObjects = EnumerateCandidateObjects(context.Project)
            .Where(static candidate => candidate.Object.Variables.Any(IsPlayerMarkerTrue))
            .ToList();

        if (markedObjects.Count == 1)
        {
            var markedPlayer = markedObjects[0];
            if (!markedPlayer.Object.IsContainer)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    markedPlayer.Path,
                    $"Object '{NormalizeName(markedPlayer.Object.Name, "GameObject")}' is marked isPlayer=true but is not configured as a container. Player object must be a container to support inventory pickup flow.");
                yield break;
            }

            if (markedPlayer.Object.ContainerPointsDefaultValue < 1)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    markedPlayer.Path,
                    $"Object '{NormalizeName(markedPlayer.Object.Name, "GameObject")}' is marked isPlayer=true but has invalid container capacity '{markedPlayer.Object.ContainerPointsDefaultValue}'. Player container capacity must be greater than zero.");
                yield break;
            }

            yield break;
        }

        if (markedObjects.Count == 0)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project",
                "No game object is marked as player. Exactly one object variable 'isPlayer' must default to true.");
            yield break;
        }

        var markedPaths = markedObjects
            .Select(static candidate => candidate.Path)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            "Project",
            $"Multiple game objects are marked as player ({string.Join(", ", markedPaths)}). Exactly one object variable 'isPlayer' must default to true.");
    }

    private static IEnumerable<(GameObject Object, string Path)> EnumerateCandidateObjects(ProjectModel project)
    {
        foreach (var gameObject in project.GlobalScope.GameObjects)
        {
            foreach (var candidate in EnumerateObjectTree(gameObject, "Global"))
            {
                yield return candidate;
            }
        }

        foreach (var gameObject in project.ObjectTemplates)
        {
            foreach (var candidate in EnumerateObjectTree(gameObject, "Global / Templates"))
            {
                yield return candidate;
            }
        }

        foreach (var gameObject in project.BaseObjects)
        {
            foreach (var candidate in EnumerateObjectTree(gameObject, "Global / BaseObjects"))
            {
                yield return candidate;
            }
        }

        foreach (var planet in project.Planets)
        {
            var planetPath = $"Global / {NormalizeName(planet.Name, "Planet")}";

            foreach (var gameObject in planet.BaseObjects)
            {
                foreach (var candidate in EnumerateObjectTree(gameObject, planetPath + " / BaseObjects"))
                {
                    yield return candidate;
                }
            }

            foreach (var country in planet.Countries)
            {
                var countryPath = planetPath + $" / {NormalizeName(country.Name, "Country")}";

                foreach (var gameObject in country.BaseObjects)
                {
                    foreach (var candidate in EnumerateObjectTree(gameObject, countryPath + " / BaseObjects"))
                    {
                        yield return candidate;
                    }
                }

                foreach (var area in country.Areas)
                {
                    var areaPath = countryPath + $" / {NormalizeName(area.Name, "Area")}";

                    foreach (var gameObject in area.BaseObjects)
                    {
                        foreach (var candidate in EnumerateObjectTree(gameObject, areaPath + " / BaseObjects"))
                        {
                            yield return candidate;
                        }
                    }

                    foreach (var room in area.Rooms)
                    {
                        var roomPath = areaPath + $" / {NormalizeName(room.Name, "Room")}";
                        foreach (var gameObject in room.GameObjects)
                        {
                            foreach (var candidate in EnumerateObjectTree(gameObject, roomPath))
                            {
                                yield return candidate;
                            }
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<(GameObject Object, string Path)> EnumerateObjectTree(GameObject root, string parentPath)
    {
        var objectName = NormalizeName(root.Name, "GameObject");
        var objectPath = parentPath + " / " + objectName;
        yield return (root, objectPath);

        foreach (var child in root.ContainedObjects)
        {
            foreach (var nested in EnumerateObjectTree(child, objectPath))
            {
                yield return nested;
            }
        }
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static bool IsPlayerMarkerTrue(GamePropertyDefinition variable)
    {
        return string.Equals(variable.Name?.Trim(), "isPlayer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(variable.DefaultValue?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }
}
