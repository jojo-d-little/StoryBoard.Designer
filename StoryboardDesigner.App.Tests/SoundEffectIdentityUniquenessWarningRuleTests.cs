using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SoundEffectIdentityUniquenessWarningRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenSoundEffectIdsAreDuplicatedAcrossScopes()
    {
        var duplicateId = Guid.NewGuid();
        var project = new ProjectModel
        {
            Name = "SoundIdentityProject",
            Planets =
            [
                new Planet
                {
                    Name = "Planet A"
                }
            ]
        };

        project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = duplicateId,
            SoundEffectKey = "ui.click",
            DisplayName = "UI Click",
            AssetRef = "SFX/UI_Click.wav",
            RepeatMode = "None"
        });

        project.Planets[0].SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = duplicateId,
            SoundEffectKey = "amb.wind",
            DisplayName = "Wind",
            AssetRef = "SFX/Ambient_Wind.wav",
            RepeatMode = "None"
        });

        var result = Evaluate(project);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-014", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Duplicate soundEffectId", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenSoundEffectKeysAreDuplicatedCaseInsensitive()
    {
        var project = new ProjectModel
        {
            Name = "SoundIdentityProject",
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area A",
                                    Rooms = [new Room { Name = "Room A" }]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = "ambient.rain",
            DisplayName = "Rain",
            AssetRef = "SFX/Rain.wav",
            RepeatMode = "Loop"
        });

        project.Planets[0].Countries[0].Areas[0].Rooms[0].SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = "  AMBIENT.RAIN  ",
            DisplayName = "Heavy Rain",
            AssetRef = "SFX/HeavyRain.wav",
            RepeatMode = "Loop"
        });

        var result = Evaluate(project);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-014", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Duplicate soundEffectKey", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenSoundEffectIdsAndKeysAreUnique()
    {
        var project = new ProjectModel
        {
            Name = "SoundIdentityProject",
            Planets = [new Planet { Name = "Planet A" }]
        };

        project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = "global.ui.click",
            DisplayName = "UI Click",
            AssetRef = "SFX/UI_Click.wav",
            RepeatMode = "None"
        });

        project.Planets[0].SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = "planet.amb.wind",
            DisplayName = "Wind",
            AssetRef = "SFX/Wind.wav",
            RepeatMode = "None"
        });

        var result = Evaluate(project);

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-014", result.ExecutedRuleIds);
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new SoundEffectIdentityUniquenessWarningRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }
}