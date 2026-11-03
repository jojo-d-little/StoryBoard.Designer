using System.Text.RegularExpressions;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

/// <summary>Advises which authored actions are safe to run before a destination Room becomes current.</summary>
public sealed class PreRoomEntrySetupAdvisoryRule : IValidationRule
{
    private static readonly Regex CurrentRoomReference = new(@"(?<![A-Za-z0-9_])currentRoom(?:\.|\[|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed record ActionEntry(CommandAction Action, Guid OwnerId);

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
            foreach (var leaf in Walk(root, root.OwnerId, rootPath, actionLookup, new HashSet<Guid>()))
            {
                var action = leaf.Entry.Action;
                if (ReferencesCurrentRoom(action))
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        ValidationSeverity.Warning,
                        leaf.Path,
                        $"{action.ActionType} action '{action.Name}' explicitly references currentRoom, which remains the source Room until setup finishes.",
                        "Use an explicit destination or placed-object reference for destination setup. This advisory does not block authoring or navigation.");
                }

                if (!IsQuietLeafAction(project, action, leaf.RelativeOwnerId))
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        ValidationSeverity.Warning,
                        leaf.Path,
                        BuildLeafWarning(action),
                        "Review the action's synchronous effects and target scope. This warning is advisory and does not block authoring or runtime navigation.");
                }
            }
        }
    }

    private sealed record LeafAction(ActionEntry Entry, Guid RelativeOwnerId, string Path);

    private static IEnumerable<LeafAction> Walk(
        ActionEntry entry,
        Guid relativeOwnerId,
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
            yield return new LeafAction(entry, relativeOwnerId, path);
            yield break;
        }

        foreach (var node in action.LinkedFlowDefinition?.Nodes ?? [])
        {
            if (!actionsById.TryGetValue(node.ActionId, out var nested))
            {
                continue;
            }

            var nestedOwnerId = node.TargetObjectId
                ?? (nested.OwnerId != Guid.Empty ? nested.OwnerId : relativeOwnerId);
            var nodePath = $"{path} / Linked Flow Node {node.NodeId}";
            foreach (var leaf in Walk(nested, nestedOwnerId, nodePath, actionsById, new HashSet<Guid>(ancestorIds)))
            {
                yield return leaf;
            }
        }
    }

    private static bool IsQuietLeafAction(ProjectModel project, CommandAction action, Guid relativeOwnerId)
    {
        if (action.ActionType == CommandActionType.CheckGameProperty)
        {
            return true;
        }

        if (action.ActionType != CommandActionType.SetGameProperty)
        {
            return false;
        }

        var reference = ActionPayloadAccessors.GetSetGamePropertyReference(action);
        if (reference is null)
        {
            return false;
        }

        var targets = GamePropertySourceResolver.Resolve(project, reference, relativeOwnerId);
        return targets.Count > 0 && targets.All(static target => target.IsEngineProperty && target.CanWrite);
    }

    private static string BuildLeafWarning(CommandAction action)
    {
        if (!Enum.IsDefined(action.ActionType))
        {
            return $"Action type '{action.ActionType}' has not been reviewed for pre-room-entry setup.";
        }

        var explanation = action.ActionType switch
        {
            CommandActionType.NavigateDirection or CommandActionType.NavigateDirectionPreFlightCheck
                or CommandActionType.NavigateToAdjacent or CommandActionType.NavigateObjectSetToAdjacent
                => "Navigation can redirect or re-enter a transition while the source Room is still active.",
            CommandActionType.StartSequence
                => "A timed sequence can continue after the synchronous destination setup boundary has finished.",
            CommandActionType.MoveRoomObjectOnGrid or CommandActionType.MoveRoomObjectByPoints
                or CommandActionType.RotateRoomObjectOnGrid or CommandActionType.StackRoomObjectOnAnother
                => "Room-object mutation may target the still-active source Room before destination activation.",
            CommandActionType.CaptureObjectsOnObjectFootprint or CommandActionType.CheckObjectSetContainsObject
                or CommandActionType.ClearActiveRoomObjects or CommandActionType.SelectRoomObjectByPoint
                or CommandActionType.SetActiveRoomObject
                => "This action reads or changes current-room object state while the source Room is still active.",
            CommandActionType.AddObjectPresentationCue or CommandActionType.RemoveObjectPresentationCue
                or CommandActionType.ClearObjectPresentationCues or CommandActionType.TriggerParticleEffect
                => "Presentation effects may be applied to the source Room before the destination is activated.",
            CommandActionType.SetGameProperty
                => "The target is not a cataloged writable Engine property for the selected scope.",
            _ => $"Action type '{action.ActionType}' has not been reviewed for pre-room-entry setup."
        };

        return $"Pre-room setup action '{action.Name}' ({action.ActionType}) is not allowlisted. {explanation}";
    }

    private static bool ReferencesCurrentRoom(CommandAction action)
    {
        var texts = action.OutcomeMessageMap.Values
            .Concat(action.GetScriptFields().Select(static field => field.Value))
            .ToList();
        if (action.ActionType == CommandActionType.EchoMessage)
        {
            texts.Add(ActionPayloadAccessors.GetEchoMessage(action));
        }

        if (action.ActionType == CommandActionType.CheckGameProperty)
        {
            texts.Add(ActionPayloadAccessors.GetCheckPropertyName(action));
            var reference = ActionPayloadAccessors.GetCheckGamePropertyReference(action);
            if (reference is not null)
            {
                texts.Add(reference.AnchorKey ?? string.Empty);
                if (string.Equals(reference.AnchorKey, "currentRoom", StringComparison.OrdinalIgnoreCase))
                    texts.Add($"currentRoom.{reference.PropertyName}");
            }
        }

        if (action.ActionType == CommandActionType.SetGameProperty)
        {
            texts.Add(ActionPayloadAccessors.GetSetPropertyName(action));
            var reference = ActionPayloadAccessors.GetSetGamePropertyReference(action);
            if (reference is not null)
            {
                texts.Add(reference.AnchorKey ?? string.Empty);
                if (string.Equals(reference.AnchorKey, "currentRoom", StringComparison.OrdinalIgnoreCase))
                    texts.Add($"currentRoom.{reference.PropertyName}");
            }
        }

        return texts.Any(text => !string.IsNullOrWhiteSpace(text) && CurrentRoomReference.IsMatch(text));
    }

    private static IEnumerable<ActionEntry> EnumerateActionEntries(ProjectModel project)
    {
        foreach (var scope in EnumerateScopes(project))
        {
            var (ownerId, actions) = scope switch
            {
                ProjectModel projectScope => (Guid.Empty, projectScope.GlobalScope.AvailableActions),
                GameObject gameObject => (gameObject.ObjectId, gameObject.AvailableActions),
                Room room => (room.Id, room.AvailableActions),
                Planet planet => (planet.Id, planet.AvailableActions),
                Country country => (country.Id, country.AvailableActions),
                Area area => (area.Id, area.AvailableActions),
                PhaseNode phase => (phase.Id, phase.AvailableActions),
                _ => (Guid.Empty, Enumerable.Empty<CommandAction>())
            };
            foreach (var action in actions)
            {
                yield return new ActionEntry(action, ownerId);
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
