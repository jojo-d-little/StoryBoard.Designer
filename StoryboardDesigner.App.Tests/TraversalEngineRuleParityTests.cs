using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalEngineRuleParityTests
{
    [Fact]
    public void Execute_ReportsConnectionError_WhenFourDirectionalHasDiagonalDirection()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };

        var project = BuildProject(new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalModeOverride = AreaAdjacencyMode.FourDirectional,
                    BaseTraversalDirectionFromA = Direction10.NorthEast,
                    TraversalStateFromA = BuildPassableLeg("true"),
                    TraversalStateFromB = BuildPassableLeg("true")
                }
            ]
        });

        var issues = ExecuteTraversalRules(project);

        var issue = Assert.Single(issues, i => i.RuleId == "TRV-002");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("diagonal", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ReportsLegError_WhenPassableDefaultIsInvalid()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };

        var project = BuildProject(new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalStateFromA = BuildPassableLeg("maybe"),
                    TraversalStateFromB = BuildPassableLeg("true")
                }
            ]
        });

        var issues = ExecuteTraversalRules(project);

        var issue = Assert.Single(issues, i => i.RuleId == "TRV-003");
        Assert.Contains("default value", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ReportsDoorLinkError_WhenDoorAndLegDoNotShareVariable()
    {
        var sharedDoor = Guid.NewGuid();
        var sharedPassable = Guid.NewGuid();

        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        var doorA = new GameObject
        {
            Name = "DoorA",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "true",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedDoor
                }
            ]
        };
        roomA.GameObjects.Add(doorA);

        var project = BuildProject(new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalStateFromA = new TraversalLegState
                    {
                        OpenableObjectId = doorA.ObjectId,
                        Variables =
                        [
                            new GamePropertyDefinition
                            {
                                Name = "isPassable",
                                DefaultValue = "true",
                                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                                SharedVariableId = sharedPassable
                            }
                        ]
                    },
                    TraversalStateFromB = BuildPassableLeg("true")
                }
            ]
        });

        var issues = ExecuteTraversalRules(project);

        var issue = Assert.Single(issues, i => i.RuleId == "TRV-004");
        Assert.Contains("not linked to the same shared variable", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ValidationIssue> ExecuteTraversalRules(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalRequireOpenSharedIsOpenRule());
        registry.Register(new TraversalConnectionIntegrityRule());
        registry.Register(new TraversalLegPassableContractRule());
        registry.Register(new TraversalDoorLinkIntegrityRule());
        registry.Register(new TraversalDoorOpenStateLinkConventionRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }

    private static TraversalLegState BuildPassableLeg(string defaultValue)
    {
        return new TraversalLegState
        {
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPassable",
                    DefaultValue = defaultValue,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };
    }

    private static ProjectModel BuildProject(Area area)
    {
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "TraversalValidationProject",
            Planets = [planet]
        };
    }
}
