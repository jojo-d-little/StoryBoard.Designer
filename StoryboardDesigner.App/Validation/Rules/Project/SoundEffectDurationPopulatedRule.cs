using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SoundEffectDurationPopulatedRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-018",
        Title: "Sound Effect Duration Required",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        foreach (var location in EnumerateLocations(context.Project))
        {
            var durationMs = location.Entry.DurationMs.GetValueOrDefault();
            if (durationMs > 0)
            {
                continue;
            }

            var soundEffectKey = location.Entry.SoundEffectKey?.Trim() ?? string.Empty;
            var keyOrId = string.IsNullOrWhiteSpace(soundEffectKey)
                ? location.Entry.SoundEffectId.ToString("D")
                : soundEffectKey;

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                location.ScopePath,
                $"Sound effect '{keyOrId}' is missing durationMs. Provide a nonzero durationMs value.",
                "Open the sound effect entry and save with a resolvable asset so durationMs can be populated.");
        }
    }

    private static IEnumerable<SoundEntryLocation> EnumerateLocations(ProjectModel project)
    {
        foreach (var entry in project.GlobalScope.SoundEffectLibraryEntries)
        {
            yield return new SoundEntryLocation("Global", entry);
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
                yield return new SoundEntryLocation(roomPath, entry);
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
                yield return new SoundEntryLocation(planetPath, entry);
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
                    yield return new SoundEntryLocation(countryPath, entry);
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
                        yield return new SoundEntryLocation(areaPath, entry);
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
                            yield return new SoundEntryLocation(roomPath, entry);
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
            yield return new SoundEntryLocation(objectPath, entry);
        }

        foreach (var child in gameObject.ContainedObjects)
        {
            foreach (var location in EnumerateGameObjectLocations(child, objectPath))
            {
                yield return location;
            }
        }
    }

    private static string GetName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private sealed record SoundEntryLocation(string ScopePath, SoundEffectLibraryEntry Entry);
}
