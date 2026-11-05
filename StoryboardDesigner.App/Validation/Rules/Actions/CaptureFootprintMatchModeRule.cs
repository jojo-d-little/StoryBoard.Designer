using Storyboard.Shared.RuntimeContracts.Enums;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class CaptureFootprintMatchModeRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-027",
        "Capture Footprint Match Mode",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = Enum.GetValues<ScopeNodeKind>().ToHashSet();
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (ruleContext.CandidateNode is ProjectModel project)
        {
            foreach (var owner in EnumerateSequenceOwners(project))
            {
                foreach (var issue in CheckInlineSequenceActions(owner))
                {
                    yield return issue;
                }
            }

            yield break;
        }

        if (ruleContext.CandidateNode is ValidationActionCandidateNode)
        {
            if (ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
                && context.Action.ActionType == CommandActionType.CaptureObjectsOnObjectFootprint)
            {
                foreach (var issue in CheckAction(context.Action, context.Path))
                {
                    yield return issue;
                }
            }

            yield break;
        }

        if (ruleContext.CandidateNode is ScopeNodeBase ownerScope)
        {
            if (!ruleContext.IsFullProject)
            {
                foreach (var issue in CheckInlineSequenceActions(ownerScope))
                {
                    yield return issue;
                }
            }

            yield break;
        }
    }

    private IEnumerable<ValidationIssue> CheckInlineSequenceActions(ScopeNodeBase ownerScope)
    {
        var ownerPath = string.Join(" / ", ownerScope.EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
        foreach (var sequence in ownerScope.SequenceDefinitions ?? [])
        {
            foreach (var step in sequence.Steps ?? [])
            {
                if (step.InlineAction is null) continue;
                var action = JsonExportService.ToCommandActionModel(step.InlineAction);
                foreach (var issue in CheckAction(
                             action,
                             $"{ownerPath} / Sequence:{sequence.DisplayName} / Step:{step.StepId:D}"))
                {
                    yield return issue;
                }
            }
        }
    }

    private IEnumerable<ValidationIssue> CheckAction(CommandAction action, string path)
    {
        if (action.ActionType != CommandActionType.CaptureObjectsOnObjectFootprint) yield break;
        var mode = (action.Payload as CaptureObjectsOnObjectFootprintPayload)?.FootprintMatchMode;
        if (mode is null)
        {
            yield return CreateIssue(action, path, "Capture footprint match mode is required.", "Choose Any overlap or Fully contained.");
            yield break;
        }

        if (!Enum.IsDefined(mode.Value))
        {
            yield return CreateIssue(action, path, $"Capture footprint match mode '{mode.Value}' is not supported.", "Choose Any overlap or Fully contained.");
        }
    }

    private ValidationIssue CreateIssue(CommandAction action, string path, string description, string hint) =>
        new(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(path, action),
            description,
            hint);

    private static IEnumerable<ScopeNodeBase> EnumerateSequenceOwners(ProjectModel project)
    {
        var visited = new HashSet<ScopeNodeBase>(ReferenceEqualityComparer.Instance);
        IEnumerable<ScopeNodeBase> Visit(ScopeNodeBase scope)
        {
            if (!visited.Add(scope)) yield break;
            yield return scope;
            foreach (var child in scope.ChildScopes.OfType<ScopeNodeBase>())
            {
                foreach (var nested in Visit(child)) yield return nested;
            }
        }

        foreach (var scope in Visit(project)) yield return scope;
        var baseObjectRoots = project.BaseObjects
            .Concat(project.Planets.SelectMany(static planet => planet.BaseObjects))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.BaseObjects))
            .Concat(project.Planets.SelectMany(static planet => planet.Countries)
                .SelectMany(static country => country.Areas).SelectMany(static area => area.BaseObjects));
        foreach (var root in baseObjectRoots)
        {
            foreach (var scope in Visit(root)) yield return scope;
        }
    }
}
