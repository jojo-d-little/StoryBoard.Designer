using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

/// <summary>Advises which authored actions are safe to run before a destination Room becomes current.</summary>
public sealed class PreRoomEntrySetupAdvisoryRule : IValidationRule
{
    private static readonly IReadOnlySet<CommandActionType> AllowedActionTypes = new HashSet<CommandActionType>
    {
        CommandActionType.SetGameProperty,
        CommandActionType.CheckGameProperty,
        CommandActionType.StartTimer
    };

    private sealed record ActionEntry(CommandAction Action);

    public ValidationRuleMetadata Metadata { get; } = new(
        "ELEV-001",
        "Pre-Room Entry Setup Context",
        ValidationSeverity.Warning,
        "Room Entry");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public bool SupportsActionCandidates => false;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ProjectModel project)
        {
            yield break;
        }

        var actionLookup = EnumerateActionEntries(project)
            .Where(static entry => entry.Action.Id != Guid.Empty)
            .GroupBy(static entry => entry.Action.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        foreach (var room in EnumerateRooms(project).DistinctBy(static room => room.Id))
        {
            if (room.PreRoomEntryActionId is not Guid actionId || !actionLookup.TryGetValue(actionId, out var root))
            {
                continue;
            }

            var roomPath = string.Join(" / ", room.EnumerateSelfAndAncestors().Reverse()
                .Select(static node => node.ScopeName).Where(static name => !string.IsNullOrWhiteSpace(name)));
            var rootPath = $"{roomPath} / Pre-Room Entry";
            foreach (var leaf in Walk(root, rootPath, actionLookup, new HashSet<Guid>()))
            {
                if (AllowedActionTypes.Contains(leaf.Action.ActionType))
                {
                    continue;
                }

                yield return new ValidationIssue(
                    Metadata.RuleId,
                    ValidationSeverity.Warning,
                    leaf.Path,
                    $"Action type '{leaf.Action.ActionType}' is not on the pre-room setup allowlist.",
                    "This warning is advisory and does not block authoring or runtime navigation.");
            }
        }
    }

    private sealed record LeafAction(CommandAction Action, string Path);

    private static IEnumerable<LeafAction> Walk(
        ActionEntry entry,
        string parentPath,
        IReadOnlyDictionary<Guid, ActionEntry> actionsById,
        HashSet<Guid> ancestorIds)
    {
        var action = entry.Action;
        if (action.Id != Guid.Empty && !ancestorIds.Add(action.Id))
        {
            yield break;
        }

        var path = $"{parentPath} / {action.Name} ({action.ActionType})";
        if (action.ActionType != CommandActionType.LinkedActions)
        {
            yield return new LeafAction(action, path);
            yield break;
        }

        foreach (var node in action.LinkedFlowDefinition?.Nodes ?? [])
        {
            if (!actionsById.TryGetValue(node.ActionId, out var nested))
            {
                continue;
            }

            var nodePath = $"{path} / Linked Flow Node {node.NodeId}";
            foreach (var leaf in Walk(nested, nodePath, actionsById, new HashSet<Guid>(ancestorIds)))
            {
                yield return leaf;
            }
        }
    }

    private static IEnumerable<ActionEntry> EnumerateActionEntries(ProjectModel project)
    {
        foreach (var scope in EnumerateScopes(project))
        {
            var actions = scope switch
            {
                ProjectModel projectScope => projectScope.GlobalScope.AvailableActions,
                GameObject gameObject => gameObject.AvailableActions,
                Room room => room.AvailableActions,
                Planet planet => planet.AvailableActions,
                Country country => country.AvailableActions,
                Area area => area.AvailableActions,
                PhaseNode phase => phase.AvailableActions,
                _ => Enumerable.Empty<CommandAction>()
            };
            foreach (var action in actions)
            {
                yield return new ActionEntry(action);
            }
        }

        static IEnumerable<IScopedAwareNode> EnumerateScopes(IScopedAwareNode root)
        {
            var pending = new Stack<IScopedAwareNode>();
            var seen = new HashSet<IScopedAwareNode>(ReferenceEqualityComparer.Instance);
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!seen.Add(current)) continue;
                yield return current;
                foreach (var child in current.ChildScopes) pending.Push(child);
            }
        }
    }

    private static IEnumerable<Room> EnumerateRooms(ProjectModel project) =>
        project.RoomTemplates
            .Concat(project.Planets.SelectMany(static planet => planet.RoomTemplates))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.RoomTemplates))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.RoomTemplates))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.Rooms));
}
