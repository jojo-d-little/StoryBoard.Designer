using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class EventSubscriptionIntegrityRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssues_ForMissingEventKeyDuplicateOrderMissingTargetAndBlankFilterVariable()
    {
        var room = new Room
        {
            Name = "Event Room",
            EventSubscriptions =
            [
                new EventSubscriptionDefinition
                {
                    Id = Guid.NewGuid(),
                    EventKey = " ",
                    ActionBindings =
                    [
                        new EventActionBindingDefinition
                        {
                            Order = 1,
                            Target = new EventBindingTargetDefinition
                            {
                                ActionName = ""
                            },
                            Condition = new EventBindingConditionDefinition
                            {
                                Filters =
                                [
                                    new EventBindingFilterConditionDefinition
                                    {
                                        VariableName = ""
                                    }
                                ]
                            }
                        },
                        new EventActionBindingDefinition
                        {
                            Order = 1,
                            Target = new EventBindingTargetDefinition
                            {
                                ActionName = "Inspect"
                            }
                        }
                    ]
                }
            ]
        };

        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "Events", Planets = [planet] };

        var result = Evaluate(project);

        Assert.Contains(result.Issues, issue => issue.RuleId == "PROJ-015"
            && issue.Description.Contains("eventKey", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.RuleId == "PROJ-015"
            && issue.Description.Contains("duplicate action binding order", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.RuleId == "PROJ-015"
            && issue.Description.Contains("actionName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.RuleId == "PROJ-015"
            && issue.Description.Contains("variableName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_DoesNotReportIssues_ForValidEventSubscription()
    {
        var room = new Room
        {
            Name = "Event Room",
            EventSubscriptions =
            [
                new EventSubscriptionDefinition
                {
                    Id = Guid.NewGuid(),
                    EventKey = "player.room.entered",
                    ActionBindings =
                    [
                        new EventActionBindingDefinition
                        {
                            Order = 1,
                            Target = new EventBindingTargetDefinition
                            {
                                ActionName = "Inspect"
                            },
                            Condition = new EventBindingConditionDefinition
                            {
                                Filters =
                                [
                                    new EventBindingFilterConditionDefinition
                                    {
                                        VariableName = "action.trigger.isEventFired"
                                    }
                                ]
                            }
                        }
                    ]
                }
            ]
        };

        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "Events", Planets = [planet] };

        var result = Evaluate(project);

        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "PROJ-015");
        Assert.Contains("PROJ-015", result.ExecutedRuleIds);
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new EventSubscriptionIntegrityRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }
}
