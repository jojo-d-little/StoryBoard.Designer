using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class DuplicateGamePropertyNameRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "GPROP-001",
        Title: "Duplicate Game Property Names",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Game Properties");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ProjectModel project)
        {
            yield break;
        }

        foreach (var owner in EnumerateOwners(project))
        {
            var duplicateGroups = owner.Properties
                .Select(static property => property.Name?.Trim() ?? string.Empty)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .Where(static group => group.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                var spellings = group
                    .GroupBy(static name => name, StringComparer.Ordinal)
                    .Select(static spelling => spelling.Count() > 1
                        ? $"'{spelling.Key}' ({spelling.Count()} definitions)"
                        : $"'{spelling.Key}'")
                    .ToList();
                var names = string.Join(", ", spellings);
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    $"{owner.Path}.Game Properties.{group.First()}",
                    $"Game Property names {names} are duplicates in this owner. Game Property names are case-insensitive.",
                    "Remove or rename the duplicate Game Property definition.");
            }
        }
    }

    private static IEnumerable<PropertyOwner> EnumerateOwners(ProjectModel project)
    {
        yield return new PropertyOwner("Global", project.GlobalVariables);
        foreach (var obj in project.GlobalScope.GameObjects)
            foreach (var owner in EnumerateObject(obj, $"Global.Object.{obj.Name}"))
                yield return owner;

        foreach (var obj in project.BaseObjects)
            foreach (var owner in EnumerateObject(obj, $"Base Objects.Object.{obj.Name}"))
                yield return owner;
        foreach (var obj in project.ObjectTemplates)
            foreach (var owner in EnumerateObject(obj, $"Object Templates.Object.{obj.Name}"))
                yield return owner;
        foreach (var room in project.RoomTemplates)
            foreach (var owner in EnumerateRoom(room, $"Room Template.{room.Name}"))
                yield return owner;

        foreach (var planet in project.Planets)
        {
            var planetPath = $"Planet.{planet.Name}";
            yield return new PropertyOwner(planetPath, planet.Variables);
            foreach (var obj in planet.BaseObjects)
                foreach (var owner in EnumerateObject(obj, $"{planetPath}.Base Objects.Object.{obj.Name}"))
                    yield return owner;
            foreach (var obj in planet.ObjectTemplates)
                foreach (var owner in EnumerateObject(obj, $"{planetPath}.Object Templates.Object.{obj.Name}"))
                    yield return owner;
            foreach (var obj in planet.GameObjects)
                foreach (var owner in EnumerateObject(obj, $"{planetPath}.Object.{obj.Name}"))
                    yield return owner;
            foreach (var room in planet.RoomTemplates)
                foreach (var owner in EnumerateRoom(room, $"{planetPath}.Room Template.{room.Name}"))
                    yield return owner;

            foreach (var country in planet.Countries)
            {
                var countryPath = $"{planetPath}.Country.{country.Name}";
                yield return new PropertyOwner(countryPath, country.Variables);
                foreach (var obj in country.BaseObjects)
                    foreach (var owner in EnumerateObject(obj, $"{countryPath}.Base Objects.Object.{obj.Name}"))
                        yield return owner;
                foreach (var obj in country.ObjectTemplates)
                    foreach (var owner in EnumerateObject(obj, $"{countryPath}.Object Templates.Object.{obj.Name}"))
                        yield return owner;
                foreach (var obj in country.GameObjects)
                    foreach (var owner in EnumerateObject(obj, $"{countryPath}.Object.{obj.Name}"))
                        yield return owner;
                foreach (var room in country.RoomTemplates)
                    foreach (var owner in EnumerateRoom(room, $"{countryPath}.Room Template.{room.Name}"))
                        yield return owner;

                foreach (var area in country.Areas)
                {
                    var areaPath = $"{countryPath}.Area.{area.Name}";
                    yield return new PropertyOwner(areaPath, area.Variables);
                    foreach (var obj in area.BaseObjects)
                        foreach (var owner in EnumerateObject(obj, $"{areaPath}.Base Objects.Object.{obj.Name}"))
                            yield return owner;
                    foreach (var obj in area.ObjectTemplates)
                        foreach (var owner in EnumerateObject(obj, $"{areaPath}.Object Templates.Object.{obj.Name}"))
                            yield return owner;
                    foreach (var obj in area.GameObjects)
                        foreach (var owner in EnumerateObject(obj, $"{areaPath}.Object.{obj.Name}"))
                            yield return owner;
                    foreach (var room in area.RoomTemplates)
                        foreach (var owner in EnumerateRoom(room, $"{areaPath}.Room Template.{room.Name}"))
                            yield return owner;

                    foreach (var room in area.Rooms)
                        foreach (var owner in EnumerateRoom(room, $"{areaPath}.Room.{room.Name}"))
                            yield return owner;
                }
            }
        }

        foreach (var phaseBook in project.PhaseBooks)
            foreach (var owner in EnumeratePhase(phaseBook))
                yield return owner;
    }

    private static IEnumerable<PropertyOwner> EnumerateRoom(Room room, string path)
    {
        yield return new PropertyOwner(path, room.Variables);
        foreach (var obj in room.GameObjects)
            foreach (var owner in EnumerateObject(obj, $"{path}.Object.{obj.Name}"))
                yield return owner;
    }

    private static IEnumerable<PropertyOwner> EnumerateObject(GameObject gameObject, string path)
    {
        yield return new PropertyOwner(path, gameObject.Variables);
        foreach (var child in gameObject.ContainedObjects)
            foreach (var owner in EnumerateObject(child, $"{path}.Object.{child.Name}"))
                yield return owner;
    }

    private static IEnumerable<PropertyOwner> EnumeratePhase(PhaseNode phase)
    {
        yield return new PropertyOwner($"Phase.{phase.PhaseKey}", phase.Variables);
        foreach (var child in phase.Children)
            foreach (var owner in EnumeratePhase(child))
                yield return owner;
    }

    private sealed record PropertyOwner(string Path, IReadOnlyList<GamePropertyDefinition> Properties);
}
