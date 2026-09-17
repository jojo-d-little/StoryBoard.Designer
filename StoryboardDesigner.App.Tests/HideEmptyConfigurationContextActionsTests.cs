using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class HideEmptyConfigurationContextActionsTests
{
    [Fact]
    public void GetTreeContextActions_SupportedScopeNodes_IncludeHideEmptyConfigurationAction()
    {
        var project = BuildProject();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        var hierarchy = BuildHierarchy(project);
        var rootNode = Assert.IsType<ProjectRootNodeViewModel>(Assert.Single(hierarchy));
        var planetNode = EnumerateNodes(hierarchy).OfType<PlanetNodeViewModel>().Single();
        var countryNode = EnumerateNodes(hierarchy).OfType<CountryNodeViewModel>().Single();
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();
        var roomNode = EnumerateNodes(hierarchy).OfType<RoomNodeViewModel>().Single();
        var templateRoomNode = EnumerateNodes(hierarchy).OfType<TemplateRoomNodeViewModel>().Single();
        var objectNode = EnumerateNodes(hierarchy).OfType<GameObjectNodeViewModel>().Single();

        Assert.Contains(viewModel.GetTreeContextActions(rootNode), action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Contains(viewModel.GetTreeContextActions(planetNode), action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Contains(viewModel.GetTreeContextActions(countryNode), action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Contains(viewModel.GetTreeContextActions(areaNode), action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Contains(viewModel.GetTreeContextActions(roomNode), action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Contains(viewModel.GetTreeContextActions(templateRoomNode), action => action.ActionId == "toggle-hide-empty-configuration");

        Assert.DoesNotContain(viewModel.GetTreeContextActions(objectNode), action => action.ActionId == "toggle-hide-empty-configuration");

        Assert.Contains(viewModel.GetTreeContextActions(rootNode), action => action.ActionId == "hide-empty-configuration-global");
        Assert.Contains(viewModel.GetTreeContextActions(rootNode), action => action.ActionId == "show-empty-configuration-global");
    }

    [Fact]
    public void ExecuteTreeContextAction_ToggleHideEmptyConfiguration_RoomNode_RevealsAndHidesEmptyGroups()
    {
        var project = BuildProject();
        var roomModel = project.Planets[0].Countries[0].Areas[0].Rooms[0];
        roomModel.GameObjects.Clear();
        roomModel.HideEmptyConfiguration = true;

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        var hierarchy = BuildHierarchy(project);
        var roomNode = EnumerateNodes(hierarchy).OfType<RoomNodeViewModel>().Single();

        Assert.DoesNotContain(roomNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is RoomTraversalLegsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is RoomGameObjectsNodeViewModel);

        var initialAction = viewModel.GetTreeContextActions(roomNode)
            .Single(action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Equal("Show Empty Configuration", initialAction.Header);

        var handledShow = viewModel.ExecuteTreeContextAction("toggle-hide-empty-configuration", roomNode);

        Assert.True(handledShow);
        Assert.False(roomModel.HideEmptyConfiguration);
        Assert.Contains(roomNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is RoomTraversalLegsNodeViewModel);
        Assert.Contains(roomNode.Children, child => child is RoomGameObjectsNodeViewModel);

        var postShowAction = viewModel.GetTreeContextActions(roomNode)
            .Single(action => action.ActionId == "toggle-hide-empty-configuration");
        Assert.Equal("Hide Empty Configuration", postShowAction.Header);

        var handledHide = viewModel.ExecuteTreeContextAction("toggle-hide-empty-configuration", roomNode);

        Assert.True(handledHide);
        Assert.True(roomModel.HideEmptyConfiguration);
        Assert.DoesNotContain(roomNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.DoesNotContain(roomNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
    }

    [Fact]
    public void ExecuteTreeContextAction_GlobalHideShow_RecursesAcrossSupportedNodes()
    {
        var project = BuildProject();
        var planet = project.Planets[0];
        var country = planet.Countries[0];
        var area = country.Areas[0];
        var room = area.Rooms[0];

        planet.GameObjects.Add(new GameObject
        {
            Name = "PlanetObject",
            ContainedObjects = [new GameObject { Name = "PlanetNested" }]
        });

        country.BaseObjects.Add(new GameObject
        {
            Name = "CountryBase",
            ContainedObjects = [new GameObject { Name = "CountryBaseNested" }]
        });

        area.BaseObjects.Add(new GameObject
        {
            Name = "AreaBase",
            ContainedObjects = [new GameObject { Name = "AreaBaseNested" }]
        });

        room.GameObjects.Add(new GameObject
        {
            Name = "RoomObject",
            ContainedObjects = [new GameObject { Name = "RoomNested" }]
        });

        project.GameObjects.Add(new GameObject
        {
            Name = "GlobalObject",
            ContainedObjects = [new GameObject { Name = "GlobalNested" }]
        });

        project.ObjectTemplates.Add(new GameObject
        {
            Name = "TemplateObject",
            ContainedObjects = [new GameObject { Name = "TemplateNested" }]
        });

        project.BaseObjects.Add(new GameObject
        {
            Name = "ProjectBaseObject",
            ContainedObjects = [new GameObject { Name = "ProjectBaseNested" }]
        });

        var templateRoom = project.RoomTemplates[0];
        templateRoom.GameObjects.Add(new GameObject
        {
            Name = "TemplateRoomObject",
            ContainedObjects = [new GameObject { Name = "TemplateRoomNested" }]
        });

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);

        Assert.True(viewModel.ExecuteTreeContextAction("hide-empty-configuration-global", rootNode));
        Assert.True(AllHideEmptyConfigurationValues(project, expected: true));

        Assert.True(viewModel.ExecuteTreeContextAction("show-empty-configuration-global", rootNode));
        Assert.True(AllHideEmptyConfigurationValues(project, expected: false));
    }

    [Fact]
    public void GlobalHideShowCommands_ApplyRecursively()
    {
        var project = BuildProject();
        project.Planets[0].Countries[0].Areas[0].Rooms[0].GameObjects.Add(new GameObject
        {
            Name = "RoomObject",
            ContainedObjects = [new GameObject { Name = "Nested" }]
        });

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());

        viewModel.HideEmptyConfigurationGlobalCommand.Execute(null);
        Assert.True(AllHideEmptyConfigurationValues(project, expected: true));

        viewModel.ShowEmptyConfigurationGlobalCommand.Execute(null);
        Assert.True(AllHideEmptyConfigurationValues(project, expected: false));
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewRoom_FromTemplate_DoesNotInheritHideEmptyConfiguration()
    {
        var templateRoom = new Room
        {
            Name = "Hidden Template",
            HideEmptyConfiguration = true,
            GameObjects =
            [
                new GameObject
                {
                    Name = "TemplateObject",
                    HideEmptyConfiguration = true,
                    ContainedObjects = [new GameObject { Name = "Nested", HideEmptyConfiguration = true }]
                }
            ]
        };

        var project = BuildProject();
        project.RoomTemplates.Clear();
        project.RoomTemplates.Add(templateRoom);

        var treeContext = new TreeContextInteractionServiceStub
        {
            RoomTemplateSelectionHandler = templates => (true, templates.Single()),
            EditRoomSettingsHandler = initial => initial with { Name = "Created Room" }
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        Assert.True(viewModel.ExecuteTreeContextAction("hide-empty-configuration-global", rootNode));

        var hierarchy = BuildHierarchy(project);
        var areaNode = EnumerateNodes(hierarchy).OfType<AreaNodeViewModel>().Single();

        Assert.True(viewModel.ExecuteTreeContextAction("add-new-room", areaNode));

        var created = project.Planets[0].Countries[0].Areas[0].Rooms.Single(room => room.Name == "Created Room");
        Assert.False(created.HideEmptyConfiguration);
        Assert.False(created.GameObjects.Single().HideEmptyConfiguration);
        Assert.False(created.GameObjects.Single().ContainedObjects.Single().HideEmptyConfiguration);
    }

    [Fact]
    public void ExecuteTreeContextAction_AddNewObject_FromTemplate_DoesNotInheritHideEmptyConfiguration()
    {
        var project = BuildProject();
        project.ObjectTemplates.Clear();
        project.ObjectTemplates.Add(new GameObject
        {
            Name = "Hidden Object Template",
            HideEmptyConfiguration = true,
            ContainedObjects = [new GameObject { Name = "Nested Template", HideEmptyConfiguration = true }]
        });

        var treeContext = new TreeContextInteractionServiceStub
        {
            ObjectTemplateSelectionHandler = templates => (true, templates.Single()),
            EditObjectBasicPropertiesHandler = initial => initial
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        Assert.True(viewModel.ExecuteTreeContextAction("hide-empty-configuration-global", rootNode));

        var hierarchy = BuildHierarchy(project);
        var roomNode = EnumerateNodes(hierarchy).OfType<RoomNodeViewModel>().Single();

        Assert.True(viewModel.ExecuteTreeContextAction("add-new-object", roomNode));

        var created = project.Planets[0].Countries[0].Areas[0].Rooms[0].GameObjects
            .Single(obj => !string.Equals(obj.Name, "Lantern", StringComparison.Ordinal));

        Assert.False(created.HideEmptyConfiguration);
        Assert.False(created.ContainedObjects.Single().HideEmptyConfiguration);
    }

    [Fact]
    public void ExecuteTreeContextAction_GlobalHide_ThenLocalShow_RestoresAreaDiscoverability()
    {
        var project = BuildProject();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);

        Assert.True(viewModel.ExecuteTreeContextAction("hide-empty-configuration-global", rootNode));

        var hiddenHierarchy = BuildHierarchy(project);
        var hiddenAreaNode = EnumerateNodes(hiddenHierarchy).OfType<AreaNodeViewModel>().Single();

        Assert.True(hiddenAreaNode.Area.HideEmptyConfiguration);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.DoesNotContain(hiddenAreaNode.Children, child => child is AreaGameObjectsNodeViewModel);

        Assert.True(viewModel.ExecuteTreeContextAction("toggle-hide-empty-configuration", hiddenAreaNode));

        Assert.False(hiddenAreaNode.Area.HideEmptyConfiguration);
        Assert.Contains(hiddenAreaNode.Children, child => child is GamePropertiesContainerNodeViewModel);
        Assert.Contains(hiddenAreaNode.Children, child => child is ScopedActionsNodeViewModel);
        Assert.Contains(hiddenAreaNode.Children, child => child is ScopedVerbsNodeViewModel);
        Assert.Contains(hiddenAreaNode.Children, child => child is ScopedDirectionalsNodeViewModel);
        Assert.Contains(hiddenAreaNode.Children, child => child is ScopedEventSubscriptionsNodeViewModel);
        Assert.Contains(hiddenAreaNode.Children, child => child is ScopedTimerDefinitionsNodeViewModel);
        Assert.Contains(hiddenAreaNode.Children, child => child is AreaGameObjectsNodeViewModel);
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

    private static ProjectModel BuildProject()
    {
        var room = new Room
        {
            Name = "Room A",
            GameObjects = [new GameObject { Name = "Lantern" }]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area],
            StartingAreaName = "Area A"
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country],
            StartingCountryName = "Country A"
        };

        return new ProjectModel
        {
            Name = "Hide Empty Context Actions",
            Planets = [planet],
            RoomTemplates = [new Room { Name = "Template Room" }]
        };
    }

    private static bool AllHideEmptyConfigurationValues(ProjectModel project, bool expected)
    {
        if (project.HideEmptyConfiguration != expected)
        {
            return false;
        }

        if (project.RoomTemplates.Any(room => room.HideEmptyConfiguration != expected))
        {
            return false;
        }

        if (!AllObjectsMatch(project.GameObjects, expected)
            || !AllObjectsMatch(project.ObjectTemplates, expected)
            || !AllObjectsMatch(project.BaseObjects, expected))
        {
            return false;
        }

        foreach (var planet in project.Planets)
        {
            if (planet.HideEmptyConfiguration != expected)
            {
                return false;
            }

            if (!AllObjectsMatch(planet.GameObjects, expected) || !AllObjectsMatch(planet.BaseObjects, expected))
            {
                return false;
            }

            foreach (var country in planet.Countries)
            {
                if (country.HideEmptyConfiguration != expected)
                {
                    return false;
                }

                if (!AllObjectsMatch(country.GameObjects, expected) || !AllObjectsMatch(country.BaseObjects, expected))
                {
                    return false;
                }

                foreach (var area in country.Areas)
                {
                    if (area.HideEmptyConfiguration != expected)
                    {
                        return false;
                    }

                    if (!AllObjectsMatch(area.GameObjects, expected) || !AllObjectsMatch(area.BaseObjects, expected))
                    {
                        return false;
                    }

                    foreach (var room in area.Rooms)
                    {
                        if (room.HideEmptyConfiguration != expected || !AllObjectsMatch(room.GameObjects, expected))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private static bool AllObjectsMatch(IEnumerable<GameObject> objects, bool expected)
    {
        foreach (var gameObject in objects)
        {
            if (gameObject.HideEmptyConfiguration != expected)
            {
                return false;
            }

            if (!AllObjectsMatch(gameObject.ContainedObjects, expected))
            {
                return false;
            }
        }

        return true;
    }
}
