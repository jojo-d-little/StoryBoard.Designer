using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class TriggerParticleEffectTargetIsBurstEligibleRule : IValidationRule
{
    private readonly ParticleEffectAuthoringCatalogService _catalog = new();

    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-024",
        "Trigger Particle Effect Target Is Burst Eligible",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global, ScopeNodeKind.GameObject };

    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
            || context.Action.ActionType != CommandActionType.TriggerParticleEffect)
        {
            yield break;
        }

        var action = context.Action;
        var path = ActionRuleSupport.BuildActionIssuePath(context.Path, action);
        var targetId = ActionPayloadAccessors.GetTriggerParticleEffectObjectId(action);
        if (targetId is null || targetId == Guid.Empty)
        {
            yield return Issue(path, "TriggerParticleEffect requires a target object.", "Choose an object that has an attached, burst-eligible particle effect.");
            yield break;
        }

        var target = EnumerateKnownObjects(ruleContext.Project)
            .FirstOrDefault(obj => obj.ObjectId == targetId.Value);
        if (target is null)
        {
            yield return Issue(path, $"TriggerParticleEffect references object id '{targetId.Value:D}' that does not exist in the project.", "Choose an existing object target.");
            yield break;
        }

        var effectKey = target.ParticleEffect?.EffectKey?.Trim();
        if (string.IsNullOrWhiteSpace(effectKey))
        {
            yield return Issue(path, $"TriggerParticleEffect target '{target.Name}' has no particle effect attachment.", "Attach a particle effect to the target object, or choose another object.");
            yield break;
        }

        if (!_catalog.HasBurstEligibleStreams(effectKey))
        {
            yield return Issue(path, $"Particle effect '{effectKey}' attached to target '{target.Name}' has no surgeOnly streams and cannot be triggered as a burst.", "Choose a target whose attached recipe contains at least one surgeOnly stream.");
        }
    }

    private ValidationIssue Issue(string path, string message, string suggestion) =>
        new(Metadata.RuleId, Metadata.DefaultSeverity, path, message, suggestion);

    private static IEnumerable<GameObject> EnumerateKnownObjects(ProjectModel project)
    {
        var visited = new HashSet<Guid>();

        IEnumerable<GameObject> Walk(IScopedAwareNode node)
        {
            if (node is GameObject obj && obj.ObjectId != Guid.Empty && visited.Add(obj.ObjectId))
            {
                yield return obj;
            }

            foreach (var childNode in node.ChildScopes)
            {
                foreach (var child in Walk(childNode))
                {
                    yield return child;
                }
            }
        }

        return Walk(project);
    }
}
