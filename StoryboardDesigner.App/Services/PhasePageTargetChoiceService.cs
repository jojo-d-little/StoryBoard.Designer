using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

internal sealed record PhasePageTargetChoice(PhaseNode? Page, string PhaseKey, string DisplayName, bool IsKeyUnique, bool IsStoredInvalid = false);

internal static class PhasePageTargetChoiceService
{
    internal static IReadOnlyList<PhasePageTargetChoice> BuildChoices(ProjectModel project)
    {
        var pages = new List<(PhaseNode Page, string Hierarchy)>();
        foreach (var book in project.PhaseBooks)
        {
            CollectPages(book, string.Empty, pages);
        }

        var keyCounts = pages
            .Where(static item => !string.IsNullOrWhiteSpace(item.Page.PhaseKey))
            .GroupBy(static item => item.Page.PhaseKey, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        return pages.Select(item =>
        {
            var key = item.Page.PhaseKey?.Trim() ?? string.Empty;
            var isUnique = !string.IsNullOrWhiteSpace(key)
                && keyCounts.TryGetValue(key, out var count)
                && count == 1;
            var keyLabel = string.IsNullOrWhiteSpace(key) ? "(phaseKey missing)" : key;
            var invalidLabel = string.IsNullOrWhiteSpace(key)
                ? " — missing phaseKey"
                : isUnique ? string.Empty : " — duplicate phaseKey";
            return new PhasePageTargetChoice(
                item.Page,
                key,
                $"{item.Hierarchy} [{keyLabel}]{invalidLabel}",
                isUnique);
        }).ToList();
    }

    internal static IReadOnlyList<PhasePageTargetChoice> BuildEditorChoices(ProjectModel project, string storedPhaseKey)
    {
        var choices = BuildChoices(project).ToList();
        var key = storedPhaseKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return choices;
        }

        var matches = choices.Count(choice => string.Equals(choice.PhaseKey, key, StringComparison.Ordinal));
        if (matches == 1 && choices.Single(choice => string.Equals(choice.PhaseKey, key, StringComparison.Ordinal)).IsKeyUnique)
        {
            return choices;
        }

        var invalidDisplay = matches == 0
            ? $"⚠ Stored target is unresolved: {key}"
            : $"⚠ Stored target is ambiguous: {key} matches {matches} Pages";
        choices.Insert(0, new PhasePageTargetChoice(null, key, invalidDisplay, false, IsStoredInvalid: true));
        return choices;
    }

    internal static int CountMatches(ProjectModel project, string phaseKey)
    {
        var count = 0;
        foreach (var book in project.PhaseBooks)
        {
            Count(book);
        }

        return count;

        void Count(PhaseNode node)
        {
            if (node.Tier == PhaseTier.Page
                && string.Equals(node.PhaseKey, phaseKey, StringComparison.Ordinal))
            {
                count++;
            }

            foreach (var child in node.Children)
            {
                Count(child);
            }
        }
    }

    private static void CollectPages(PhaseNode node, string parentPath, ICollection<(PhaseNode Page, string Hierarchy)> pages)
    {
        var name = string.IsNullOrWhiteSpace(node.DisplayName)
            ? string.IsNullOrWhiteSpace(node.PhaseKey) ? "(unnamed)" : node.PhaseKey
            : node.DisplayName;
        var path = string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath} › {name}";
        if (node.Tier == PhaseTier.Page)
        {
            pages.Add((node, path));
            return;
        }

        foreach (var child in node.Children)
        {
            CollectPages(child, path, pages);
        }
    }
}
