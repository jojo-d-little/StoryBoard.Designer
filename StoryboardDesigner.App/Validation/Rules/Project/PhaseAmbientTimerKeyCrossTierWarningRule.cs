using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class PhaseAmbientTimerKeyCrossTierWarningRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-019",
        Title: "Phase Ambient Timer Key Cross-Tier Reuse",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Phases");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel project)
        {
            yield break;
        }

        var usagesByTimerKey = EnumeratePhaseAmbientTimerUsages(project)
            .GroupBy(static usage => usage.TimerKey, StringComparer.OrdinalIgnoreCase);

        foreach (var usageGroup in usagesByTimerKey)
        {
            var distinctTiers = usageGroup
                .Select(static usage => usage.Tier)
                .Distinct()
                .OrderBy(static tier => TierSortOrder(tier))
                .ToList();
            if (distinctTiers.Count <= 1)
            {
                continue;
            }

            var locations = usageGroup
                .OrderBy(static usage => usage.Path, StringComparer.OrdinalIgnoreCase)
                .Select(static usage => $"{usage.Path} ({usage.Tier})")
                .ToList();

            var previewLocations = locations.Take(4).ToList();
            var locationSummary = string.Join("; ", previewLocations);
            if (locations.Count > previewLocations.Count)
            {
                locationSummary += $"; +{locations.Count - previewLocations.Count} more";
            }

            var tierSummary = string.Join(", ", distinctTiers);
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Global / Phases",
                $"phaseAmbientTimerKey '{usageGroup.Key}' is used across multiple phase tiers ({tierSummary}). This can cause inherited ambience timers to remain running when a new tier becomes active if timer keys are reused across tiers. Locations: {locationSummary}",
                "Use distinct timer keys per phase tier (book/chapter/page) to keep ambience start/stop lifecycle deterministic.");
        }
    }

    private static IEnumerable<PhaseAmbientTimerUsage> EnumeratePhaseAmbientTimerUsages(ProjectModel project)
    {
        foreach (var book in project.PhaseBooks)
        {
            foreach (var usage in EnumeratePhaseAmbientTimerUsages(book, "Global / Phases"))
            {
                yield return usage;
            }
        }
    }

    private static IEnumerable<PhaseAmbientTimerUsage> EnumeratePhaseAmbientTimerUsages(PhaseNode node, string parentPath)
    {
        var nodeLabel = ResolveNodeLabel(node);
        var path = $"{parentPath} / {node.Tier}:{nodeLabel}";
        var timerKey = node.PhaseAmbientTimerKey?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(timerKey))
        {
            yield return new PhaseAmbientTimerUsage(timerKey, node.Tier, path);
        }

        foreach (var child in node.Children)
        {
            foreach (var usage in EnumeratePhaseAmbientTimerUsages(child, path))
            {
                yield return usage;
            }
        }
    }

    private static string ResolveNodeLabel(PhaseNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.DisplayName))
        {
            return node.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(node.PhaseKey))
        {
            return node.PhaseKey.Trim();
        }

        return node.Id.ToString("D");
    }

    private static int TierSortOrder(PhaseTier tier)
    {
        return tier switch
        {
            PhaseTier.Book => 0,
            PhaseTier.Chapter => 1,
            _ => 2
        };
    }

    private sealed record PhaseAmbientTimerUsage(
        string TimerKey,
        PhaseTier Tier,
        string Path);
}
