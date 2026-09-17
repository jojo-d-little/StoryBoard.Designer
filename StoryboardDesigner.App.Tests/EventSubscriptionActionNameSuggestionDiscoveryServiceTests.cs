using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class EventSubscriptionActionNameSuggestionDiscoveryServiceTests
{
    [Fact]
    public void DiscoverLikelyActionNames_NullScope_ReturnsEmpty()
    {
        var service = new EventSubscriptionActionNameSuggestionDiscoveryService();

        var result = service.DiscoverLikelyActionNames(null);

        Assert.Empty(result);
    }

    [Fact]
    public void DiscoverLikelyActionNames_OrdersNearestScopeFirst()
    {
        var service = new EventSubscriptionActionNameSuggestionDiscoveryService();

        var room = new Room
        {
            Name = "Start Room",
            AvailableActions =
            [
                new CommandAction { Name = "roomOnly" }
            ]
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id,
            AvailableActions =
            [
                new CommandAction { Name = "areaOnly" }
            ]
        };

        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };

        var project = new ProjectModel
        {
            Name = "Suggestions",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        project.GlobalScope.AvailableActions.Add(new CommandAction { Name = "globalOnly" });

        ScopeHierarchy.AttachParents(project);

        var result = service.DiscoverLikelyActionNames(room);

        Assert.Equal(["roomOnly", "areaOnly", "globalOnly"], result);
    }

    [Fact]
    public void DiscoverLikelyActionNames_DeduplicatesByClosestScope()
    {
        var service = new EventSubscriptionActionNameSuggestionDiscoveryService();

        var room = new Room
        {
            Name = "Start Room",
            AvailableActions =
            [
                new CommandAction { Name = "inspect" },
                new CommandAction { Name = "openDoor" }
            ]
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };

        var project = new ProjectModel
        {
            Name = "Suggestions",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        project.GlobalScope.AvailableActions.Add(new CommandAction { Name = "inspect" });
        project.GlobalScope.AvailableActions.Add(new CommandAction { Name = "globalOnly" });

        ScopeHierarchy.AttachParents(project);

        var result = service.DiscoverLikelyActionNames(room);

        Assert.Equal(["inspect", "openDoor", "globalOnly"], result);
    }
}
