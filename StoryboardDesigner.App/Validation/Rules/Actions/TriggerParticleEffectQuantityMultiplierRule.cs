using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class TriggerParticleEffectQuantityMultiplierRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-025",
        "Trigger Particle Effect Quantity Multiplier",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global, ScopeNodeKind.GameObject };

    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context)
            || context.Action.ActionType != CommandActionType.TriggerParticleEffect
            || context.Action.Payload is not TriggerParticleEffectPayload payload)
        {
            yield break;
        }

        var path = ActionRuleSupport.BuildActionIssuePath(context.Path, context.Action);
        var literal = payload.QuantityMultiplier ?? 1d;
        if (!double.IsFinite(literal) || literal < 0)
        {
            yield return Issue(path, "TriggerParticleEffect quantity multiplier must be finite and nonnegative.", "Use a nonnegative number. Zero is allowed.");
        }

        var binding = payload.QuantityMultiplierBinding;
        if (binding is null)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(binding.PropertyName)
            || binding.OwnerId.HasValue && !string.IsNullOrWhiteSpace(binding.AnchorKey))
        {
            yield return Issue(path, "TriggerParticleEffect quantity multiplier binding has an invalid source reference.", "Choose a numeric variable with the standard variable picker.");
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(binding.AnchorKey)
            && !GamePropertySourceResolver.SupportsAnchor(ruleContext.Project, binding.AnchorKey.Trim()))
        {
            yield return Issue(path, $"TriggerParticleEffect quantity multiplier uses unsupported anchor '{binding.AnchorKey}'.", "Choose a supported variable anchor.");
            yield break;
        }

        var relativeOwnerId = GetScopeOwnerId(ruleContext.CandidateNode is ValidationActionCandidateNode candidate ? candidate.OwnerScope : null);
        var matches = GamePropertySourceResolver.Resolve(ruleContext.Project, binding, relativeOwnerId);
        if (matches.Count == 0)
        {
            yield return Issue(path, $"TriggerParticleEffect quantity multiplier binding cannot resolve variable '{binding.PropertyName}'.", "Choose an existing numeric variable.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(binding.AnchorKey) && matches.Count > 1)
        {
            yield return Issue(path, $"TriggerParticleEffect quantity multiplier binding is ambiguous for variable '{binding.PropertyName}'.", "Choose a uniquely scoped variable or a well-known anchor.");
            yield break;
        }

        var restrictions = matches.Select(static match => match.Property.ValueRestriction).Distinct().ToList();
        if (restrictions.Count != 1 || restrictions[0] != GamePropertyValueRestriction.Numeric)
        {
            yield return Issue(path, "TriggerParticleEffect quantity multiplier binding must resolve only to numeric properties.", "Choose a numeric variable.");
        }
    }

    private ValidationIssue Issue(string path, string message, string suggestion) =>
        new(Metadata.RuleId, Metadata.DefaultSeverity, path, message, suggestion);

    private static Guid GetScopeOwnerId(ScopeNodeBase? scope) => scope switch
    {
        GameObject gameObject => gameObject.ObjectId,
        Planet planet => planet.Id,
        Country country => country.Id,
        Area area => area.Id,
        Room room => room.Id,
        _ => Guid.Empty
    };
}
