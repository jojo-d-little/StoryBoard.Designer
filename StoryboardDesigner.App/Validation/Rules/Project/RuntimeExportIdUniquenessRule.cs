using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class RuntimeExportIdUniquenessRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-013",
        Title: "Runtime Export Id Uniqueness",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var seenById = new Dictionary<Guid, RuntimeIdentityEntry>();

        foreach (var entry in EnumerateRuntimeIdentityEntries(context.Project))
        {
            if (!seenById.TryGetValue(entry.Id, out var existing))
            {
                seenById.Add(entry.Id, entry);
                continue;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                entry.Path,
                $"runtime export id '{entry.Id:N}' is duplicated between {existing.Kind} '{existing.Path}' and {entry.Kind} '{entry.Path}'.",
                "Assign unique IDs to each runtime-export scope node before export.");
        }
    }

    private static IEnumerable<RuntimeIdentityEntry> EnumerateRuntimeIdentityEntries(ProjectModel project)
    {
        foreach (var planet in project.Planets)
        {
            if (planet.Id != Guid.Empty)
            {
                yield return new RuntimeIdentityEntry(planet.Id, "Planet", BuildScopePath(planet));
            }

            foreach (var gameObject in planet.GameObjects)
            {
                foreach (var objectEntry in EnumerateGameObjectEntries(gameObject))
                {
                    yield return objectEntry;
                }
            }

            foreach (var baseObject in planet.BaseObjects)
            {
                foreach (var objectEntry in EnumerateGameObjectEntries(baseObject))
                {
                    yield return objectEntry;
                }
            }

            foreach (var country in planet.Countries)
            {
                if (country.Id != Guid.Empty)
                {
                    yield return new RuntimeIdentityEntry(country.Id, "Country", BuildScopePath(country));
                }

                foreach (var gameObject in country.GameObjects)
                {
                    foreach (var objectEntry in EnumerateGameObjectEntries(gameObject))
                    {
                        yield return objectEntry;
                    }
                }

                foreach (var baseObject in country.BaseObjects)
                {
                    foreach (var objectEntry in EnumerateGameObjectEntries(baseObject))
                    {
                        yield return objectEntry;
                    }
                }

                foreach (var area in country.Areas)
                {
                    if (area.Id != Guid.Empty)
                    {
                        yield return new RuntimeIdentityEntry(area.Id, "Area", BuildScopePath(area));
                    }

                    foreach (var gameObject in area.GameObjects)
                    {
                        foreach (var objectEntry in EnumerateGameObjectEntries(gameObject))
                        {
                            yield return objectEntry;
                        }
                    }

                    foreach (var baseObject in area.BaseObjects)
                    {
                        foreach (var objectEntry in EnumerateGameObjectEntries(baseObject))
                        {
                            yield return objectEntry;
                        }
                    }

                    foreach (var room in area.Rooms)
                    {
                        if (room.Id != Guid.Empty)
                        {
                            yield return new RuntimeIdentityEntry(room.Id, "Room", BuildScopePath(room));
                        }

                        foreach (var gameObject in room.GameObjects)
                        {
                            foreach (var objectEntry in EnumerateGameObjectEntries(gameObject))
                            {
                                yield return objectEntry;
                            }
                        }
                    }
                }
            }
        }

        foreach (var gameObject in project.GlobalScope.GameObjects)
        {
            foreach (var objectEntry in EnumerateGameObjectEntries(gameObject))
            {
                yield return objectEntry;
            }
        }

        foreach (var templateObject in project.ObjectTemplates)
        {
            foreach (var objectEntry in EnumerateGameObjectEntries(templateObject))
            {
                yield return objectEntry;
            }
        }

        foreach (var baseObject in project.BaseObjects)
        {
            foreach (var objectEntry in EnumerateGameObjectEntries(baseObject))
            {
                yield return objectEntry;
            }
        }

        foreach (var roomTemplate in project.RoomTemplates)
        {
            if (roomTemplate.Id != Guid.Empty)
            {
                yield return new RuntimeIdentityEntry(roomTemplate.Id, "Room", BuildScopePath(roomTemplate));
            }

            foreach (var gameObject in roomTemplate.GameObjects)
            {
                foreach (var objectEntry in EnumerateGameObjectEntries(gameObject))
                {
                    yield return objectEntry;
                }
            }
        }
    }

    private static IEnumerable<RuntimeIdentityEntry> EnumerateGameObjectEntries(GameObject root)
    {
        if (root.ObjectId != Guid.Empty)
        {
            yield return new RuntimeIdentityEntry(root.ObjectId, "GameObject", BuildScopePath(root));
        }

        foreach (var child in root.ContainedObjects)
        {
            foreach (var descendant in EnumerateGameObjectEntries(child))
            {
                yield return descendant;
            }
        }
    }

    private static string BuildScopePath(IScopedAwareNode node)
    {
        var scopes = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(scope => scope.ScopeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return scopes.Count == 0
            ? "Project"
            : string.Join(" / ", scopes);
    }

    private sealed record RuntimeIdentityEntry(Guid Id, string Kind, string Path);
}