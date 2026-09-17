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
        var treeContext = new TreeContextInteractionServiceStub
        {
            AutoInvokeOpenSharedVariablesManagerAction = true
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        Assert.True(viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode));
        Assert.NotNull(treeContext.LastRenameSharedVariableManagerAction);

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

    private static GamePropertyNodeViewModel BuildVariableNode(ProjectModel project, GameObject gameObject, GamePropertyDefinition variable)
    {
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
        return variableNode;
    }
}
