using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class EventSubscriptionManifestEventKeyRule : IValidationRule
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
        RuleId: "EVT-001",
        Title: "Event Key Known In Manifest",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Events");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = EventSubscriptionRuleSupport.LoadManifestIndex();
        if (manifest.EventKeys.Count == 0)
        {
            yield break;
        }

        foreach (var (subscription, _, subscriptionPath) in EventSubscriptionRuleSupport.EnumerateSubscriptions(context.CandidateNode))
        {
            var eventKey = subscription.EventKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(eventKey) || manifest.EventKeys.Contains(eventKey))
            {
                continue;
            }

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                subscriptionPath,
                $"event subscription eventKey '{eventKey}' is not defined in event-payload.manifest.json.",
                "Use a manifest-defined eventKey or update the manifest before authoring subscriptions.");
        }
    }
}
