using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionAnchorDynamicTailReferenceRule : IValidationRule
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
        RuleId: "EVT-007",
        Title: "Anchor Reference Dynamic Tail",
        DefaultSeverity: ValidationSeverity.Warning,
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

                    var dotIndex = anchoredPath.IndexOf('.', StringComparison.Ordinal);
                    if (dotIndex <= 0)
                    {
                        continue;
                    }

                    var subPropertyKey = anchoredPath[..dotIndex].Trim();
                    if (!manifest.AnchorKeys.Contains(anchorKey))
                    {
                        continue;
                    }

                    if (manifest.SubPropertiesByAnchor.TryGetValue(anchorKey, out var allowedSubProperties)
                        && allowedSubProperties.Count > 0
                        && !allowedSubProperties.Contains(subPropertyKey))
                    {
                        continue;
                    }

                    var tail = anchoredPath[(dotIndex + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(tail))
                    {
                        continue;
                    }

                    if (IsIntrinsicLeaf(tail))
                    {
                        continue;
                    }

                    if (!LooksDynamicTail(tail))
                    {
                        continue;
                    }

                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        $"{bindingPath} / filter[{filterIndex + 1}]",
                        $"anchor reference '{variableName}' has a dynamic tail that cannot be fully validated at design time.",
                        "Keep the anchor root and supported subproperty explicit; confirm runtime payload shape in validation report output.");
                }
            }
        }
    }

    private static bool IsIntrinsicLeaf(string tail)
    {
        return string.Equals(tail, "name", StringComparison.OrdinalIgnoreCase)
               || string.Equals(tail, "nameInGame", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksDynamicTail(string tail)
    {
        return tail.Contains('.', StringComparison.Ordinal)
               || tail.Contains('[', StringComparison.Ordinal)
               || tail.Contains(']', StringComparison.Ordinal);
    }
}
