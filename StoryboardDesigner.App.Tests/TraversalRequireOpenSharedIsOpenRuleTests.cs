using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TraversalRequireOpenSharedIsOpenRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenRequireOpenLegIsMissingPassableVariable()
    {
        var project = BuildProjectWithSingleConnection(
            legA: new TraversalLegState
            {
                OpenStatePolicy = OpenablePolicy.RequireOpen,
                Variables = []
            },
            legB: BuildDefaultPassableLeg());

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-001", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("isPassable is missing", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LegAtoB", issue.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenRequireOpenLegPassableIsNotShared()
    {
        var project = BuildProjectWithSingleConnection(
            legA: new TraversalLegState
            {
                OpenStatePolicy = OpenablePolicy.RequireOpen,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPassable",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            },
            legB: BuildDefaultPassableLeg());

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-001", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("requires open state", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LegAtoB", issue.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenRequireOpenLegSharedIdDoesNotMatchAnyObjectIsOpen()
    {
        var project = BuildProjectWithSingleConnection(
            legA: new TraversalLegState
            {
                OpenStatePolicy = OpenablePolicy.RequireOpen,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPassable",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                        SharedVariableId = Guid.NewGuid()
                    }
                ]
            },
            legB: BuildDefaultPassableLeg());

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("TRV-001", issue.RuleId);
        Assert.Contains("isOpen", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not linked", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenRequireOpenLegSharesWithObjectIsOpen()
    {
        var shared = Guid.NewGuid();

        var project = BuildProjectWithSingleConnection(
            legA: new TraversalLegState
            {
                OpenStatePolicy = OpenablePolicy.RequireOpen,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPassable",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                        SharedVariableId = shared
                    }
                ]
            },
            legB: BuildDefaultPassableLeg(),
            roomAObjectFactory: () => new GameObject
            {
                Name = "DoorA",
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isOpen",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                        SharedVariableId = shared
                    }
                ]
            });

        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    private static List<StoryboardDesigner.App.Validation.Contracts.ValidationIssue> ExecuteRule(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalRequireOpenSharedIsOpenRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }

    private static TraversalLegState BuildDefaultPassableLeg()
    {
        return new TraversalLegState
        {
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPassable",
                    DefaultValue = "true",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };
    }

    private static ProjectModel BuildProjectWithSingleConnection(
        TraversalLegState legA,
        TraversalLegState legB,
        Func<GameObject>? roomAObjectFactory = null)
    {
        var roomA = new Room { Name = "RoomA" };
        var roomB = new Room { Name = "RoomB" };

        if (roomAObjectFactory is not null)
        {
            roomA.GameObjects.Add(roomAObjectFactory());
        }

        var area = new Area
        {
            Name = "AreaA",
            Rooms = [roomA, roomB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalStateFromA = legA,
                    TraversalStateFromB = legB
                }
            ]
        };

        var country = new Country
        {
            Name = "CountryA",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "PlanetA",
            Countries = [country]
        };

        return new ProjectModel
        {
            Name = "TestProject",
            Planets = [planet]
        };
    }
}
