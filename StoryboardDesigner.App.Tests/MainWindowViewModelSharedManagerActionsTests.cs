using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelSharedManagerActionsTests
{
    [Fact]
    public void SharedManager_RenameAction_UpdatesDisplayName()
    {
        var sharedId = Guid.NewGuid();
        var variable = new GamePropertyDefinition
        {
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = sharedId
        };

        var gameObject = new GameObject
        {
            Name = "Door",
            Variables = [variable]
        };

        var project = new ProjectModel
        {
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = sharedId,
                    Name = "DoorState",
                    Participants =
                    [
                        new SharedVariableParticipant
                        {
                            Kind = "object",
                            OwnerId = gameObject.ObjectId,
                            VariableName = "isOpen"
                        }
                    ]
                }
            ]
        };

        var variableNode = BuildVariableNode(project, gameObject, variable);
        var ownerArea = project.Planets.Single().Countries.Single().Areas.Single();
        var ownerRoom = ownerArea.Rooms.Single();
        ownerRoom.GameObjects.Remove(gameObject);
        ownerArea.GameObjects.Add(gameObject);
        var treeContext = new TreeContextInteractionServiceStub
        {
            AutoInvokeOpenSharedVariablesManagerAction = true
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        Assert.True(viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode));
        Assert.NotNull(treeContext.LastRenameSharedVariableManagerAction);
        var managerItem = Assert.Single(treeContext.LastSharedVariableManagerItems);
        var participant = Assert.Single(managerItem.Participants);
        Assert.Equal(1, managerItem.ParticipantCount);
        Assert.Equal("Single Participant", managerItem.Status);
        Assert.Equal(gameObject.ObjectId, participant.OwnerId);
        Assert.Equal("isOpen", participant.VariableName);
        Assert.Equal("Planet/Country/Area/Game Objects/Door.isOpen", participant.Endpoint);
        Assert.Equal("Planet/Country/Area/Game Objects/Door", participant.OwnerDisplayName);

        var renamed = treeContext.LastRenameSharedVariableManagerAction!.Invoke(sharedId, "DoorStateRenamed");

        Assert.True(renamed);
        Assert.Equal("DoorStateRenamed", Assert.Single(project.SharedVariables).Name);

        var refreshed = treeContext.LastReloadSharedVariableManagerItemsAction!.Invoke();
        Assert.Equal("DoorStateRenamed", Assert.Single(refreshed).DisplayName);
    }

    [Fact]
    public void SharedManager_DropEmptyAction_RemovesSharedMetadata_WhenConfirmed()
    {
        var sharedId = Guid.NewGuid();
        var variable = new GamePropertyDefinition
        {
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            SharedVariableId = sharedId
        };

        var gameObject = new GameObject
        {
            Name = "Door",
            Variables = [variable]
        };

        var project = new ProjectModel
        {
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = sharedId,
                    Name = "DoorState",
                    Participants = []
                }
            ]
        };

        var variableNode = BuildVariableNode(project, gameObject, variable);
        var treeContext = new TreeContextInteractionServiceStub
        {
            AutoInvokeOpenSharedVariablesManagerAction = true
        };
        var projectUi = new ProjectUiServiceStub
        {
            ConfirmResult = true
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, projectUi);

        Assert.True(viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode));
        Assert.NotNull(treeContext.LastDropEmptySharedVariableManagerAction);

        var dropped = treeContext.LastDropEmptySharedVariableManagerAction!.Invoke(sharedId);

        Assert.True(dropped);
        Assert.Empty(project.SharedVariables);
        Assert.Equal("Shared Variables Manager", projectUi.LastConfirmTitle);
    }

    [Fact]
    public void SharedManager_ResolvesNamesForObjectScopedAndTraversalOwners()
    {
        var sharedId = Guid.NewGuid();
        var variable = new GamePropertyDefinition
        {
            Name = "isOpen",
            DefaultValue = "false",
            SharedVariableId = sharedId
        };
        var gameObject = new GameObject { Name = "Door", Variables = [variable] };
        var project = new ProjectModel
        {
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = sharedId,
                    Name = "Open State",
                    Participants =
                    [
                        new SharedVariableParticipant { Kind = "object", OwnerId = gameObject.ObjectId, VariableName = "isOpen" }
                    ]
                }
            ]
        };
        var variableNode = BuildVariableNode(project, gameObject, variable);
        var planet = project.Planets.Single();
        var country = planet.Countries.Single();
        var area = country.Areas.Single();
        var scopedProperty = new GamePropertyDefinition { Name = "weather", SharedVariableId = sharedId };
        planet.Variables.Add(scopedProperty);
        var roomA = area.Rooms.Single();
        roomA.Name = "Atrium";
        var roomB = new Room { Name = "Gallery" };
        area.Rooms.Add(roomB);
        var connection = new TraversalConnection { RoomAId = roomA.Id, RoomBId = roomB.Id };
        area.TraversalConnections.Add(connection);
        var templateElevator = new GameObject { Name = "Elevator" };
        project.RoomTemplates.Add(new Room { Name = "ElevatorFoyer", GameObjects = [templateElevator] });
        project.SharedVariables.Single().Participants.AddRange([
            new SharedVariableParticipant { Kind = "global", OwnerId = scopedProperty.Id, VariableName = "weather" },
            new SharedVariableParticipant { Kind = "traversal", OwnerId = connection.TraversalConnectionId, VariableName = "isPassable" },
            new SharedVariableParticipant { Kind = "traversal-leg", OwnerId = connection.TraversalConnectionId, VariableName = "isPassable", Leg = "a2b" },
            new SharedVariableParticipant { Kind = "object", OwnerId = templateElevator.ObjectId, VariableName = "isMoving" }
        ]);

        var treeContext = new TreeContextInteractionServiceStub { AutoInvokeOpenSharedVariablesManagerAction = true };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        Assert.True(viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode));

        var item = Assert.Single(treeContext.LastSharedVariableManagerItems);
        Assert.Equal("Planet/Country/Area/Atrium/Door", Assert.Single(item.Participants, participant => participant.OwnerId == gameObject.ObjectId).OwnerDisplayName);
        Assert.Equal("Planet.Planet", Assert.Single(item.Participants, participant => participant.Kind == "global").OwnerDisplayName);
        Assert.Equal("Planet.Country.Area.Traversal Atrium ↔ Gallery", Assert.Single(item.Participants, participant => participant.Kind == "traversal").OwnerDisplayName);
        Assert.Equal("Planet.Country.Area.Traversal Atrium ↔ Gallery", Assert.Single(item.Participants, participant => participant.Kind == "traversal-leg").OwnerDisplayName);
        Assert.Equal("Global/Room Templates/ElevatorFoyer/Elevator", Assert.Single(item.Participants, participant => participant.VariableName == "isMoving").OwnerDisplayName);
    }

    private static GamePropertyNodeViewModel BuildVariableNode(ProjectModel project, GameObject gameObject, GamePropertyDefinition variable)
    {
        var rootNode = new ProjectRootNodeViewModel(project);
        var area = new Area { Name = "Area" };
        var room = new Room { Name = "Room" };
        room.GameObjects.Add(gameObject);
        area.Rooms.Add(room);
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        project.Planets = [planet];

        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(gameObject, roomObjectsNode);
        var variableContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, gameObject, gameObject.Variables, gameObject.Name, objectNode);
        objectNode.Children.Add(variableContainer);
        var variableNode = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer);
        variableContainer.Children.Add(variableNode);
        return variableNode;
    }
}
