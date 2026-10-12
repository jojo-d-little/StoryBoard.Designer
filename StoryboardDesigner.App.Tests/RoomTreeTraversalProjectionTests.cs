using System.Reflection;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class RoomTreeTraversalProjectionTests
{
    [Fact]
    public void BuildHierarchy_PlanetNode_PlacesPropertiesBesideSettingsAndConstructsBeforeChildren()
    {
        var project = CreateBasicProject();

        var hierarchy = BuildHierarchy(project);
        var planetNode = EnumerateNodes(hierarchy)
            .OfType<PlanetNodeViewModel>()
            .Single();
        var constructsNode = Assert.Single(planetNode.Children.OfType<ConstructsNodeViewModel>());
        var actionsNode = Assert.Single(constructsNode.Children.OfType<ScopedActionsNodeViewModel>());

        var settingsNode = Assert.Single(planetNode.Children.OfType<PlanetSettingsNodeViewModel>());
        var variablesNode = Assert.Single(planetNode.Children.OfType<GamePropertiesContainerNodeViewModel>());
        Assert.Same(planetNode, variablesNode.Parent);
        Assert.Equal(1, planetNode.Children.IndexOf(variablesNode));
        Assert.Same(planetNode.Planet, variablesNode.ScopeOwner);
        Assert.Equal("Constructs", constructsNode.EditableName);
        Assert.Equal("Actions", actionsNode.EditableName);
        Assert.Same(planetNode, constructsNode.Parent);
        Assert.Same(planetNode.Planet, constructsNode.ScopeOwner);
        Assert.Same(constructsNode, actionsNode.Parent);
        Assert.Same(planetNode.Planet, actionsNode.ScopeOwner);
        Assert.Contains(constructsNode.Children, child => child is SequencesNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedSoundEffectsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.EditableName == "Base Objects");
        Assert.Contains(constructsNode.Children, child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.EditableName == "Object Templates");
        Assert.Contains(constructsNode.Children, child => child is RoomTemplatesNodeViewModel);
        var objectsNode = Assert.Single(constructsNode.Children.OfType<PlanetGameObjectsNodeViewModel>());
        Assert.Same(constructsNode, objectsNode.Parent);
        Assert.Same(planetNode.Planet, objectsNode.ScopeOwner);

        var planetGroup = planetNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();
        Assert.Equal("Countries", planetGroup.EditableName);
    }

    [Fact]
    public void BuildHierarchy_SequenceFolders_DoNotBecomeModelScopeOwners()
    {
        var project = CreateBasicProject();
        var area = project.Planets[0].Countries[0].Areas[0];
        area.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = "Area Sequence"
        });

        var hierarchy = BuildHierarchy(project);
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();
        var constructsNode = Assert.Single(areaNode.Children.OfType<ConstructsNodeViewModel>());
        var sequencesNode = Assert.Single(constructsNode.Children.OfType<SequencesNodeViewModel>());

        Assert.Same(area, sequencesNode.Owner);
        Assert.Same(constructsNode, sequencesNode.Parent);
        Assert.Single(sequencesNode.Children.OfType<SequenceDefinitionNodeViewModel>());
        Assert.DoesNotContain(
            EnumerateNodes([sequencesNode]),
            node => node is ConstructsNodeViewModel);
    }

    [Fact]
    public void BuildHierarchy_PhasesRemainBookChapterPageOnly()
    {
        var page = new PhaseNode { Tier = PhaseTier.Page, PhaseKey = "page-one", DisplayName = "Page One" };
        var chapter = new PhaseNode { Tier = PhaseTier.Chapter, PhaseKey = "chapter-one", DisplayName = "Chapter One" };
        chapter.Children.Add(page);
        var book = new PhaseNode { Tier = PhaseTier.Book, PhaseKey = "book-one", DisplayName = "Book One" };
        book.Children.Add(chapter);
        var project = CreateBasicProject();
        project.PhaseBooks.Add(book);

        var hierarchy = BuildHierarchy(project);
        var phaseBooks = Assert.Single(EnumerateNodes(hierarchy).OfType<PhaseBooksNodeViewModel>());
        var bookNode = Assert.Single(phaseBooks.Children.OfType<PhaseNodeViewModel>());
        var chapterNode = Assert.Single(bookNode.Children.OfType<PhaseNodeViewModel>());
        var pageNode = Assert.Single(chapterNode.Children.OfType<PhaseNodeViewModel>());

        Assert.Equal(PhaseTier.Book, bookNode.PhaseNode.Tier);
        Assert.Equal(PhaseTier.Chapter, chapterNode.PhaseNode.Tier);
        Assert.Equal(PhaseTier.Page, pageNode.PhaseNode.Tier);
        Assert.DoesNotContain(
            EnumerateNodes([phaseBooks]),
            node => node is ConstructsNodeViewModel or SequencesNodeViewModel);
    }

    [Fact]
    public void BuildHierarchy_TypedChildrenGroups_UseSpecificPluralLabels()
    {
        var project = CreateBasicProject();

        var hierarchy = BuildHierarchy(project);
        var countryNode = EnumerateNodes(hierarchy).OfType<CountryNodeViewModel>().Single();
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();
        var roomObjectNode = EnumerateNodes(hierarchy).OfType<GameObjectNodeViewModel>().FirstOrDefault();

        var countryGroup = countryNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();
        var areaGroup = areaNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();

        Assert.Equal("Areas", countryGroup.EditableName);
        Assert.Equal("Rooms", areaGroup.EditableName);

        if (roomObjectNode is not null)
        {
            var constructsNode = Assert.Single(roomObjectNode.Children.OfType<ConstructsNodeViewModel>());
            Assert.Contains(roomObjectNode.Children, child => child is GameObjectSettingsNodeViewModel);
            var objectSettings = Assert.Single(roomObjectNode.Children.OfType<GameObjectSettingsNodeViewModel>());
            var objectProperties = Assert.Single(roomObjectNode.Children.OfType<GamePropertiesContainerNodeViewModel>());
            Assert.Same(roomObjectNode, objectProperties.Parent);
            Assert.Equal(roomObjectNode.Children.IndexOf(objectSettings) + 1, roomObjectNode.Children.IndexOf(objectProperties));
            var actionsNode = Assert.Single(constructsNode.Children.OfType<ScopedActionsNodeViewModel>());
            Assert.Equal("Actions", actionsNode.EditableName);
            Assert.Same(constructsNode, actionsNode.Parent);
            Assert.Same(roomObjectNode.GameObject, actionsNode.ScopeOwner);
            Assert.Contains(constructsNode.Children, child => child is ScopedVerbsNodeViewModel);
            Assert.Contains(constructsNode.Children, child => child is ScopedDirectionalsNodeViewModel);
            Assert.Contains(constructsNode.Children, child => child is ScopedSoundEffectsNodeViewModel);
            Assert.Contains(constructsNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
            Assert.Contains(constructsNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
            Assert.Contains(constructsNode.Children, child => child is ScopedProceduresNodeViewModel);
            var objectGroup = roomObjectNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();
            Assert.Equal("Contained Objects", objectGroup.EditableName);
        }
    }

    [Fact]
    public void BuildHierarchy_PrimaryWorldChildren_AppearBeforeConfigurationFolders()
    {
        var project = CreateBasicProject();
        var primaryPlanet = project.Planets[0];
        primaryPlanet.Name = "Zulu Planet";
        primaryPlanet.Countries[0].Name = "Zulu Country";
        primaryPlanet.Countries[0].Areas[0].Name = "Zulu Area";
        primaryPlanet.Countries[0].Areas[0].Rooms =
        [
            new Room { Name = "Zulu Room" },
            new Room { Name = "Alpha Room" }
        ];
        primaryPlanet.Countries[0].Areas.Add(new Area { Name = "Alpha Area" });
        primaryPlanet.Countries.Add(new Country { Name = "Alpha Country" });
        project.Planets.Add(new Planet { Name = "Alpha Planet" });

        var hierarchy = BuildHierarchy(project);
        var globalNode = Assert.IsType<ProjectRootNodeViewModel>(Assert.Single(hierarchy));
        var planetNode = globalNode.Children.OfType<PlanetNodeViewModel>()
            .Single(node => node.EditableName == "Zulu Planet");
        var countriesFolder = Assert.IsType<ChildrenGroupNodeViewModel>(planetNode.Children[3]);
        var countryNode = countriesFolder.Children.OfType<CountryNodeViewModel>()
            .Single(node => node.EditableName == "Zulu Country");
        var areasFolder = Assert.IsType<ChildrenGroupNodeViewModel>(countryNode.Children[3]);
        var areaNode = Assert.IsType<AreaNodeViewModel>(areasFolder.Children[1]);
        var roomsFolder = Assert.IsType<ChildrenGroupNodeViewModel>(areaNode.Children[3]);

        Assert.IsType<GlobalSettingsNodeViewModel>(globalNode.Children[0]);
        Assert.IsType<GamePropertiesContainerNodeViewModel>(globalNode.Children[1]);
        Assert.IsType<ConstructsNodeViewModel>(globalNode.Children[2]);
        Assert.Equal("Alpha Planet", Assert.IsType<PlanetNodeViewModel>(globalNode.Children[3]).EditableName);
        Assert.Equal("Zulu Planet", Assert.IsType<PlanetNodeViewModel>(globalNode.Children[4]).EditableName);
        var globalConstructs = Assert.IsType<ConstructsNodeViewModel>(globalNode.Children[2]);
        Assert.Contains(globalConstructs.Children, child => child is GlobalObjectsNodeViewModel);
        Assert.Contains(globalConstructs.Children, child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.IsBaseCatalog);
        Assert.Contains(globalConstructs.Children, child => child is ObjectTemplatesNodeViewModel templatesNode && !templatesNode.IsBaseCatalog);
        Assert.Contains(globalConstructs.Children, child => child is RoomTemplatesNodeViewModel);
        Assert.Equal("Countries", countriesFolder.EditableName);
        Assert.IsType<PlanetSettingsNodeViewModel>(planetNode.Children[0]);
        Assert.IsType<GamePropertiesContainerNodeViewModel>(planetNode.Children[1]);
        Assert.IsType<ConstructsNodeViewModel>(planetNode.Children[2]);
        Assert.Contains(Assert.IsType<ConstructsNodeViewModel>(planetNode.Children[2]).Children,
            child => child is PlanetGameObjectsNodeViewModel);
        Assert.Contains(Assert.IsType<ConstructsNodeViewModel>(planetNode.Children[2]).Children,
            child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.IsBaseCatalog);
        Assert.Equal("Areas", areasFolder.EditableName);
        Assert.IsType<CountrySettingsNodeViewModel>(countryNode.Children[0]);
        Assert.IsType<GamePropertiesContainerNodeViewModel>(countryNode.Children[1]);
        Assert.IsType<ConstructsNodeViewModel>(countryNode.Children[2]);
        Assert.Contains(Assert.IsType<ConstructsNodeViewModel>(countryNode.Children[2]).Children,
            child => child is CountryGameObjectsNodeViewModel);
        Assert.Contains(Assert.IsType<ConstructsNodeViewModel>(countryNode.Children[2]).Children,
            child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.IsBaseCatalog);
        Assert.Equal("Rooms", roomsFolder.EditableName);
        Assert.IsType<AreaSettingsNodeViewModel>(areaNode.Children[0]);
        Assert.IsType<GamePropertiesContainerNodeViewModel>(areaNode.Children[1]);
        Assert.IsType<ConstructsNodeViewModel>(areaNode.Children[2]);
        Assert.Contains(Assert.IsType<ConstructsNodeViewModel>(areaNode.Children[2]).Children,
            child => child is AreaGameObjectsNodeViewModel);
        Assert.Contains(Assert.IsType<ConstructsNodeViewModel>(areaNode.Children[2]).Children,
            child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.IsBaseCatalog);
        Assert.Equal(["Alpha Country", "Zulu Country"], countriesFolder.Children.Select(node => node.EditableName));
        Assert.Equal(["Alpha Area", "Zulu Area"], areasFolder.Children.Select(node => node.EditableName));
        Assert.Equal(["Alpha Room", "Zulu Room"], roomsFolder.Children.Select(node => node.EditableName));
    }

    [Fact]
    public void BuildHierarchy_ScopedActionsNodes_DefaultToCollapsed()
    {
        var project = CreateBasicProject();

        var hierarchy = BuildHierarchy(project);
        var actionsNodes = EnumerateNodes(hierarchy)
            .OfType<ScopedActionsNodeViewModel>()
            .ToList();

        Assert.NotEmpty(actionsNodes);
        Assert.All(actionsNodes, node => Assert.False(node.IsExpanded));
    }

    [Fact]
    public void BuildHierarchy_ScopedBaseObjectCatalogs_AppearUnderConstructs_NotChildrenGroups()
    {
        var project = CreateBasicProject();
        var country = project.Planets[0].Countries[0];
        var area = country.Areas[0];

        country.BaseObjects.Add(new GameObject { Name = "Country Base Object" });
        area.BaseObjects.Add(new GameObject { Name = "Area Base Object" });

        var hierarchy = BuildHierarchy(project);
        var countryNode = EnumerateNodes(hierarchy).OfType<CountryNodeViewModel>().Single();
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();

        var countryChildrenGroup = countryNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();
        var areaChildrenGroup = areaNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();
        var countryConstructs = Assert.Single(countryNode.Children.OfType<ConstructsNodeViewModel>());
        var areaConstructs = Assert.Single(areaNode.Children.OfType<ConstructsNodeViewModel>());
        Assert.Same(country, countryConstructs.ScopeOwner);
        Assert.Same(area, areaConstructs.ScopeOwner);

        var countryBaseCatalog = Assert.Single(
            countryConstructs.Children.OfType<ObjectTemplatesNodeViewModel>(),
            templatesNode => templatesNode.IsBaseCatalog);
        Assert.Same(countryConstructs, countryBaseCatalog.Parent);
        Assert.Same(country, countryBaseCatalog.ScopeOwner);

        Assert.Contains(countryConstructs.Children, child =>
            child is ObjectTemplatesNodeViewModel templatesNode
            && templatesNode.IsBaseCatalog
            && templatesNode.EditableName == "Base Objects"
            && ReferenceEquals(templatesNode.CatalogParentScope, country));
        Assert.DoesNotContain(countryChildrenGroup.Children, child => child is ObjectTemplatesNodeViewModel);

        Assert.Contains(areaConstructs.Children, child =>
            child is ObjectTemplatesNodeViewModel templatesNode
            && templatesNode.IsBaseCatalog
            && templatesNode.EditableName == "Base Objects"
            && ReferenceEquals(templatesNode.CatalogParentScope, area));
        Assert.DoesNotContain(areaChildrenGroup.Children, child => child is ObjectTemplatesNodeViewModel);
    }

    [Fact]
    public void BuildHierarchy_GlobalRoot_RemainsDirectConfigurationLayout()
    {
        var project = CreateBasicProject();
        project.ProcedureIds.Add(Guid.NewGuid());

        var hierarchy = BuildHierarchy(project);
        var root = Assert.IsType<ProjectRootNodeViewModel>(Assert.Single(hierarchy));
        var constructsNode = Assert.Single(root.Children.OfType<ConstructsNodeViewModel>());

        Assert.DoesNotContain(root.Children, child => child is ConfigurationGroupNodeViewModel);
        Assert.DoesNotContain(root.Children, child => child is ChildrenGroupNodeViewModel);
        Assert.Contains(root.Children, child => child is GlobalSettingsNodeViewModel);
        var proceduresNode = Assert.Single(constructsNode.Children.OfType<ScopedProceduresNodeViewModel>());
        Assert.Same(constructsNode, proceduresNode.Parent);
        Assert.Same(project, proceduresNode.ScopeOwner);
    }

    [Fact]
    public void BuildHierarchy_GlobalCollectionNode_UsesSharedAuthoredCollectionModel()
    {
        var project = CreateBasicProject();
        project.GameObjects.Add(new GameObject { Name = "Global Coin" });

        var hierarchy = BuildHierarchy(project);
        var root = Assert.IsType<ProjectRootNodeViewModel>(Assert.Single(hierarchy));
        var constructsNode = Assert.Single(root.Children.OfType<ConstructsNodeViewModel>());
        var globalCollectionNode = Assert.IsType<GlobalObjectsNodeViewModel>(constructsNode.Children.Single(child => child is GlobalObjectsNodeViewModel));
        var authoredNode = Assert.IsAssignableFrom<GameObjectsNodeViewModel>(globalCollectionNode);

        var scopeNode = Assert.IsAssignableFrom<ScopeNodeBase>(authoredNode.ScopeNode);
        Assert.Equal(ScopeNodeKind.Global, scopeNode.ScopeKind);
        Assert.Equal("Global", scopeNode.ScopeName);
        Assert.Same(constructsNode, globalCollectionNode.Parent);
        Assert.Same(project, globalCollectionNode.ScopeOwner);
        Assert.Single(authoredNode.GameObjects);
        Assert.Equal("Global Coin", authoredNode.GameObjects[0].Name);
    }

    [Fact]
    public void BuildHierarchy_RoomObjectsNode_UsesSharedAuthoredCollectionModel()
    {
        var project = CreateBasicProject();
        var area = project.Planets[0].Countries[0].Areas[0];
        var room = new Room
        {
            Name = "Room A",
            GameObjects = [new GameObject { Name = "Room Key" }]
        };
        area.Rooms.Add(room);

        var hierarchy = BuildHierarchy(project);
        var roomNode = EnumerateNodes(hierarchy)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == room.Id);
        var constructsNode = Assert.Single(roomNode.Children.OfType<ConstructsNodeViewModel>());
        var roomObjectsNode = constructsNode.Children.OfType<RoomGameObjectsNodeViewModel>().Single();
        var authoredNode = Assert.IsAssignableFrom<GameObjectsNodeViewModel>(roomObjectsNode);

        Assert.Same(room, authoredNode.ScopeNode);
        Assert.Same(constructsNode, roomObjectsNode.Parent);
        Assert.Same(room, roomObjectsNode.ScopeOwner);
        Assert.Single(authoredNode.GameObjects);
        Assert.Equal("Room Key", authoredNode.GameObjects[0].Name);
    }

    [Fact]
    public void BuildHierarchy_PlanetCountryAreaObjectsNodes_UseSharedAuthoredCollectionModel()
    {
        var project = CreateBasicProject();
        var planet = project.Planets[0];
        var country = planet.Countries[0];
        var area = country.Areas[0];

        planet.GameObjects.Add(new GameObject { Name = "Planet Relic" });
        country.GameObjects.Add(new GameObject { Name = "Country Seal" });
        area.GameObjects.Add(new GameObject { Name = "Area Cache" });

        var hierarchy = BuildHierarchy(project);

        var planetNode = EnumerateNodes(hierarchy).OfType<PlanetNodeViewModel>().Single();
        var countryNode = EnumerateNodes(hierarchy).OfType<CountryNodeViewModel>().Single();
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();

        var planetObjectsNode = Assert.Single(planetNode.Children.OfType<ConstructsNodeViewModel>().Single().Children.OfType<PlanetGameObjectsNodeViewModel>());
        var countryObjectsNode = Assert.Single(countryNode.Children.OfType<ConstructsNodeViewModel>().Single().Children.OfType<CountryGameObjectsNodeViewModel>());
        var areaObjectsNode = Assert.Single(areaNode.Children.OfType<ConstructsNodeViewModel>().Single().Children.OfType<AreaGameObjectsNodeViewModel>());

        var planetAuthoredNode = Assert.IsAssignableFrom<GameObjectsNodeViewModel>(planetObjectsNode);
        var countryAuthoredNode = Assert.IsAssignableFrom<GameObjectsNodeViewModel>(countryObjectsNode);
        var areaAuthoredNode = Assert.IsAssignableFrom<GameObjectsNodeViewModel>(areaObjectsNode);

        Assert.Same(planet, planetAuthoredNode.ScopeNode);
        Assert.Same(country, countryAuthoredNode.ScopeNode);
        Assert.Same(area, areaAuthoredNode.ScopeNode);

        Assert.Single(planetAuthoredNode.GameObjects);
        Assert.Single(countryAuthoredNode.GameObjects);
        Assert.Single(areaAuthoredNode.GameObjects);
    }

    [Fact]
    public void BuildHierarchy_GlobalRoot_ProjectsRoomTemplatesCatalogAndTemplateRooms()
    {
        var project = CreateBasicProject();
        project.RoomTemplates.Add(new Room { Name = "Template Room" });

        var hierarchy = BuildHierarchy(project);
        var root = Assert.IsType<ProjectRootNodeViewModel>(Assert.Single(hierarchy));
        var rootConstructsNode = Assert.Single(root.Children.OfType<ConstructsNodeViewModel>());
        var templatesNode = Assert.IsType<RoomTemplatesNodeViewModel>(
            rootConstructsNode.Children.Single(child => child is RoomTemplatesNodeViewModel));

        var templateRoomNode = Assert.IsType<TemplateRoomNodeViewModel>(Assert.Single(templatesNode.Children));
        Assert.Equal("Template Room", templateRoomNode.EditableName);

        Assert.Contains(templateRoomNode.Children, child => child is RoomSettingsNodeViewModel);
        var roomSettings = Assert.Single(templateRoomNode.Children.OfType<RoomSettingsNodeViewModel>());
        var templateVariables = Assert.Single(templateRoomNode.Children.OfType<GamePropertiesContainerNodeViewModel>());
        Assert.Same(templateRoomNode, templateVariables.Parent);
        Assert.Equal(1, templateRoomNode.Children.IndexOf(templateVariables));
        Assert.Same(templateRoomNode.Room, templateVariables.ScopeOwner);
        var constructsNode = Assert.Single(templateRoomNode.Children.OfType<ConstructsNodeViewModel>());
        Assert.Contains(constructsNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is SequencesNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedSoundEffectsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);

        var templateObjectsNode = constructsNode.Children.OfType<ObjectTemplatesNodeViewModel>().Single();
        Assert.Equal("Game Objects", templateObjectsNode.EditableName);
        var getOrCreateObjectsNode = typeof(MainWindowViewModel).GetMethod(
            "GetOrCreateTemplateRoomObjectsNode",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(getOrCreateObjectsNode);
        Assert.Same(templateObjectsNode, getOrCreateObjectsNode!.Invoke(null, [templateRoomNode]));
        Assert.Single(constructsNode.Children.OfType<ObjectTemplatesNodeViewModel>());
    }

    [Fact]
    public void BuildHierarchy_RoomNode_ProjectsConfigurationTraversalAndObjectsAsDirectChildren()
    {
        var project = CreateBasicProject();

        var area = project.Planets[0].Countries[0].Areas[0];
        var roomA = new Room { Name = "Room A" };
        var roomB = new Room { Name = "Room B" };
        area.Rooms.Add(roomA);
        area.Rooms.Add(roomB);

        area.TraversalConnections.Add(new TraversalConnection
        {
            TraversalConnectionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RoomAId = roomA.Id,
            RoomBId = roomB.Id,
            BaseTraversalDirectionFromA = Direction10.East,
            TraversalStateFromA = new TraversalLegState
            {
                AvailableActions =
                [
                    new CommandAction { Name = "Open Gate", ActionType = CommandActionType.EchoMessage }
                ]
            },
            TraversalStateFromB = new TraversalLegState()
        });

        var hierarchy = BuildHierarchy(project);
        var roomNode = EnumerateNodes(hierarchy)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == roomA.Id);

        Assert.Contains(roomNode.Children, child => child is RoomSettingsNodeViewModel);
        var roomSettings = Assert.Single(roomNode.Children.OfType<RoomSettingsNodeViewModel>());
        var roomVariables = Assert.Single(roomNode.Children.OfType<GamePropertiesContainerNodeViewModel>());
        Assert.Same(roomNode, roomVariables.Parent);
        Assert.Equal(1, roomNode.Children.IndexOf(roomVariables));
        Assert.Same(roomNode.Room, roomVariables.ScopeOwner);
        var constructsNode = Assert.Single(roomNode.Children.OfType<ConstructsNodeViewModel>());
        Assert.Contains(constructsNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is SequencesNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedSoundEffectsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is RoomTraversalLegsNodeViewModel);
        Assert.Contains(constructsNode.Children, child => child is RoomGameObjectsNodeViewModel);

        var traversalLegsFolder = roomNode.Children.OfType<RoomTraversalLegsNodeViewModel>().Single();
        var objectsFolder = constructsNode.Children.OfType<RoomGameObjectsNodeViewModel>().Single();

        Assert.NotNull(objectsFolder);

        var legNode = Assert.IsType<TraversalLegNodeViewModel>(Assert.Single(traversalLegsFolder.Children));
        Assert.Equal(Direction10.East, legNode.Direction);
        Assert.Equal("Room B", legNode.DestinationRoomName);
        var legConstructsNode = Assert.Single(legNode.Children.OfType<ConstructsNodeViewModel>());
        Assert.Contains(legConstructsNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.Contains(legNode.Children, child => child is GamePropertiesContainerNodeViewModel);
    }

    [Fact]
    public void BuildHierarchy_RoomTraversalLegs_AreSortedByDirectionThenDestinationNameThenConnectionId()
    {
        var project = CreateBasicProject();

        var area = project.Planets[0].Countries[0].Areas[0];
        var hub = new Room { Name = "Hub" };
        var alpha = new Room { Name = "Alpha" };
        var beta = new Room { Name = "Beta" };
        var zulu = new Room { Name = "Zulu" };
        area.Rooms.Add(hub);
        area.Rooms.Add(alpha);
        area.Rooms.Add(beta);
        area.Rooms.Add(zulu);

        area.TraversalConnections.Add(new TraversalConnection
        {
            TraversalConnectionId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            RoomAId = hub.Id,
            RoomBId = zulu.Id,
            BaseTraversalDirectionFromA = Direction10.East
        });

        area.TraversalConnections.Add(new TraversalConnection
        {
            TraversalConnectionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RoomAId = hub.Id,
            RoomBId = alpha.Id,
            BaseTraversalDirectionFromA = Direction10.East
        });

        area.TraversalConnections.Add(new TraversalConnection
        {
            TraversalConnectionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RoomAId = hub.Id,
            RoomBId = beta.Id,
            BaseTraversalDirectionFromA = Direction10.North
        });

        var hierarchy = BuildHierarchy(project);
        var roomNode = EnumerateNodes(hierarchy)
            .OfType<RoomNodeViewModel>()
            .Single(node => node.Room.Id == hub.Id);
        var traversalLegsFolder = roomNode.Children
            .OfType<RoomTraversalLegsNodeViewModel>()
            .Single();

        var legs = traversalLegsFolder.Children.OfType<TraversalLegNodeViewModel>().ToList();

        Assert.Collection(legs,
            leg =>
            {
                Assert.Equal(Direction10.North, leg.Direction);
                Assert.Equal("Beta", leg.DestinationRoomName);
            },
            leg =>
            {
                Assert.Equal(Direction10.East, leg.Direction);
                Assert.Equal("Alpha", leg.DestinationRoomName);
            },
            leg =>
            {
                Assert.Equal(Direction10.East, leg.Direction);
                Assert.Equal("Zulu", leg.DestinationRoomName);
            });
    }

    [Fact]
    public void BuildHierarchy_HideEmptyOnPlanet_SuppressesEmptyConfigurationGroups_ButKeepsCountriesFolder()
    {
        var project = CreateBasicProject();
        var planet = project.Planets.Single();
        planet.HideEmptyConfiguration = true;

        var hierarchy = BuildHierarchy(project);
        var planetNode = EnumerateNodes(hierarchy).OfType<PlanetNodeViewModel>().Single();

        Assert.Contains(planetNode.Children, child => child is PlanetSettingsNodeViewModel);
        Assert.Contains(planetNode.Children, child => child is ChildrenGroupNodeViewModel);

        Assert.DoesNotContain(planetNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children.OfType<ConstructsNodeViewModel>().SelectMany(node => node.Children), child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is PlanetGameObjectsNodeViewModel);
        Assert.DoesNotContain(planetNode.Children, child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.IsBaseCatalog);
    }

    [Fact]
    public void BuildHierarchy_HideEmptyOnArea_KeepsRoomsFolderVisible_WhenNoRoomsExist()
    {
        var project = CreateBasicProject();
        var area = project.Planets.Single().Countries.Single().Areas.Single();
        area.HideEmptyConfiguration = true;

        var hierarchy = BuildHierarchy(project);
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();

        var roomsFolder = areaNode.Children.OfType<ChildrenGroupNodeViewModel>().Single();
        Assert.Empty(roomsFolder.Children);

        Assert.Contains(areaNode.Children, child => child is AreaSettingsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children.OfType<ConstructsNodeViewModel>().SelectMany(node => node.Children), child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is AreaGameObjectsNodeViewModel);
        Assert.DoesNotContain(areaNode.Children, child => child is ObjectTemplatesNodeViewModel templatesNode && templatesNode.IsBaseCatalog);
    }

    [Fact]
    public void BuildHierarchy_HideEmptyOnGameObject_KeepsSettingsButHidesEmptyFolders()
    {
        var objectModel = new GameObject
        {
            Name = "HiddenEmptyObject",
            HideEmptyConfiguration = true
        };

        var area = new Area
        {
            Name = "Area",
            Rooms =
            [
                new Room
                {
                    Name = "Room",
                    GameObjects = [objectModel]
                }
            ]
        };

        var project = new ProjectModel
        {
            Name = "Object hide-empty projection",
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [area] }] }]
        };

        var hierarchy = BuildHierarchy(project);
        var objectNode = EnumerateNodes(hierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == objectModel.ObjectId);

        Assert.Contains(objectNode.Children, child => child is GameObjectSettingsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children.OfType<ConstructsNodeViewModel>().SelectMany(node => node.Children), child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.DoesNotContain(objectNode.Children, child => child is ChildrenGroupNodeViewModel);
    }

    [Fact]
    public void BuildHierarchy_LinkedObjectNodes_UseInstanceNameAndEffectiveNameInGameAlias()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Flowers",
            NameInGame = "Garden Flowers"
        };

        var roomLinked = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Stale Room Name",
            LinkedBaseObjectId = baseObjectId,
            Variables = [new GamePropertyDefinition { Name = "instanceState", DefaultValue = "ready" }]
        };

        var playerLinked = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Stale Player Name",
            LinkedBaseObjectId = baseObjectId
        };

        var templateLinked = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Stale Template Name",
            LinkedBaseObjectId = baseObjectId
        };

        var area = new Area
        {
            Name = "Area",
            Rooms =
            [
                new Room
                {
                    Name = "Room",
                    GameObjects = [baseObject, roomLinked]
                }
            ]
        };

        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "Effective Name Test",
            Planets = [planet],
            GameObjects = [playerLinked],
            ObjectTemplates = [templateLinked]
        };

        var hierarchy = BuildHierarchy(project);

        var roomNode = EnumerateNodes(hierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == roomLinked.ObjectId);
        var playerNode = EnumerateNodes(hierarchy)
            .OfType<GlobalObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == playerLinked.ObjectId);
        var templateNode = EnumerateNodes(hierarchy)
            .OfType<TemplateGameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == templateLinked.ObjectId);

        Assert.Equal("Stale Room Name", roomNode.EditableName);
        Assert.Equal("Stale Player Name", playerNode.EditableName);
        Assert.Equal("Stale Template Name", templateNode.EditableName);
        Assert.Equal(string.Empty, roomNode.EffectiveNameInGameDisplay);
        Assert.DoesNotContain(roomNode.Children, child => child is GameObjectSettingsNodeViewModel);
        var roomProperties = Assert.Single(roomNode.Children.OfType<GamePropertiesContainerNodeViewModel>());
        Assert.Same(roomNode, roomProperties.Parent);
        Assert.Equal(string.Empty, playerNode.EffectiveNameInGameDisplay);
        Assert.Equal(string.Empty, templateNode.EffectiveNameInGameDisplay);
    }

    [Fact]
    public void BuildHierarchy_LinkedObjectNodes_UseNameInGameOverride_WhenPresent()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Door",
            NameInGame = "Base Alias"
        };

        var linked = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Door Instance",
            LinkedBaseObjectId = baseObjectId,
            NameInGame = "West Door"
        };

        var area = new Area
        {
            Name = "Area",
            Rooms =
            [
                new Room
                {
                    Name = "Room",
                    GameObjects = [baseObject, linked]
                }
            ]
        };

        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "Effective NameInGame Override Test",
            Planets = [planet]
        };

        var hierarchy = BuildHierarchy(project);

        var linkedNode = EnumerateNodes(hierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linked.ObjectId);

        Assert.Equal("Door Instance", linkedNode.EditableName);
        Assert.Equal("West Door", linkedNode.EffectiveNameInGameDisplay);
    }

    [Fact]
    public void BuildHierarchy_LinkedObjectNodes_IgnoreLegacyNameOverride_WhenPresent()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Flowers"
        };

        var linked = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Stale Name",
            LinkedBaseObjectId = baseObjectId,
            InstanceOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "Overridden Flowers"
            }
        };

        var area = new Area
        {
            Name = "Area",
            Rooms =
            [
                new Room
                {
                    Name = "Room",
                    GameObjects = [baseObject, linked]
                }
            ]
        };

        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "Effective Name Override Test",
            Planets = [planet]
        };

        var hierarchy = BuildHierarchy(project);

        var linkedNode = EnumerateNodes(hierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linked.ObjectId);

        Assert.Equal("Stale Name", linkedNode.EditableName);
    }

    [Fact]
    public void BuildHierarchy_AfterBaseRename_KeepsLinkedInstanceNamesOnRebuild()
    {
        var baseObjectId = Guid.NewGuid();
        var baseObject = new GameObject
        {
            ObjectId = baseObjectId,
            Name = "Base Flowers"
        };

        var linkedInherited = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Stale Inherited",
            LinkedBaseObjectId = baseObjectId
        };

        var linkedOverridden = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Stale Overridden",
            LinkedBaseObjectId = baseObjectId,
            InstanceOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Description"] = "Custom Flowers"
            }
        };

        var area = new Area
        {
            Name = "Area",
            Rooms =
            [
                new Room
                {
                    Name = "Room",
                    GameObjects = [baseObject, linkedInherited, linkedOverridden]
                }
            ]
        };

        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel
        {
            Name = "Hierarchy Rebuild Name Refresh",
            Planets = [planet]
        };

        var firstHierarchy = BuildHierarchy(project);
        var firstInheritedNode = EnumerateNodes(firstHierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linkedInherited.ObjectId);
        var firstOverriddenNode = EnumerateNodes(firstHierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linkedOverridden.ObjectId);

        Assert.Equal("Stale Inherited", firstInheritedNode.EditableName);
        Assert.Equal("Stale Overridden", firstOverriddenNode.EditableName);

        baseObject.Name = "Base Flowers Renamed";

        var rebuiltHierarchy = BuildHierarchy(project);
        var rebuiltInheritedNode = EnumerateNodes(rebuiltHierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linkedInherited.ObjectId);
        var rebuiltOverriddenNode = EnumerateNodes(rebuiltHierarchy)
            .OfType<GameObjectNodeViewModel>()
            .Single(node => node.GameObject.ObjectId == linkedOverridden.ObjectId);

        Assert.Equal("Stale Inherited", rebuiltInheritedNode.EditableName);
        Assert.Equal("Stale Overridden", rebuiltOverriddenNode.EditableName);
    }

    private static IReadOnlyList<HierarchyNodeViewModel> BuildHierarchy(ProjectModel project)
    {
        var method = typeof(MainWindowViewModel).GetMethod("BuildHierarchy", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [project]);
        return Assert.IsType<List<HierarchyNodeViewModel>>(result);
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        var stack = new Stack<HierarchyNodeViewModel>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static ProjectModel CreateBasicProject()
    {
        var area = new Area { Name = "Area" };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };

        return new ProjectModel
        {
            Name = "Projection Test",
            Planets = [planet]
        };
    }
}
