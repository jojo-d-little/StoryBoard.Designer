using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EnvironmentalEffectDefinitionIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        Enum.GetValues<ScopeNodeKind>().ToHashSet();

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "ENV-001",
        Title: "Environmental Effect Definitions Must Be Complete",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Presentation");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not ScopeNodeBase scope || scope.EnvironmentalEffects.Count == 0)
        {
            yield break;
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in scope.EnvironmentalEffects)
        {
            var path = BuildPath(scope, effect.Name);
            if (string.IsNullOrWhiteSpace(effect.Name))
            {
                yield return NewIssue(path, "Environmental effects need a name.");
            }
            else if (!seenNames.Add(effect.Name.Trim()))
            {
                yield return NewIssue(path, $"Environmental effect name '{effect.Name.Trim()}' is repeated in this scope.");
            }

            if (string.IsNullOrWhiteSpace(effect.EffectKey))
            {
                yield return NewIssue(path, "Choose a catalog effect style for this environmental effect.");
            }

            if (effect.EnabledBinding is { } enabledBinding && !IsValidReferenceShape(enabledBinding))
                yield return NewIssue(path, "Environmental effect Enabled binding needs a property name and may specify either an owner or an anchor, not both.");

            var duplicateProperties = effect.Settings
                .Where(static setting => !string.IsNullOrWhiteSpace(setting.ControlKey))
                .GroupBy(static setting => setting.ControlKey.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key);
            foreach (var propertyName in duplicateProperties)
            {
                yield return NewIssue(path, $"Control property '{propertyName}' is defined more than once.");
            }
        }
    }

    private ValidationIssue NewIssue(string path, string message) =>
        new(Metadata.RuleId, Metadata.DefaultSeverity, path, message);

    private static bool IsValidReferenceShape(Storyboard.Shared.DesignerContracts.ProjectGamePropertySourceReferenceDto reference) =>
        !string.IsNullOrWhiteSpace(reference.PropertyName)
        && !(reference.OwnerId.HasValue && !string.IsNullOrWhiteSpace(reference.AnchorKey));

    private static string BuildPath(ScopeNodeBase scope, string effectName)
    {
        var scopes = scope.EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name));
        var prefix = string.Join(" / ", scopes);
        return string.IsNullOrWhiteSpace(effectName) ? prefix : $"{prefix} / Environmental effect: {effectName.Trim()}";
    }
}
