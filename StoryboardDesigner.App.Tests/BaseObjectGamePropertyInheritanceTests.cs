using System.Reflection;
using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Validation.Rules.Scripting;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class BaseObjectGamePropertyInheritanceTests
{
    [Fact]
    public void PlacedObjectPropertyView_IncludesBaseOnlyPropertyAndMarksItsSource()
    {
        var baseProperty = NumericProperty("BaseSpeed", "12");
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        ScopeHierarchy.AttachParents(project);
        var parent = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", BindingFlags.NonPublic | BindingFlags.Static)!;

        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [parent, instance, instance.Variables, PropertyResolutionScope.Object, null, null]));

        var propertyNode = Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>());
        Assert.Equal("BaseSpeed", propertyNode.Variable.Name);
        Assert.Empty(instance.Variables);
        Assert.True(propertyNode.IsInherited);
        Assert.Same(baseObject, propertyNode.DefinitionOwner);
        Assert.True(propertyNode.ShowInheritedOwnershipIcon);
        Assert.False(propertyNode.ShowLocalCopyOwnershipIcon);
        Assert.Contains("Inherited from Base Object 'Elevator'", propertyNode.OwnershipToolTip);
    }

    [Fact]
    public void UpdatingInheritedRowValue_ChangesOnlyPlacementDisplayState()
    {
        var baseProperty = NumericProperty("MoveSpeed", "12");
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        ScopeHierarchy.AttachParents(project);
        var parent = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [parent, instance, instance.Variables, PropertyResolutionScope.Object, null, null]));
        var node = Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>());

        node.UpdateEffectiveValue("3");

        Assert.Equal("12", baseProperty.DefaultValue);
        Assert.Same(baseProperty, node.Variable);
        Assert.Equal("3", node.DisplayValue);
        Assert.True(node.HasValueOverride);
    }

    [Fact]
    public void FirstInstanceValueSave_CreatesOneCopyAndLeavesBaseAndOtherInstanceUnchanged()
    {
        var baseProperty = NumericProperty("elevatorHomeFloor", "0");
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        var second = new GameObject { Name = "Elevator B", LinkedBaseObjectId = baseObject.ObjectId };
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        room.GameObjects.Add(second);
        var node = CreatePlacementPropertyNode(project, instance);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project,
            new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        Assert.True(viewModel.SaveInstancePropertyValue(node, "3"));

        var local = Assert.Single(instance.Variables);
        var localId = local.Id;
        Assert.Same(local, node.Variable);
        Assert.NotEqual(Guid.Empty, local.Id);
        Assert.NotEqual(baseProperty.Id, local.Id);
        Assert.Equal("3", local.DefaultValue);
        Assert.Equal("0", baseProperty.DefaultValue);
        Assert.Empty(second.Variables);
        Assert.Equal("elevatorHomeFloor", local.Name);
        Assert.Equal(GamePropertyValueRestriction.Numeric, local.ValueRestriction);
        Assert.Equal(GamePropertyLifetime.Singleton, local.Lifetime);
        Assert.Equal("3", node.DisplayValue);
        Assert.True(node.ShowLocalCopyOwnershipIcon);
        Assert.False(node.ShowInheritedOwnershipIcon);

        Assert.True(viewModel.SaveInstancePropertyValue(node, "4"));
        Assert.Same(local, Assert.Single(instance.Variables));
        Assert.Equal(localId, local.Id);
        Assert.Equal("4", local.DefaultValue);

        var secondNode = CreatePlacementPropertyNode(project, second);
        Assert.True(viewModel.SaveInstancePropertyValue(secondNode, "5"));
        var secondLocal = Assert.Single(second.Variables);
        Assert.NotEqual(Guid.Empty, secondLocal.Id);
        Assert.NotEqual(baseProperty.Id, secondLocal.Id);
        Assert.NotEqual(local.Id, secondLocal.Id);
        Assert.Equal("0", baseProperty.DefaultValue);

        var ids = new[] { baseProperty.Id, local.Id, secondLocal.Id };
        Assert.Equal(ids.Length, ids.Distinct().Count());
        var lookup = ValidationLookupService.Build(project);
        foreach (var property in new[] { baseProperty, local, secondLocal })
        {
            Assert.True(lookup.TryGetPropertyByVariableId(property.Id, out var descriptor));
            Assert.Equal(property.Name, descriptor.VariableName);
        }
    }

    [Fact]
    public void NewLinkedPlacement_ClearsCopiedSnapshotsButRestorePreservesEqualLocalValues()
    {
        var baseProperty = NumericProperty("elevatorHomeFloor", "0");
        var (project, baseObject, _) = BuildLinkedObjects(baseProperty);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project,
            new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        var copiedSnapshot = LocalCopy(baseProperty, baseProperty.DefaultValue);
        var newPlacement = new GameObject { Name = "New Elevator", Variables = [copiedSnapshot] };
        var configure = typeof(MainWindowViewModel).GetMethod("ConfigureAsLinkedRoomInstance", BindingFlags.NonPublic | BindingFlags.Instance)!;

        configure.Invoke(viewModel, [newPlacement, baseObject, baseObject.ObjectId, true]);

        Assert.Empty(newPlacement.Variables);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        room.GameObjects.Add(newPlacement);
        ScopeHierarchy.AttachParents(project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var path = Path.Combine(tempRoot, "InheritedPlacement.sbr");
            var exporter = new JsonExportService();
            exporter.SaveProjectModel(path, project);
            var reopened = Assert.IsType<ProjectModel>(exporter.TryLoadProjectModel(path));
            var reopenedRoom = reopened.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var reopenedPlacement = Assert.Single(reopenedRoom.GameObjects, item => item.Name == newPlacement.Name);
            Assert.Empty(reopenedPlacement.Variables);

            exporter.ExportCleanProjectV1(path, reopened);
            var objectPath = Path.Combine(Path.GetDirectoryName(path)!, "GameRuntimeJson", "GameObject", reopenedPlacement.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtime = JsonDocument.Parse(File.ReadAllText(objectPath));
            var runtimeProperty = runtime.RootElement.GetProperty("gameProperties").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "elevatorHomeFloor");
            Assert.Equal("0", runtimeProperty.GetProperty("defaultValue").GetString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        var restoredPlacement = new GameObject { Variables = [LocalCopy(baseProperty, baseProperty.DefaultValue)] };
        configure.Invoke(viewModel, [restoredPlacement, baseObject, baseObject.ObjectId, false]);

        Assert.Single(restoredPlacement.Variables);
        Assert.Equal("0", restoredPlacement.Variables[0].DefaultValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InstanceRoomSharingAndValueSave_ReuseOneLocalRecordAndRoundTrip(bool shareFirst)
    {
        var baseProperty = new GamePropertyDefinition
        {
            Name = "DoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Lifetime = GamePropertyLifetime.Singleton
        };
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var roomProperty = new GamePropertyDefinition
        {
            Name = "RoomDoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Lifetime = GamePropertyLifetime.Singleton
        };
        room.Variables.Add(roomProperty);
        var second = new GameObject { Name = "Elevator B", LinkedBaseObjectId = baseObject.ObjectId };
        room.GameObjects.Add(second);
        ScopeHierarchy.AttachParents(project);

        var selectedTarget = string.Empty;
        var treeContext = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) =>
            {
                selectedTarget = choices.Single(choice => choice.OwnerVariable == "RoomDoorOpen").Value;
                return (true, selectedTarget);
            }
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var node = CreatePlacementPropertyNode(project, instance);

        if (shareFirst)
        {
            Assert.True(viewModel.ExecuteTreeContextAction("share-game-property", node));
            Assert.Single(instance.Variables);
            Assert.NotEqual(Guid.Empty, node.Variable.Id);
            Assert.NotEqual(baseProperty.Id, node.Variable.Id);
        }

        Assert.True(viewModel.SaveInstancePropertyValue(node, "true"));
        var local = Assert.Single(instance.Variables);
        Assert.Same(local, node.Variable);
        Assert.Equal("true", local.DefaultValue);
        Assert.Equal("false", baseProperty.DefaultValue);

        if (!shareFirst)
        {
            Assert.True(viewModel.ExecuteTreeContextAction("share-game-property", node));
        }

        Assert.Same(local, Assert.Single(instance.Variables));
        Assert.Equal(local.SharedVariableId, roomProperty.SharedVariableId);
        Assert.NotEqual(Guid.Empty, local.Id);
        Assert.NotEqual(baseProperty.Id, local.Id);
        Assert.NotEqual(roomProperty.Id, local.Id);
        Assert.Equal(local.SharedVariableId, roomProperty.SharedVariableId);
        Assert.Empty(second.Variables);
        Assert.Equal("DoorOpen", local.Name);
        Assert.Equal(GamePropertyValueRestriction.TrueFalse, local.ValueRestriction);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var path = Path.Combine(tempRoot, "PlacementSharing.sbr");
            var exporter = new JsonExportService();
            exporter.SaveProjectModel(path, project);
            var reopened = Assert.IsType<ProjectModel>(exporter.TryLoadProjectModel(path));
            var reopenedArea = reopened.Planets.Single().Countries.Single().Areas.Single();
            var reopenedRoom = reopenedArea.Rooms.Single();
            var reopenedInstance = reopenedRoom.GameObjects.Single(item => item.Name == instance.Name);
            var reopenedLocal = Assert.Single(reopenedInstance.Variables);
            var reopenedRoomProperty = Assert.Single(reopenedRoom.Variables);
            Assert.Equal(local.Id, reopenedLocal.Id);
            var reopenedEffective = Assert.Single(EffectiveGamePropertyResolver.Resolve(
                reopenedInstance,
                id => EffectiveGamePropertyResolver.ResolveBaseObject(reopened, id)));
            Assert.Equal(reopenedLocal.Id, reopenedEffective.Definition.Id);
            Assert.Equal(baseProperty.Name, reopenedEffective.Definition.Name);
            Assert.Equal(baseProperty.ValueRestriction, reopenedEffective.Definition.ValueRestriction);
            Assert.Equal(baseProperty.Lifetime, reopenedEffective.Definition.Lifetime);
            Assert.Equal("true", reopenedLocal.DefaultValue);
            Assert.Equal(reopenedLocal.SharedVariableId, reopenedRoomProperty.SharedVariableId);

            exporter.ExportCleanProjectV1(path, reopened);
            using var objectJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "GameRuntimeJson", "GameObject", reopenedInstance.ObjectId.ToString("D").ToUpperInvariant() + ".runtime.json")));
            using var roomJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "GameRuntimeJson", "Room", reopenedRoom.Id.ToString("D").ToUpperInvariant() + ".runtime.json")));
            var runtimeProperty = objectJson.RootElement.GetProperty("gameProperties").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "DoorOpen");
            Assert.Equal(reopenedLocal.Id, runtimeProperty.GetProperty("id").GetGuid());
            Assert.Equal("true", runtimeProperty.GetProperty("defaultValue").GetString());
            Assert.Equal(reopenedLocal.SharedVariableId?.ToString("D"), runtimeProperty.GetProperty("sharedVariableId").GetString());
            var runtimeRoomProperty = roomJson.RootElement.GetProperty("gameProperties").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "RoomDoorOpen");
            Assert.Equal(reopenedLocal.SharedVariableId?.ToString("D"), runtimeRoomProperty.GetProperty("sharedVariableId").GetString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LeavingInheritedShare_CreatesLocalCopyAndPreservesBaseAndRoomMembership()
    {
        var sharedId = Guid.NewGuid();
        var baseProperty = new GamePropertyDefinition
        {
            Name = "DoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = sharedId
        };
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var roomProperty = new GamePropertyDefinition
        {
            Name = "RoomDoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = sharedId
        };
        room.Variables.Add(roomProperty);
        project.SharedVariables.Add(new SharedVariableDefinition
        {
            Id = sharedId,
            Name = "Base room door state",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Participants =
            [
                new SharedVariableParticipant { Kind = "object", OwnerId = baseObject.ObjectId, VariableName = "DoorOpen" },
                new SharedVariableParticipant { Kind = "global", OwnerId = roomProperty.Id, VariableName = "RoomDoorOpen" }
            ]
        });
        ScopeHierarchy.AttachParents(project);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project,
            new TreeContextInteractionServiceStub(), new ProjectUiServiceStub { ConfirmResult = true });
        var node = CreatePlacementPropertyNode(project, instance);

        Assert.True(viewModel.ExecuteTreeContextAction("leave-shared-variable", node));

        Assert.Null(Assert.Single(instance.Variables).SharedVariableId);
        Assert.Equal(sharedId, baseProperty.SharedVariableId);
        Assert.Equal(sharedId, roomProperty.SharedVariableId);
        Assert.Contains(project.SharedVariables.Single(shared => shared.Id == sharedId).Participants,
            participant => participant.OwnerId == baseObject.ObjectId && participant.VariableName == "DoorOpen");
        Assert.Contains(project.SharedVariables.Single(shared => shared.Id == sharedId).Participants,
            participant => participant.OwnerId == roomProperty.Id && participant.VariableName == "RoomDoorOpen");
    }

    [Fact]
    public void SharingPlacedBaseProperty_UsesPlacementOwnerAndReviewSurvivesSaveReopen()
    {
        var baseProperty = new GamePropertyDefinition
        {
            Name = "DoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var roomProperty = new GamePropertyDefinition
        {
            Name = "RoomDoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        room.Variables.Add(roomProperty);
        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) =>
                (true, choices.Single(choice => choice.OwnerVariable == "RoomDoorOpen").Value)
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var node = CreatePlacementPropertyNode(project, instance);
        AttachPropertyNode(viewModel, node);

        Assert.True(viewModel.ExecuteTreeContextAction("share-game-property", node));
        Assert.True(node.IsShared);
        var local = Assert.Single(instance.Variables);
        var sharedId = Assert.IsType<Guid>(local.SharedVariableId);
        var shared = Assert.Single(project.SharedVariables, entry => entry.Id == sharedId);
        Assert.Contains(shared.Participants, participant =>
            participant.Kind == "object" && participant.OwnerId == instance.ObjectId && participant.VariableName == "DoorOpen");
        Assert.DoesNotContain(shared.Participants, participant =>
            participant.Kind == "object" && participant.OwnerId == baseObject.ObjectId && participant.VariableName == "DoorOpen");

        Assert.True(viewModel.ExecuteTreeContextAction("review-shared-property-relationships", node));
        Assert.Equal("Elevator in Foyer.DoorOpen", treeContext.LastSharedPropertyPath);
        Assert.Contains(treeContext.LastSharedPropertyRelationships, relationship =>
            relationship.ParticipantOwnerId == roomProperty.Id && relationship.ParticipantVariableName == "RoomDoorOpen");

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var path = Path.Combine(tempRoot, "PlacementReview.sbr");
            var exporter = new JsonExportService();
            exporter.SaveProjectModel(path, project);
            var reopened = Assert.IsType<ProjectModel>(exporter.TryLoadProjectModel(path));
            ScopeHierarchy.AttachParents(reopened);
            var reopenedRoom = reopened.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var reopenedInstance = Assert.Single(reopenedRoom.GameObjects);
            var reopenedNode = CreatePlacementPropertyNode(reopened, reopenedInstance);
            var reopenedContext = new TreeContextInteractionServiceStub();
            var reopenedViewModel = MainWindowViewModelTestHarness.CreateViewModel(reopened, reopenedContext, new ProjectUiServiceStub());

            Assert.True(reopenedNode.ShowLocalCopyOwnershipIcon);
            Assert.True(reopenedViewModel.ExecuteTreeContextAction("review-shared-property-relationships", reopenedNode));
            Assert.Equal("Elevator in Foyer.DoorOpen", reopenedContext.LastSharedPropertyPath);
            Assert.Equal(sharedId, reopenedContext.LastSharedPropertySharedVariableId);
            Assert.Contains(reopenedContext.LastSharedPropertyRelationships, relationship =>
                relationship.ParticipantOwnerId == reopenedRoom.Variables.Single().Id
                && relationship.ParticipantVariableName == "RoomDoorOpen");
            Assert.Contains(reopened.SharedVariables.Single(entry => entry.Id == sharedId).Participants, participant =>
                participant.Kind == "object" && participant.OwnerId == reopenedInstance.ObjectId);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void FailedShareOfInheritedMember_DoesNotCreateLocalCopyOrGroup()
    {
        var sourceGroupId = Guid.NewGuid();
        var targetGroupId = Guid.NewGuid();
        var baseProperty = SharedDoorProperty(sourceGroupId);
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var target = SharedDoorProperty(targetGroupId, "RoomDoorOpen");
        room.Variables.Add(target);
        var sourceGroup = SharedGroup(sourceGroupId, "G", ("object", Guid.NewGuid(), "DoorOpen"));
        sourceGroup.Participants[0].OwnerId = project.BaseObjects.Single().ObjectId;
        var targetGroup = SharedGroup(targetGroupId, "H", ("global", target.Id, target.Name));
        project.SharedVariables = [sourceGroup, targetGroup];
        ScopeHierarchy.AttachParents(project);
        var node = CreatePlacementPropertyNode(project, instance);
        var context = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) => (true, choices.Single(choice => choice.OwnerVariable == target.Name).Value)
        };
        var ui = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, context, ui);
        var participantsBefore = project.SharedVariables.Select(group => group.Participants.Select(CloneParticipant).ToArray()).ToArray();

        Assert.False(viewModel.ExecuteTreeContextAction("share-game-property", node));

        Assert.Empty(instance.Variables);
        Assert.Equal(sourceGroupId, baseProperty.SharedVariableId);
        Assert.Equal(targetGroupId, target.SharedVariableId);
        Assert.Equal(new[] { sourceGroupId, targetGroupId }, project.SharedVariables.Select(group => group.Id));
        Assert.Equal(participantsBefore[0], sourceGroup.Participants.Select(CloneParticipant));
        Assert.Equal(participantsBefore[1], targetGroup.Participants.Select(CloneParticipant));
        Assert.False(viewModel.IsProjectDirty);
        Assert.Single(ui.WarningMessages);
    }

    [Fact]
    public void FailedShareOfLocalCopy_PreservesRecordAndGroups()
    {
        var sourceGroupId = Guid.NewGuid();
        var targetGroupId = Guid.NewGuid();
        var baseProperty = SharedDoorProperty(Guid.NewGuid());
        var local = LocalCopy(baseProperty, "true");
        local.SharedVariableId = sourceGroupId;
        var (project, _, instance) = BuildLinkedObjects(baseProperty, local);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var target = SharedDoorProperty(targetGroupId, "RoomDoorOpen");
        room.Variables.Add(target);
        var sourceGroup = SharedGroup(sourceGroupId, "G", ("object", instance.ObjectId, "DoorOpen"));
        var targetGroup = SharedGroup(targetGroupId, "H", ("global", target.Id, target.Name));
        project.SharedVariables = [sourceGroup, targetGroup];
        ScopeHierarchy.AttachParents(project);
        var node = CreatePlacementPropertyNode(project, instance);
        var context = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) => (true, choices.Single(choice => choice.OwnerVariable == target.Name).Value)
        };
        var ui = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, context, ui);
        var id = local.Id;
        var participantsBefore = project.SharedVariables.Select(group => group.Participants.Select(CloneParticipant).ToArray()).ToArray();

        Assert.False(viewModel.ExecuteTreeContextAction("share-game-property", node));

        Assert.Same(local, Assert.Single(instance.Variables));
        Assert.Equal(id, local.Id);
        Assert.Equal("true", local.DefaultValue);
        Assert.Equal(sourceGroupId, local.SharedVariableId);
        Assert.Equal(targetGroupId, target.SharedVariableId);
        Assert.Equal(participantsBefore[0], sourceGroup.Participants.Select(CloneParticipant));
        Assert.Equal(participantsBefore[1], targetGroup.Participants.Select(CloneParticipant));
        Assert.False(viewModel.IsProjectDirty);
    }

    [Fact]
    public void FailedShareOfUnsharedInheritedProperty_DoesNotCreateLocalCopyOrGroupShell()
    {
        var baseProperty = SharedDoorProperty(Guid.Empty);
        baseProperty.SharedVariableId = null;
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var targetGroupId = Guid.NewGuid();
        var target = SharedDoorProperty(targetGroupId, "RoomDoorOpen");
        room.Variables.Add(target);
        var targetGroup = SharedGroup(targetGroupId, "H", ("global", target.Id, target.Name));
        project.SharedVariables = [targetGroup];
        ScopeHierarchy.AttachParents(project);
        var node = CreatePlacementPropertyNode(project, instance);
        var context = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) => (true, choices.Single(choice => choice.OwnerVariable == target.Name).Value)
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, context, new ProjectUiServiceStub());

        Assert.False(viewModel.ExecuteTreeContextAction("share-game-property", node));

        Assert.Empty(instance.Variables);
        Assert.Null(baseProperty.SharedVariableId);
        Assert.Equal(targetGroupId, target.SharedVariableId);
        Assert.Single(project.SharedVariables);
        Assert.False(viewModel.IsProjectDirty);
    }

    [Fact]
    public void CancelledSharePicker_DoesNotCreateLocalCopyOrChangeMembership()
    {
        var groupId = Guid.NewGuid();
        var baseProperty = SharedDoorProperty(groupId);
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        project.SharedVariables = [SharedGroup(groupId, "G", ("object", project.BaseObjects.Single().ObjectId, "DoorOpen"))];
        ScopeHierarchy.AttachParents(project);
        var node = CreatePlacementPropertyNode(project, instance);
        var context = new TreeContextInteractionServiceStub { ChooseVariableHandler = (_, _, _, _) => (false, string.Empty) };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, context, new ProjectUiServiceStub());

        Assert.False(viewModel.ExecuteTreeContextAction("share-game-property", node));

        Assert.Empty(instance.Variables);
        Assert.Equal(groupId, baseProperty.SharedVariableId);
        Assert.Single(project.SharedVariables);
        Assert.False(viewModel.IsProjectDirty);
    }

    [Fact]
    public void ReviewingAndRemovingPlacementGroup_UsesLocalMembershipAndPreservesBaseAndOtherPlacement()
    {
        var baseGroupId = Guid.NewGuid();
        var placementGroupId = Guid.NewGuid();
        var baseProperty = new GamePropertyDefinition
        {
            Name = "DoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = baseGroupId
        };
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var roomProperty = new GamePropertyDefinition
        {
            Name = "RoomDoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = placementGroupId
        };
        room.Variables.Add(roomProperty);
        var local = LocalCopy(baseProperty, baseProperty.DefaultValue);
        local.SharedVariableId = placementGroupId;
        instance.Variables.Add(local);
        var otherPlacement = new GameObject
        {
            Name = "Elevator B",
            LinkedBaseObjectId = baseObject.ObjectId,
            Variables = [LocalCopy(baseProperty, "false")]
        };
        otherPlacement.Variables[0].SharedVariableId = baseGroupId;
        room.GameObjects.Add(otherPlacement);
        project.SharedVariables =
        [
            new SharedVariableDefinition
            {
                Id = baseGroupId,
                Name = "Base door state",
                Participants =
                [
                    new SharedVariableParticipant { Kind = "object", OwnerId = baseObject.ObjectId, VariableName = "DoorOpen" },
                    new SharedVariableParticipant { Kind = "object", OwnerId = otherPlacement.ObjectId, VariableName = "DoorOpen" }
                ]
            },
            new SharedVariableDefinition
            {
                Id = placementGroupId,
                Name = "Placement door state",
                Participants =
                [
                    new SharedVariableParticipant { Kind = "object", OwnerId = instance.ObjectId, VariableName = "DoorOpen" },
                    new SharedVariableParticipant { Kind = "global", OwnerId = roomProperty.Id, VariableName = "RoomDoorOpen" }
                ]
            }
        ];
        ScopeHierarchy.AttachParents(project);
        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var node = CreatePlacementPropertyNode(project, instance);

        Assert.True(viewModel.ExecuteTreeContextAction("review-shared-property-relationships", node));
        Assert.Equal(placementGroupId, treeContext.LastSharedPropertySharedVariableId);
        var reviewedRelationship = Assert.Single(treeContext.LastSharedPropertyRelationships);
        Assert.Equal(roomProperty.Id, reviewedRelationship.ParticipantOwnerId);
        Assert.DoesNotContain(treeContext.LastSharedPropertyRelationships, relationship => relationship.ParticipantOwnerId == baseObject.ObjectId);

        Assert.NotNull(treeContext.LastRemoveSharedPropertyRelationshipAction);
        Assert.True(treeContext.LastRemoveSharedPropertyRelationshipAction!(reviewedRelationship));

        Assert.Null(Assert.Single(instance.Variables).SharedVariableId);
        Assert.Equal(baseGroupId, baseProperty.SharedVariableId);
        Assert.Equal(baseGroupId, Assert.Single(otherPlacement.Variables).SharedVariableId);
        Assert.Equal(placementGroupId, roomProperty.SharedVariableId);
        Assert.Contains(project.SharedVariables.Single(entry => entry.Id == baseGroupId).Participants, participant =>
            participant.OwnerId == baseObject.ObjectId && participant.VariableName == "DoorOpen");
        Assert.Contains(project.SharedVariables.Single(entry => entry.Id == baseGroupId).Participants, participant =>
            participant.OwnerId == otherPlacement.ObjectId && participant.VariableName == "DoorOpen");
        Assert.DoesNotContain(project.SharedVariables.Single(entry => entry.Id == placementGroupId).Participants, participant =>
            participant.OwnerId == instance.ObjectId && participant.VariableName == "DoorOpen");
    }

    [Fact]
    public void LocalCopyTreeStateAndRemoval_RevertEqualValuedSharingCopyWithoutAConfirmation()
    {
        var baseGroupId = Guid.NewGuid();
        var localGroupId = Guid.NewGuid();
        var baseProperty = new GamePropertyDefinition
        {
            Name = "DoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = baseGroupId,
            Computation = new ProjectGamePropertyComputationDto
            {
                OutputType = "Boolean",
                Expression = new ProjectGamePropertyExpressionDto { Kind = "literal", Value = true }
            }
        };
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty, LocalCopy(baseProperty, baseProperty.DefaultValue));
        instance.Variables[0].SharedVariableId = localGroupId;
        var unrelated = NumericProperty("LocalNote", "keep");
        instance.Variables.Add(unrelated);
        var room = project.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
        var roomProperty = new GamePropertyDefinition
        {
            Name = "RoomDoorOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = localGroupId
        };
        room.Variables.Add(roomProperty);
        var otherPlacement = new GameObject { Name = "Elevator B", LinkedBaseObjectId = baseObject.ObjectId };
        room.GameObjects.Add(otherPlacement);
        project.SharedVariables =
        [
            new SharedVariableDefinition
            {
                Id = baseGroupId,
                Name = "Base door state",
                Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = baseObject.ObjectId, VariableName = "DoorOpen" }]
            },
            new SharedVariableDefinition
            {
                Id = localGroupId,
                Name = "Placement door state",
                Participants = [new SharedVariableParticipant { Kind = "object", OwnerId = instance.ObjectId, VariableName = "DoorOpen" }, new SharedVariableParticipant { Kind = "global", OwnerId = roomProperty.Id, VariableName = "RoomDoorOpen" }]
            }
        ];
        ScopeHierarchy.AttachParents(project);
        var treeContext = new TreeContextInteractionServiceStub();
        var projectUi = new ProjectUiServiceStub { ConfirmResult = false };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, projectUi);
        var node = CreateNamedPlacementPropertyNode(project, instance, "DoorOpen");
        AttachPropertyNode(viewModel, node);

        node.SetSharedState(1);
        Assert.True(node.ShowLocalCopyOwnershipIcon);
        Assert.True(node.IsShared);
        Assert.True(node.IsComputed);
        Assert.Contains("Local value/sharing copy", node.OwnershipToolTip);
        Assert.Contains("Name and type remain Base-owned", node.OwnershipToolTip);
        Assert.Contains("Base Object 'Elevator'", node.OwnershipToolTip);

        var localCopyAction = Assert.Single(viewModel.GetTreeContextActions(node), action => action.ActionId == "remove-local-copy");
        Assert.Equal("Remove Local Copy — Revert to Base", localCopyAction.Header);
        Assert.DoesNotContain(viewModel.GetTreeContextActions(CreatePlacementPropertyNode(project, otherPlacement)),
            action => action.ActionId is "remove-local-copy" or "remove-game-property");

        Assert.True(viewModel.ExecuteTreeContextAction("remove-local-copy", node));
        Assert.Same(baseProperty, node.Variable);
        Assert.Equal("false", node.DisplayValue);
        Assert.True(node.ShowInheritedOwnershipIcon);
        Assert.False(node.ShowLocalCopyOwnershipIcon);
        Assert.True(node.IsShared);
        Assert.True(node.IsComputed);
        Assert.Single(instance.Variables);
        Assert.Same(unrelated, Assert.Single(instance.Variables));
        Assert.Equal(baseGroupId, baseProperty.SharedVariableId);
        Assert.Empty(otherPlacement.Variables);
        Assert.Equal(localGroupId, roomProperty.SharedVariableId);
        Assert.DoesNotContain(project.SharedVariables.Single(group => group.Id == localGroupId).Participants,
            participant => participant.OwnerId == instance.ObjectId && participant.VariableName == "DoorOpen");
        Assert.Contains(project.SharedVariables.Single(group => group.Id == localGroupId).Participants,
            participant => participant.OwnerId == roomProperty.Id && participant.VariableName == "RoomDoorOpen");
        Assert.Equal("Removed local copy; property reverted to Base Object value and sharing.", viewModel.ExportStatus);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var path = Path.Combine(tempRoot, "RevertedPlacement.sbr");
            var exporter = new JsonExportService();
            exporter.SaveProjectModel(path, project);
            var reopened = Assert.IsType<ProjectModel>(exporter.TryLoadProjectModel(path));
            var reopenedInstance = reopened.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().GameObjects.Single(item => item.Name == instance.Name);
            Assert.Single(reopenedInstance.Variables);
            Assert.Equal("LocalNote", reopenedInstance.Variables[0].Name);
            var reopenedNode = CreateNamedPlacementPropertyNode(reopened, reopenedInstance, "DoorOpen");
            Assert.True(reopenedNode.ShowInheritedOwnershipIcon);
            Assert.False(reopenedNode.ShowLocalCopyOwnershipIcon);
            Assert.Equal("false", reopenedNode.DisplayValue);
            Assert.Equal(baseGroupId, reopenedNode.Variable.SharedVariableId);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CompactPropertyIndicators_KeepSharedDotActionAndCombineOwnershipWithComputation()
    {
        var baseProperty = new GamePropertyDefinition
        {
            Name = "DoorOpen",
            DefaultValue = "false",
            Computation = new ProjectGamePropertyComputationDto
            {
                OutputType = "Boolean",
                Expression = new ProjectGamePropertyExpressionDto { Kind = "literal", Value = true }
            }
        };
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        var inheritedNode = CreatePlacementPropertyNode(project, instance);
        inheritedNode.SetSharedState(1);
        var ordinaryOwner = new GameObject { Name = "Ordinary" };
        var ordinaryProperty = NumericProperty("Local", "1");
        var ordinaryContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, ordinaryOwner, ordinaryOwner.Variables, ordinaryOwner.Name);
        var ordinaryNode = new GamePropertyNodeViewModel(ordinaryProperty, PropertyResolutionScope.Object, ordinaryContainer);

        Assert.True(ordinaryNode.ShowOrdinaryPropertyIcon);
        Assert.True(inheritedNode.ShowInheritedOwnershipIcon);
        Assert.True(inheritedNode.IsShared);
        Assert.True(inheritedNode.IsComputed);
        Assert.Equal("Computed property; its value is evaluated from its computation.", inheritedNode.ComputedIndicatorToolTip);
        Assert.Contains("Shared with 1 endpoint", inheritedNode.SharedIndicatorToolTip);

        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../StoryboardDesigner.App/Views/Controls/ProjectHierarchyPane.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        Assert.Contains("Visibility=\"{Binding IsShared, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding SharedIndicatorToolTip}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"Open shared relationship details\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonUp=\"SharedIndicator_OnMouseLeftButtonUp\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Panel.ZIndex=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"ƒx\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Changes apply to the Base Object", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void EffectiveProperties_InstanceDefinitionTakesPrecedenceCaseInsensitively()
    {
        var baseProperty = NumericProperty("MoveSpeed", "12");
        var localProperty = NumericProperty("movespeed", "20");
        var (project, _, instance) = BuildLinkedObjects(baseProperty, localProperty);

        var effective = EffectiveGamePropertyResolver.Resolve(instance, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id));

        var selected = Assert.Single(effective, property => string.Equals(property.Definition.Name, "MoveSpeed", StringComparison.OrdinalIgnoreCase));
        Assert.NotSame(localProperty, selected.Definition);
        Assert.Equal("MoveSpeed", selected.Definition.Name);
        Assert.Equal("20", selected.Definition.DefaultValue);
        Assert.Equal(GamePropertyValueRestriction.Numeric, selected.Definition.ValueRestriction);
        Assert.True(selected.IsBaseOwned);
        Assert.True(selected.IsInherited);
        Assert.True(selected.HasValueOverride);
    }

    [Fact]
    public void EffectiveProperties_ValueOverrideIsPlacementLocalAndRetainsBaseDefinition()
    {
        var baseProperty = NumericProperty("MoveSpeed", "12");
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty, LocalCopy(baseProperty, "20"));
        var localCopy = Assert.Single(instance.Variables);

        var effective = Assert.Single(EffectiveGamePropertyResolver.Resolve(instance, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id)));

        Assert.Equal(localCopy.Id, effective.Definition.Id);
        Assert.Equal("20", effective.Definition.DefaultValue);
        Assert.Equal("MoveSpeed", effective.Definition.Name);
        Assert.Equal(GamePropertyLifetime.Singleton, effective.Definition.Lifetime);
        Assert.Equal(GamePropertyValueRestriction.Numeric, effective.Definition.ValueRestriction);
        Assert.Same(baseObject, effective.DefinitionOwner);
        Assert.True(effective.IsBaseOwned);
        Assert.True(effective.IsInherited);
        Assert.True(effective.HasValueOverride);
        Assert.Equal("12", baseProperty.DefaultValue);
    }

    [Fact]
    public void ResetValueOverride_ReturnsToInheritanceWithoutChangingBaseDefinition()
    {
        var baseProperty = NumericProperty("MoveSpeed", "12");
        var localCopy = LocalCopy(baseProperty, "20");
        var (project, _, instance) = BuildLinkedObjects(baseProperty, localCopy);
        instance.Variables.Remove(localCopy);
        var effective = Assert.Single(EffectiveGamePropertyResolver.Resolve(instance, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id)));

        Assert.True(effective.IsInherited);
        Assert.Equal("12", effective.Definition.DefaultValue);
    }

    [Fact]
    public void ResetLocalSameNameOverride_RestoresBaseValueAfterRemovingLocalRecord()
    {
        var baseProperty = NumericProperty("MoveSpeed", "12");
        var legacy = NumericProperty("movespeed", "20");
        legacy.Lifetime = GamePropertyLifetime.Transient;
        var (project, _, instance) = BuildLinkedObjects(baseProperty, legacy);
        var node = CreatePlacementPropertyNode(project, instance);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project,
            new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        Assert.True(viewModel.RemoveVariableNode(node));
        var effective = Assert.Single(EffectiveGamePropertyResolver.Resolve(instance, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id)));

        Assert.Empty(instance.Variables);
        Assert.Equal("12", effective.Definition.DefaultValue);
        Assert.Equal(GamePropertyLifetime.Singleton, effective.Definition.Lifetime);
        Assert.False(effective.HasValueOverride);
    }

    [Fact]
    public void ObjectPropertyChoices_IncludePropertyDefinedOnlyOnLinkedBaseObject()
    {
        var (project, _, instance) = BuildLinkedObjects(NumericProperty("BaseSpeed", "12"));
        ScopeHierarchy.AttachParents(project);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(instance, PropertyResolutionScope.Object);

        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.BaseSpeed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParticleEffectValidation_ResolvesPropertyDefinedOnlyOnLinkedBaseObject()
    {
        var baseProperty = NumericProperty("BaseEmissionRate", "12");
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        var catalog = new ParticleEffectAuthoringCatalogService();
        var recipe = catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment)
            .First(option => catalog.GetControls(option.EffectKey).Any(control =>
                string.Equals(control.Name, "EmissionRateMultiplier", StringComparison.OrdinalIgnoreCase)));
        instance.ParticleEffect = new ParticleEffectAttachment
        {
            EffectKey = recipe.EffectKey,
            Settings = [new ParticleEffectControlSettingDto
            {
                ControlKey = "EmissionRateMultiplier",
                Binding = new ProjectGamePropertySourceReferenceDto
                {
                    OwnerId = instance.ObjectId,
                    PropertyName = "BaseEmissionRate"
                }
            }]
        };
        ScopeHierarchy.AttachParents(project);
        var request = new ValidationExecutionRequest(project);
        var context = new ValidationRuleContext(
            project, instance, project, request, ValidationProfile.Unspecified,
            IncludeDescendants: true, IsRecursivePass: true, IsFullProject: true,
            ValidationLookupService.Build(project), new ValidationSharedState());

        var descriptions = new ParticleEffectAuthoringValidationRule(catalog).Evaluate(context)
            .Select(issue => issue.Description)
            .ToList();

        Assert.DoesNotContain(descriptions, description => description.Contains("cannot resolve variable 'BaseEmissionRate'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActionReferenceValidation_AcceptsPropertyDefinedOnlyOnLinkedBaseObject()
    {
        var (project, _, instance) = BuildLinkedObjects(NumericProperty("BaseSpeed", "12"));
        instance.AvailableActions =
        [
            new CommandAction
            {
                Name = "Move",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "ECHO {BaseSpeed}",
                Payload = new EchoPayload()
            }
        ];
        ScopeHierarchy.AttachParents(project);
        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptUnknownReferenceRule());

        var issues = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.Description.Contains("Unknown reference 'BaseSpeed'", StringComparison.OrdinalIgnoreCase));
    }

    private static (ProjectModel Project, GameObject BaseObject, GameObject Instance) BuildLinkedObjects(
        GamePropertyDefinition baseProperty,
        GamePropertyDefinition? instanceProperty = null)
    {
        var baseObject = new GameObject { Name = "Elevator", Variables = [baseProperty] };
        var instance = new GameObject
        {
            Name = "Elevator in Foyer",
            LinkedBaseObjectId = baseObject.ObjectId,
            Variables = instanceProperty is null ? [] : [instanceProperty]
        };
        var room = new Room { Name = "Foyer", GameObjects = [instance] };
        var project = new ProjectModel
        {
            Name = "Inheritance",
            BaseObjects = [baseObject],
            Planets = [new Planet { Name = "Earth", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
        };
        return (project, baseObject, instance);
    }

    private static GamePropertyDefinition NumericProperty(string name, string value) => new()
    {
        Name = name,
        DefaultValue = value,
        ValueRestriction = GamePropertyValueRestriction.Numeric,
        Lifetime = GamePropertyLifetime.Singleton
    };

    private static GamePropertyDefinition SharedDoorProperty(Guid sharedId, string name = "DoorOpen") => new()
    {
        Name = name,
        DefaultValue = "false",
        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
        Lifetime = GamePropertyLifetime.Singleton,
        SharedVariableId = sharedId
    };

    private static SharedVariableDefinition SharedGroup(Guid id, string name, (string Kind, Guid OwnerId, string VariableName) participant) => new()
    {
        Id = id,
        Name = name,
        DefaultValue = "false",
        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
        Participants = [new SharedVariableParticipant { Kind = participant.Kind, OwnerId = participant.OwnerId, VariableName = participant.VariableName }]
    };

    private static (string Kind, Guid OwnerId, string VariableName, string? Leg) CloneParticipant(SharedVariableParticipant participant) =>
        (participant.Kind, participant.OwnerId, participant.VariableName, participant.Leg);

    private static GamePropertyDefinition LocalCopy(GamePropertyDefinition source, string value) => new()
    {
        Name = source.Name,
        DefaultValue = value,
        ValueRestriction = source.ValueRestriction,
        Lifetime = source.Lifetime,
        SharedVariableId = source.SharedVariableId,
        Computation = source.Computation
    };

    private static GamePropertyNodeViewModel CreatePlacementPropertyNode(ProjectModel project, GameObject instance)
    {
        ScopeHierarchy.AttachParents(project);
        var parent = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [parent, instance, instance.Variables, PropertyResolutionScope.Object, null, null]));
        return Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>());
    }

    private static GamePropertyNodeViewModel CreateNamedPlacementPropertyNode(ProjectModel project, GameObject instance, string propertyName)
    {
        ScopeHierarchy.AttachParents(project);
        var parent = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [parent, instance, instance.Variables, PropertyResolutionScope.Object, null, null]));
        return Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>(), node =>
            string.Equals(node.Variable.Name, propertyName, StringComparison.OrdinalIgnoreCase));
    }

    private static void AttachPropertyNode(MainWindowViewModel viewModel, GamePropertyNodeViewModel propertyNode)
    {
        var root = Assert.IsType<ProjectRootNodeViewModel>(propertyNode.VariablesContainer.Parent);
        root.Children.Add(propertyNode.VariablesContainer);
        viewModel.HierarchyRoots.Add(root);
    }
}
