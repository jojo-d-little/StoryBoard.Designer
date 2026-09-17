using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SoundEffectIdentityUniquenessWarningRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-014",
        Title: "Sound Effect Identity Uniqueness",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var seenById = new Dictionary<Guid, SoundEntryLocation>();
        var seenByKey = new Dictionary<string, SoundEntryLocation>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in EnumerateLocations(context.Project))
        {
            var entry = location.Entry;

            if (entry.SoundEffectId != Guid.Empty)
            {
                if (seenById.TryGetValue(entry.SoundEffectId, out var existingById))
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        location.ScopePath,
                        $"Duplicate soundEffectId '{entry.SoundEffectId:D}' appears in {existingById.DisplayPath} and {location.DisplayPath}.",
                        "Assign a new soundEffectId to one entry and keep soundEffectKey values stable for existing references.");
                }
                else
                {
                    seenById.Add(entry.SoundEffectId, location);
                }
            }

            var normalizedKey = NormalizeKey(entry.SoundEffectKey);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            if (seenByKey.TryGetValue(normalizedKey, out var existingByKey))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    location.ScopePath,
                    $"Duplicate soundEffectKey '{normalizedKey}' appears in {existingByKey.DisplayPath} and {location.DisplayPath}.",
                    "Rename one soundEffectKey to a unique value. Keep keys case-insensitively unique across the project.");
            }
            else
            {
                seenByKey.Add(normalizedKey, location);
            }
        }
    }

    private static IEnumerable<SoundEntryLocation> EnumerateLocations(ProjectModel project)
    {
        foreach (var entry in project.GlobalScope.SoundEffectLibraryEntries)
        {
            yield return new SoundEntryLocation("Global", "Global", entry);
        }

        foreach (var template in project.ObjectTemplates)
        {
            foreach (var location in EnumerateGameObjectLocations(template, "Templates / Object Templates"))
            {
                yield return location;
            }
        }

        foreach (var baseObject in project.BaseObjects)
        {
            foreach (var location in EnumerateGameObjectLocations(baseObject, "Templates / Base Objects"))
            {
                yield return location;
            }
        }

        foreach (var roomTemplate in project.RoomTemplates)
        {
            var roomPath = $"Templates / Room Templates / {GetName(roomTemplate.Name, "Room")}";
            foreach (var entry in roomTemplate.SoundEffectLibraryEntries)
            {
                yield return new SoundEntryLocation(roomPath, roomPath, entry);
            }

            foreach (var gameObject in roomTemplate.GameObjects)
            {
                foreach (var location in EnumerateGameObjectLocations(gameObject, roomPath))
                {
                    yield return location;
                }
            }
        }

        foreach (var planet in project.Planets)
        {
            var planetPath = $"Global / {GetName(planet.Name, "Planet")}";
            foreach (var entry in planet.SoundEffectLibraryEntries)
            {
                yield return new SoundEntryLocation(planetPath, planetPath, entry);
            }

            foreach (var gameObject in planet.GameObjects)
            {
                foreach (var location in EnumerateGameObjectLocations(gameObject, planetPath))
                {
                    yield return location;
                }
            }

            foreach (var baseObject in planet.BaseObjects)
            {
                foreach (var location in EnumerateGameObjectLocations(baseObject, planetPath))
                {
                    yield return location;
                }
            }

            foreach (var country in planet.Countries)
            {
                var countryPath = $"{planetPath} / {GetName(country.Name, "Country")}";
                foreach (var entry in country.SoundEffectLibraryEntries)
                {
                    yield return new SoundEntryLocation(countryPath, countryPath, entry);
                }

                foreach (var gameObject in country.GameObjects)
                {
                    foreach (var location in EnumerateGameObjectLocations(gameObject, countryPath))
                    {
                        yield return location;
                    }
                }

                foreach (var baseObject in country.BaseObjects)
                {
                    foreach (var location in EnumerateGameObjectLocations(baseObject, countryPath))
                    {
                        yield return location;
                    }
                }

                foreach (var area in country.Areas)
                {
                    var areaPath = $"{countryPath} / {GetName(area.Name, "Area")}";
                    foreach (var entry in area.SoundEffectLibraryEntries)
                    {
                        yield return new SoundEntryLocation(areaPath, areaPath, entry);
                    }

                    foreach (var gameObject in area.GameObjects)
                    {
                        foreach (var location in EnumerateGameObjectLocations(gameObject, areaPath))
                        {
                            yield return location;
                        }
                    }

                    foreach (var baseObject in area.BaseObjects)
                    {
                        foreach (var location in EnumerateGameObjectLocations(baseObject, areaPath))
                        {
                            yield return location;
                        }
                    }

                    foreach (var room in area.Rooms)
                    {
                        var roomPath = $"{areaPath} / {GetName(room.Name, "Room")}";
                        foreach (var entry in room.SoundEffectLibraryEntries)
                        {
                            yield return new SoundEntryLocation(roomPath, roomPath, entry);
                        }

                        foreach (var gameObject in room.GameObjects)
                        {
                            foreach (var location in EnumerateGameObjectLocations(gameObject, roomPath))
                            {
                                yield return location;
                            }
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<SoundEntryLocation> EnumerateGameObjectLocations(GameObject gameObject, string parentPath)
    {
        var objectPath = $"{parentPath} / {GetName(gameObject.Name, "GameObject")}";
        foreach (var entry in gameObject.SoundEffectLibraryEntries)
        {
            yield return new SoundEntryLocation(objectPath, objectPath, entry);
        }

        foreach (var child in gameObject.ContainedObjects)
        {
            foreach (var location in EnumerateGameObjectLocations(child, objectPath))
            {
                yield return location;
            }
        }
    }

    private static string NormalizeKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string GetName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private sealed record SoundEntryLocation(string ScopePath, string DisplayPath, SoundEffectLibraryEntry Entry);
}