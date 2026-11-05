using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Sequences;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed record SequenceDefinitionIssue(
    Guid? StepId,
    string Description,
    string Hint,
    string? Route = null,
    Guid? TargetStepId = null)
{
    public string RouteTargetLabel => Route is null
        ? string.Empty
        : TargetStepId is Guid targetId ? $"Route: {Route} → {targetId:D}" : $"Route: {Route}";
}

public static class SequenceDefinitionIntegrityEvaluator
{
    private const string MainBranchId = "main";

    public static IReadOnlyList<SequenceDefinitionIssue> Evaluate(ProjectSequenceDefinitionDto sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        var issues = new List<SequenceDefinitionIssue>();
        var steps = sequence.Steps ?? new List<ProjectSequenceStepDto>();
        if (string.IsNullOrWhiteSpace(sequence.DisplayName))
        {
            issues.Add(new SequenceDefinitionIssue(null, "Sequence name is required.", "Give the sequence a name."));
        }

        var branches = sequence.Branches ?? new List<ProjectSequenceBranchDto>();
        var branchesById = branches
            .Where(static branch => !string.IsNullOrWhiteSpace(branch.BranchId))
            .GroupBy(static branch => branch.BranchId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);
        if (!branchesById.TryGetValue(MainBranchId, out var mainBranchDefinitions) || mainBranchDefinitions.Count != 1)
        {
            issues.Add(new SequenceDefinitionIssue(null, "Sequence must define exactly one main branch.", "Add one main branch definition with ID 'main'."));
        }

        foreach (var branchGroup in branchesById.Values.Where(static group => group.Count > 1))
        {
            issues.Add(new SequenceDefinitionIssue(null, $"Branch ID '{branchGroup[0].BranchId}' is defined more than once.", "Keep each branch ID unique within its sequence."));
        }

        foreach (var branch in branches)
        {
            if (string.IsNullOrWhiteSpace(branch.BranchId) || branch.BranchId != branch.BranchId.Trim() || branch.BranchId.Any(char.IsWhiteSpace))
            {
                issues.Add(new SequenceDefinitionIssue(null, "Sequence contains a branch with an invalid ID.", "Assign a stable, non-empty branch ID without whitespace."));
            }
            if (string.IsNullOrWhiteSpace(branch.DisplayName))
            {
                issues.Add(new SequenceDefinitionIssue(null, $"Branch '{branch.BranchId}' has no display name.", "Give every branch a name."));
            }
            if (!IsValidDisplayColor(branch.DisplayColor))
            {
                issues.Add(new SequenceDefinitionIssue(null, $"Branch '{branch.BranchId}' has an invalid display color.", "Use a six-digit #RRGGBB color."));
            }
        }

        foreach (var nameGroup in branches
                     .Where(static branch => !string.IsNullOrWhiteSpace(branch.DisplayName))
                     .GroupBy(static branch => branch.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() > 1))
        {
            issues.Add(new SequenceDefinitionIssue(null, $"Branch display name '{nameGroup.Key}' is used more than once.", "Give each branch a distinct display name."));
        }

        if (steps.Count == 0)
        {
            issues.Add(new SequenceDefinitionIssue(null, "Sequence has no steps.", "Add an action or sound effect step."));
            return issues;
        }

        // Labels and ties are stable across saves and never participate in flow.
        var visualSteps = SequenceVisualOrdering.OrderSteps(steps).ToList();
        var labels = visualSteps.Select((step, index) => (step.StepId, Number: index + 1))
            .Where(static item => item.StepId != Guid.Empty)
            .GroupBy(static item => item.StepId)
            .ToDictionary(static group => group.Key, static group => group.First().Number);
        string DescribeStep(ProjectSequenceStepDto step)
        {
            var label = labels.TryGetValue(step.StepId, out var number) ? $"Step {number}" : "Step without an ID";
            var branchName = !string.IsNullOrWhiteSpace(step.BranchId)
                && branchesById.TryGetValue(step.BranchId, out var matches)
                && !string.IsNullOrWhiteSpace(matches[0].DisplayName)
                    ? matches[0].DisplayName.Trim()
                    : string.IsNullOrWhiteSpace(step.BranchId) ? "(no branch)" : step.BranchId;
            var identity = step.StepType == SequenceStepType.Sound
                ? $"Sound {step.SoundEffectId?.ToString("D") ?? "(unassigned)"}"
                : step.InlineAction?.Name is { Length: > 0 } inlineName
                    ? $"Action '{inlineName}'"
                    : $"Action {step.ActionId?.ToString("D") ?? "(unassigned)"}";
            return $"{label} {identity} (ID {step.StepId:D}, branch '{branchName}', {step.OffsetMs:N0} ms)";
        }

        var duplicateIds = steps.Where(static step => step.StepId != Guid.Empty)
            .GroupBy(static step => step.StepId).Where(static group => group.Count() > 1)
            .Select(static group => group.Key).ToHashSet();
        var stepsById = steps.Where(static step => step.StepId != Guid.Empty)
            .GroupBy(static step => step.StepId).Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single());

        foreach (var step in visualSteps)
        {
            var label = DescribeStep(step);
            if (step.StepId == Guid.Empty)
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} has no step ID.", "Recreate this step so it receives a unique ID."));
            else if (duplicateIds.Contains(step.StepId))
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} duplicates another step ID.", "Each step must have a unique ID."));

            if (string.IsNullOrWhiteSpace(step.BranchId) || step.BranchId != step.BranchId.Trim() || step.BranchId.Any(char.IsWhiteSpace))
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} has an invalid branch ID.", "Assign a stable, non-empty branch ID without whitespace."));
            else if (!branchesById.ContainsKey(step.BranchId))
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} references an undefined branch.", "Create a branch definition for this step's branch ID."));

            if (step.OffsetMs < 0)
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} has a negative time offset.", "Use zero or a later time."));
            else if (step.OffsetMs % 50 != 0)
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} is not aligned to a 50 ms tick.", "Use an offset that is a multiple of 50 ms."));

            if (step.StepType == SequenceStepType.Action)
            {
                if (step.SoundEffectId.HasValue)
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} is an action step with sound data.", "Clear the sound effect from this action step."));
                var hasExistingAction = step.ActionId is Guid actionId && actionId != Guid.Empty;
                if (hasExistingAction == (step.InlineAction is not null))
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} must use exactly one action source.", "Choose an existing action or configure an inline action."));
                else if (step.InlineAction is not null && string.IsNullOrWhiteSpace(step.InlineAction.Name))
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} has an unnamed action.", "Give the action a name."));
            }
            else if (step.StepType == SequenceStepType.Sound)
            {
                if (step.ActionId.HasValue || step.InlineAction is not null)
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} is a sound step with action data.", "Clear the action reference and inline action."));
                if (!step.SoundEffectId.HasValue || step.SoundEffectId.Value == Guid.Empty)
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label} has no sound effect selected.", "Choose a sound effect for this step."));
            }

            var mode = step.ContinuationMode ?? ContinuationMode.Continue;
            switch (mode)
            {
                case ContinuationMode.Continue:
                    if (step.OnSuccessStepId.HasValue || step.OnFailureStepId.HasValue || step.MergeTargetStepId.HasValue)
                        AddIssue("Continue mode cannot have branch or merge targets.", "Clear its success, failure, and merge targets.");
                    ValidateTarget(step, step.OnContinueStepId, "Continue");
                    break;
                case ContinuationMode.Branch:
                    if (step.StepType != SequenceStepType.Action)
                        AddIssue("Only an action step can use Branch mode.", "Choose Continue, Merge, or End for a sound step.");
                    if (step.OnContinueStepId.HasValue || step.MergeTargetStepId.HasValue)
                        AddIssue("Branch mode cannot have Continue or sound merge targets.", "Clear its Continue and merge targets.");
                    ValidateTarget(step, step.OnSuccessStepId, "Success");
                    ValidateTarget(step, step.OnFailureStepId, "Failure");
                    break;
                case ContinuationMode.Merge when step.StepType == SequenceStepType.Action:
                    if (step.OnContinueStepId.HasValue || step.MergeTargetStepId.HasValue)
                        AddIssue("An action merge uses success and failure targets.", "Clear its Continue and sound merge targets.");
                    ValidateTarget(step, step.OnSuccessStepId, "Merge success");
                    ValidateTarget(step, step.OnFailureStepId, "Merge failure");
                    break;
                case ContinuationMode.Merge when step.StepType == SequenceStepType.Sound:
                    if (step.OnContinueStepId.HasValue || step.OnSuccessStepId.HasValue || step.OnFailureStepId.HasValue)
                        AddIssue("A sound merge uses one unconditional merge target.", "Clear Continue, success, and failure targets.");
                    ValidateTarget(step, step.MergeTargetStepId, "Merge");
                    break;
                case ContinuationMode.End:
                    if (step.OnContinueStepId.HasValue || step.OnSuccessStepId.HasValue || step.OnFailureStepId.HasValue || step.MergeTargetStepId.HasValue)
                        AddIssue("End mode cannot have successor targets.", "Clear all successor targets.");
                    break;
                default:
                    AddIssue("Step has an unsupported continuation mode.", "Choose Continue, Branch, Merge, or End.");
                    break;
            }

            void AddIssue(string description, string hint) => issues.Add(new SequenceDefinitionIssue(step.StepId, $"{label}: {description}", hint));
        }

        var edges = stepsById.Keys.ToDictionary(static id => id, static _ => new HashSet<Guid>());
        void AddEdge(Guid source, Guid? target)
        {
            if (target is Guid id && stepsById.ContainsKey(id)) edges[source].Add(id);
        }
        foreach (var step in stepsById.Values)
        {
            switch (step.ContinuationMode ?? ContinuationMode.Continue)
            {
                case ContinuationMode.Continue: AddEdge(step.StepId, step.OnContinueStepId); break;
                case ContinuationMode.Branch:
                case ContinuationMode.Merge when step.StepType == SequenceStepType.Action:
                    AddEdge(step.StepId, step.OnSuccessStepId);
                    AddEdge(step.StepId, step.OnFailureStepId);
                    break;
                case ContinuationMode.Merge when step.StepType == SequenceStepType.Sound:
                    AddEdge(step.StepId, step.MergeTargetStepId);
                    break;
            }
        }

        var incoming = stepsById.Keys.ToDictionary(static id => id, static _ => 0);
        foreach (var targets in edges.Values)
            foreach (var target in targets) incoming[target]++;
        var roots = incoming.Where(static item => item.Value == 0).Select(static item => item.Key).ToList();
        if (roots.Count != 1)
        {
            issues.Add(new SequenceDefinitionIssue(null,
                roots.Count == 0 ? "Sequence flow has no root step." : $"Sequence flow has {roots.Count} root steps.",
                "Connect every flow path so it has exactly one entry step."));
        }
        else if (stepsById.TryGetValue(roots[0], out var root) && !string.Equals(root.BranchId, MainBranchId, StringComparison.Ordinal))
        {
            issues.Add(new SequenceDefinitionIssue(root.StepId, $"{DescribeStep(root)} is the flow root, but it is not on Main.", "Move the entry step onto the Main branch."));
        }

        var reachable = new HashSet<Guid>();
        if (roots.Count == 1)
        {
            var pending = new Stack<Guid>();
            pending.Push(roots[0]);
            while (pending.TryPop(out var id))
            {
                if (!reachable.Add(id) || !edges.TryGetValue(id, out var targets)) continue;
                foreach (var target in targets) pending.Push(target);
            }
        }
        foreach (var step in steps.Where(step => step.StepId != Guid.Empty && !reachable.Contains(step.StepId)))
        {
            issues.Add(new SequenceDefinitionIssue(step.StepId, $"{DescribeStep(step)} is unreachable from the flow root.", "Connect it to the reachable flow or remove it."));
        }

        var inDegree = incoming.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        var ready = new Queue<Guid>(inDegree.Where(static pair => pair.Value == 0).Select(static pair => pair.Key));
        var visited = 0;
        while (ready.TryDequeue(out var id))
        {
            visited++;
            foreach (var target in edges[id])
                if (--inDegree[target] == 0) ready.Enqueue(target);
        }
        if (visited != stepsById.Count)
            issues.Add(new SequenceDefinitionIssue(null, "Sequence flow contains a cycle.", "Adjust the route targets so every path can progress without looping."));

        return issues;

        void ValidateTarget(ProjectSequenceStepDto source, Guid? targetId, string route)
        {
            if (!targetId.HasValue) return;
            if (targetId.Value == Guid.Empty || !stepsById.TryGetValue(targetId.Value, out var target))
            {
                issues.Add(new SequenceDefinitionIssue(source.StepId,
                    $"{DescribeStep(source)} {route} route points to a missing step ({targetId.Value:D}).",
                    "Choose an existing step or remove this route.", route, targetId));
            }
        }
    }

    private static bool IsValidDisplayColor(string? value) => value is { Length: 7 }
        && value[0] == '#'
        && value.AsSpan(1).ToArray().All(Uri.IsHexDigit);
}
