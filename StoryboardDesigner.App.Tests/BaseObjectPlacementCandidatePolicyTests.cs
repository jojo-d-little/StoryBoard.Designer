using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class BaseObjectPlacementCandidatePolicyTests
{
    [Fact]
    public void AddInstanceCandidates_UseBaseObjectsOnly()
    {
        var project = BuildProjectWithScopedBaseObjects(
            out var room,
            out var globalBase,
            out var planetBase,
            out var countryBase,
            out var areaBase,
            out var templateObject,
            out var playerObject,
            out var roomObject);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var roomNode = BuildRoomNode(project, room);
        var handled = viewModel.ExecuteTreeContextAction("add-existing-quantifiable-object", roomNode);

        Assert.False(handled);
        var candidateObjects = treeContext.LastQuantifiablePlacementCandidates
            .Select(candidate => candidate.SourceObject)
            .ToList();

        Assert.Contains(globalBase, candidateObjects);
        Assert.Contains(planetBase, candidateObjects);
        Assert.Contains(countryBase, candidateObjects);
        Assert.Contains(areaBase, candidateObjects);

        Assert.DoesNotContain(templateObject, candidateObjects);
        Assert.DoesNotContain(playerObject, candidateObjects);
        Assert.DoesNotContain(roomObject, candidateObjects);
    }

    [Fact]
    public void AddInstanceCandidates_RespectScopePrecedence_AreaCountryPlanetGlobal()
    {
        var project = BuildProjectWithScopedBaseObjects(
            out var room,
            out var globalBase,
            out var planetBase,
            out var countryBase,
            out var areaBase,
            out _,
            out _,
            out _);

        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        var roomNode = BuildRoomNode(project, room);
        _ = viewModel.ExecuteTreeContextAction("add-existing-quantifiable-object", roomNode);

        var candidates = treeContext.LastQuantifiablePlacementCandidates;
        Assert.NotEmpty(candidates);

        Assert.Equal(areaBase.ObjectId, candidates[0].SourceObject.ObjectId);
        Assert.Equal(0, candidates[0].ScopePriority);
        Assert.Equal("Area: Area A", candidates[0].SourceScopePath);

        Assert.Equal(countryBase.ObjectId, candidates[1].SourceObject.ObjectId);
        Assert.Equal(1, candidates[1].ScopePriority);
        Assert.Equal("Country: Country A", candidates[1].SourceScopePath);

        Assert.Equal(planetBase.ObjectId, candidates[2].SourceObject.ObjectId);
        Assert.Equal(2, candidates[2].ScopePriority);
        Assert.Equal("Planet: Planet A", candidates[2].SourceScopePath);

        Assert.Equal(globalBase.ObjectId, candidates[3].SourceObject.ObjectId);
        Assert.Equal(3, candidates[3].ScopePriority);
        Assert.Equal("Global", candidates[3].SourceScopePath);
    }

    private static ProjectModel BuildProjectWithScopedBaseObjects(
        out Room room,
        out GameObject globalBase,
        out GameObject planetBase,
        out GameObject countryBase,
        out GameObject areaBase,
        out GameObject templateObject,
        out GameObject playerObject,
        out GameObject roomObject)
    {
        globalBase = CreateQuantifiableBaseObject("Global Base Coin");
        planetBase = CreateQuantifiableBaseObject("Planet Base Coin");
        countryBase = CreateQuantifiableBaseObject("Country Base Coin");
        areaBase = CreateQuantifiableBaseObject("Area Base Coin");

        templateObject = CreateQuantifiableBaseObject("Template Coin");
        playerObject = CreateQuantifiableBaseObject("Player Coin");
        roomObject = CreateQuantifiableBaseObject("Room Coin");

        room = new Room
        {
            Name = "Room A",
            GameObjects = [roomObject]
        };

        var area = new Area
        {
            Name = "Area A",
            BaseObjects = [areaBase],
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            BaseObjects = [countryBase],
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            BaseObjects = [planetBase],
            Countries = [country]
        };

        return new ProjectModel
        {
            Name = "PlacementPolicyFixture",
            ObjectTemplates = [templateObject],
            BaseObjects = [globalBase],
            GlobalObjectScopeName = "Global Objects",
            GameObjects = [playerObject],
            Planets = [planet]
        };
    }

    private static GameObject CreateQuantifiableBaseObject(string name)
    {
        return new GameObject
        {
            Name = name,
            IsQuantifiable = true,
            QuantifiablePlacementDistributionMode = "GroupedStack",
            Quantity = 1
        };
    }

    private static RoomNodeViewModel BuildRoomNode(ProjectModel project, Room room)
    {
        var planet = Assert.Single(project.Planets);
        var country = Assert.Single(planet.Countries);
        var area = Assert.Single(country.Areas);

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        return new RoomNodeViewModel(room, area, areaNode);
    }
}
