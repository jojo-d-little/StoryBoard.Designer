using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionAmbiguousAnchorLikePathRule : IValidationRule
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
        RuleId: "EVT-006",
        Title: "Ambiguous Anchor-Like Filter Path",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = EventSubscriptionRuleSupport.LoadManifestIndex();
        if (manifest.AnchorKeys.Count == 0 && manifest.AllSubPropertyKeys.Count == 0)
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
                    if (string.IsNullOrWhiteSpace(variableName)
                        || VariableReferenceSyntax.TryParseAnchoredReference(variableName, out _, out _)
                        || !variableName.Contains('.', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var firstSegment = variableName.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault() ?? string.Empty;

                    var looksLikeKnownAnchorOrSubProperty =
                        manifest.AnchorKeys.Contains(firstSegment)
                        || manifest.AllSubPropertyKeys.Contains(firstSegment);

                    if (!looksLikeKnownAnchorOrSubProperty)
                    {
                        continue;
                    }

                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        $"{bindingPath} / filter[{filterIndex + 1}]",
                        $"event filter variable '{variableName}' looks like a manifest subproperty path but is not anchor-rooted.",
                        "Use explicit syntax anchor::subProperty.variable to avoid ambiguity.");
                }
            }
        }
    }
}
