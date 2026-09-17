using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalDoorOpenStateLinkConventionRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenOpenableDoorIsOpenVariableIsMissing()
    {
        var door = new GameObject
        {
            Name = "Steel Door",
            IsOpenable = true
        };
        door.Variables.Clear();

        var issues = ExecuteRule(BuildProjectWithDoor(door));

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-005", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("isOpen variable is missing", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenOpenableDoorIsOpenHasNoSharedLink()
    {
        var door = new GameObject
        {
            Name = "North Door",
            IsOpenable = true,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var issues = ExecuteRule(BuildProjectWithDoor(door));

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-005", issue.RuleId);
        Assert.Contains("not linked", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenOpenableDoorSharedOnlyReferencesItself()
    {
        var sharedId = Guid.NewGuid();
        var door = new GameObject
        {
            Name = "Vault Door",
            IsOpenable = true,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedId
                }
            ]
        };

        var project = BuildProjectWithDoor(door);
        project.SharedVariables =
        [
            new SharedVariableDefinition
            {
                Id = sharedId,
                Name = "VaultDoorState",
                Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = door.ObjectId, VariableName = "isOpen" }]
            }
        ];

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-005", issue.RuleId);
        Assert.Contains("no linked participants beyond this door", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenOpenableDoorHasExternalSharedLink()
    {
        var sharedId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        var door = new GameObject
        {
            Name = "East Door",
            IsOpenable = true,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    SharedVariableId = sharedId
                }
            ]
        };

        var project = BuildProjectWithDoor(door);
        project.SharedVariables =
        [
            new SharedVariableDefinition
            {
                Id = sharedId,
                Name = "EastDoorState",
                Participants =
                [
                    new SharedVariableParticipant { Kind = "object", OwnerId = door.ObjectId, VariableName = "isOpen" },
                    new SharedVariableParticipant { Kind = "traversal-leg", OwnerId = connectionId, VariableName = "isPassable", Leg = "a2b" }
                ]
            }
        ];

        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_ForOpenableObjectWithoutDoorName()
    {
        var hatch = new GameObject
        {
            Name = "Maintenance Hatch",
            IsOpenable = true,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var issues = ExecuteRule(BuildProjectWithDoor(hatch));

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_ReportsTemplateRoomPath_ForTemplateRoomDoor()
    {
        var door = new GameObject
        {
            Name = "VicHotelDoor_E",
            IsOpenable = true,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var project = new ProjectModel
        {
            Name = "TemplateDoorPathProject",
            RoomTemplates =
            [
                new Room
                {
                    Name = "Room_4_Doors",
                    GameObjects = [door]
                }
            ]
        };

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-005", issue.RuleId);
        Assert.Equal("Global / Room Templates / Room_4_Doors / VicHotelDoor_E", issue.Path);
    }

    [Fact]
    public void Evaluate_ReportsBaseObjectPath_ForGlobalBaseObjectDoor()
    {
        var door = new GameObject
        {
            Name = "Base Door",
            IsOpenable = true,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isOpen",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var project = new ProjectModel
        {
            Name = "BaseDoorPathProject",
            BaseObjects = [door]
        };

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-005", issue.RuleId);
        Assert.Equal("Global / Base Objects / Base Door", issue.Path);
    }

    private static List<ValidationIssue> ExecuteRule(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalDoorOpenStateLinkConventionRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }

    private static ProjectModel BuildProjectWithDoor(GameObject door)
    {
        var roomA = new Room
        {
            Name = "A",
            GameObjects = [door]
        };

        var roomB = new Room { Name = "B" };

        var area = new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    TraversalConnectionId = Guid.NewGuid(),
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalStateFromA = new TraversalLegState(),
                    TraversalStateFromB = new TraversalLegState()
                }
            ]
        };

        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "DoorConventionProject",
            Planets = [planet]
        };
    }
}
