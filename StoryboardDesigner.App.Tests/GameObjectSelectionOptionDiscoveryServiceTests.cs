using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class GameObjectSelectionOptionDiscoveryServiceTests
{
    [Fact]
    public void Discover_ProjectRealObjects_ExcludesTemplateAndBaseCatalogBranches()
    {
        var service = new GameObjectSelectionOptionDiscoveryService();

        var roomObject = new GameObject { Name = "Room Rock" };
        var room = new Room { Name = "Room", GameObjects = [roomObject] };
        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };

        var globalObject = new GameObject { Name = "Global Torch" };
        var objectTemplate = new GameObject { Name = "Template Cup" };
        var projectBaseObject = new GameObject { Name = "Project Base Knife" };
        var areaBaseObject = new GameObject { Name = "Area Base Map" };

        area.BaseObjects.Add(areaBaseObject);

        var project = new ProjectModel
        {
            Name = "Discovery",
            GameObjects = [globalObject],
            ObjectTemplates = [objectTemplate],
            BaseObjects = [projectBaseObject],
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var results = service.Discover(new GameObjectSelectionOptionDiscoveryRequest(
            CurrentObject: roomObject,
            ScopeSearchDepth: ScopeSearchDepth.Project,
            ScopeSearchType: GameObjectOptionSourceTarget.RealObjects,
            ExcludeCurrentObject: false));

        Assert.Contains(results, option => option.Name == roomObject.Name);
        Assert.Contains(results, option => option.Name == globalObject.Name);

        Assert.DoesNotContain(results, option => option.Name == objectTemplate.Name);
        Assert.DoesNotContain(results, option => option.Name == projectBaseObject.Name);
        Assert.DoesNotContain(results, option => option.Name == areaBaseObject.Name);
    }

    [Fact]
    public void Discover_ProjectTemplateObjects_IncludesTemplateAndBaseCatalogBranches()
    {
        var service = new GameObjectSelectionOptionDiscoveryService();

        var roomObject = new GameObject { Name = "Room Lamp" };
        var room = new Room { Name = "Room", GameObjects = [roomObject] };
        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };

        var objectTemplate = new GameObject { Name = "Template Cup" };
        var projectBaseObject = new GameObject { Name = "Project Base Knife" };
        var areaBaseObject = new GameObject { Name = "Area Base Map" };
        area.BaseObjects.Add(areaBaseObject);

        var templateRoomObject = new GameObject { Name = "Template Room Object" };
        var templateRoom = new Room { Name = "Template Room", GameObjects = [templateRoomObject] };

        var project = new ProjectModel
        {
            Name = "Discovery",
            ObjectTemplates = [objectTemplate],
            BaseObjects = [projectBaseObject],
            RoomTemplates = [templateRoom],
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var results = service.Discover(new GameObjectSelectionOptionDiscoveryRequest(
            CurrentObject: objectTemplate,
            ScopeSearchDepth: ScopeSearchDepth.Project,
            ScopeSearchType: GameObjectOptionSourceTarget.ObjectTemplates,
            ExcludeCurrentObject: false));

        Assert.Contains(results, option => option.Name == objectTemplate.Name);
        Assert.Contains(results, option => option.Name == projectBaseObject.Name);
        Assert.Contains(results, option => option.Name == templateRoomObject.Name);

        Assert.DoesNotContain(results, option => option.Name == roomObject.Name);
    }

    [Fact]
    public void Discover_LocalTemplateScope_FromProjectBaseObject_UsesProjectBaseCollection()
    {
        var service = new GameObjectSelectionOptionDiscoveryService();

        var projectBaseA = new GameObject { Name = "Project Base A" };
        var projectBaseB = new GameObject { Name = "Project Base B" };

        var project = new ProjectModel
        {
            Name = "Discovery",
            BaseObjects = [projectBaseA, projectBaseB]
        };

        ScopeHierarchy.AttachParents(project);

        var results = service.Discover(new GameObjectSelectionOptionDiscoveryRequest(
            CurrentObject: projectBaseA,
            ScopeSearchDepth: ScopeSearchDepth.LocalRoom,
            ScopeSearchType: GameObjectOptionSourceTarget.ObjectTemplates,
            ExcludeCurrentObject: true));

        Assert.Contains(results, option => option.Name == projectBaseB.Name);
    }
}

