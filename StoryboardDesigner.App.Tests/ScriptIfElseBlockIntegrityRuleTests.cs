using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Scripting;

namespace StoryboardDesigner.App.Tests;

public sealed class ScriptIfElseBlockIntegrityRuleTests
{
    [Fact]
    public void Evaluate_ReportsError_WhenScriptHasIfElseBlockIntegrityIssue()
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
                                                    EchoMessage = "IF self.name is true",
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
        registry.Register(new ScriptIfElseBlockIntegrityRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-003", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("ENDIF", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Global / Planet A / Country A / Area A / Room A / Action:Inspect", issue.Path);
    }

    [Fact]
    public void Evaluate_ReportsError_WhenElseAppearsWithoutMatchingIf()
    {
        var project = BuildSingleActionProject("ELSE\nEcho line");

        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptIfElseBlockIntegrityRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-003", issue.RuleId);
        Assert.Contains("matching IF", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsError_WhenEndIfAppearsWithoutMatchingIf()
    {
        var project = BuildSingleActionProject("ENDIF");

        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptIfElseBlockIntegrityRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-003", issue.RuleId);
        Assert.Contains("matching IF", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsError_ForCompositeOutcomeMapScript_WhenIfBlockIsUnclosed()
    {
        var action = new CommandAction
        {
            Name = "Assemble",
            ActionType = CommandActionType.BuildCompositeByParts,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IncompleteRecipe"] = "IF self.name is true"
            }
        };

        action.Payload = new CompositeByPartsPayload(
            CompositeTargetObjectId: null,
            CompositeRecipeId: null,
            CompositeRequiredPartObjectIds: [],
            CompositeStrictPartCountEnforcement: null,
            CompositeMinimumRequiredPartCount: null,
            CompositeMatchMode: string.Empty,
            CompositeAmbiguityPolicy: string.Empty,
            CompositePartConsumptionMode: "ContainedInComposite",
            CompositeResolvedTargetOutputTemplate: string.Empty);

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
                                                action
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
        registry.Register(new ScriptIfElseBlockIntegrityRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-003", issue.RuleId);
        Assert.Contains("OutcomeMessageMap[IncompleteRecipe]", issue.Description, StringComparison.Ordinal);
        Assert.Contains("ENDIF", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectModel BuildSingleActionProject(string echoScript)
    {
        return new ProjectModel
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
                                                    EchoMessage = echoScript,
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
    }
}

