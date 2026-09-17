using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed class ValidationRuleRegistry
{
    private readonly List<IValidationRule> _rules = new();

    public IReadOnlyList<IValidationRule> Rules => _rules;

    public void Register(IValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (_rules.Any(existing => string.Equals(existing.Metadata.RuleId, rule.Metadata.RuleId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"RuleId '{rule.Metadata.RuleId}' is already registered.");
        }

        _rules.Add(rule);
    }
}
