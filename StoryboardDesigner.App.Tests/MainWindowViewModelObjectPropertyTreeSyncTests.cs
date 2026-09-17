using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelObjectPropertyTreeSyncTests
{
    [Fact]
    public void SelectedObject_WhenContainerEnabled_RefreshesObjectVariableNodesInTree()
    {
        var project = new ProjectModel();
        var objectModel = new GameObject
        {
            Name = "Backpack",
            IsContainer = false
        };

        var room = new Room
        {
            Name = "Cabin"
        };

        var area = new Area
        {
            Name = "Forest",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "West",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Earth",
            Countries = [country]
        };

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(objectModel, roomObjectsNode);
        var variableContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, objectNode);
        objectNode.Children.Add(variableContainer);

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        viewModel.SelectedNode = objectNode;

        objectModel.IsContainer = true;

        var variableNames = variableContainer.Children
            .OfType<GamePropertyNodeViewModel>()
            .Select(node => node.Variable.Name)
            .ToList();

        Assert.Contains("containerPoints", variableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("containerPointsRemaining", variableNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReviewVariables_WhenDialogAddsVariable_UpdatesContainerTreeImmediately()
    {
        var variable = new GamePropertyDefinition
        {
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Lifetime = GamePropertyLifetime.Singleton
        };

        var objectModel = new GameObject
        {
            Name = "Crate",
            Variables = [variable]
        };

        var room = new Room
        {
            Name = "Storage"
        };

        var area = new Area
        {
            Name = "Warehouse",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "North",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Earth",
            Countries = [country]
        };

        var project = new ProjectModel();
        var treeContext = new TreeContextInteractionServiceStub
        {
            ShowVariableScopeReviewHandler = (_, variables) =>
            {
                variables.Add(new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    Lifetime = GamePropertyLifetime.Singleton
                });
            }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            treeContext,
            new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var roomObjectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(objectModel, roomObjectsNode);
        var variableContainer = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Object, objectNode);
        variableContainer.Children.Add(new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variableContainer));
        objectNode.Children.Add(variableContainer);

        viewModel.SelectedNode = variableContainer;

        var handled = viewModel.ExecuteTreeContextAction("review-game-properties", variableContainer);

        Assert.True(handled);

        var variableNames = variableContainer.Children
            .OfType<GamePropertyNodeViewModel>()
            .Select(node => node.Variable.Name)
            .ToList();

        Assert.Contains("isOpen", variableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("isPlayer", variableNames, StringComparer.OrdinalIgnoreCase);
    }
}
