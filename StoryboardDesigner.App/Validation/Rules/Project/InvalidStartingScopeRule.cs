using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class InvalidStartingScopeRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global,
            ScopeNodeKind.Planet,
            ScopeNodeKind.Country,
            ScopeNodeKind.Area
        };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-002",
        Title: "Invalid Starting Scope",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        var project = context.Project;
        ArgumentNullException.ThrowIfNull(project);

        switch (context.CandidateNode)
        {
            case ProjectModel:
                if (project.Planets.Count == 0)
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        "Project",
                        "Project has no planets. Add at least one planet.");
                    yield break;
                }

                var startingPlanet = project.Planets.FirstOrDefault(planet =>
                    string.Equals(planet.Name, project.StartingPlanetName, StringComparison.OrdinalIgnoreCase));
                if (startingPlanet is null)
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        "Project",
                        $"Starting planet '{project.StartingPlanetName}' was not found.");
                }

                yield break;

            case Planet planet:
                if (planet.Countries.Count > 0)
                {
                    var startingCountry = planet.Countries.FirstOrDefault(country =>
                        string.Equals(country.Name, planet.StartingCountryName, StringComparison.OrdinalIgnoreCase));
                    if (startingCountry is null)
                    {
                        yield return new ValidationIssue(
                            Metadata.RuleId,
                            Metadata.DefaultSeverity,
                            BuildPlanetPath(project, planet),
                            $"Planet '{planet.Name}' has invalid starting country '{planet.StartingCountryName}'.");
                    }
                }

                yield break;

            case Country country:
                if (country.Areas.Count > 0)
                {
                    var startingArea = country.Areas.FirstOrDefault(area =>
                        string.Equals(area.Name, country.StartingAreaName, StringComparison.OrdinalIgnoreCase));
                    if (startingArea is null)
                    {
                        yield return new ValidationIssue(
                            Metadata.RuleId,
                            Metadata.DefaultSeverity,
                            BuildCountryPath(project, country),
                            $"Country '{country.Name}' has invalid starting area '{country.StartingAreaName}'.");
                    }
                }

                yield break;

            case Area area:
                if (area.Rooms.Count > 0 && (!area.StartingRoomId.HasValue || area.Rooms.All(room => room.Id != area.StartingRoomId.Value)))
                {
                    var startingRoomId = area.StartingRoomId?.ToString() ?? "(null)";
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        BuildAreaPath(project, area),
                        $"Area '{area.Name}' has invalid starting room id '{startingRoomId}'.");
                }

                yield break;
        }
    }

    private static string BuildPlanetPath(ProjectModel project, Planet planet)
    {
        if (project.Planets.Any(candidate => ReferenceEquals(candidate, planet) || candidate.Id == planet.Id))
        {
            return $"Global / {planet.Name}";
        }

        return $"Global / {planet.Name}";
    }

    private static string BuildCountryPath(ProjectModel project, Country country)
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

    private static string BuildAreaPath(ProjectModel project, Area area)
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
}
