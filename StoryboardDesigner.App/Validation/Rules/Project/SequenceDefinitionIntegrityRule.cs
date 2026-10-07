using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed record SequenceDefinitionIssue(Guid? StepId, string Description, string Hint);

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
            issues.Add(new SequenceDefinitionIssue(null, "Sequence must define exactly one main branch.", "Add the main branch definition with ID 'main'."));
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

        var duplicateIds = steps
            .Where(static step => step.StepId != Guid.Empty)
            .GroupBy(static step => step.StepId)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var stepsById = steps
            .Where(static step => step.StepId != Guid.Empty)
            .GroupBy(static step => step.StepId)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single());
        var stepsByBranch = steps
            .Where(static step => !string.IsNullOrWhiteSpace(step.BranchId))
            .GroupBy(static step => step.BranchId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static step => step.OffsetMs).ThenBy(static step => step.Order).ToList(),
                StringComparer.Ordinal);

        foreach (var (step, index) in steps.Select((step, index) => (step, index)))
        {
            var stepLabel = $"Step {index + 1}";
            if (step.StepId == Guid.Empty)
            {
                issues.Add(new SequenceDefinitionIssue(null, $"{stepLabel} has no step ID.", "Recreate this step so it receives a unique ID."));
            }
            else if (duplicateIds.Contains(step.StepId))
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} duplicates another step ID.", "Each step must have a unique ID."));
            }

            if (string.IsNullOrWhiteSpace(step.BranchId) || step.BranchId != step.BranchId.Trim() || step.BranchId.Any(char.IsWhiteSpace))
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} has an invalid branch ID.", "Assign a stable, non-empty branch ID without whitespace."));
            }
            else if (!branchesById.ContainsKey(step.BranchId))
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} references an undefined branch.", "Create a branch definition for this step's branch ID."));
            }

            if (step.OffsetMs < 0)
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} has a negative time offset.", "Use zero or a later time."));
            }
            else if (step.OffsetMs % 50 != 0)
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} is not aligned to a 50 ms tick.", "Use an offset that is a multiple of 50 ms."));
            }

            if (step.Order < 0)
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} has a negative order within its time slot.", "Use zero or a later order."));
            }

            if (step.StepType == SequenceStepType.Action)
            {
                if (step.SoundEffectId.HasValue)
                {
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} is an action step with sound data.", "Clear the sound effect from this action step."));
                }

                var hasExistingAction = step.ActionId is Guid actionId && actionId != Guid.Empty;
                if (hasExistingAction == (step.InlineAction is not null))
                {
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} must use exactly one action source.", "Choose an existing action or configure an inline action."));
                }
                else if (step.InlineAction is not null && string.IsNullOrWhiteSpace(step.InlineAction.Name))
                {
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} has an unnamed action.", "Give the action a name."));
                }
            }
            else if (step.StepType == SequenceStepType.Sound)
            {
                if (step.ActionId.HasValue || step.InlineAction is not null)
                {
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} is a sound step with action data.", "Clear the action reference and inline action."));
                }

                if (!step.SoundEffectId.HasValue || step.SoundEffectId.Value == Guid.Empty)
                {
                    issues.Add(new SequenceDefinitionIssue(step.StepId, $"{stepLabel} has no sound effect selected.", "Choose a sound effect for this step."));
                }
            }

            var mode = step.ContinuationMode ?? ContinuationMode.Continue;
            var hasSuccess = step.OnSuccessStepId.HasValue;
            var hasFailure = step.OnFailureStepId.HasValue;
            var hasMergeTarget = step.MergeTargetStepId.HasValue;
            switch (mode)
            {
                case ContinuationMode.Continue:
                    if (hasSuccess || hasFailure || hasMergeTarget)
                    {
                        AddIssue("Continue mode cannot have explicit targets.", "Clear its success, failure, and merge targets.");
                    }
                    break;

                case ContinuationMode.Branch:
                    if (step.StepType != SequenceStepType.Action)
                    {
                        AddIssue("Only an action step can use Branch mode.", "Choose Continue, Merge, or End for a sound step.");
                    }
                    if (hasMergeTarget)
                    {
                        AddIssue("Branch mode cannot have a merge target.", "Clear the merge target.");
                    }
                    if (!hasSuccess && !hasFailure)
                    {
                        AddIssue("Branch mode needs at least one outcome target.", "Choose the first step on a different branch for success or failure; an unset outcome ends the run.");
                    }

                    var successTarget = ValidateTarget(step, step.OnSuccessStepId, "success branch", requireDifferentBranch: true, requireNewBranchEntry: true);
                    var failureTarget = ValidateTarget(step, step.OnFailureStepId, "failure branch", requireDifferentBranch: true, requireNewBranchEntry: true);
                    if (successTarget is not null && failureTarget is not null
                        && (ReferenceEquals(successTarget, failureTarget)
                            || string.Equals(successTarget.BranchId, failureTarget.BranchId, StringComparison.Ordinal)))
                    {
                        AddIssue("Success and failure must begin on separate branches.", "Assign each outcome its own branch and first step.");
                    }
                    break;

                case ContinuationMode.Merge when step.StepType == SequenceStepType.Action:
                    if (hasMergeTarget)
                    {
                        AddIssue("An action merge uses its success and failure targets, not the sound merge target field.", "Clear the merge target field.");
                    }
                    if (string.Equals(step.BranchId, MainBranchId, StringComparison.Ordinal))
                    {
                        AddIssue("An action merge must start on a non-main branch.", "Move this action onto a branch before merging.");
                    }
                    if (!hasSuccess && !hasFailure)
                    {
                        AddIssue("Action Merge mode needs at least one outcome target.", "Choose a merge target for success or failure; an unset outcome ends the run.");
                    }
                    ValidateTarget(step, step.OnSuccessStepId, "success merge", requireDifferentBranch: true, requireNewBranchEntry: false, allowSameTimeTarget: true);
                    ValidateTarget(step, step.OnFailureStepId, "failure merge", requireDifferentBranch: true, requireNewBranchEntry: false, allowSameTimeTarget: true);
                    break;

                case ContinuationMode.Merge when step.StepType == SequenceStepType.Sound:
                    if (hasSuccess || hasFailure)
                    {
                        AddIssue("A sound merge uses one unconditional merge target.", "Clear its success and failure targets.");
                    }
                    if (string.Equals(step.BranchId, MainBranchId, StringComparison.Ordinal))
                    {
                        AddIssue("A sound merge must start on a non-main branch.", "Move this sound onto a branch before merging.");
                    }
                    if (!hasMergeTarget)
                    {
                        AddIssue("Sound Merge mode needs a target.", "Choose a same-time or future step on another branch.");
                    }
                    ValidateTarget(step, step.MergeTargetStepId, "sound merge", requireDifferentBranch: true, requireNewBranchEntry: false, allowSameTimeTarget: true);
                    break;

                case ContinuationMode.End:
                    if (hasSuccess || hasFailure || hasMergeTarget)
                    {
                        AddIssue("End mode cannot have successor targets.", "Clear its success, failure, and merge targets.");
                    }
                    break;

                default:
                    AddIssue("Step has an unsupported continuation mode.", "Choose Continue, Branch, Merge, or End.");
                    break;
            }

            void AddIssue(string description, string hint) => issues.Add(new SequenceDefinitionIssue(step.StepId, description, hint));
        }

        foreach (var timeGroup in steps.GroupBy(static step => step.OffsetMs))
        {
            foreach (var orderGroup in timeGroup.GroupBy(static step => step.Order).Where(static group => group.Count() > 1))
            {
                foreach (var step in orderGroup)
                {
                    issues.Add(new SequenceDefinitionIssue(step.StepId, "Two or more steps in this time slot share the same order.", "Give the steps distinct order values."));
                }
            }
        }

        if (!stepsByBranch.TryGetValue(MainBranchId, out var mainSteps) || mainSteps.Count == 0)
        {
            issues.Add(new SequenceDefinitionIssue(null, "Sequence has no main-branch entry step.", "Place the sequence's first step on the main branch."));
        }
        else
        {
            var reachable = new HashSet<Guid>();
            var pending = new Queue<ProjectSequenceStepDto>();
            pending.Enqueue(mainSteps[0]);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (current.StepId == Guid.Empty || !reachable.Add(current.StepId))
                {
                    continue;
                }

                var mode = current.ContinuationMode ?? ContinuationMode.Continue;
                if (mode == ContinuationMode.Continue)
                {
                    if (!string.IsNullOrWhiteSpace(current.BranchId)
                        && stepsByBranch.TryGetValue(current.BranchId, out var branchSteps))
                    {
                        var currentIndex = branchSteps.IndexOf(current);
                        if (currentIndex >= 0 && currentIndex + 1 < branchSteps.Count)
                        {
                            pending.Enqueue(branchSteps[currentIndex + 1]);
                        }
                    }
                }
                else if (current.StepType == SequenceStepType.Action
                         && mode is ContinuationMode.Branch or ContinuationMode.Merge)
                {
                    EnqueueTarget(current.OnSuccessStepId);
                    EnqueueTarget(current.OnFailureStepId);
                }
                else if (current.StepType == SequenceStepType.Sound
                         && mode == ContinuationMode.Merge)
                {
                    EnqueueTarget(current.MergeTargetStepId);
                }
            }

            foreach (var step in steps.Where(step => step.StepId != Guid.Empty && !reachable.Contains(step.StepId)))
            {
                issues.Add(new SequenceDefinitionIssue(step.StepId, "Step is unreachable from the sequence's main-branch entry.", "Connect it to a reachable branch or remove it."));
            }

            void EnqueueTarget(Guid? targetId)
            {
                if (targetId.HasValue && stepsById.TryGetValue(targetId.Value, out var target))
                {
                    pending.Enqueue(target);
                }
            }
        }

        if (ContainsFlowCycle())
        {
            issues.Add(new SequenceDefinitionIssue(null, "Sequence flow contains a cycle.", "Adjust the outcome and merge targets so every path can progress without looping."));
        }

        return issues;

        ProjectSequenceStepDto? ValidateTarget(
            ProjectSequenceStepDto source,
            Guid? targetId,
            string route,
            bool requireDifferentBranch,
            bool requireNewBranchEntry,
            bool allowSameTimeTarget = false)
        {
            if (!targetId.HasValue)
            {
                return null;
            }

            if (targetId.Value == Guid.Empty || !stepsById.TryGetValue(targetId.Value, out var target))
            {
                issues.Add(new SequenceDefinitionIssue(source.StepId, $"Step route '{route}' points to a missing step.", "Choose an existing step or end this outcome."));
                return null;
            }

            var isLater = target.OffsetMs > source.OffsetMs
                || target.OffsetMs == source.OffsetMs && (allowSameTimeTarget || target.Order > source.Order);
            if (!isLater)
            {
                issues.Add(new SequenceDefinitionIssue(source.StepId, $"Step route '{route}' does not move forward in authored time.", allowSameTimeTarget
                    ? "Choose a step at the same or a later offset."
                    : "Choose a step at a later offset or order."));
            }

            if (requireDifferentBranch && string.Equals(target.BranchId, source.BranchId, StringComparison.Ordinal))
            {
                issues.Add(new SequenceDefinitionIssue(source.StepId, $"Step route '{route}' targets its current branch.", "Choose the first step on a different branch."));
            }

            if (requireNewBranchEntry
                && !string.IsNullOrWhiteSpace(target.BranchId)
                && stepsByBranch.TryGetValue(target.BranchId, out var targetBranch)
                && !ReferenceEquals(targetBranch.FirstOrDefault(), target))
            {
                issues.Add(new SequenceDefinitionIssue(source.StepId, $"Step route '{route}' does not target the first step on its branch.", "Choose the first authored step on the target branch."));
            }

            return target;
        }

        bool ContainsFlowCycle()
        {
            if (stepsById.Count == 0)
            {
                return false;
            }

            var edges = stepsById.Keys.ToDictionary(static id => id, static _ => new HashSet<Guid>());
            var inDegree = stepsById.Keys.ToDictionary(static id => id, static _ => 0);

            void AddEdge(Guid sourceId, Guid? targetId)
            {
                if (targetId is Guid target && stepsById.ContainsKey(target) && edges[sourceId].Add(target))
                {
                    inDegree[target]++;
                }
            }

            foreach (var step in stepsById.Values)
            {
                var mode = step.ContinuationMode ?? ContinuationMode.Continue;
                if (mode == ContinuationMode.Continue
                    && !string.IsNullOrWhiteSpace(step.BranchId)
                    && stepsByBranch.TryGetValue(step.BranchId, out var branchSteps))
                {
                    var index = branchSteps.IndexOf(step);
                    if (index >= 0 && index + 1 < branchSteps.Count)
                    {
                        AddEdge(step.StepId, branchSteps[index + 1].StepId);
                    }
                }
                else if (step.StepType == SequenceStepType.Action
                         && mode is ContinuationMode.Branch or ContinuationMode.Merge)
                {
                    AddEdge(step.StepId, step.OnSuccessStepId);
                    AddEdge(step.StepId, step.OnFailureStepId);
                }
                else if (step.StepType == SequenceStepType.Sound && mode == ContinuationMode.Merge)
                {
                    AddEdge(step.StepId, step.MergeTargetStepId);
                }
            }

            var ready = new Queue<Guid>(inDegree.Where(static pair => pair.Value == 0).Select(static pair => pair.Key));
            var visited = 0;
            while (ready.Count > 0)
            {
                var current = ready.Dequeue();
                visited++;
                foreach (var target in edges[current])
                {
                    if (--inDegree[target] == 0)
                    {
                        ready.Enqueue(target);
                    }
                }
            }

            return visited != stepsById.Count;
        }
    }

    private static bool IsValidDisplayColor(string? value) => value is { Length: 7 }
        && value[0] == '#'
        && value.AsSpan(1).ToArray().All(Uri.IsHexDigit);
}

public sealed class SequenceDefinitionIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        Enum.GetValues<ScopeNodeKind>().ToHashSet();

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "SEQ-001",
        Title: "Authored Sequence Integrity",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Sequences");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scopePath = EventSubscriptionRuleSupport.BuildScopePath(context.CandidateNode);
        foreach (var sequence in context.CandidateNode.SequenceDefinitions ?? new List<ProjectSequenceDefinitionDto>())
        {
            var sequenceName = string.IsNullOrWhiteSpace(sequence.DisplayName) ? "(unnamed sequence)" : sequence.DisplayName.Trim();
            var sequencePath = $"{scopePath} / Sequence[{sequence.SequenceId:N}] '{sequenceName}'";
            foreach (var issue in SequenceDefinitionIntegrityEvaluator.Evaluate(sequence))
            {
                var issuePath = issue.StepId.HasValue
                    ? $"{sequencePath} / Step {issue.StepId.Value:N}"
                    : sequencePath;
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    issuePath,
                    issue.Description,
                    issue.Hint);
            }
        }
    }
}
