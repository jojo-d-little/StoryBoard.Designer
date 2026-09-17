using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelSharedCalloutTests
{
    [Fact]
    public void ReviewSharedRelationships_SetsSingletonCallout_WhenOnlyOneParticipantExists()
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

        var rootNode = new ProjectRootNodeViewModel(project);
        var area = new Area { Name = "Area" };
        var room = new Room { Name = "Room" };
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
        var variableContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, objectNode);
        objectNode.Children.Add(variableContainer);
        var variableNode = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer);
        variableContainer.Children.Add(variableNode);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode);

        Assert.True(handled);
        Assert.Contains("Single Participant Share", treeContext.LastSharedPropertyCallout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReviewSharedRelationships_SetsEmptyShellCallout_WhenSharedMetadataHasNoParticipants()
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

        var rootNode = new ProjectRootNodeViewModel(project);
        var area = new Area { Name = "Area" };
        var room = new Room { Name = "Room" };
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
        var variableContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, objectNode);
        objectNode.Children.Add(variableContainer);
        var variableNode = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer);
        variableContainer.Children.Add(variableNode);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode);

        Assert.True(handled);
        Assert.Contains("empty", treeContext.LastSharedPropertyCallout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReviewSharedRelationships_DeepLinkOpensSharedManager_WithSelectedSharedId()
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

        var rootNode = new ProjectRootNodeViewModel(project);
        var area = new Area { Name = "Area" };
        var room = new Room { Name = "Room" };
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
        var variableContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, objectNode);
        objectNode.Children.Add(variableContainer);
        var variableNode = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer);
        variableContainer.Children.Add(variableNode);

        var treeContext = new TreeContextInteractionServiceStub
        {
            AutoInvokeOpenSharedVariablesManagerAction = true
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var handled = viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode);

        Assert.True(handled);
        Assert.Equal(sharedId, treeContext.LastSharedVariableManagerSelectedId);
        Assert.Single(treeContext.LastSharedVariableManagerItems);
        Assert.Equal("DoorState", treeContext.LastSharedVariableManagerItems[0].DisplayName);
    }
}
