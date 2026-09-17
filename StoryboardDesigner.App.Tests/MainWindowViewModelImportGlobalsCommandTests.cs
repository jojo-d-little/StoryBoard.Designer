using Storyboard.Shared.GameServices;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelImportGlobalsCommandTests
{
    [Fact]
    public void ImportGlobalsFromProject_ImportsAllTopLevelConstructs_WhenSelected()
    {
        var project = new ProjectModel();
        project.CommandVerbs.Clear();
        project.Directionals.Clear();
        project.DirectionalTraversalMappings.Clear();
        project.GlobalScope.AvailableActions.Clear();
        project.GlobalScope.SoundEffectLibraryEntries.Clear();
        project.GlobalScope.EventSubscriptions.Clear();
        project.GlobalScope.TimerDefinitions.Clear();
        project.ObjectTemplates.Clear();
        project.RoomTemplates.Clear();
        project.BaseObjects.Clear();
        project.GlobalScope.GameObjects.Clear();

        var projectUi = new ProjectUiServiceStub
        {
            ImportGlobalNodePathAccepted = true,
            ImportGlobalNodeProjectPath = "c:/tmp/source.sbe.json",
            ImportGlobalNodeSelectionAccepted = true,
            ImportGlobalNodeSelection = new GlobalImportSelectionResult
            {
                ImportVerbs = true,
                ImportDirectionals = true,
                ImportGlobalActions = true,
                ImportGlobalSoundEffects = true,
                ImportGlobalEventSubscriptions = true,
                ImportGlobalTimers = true,
                ImportTemplateObjects = true,
                ImportRoomTemplates = true,
                ImportBaseObjects = true,
                ImportGlobalObjects = true,
                CollisionStrategy = GlobalImportCollisionStrategy.KeepExisting
            }
        };

        var treeContext = new TreeContextInteractionServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        bundle.JsonExport.GlobalNodeImportData = new ProjectGlobalNodeImportData
        {
            CommandVerbs = ["chant"],
            Directionals = ["northward"],
            DirectionalTraversalMappings =
            [
                new DirectionalTraversalMapping
                {
                    Token = "northward",
                    TraversalDirection = Direction10.North
                }
            ],
            GlobalAvailableActions =
            [
                CreateGlobalAction("spin")
            ],
            GlobalSoundEffectLibraryEntries =
            [
                CreateSoundEffect("sfx.spin")
            ],
            GlobalEventSubscriptions =
            [
                CreateEventSubscription("OnSpin", "spin-sub")
            ],
            GlobalTimerDefinitions =
            [
                CreateTimer("spin.timer", "spin")
            ],
            ObjectTemplates =
            [
                CreateGameObject("Template Lantern")
            ],
            RoomTemplates =
            [
                CreateRoom("Template Atrium")
            ],
            BaseObjects =
            [
                CreateGameObject("Base Crate")
            ],
            GlobalObjects =
            [
                CreateGameObject("Global Compass")
            ]
        };

        bundle.ViewModel.ImportGlobalsFromProjectCommand.Execute(null);

        Assert.Contains("chant", project.CommandVerbs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("northward", project.Directionals, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(project.DirectionalTraversalMappings, mapping =>
            string.Equals(mapping.Token, "northward", StringComparison.OrdinalIgnoreCase)
            && mapping.TraversalDirection == Direction10.North);

        var importedAction = Assert.Single(project.GlobalScope.AvailableActions);
        Assert.Equal("spin", importedAction.Name);

        var importedSound = Assert.Single(project.GlobalScope.SoundEffectLibraryEntries);
        Assert.Equal("sfx.spin", importedSound.SoundEffectKey);

        var importedEventSubscription = Assert.Single(project.GlobalScope.EventSubscriptions);
        Assert.Equal("OnSpin", importedEventSubscription.EventKey);

        var importedTimer = Assert.Single(project.GlobalScope.TimerDefinitions);
        Assert.Equal("spin.timer", importedTimer.TimerKey);

        var importedTemplate = Assert.Single(project.ObjectTemplates);
        Assert.Equal("Template Lantern", importedTemplate.Name);

        var importedRoomTemplate = Assert.Single(project.RoomTemplates);
        Assert.Equal("Template Atrium", importedRoomTemplate.Name);

        var importedBaseObject = Assert.Single(project.BaseObjects);
        Assert.Equal("Base Crate", importedBaseObject.Name);

        var importedGlobalObject = Assert.Single(project.GlobalScope.GameObjects);
        Assert.Equal("Global Compass", importedGlobalObject.Name);

        Assert.Contains("Imported globals:", bundle.ViewModel.ExportStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportGlobalsFromProject_ImportsGlobalActions_WhenSelected()
    {
        var project = new ProjectModel();
        project.GlobalObjectAvailableActions.Clear();

        var projectUi = new ProjectUiServiceStub
        {
            ImportGlobalNodePathAccepted = true,
            ImportGlobalNodeProjectPath = "c:/tmp/source.sbe.json",
            ImportGlobalNodeSelectionAccepted = true,
            ImportGlobalNodeSelection = new GlobalImportSelectionResult
            {
                ImportGlobalActions = true,
                CollisionStrategy = GlobalImportCollisionStrategy.KeepExisting
            }
        };

        var treeContext = new TreeContextInteractionServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        bundle.JsonExport.GlobalNodeImportData = new ProjectGlobalNodeImportData
        {
            GlobalAvailableActions =
            [
                CreateGlobalAction("spin")
            ]
        };

        bundle.ViewModel.ImportGlobalsFromProjectCommand.Execute(null);

        Assert.Single(project.GlobalObjectAvailableActions);
        Assert.Equal("spin", project.GlobalObjectAvailableActions[0].Name);
        Assert.Contains("Imported globals:", bundle.ViewModel.ExportStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportGlobalsFromProject_ReplacesGlobalActions_WhenCollisionStrategyIsReplaceExisting()
    {
        var project = new ProjectModel();
        project.GlobalObjectAvailableActions.Clear();
        project.GlobalObjectAvailableActions.Add(CreateGlobalAction("spin", verb: "oldVerb"));

        var projectUi = new ProjectUiServiceStub
        {
            ImportGlobalNodePathAccepted = true,
            ImportGlobalNodeProjectPath = "c:/tmp/source.sbe.json",
            ImportGlobalNodeSelectionAccepted = true,
            ImportGlobalNodeSelection = new GlobalImportSelectionResult
            {
                ImportGlobalActions = true,
                CollisionStrategy = GlobalImportCollisionStrategy.ReplaceExisting
            }
        };

        var treeContext = new TreeContextInteractionServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        bundle.JsonExport.GlobalNodeImportData = new ProjectGlobalNodeImportData
        {
            GlobalAvailableActions =
            [
                CreateGlobalAction("spin", verb: "newVerb")
            ]
        };

        var existingActionId = project.GlobalObjectAvailableActions[0].Id;

        bundle.ViewModel.ImportGlobalsFromProjectCommand.Execute(null);

        var updated = Assert.Single(project.GlobalObjectAvailableActions);
        Assert.Equal(existingActionId, updated.Id);
        Assert.Contains("newVerb", updated.Verbs, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportGlobalsFromProject_ImportTemplateObjects_PreservesAppearanceAndVariantScript()
    {
        var project = new ProjectModel();
        project.ObjectTemplates.Clear();

        var projectUi = new ProjectUiServiceStub
        {
            ImportGlobalNodePathAccepted = true,
            ImportGlobalNodeProjectPath = "c:/tmp/source.sbe.json",
            ImportGlobalNodeSelectionAccepted = true,
            ImportGlobalNodeSelection = new GlobalImportSelectionResult
            {
                ImportTemplateObjects = true,
                CollisionStrategy = GlobalImportCollisionStrategy.KeepExisting
            }
        };

        var treeContext = new TreeContextInteractionServiceStub();
        var bundle = MainWindowViewModelTestHarness.CreateViewModelBundle(project, treeContext, projectUi);
        bundle.JsonExport.GlobalNodeImportData = new ProjectGlobalNodeImportData
        {
            ObjectTemplates =
            [
                new GameObject
                {
                    ObjectId = Guid.NewGuid(),
                    Name = "DoorTemplate",
                    ObjectType = "DoorObjectType",
                    ImageVariantChooserScript = "IF {self.isOpen}\n\"Open\"\nELSE\n\"Closed\"\nENDIF",
                    IsMovable = true,
                    IsMovableDefaultValue = true,
                    SpatialType = RuntimeObjectSpatialTypes.SolidObject,
                    StackGroup = 7,
                    FootprintWidthCells = 6,
                    FootprintHeightCells = 2,
                    FootprintOrientation = "E",
                    HeadingDirection = "S",
                    ObjectHeightUnits = 3,
                    HeightInRoom = 1,
                    AuthoredBaseHeightInRoom = 2,
                    IsHeightPinned = true,
                    OccupiedCellIds = ["X1Y2", "X2Y2"],
                    OccupancyDerivationSourceEcho = "echo",
                    StackScaleStepOverride = 0.25,
                    MinStackScaleOverride = 0.5,
                    IncludeInPreview = false,
                    PositionX = 10,
                    PositionY = 20,
                    RenderZOrder = 11,
                    AuthoredRenderOrder = 12,
                    MovementRestrictions = new ObjectMovementRestrictions
                    {
                        MultiLegMaxTotalDistanceCells = 4,
                        FirstUnstacked = new ObjectMovementRestrictionCategory
                        {
                            N = new ObjectMovementRestrictionRule
                            {
                                MaxDistance = 2,
                                AllowJumpOver = true
                            }
                        }
                    }
                }
            ]
        };

        bundle.ViewModel.ImportGlobalsFromProjectCommand.Execute(null);

        var imported = Assert.Single(project.ObjectTemplates);
        Assert.Equal("DoorTemplate", imported.Name);
        Assert.Equal("DoorObjectType", imported.ObjectType);
        Assert.Equal("IF {self.isOpen}\n\"Open\"\nELSE\n\"Closed\"\nENDIF", imported.ImageVariantChooserScript);
        Assert.True(imported.IsMovable);
        Assert.True(imported.IsMovableDefaultValue);
        Assert.Equal(RuntimeObjectSpatialTypes.SolidObject, imported.SpatialType);
        Assert.Equal(7, imported.StackGroup);
        Assert.Equal(6, imported.FootprintWidthCells);
        Assert.Equal(2, imported.FootprintHeightCells);
        Assert.Equal("E", imported.FootprintOrientation);
        Assert.Equal("S", imported.HeadingDirection);
        Assert.Equal(3, imported.ObjectHeightUnits);
        Assert.Equal(1, imported.HeightInRoom);
        Assert.Equal(2, imported.AuthoredBaseHeightInRoom);
        Assert.True(imported.IsHeightPinned);
        Assert.Equal(["X1Y2", "X2Y2"], imported.OccupiedCellIds);
        Assert.Equal("echo", imported.OccupancyDerivationSourceEcho);
        Assert.Equal(0.2, imported.StackScaleStepOverride);
        Assert.Equal(0.5, imported.MinStackScaleOverride);
        Assert.False(imported.IncludeInPreview);
        Assert.Equal(10, imported.PositionX);
        Assert.Equal(20, imported.PositionY);
        Assert.Equal(11, imported.RenderZOrder);
        Assert.Equal(12, imported.AuthoredRenderOrder);
        Assert.NotNull(imported.MovementRestrictions);
        Assert.Equal(4, imported.MovementRestrictions!.MultiLegMaxTotalDistanceCells);
        Assert.Equal(2, imported.MovementRestrictions.FirstUnstacked.N.MaxDistance);
        Assert.True(imported.MovementRestrictions.FirstUnstacked.N.AllowJumpOver);
    }

    private static CommandAction CreateGlobalAction(string name, string verb = "move")
    {
        return new CommandAction
        {
            Name = name,
            ActionType = CommandActionType.EchoMessage,
            Verbs = [verb],
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "ok"
            }
        };
    }

    private static GameObject CreateGameObject(string name)
    {
        return new GameObject
        {
            Name = name,
            ObjectId = Guid.NewGuid()
        };
    }

    private static SoundEffectLibraryEntry CreateSoundEffect(string key)
    {
        return new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = key,
            DisplayName = key,
            AssetRef = "audio/sfx/spin.wav"
        };
    }

    private static EventSubscriptionDefinition CreateEventSubscription(string eventKey, string subscriptionName)
    {
        return new EventSubscriptionDefinition
        {
            Id = Guid.NewGuid(),
            EventKey = eventKey,
            SubscriptionName = subscriptionName,
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition
                    {
                        ActionName = "spin"
                    }
                }
            ]
        };
    }

    private static RuntimeTimerDefinitionDto CreateTimer(string timerKey, string actionRef)
    {
        return new RuntimeTimerDefinitionDto
        {
            TimerKey = timerKey,
            ScheduleAfterMs = 500,
            FireMode = TimerFireMode.OneShot,
            TargetActionRef = actionRef,
            LifetimeOwnerType = TimerOwnerType.Room,
            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
            Enabled = true
        };
    }

    private static Room CreateRoom(string name)
    {
        return new Room
        {
            Name = name,
            Id = Guid.NewGuid()
        };
    }
}
