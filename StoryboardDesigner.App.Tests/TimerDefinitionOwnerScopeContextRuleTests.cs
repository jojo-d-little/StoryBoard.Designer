using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TimerDefinitionOwnerScopeContextRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenOwnerScopeIsNotReachableFromScopeContext()
    {
        var project = new ProjectModel
        {
            Name = "TimerOwnerScopeInvalid",
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    TimerDefinitions =
                    [
                        new RuntimeTimerDefinitionDto
                        {
                            TimerKey = "planet.room.timer",
                            ScheduleAfterMs = 100,
                            FireMode = TimerFireMode.OneShot,
                            TargetActionRef = "Act",
                            LifetimeOwnerType = TimerOwnerType.Room,
                            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
                            Enabled = true
                        }
                    ]
                }
            ]
        };

        var result = Evaluate(project);

        Assert.Contains(result.Issues, issue => issue.RuleId == "PROJ-017"
            && issue.Description.Contains("not resolvable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_WhenOwnerScopeIsReachableFromScopeContext()
    {
        var room = new Room { Name = "Room" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [room]
        };
        var country = new Country
        {
            Name = "Country",
            Areas = [area]
        };
        var planet = new Planet
        {
            Name = "Planet",
            Countries = [country],
            TimerDefinitions =
            [
                new RuntimeTimerDefinitionDto
                {
                    TimerKey = "planet.room.timer",
                    ScheduleAfterMs = 100,
                    FireMode = TimerFireMode.OneShot,
                    TargetActionRef = "Act",
                    LifetimeOwnerType = TimerOwnerType.Room,
                    ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
                    Enabled = true
                }
            ]
        };

        var project = new ProjectModel
        {
            Name = "TimerOwnerScopeValid",
            Planets = [planet]
        };

        var result = Evaluate(project);

        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "PROJ-017");
        Assert.Contains("PROJ-017", result.ExecutedRuleIds);
    }

    [Fact]
    public void Evaluate_ReportsIssue_WhenPlayerOwnerConfiguredWithoutPlayerCharacter()
    {
        var project = new ProjectModel
        {
            Name = "TimerPlayerOwnerMissing",
            PlayerCharacterObjectName = string.Empty
        };

        project.GlobalScope.TimerDefinitions.Add(new RuntimeTimerDefinitionDto
        {
            TimerKey = "player.timer",
            ScheduleAfterMs = 100,
            FireMode = TimerFireMode.OneShot,
            TargetActionRef = "Act",
            LifetimeOwnerType = TimerOwnerType.Player,
            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
            Enabled = true
        });

        var result = Evaluate(project);

        Assert.Contains(result.Issues, issue => issue.RuleId == "PROJ-017"
            && issue.Description.Contains("Player", StringComparison.OrdinalIgnoreCase));
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new TimerDefinitionOwnerScopeContextRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }
}
