using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Models.Sequences;

internal static class SequenceVisualOrdering
{
    internal sealed record StepPresentation(
        ProjectSequenceStepDto Step,
        string Label,
        IReadOnlyList<SequenceDefinitionIssue> ValidationIssues);

    public static IReadOnlyList<ProjectSequenceStepDto> OrderSteps(IEnumerable<ProjectSequenceStepDto> source)
    {
        var steps = source.ToList();
        var depths = new Dictionary<ProjectSequenceStepDto, int>();
        var calculating = new HashSet<ProjectSequenceStepDto>();

        int Depth(ProjectSequenceStepDto step)
        {
            if (depths.TryGetValue(step, out var known)) return known;
            if (!calculating.Add(step)) return 0;
            var parents = steps.Where(parent => parent.OffsetMs == step.OffsetMs && RoutesTo(parent, step));
            var result = parents.Any() ? parents.Max(parent => Depth(parent) + 1) : 0;
            calculating.Remove(step);
            depths[step] = result;
            return result;
        }

        return steps.OrderBy(static step => step.OffsetMs)
            .ThenBy(Depth)
            .ThenBy(static step => string.Equals(step.BranchId, "main", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(static step => step.BranchId, StringComparer.Ordinal)
            .ThenBy(static step => step.StepId)
            .ToList();
    }

    public static IReadOnlyList<StepPresentation> CreatePresentation(
        IEnumerable<ProjectSequenceStepDto> source,
        IEnumerable<SequenceDefinitionIssue> validationIssues)
    {
        var orderedSteps = OrderSteps(source);
        var issues = validationIssues.ToList();
        var idCounts = orderedSteps
            .Where(static step => step.StepId != Guid.Empty)
            .GroupBy(static step => step.StepId)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var idInstances = new Dictionary<Guid, int>();
        var presentation = new List<StepPresentation>(orderedSteps.Count);

        for (var index = 0; index < orderedSteps.Count; index++)
        {
            var step = orderedSteps[index];
            var label = $"Step {index + 1}";
            if (step.StepId == Guid.Empty)
            {
                label += " · missing ID";
            }
            else if (idCounts[step.StepId] > 1)
            {
                var instance = idInstances.GetValueOrDefault(step.StepId) + 1;
                idInstances[step.StepId] = instance;
                label += $" · duplicate ID instance {instance} of {idCounts[step.StepId]}";
            }

            presentation.Add(new StepPresentation(
                step,
                label,
                issues.Where(issue => issue.StepId == step.StepId).ToList()));
        }

        return presentation;
    }

    private static bool RoutesTo(ProjectSequenceStepDto source, ProjectSequenceStepDto target)
    {
        var mode = source.ContinuationMode ?? ContinuationMode.Continue;
        return mode switch
        {
            ContinuationMode.Continue => source.OnContinueStepId == target.StepId,
            ContinuationMode.Branch => source.OnSuccessStepId == target.StepId || source.OnFailureStepId == target.StepId,
            ContinuationMode.Merge when source.StepType == SequenceStepType.Action => source.OnSuccessStepId == target.StepId || source.OnFailureStepId == target.StepId,
            ContinuationMode.Merge => source.MergeTargetStepId == target.StepId,
            _ => false
        };
    }
}
