using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class PhaseAmbientTimerKeyCrossTierWarningRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenSameAmbientTimerKeyIsUsedAcrossBookAndChapterTiers()
    {
        var project = new ProjectModel();

        var book = new PhaseNode
        {
            Tier = PhaseTier.Book,
            PhaseKey = "book-1",
            DisplayName = "Book One",
            PhaseAmbientTimerKey = "timer.amb.shared"
        };

        var chapter = new PhaseNode
        {
            Tier = PhaseTier.Chapter,
            PhaseKey = "chapter-1",
            DisplayName = "Chapter One",
            PhaseAmbientTimerKey = "timer.amb.shared"
        };

        var page = new PhaseNode
        {
            Tier = PhaseTier.Page,
            PhaseKey = "page-1",
            DisplayName = "Page One"
        };

        Assert.True(book.AddChildScope(chapter));
        Assert.True(chapter.AddChildScope(page));
        project.PhaseBooks.Add(book);

        var result = Evaluate(project);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-019", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("timer.amb.shared", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Book", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chapter", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenAmbientTimerKeyReuseStaysWithinSingleTier()
    {
        var project = new ProjectModel();

        project.PhaseBooks.Add(new PhaseNode
        {
            Tier = PhaseTier.Book,
            PhaseKey = "book-1",
            DisplayName = "Book One",
            PhaseAmbientTimerKey = "timer.amb.shared"
        });

        project.PhaseBooks.Add(new PhaseNode
        {
            Tier = PhaseTier.Book,
            PhaseKey = "book-2",
            DisplayName = "Book Two",
            PhaseAmbientTimerKey = "timer.amb.shared"
        });

        var result = Evaluate(project);

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-019", result.ExecutedRuleIds);
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new PhaseAmbientTimerKeyCrossTierWarningRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }
}
