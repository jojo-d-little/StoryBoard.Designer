using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelObjectStackScaleDefaultPrefillTests
{
    [Fact]
    public void EditObjectBasicProperties_WhenObjectOverridesAreBlank_PrefillsFromProjectDefaultsAndSavesObjectValues()
    {
        var target = new GameObject
        {
            Name = "Crate",
            StackScaleStepOverride = null,
            MinStackScaleOverride = null
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [target]
        };

        var area = new Area
        {
            Name = "Area 1",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country 1",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Planet 1",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "Project",
            Planets = [planet],
            StartingPlanetName = planet.Name,
            StackScaleStepDefault = 0.11,
            MinStackScaleDefault = 0.66
        };

        ScopeHierarchy.AttachParents(project);

        var treeContext = new TreeContextInteractionServiceStub
        {
            // Accept and return values exactly as presented by the edit dialog request.
            EditObjectBasicPropertiesHandler = initial => initial
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(target, objectsNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.True(edited);
        var request = Assert.IsType<ObjectBasicPropertiesEditRequest>(treeContext.LastObjectBasicPropertiesRequest);
        Assert.Equal(project.StackScaleStepDefault, request.StackScaleStepOverride);
        Assert.Equal(project.MinStackScaleDefault, request.MinStackScaleOverride);
        Assert.Equal(project.StackScaleStepDefault, target.StackScaleStepOverride);
        Assert.Equal(project.MinStackScaleDefault, target.MinStackScaleOverride);
    }
}
