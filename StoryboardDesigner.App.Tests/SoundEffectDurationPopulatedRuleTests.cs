using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SoundEffectDurationPopulatedRuleTests
{
    [Fact]
    public void Evaluate_ReportsError_WhenDurationMsIsMissingOrZero()
    {
        var project = new ProjectModel
        {
            Name = "DurationValidationProject"
        };

        project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = "sound.drag.long",
            DisplayName = "Drag Long",
            AssetRef = "assets/sounds/drag_long.wav",
            RepeatMode = "None",
            DurationMs = null
        });

        var result = Evaluate(project);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-018", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("missing durationMs", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenDurationMsIsPositive()
    {
        var project = new ProjectModel
        {
            Name = "DurationValidationProject"
        };

        project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = "sound.drag.long",
            DisplayName = "Drag Long",
            AssetRef = "assets/sounds/drag_long.wav",
            RepeatMode = "None",
            DurationMs = 4947
        });

        var result = Evaluate(project);

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-018", result.ExecutedRuleIds);
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new SoundEffectDurationPopulatedRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }
}
