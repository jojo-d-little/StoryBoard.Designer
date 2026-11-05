using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models.Sequences;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SequenceVisualOrderingTests
{
    [Fact]
    public void CreatePresentation_KeepsDuplicateAndEmptyIdCardsVisibleWithValidationFindings()
    {
        var duplicateId = Guid.NewGuid();
        var firstDuplicate = Sound(duplicateId, 0);
        var secondDuplicate = Sound(duplicateId, 100);
        var missingId = Sound(Guid.Empty, 200);
        var sequence = new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = "Malformed sequence",
            Steps = [firstDuplicate, secondDuplicate, missingId],
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

        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(sequence);
        var cards = SequenceVisualOrdering.CreatePresentation(sequence.Steps!, issues);

        Assert.Equal(3, cards.Count);
        Assert.Equal(3, cards.Select(static card => card.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cards.Where(card => card.Step.StepId == duplicateId), card =>
            Assert.Contains("duplicate ID instance", card.Label, StringComparison.Ordinal));
        Assert.All(cards.Where(card => card.Step.StepId == duplicateId), card =>
            Assert.Contains(card.ValidationIssues, issue => issue.Description.Contains("duplicates another step ID", StringComparison.Ordinal)));

        var missingIdCard = Assert.Single(cards, static card => card.Step.StepId == Guid.Empty);
        Assert.Contains("missing ID", missingIdCard.Label, StringComparison.Ordinal);
        Assert.Contains(missingIdCard.ValidationIssues, issue => issue.Description.Contains("has no step ID", StringComparison.Ordinal));
    }

    private static ProjectSequenceStepDto Sound(Guid id, int offset) => new()
    {
        StepId = id,
        StepType = SequenceStepType.Sound,
        SoundEffectId = Guid.NewGuid(),
        BranchId = "main",
        ContinuationMode = ContinuationMode.End,
        OffsetMs = offset
    };
}
