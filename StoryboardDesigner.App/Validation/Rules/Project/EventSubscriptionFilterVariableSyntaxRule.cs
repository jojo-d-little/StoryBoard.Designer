using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionFilterVariableSyntaxRule : IValidationRule
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
        RuleId: "EVT-002",
        Title: "Event Filter Variable Syntax",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = EventSubscriptionRuleSupport.LoadManifestIndex();

        foreach (var (subscription, _, subscriptionPath) in EventSubscriptionRuleSupport.EnumerateSubscriptions(context.CandidateNode))
        {
            var eventKey = subscription.EventKey?.Trim() ?? string.Empty;
            manifest.PayloadKeysByEvent.TryGetValue(eventKey, out var payloadKeysForEvent);
            payloadKeysForEvent ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var bindings = subscription.ActionBindings;
            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var bindingPath = $"{subscriptionPath} / actionBinding[{bindingIndex + 1}]";
                var filters = bindings[bindingIndex].Condition.Filters;

                for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
                {
                    var variableName = filters[filterIndex].VariableName?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(variableName))
                    {
                        continue;
                    }

                    var filterPath = $"{bindingPath} / filter[{filterIndex + 1}]";
                    if (payloadKeysForEvent.Contains(variableName))
                    {
                        continue;
                    }

                    if (VariableReferenceSyntax.TryParseAnchoredReference(variableName, out _, out var anchoredPath))
                    {
                        if (anchoredPath.Contains('.', StringComparison.Ordinal))
                        {
                            continue;
                        }

                        yield return new ValidationIssue(
                            Metadata.RuleId,
                            ValidationSeverity.Error,
                            filterPath,
                            $"event filter variable '{variableName}' must include a subproperty and variable segment after anchor root syntax.",
                            "Use syntax like currentCommand::primaryCommandObject.isBent.");
                        continue;
                    }

                    if (variableName.Contains('.', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (payloadKeysForEvent.Count == 0)
                    {
                        continue;
                    }

                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        filterPath,
                        $"event filter variable '{variableName}' is not a known payload key for eventKey '{eventKey}' and is not anchor-rooted.",
                        "Use a manifest payloadKey or use anchor root syntax (anchor::subProperty.variable). ");
                }
            }
        }
    }
}
