using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelLeaveShareTests
{
    [Fact]
    public void ExecuteTreeContextAction_LeaveSharedVariable_ClearsLink_WhenConfirmed()
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

        var variableContainer = new GamePropertiesContainerNodeViewModel(
            PropertyResolutionScope.Object,
            new GameObjectNodeViewModel(gameObject, new RoomGameObjectsNodeViewModel(new RoomNodeViewModel(new Room(), new Area(), new AreaNodeViewModel(new Area(), new CountryNodeViewModel(new Country(), new PlanetNodeViewModel(project, new Planet(), new ProjectRootNodeViewModel(project))))))));
        var variableNode = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer);
        variableContainer.Children.Add(variableNode);

        var projectUi = new ProjectUiServiceStub
        {
            ConfirmResult = true
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        var handled = viewModel.ExecuteTreeContextAction("leave-shared-variable", variableNode);

        Assert.True(handled);
        Assert.Null(variable.SharedVariableId);
        Assert.Equal("Leave Share", projectUi.LastConfirmTitle);
        Assert.Contains("DoorState", projectUi.LastConfirmMessage, StringComparison.OrdinalIgnoreCase);

        var shared = Assert.Single(project.SharedVariables);
        Assert.Equal(sharedId, shared.Id);
        Assert.Empty(shared.Participants);
    }

    [Fact]
    public void ExecuteTreeContextAction_LeaveSharedVariable_DoesNotChangeLink_WhenCanceled()
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

        var variableContainer = new GamePropertiesContainerNodeViewModel(
            PropertyResolutionScope.Object,
            new GameObjectNodeViewModel(gameObject, new RoomGameObjectsNodeViewModel(new RoomNodeViewModel(new Room(), new Area(), new AreaNodeViewModel(new Area(), new CountryNodeViewModel(new Country(), new PlanetNodeViewModel(project, new Planet(), new ProjectRootNodeViewModel(project))))))));
        var variableNode = new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer);
        variableContainer.Children.Add(variableNode);

        var projectUi = new ProjectUiServiceStub
        {
            ConfirmResult = false
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        var handled = viewModel.ExecuteTreeContextAction("leave-shared-variable", variableNode);

        Assert.False(handled);
        Assert.Equal(sharedId, variable.SharedVariableId);
        var shared = Assert.Single(project.SharedVariables);
        Assert.Single(shared.Participants);
    }
}
