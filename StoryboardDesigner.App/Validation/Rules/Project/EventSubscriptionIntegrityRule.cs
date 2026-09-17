using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionIntegrityRule : IValidationRule
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
        RuleId: "PROJ-015",
        Title: "Event Subscription Integrity",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = context.CandidateNode;
        var scopePath = BuildScopePath(scope);
        var subscriptions = scope.EventSubscriptions ?? new List<EventSubscriptionDefinition>();

        for (var subscriptionIndex = 0; subscriptionIndex < subscriptions.Count; subscriptionIndex++)
        {
            var subscription = subscriptions[subscriptionIndex];
            var subscriptionPath = $"{scopePath} / eventSubscription[{subscriptionIndex + 1}]";

            if (string.IsNullOrWhiteSpace(subscription.EventKey))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    subscriptionPath,
                    "event subscription must define a non-empty eventKey.");
            }

            var bindings = subscription.ActionBindings ?? new List<EventActionBindingDefinition>();
            var duplicateOrders = bindings
                .GroupBy(static binding => binding.Order)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .OrderBy(static order => order)
                .ToList();

            foreach (var duplicateOrder in duplicateOrders)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    subscriptionPath,
                    $"event subscription has duplicate action binding order '{duplicateOrder}'.");
            }

            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                var bindingPath = $"{subscriptionPath} / actionBinding[{bindingIndex + 1}]";

                if (string.IsNullOrWhiteSpace(binding.Target?.ActionName))
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        bindingPath,
                        "event action binding target must define a non-empty actionName.");
                }

                var filters = binding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>();
                for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
                {
                    var filter = filters[filterIndex];
                    if (!string.IsNullOrWhiteSpace(filter.VariableName))
                    {
                        continue;
                    }

                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        $"{bindingPath} / filter[{filterIndex + 1}]",
                        "event binding filter must define a non-empty variableName.");
                }
            }
        }
    }

    private static string BuildScopePath(ScopeNodeBase node)
    {
        var scopes = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name));

        return string.Join(" / ", scopes);
    }
}