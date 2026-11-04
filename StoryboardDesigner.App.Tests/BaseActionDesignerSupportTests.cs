using System.Text.Json;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;
using StoryboardDesigner.App.Views;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

[Collection("WPF STA")]
public sealed class BaseActionDesignerSupportTests
{
    [Fact]
    public void CaptureObjectSetEditor_ShowsPlacedObjectAndSetKeyFields()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var placed = new GameObject { Name = "Elevator" };
                var action = new CommandAction
                {
                    Name = "Capture riders",
                    ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
                    Payload = new CaptureObjectsOnObjectFootprintPayload(string.Empty, null)
                };
                var choices = new List<MaterializeSourceObjectChoiceItem>
                {
                    new() { ObjectId = placed.ObjectId, SourceObject = placed, DisplayName = placed.Name, SourceCategory = "Current Room" }
                };
                var dialog = new RoomActionEditorDialog(
                    action,
                    PropertyResolutionScope.Room,
                    new GamePropertyPickerContext(new ProjectModel(), placed, PropertyResolutionScope.Room, _ => []),
                    [], choices, []);

                Assert.Equal(Visibility.Visible, ((Border)dialog.FindName("ActionSpecificEditorBorder")!).Visibility);
                Assert.Equal(Visibility.Visible, ((Grid)dialog.FindName("CaptureObjectSetEditorPanel")!).Visibility);
                Assert.True(GetDialogField(dialog, "CaptureObjectSetKeyTextBox") is not null, "Set Key input must be present.");
                Assert.True(GetDialogField(dialog, "CaptureObjectSetTargetTextBox") is not null, "Placed Object display must be present.");
                Assert.True(GetDialogField(dialog, "CaptureObjectSetTargetButton") is not null, "Placed Object picker button must be present.");

                ((TextBox)GetDialogField(dialog, "CaptureObjectSetKeyTextBox")!).Text = "liftOccupants";
                typeof(RoomActionEditorDialog)
                    .GetField("_captureObjectSetTargetId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(dialog, placed.ObjectId);
                typeof(RoomActionEditorDialog)
                    .GetMethod("TryApplyObjectSetPayloadInputs", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(dialog, null);

                var workingCopy = (CommandAction)typeof(RoomActionEditorDialog)
                    .GetField("_workingCopy", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(dialog)!;
                var payload = Assert.IsType<CaptureObjectsOnObjectFootprintPayload>(workingCopy.Payload);
                Assert.Equal("liftOccupants", payload.ObjectSetKey);
                Assert.Equal(placed.ObjectId, payload.TargetObjectId);
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        StaTestThreadRunner.StartAndWait(thread, nameof(CaptureObjectSetEditor_ShowsPlacedObjectAndSetKeyFields));
        if (failure is not null) throw failure;
    }

    [Fact]
    public void CheckObjectSetEditor_OffersCaptureKeysFromCurrentSequence()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var action = new CommandAction
                {
                    Name = "Check riders",
                    ActionType = CommandActionType.CheckObjectSetContainsObject,
                    Payload = new CheckObjectSetContainsObjectPayload(string.Empty, "CurrentPlayer")
                };
                var dialog = new RoomActionEditorDialog(
                    action,
                    PropertyResolutionScope.Object,
                    new GamePropertyPickerContext(new ProjectModel(), null, PropertyResolutionScope.Object, _ => []),
                    [], [], [],
                    objectSetKeySuggestions: ["elevatorRiders"]);

                var picker = (ComboBox)dialog.FindName("CheckObjectSetKeyComboBox")!;
                Assert.Contains("elevatorRiders", Assert.IsAssignableFrom<IEnumerable<string>>(picker.ItemsSource));
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        StaTestThreadRunner.StartAndWait(thread, nameof(CheckObjectSetEditor_OffersCaptureKeysFromCurrentSequence));
        if (failure is not null) throw failure;
    }

    private static object? GetDialogField(RoomActionEditorDialog dialog, string fieldName)
    {
        return typeof(RoomActionEditorDialog)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(dialog);
    }

    [Fact]
    public void CueChoices_PreserveSavedAbsoluteTargetAndOfferExplicitLocalAndClearChoices()
    {
        var baseId = Guid.Parse("39a297a1-ac6d-46e1-ad54-438907697f3c");
        var placedId = Guid.Parse("46698d7c-24ef-4c92-85f6-b7ca6d6af748");
        var child = new GameObject { Name = "Door" };
        var baseObject = new GameObject { ObjectId = baseId, Name = "Elevator", ContainedObjects = [child] };
        var choices = new List<MaterializeSourceObjectChoiceItem>
        {
            new() { ObjectId = baseId, SourceObject = baseObject, DisplayName = "Elevator", SourceCategory = "Area Base Objects" },
            new() { ObjectId = child.ObjectId, SourceObject = child, DisplayName = "Door", SourceCategory = "Area Base Objects" },
            new() { ObjectId = placedId, DisplayName = "Elevator", SourceCategory = "Current Room" }
        };

        var shown = PresentationCueTargetChoices.Build(choices, baseId, placedId);
        Assert.Contains(shown, choice => choice.ObjectId == placedId && choice.DisplayName.Contains("absolute"));
        Assert.Contains(shown, choice => choice.ObjectId == baseId && choice.DisplayName.Contains("This base object (local)"));
        Assert.Contains(shown, choice => choice.ObjectId == child.ObjectId && choice.DisplayName.Contains("Base child (local)"));
        Assert.Contains(shown, choice => choice.ObjectId is null && choice.DisplayName.Contains("Command primary target"));
        var childOwned = PresentationCueTargetChoices.Build(choices, child.ObjectId, placedId);
        Assert.Contains(childOwned, choice => choice.ObjectId == baseId && choice.DisplayName.Contains("This base object (local)"));

        var outsideId = Guid.NewGuid();
        var reopened = PresentationCueTargetChoices.Build(choices, baseId, outsideId);
        Assert.Contains(reopened, choice => choice.ObjectId == outsideId && choice.DisplayName.Contains("Saved target (absolute)"));
    }

    [Fact]
    public void CueDialog_LoadNoOpAndExplicitSelections_PreserveIntendedId()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var baseObject = new GameObject { Name = "Elevator", ContainedObjects = [new GameObject { Name = "Door" }] };
                var placed = new GameObject { Name = "Placed Elevator" };
                var savedOutside = Guid.Parse("46698d7c-24ef-4c92-85f6-b7ca6d6af748");
                var choices = new List<MaterializeSourceObjectChoiceItem>
                {
                    new() { ObjectId = baseObject.ObjectId, SourceObject = baseObject, DisplayName = baseObject.Name, SourceCategory = "Area Base Objects" },
                    new() { ObjectId = baseObject.ContainedObjects[0].ObjectId, SourceObject = baseObject.ContainedObjects[0], DisplayName = "Door", SourceCategory = "Area Base Objects" },
                    new() { ObjectId = placed.ObjectId, SourceObject = placed, DisplayName = placed.Name, SourceCategory = "Current Room" }
                };

                foreach (var inline in new[] { false, true })
                {
                    foreach (var selected in new Guid?[] { savedOutside, baseObject.ObjectId, baseObject.ContainedObjects[0].ObjectId, placed.ObjectId, null })
                    {
                        var action = new CommandAction
                        {
                            Name = inline ? "Inline cue" : "Ordinary cue",
                            ActionType = CommandActionType.ClearObjectPresentationCues,
                            ObjectPresentationCueObjectId = savedOutside
                        };
                        if (inline)
                        {
                            action = JsonExportService.ToCommandActionModel(JsonExportService.ToCommandActionDtoForSequence(action, []));
                        }

                        var dialog = new RoomActionEditorDialog(
                            action,
                            PropertyResolutionScope.Object,
                            new GamePropertyPickerContext(new ProjectModel(), baseObject, PropertyResolutionScope.Object, _ => []),
                            [],
                            choices,
                            [],
                            defaultCurrentObjectId: baseObject.ObjectId);
                        dialog.Loaded += (_, _) =>
                        {
                            try
                            {
                                var combo = (ComboBox)dialog.FindName("ObjectPresentationCueObjectIdComboBox")!;
                                Assert.Equal(savedOutside, combo.SelectedValue);
                                Assert.Contains("Saved target (absolute)", combo.SelectedItem!.GetType()
                                    .GetProperty("DisplayName")!.GetValue(combo.SelectedItem)!.ToString());
                                if (selected != savedOutside)
                                {
                                    combo.SelectedValue = selected;
                                }
                                typeof(RoomActionEditorDialog)
                                    .GetMethod("Save_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .Invoke(dialog, [dialog, new System.Windows.RoutedEventArgs()]);
                            }
                            finally
                            {
                                if (dialog.IsVisible)
                                {
                                    dialog.Close();
                                }
                            }
                        };
                        Assert.True(dialog.ShowDialog());
                        Assert.Equal(selected, action.ObjectPresentationCueObjectId);
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        StaTestThreadRunner.StartAndWait(thread, nameof(CueDialog_LoadNoOpAndExplicitSelections_PreserveIntendedId));
        if (failure is not null) throw failure;
    }

    [Fact]
    public void CueDialog_SwitchingBetweenSingleAndSetTargetsKeepsPayloadAndExportsExclusive()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var objectId = Guid.NewGuid();
                var gameObject = new GameObject { ObjectId = objectId, Name = "Elevator" };
                var action = new CommandAction
                {
                    Name = "Show occupants",
                    ActionType = CommandActionType.AddObjectPresentationCue,
                    ObjectPresentationCueObjectId = objectId,
                    ObjectPresentationCueEffectKey = new PresentationEffectsCatalogService().GetObjectCueEligibleEffectKeys().First(),
                    Payload = new AddObjectPresentationCuePayload(objectId, "appearance.selection.outline.cyan")
                };
                var choices = new List<MaterializeSourceObjectChoiceItem>
                {
                    new() { ObjectId = objectId, SourceObject = gameObject, DisplayName = gameObject.Name, SourceCategory = "Current Room" }
                };
                var captureAction = new CommandAction
                {
                    Name = "Capture riders",
                    ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
                    Payload = new CaptureObjectsOnObjectFootprintPayload("liftOccupants", objectId)
                };
                var dialog = new RoomActionEditorDialog(
                    action,
                    PropertyResolutionScope.Object,
                    new GamePropertyPickerContext(new ProjectModel(), gameObject, PropertyResolutionScope.Object, _ => []),
                    [],
                    choices,
                    [],
                    defaultCurrentObjectId: objectId,
                    availableActions: [captureAction]);
                dialog.Loaded += (_, _) =>
                {
                    try
                    {
                        var setMode = (CheckBox)dialog.FindName("ObjectPresentationCueSetModeCheckBox")!;
                        setMode.IsChecked = true;
                        typeof(RoomActionEditorDialog)
                            .GetMethod("ObjectPresentationCueSetMode_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(dialog, [setMode, new System.Windows.RoutedEventArgs()]);
                        var setKeyPicker = (ComboBox)dialog.FindName("ObjectPresentationCueSetKeyComboBox")!;
                        Assert.Contains("liftOccupants", (IReadOnlyList<string>)setKeyPicker.ItemsSource!);
                        setKeyPicker.SelectedItem = "liftOccupants";
                        typeof(RoomActionEditorDialog)
                            .GetMethod("TryApplyObjectSetPayloadInputs", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(dialog, null);
                        var workingCopy = (CommandAction)typeof(RoomActionEditorDialog)
                            .GetField("_workingCopy", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(dialog)!;
                        Assert.Null(workingCopy.ObjectPresentationCueObjectId);
                        Assert.Equal("liftOccupants", Assert.IsType<AddObjectPresentationCuePayload>(workingCopy.Payload).ObjectSetReference!.ObjectSetKey);

                        setMode.IsChecked = false;
                        typeof(RoomActionEditorDialog)
                            .GetMethod("ObjectPresentationCueSetMode_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(dialog, [setMode, new System.Windows.RoutedEventArgs()]);
                        ((ComboBox)dialog.FindName("ObjectPresentationCueObjectIdComboBox")!).SelectedValue = objectId;
                        typeof(RoomActionEditorDialog)
                            .GetMethod("TryApplyObjectSetPayloadInputs", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(dialog, null);
                        Assert.Equal(objectId, workingCopy.ObjectPresentationCueObjectId);
                        Assert.Null(Assert.IsType<AddObjectPresentationCuePayload>(workingCopy.Payload).ObjectSetReference);
                        typeof(RoomActionEditorDialog)
                            .GetMethod("Save_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(dialog, [dialog, new System.Windows.RoutedEventArgs()]);
                    }
                    finally
                    {
                        if (dialog.IsVisible)
                        {
                            dialog.Close();
                        }
                    }
                };

                Assert.True(dialog.ShowDialog());
                Assert.Equal(objectId, action.ObjectPresentationCueObjectId);
                var payload = Assert.IsType<AddObjectPresentationCuePayload>(action.Payload);
                Assert.Null(payload.ObjectSetReference);
                var dto = JsonExportService.ToCommandActionDtoForSequence(action, []);
                var exported = Assert.IsType<ProjectAddObjectPresentationCueActionPayloadDto>(dto.Payload);
                Assert.Equal(objectId, exported.ObjectId);
                Assert.Null(exported.ObjectSetReference);

                var project = new ProjectModel { Name = "Cue Target Switch" };
                project.GlobalScope.GameObjects.Add(gameObject);
                project.GlobalScope.AvailableActions.Add(action);
                var tempRoot = CreateTempRoot();
                try
                {
                    var projectPath = Path.Combine(tempRoot, "CueTargetSwitch.sbe.json");
                    var service = new JsonExportService();
                    service.SaveProjectModel(projectPath, project);
                    var runtimePath = service.ExportCleanProjectV1(projectPath, project);
                    using var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath));
                    var runtimeCue = runtime.RootElement.GetProperty("availableGameActions").EnumerateArray()
                        .Single(item => item.GetProperty("name").GetString() == "Show occupants")
                        .GetProperty("payload");
                    Assert.Equal(objectId, runtimeCue.GetProperty("objectId").GetGuid());
                    Assert.False(runtimeCue.TryGetProperty("objectSetReference", out _));
                }
                finally
                {
                    if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        StaTestThreadRunner.StartAndWait(thread, nameof(CueDialog_SwitchingBetweenSingleAndSetTargetsKeepsPayloadAndExportsExclusive));
        if (failure is not null) throw failure;
    }

    [Fact]
    public void ObjectSetReferenceEditors_OfferCapturedLogicalKeyForMembershipAndNavigation()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var gameObject = new GameObject { Name = "Elevator" };
                var captureAction = new CommandAction
                {
                    Name = "Capture riders",
                    ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
                    Payload = new CaptureObjectsOnObjectFootprintPayload("liftOccupants", gameObject.ObjectId)
                };
                var project = new ProjectModel();
                foreach (var (action, pickerName) in new[]
                {
                    (new CommandAction { Name = "Check riders", ActionType = CommandActionType.CheckObjectSetContainsObject,
                        Payload = new CheckObjectSetContainsObjectPayload("liftOccupants", "CurrentPlayer") }, "CheckObjectSetKeyComboBox"),
                    (new CommandAction { Name = "Ride riders", ActionType = CommandActionType.NavigateObjectSetToAdjacent,
                        Payload = new NavigateObjectSetToAdjacentPayload("liftOccupants", "North") }, "NavigateObjectSetKeyComboBox")
                })
                {
                    var dialog = new RoomActionEditorDialog(
                        action,
                        PropertyResolutionScope.Object,
                        new GamePropertyPickerContext(project, gameObject, PropertyResolutionScope.Object, _ => []),
                        [],
                        [],
                        [],
                        availableActions: [captureAction]);
                    dialog.Loaded += (_, _) =>
                    {
                        try
                        {
                            var picker = (ComboBox)dialog.FindName(pickerName)!;
                            Assert.Contains("liftOccupants", (IReadOnlyList<string>)picker.ItemsSource!);
                            picker.SelectedItem = "liftOccupants";
                            Assert.Equal("liftOccupants", picker.Text);
                        }
                        finally
                        {
                            if (dialog.IsVisible) dialog.Close();
                        }
                    };
                    Assert.False(dialog.ShowDialog());
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        StaTestThreadRunner.StartAndWait(thread, nameof(ObjectSetReferenceEditors_OfferCapturedLogicalKeyForMembershipAndNavigation));
        if (failure is not null) throw failure;
    }

    [Fact]
    public void SelfPropertyToken_SurvivesEditorProjectReloadAndRuntimeExport()
    {
        const string propertyName = "self.IsElevatorDoorOpen";
        var action = new CommandAction { Name = "IsOpen", ActionType = CommandActionType.CheckGameProperty };
        ActionPayloadAccessors.SetCheckGameProperty(action, propertyName, true);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new RoomActionEditorDialog(
                    action,
                    PropertyResolutionScope.Object,
                    new GamePropertyPickerContext(new ProjectModel(), null, PropertyResolutionScope.Object, _ => []),
                    [],
                    [],
                    [],
                    []);
                dialog.Loaded += (_, _) =>
                {
                    try
                    {
                        typeof(RoomActionEditorDialog)
                            .GetMethod("Save_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(dialog, [dialog, new System.Windows.RoutedEventArgs()]);
                    }
                    finally
                    {
                        if (dialog.IsVisible)
                        {
                            dialog.Close();
                        }
                    }
                };
                Assert.True(dialog.ShowDialog());
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        StaTestThreadRunner.StartAndWait(thread, nameof(SelfPropertyToken_SurvivesEditorProjectReloadAndRuntimeExport));
        if (failure is not null) throw failure;
        Assert.Equal(propertyName, ActionPayloadAccessors.GetCheckPropertyName(action));

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var baseObject = new GameObject { Name = "Elevator", AvailableActions = [action] };
            var project = new ProjectModel { Name = "SelfReference", BaseObjects = [baseObject] };
            var path = Path.Combine(tempRoot, "SelfReference.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(path, project);
            var reopened = service.TryLoadProjectModel(path);
            Assert.NotNull(reopened);
            Assert.Equal(propertyName, ActionPayloadAccessors.GetCheckPropertyName(
                Assert.Single(Assert.Single(reopened!.BaseObjects).AvailableActions)));
            service.ExportCleanProjectV1(path, reopened);
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "GameRuntimeJson", "GameObject",
                baseObject.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json")));
            Assert.Equal(propertyName, doc.RootElement.GetProperty("availableGameActions")[0]
                .GetProperty("payload").GetProperty("propertyName").GetString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BaseActionAbsolutePlacementWarning_CoversOrdinaryAndInlineActions()
    {
        var baseObject = new GameObject { Name = "Elevator" };
        var placedObject = new GameObject { Name = "Elevator", LinkedBaseObjectId = baseObject.ObjectId };
        var ordinary = Cue("Ordinary", placedObject.ObjectId);
        var inline = Cue("Inline", placedObject.ObjectId);
        baseObject.AvailableActions.Add(ordinary);
        baseObject.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = "Use Elevator",
            Steps =
            [
                new ProjectSequenceStepDto
                {
                    StepId = Guid.NewGuid(),
                    StepType = SequenceStepType.Action,
                    InlineAction = JsonExportService.ToCommandActionDtoForSequence(inline, [])
                }
            ]
        });
        var room = new Room { Name = "Room", GameObjects = [placedObject] };
        var project = new ProjectModel
        {
            BaseObjects = [baseObject],
            Planets = [new Planet { Countries = [new Country { Areas = [new Area { Rooms = [room] }] }] }]
        };
        var registry = new ValidationRuleRegistry();
        registry.Register(new BaseActionAbsolutePlacementTargetRule());
        var issues = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.Equal(2, issues.Count(issue => issue.RuleId == "ACT-021"));
        Assert.All(issues, issue => Assert.Contains(baseObject.ObjectId.ToString("D"), issue.Hint));

        ordinary.ObjectPresentationCueObjectId = baseObject.ObjectId;
        inline.ObjectPresentationCueObjectId = baseObject.ObjectId;
        baseObject.SequenceDefinitions[0].Steps[0].InlineAction = JsonExportService.ToCommandActionDtoForSequence(inline, []);
        Assert.DoesNotContain(new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues,
            issue => issue.RuleId == "ACT-021");
    }

    [Fact]
    public void BaseActionAbsolutePlacementWarning_CoversNestedBaseChildAction()
    {
        var doorTemplateId = Guid.NewGuid();
        var baseChild = new GameObject { Name = "Door A", LinkedBaseObjectId = doorTemplateId };
        var secondBaseChild = new GameObject { Name = "Door B", LinkedBaseObjectId = doorTemplateId };
        var baseObject = new GameObject { Name = "Elevator", ContainedObjects = [baseChild, secondBaseChild] };
        var placedChild = new GameObject { Name = "Door A", LinkedBaseObjectId = doorTemplateId };
        var secondPlacedChild = new GameObject { Name = "Door B", LinkedBaseObjectId = doorTemplateId };
        var placedObject = new GameObject
        {
            Name = "Elevator", LinkedBaseObjectId = baseObject.ObjectId, ContainedObjects = [placedChild, secondPlacedChild]
        };
        baseChild.AvailableActions.Add(Cue("Cue door", secondPlacedChild.ObjectId));
        var project = new ProjectModel
        {
            BaseObjects = [baseObject],
            Planets = [new Planet { Countries = [new Country { Areas = [new Area { Rooms = [new Room { GameObjects = [placedObject] }] }] }] }]
        };
        var registry = new ValidationRuleRegistry();
        registry.Register(new BaseActionAbsolutePlacementTargetRule());

        var issue = Assert.Single(new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues);
        Assert.Contains(secondBaseChild.ObjectId.ToString("D"), issue.Hint);
    }

    [Fact]
    public void RuntimeExport_TwoLinkedPlacementsKeepDistinctIdsAndNoCopiedBehavior()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var sharedId = Guid.NewGuid();
            var crossBoundaryId = Guid.NewGuid();
            var doorTemplate = new GameObject { Name = "Door Template" };
            var baseChildA = new GameObject { Name = "Door A", LinkedBaseObjectId = doorTemplate.ObjectId };
            var baseChildB = new GameObject { Name = "Door B", LinkedBaseObjectId = doorTemplate.ObjectId };
            var baseObject = new GameObject
            {
                Name = "Elevator",
                ContainedObjects = [baseChildA, baseChildB],
                AvailableActions = [Cue("CueBaseRoot", null), Cue("CueBaseChild", null), Cue("CueAbsolutePlacement", null)],
                Variables = [new GamePropertyDefinition { Name = "BoundarySignal", DefaultValue = "false", SharedVariableId = crossBoundaryId }]
            };
            baseObject.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
            {
                SequenceId = Guid.NewGuid(),
                DisplayName = "Use Elevator",
                Branches = [new ProjectSequenceBranchDto { BranchId = "main", DisplayName = "Main", DisplayColor = "#64748B" }]
            });
            var project = new ProjectModel { Name = "TwoElevators", ObjectTemplates = [doorTemplate], BaseObjects = [baseObject] };
            var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

            GameObject Place()
            {
                var clone = (GameObject)typeof(MainWindowViewModel)
                    .GetMethod("CloneTemplateObject", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [baseObject])!;
                typeof(MainWindowViewModel)
                    .GetMethod("ConfigureAsLinkedRoomInstance", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(viewModel, [clone, baseObject, baseObject.ObjectId]);
                Assert.Empty(clone.AvailableActions);
                Assert.Empty(clone.SequenceDefinitions);
                Assert.Empty(clone.ContainedObjects);
                // Legacy copied behavior must also be stripped when old project data is loaded or exported.
                clone.AvailableActions.Add(Cue("Stale", null));
                clone.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto { SequenceId = Guid.NewGuid(), DisplayName = "Stale" });
                clone.Variables.Add(new GamePropertyDefinition { Name = "IsMoving", DefaultValue = "false", SharedVariableId = sharedId });
                return clone;
            }

            var first = Place();
            var second = Place();
            baseObject.AvailableActions[0].ObjectPresentationCueObjectId = baseObject.ObjectId;
            baseObject.AvailableActions[1].ObjectPresentationCueObjectId = baseChildA.ObjectId;
            baseObject.AvailableActions[2].ObjectPresentationCueObjectId = first.ObjectId;
            baseObject.SequenceDefinitions[0].Steps.Add(new ProjectSequenceStepDto
            {
                StepId = Guid.NewGuid(),
                BranchId = "main",
                StepType = SequenceStepType.Action,
                InlineAction = JsonExportService.ToCommandActionDtoForSequence(Cue("CueInlineBaseRoot", baseObject.ObjectId), [])
            });
            baseObject.SequenceDefinitions[0].Steps.Add(new ProjectSequenceStepDto
            {
                StepId = Guid.NewGuid(),
                BranchId = "main",
                StepType = SequenceStepType.Action,
                InlineAction = JsonExportService.ToCommandActionDtoForSequence(Cue("CueInlineBaseChild", baseChildB.ObjectId), [])
            });
            baseObject.SequenceDefinitions[0].Steps.Add(new ProjectSequenceStepDto
            {
                StepId = Guid.NewGuid(),
                BranchId = "main",
                StepType = SequenceStepType.Action,
                InlineAction = JsonExportService.ToCommandActionDtoForSequence(Cue("CueInlineAbsolutePlacement", first.ObjectId), [])
            });
            var room = new Room
            {
                Name = "Room",
                GameObjects = [first, second],
                Variables = [new GamePropertyDefinition { Name = "BoundarySignal", DefaultValue = "false", SharedVariableId = crossBoundaryId }]
            };
            project.Planets.Add(new Planet { Countries = [new Country { Areas = [new Area { Rooms = [room] }] }] });
            ScopeHierarchy.AttachParents(project);
            var path = Path.Combine(tempRoot, "TwoElevators.sbe.json");
            var service = new JsonExportService();
            service.SaveProjectModel(path, project);
            var reopened = service.TryLoadProjectModel(path);
            Assert.NotNull(reopened);
            var reopenedPlacements = reopened!.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().GameObjects;
            Assert.Equal(new[] { first.ObjectId, second.ObjectId }, reopenedPlacements.Select(obj => obj.ObjectId));
            Assert.All(reopenedPlacements, placement =>
            {
                Assert.Equal(baseObject.ObjectId, placement.LinkedBaseObjectId);
                Assert.Empty(placement.AvailableActions);
                Assert.Empty(placement.SequenceDefinitions);
                Assert.Empty(placement.ContainedObjects);
            });
            Assert.Equal(3, Assert.Single(reopened.BaseObjects).AvailableActions.Count);
            Assert.Single(Assert.Single(reopened.BaseObjects).SequenceDefinitions);
            service.ExportCleanProjectV1(path, reopened);

            foreach (var placement in new[] { first, second })
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "GameRuntimeJson", "GameObject", placement.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json")));
                var root = doc.RootElement;
                Assert.Equal(placement.ObjectId, root.GetProperty("Id").GetGuid());
                Assert.Equal(baseObject.ObjectId, root.GetProperty("linkedBaseObjectId").GetGuid());
                Assert.False(root.TryGetProperty("availableGameActions", out _));
                Assert.False(root.TryGetProperty("sequenceDefinitions", out _));
                Assert.False(root.TryGetProperty("containedObjects", out _));
                Assert.Equal(sharedId, root.GetProperty("gameProperties").EnumerateArray()
                    .Single(property => property.GetProperty("name").GetString() == "IsMoving")
                    .GetProperty("sharedVariableId").GetGuid());
            }

            Assert.NotEqual(first.ObjectId, second.ObjectId);
            using var baseDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "GameRuntimeJson", "GameObject", baseObject.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json")));
            Assert.True(baseDoc.RootElement.TryGetProperty("availableGameActions", out _));
            Assert.True(baseDoc.RootElement.TryGetProperty("sequenceDefinitions", out _));
            var exportedActions = baseDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().ToDictionary(
                action => action.GetProperty("name").GetString()!,
                action => action.GetProperty("payload").GetProperty("objectId").GetGuid());
            Assert.Equal(baseObject.ObjectId, exportedActions["CueBaseRoot"]);
            Assert.Equal(baseChildA.ObjectId, exportedActions["CueBaseChild"]);
            Assert.Equal(first.ObjectId, exportedActions["CueAbsolutePlacement"]);
            var exportedInlineTargets = baseDoc.RootElement.GetProperty("sequenceDefinitions")[0]
                .GetProperty("steps").EnumerateArray()
                .Select(step => step.GetProperty("inlineAction").GetProperty("payload").GetProperty("objectId").GetGuid())
                .ToArray();
            Assert.Equal([baseObject.ObjectId, baseChildB.ObjectId, first.ObjectId], exportedInlineTargets);
            Assert.Equal(crossBoundaryId, baseDoc.RootElement.GetProperty("gameProperties").EnumerateArray()
                .Single(property => property.GetProperty("name").GetString() == "BoundarySignal")
                .GetProperty("sharedVariableId").GetGuid());
            var baseChildren = baseDoc.RootElement.GetProperty("containedObjects").EnumerateArray().ToArray();
            Assert.Equal(2, baseChildren.Select(child => child.GetProperty("Id").GetGuid()).Distinct().Count());
            Assert.All(baseChildren, child => Assert.Equal(doorTemplate.ObjectId, child.GetProperty("linkedBaseObjectId").GetGuid()));
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "GameRuntimeJson", "Room", room.Id.ToString("D").ToUpperInvariant() + ".runtime.json")));
            Assert.Equal(crossBoundaryId, roomDoc.RootElement.GetProperty("gameProperties").EnumerateArray()
                .Single(property => property.GetProperty("name").GetString() == "BoundarySignal")
                .GetProperty("sharedVariableId").GetGuid());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CloneIndependentBase_RemapsActionAndInlineTargetsToNewObjects()
    {
        var door = new GameObject { Name = "Door" };
        var source = new GameObject
        {
            Name = "Elevator",
            ContainedObjects = [door],
            AvailableActions = [Cue("Cue door", door.ObjectId)]
        };
        source.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = "Use Elevator",
            Steps =
            [
                new ProjectSequenceStepDto
                {
                    StepId = Guid.NewGuid(),
                    StepType = SequenceStepType.Action,
                    InlineAction = JsonExportService.ToCommandActionDtoForSequence(Cue("Cue self", source.ObjectId), [])
                }
            ]
        });

        var clone = (GameObject)typeof(MainWindowViewModel)
            .GetMethod("CloneTemplateObject", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [source])!;

        Assert.NotEqual(source.ObjectId, clone.ObjectId);
        Assert.NotEqual(door.ObjectId, clone.ContainedObjects[0].ObjectId);
        Assert.Equal(clone.ContainedObjects[0].ObjectId, Assert.Single(clone.AvailableActions).ObjectPresentationCueObjectId);
        Assert.Equal(clone.ObjectId, JsonExportService.ToCommandActionModel(
            Assert.Single(Assert.Single(clone.SequenceDefinitions).Steps).InlineAction!).ObjectPresentationCueObjectId);
    }

    private static CommandAction Cue(string name, Guid? targetId) => new()
    {
        Name = name,
        ActionType = CommandActionType.AddObjectPresentationCue,
        ObjectPresentationCueObjectId = targetId,
        ObjectPresentationCueEffectKey = "scale.grow.medium"
    };

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
