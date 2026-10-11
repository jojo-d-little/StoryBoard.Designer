using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

/// <summary>
/// Checks that explicit room-object targets still belong to the action owner's room.
/// Project/global object targets remain valid because they are not attached to a sibling room.
/// </summary>
public sealed class ActionObjectTargetReachabilityRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-018",
        "Action Object Target Room Reachability",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
            || ruleContext.CandidateNode is not ValidationActionCandidateNode candidate)
        {
            yield break;
        }

        var action = context.Action;
        var targetObjectId = GetRoomObjectTargetId(action);
        if (!targetObjectId.HasValue || targetObjectId.Value == Guid.Empty)
        {
            yield break;
        }

        var ownerRoom = candidate.OwnerScope
            .EnumerateSelfAndAncestors()
            .OfType<Room>()
            .FirstOrDefault();
        if (ownerRoom is null)
        {
            yield break;
        }

        var objectsById = ruleContext.SharedState.GetOrAdd(
            "ACT:RoomReachabilityObjectsById",
            () => BuildObjectLookup(ruleContext.Project));

        if (!objectsById.TryGetValue(targetObjectId.Value, out var targetObject))
        {
            // The presentation-cue missing-ID rule already reports this case. The move and
            // rotate editors also retain project identity, so report missing IDs here too.
            if (action.ActionType is CommandActionType.MoveRoomObjectOnGrid
                or CommandActionType.RotateRoomObjectOnGrid)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                    $"action '{DisplayName(action)}' targets object id '{targetObjectId.Value:D}', which no longer exists in the project.",
                    "Choose an existing room object or clear the explicit target to use the command's primary object.");
            }

            yield break;
        }

        var targetRoom = targetObject
            .EnumerateSelfAndAncestors()
            .OfType<Room>()
            .FirstOrDefault();
        if (targetRoom is null || ReferenceEquals(targetRoom, ownerRoom))
        {
            yield break;
        }

        var targetName = DisplayName(targetObject);
        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"action '{DisplayName(action)}' targets room object '{targetName}' in '{BuildScopePath(targetRoom)}', but the action is owned by '{BuildScopePath(ownerRoom)}'. Room-object targets cannot cross between sibling rooms.",
            "Choose an object in the action's room, or clear the explicit target to use the command's primary object.");
    }

    private static Guid? GetRoomObjectTargetId(CommandAction action)
    {
        return action.ActionType switch
        {
            CommandActionType.MoveRoomObjectOnGrid => action.MoveTargetObjectId,
            CommandActionType.RotateRoomObjectOnGrid => action.RotateTargetObjectId,
            CommandActionType.AddObjectPresentationCue or CommandActionType.RemoveObjectPresentationCue or CommandActionType.ClearObjectPresentationCues
                => action.ObjectPresentationCueObjectId,
            _ => null
        };
    }

    private static Dictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var result = new Dictionary<Guid, GameObject>();

        void Visit(IScopedAwareNode node)
        {
            if (node is GameObject gameObject && gameObject.ObjectId != Guid.Empty)
            {
                result.TryAdd(gameObject.ObjectId, gameObject);
            }

            foreach (var child in node.ChildScopes)
            {
                Visit(child);
            }
        }

        Visit(project);
        return result;
    }

    private static string DisplayName(GameObject gameObject)
    {
        return string.IsNullOrWhiteSpace(gameObject.Name) ? "(unnamed object)" : gameObject.Name.Trim();
    }

    private static string DisplayName(CommandAction action)
    {
        return string.IsNullOrWhiteSpace(action.Name) ? "(unnamed action)" : action.Name.Trim();
    }

    private static string BuildScopePath(IScopedAwareNode scope)
    {
        return string.Join(" / ", scope
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
    }
}
