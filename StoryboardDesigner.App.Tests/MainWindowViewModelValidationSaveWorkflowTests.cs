using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelValidationSaveWorkflowTests
{
    [Fact]
    public void SaveProject_WithValidationIssues_WhenUserCancels_DoesNotSave()
    {
        var project = new ProjectModel { Name = "Validation Cancel" };
        var projectUi = new ProjectUiServiceStub
        {
            ContinueSaveWithValidation = false
        };

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "validation-cancel.sbe.json"));

        var saved = bundle.ViewModel.SaveProject();

        Assert.False(saved);
        Assert.Equal(0, bundle.JsonExport.SaveProjectModelCallCount);
        Assert.True(bundle.ProjectUi.LastValidationErrorCount > 0);
        Assert.NotEmpty(bundle.ProjectUi.LastValidationReportPath);
        Assert.NotEmpty(bundle.ProjectUi.LastValidationIssues);
    }

    [Fact]
    public void SaveProject_WithValidationIssues_WhenUserContinues_SavesProject()
    {
        var project = new ProjectModel { Name = "Validation Continue" };
        var projectUi = new ProjectUiServiceStub
        {
            ContinueSaveWithValidation = true
        };

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "validation-continue.sbe.json"));

        var saved = bundle.ViewModel.SaveProject();

        Assert.True(saved);
        Assert.True(bundle.JsonExport.SaveProjectModelCallCount > 0);
        Assert.True(bundle.JsonExport.ExportCleanProjectCallCount > 0);
        Assert.True(bundle.ProjectUi.LastValidationErrorCount > 0);
        Assert.NotEmpty(bundle.ProjectUi.LastValidationReportPath);
    }

    [Fact]
    public void SaveProject_WithAcceptedTraversalErrors_PopulatesScopeNodeValidationErrors()
    {
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalStateFromA = new TraversalLegState { Variables = new List<GamePropertyDefinition>() },
                    TraversalStateFromB = new TraversalLegState { Variables = new List<GamePropertyDefinition>() }
                }
            ]
        };

        var project = new ProjectModel
        {
            Name = "Validation Stamp",
            StartingPlanetName = "Planet",
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    StartingCountryName = "Country",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country",
                            StartingAreaName = "Area",
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        ScopeHierarchy.AttachParents(project);

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub { ContinueSaveWithValidation = true });

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "validation-stamp.sbe.json"));

        var saved = bundle.ViewModel.SaveProject();

        Assert.True(saved);
        Assert.NotEmpty(area.ValidationErrors);
        Assert.Contains(area.ValidationErrors, value => value.Contains("isPassable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveProject_WithAcceptedTraversalErrors_ProjectsTraversalValidationToAreaMapArrows()
    {
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var area = new Area
        {
            Name = "Area",
            Rooms = [roomA, roomB],
            RoomPlacements =
            [
                new AreaRoomPlacement { RoomId = roomA.Id, X = 24, Y = 24 },
                new AreaRoomPlacement { RoomId = roomB.Id, X = 204, Y = 24 }
            ],
            TraversalConnections =
            [
                new TraversalConnection
                {
                    RoomAId = roomA.Id,
                    RoomBId = roomB.Id,
                    TraversalStateFromA = new TraversalLegState { Variables = new List<GamePropertyDefinition>() },
                    TraversalStateFromB = new TraversalLegState { Variables = new List<GamePropertyDefinition>() }
                }
            ]
        };

        var project = new ProjectModel
        {
            Name = "Validation Area Map",
            StartingPlanetName = "Planet",
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    StartingCountryName = "Country",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country",
                            StartingAreaName = "Area",
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        ScopeHierarchy.AttachParents(project);

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub { ContinueSaveWithValidation = true });

        bundle.ViewModel.OpenAreaEditor(area);
        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "validation-area-map.sbe.json"));

        var saved = bundle.ViewModel.SaveProject();

        Assert.True(saved);
        Assert.NotNull(bundle.ViewModel.SelectedAreaEditor);
        Assert.NotEmpty(bundle.ViewModel.SelectedAreaEditor!.NavigationArrows);
        Assert.All(bundle.ViewModel.SelectedAreaEditor!.NavigationArrows, arrow => Assert.True(arrow.HasValidationError));
    }

    [Fact]
    public void SaveProject_SuppressesAnchorDynamicTailSoftFinding_FromSavePopupIssueList()
    {
        var player = new GameObject
        {
            Name = "Player",
            IsContainer = true,
            ContainerPointsDefaultValue = 5,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var room = new Room
        {
            Name = "Room",
            AvailableActions =
            [
                new CommandAction
                {
                    Name = "inspect",
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "ECHO"
                }
            ],
            EventSubscriptions =
            [
                new EventSubscriptionDefinition
                {
                    EventKey = "procedure.started",
                    ActionBindings =
                    [
                        new EventActionBindingDefinition
                        {
                            Order = 1,
                            Target = new EventBindingTargetDefinition { ActionName = "inspect" },
                            Condition = new EventBindingConditionDefinition
                            {
                                Filters =
                                [
                                    new EventBindingFilterConditionDefinition
                                    {
                                        VariableName = "currentAction::scope.inventory.lastAdded.name",
                                        Operator = Storyboard.Shared.RuntimeContracts.Enums.RuntimeVariableComparisonOperator.Equals,
                                        ExpectedValue = "true"
                                    }
                                ]
                            }
                        }
                    ]
                }
            ]
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Planet",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "SoftFindingsOnly",
            StartingPlanetName = planet.Name,
            GameObjects = [player],
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var projectUi = new ProjectUiServiceStub
        {
            ContinueSaveWithValidation = true
        };

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "soft-findings-only.sbe.json"));

        var saved = bundle.ViewModel.SaveProject();

        Assert.True(saved);
        Assert.True(bundle.JsonExport.SaveProjectModelCallCount > 0);
        Assert.DoesNotContain(bundle.ProjectUi.LastValidationIssues, issue =>
            string.Equals(issue.RuleId, "EVT-007", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Validation:", bundle.ViewModel.ExportStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveProject_WhenSaveProjectModelThrows_ReturnsFalseAndPublishesSaveError()
    {
        var project = new ProjectModel
        {
            Name = "Save Exception",
            StartingPlanetName = "Planet",
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    StartingCountryName = "Country",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country",
                            StartingAreaName = "Area",
                            Areas = [new Area { Name = "Area", Rooms = [new Room { Name = "Room" }] }]
                        }
                    ]
                }
            ]
        };

        ScopeHierarchy.AttachParents(project);

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub { ContinueSaveWithValidation = true });

        bundle.JsonExport.SaveProjectModelException = new InvalidOperationException("Traversal validation failed during save.");
        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "save-throws.sbe.json"));

        var saved = bundle.ViewModel.SaveProject();

        Assert.False(saved);
        Assert.Equal(1, bundle.JsonExport.SaveProjectModelCallCount);
        Assert.Equal(0, bundle.JsonExport.ExportCleanProjectCallCount);
        Assert.Contains("Save failed:", bundle.ViewModel.ExportStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Traversal validation failed during save", bundle.ViewModel.ExportStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveProjectAs_WithValidInput_SavesToNewProjectPath()
    {
        var project = new ProjectModel
        {
            Name = "Original",
            GlobalObjectScopeName = "Global Objects",
            CommandVerbs = ["look"],
            Directionals = ["north"]
        };

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub { ContinueSaveWithValidation = true });

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "original.sbe.json"));

        var targetFolder = Path.Combine(Path.GetTempPath(), "save-as-target");
        var saved = bundle.ViewModel.SaveProjectAs(targetFolder, "CopiedProject");

        Assert.True(saved);
        Assert.Equal("CopiedProject", bundle.ViewModel.Project.Name);
        Assert.Equal(1, bundle.JsonExport.CreateProjectSkeletonCallCount);
        Assert.Equal(targetFolder, bundle.JsonExport.LastCreateProjectBaseFolder);
        Assert.Equal("CopiedProject", bundle.JsonExport.LastCreateProjectName);
        Assert.True(bundle.JsonExport.SaveProjectModelCallCount > 0);
        Assert.True(bundle.JsonExport.ExportCleanProjectCallCount > 0);
        Assert.Equal(Path.Combine(targetFolder, "CopiedProject", "CopiedProject.sbe.json"), GetProjectFilePath(bundle.ViewModel));
    }

    [Fact]
    public void SaveProjectAs_WhenSaveCanceled_RestoresOriginalPathAndProjectName()
    {
        var project = new ProjectModel { Name = "Original" };
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub { ContinueSaveWithValidation = false });

        var originalPath = Path.Combine(Path.GetTempPath(), "original-cancel.sbe.json");
        SetProjectFilePath(bundle.ViewModel, originalPath);

        var saved = bundle.ViewModel.SaveProjectAs(Path.GetTempPath(), "CopyCanceled");

        Assert.False(saved);
        Assert.Equal("Original", bundle.ViewModel.Project.Name);
        Assert.Equal(originalPath, GetProjectFilePath(bundle.ViewModel));
    }

    [Fact]
    public void TryExportCleanProject_WithValidationErrors_BlocksRuntimeExport()
    {
        var project = new ProjectModel { Name = "Validation Export Block" };
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "validation-export-block.sbe.json"));

        var method = typeof(MainWindowViewModel).GetMethod("TryExportCleanProject", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var args = new object?[] { string.Empty, string.Empty, false };
        var exported = (bool)method!.Invoke(bundle.ViewModel, args)!;

        Assert.False(exported);
        Assert.Equal(0, bundle.JsonExport.ExportCleanProjectCallCount);
        Assert.Contains("error(s)", (string?)args[1] ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryExportCleanProject_WithWarningsOnly_AllowsRuntimeExport()
    {
        var player = new GameObject
        {
            Name = "Player",
            IsContainer = true,
            ContainerPointsDefaultValue = 5,
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    Lifetime = GamePropertyLifetime.Singleton,
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var roomObjectOutsideBounds = new GameObject
        {
            Name = "FarTorch",
            PositionX = 5000,
            PositionY = 5000
        };

        var room = new Room
        {
            Name = "Room",
            RoomImageCanvasWidth = 800,
            RoomImageCanvasHeight = 600,
            GameObjects = [roomObjectOutsideBounds]
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Planet",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "WarningOnlyExport",
            StartingPlanetName = planet.Name,
            GameObjects = [player],
            Planets = [planet]
        };

        ScopeHierarchy.AttachParents(project);

        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        SetProjectFilePath(bundle.ViewModel, Path.Combine(Path.GetTempPath(), "warning-only-export.sbe.json"));

        var method = typeof(MainWindowViewModel).GetMethod("TryExportCleanProject", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var args = new object?[] { string.Empty, string.Empty, false };
        var exported = (bool)method!.Invoke(bundle.ViewModel, args)!;

        Assert.True(exported);
        Assert.Equal(1, bundle.JsonExport.ExportCleanProjectCallCount);
        Assert.Equal(string.Empty, (string?)args[1] ?? string.Empty);
    }

    private static void SetProjectFilePath(MainWindowViewModel vm, string projectFilePath)
    {
        var field = typeof(MainWindowViewModel).GetField("_projectFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, projectFilePath);
    }

    private static string GetProjectFilePath(MainWindowViewModel vm)
    {
        var field = typeof(MainWindowViewModel).GetField("_projectFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (string)(field!.GetValue(vm) ?? string.Empty);
    }
}
