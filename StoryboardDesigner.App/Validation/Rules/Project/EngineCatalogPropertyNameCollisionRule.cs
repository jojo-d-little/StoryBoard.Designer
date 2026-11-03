using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

/// <summary>Warns when an authored key is shadowed by an Engine-owned property catalog entry.</summary>
public sealed class EngineCatalogPropertyNameCollisionRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "GPROP-002",
        "Engine Catalog Property Name Collisions",
        ValidationSeverity.Warning,
        "Game Properties");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CandidateNode is not ProjectModel project)
        {
            yield break;
        }

        foreach (var collision in GamePropertySourceResolver.EnumerateCatalogNameCollisions(project))
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                $"{collision.ScopePath}.Game Properties.{collision.PropertyName}",
                $"Authored Game Property '{collision.PropertyName}' on '{collision.OwnerName}' collides with an Engine catalog key for the {collision.Scope} scope. The Engine catalog definition is used in property discovery.",
                "Rename or remove the authored property; Engine catalog properties are authoritative and are not authored on project objects.");
        }
    }
}
