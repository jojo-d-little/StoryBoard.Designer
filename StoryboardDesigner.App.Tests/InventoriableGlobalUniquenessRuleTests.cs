using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;

namespace StoryboardDesigner.App.Tests;

public sealed class InventoriableGlobalUniquenessRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenInventoriableObjectNamesDuplicateGlobally()
    {
        var project = new ProjectModel
        {
            GameObjects =
            [
                new GameObject
                {
                    Name = "Key",
                    IsInventoriable = true
                }
            ],
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    Name = "Key",
                                                    IsInventoriable = true
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

        var issues = ExecuteRule(project);

        var issue = Assert.Single(issues);
        Assert.Equal("OBJ-002", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("must be globally unique", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Global / Key", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_WhenInventoriableNamesAreUnique()
    {
        var project = new ProjectModel
        {
            GameObjects =
            [
                new GameObject
                {
                    Name = "Key",
                    IsInventoriable = true
                }
            ],
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    Name = "Lantern",
                                                    IsInventoriable = true
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

        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_ForLinkedRoomInstanceInventoriables()
    {
        var project = new ProjectModel
        {
            GameObjects =
            [
                new GameObject
                {
                    Name = "Key",
                    IsInventoriable = true,
                    LinkedBaseObjectId = Guid.NewGuid(),
                    LinkActionsToBaseObject = true
                }
            ],
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    Name = "Key",
                                                    IsInventoriable = true
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

        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_ForLinkedRoomInstanceInventoriables_WhenLinkActionsFlagIsFalse()
    {
        var baseObjectId = Guid.NewGuid();
        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    ObjectId = baseObjectId,
                                                    Name = "Key",
                                                    IsInventoriable = true
                                                }
                                            ]
                                        },
                                        new Room
                                        {
                                            Name = "RoomB",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    Name = "Key",
                                                    IsInventoriable = true,
                                                    LinkedBaseObjectId = baseObjectId,
                                                    LinkActionsToBaseObject = false
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

        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_DoesNotReportIssue_ForInventoriablesInsideLinkedRoomInstanceSubtree()
    {
        var baseObjectId = Guid.NewGuid();
        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    ObjectId = baseObjectId,
                                                    Name = "Satchel",
                                                    IsInventoriable = false,
                                                    ContainedObjects =
                                                    [
                                                        new GameObject
                                                        {
                                                            Name = "Gem",
                                                            IsInventoriable = true
                                                        }
                                                    ]
                                                }
                                            ]
                                        },
                                        new Room
                                        {
                                            Name = "RoomB",
                                            GameObjects =
                                            [
                                                new GameObject
                                                {
                                                    Name = "Satchel",
                                                    IsInventoriable = false,
                                                    LinkedBaseObjectId = baseObjectId,
                                                    LinkActionsToBaseObject = true,
                                                    ContainedObjects =
                                                    [
                                                        new GameObject
                                                        {
                                                            Name = "Gem",
                                                            IsInventoriable = true
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
                }
            ]
        };

        var issues = ExecuteRule(project);

        Assert.Empty(issues);
    }

    private static List<ValidationIssue> ExecuteRule(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new InventoriableGlobalUniquenessRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }
}
