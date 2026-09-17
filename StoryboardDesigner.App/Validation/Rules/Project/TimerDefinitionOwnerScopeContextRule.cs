using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class TimerDefinitionOwnerScopeContextRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global,
            ScopeNodeKind.Planet,
            ScopeNodeKind.Country,
            ScopeNodeKind.Area,
            ScopeNodeKind.Room,
            ScopeNodeKind.GameObject,
            ScopeNodeKind.Templates,
            ScopeNodeKind.RoomTemplates
        };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-017",
        Title: "Timer Definition Owner Scope Context",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = context.CandidateNode;
        var timers = scope.TimerDefinitions ?? new List<RuntimeTimerDefinitionDto>();
        if (timers.Count == 0)
        {
            yield break;
        }

        var scopePath = EventSubscriptionRuleSupport.BuildScopePath(scope);
        var project = context.Project;

        for (var index = 0; index < timers.Count; index++)
        {
            var timer = timers[index];
            var timerPath = $"{scopePath} / timerDefinition[{index + 1}]";
            var timerKey = timer.TimerKey?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(timerKey))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    timerPath,
                    "timer definition must define a non-empty timerKey.");
            }

            var targetActionRef = timer.TargetActionRef?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetActionRef))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    timerPath,
                    "timer definition must define a non-empty targetActionRef.");
            }

            if (!IsOwnerContextResolvable(project, scope, timer.LifetimeOwnerType))
            {
                var effectiveKey = string.IsNullOrWhiteSpace(timerKey) ? "<unnamed>" : timerKey;
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    timerPath,
                    $"timer definition '{effectiveKey}' uses owner '{timer.LifetimeOwnerType}' that is not resolvable from this scope context.");
            }
        }
    }

    private static bool IsOwnerContextResolvable(ProjectModel project, ScopeNodeBase scope, TimerOwnerType ownerType)
    {
        return ownerType switch
        {
            TimerOwnerType.Session => true,
            TimerOwnerType.Player => !string.IsNullOrWhiteSpace(project.PlayerCharacterObjectName),
            TimerOwnerType.Planet => IsScopeKindReachable(scope, ScopeNodeKind.Planet),
            TimerOwnerType.Country => IsScopeKindReachable(scope, ScopeNodeKind.Country),
            TimerOwnerType.Area => IsScopeKindReachable(scope, ScopeNodeKind.Area),
            TimerOwnerType.Room => IsScopeKindReachable(scope, ScopeNodeKind.Room),
            _ => false
        };
    }

    private static bool IsScopeKindReachable(ScopeNodeBase scope, ScopeNodeKind targetKind)
    {
        if (scope.EnumerateSelfAndAncestors().Any(node => node.ScopeKind == targetKind))
        {
            return true;
        }

        return HasDescendantScopeKind(scope, targetKind);
    }

    private static bool HasDescendantScopeKind(IScopedAwareNode scope, ScopeNodeKind targetKind)
    {
        foreach (var child in scope.ChildScopes)
        {
            if (child.ScopeKind == targetKind)
            {
                return true;
            }

            if (HasDescendantScopeKind(child, targetKind))
            {
                return true;
            }
        }

        return false;
    }
}
