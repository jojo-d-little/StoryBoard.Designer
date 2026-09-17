using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class ProcedureOwnershipReferenceExistsRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssues_WhenProjectAndScopedProcedureIdsReferenceMissingProcedures()
    {
        var knownProcedureId = Guid.NewGuid();
        var missingGlobalProcedureId = Guid.NewGuid();
        var missingOwnedProcedureId = Guid.NewGuid();

        var owner = new GameObject
        {
            Name = "Owner",
            ProcedureIds = [knownProcedureId, missingOwnedProcedureId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [owner]
        };

        var project = new ProjectModel
        {
            ProcedureIds = [knownProcedureId, missingGlobalProcedureId],
            Procedures =
            [
                new ProcedureDefinition
                {
                    Id = knownProcedureId,
                    Name = "Known Procedure"
                }
            ],
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
                                    Rooms = [room]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ProcedureOwnershipReferenceExistsRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal("PROJ-012", issue.RuleId));
        Assert.Contains(result.Issues, issue => issue.Path == "Project / Global Procedures"
            && issue.Description.Contains($"{missingGlobalProcedureId:N}", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.Description.Contains($"{missingOwnedProcedureId:N}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenAllProcedureOwnershipReferencesExist()
    {
        var globalProcedureId = Guid.NewGuid();
        var objectProcedureId = Guid.NewGuid();

        var owner = new GameObject
        {
            Name = "Owner",
            ProcedureIds = [objectProcedureId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [owner]
        };

        var project = new ProjectModel
        {
            ProcedureIds = [globalProcedureId],
            Procedures =
            [
                new ProcedureDefinition
                {
                    Id = globalProcedureId,
                    Name = "Global Procedure"
                },
                new ProcedureDefinition
                {
                    Id = objectProcedureId,
                    Name = "Object Procedure"
                }
            ],
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
                                    Rooms = [room]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ProcedureOwnershipReferenceExistsRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-012", result.ExecutedRuleIds);
    }
}