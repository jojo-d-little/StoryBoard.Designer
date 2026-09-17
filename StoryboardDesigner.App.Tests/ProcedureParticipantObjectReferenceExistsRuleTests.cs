using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class ProcedureParticipantObjectReferenceExistsRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssues_WhenProcedureParticipantsReferenceMissingObjects()
    {
        var presentObjectId = Guid.NewGuid();
        var missingParticipantId = Guid.NewGuid();
        var missingMutationId = Guid.NewGuid();

        var project = new ProjectModel
        {
            GameObjects =
            [
                new GameObject
                {
                    ObjectId = presentObjectId,
                    Name = "Lantern"
                }
            ],
            Procedures =
            [
                new ProcedureDefinition
                {
                    Name = "Unlock Door",
                    Participants =
                    [
                        new ProcedureParticipantRequirement
                        {
                            ObjectId = presentObjectId,
                            DisplayName = "Lantern"
                        },
                        new ProcedureParticipantRequirement
                        {
                            ObjectId = missingParticipantId,
                            DisplayName = "Missing Key"
                        }
                    ],
                    ParticipantMutations =
                    [
                        new ProcedureParticipantMutation
                        {
                            ObjectId = missingMutationId,
                            ParticipantDisplayName = "Missing Lock",
                            Operation = ProcedureParticipantMutationOperation.SetVariable,
                            VariableName = "isOpen",
                            Value = "true"
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ProcedureParticipantObjectReferenceExistsRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal("PROJ-010", issue.RuleId));
        Assert.All(result.Issues, issue => Assert.Equal("Project / Procedures / Unlock Door", issue.Path));
        Assert.Contains(result.Issues, issue => issue.Description.Contains($"{missingParticipantId:N}", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.Description.Contains($"{missingMutationId:N}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenAllProcedureParticipantObjectsExistInProject()
    {
        var globalObjectId = Guid.NewGuid();
        var roomObjectId = Guid.NewGuid();

        var room = new Room
        {
            Name = "Hall",
            GameObjects =
            [
                new GameObject
                {
                    ObjectId = roomObjectId,
                    Name = "Door"
                }
            ]
        };

        var project = new ProjectModel
        {
            GameObjects =
            [
                new GameObject
                {
                    ObjectId = globalObjectId,
                    Name = "Key"
                }
            ],
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area",
                                    Rooms = [room]
                                }
                            ]
                        }
                    ]
                }
            ],
            Procedures =
            [
                new ProcedureDefinition
                {
                    Name = "Open Door",
                    Participants =
                    [
                        new ProcedureParticipantRequirement
                        {
                            ObjectId = globalObjectId,
                            DisplayName = "Key"
                        }
                    ],
                    ParticipantMutations =
                    [
                        new ProcedureParticipantMutation
                        {
                            ObjectId = roomObjectId,
                            ParticipantDisplayName = "Door",
                            Operation = ProcedureParticipantMutationOperation.SetVariable,
                            VariableName = "isOpen",
                            Value = "true"
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ProcedureParticipantObjectReferenceExistsRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-010", result.ExecutedRuleIds);
    }
}