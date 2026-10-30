using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

/// <summary>Validates child cue inheritance selections against the installed presentation-effects catalog.</summary>
public sealed class ParentCueInheritanceCategoryRule : IValidationRule
{
    private readonly PresentationEffectsCatalogService _catalog;

    public ParentCueInheritanceCategoryRule() : this(new PresentationEffectsCatalogService()) { }

    public ParentCueInheritanceCategoryRule(PresentationEffectsCatalogService catalog) => _catalog = catalog;

    /// <summary>Gets the scope-node kinds checked by this rule.</summary>
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    /// <summary>Gets this rule's validation identifier, severity, and authoring category.</summary>
    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-022",
        Title: "Parent Cue Inheritance Must Use Eligible Catalog Categories",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Objects");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not GameObject gameObject || gameObject.InheritParentCueCategories is null)
        {
            yield break;
        }

        var path = string.Join(" / ", gameObject.EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));

        if (!gameObject.IsPartOfParent)
        {
            yield return Issue(path, "Parent cue inheritance categories may only be set on a direct structural child.");
            yield break;
        }

        var eligibleCategories = _catalog.GetCatalog().Categories
            .Where(static category => category.ChildCueInheritanceEligible)
            .Select(static category => category.Category)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in gameObject.InheritParentCueCategories)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                yield return Issue(path, "Parent cue inheritance category IDs must be nonempty.");
                continue;
            }

            var normalized = category.Trim();
            if (!seen.Add(normalized))
            {
                yield return Issue(path, $"Parent cue inheritance category '{normalized}' is listed more than once.");
            }
            else if (!eligibleCategories.Contains(normalized))
            {
                yield return Issue(path, $"Parent cue inheritance category '{normalized}' is not eligible in the installed catalog.");
            }
        }
    }

    private ValidationIssue Issue(string path, string message) =>
        new(Metadata.RuleId, Metadata.DefaultSeverity, path, message);
}
