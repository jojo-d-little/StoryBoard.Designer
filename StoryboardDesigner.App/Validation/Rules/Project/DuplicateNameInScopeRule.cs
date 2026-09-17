using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class DuplicateNameInScopeRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global,
            ScopeNodeKind.Planet,
            ScopeNodeKind.Country,
            ScopeNodeKind.Area,
            ScopeNodeKind.Room,
            ScopeNodeKind.GameObject
        };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "NAME-001",
        Title: "Duplicate Name In Scope",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Naming");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var project = context.Project;

        switch (context.CandidateNode)
        {
            case ProjectModel:
                foreach (var issue in BuildDuplicateNameIssues(project.GlobalScope.GameObjects, static obj => obj.Name, "global objects", "game object", "Global"))
                {
                    yield return issue;
                }

                foreach (var issue in BuildDuplicateNameIssues(project.Planets, static planet => planet.Name, "project", "planet", "Global"))
                {
                    yield return issue;
                }

                foreach (var issue in BuildDuplicateNameIssues(project.GlobalVariables, static variable => variable.Name, "global scope", "variable", "Global"))
                {
                    yield return issue;
                }

                yield break;

            case Planet planet:
                foreach (var issue in BuildDuplicateNameIssues(planet.Countries, static country => country.Name, $"planet '{planet.Name}'", "country", BuildPlanetPath(planet, project)))
                {
                    yield return issue;
                }

                foreach (var issue in BuildDuplicateNameIssues(planet.Variables, static variable => variable.Name, $"planet '{planet.Name}'", "variable", BuildPlanetPath(planet, project)))
                {
                    yield return issue;
                }

                yield break;

            case Country country:
                var countryPath = BuildCountryPath(country, project);
                foreach (var issue in BuildDuplicateNameIssues(country.Areas, static area => area.Name, $"country '{country.Name}'", "area", countryPath))
                {
                    yield return issue;
                }

                foreach (var issue in BuildDuplicateNameIssues(country.Variables, static variable => variable.Name, $"country '{country.Name}'", "variable", countryPath))
                {
                    yield return issue;
                }

                yield break;

            case Area area:
                var areaPath = BuildAreaPath(area, project);
                foreach (var issue in BuildDuplicateNameIssues(area.Rooms, static room => room.Name, $"area '{area.Name}'", "room", areaPath))
                {
                    yield return issue;
                }

                foreach (var issue in BuildDuplicateNameIssues(area.Variables, static variable => variable.Name, $"area '{area.Name}'", "variable", areaPath))
                {
                    yield return issue;
                }

                yield break;

            case Room room:
                foreach (var issue in BuildRoomObjectDuplicateNameIssues(room, project))
                {
                    yield return issue;
                }

                foreach (var issue in BuildDuplicateNameIssues(room.Variables, static variable => variable.Name, $"room '{room.Name}'", "variable", BuildRoomPath(room, project)))
                {
                    yield return issue;
                }

                yield break;

            case GameObject gameObject:
                foreach (var issue in BuildDuplicateNameIssues(gameObject.Variables, static variable => variable.Name, $"game object '{gameObject.Name}'", "variable", BuildGameObjectPath(gameObject, project)))
                {
                    yield return issue;
                }

                yield break;
        }
    }

    private IEnumerable<ValidationIssue> BuildDuplicateNameIssues<T>(
        IEnumerable<T> items,
        Func<T, string> selector,
        string scopeLabel,
        string itemLabel,
        string path)
    {
        var groups = items
            .Select(item => selector(item)?.Trim() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                path,
                $"Duplicate {itemLabel} name '{group.Key}' in {scopeLabel}.");
        }
    }

    private IEnumerable<ValidationIssue> BuildRoomObjectDuplicateNameIssues(Room room, ProjectModel project)
    {
        var groups = room.GameObjects
            .Where(static obj =>
                !(obj.IsQuantifiable
                  && string.Equals(obj.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)))
            .Select(static obj => obj.Name?.Trim() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                BuildRoomPath(room, project),
                $"Duplicate game object name '{group.Key}' in room '{room.Name}'.");
        }
    }

    private static string BuildPlanetPath(Planet planet, ProjectModel project)
    {
        return project.Planets.Any(candidate => ReferenceEquals(candidate, planet) || candidate.Id == planet.Id)
            ? $"Global / {planet.Name}"
            : $"Global / {planet.Name}";
    }

    private static string BuildCountryPath(Country country, ProjectModel project)
    {
        foreach (var planet in project.Planets)
        {
            if (planet.Countries.Any(candidate => ReferenceEquals(candidate, country) || candidate.Id == country.Id))
            {
                return $"Global / {planet.Name} / {country.Name}";
            }
        }

        return $"Global / {country.Name}";
    }

    private static string BuildAreaPath(Area area, ProjectModel project)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                if (country.Areas.Any(candidate => ReferenceEquals(candidate, area) || candidate.Id == area.Id))
                {
                    return $"Global / {planet.Name} / {country.Name} / {area.Name}";
                }
            }
        }

        return $"Global / {area.Name}";
    }

    private static string BuildRoomPath(Room room, ProjectModel project)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    if (area.Rooms.Any(candidate => ReferenceEquals(candidate, room) || candidate.Id == room.Id))
                    {
                        return $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name}";
                    }
                }
            }
        }

        return $"Global / {room.Name}";
    }

    private static string BuildGameObjectPath(GameObject gameObject, ProjectModel project)
    {
        if (project.GlobalScope.GameObjects.Any(candidate => ReferenceEquals(candidate, gameObject)))
        {
            return $"Global / {gameObject.Name}";
        }

        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        if (room.GameObjects.Any(candidate => ReferenceEquals(candidate, gameObject)))
                        {
                            return $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name} / {gameObject.Name}";
                        }
                    }
                }
            }
        }

        return $"Global / {gameObject.Name}";
    }
}
