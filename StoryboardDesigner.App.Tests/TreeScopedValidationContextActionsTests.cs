using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class TreeScopedValidationContextActionsTests
{
    private const string RunValidationActionId = "run-validation";
    private const string ValidateNodeOnlyActionId = "validate-node-only";
    private const string ValidateFromHereActionId = "validate-from-here";
    private const string ValidateFromHereStopOnFirstBlockingActionId = "validate-from-here-stop-on-first-blocking";
    private const string ValidateWholeProjectActionId = "validate-whole-project";

    [Fact]
    public void GetTreeContextActions_IncludesScopedValidationActions()
    {
        var project = BuildProjectWithGlobalAndRoomIssues();
        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), projectUi);

        var rootNode = new ProjectRootNodeViewModel(project);

        var actions = viewModel.GetTreeContextActions(rootNode);

        Assert.Contains(actions, action => action.ActionId == RunValidationActionId);
        Assert.DoesNotContain(actions, action => action.ActionId == ValidateNodeOnlyActionId);
        Assert.DoesNotContain(actions, action => action.ActionId == ValidateFromHereActionId);
        Assert.DoesNotContain(actions, action => action.ActionId == ValidateFromHereStopOnFirstBlockingActionId);
        Assert.DoesNotContain(actions, action => action.ActionId == ValidateWholeProjectActionId);
    }

    [Fact]
    public void GetTreeContextActions_ProjectRoot_IncludesEditScopedActions()
    {
        var project = BuildProjectWithGlobalAndRoomIssues();
        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), projectUi);

        var rootNode = new ProjectRootNodeViewModel(project);

        var actions = viewModel.GetTreeContextActions(rootNode);

        Assert.Contains(actions, action => string.Equals(action.ActionId, "edit-scoped-actions", StringComparison.Ordinal));
        Assert.Contains(actions, action => string.Equals(action.ActionId, "edit-scoped-timer-definitions", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_ValidateFromHereStopOnFirstBlocking_TruncatesReportAndPersistsCompletionMode()
    {
        var project = BuildProjectWithGlobalAndRoomIssues();
        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), projectUi);

        var roomNode = BuildRoomNode(project);

        var handled = viewModel.ExecuteTreeContextAction(ValidateFromHereStopOnFirstBlockingActionId, roomNode);

        Assert.True(handled);
        Assert.Single(projectUi.LastValidationIssues);
        Assert.Equal("StopOnFirstBlocking", project.UiState.LastTreeValidationCompletionMode);
        Assert.Equal(ValidateFromHereStopOnFirstBlockingActionId, project.UiState.LastTreeValidationActionId);
    }

    [Fact]
    public void ExecuteTreeContextAction_ValidateNodeOnly_FiltersToNodeScope()
    {
        var project = BuildProjectWithGlobalAndRoomIssues();
        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), projectUi);

        var roomNode = BuildRoomNode(project);

        var handled = viewModel.ExecuteTreeContextAction(ValidateNodeOnlyActionId, roomNode);

        Assert.True(handled);
        Assert.Single(projectUi.LastValidationIssues);
        var issue = projectUi.LastValidationIssues[0];
        Assert.Equal("NAME-001", issue.RuleId);
        Assert.Contains("Planet A / Country A / Area A / Room A", issue.Path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(projectUi.LastValidationIssues, candidate =>
            string.Equals(candidate.RuleId, "PROJ-004", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecuteTreeContextAction_ValidateWholeProject_IncludesGlobalAndRoomIssues()
    {
        var project = BuildProjectWithGlobalAndRoomIssues();
        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), projectUi);

        var roomNode = BuildRoomNode(project);

        var handled = viewModel.ExecuteTreeContextAction(ValidateWholeProjectActionId, roomNode);

        Assert.True(handled);
        Assert.Contains(projectUi.LastValidationIssues, issue =>
            string.Equals(issue.RuleId, "PROJ-004", StringComparison.Ordinal)
            && issue.Description.Contains("No game object is marked as player", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(projectUi.LastValidationIssues, issue => issue.Path.Contains("Planet A / Country A / Area A / Room A", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectModel BuildProjectWithGlobalAndRoomIssues()
    {
        var room = new Room
        {
            Name = "Room A",
            Commands = ["look door", "open door"],
            GameObjects =
            [
                new GameObject
                {
                    Name = "RoomDuplicate",
                    ProducerNotes = string.Empty
                },
                new GameObject
                {
                    Name = "RoomDuplicate",
                    ProducerNotes = string.Empty
                }
            ]
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

        var project = new ProjectModel
        {
            Name = "Scoped Validation",
            StartingPlanetName = "Planet A",
            Planets = [planet],
            GameObjects =
            [
                new GameObject
                {
                    Name = "GlobalMissing",
                    ProducerNotes = ""
                }
            ]
        };

        ScopeHierarchy.AttachParents(project);
        return project;
    }

    private static RoomNodeViewModel BuildRoomNode(ProjectModel project)
    {
        var planet = project.Planets[0];
        var country = planet.Countries[0];
        var area = country.Areas[0];
        var room = area.Rooms[0];

        var projectNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, planet, projectNode);
        var countryNode = new CountryNodeViewModel(country, planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        return new RoomNodeViewModel(room, area, areaNode);
    }
}
