using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Scripting;

namespace StoryboardDesigner.App.Tests;

public sealed class ScriptMalformedIndexedReferenceRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenScriptHasMalformedIndexedReference()
    {
        var project = new ProjectModel
        {
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
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "Room A",
                                            AvailableActions =
                                            [
                                                new CommandAction
                                                {
                                                    Name = "Inspect",
                                                    ActionType = CommandActionType.EchoMessage,
                                                    EchoMessage = "ECHO {currentAction.parts[foo]}",
                                                    Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptMalformedIndexedReferenceRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-002", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Malformed indexed reference", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Global / Planet A / Country A / Area A / Room A / Action:Inspect", issue.Path);
    }
}

