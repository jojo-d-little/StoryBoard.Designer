using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ProcedureOwnershipReferenceExistsRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-012",
        Title: "Procedure Ownership Reference Exists",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var knownProcedureIds = context.Project.Procedures
            .Where(static procedure => procedure.Id != Guid.Empty)
            .Select(static procedure => procedure.Id)
            .ToHashSet();

        for (var i = 0; i < context.Project.ProcedureIds.Count; i++)
        {
            var procedureId = context.Project.ProcedureIds[i];
            if (procedureId == Guid.Empty || knownProcedureIds.Contains(procedureId))
            {
                continue;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project / Global Procedures",
                $"ProcedureIds entry {i + 1} references missing procedure id '{procedureId:N}'.",
                "Open project procedure ownership and select a procedure that exists in Project Procedures.");
        }

        foreach (var (owner, ownerPath) in EnumerateOwnedProcedureHolders(context.Project))
        {
            for (var i = 0; i < owner.ProcedureIds.Count; i++)
            {
                var procedureId = owner.ProcedureIds[i];
                if (procedureId == Guid.Empty || knownProcedureIds.Contains(procedureId))
                {
                    continue;
                }

                var ownerName = NormalizeName(owner.Name, "GameObject");
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    ownerPath,
                    $"GameObject '{ownerName}' owned procedure entry {i + 1} references missing procedure id '{procedureId:N}'.",
                    "Open object procedure ownership and select a procedure that exists in Project Procedures.");
            }
        }
    }

    private static IEnumerable<(GameObject Owner, string Path)> EnumerateOwnedProcedureHolders(ProjectModel project)
    {
        foreach (var root in project.GlobalScope.GameObjects)
        {
            foreach (var owner in EnumerateObjectTree(root))
            {
                yield return (owner, BuildScopePath(owner));
            }
        }

        foreach (var root in project.ObjectTemplates)
        {
            foreach (var owner in EnumerateObjectTree(root))
            {
                yield return (owner, BuildScopePath(owner));
            }
        }

        foreach (var root in project.BaseObjects)
        {
            foreach (var owner in EnumerateObjectTree(root))
            {
                yield return (owner, BuildScopePath(owner));
            }
        }

        foreach (var root in project.RoomTemplates.SelectMany(static room => room.GameObjects))
        {
            foreach (var owner in EnumerateObjectTree(root))
            {
                yield return (owner, BuildScopePath(owner));
            }
        }

        foreach (var planet in project.Planets)
        {
            foreach (var root in planet.BaseObjects)
            {
                foreach (var owner in EnumerateObjectTree(root))
                {
                    yield return (owner, BuildScopePath(owner));
                }
            }

            foreach (var root in planet.GameObjects)
            {
                foreach (var owner in EnumerateObjectTree(root))
                {
                    yield return (owner, BuildScopePath(owner));
                }
            }

            foreach (var country in planet.Countries)
            {
                foreach (var root in country.BaseObjects)
                {
                    foreach (var owner in EnumerateObjectTree(root))
                    {
                        yield return (owner, BuildScopePath(owner));
                    }
                }

                foreach (var root in country.GameObjects)
                {
                    foreach (var owner in EnumerateObjectTree(root))
                    {
                        yield return (owner, BuildScopePath(owner));
                    }
                }

                foreach (var area in country.Areas)
                {
                    foreach (var root in area.BaseObjects)
                    {
                        foreach (var owner in EnumerateObjectTree(root))
                        {
                            yield return (owner, BuildScopePath(owner));
                        }
                    }

                    foreach (var root in area.GameObjects)
                    {
                        foreach (var owner in EnumerateObjectTree(root))
                        {
                            yield return (owner, BuildScopePath(owner));
                        }
                    }

                    foreach (var root in area.Rooms.SelectMany(static room => room.GameObjects))
                    {
                        foreach (var owner in EnumerateObjectTree(root))
                        {
                            yield return (owner, BuildScopePath(owner));
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateObjectTree(GameObject root)
    {
        yield return root;

        foreach (var child in root.ContainedObjects)
        {
            foreach (var descendant in EnumerateObjectTree(child))
            {
                yield return descendant;
            }
        }
    }

    private static string BuildScopePath(GameObject gameObject)
    {
        var scopes = gameObject
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(node => node.ScopeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return scopes.Count == 0
            ? "Project"
            : string.Join(" / ", scopes);
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}