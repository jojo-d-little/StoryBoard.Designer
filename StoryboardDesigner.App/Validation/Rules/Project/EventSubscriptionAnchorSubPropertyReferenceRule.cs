using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionAnchorSubPropertyReferenceRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global,
            ScopeNodeKind.Planet,
            ScopeNodeKind.Country,
            ScopeNodeKind.Area,
            ScopeNodeKind.Room,
            ScopeNodeKind.GameObject
        };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "EVT-003",
        Title: "Event Anchor Subproperty Reference Valid",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = EventSubscriptionRuleSupport.LoadManifestIndex();
        if (manifest.AnchorKeys.Count == 0)
        {
            yield break;
        }

        foreach (var (subscription, _, subscriptionPath) in EventSubscriptionRuleSupport.EnumerateSubscriptions(context.CandidateNode))
        {
            var bindings = subscription.ActionBindings;
            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var bindingPath = $"{subscriptionPath} / actionBinding[{bindingIndex + 1}]";
                var filters = bindings[bindingIndex].Condition.Filters;

                for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
                {
                    var variableName = filters[filterIndex].VariableName?.Trim() ?? string.Empty;
                    if (!VariableReferenceSyntax.TryParseAnchoredReference(variableName, out var anchorKey, out var anchoredPath))
                    {
                        continue;
                    }

                    var filterPath = $"{bindingPath} / filter[{filterIndex + 1}]";
                    var dotIndex = anchoredPath.IndexOf('.', StringComparison.Ordinal);
                    if (dotIndex <= 0)
                    {
                        continue;
                    }

                    var subPropertyKey = anchoredPath[..dotIndex].Trim();
                    if (!manifest.AnchorKeys.Contains(anchorKey))
                    {
                        yield return new ValidationIssue(
                            Metadata.RuleId,
                            Metadata.DefaultSeverity,
                            filterPath,
                            $"anchor '{anchorKey}' is not defined in session-anchordata.manifest.json.",
                            "Use a known anchor key from the session anchor manifest.");
                        continue;
                    }

                    if (manifest.SubPropertiesByAnchor.TryGetValue(anchorKey, out var allowedSubProperties)
                        && allowedSubProperties.Count > 0
                        && !allowedSubProperties.Contains(subPropertyKey))
                    {
                        yield return new ValidationIssue(
                            Metadata.RuleId,
                            Metadata.DefaultSeverity,
                            filterPath,
                            $"subproperty '{subPropertyKey}' is not declared for anchor '{anchorKey}' in session-anchordata manifest metadata.",
                            "Use an allowed supportedSubProperties.propertyKey for the selected anchor.");
                    }
                }
            }
        }
    }
}
