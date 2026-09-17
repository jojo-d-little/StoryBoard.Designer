using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class CompositePartDiscoveryServiceRoutingTests
{
    [Fact]
    public void EditObjectBasicProperties_RoomObject_CompositeOptions_AreDiscoveryBacked()
    {
        var target = new GameObject { Name = "Workbench" };
        var sibling = new GameObject { Name = "Wrench" };

        var room = new Room
        {
            Name = "Garage",
            GameObjects = [target, sibling]
        };

        var area = new Area
        {
            Name = "Area",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country",
            Areas = [area],
            StartingAreaName = area.Name
        };

        var planet = new Planet
        {
            Name = "Planet",
            Countries = [country],
            StartingCountryName = country.Name
        };

        var project = new ProjectModel
        {
            Name = "DiscoveryRouting",
            Planets = [planet],
            StartingPlanetName = planet.Name
        };

        ScopeHierarchy.AttachParents(project);

        var discovery = new RecordingGameObjectSelectionOptionDiscoveryService();
        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub(), discovery);

        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, rootNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        var objectNode = new GameObjectNodeViewModel(target, objectsNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.False(edited);

        var request = treeContext.LastObjectBasicPropertiesRequest;
        Assert.NotNull(request);
        Assert.Contains(request.AvailableCompositePartOptions, option =>
            option.Name == RecordingGameObjectSelectionOptionDiscoveryService.CompositeOptionName);
        Assert.DoesNotContain(request.AvailableCompositePartOptions, option => option.Name == sibling.Name);

        var compositeDiscoveryRequest = Assert.Single(discovery.Requests, req => req.ScopeSearchDepth == ScopeSearchDepth.LocalRoom);
        Assert.Equal(GameObjectOptionSourceTarget.RealObjects, compositeDiscoveryRequest.ScopeSearchType);
        Assert.False(compositeDiscoveryRequest.ExcludeCurrentObject);

        Assert.Contains(discovery.Requests, req =>
            req.ScopeSearchDepth == ScopeSearchDepth.Project
            && req.RequiredFeatures.HasFlag(GameObjectFeatureRequirements.Inventoriable));
    }

    [Fact]
    public void EditObjectBasicProperties_TemplateObject_CompositeDiscovery_UsesTemplateSearchType()
    {
        var target = new GameObject { Name = "Template Target" };
        var templatePart = new GameObject { Name = "Template Part" };

        var project = new ProjectModel
        {
            Name = "TemplateDiscoveryRouting",
            ObjectTemplates = [target, templatePart]
        };

        ScopeHierarchy.AttachParents(project);

        var discovery = new RecordingGameObjectSelectionOptionDiscoveryService();
        var treeContext = new TreeContextInteractionServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub(), discovery);

        var rootNode = new ProjectRootNodeViewModel(project);
        var templatesNode = new ObjectTemplatesNodeViewModel(project, rootNode);
        var objectNode = new TemplateGameObjectNodeViewModel(target, templatesNode);

        var edited = viewModel.EditObjectBasicProperties(objectNode);

        Assert.False(edited);

        var compositeDiscoveryRequest = Assert.Single(discovery.Requests, req => req.ScopeSearchDepth == ScopeSearchDepth.LocalRoom);
        Assert.Equal(GameObjectOptionSourceTarget.ObjectTemplates, compositeDiscoveryRequest.ScopeSearchType);
    }

    private sealed class RecordingGameObjectSelectionOptionDiscoveryService : IGameObjectSelectionOptionDiscoveryService
    {
        public const string CompositeOptionName = "Discovery Composite Option";

        public List<GameObjectSelectionOptionDiscoveryRequest> Requests { get; } = new();

        public IReadOnlyList<GameObjectSelectionOption> Discover(GameObjectSelectionOptionDiscoveryRequest request)
        {
            Requests.Add(request);

            if (request.ScopeSearchDepth == ScopeSearchDepth.LocalRoom)
            {
                return
                [
                    new GameObjectSelectionOption(Guid.Parse("11111111-1111-1111-1111-111111111111"), CompositeOptionName, false)
                ];
            }

            if (request.ScopeSearchDepth == ScopeSearchDepth.Project
                && request.RequiredFeatures.HasFlag(GameObjectFeatureRequirements.Inventoriable))
            {
                return
                [
                    new GameObjectSelectionOption(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Discovery Lock Option", true)
                ];
            }

            return Array.Empty<GameObjectSelectionOption>();
        }
    }
}

