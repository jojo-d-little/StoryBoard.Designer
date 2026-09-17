using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelAreaMapTraversalSyncTests
{
    [Fact]
    public void TryLinkTraversalDoors_TwoDoors_WiresSharedGroupAndDoesNotWarnAtFourParticipants()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var doorA = new GameObject { Name = "Door A", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        var doorB = new GameObject { Name = "Door B", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        roomA.GameObjects.Add(doorA);
        roomB.GameObjects.Add(doorB);
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var connection = new TraversalConnection
        {
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East
        };
        area.TraversalConnections.Add(connection);

        var project = new ProjectModel
        {
            Name = "Traversal Door Link",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);

        var linked = vm.TryLinkTraversalDoors(connection, doorA.ObjectId, doorB.ObjectId, out var warningMessage);

        Assert.True(linked);
        Assert.True(string.IsNullOrWhiteSpace(warningMessage));
        Assert.Equal(doorA.ObjectId, connection.TraversalStateFromA.OpenableObjectId);
        Assert.Equal(doorB.ObjectId, connection.TraversalStateFromB.OpenableObjectId);

        var sharedId = connection.TraversalStateFromA.Variables
            .First(variable => string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase))
            .SharedVariableId;
        Assert.NotNull(sharedId);

        var shared = project.SharedVariables.Single(variable => variable.Id == sharedId.Value);
        Assert.Equal(4, shared.Participants.Count);
    }

    [Fact]
    public void TryCreateSelectedAreaTraversal_AddsTraversalAndProjectsLegNodesToRoomTree()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var project = new ProjectModel
        {
            Name = "Traversal Create",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);

        Assert.Equal(0, GetTraversalLegCount(vm, roomA));
        Assert.Equal(0, GetTraversalLegCount(vm, roomB));

        var created = vm.TryCreateSelectedAreaTraversal(
            roomA.Id,
            roomB.Id,
            Direction10.East,
            TraversalAccessMode.TwoWay,
            OpenStateBindingMode.Independent,
            OpenablePolicy.IgnoreOpenableState,
            OpenablePolicy.IgnoreOpenableState,
            "  movement.slide  ",
            out var createdConnection);

        Assert.True(created);
        Assert.NotNull(createdConnection);
        Assert.Single(area.TraversalConnections);
        Assert.Equal("movement.slide", createdConnection!.PresentationEffectKey);
        Assert.Equal(1, GetTraversalLegCount(vm, roomA));
        Assert.Equal(1, GetTraversalLegCount(vm, roomB));
    }

    [Fact]
    public void TryRelinkTraversalDoors_RebuildsDoorAndPassableSharedBinding()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var doorA = new GameObject { Name = "Door A", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        var doorB = new GameObject { Name = "Door B", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        roomA.GameObjects.Add(doorA);
        roomB.GameObjects.Add(doorB);
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var connection = new TraversalConnection
        {
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East
        };
        area.TraversalConnections.Add(connection);

        var project = new ProjectModel
        {
            Name = "Traversal Relink",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);

        Assert.True(vm.TryLinkTraversalDoors(connection, doorA.ObjectId, doorB.ObjectId, out _));

        var passableA = connection.TraversalStateFromA.Variables.Single(variable => string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        var passableB = connection.TraversalStateFromB.Variables.Single(variable => string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        passableA.SharedVariableId = Guid.NewGuid();
        passableB.SharedVariableId = Guid.NewGuid();

        var relinked = vm.TryRelinkTraversalDoors(connection, out _);

        Assert.True(relinked);

        var doorOpenA = doorA.Variables.Single(variable => string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        var doorOpenB = doorB.Variables.Single(variable => string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        var relinkedPassableA = connection.TraversalStateFromA.Variables.Single(variable => string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        var relinkedPassableB = connection.TraversalStateFromB.Variables.Single(variable => string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(doorOpenA.SharedVariableId, relinkedPassableA.SharedVariableId);
        Assert.Equal(doorOpenB.SharedVariableId, relinkedPassableB.SharedVariableId);
        Assert.Equal(relinkedPassableA.SharedVariableId, relinkedPassableB.SharedVariableId);
        Assert.Equal(relinkedPassableA.SharedVariableId, connection.TraversalStateFromA.SharedVariableId);
        Assert.Equal(relinkedPassableB.SharedVariableId, connection.TraversalStateFromB.SharedVariableId);
    }

    [Fact]
    public void TryUpdateSelectedAreaTraversal_UpdatesPresentationEffectKey()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var connection = new TraversalConnection
        {
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East,
            PresentationEffectKey = "old.effect"
        };
        area.TraversalConnections.Add(connection);

        var project = new ProjectModel
        {
            Name = "Traversal Update",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);

        var updated = vm.TryUpdateSelectedAreaTraversal(
            connection,
            roomA.Id,
            roomB.Id,
            Direction10.East,
            TraversalAccessMode.TwoWay,
            OpenStateBindingMode.Independent,
            OpenablePolicy.IgnoreOpenableState,
            OpenablePolicy.IgnoreOpenableState,
            "  movement.crossfade  ");

        Assert.True(updated);
        Assert.Equal("movement.crossfade", connection.PresentationEffectKey);
    }

    [Fact]
    public void RemoveNavigationLink_FromAreaMap_RemovesTraversalLegNodesFromRoomTree()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var connection = new TraversalConnection
        {
            TraversalConnectionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East
        };
        area.TraversalConnections.Add(connection);

        var project = new ProjectModel
        {
            Name = "Traversal Sync",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);

        Assert.Equal(1, GetTraversalLegCount(vm, roomA));
        Assert.Equal(1, GetTraversalLegCount(vm, roomB));

        vm.RemoveNavigationLink(connection);

        Assert.Empty(area.TraversalConnections);
        Assert.Equal(0, GetTraversalLegCount(vm, roomA));
        Assert.Equal(0, GetTraversalLegCount(vm, roomB));
    }

    [Fact]
    public void RemoveNavigationLink_ThenRecreateTraversalDoorLink_DoesNotLeaveStaleSharedParticipants()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var doorA = new GameObject { Name = "Door A", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        var doorB = new GameObject { Name = "Door B", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        roomA.GameObjects.Add(doorA);
        roomB.GameObjects.Add(doorB);
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var connection = new TraversalConnection
        {
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East
        };
        area.TraversalConnections.Add(connection);

        var project = new ProjectModel
        {
            Name = "Traversal Recreate Cleanup",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);

        Assert.True(vm.TryLinkTraversalDoors(connection, doorA.ObjectId, doorB.ObjectId, out _));
        var originalSharedId = connection.TraversalStateFromA.Variables
            .Single(variable => string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase))
            .SharedVariableId;
        Assert.NotNull(originalSharedId);

        vm.RemoveNavigationLink(connection);

        var recreated = vm.TryCreateSelectedAreaTraversal(
            roomA.Id,
            roomB.Id,
            Direction10.East,
            TraversalAccessMode.TwoWay,
            OpenStateBindingMode.Independent,
            OpenablePolicy.IgnoreOpenableState,
            OpenablePolicy.IgnoreOpenableState,
            null,
            out var recreatedConnection);

        Assert.True(recreated);
        Assert.NotNull(recreatedConnection);
        Assert.True(vm.TryLinkTraversalDoors(recreatedConnection!, doorA.ObjectId, doorB.ObjectId, out _));

        var doorAVariable = doorA.Variables.Single(variable => string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        var doorBVariable = doorB.Variables.Single(variable => string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(doorAVariable.SharedVariableId);
        Assert.Equal(doorAVariable.SharedVariableId, doorBVariable.SharedVariableId);

        var doorAParticipantMembershipCount = project.SharedVariables.Count(shared =>
            shared.Participants.Any(participant =>
                string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
                && participant.OwnerId == doorA.ObjectId
                && string.Equals(participant.VariableName, "isOpen", StringComparison.OrdinalIgnoreCase)));
        var doorBParticipantMembershipCount = project.SharedVariables.Count(shared =>
            shared.Participants.Any(participant =>
                string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
                && participant.OwnerId == doorB.ObjectId
                && string.Equals(participant.VariableName, "isOpen", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(1, doorAParticipantMembershipCount);
        Assert.Equal(1, doorBParticipantMembershipCount);

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableSingleMembershipRule());
        registry.Register(new TraversalDoorLinkIntegrityRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.RuleId, "PROJ-003", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.RuleId, "TRV-004", StringComparison.Ordinal));
    }

    [Fact]
    public void OpenSharedRelationshipManager_TraversalLegIsPassable_ShowsSharedCounterparts()
    {
        var area = new Area { Name = "Area" };
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        var doorA = new GameObject { Name = "Door A", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        var doorB = new GameObject { Name = "Door B", IsOpenable = true, IsInventoriable = false, IsOpenDefaultValue = true };
        roomA.GameObjects.Add(doorA);
        roomB.GameObjects.Add(doorB);
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        var connection = new TraversalConnection
        {
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East
        };
        area.TraversalConnections.Add(connection);

        var project = new ProjectModel
        {
            Name = "Traversal Leg Shared Review",
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
                            Areas = [area]
                        }
                    ]
                }
            ]
        };

        var treeContext = new TreeContextInteractionServiceStub();
        var vm = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        InvokeLoadProjectIntoHierarchy(vm);
        vm.OpenAreaEditor(area);
        Assert.True(vm.TryLinkTraversalDoors(connection, doorA.ObjectId, doorB.ObjectId, out _));

        var passableNode = EnumerateNodes(vm.HierarchyRoots)
            .OfType<TraversalLegNodeViewModel>()
            .Where(node => ReferenceEquals(node.Connection, connection) && node.IsFromRoomA)
            .SelectMany(node => node.Children.OfType<GamePropertiesContainerNodeViewModel>())
            .SelectMany(node => node.Children.OfType<GamePropertyNodeViewModel>())
            .Single(node => string.Equals(node.Variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));

        vm.OpenSharedRelationshipManager(passableNode);

        Assert.False(string.IsNullOrWhiteSpace(treeContext.LastSharedPropertyPath));
        Assert.NotEmpty(treeContext.LastSharedPropertyRelationships);
        Assert.Contains(
            treeContext.LastSharedPropertyRelationships,
            relationship => string.Equals(relationship.ParticipantKind, "object", StringComparison.OrdinalIgnoreCase)
                && string.Equals(relationship.ParticipantVariableName, "isOpen", StringComparison.OrdinalIgnoreCase));
    }

    private static void InvokeLoadProjectIntoHierarchy(MainWindowViewModel vm)
    {
        var method = typeof(MainWindowViewModel).GetMethod("LoadProjectIntoHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, null);
    }

    private static int GetTraversalLegCount(MainWindowViewModel vm, Room room)
    {
        var roomNode = EnumerateNodes(vm.HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == room.Id);

        var traversalFolder = roomNode.Children
            .OfType<RoomTraversalLegsNodeViewModel>()
            .Single();

        return traversalFolder.Children.OfType<TraversalLegNodeViewModel>().Count();
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        var stack = new Stack<HierarchyNodeViewModel>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }
}
