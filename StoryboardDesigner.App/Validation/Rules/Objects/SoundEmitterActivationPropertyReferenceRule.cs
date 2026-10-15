using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class SoundEmitterActivationPropertyReferenceRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-008",
        Title: "Sound Emitter Activation Property Reference",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Sound");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not GameObject gameObject)
        {
            yield break;
        }

        var ownerPath = string.Join(" / ", gameObject
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));

        for (var index = 0; index < gameObject.SoundEmitters.Count; index++)
        {
            var emitter = gameObject.SoundEmitters[index];
            var propertyName = emitter.ActivationGamePropertyName?.Trim();
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                continue;
            }

            var matchingProperties = gameObject.Variables
                .Where(property => string.Equals(property.Name?.Trim(), propertyName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var emitterPath = $"{ownerPath} / soundEmitter[{index + 1}]";

            if (matchingProperties.Count == 0)
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    emitterPath,
                    $"activation game property name '{propertyName}' does not reference a property on this object.",
                    "Select or enter the name of a boolean game property defined on the emitter's own object, or leave it blank.");
                continue;
            }

            if (matchingProperties.Count != 1 || !IsBooleanProperty(matchingProperties[0]))
            {
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    emitterPath,
                    $"activation game property name '{propertyName}' is ambiguous or does not reference a boolean game property on this object.",
                    "Use the unique name of an object-local boolean game property, or leave it blank.");
            }
        }
    }

    private static bool IsBooleanProperty(GamePropertyDefinition property) =>
        property.ValueRestriction == GamePropertyValueRestriction.TrueFalse
        || bool.TryParse(property.DefaultValue, out _);
}
