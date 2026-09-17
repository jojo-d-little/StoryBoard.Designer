using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SharedVariableSingleMembershipRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenVariableParticipatesInMultipleSharedVariables()
    {
        var variable = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen"
        };

        var project = new ProjectModel
        {
            GlobalVariables = [variable],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareA",
                    Participants =
                    [
                        new SharedVariableParticipant { Kind = "global", OwnerId = variable.Id, VariableName = variable.Name }
                    ]
                },
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareB",
                    Participants =
                    [
                        new SharedVariableParticipant { Kind = "global", OwnerId = variable.Id, VariableName = variable.Name }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableSingleMembershipRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("PROJ-003", issue.RuleId);
        Assert.Equal("Global", issue.Path);
        Assert.Contains("participates in multiple shared variables", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShareA", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShareB", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(variable.Id.ToString("N"), issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_WhenVariableParticipatesInAtMostOneSharedVariable()
    {
        var variableA = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen"
        };

        var variableB = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isPassable"
        };

        var project = new ProjectModel
        {
            GlobalVariables = [variableA, variableB],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "DoorState",
                    Participants =
                    [
                        new SharedVariableParticipant { Kind = "global", OwnerId = variableA.Id, VariableName = variableA.Name },
                        new SharedVariableParticipant { Kind = "global", OwnerId = variableB.Id, VariableName = variableB.Name }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableSingleMembershipRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_IncludesFullOwnerObjectPath_WhenVariableIsOwnedByRoomObject()
    {
        var variable = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen"
        };

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Terra",
                    Countries =
                    [
                        new Country
                        {
                            Name = "USA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Lab",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "Storage",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    Name = "Crate",
                                                    Variables = [variable]
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareA",
                    Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = variable.Id, VariableName = variable.Name }]
                },
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareB",
                    Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = variable.Id, VariableName = variable.Name }]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableSingleMembershipRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        const string expectedOwnerPath = "Global / Terra / USA / Lab / Storage / Crate";
        Assert.Equal(expectedOwnerPath, issue.Path);
        Assert.Contains(expectedOwnerPath, issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_IncludesAreaScopedObjectPath_WhenVariableIsOwnedByAreaScopedObject()
    {
        var variable = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen"
        };

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Terra",
                    Countries =
                    [
                        new Country
                        {
                            Name = "USA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Lab",
                                    GameObjects =
                                    [
                                        new GameObject
                                        {
                                            Name = "Crate",
                                            Variables = [variable]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareA",
                    Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = variable.Id, VariableName = variable.Name }]
                },
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareB",
                    Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = variable.Id, VariableName = variable.Name }]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableSingleMembershipRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        const string expectedOwnerPath = "Global / Terra / USA / Lab / Crate";
        Assert.Equal(expectedOwnerPath, issue.Path);
        Assert.Contains(expectedOwnerPath, issue.Description, StringComparison.OrdinalIgnoreCase);
    }
}
