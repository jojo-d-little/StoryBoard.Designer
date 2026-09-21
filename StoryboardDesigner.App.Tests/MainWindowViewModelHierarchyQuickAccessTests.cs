using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views.Controls;

namespace StoryboardDesigner.App.Tests;

public class MainWindowViewModelHierarchyQuickAccessTests
{
    [Fact]
    public void QuickAccessPane_MaterializesReadOnlyBookmarkDisplayBindings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var item = new HierarchyQuickAccessItemViewModel(
                    "project:global",
                    "Project",
                    "Global",
                    "Project",
                    isAvailable: true);
                var pane = new QuickAccessPane
                {
                    DataContext = new
                    {
                        RecentHierarchyNodes = new[] { item },
                        HierarchyBookmarks = new[] { item }
                    }
                };

                pane.Measure(new System.Windows.Size(320, 480));
                pane.Arrange(new System.Windows.Rect(0, 0, 320, 480));
                pane.UpdateLayout();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void SelectingHierarchyNodes_TracksTenDistinctMostRecentNodes()
    {
        var project = new ProjectModel { Name = "Quick Access" };
        var vm = CreateViewModel(project);
        var root = new ProjectRootNodeViewModel(project);
        vm.HierarchyRoots.Add(root);

        var planetNodes = Enumerable.Range(1, 11)
            .Select(index => new PlanetNodeViewModel(
                project,
                new Planet { Name = $"Planet {index}" },
                root))
            .ToList();
        foreach (var planetNode in planetNodes)
        {
            root.Children.Add(planetNode);
            vm.SelectedNode = planetNode;
        }

        Assert.Equal(10, vm.RecentHierarchyNodes.Count);
        Assert.Equal("Planet 11", vm.RecentHierarchyNodes[0].DisplayName);
        Assert.DoesNotContain(vm.RecentHierarchyNodes, item => item.DisplayName == "Planet 1");

        vm.SelectedNode = planetNodes[5];

        Assert.Equal(10, vm.RecentHierarchyNodes.Count);
        Assert.Equal("Planet 6", vm.RecentHierarchyNodes[0].DisplayName);
        Assert.Single(vm.RecentHierarchyNodes, item => item.DisplayName == "Planet 6");
    }

    [Fact]
    public void BookmarkCommands_AddNavigateReorderAndRemoveWithinPanelState()
    {
        var project = new ProjectModel { Name = "Quick Access" };
        var vm = CreateViewModel(project);
        var root = new ProjectRootNodeViewModel(project);
        var firstPlanet = new PlanetNodeViewModel(project, new Planet { Name = "First" }, root);
        var secondPlanet = new PlanetNodeViewModel(project, new Planet { Name = "Second" }, root);
        root.Children.Add(firstPlanet);
        root.Children.Add(secondPlanet);
        vm.HierarchyRoots.Add(root);

        vm.SelectedNode = firstPlanet;
        vm.AddHierarchyBookmarkCommand.Execute(null);
        vm.SelectedNode = secondPlanet;
        vm.AddHierarchyBookmarkCommand.Execute(null);

        Assert.Equal(["First", "Second"], vm.HierarchyBookmarks.Select(item => item.DisplayName));

        firstPlanet.EditableName = "First Renamed";
        Assert.Equal("First Renamed", vm.HierarchyBookmarks[0].DisplayName);
        Assert.Equal("First Renamed", project.UiState.HierarchyBookmarks[0].DisplayName);

        vm.MoveHierarchyBookmarkUpCommand.Execute(vm.HierarchyBookmarks[1]);
        Assert.Equal(["Second", "First Renamed"], vm.HierarchyBookmarks.Select(item => item.DisplayName));

        root.IsExpanded = false;
        vm.SelectedNode = root;
        vm.NavigateToHierarchyQuickAccessItemCommand.Execute(vm.HierarchyBookmarks[0]);

        Assert.Same(secondPlanet, vm.SelectedNode);
        Assert.True(root.IsExpanded);

        vm.RemoveHierarchyBookmarkCommand.Execute(vm.HierarchyBookmarks[0]);
        Assert.Equal("First Renamed", Assert.Single(vm.HierarchyBookmarks).DisplayName);
        Assert.Equal("First Renamed", Assert.Single(project.UiState.HierarchyBookmarks).DisplayName);
    }

    [Fact]
    public void QuickAccessSplitRatio_IsClampedAndStoredInProjectUiState()
    {
        var project = new ProjectModel { Name = "Quick Access" };
        var vm = CreateViewModel(project);

        vm.QuickAccessRecentSectionRatio = 0.7;
        Assert.Equal(0.7, project.UiState.QuickAccessRecentSectionRatio);

        vm.QuickAccessRecentSectionRatio = 1;
        Assert.Equal(0.9, project.UiState.QuickAccessRecentSectionRatio);
    }

    [Fact]
    public void CompletedSplitterDrag_UsesPreviewMovementInsteadOfStaleActualHeight()
    {
        var ratio = QuickAccessPane.CalculateCompletedSplitRatio(
            recentHeightAtDragStart: 200,
            bookmarksHeightAtDragStart: 200,
            verticalChange: 100,
            minimumRecentHeight: 72,
            minimumBookmarksHeight: 72);

        Assert.Equal(0.75, ratio);
    }

    private static MainWindowViewModel CreateViewModel(ProjectModel project)
    {
        return MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());
    }
}
