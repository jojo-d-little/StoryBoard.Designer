using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionDuplicateFilterConditionRule : IValidationRule
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
        RuleId: "EVT-005",
        Title: "Duplicate Event Filter Conditions",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var (subscription, _, subscriptionPath) in EventSubscriptionRuleSupport.EnumerateSubscriptions(context.CandidateNode))
        {
            var bindings = subscription.ActionBindings;
            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var filters = bindings[bindingIndex].Condition.Filters;
                var duplicates = filters
                    .Select((filter, index) => new
                    {
                        Index = index,
                        VariableName = (filter.VariableName ?? string.Empty).Trim(),
                        Operator = filter.Operator,
                        ExpectedValue = (filter.ExpectedValue ?? string.Empty).Trim()
                    })
                    .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                    .GroupBy(
                        static item => (item.VariableName.ToUpperInvariant(), item.Operator, item.ExpectedValue),
                        static item => item.Index)
                    .Where(static group => group.Count() > 1)
                    .ToList();

                foreach (var duplicate in duplicates)
                {
                    var duplicateIndexes = duplicate
                        .Select(static index => index + 1)
                        .OrderBy(static index => index)
                        .ToList();

                    var bindingPath = $"{subscriptionPath} / actionBinding[{bindingIndex + 1}]";
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        bindingPath,
                        $"event binding contains duplicate filter condition rows at indexes [{string.Join(", ", duplicateIndexes)}].",
                        "Remove duplicate filter rows or consolidate to a single condition.");
                }
            }
        }
    }
}
