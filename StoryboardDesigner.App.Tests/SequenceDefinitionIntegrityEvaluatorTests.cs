using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SequenceDefinitionIntegrityEvaluatorTests
{
    [Fact]
    public void Evaluate_UsesExplicitContinueTargetEvenWhenTargetHasEarlierOffset()
    {
        var rootId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var sequence = Definition(
            Sound(rootId, "main", ContinuationMode.Continue, 500, onContinue: targetId),
            Sound(targetId, "main", ContinuationMode.End, 0));

        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(sequence);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_AcceptsContinueStepWithoutTargetAsNaturalTerminal()
    {
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(
            Sound(Guid.NewGuid(), "main", ContinuationMode.Continue, 0)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_AcceptsBranchWithAbsentOutcomeAsNaturalTerminal()
    {
        var targetId = Guid.NewGuid();
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(
            Action(Guid.NewGuid(), "main", ContinuationMode.Branch, onSuccess: targetId),
            Sound(targetId, "main", ContinuationMode.End, 0)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_AcceptsBranchWithBothOutcomesAbsentAsNaturalTerminal()
    {
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(
            Action(Guid.NewGuid(), "main", ContinuationMode.Branch)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_AcceptsActionMergeWithAbsentOutcomeAsNaturalTerminal()
    {
        var targetId = Guid.NewGuid();
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(
            Action(Guid.NewGuid(), "main", ContinuationMode.Merge, onFailure: targetId),
            Sound(targetId, "main", ContinuationMode.End, 0)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_AcceptsActionMergeWithBothOutcomesAbsentAsNaturalTerminal()
    {
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(
            Action(Guid.NewGuid(), "main", ContinuationMode.Merge)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_AcceptsSoundMergeWithoutTargetAsNaturalTerminal()
    {
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(
            Sound(Guid.NewGuid(), "main", ContinuationMode.Merge, 0)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_ReportsDefinedButUnresolvedContinueTargetWithRouteAndCorrection()
    {
        var targetId = Guid.NewGuid();
        var step = Sound(Guid.NewGuid(), "main", ContinuationMode.Continue, 0, onContinue: targetId);
        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(Definition(step));

        var issue = Assert.Single(issues, item => item.Description.Contains("Continue route", StringComparison.Ordinal));
        Assert.Contains(targetId.ToString("D"), issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Choose an existing step", issue.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsCandidateRootsWithoutMarkingTheirReachableStepsUnreachable()
    {
        var root = Sound(Guid.NewGuid(), "main", ContinuationMode.End, 0);
        var disconnected = Sound(Guid.NewGuid(), "effects", ContinuationMode.Continue, 0, onContinue: Guid.NewGuid());
        var child = Sound(disconnected.OnContinueStepId!.Value, "effects", ContinuationMode.End, 50);
        var sequence = Definition(root, disconnected, child);
        sequence.Branches!.Add(new ProjectSequenceBranchDto
        {
            BranchId = "effects",
            DisplayName = "Effects",
            DisplayColor = "#2563EB"
        });

        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(sequence);

        Assert.Contains(issues, item => item.Description.Contains("2 root steps", StringComparison.Ordinal));
        Assert.Contains(issues, item => item.StepId == root.StepId && item.Description.Contains("possible flow root", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, item => item.StepId == disconnected.StepId && item.Description.Contains("possible flow root", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, item => item.StepId == child.StepId && item.Description.Contains("unreachable", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, item => item.Description.Contains("unreachable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_ReportsCycleAcrossExplicitContinueLinks()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var sequence = Definition(
            Sound(firstId, "main", ContinuationMode.Continue, 0, onContinue: secondId),
            Sound(secondId, "main", ContinuationMode.Continue, 0, onContinue: firstId));

        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(sequence);

        Assert.Contains(issues, item => item.Description.Contains("no root step", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, item => item.Description.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectSequenceDefinitionDto Definition(params ProjectSequenceStepDto[] steps) => new()
    {
        SequenceId = Guid.NewGuid(),
        DisplayName = "Test sequence",
        Steps = steps.ToList(),
        Branches =
        [
            new ProjectSequenceBranchDto
            {
                BranchId = "main",
                DisplayName = "Main",
                DisplayColor = "#64748B"
            }
        ]
    };

    private static ProjectSequenceStepDto Sound(Guid id, string branchId, ContinuationMode mode, int offset, Guid? onContinue = null) => new()
    {
        StepId = id,
        StepType = SequenceStepType.Sound,
        SoundEffectId = Guid.NewGuid(),
        BranchId = branchId,
        ContinuationMode = mode,
        OffsetMs = offset,
        OnContinueStepId = onContinue
    };

    private static ProjectSequenceStepDto Action(
        Guid id,
        string branchId,
        ContinuationMode mode,
        Guid? onSuccess = null,
        Guid? onFailure = null) => new()
    {
        StepId = id,
        StepType = SequenceStepType.Action,
        ActionId = Guid.NewGuid(),
        BranchId = branchId,
        ContinuationMode = mode,
        OnSuccessStepId = onSuccess,
        OnFailureStepId = onFailure
    };
}
