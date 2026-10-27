using System.Text.Json;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;

namespace StoryboardDesigner.App.Tests;

public sealed class SetPhaseActionAuthoringTests
{
    [Fact]
    public void SetPhasePayload_DefaultsClonesAndSwitchesThroughTypedPayloadLifecycle()
    {
        var action = new CommandAction { ActionType = CommandActionType.SetPhase };

        Assert.IsType<SetPhasePayload>(action.CreatePayloadSnapshot());
        ActionPayloadAccessors.SetSetPhaseKey(action, "winter-day");

        var clonedPayload = Assert.IsType<SetPhasePayload>(action.CreatePayloadSnapshot());
        var clone = new CommandAction { ActionType = CommandActionType.SetPhase };
        Assert.True(clone.TryApplyPayload(clonedPayload));
        Assert.Equal("winter-day", ActionPayloadAccessors.GetSetPhaseKey(clone));
        Assert.Contains("winter-day", CommandActionSummaryPresenter.BuildSummary(action), StringComparison.Ordinal);

        action.ActionType = CommandActionType.EchoMessage;
        Assert.IsType<EchoPayload>(action.CreatePayloadSnapshot());
        action.ActionType = CommandActionType.SetPhase;
        Assert.Equal(string.Empty, ActionPayloadAccessors.GetSetPhaseKey(action));
    }

    [Fact]
    public void PhasePageChoices_ShowHierarchyAndUseUniquePageKeys()
    {
        var project = BuildProject("winter-day");

        var choice = Assert.Single(PhasePageTargetChoiceService.BuildChoices(project));

        Assert.Equal("winter-day", choice.PhaseKey);
        Assert.True(choice.IsKeyUnique);
        Assert.Contains("Winter › Chapter 1 › Day", choice.DisplayName, StringComparison.Ordinal);
        Assert.Equal(1, PhasePageTargetChoiceService.CountMatches(project, "winter-day"));

        var action = new CommandAction { ActionType = CommandActionType.SetPhase };
        ActionPayloadAccessors.SetSetPhaseKey(action, choice.PhaseKey);
        Assert.Equal("winter-day", ActionPayloadAccessors.GetSetPhaseKey(action));
    }

    [Fact]
    public void PhasePageChoices_MarkDuplicateKeysAsAmbiguous()
    {
        var project = BuildProject("winter-day");
        var secondPage = new PhaseNode { Tier = PhaseTier.Page, DisplayName = "Night", PhaseKey = "winter-day" };
        project.PhaseBooks[0].Children[0].Children.Add(secondPage);

        var choices = PhasePageTargetChoiceService.BuildChoices(project);

        Assert.Equal(2, choices.Count);
        Assert.All(choices, choice => Assert.False(choice.IsKeyUnique));
        Assert.Equal(2, PhasePageTargetChoiceService.CountMatches(project, "winter-day"));
    }

    [Theory]
    [InlineData("deleted-page", "unresolved")]
    [InlineData("winter-day", "ambiguous")]
    public void EditorChoices_PreserveAndIdentifyInvalidStoredKeys(string storedKey, string expectedLabel)
    {
        var project = BuildProject("winter-day");
        if (expectedLabel == "ambiguous")
        {
            project.PhaseBooks[0].Children[0].Children.Add(new PhaseNode
            {
                Tier = PhaseTier.Page,
                DisplayName = "Duplicate",
                PhaseKey = "winter-day"
            });
        }

        var editorChoices = PhasePageTargetChoiceService.BuildEditorChoices(project, storedKey);
        var storedChoice = Assert.IsType<PhasePageTargetChoice>(editorChoices[0]);

        Assert.True(storedChoice.IsStoredInvalid);
        Assert.Null(storedChoice.Page);
        Assert.Equal(storedKey, storedChoice.PhaseKey);
        Assert.Contains(expectedLabel, storedChoice.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(editorChoices, choice => !choice.IsStoredInvalid && choice.Page is not null);
    }

    [Fact]
    public void SaveLoadAndCleanExport_PreserveOneDirectSetPhaseActionAndExactKey()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var page = new PhaseNode { Tier = PhaseTier.Page, DisplayName = "Day", PhaseKey = "winter-day" };
            var chapter = new PhaseNode { Tier = PhaseTier.Chapter, DisplayName = "Chapter 1", PhaseKey = "winter.chapter1", Children = { page } };
            var book = new PhaseNode { Tier = PhaseTier.Book, DisplayName = "Winter", PhaseKey = "winter", Children = { chapter } };
            var action = new CommandAction
            {
                Name = "Set Winter Day",
                ActionType = CommandActionType.SetPhase,
                Payload = new SetPhasePayload("winter-day")
            };
            var room = new Room { Name = "Room", AvailableActions = [action] };
            var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
            var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
            var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };
            var project = new ProjectModel
            {
                Name = "SetPhasePersistence",
                Planets = [planet],
                PhaseBooks = [book],
                StartingPhasePageId = page.Id
            };
            ScopeHierarchy.AttachParents(project);
            var projectFilePath = Path.Combine(tempRoot, "SetPhasePersistence.sbe.json");

            service.SaveProjectModel(projectFilePath, project);

            var roomPath = Path.Combine(tempRoot, "Room", $"{room.Id:N}.room.json");
            using (var authoredDoc = JsonDocument.Parse(File.ReadAllText(roomPath)))
            {
                var authoredActions = authoredDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
                var authoredAction = Assert.Single(authoredActions);
                Assert.Equal("SetPhase", authoredAction.GetProperty("actionType").GetString());
                Assert.Equal("setPhase", authoredAction.GetProperty("payload").GetProperty("$payloadType").GetString());
                Assert.Equal("winter-day", authoredAction.GetProperty("payload").GetProperty("phaseKey").GetString());
            }

            var loaded = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectFilePath));
            var loadedAction = Assert.Single(loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single().AvailableActions);
            Assert.Equal("winter-day", ActionPayloadAccessors.GetSetPhaseKey(loadedAction));

            service.ExportCleanProjectV1(projectFilePath, loaded);
            var runtimeRoomPath = Path.Combine(tempRoot, "GameRuntimeJson", "Room", $"{room.Id:D}".ToUpperInvariant() + ".runtime.json");
            using var runtimeDoc = JsonDocument.Parse(File.ReadAllText(runtimeRoomPath));
            var runtimeActions = runtimeDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
            var runtimeAction = Assert.Single(runtimeActions);
            Assert.Equal("SetPhase", runtimeAction.GetProperty("actionType").GetString());
            Assert.Equal("setPhase", runtimeAction.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal("winter-day", runtimeAction.GetProperty("payload").GetProperty("phaseKey").GetString());
            var runtimePayload = JsonSerializer.Deserialize<RuntimeCommandActionPayloadBaseDto>(
                runtimeAction.GetProperty("payload").GetRawText(),
                JsonExportService.CreateJsonOptions());
            Assert.Equal("winter-day", Assert.IsType<RuntimeSetPhaseActionPayloadDto>(runtimePayload).PhaseKey);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-page")]
    [InlineData("ambiguous-page")]
    public void Validation_ReportsMissingUnresolvedAndAmbiguousTargets(string phaseKey)
    {
        var project = BuildProject("winter-day");
        var action = new CommandAction
        {
            Name = "Set phase",
            ActionType = CommandActionType.SetPhase,
            Payload = new SetPhasePayload(phaseKey)
        };
        project.Planets[0].Countries[0].Areas[0].Rooms[0].AvailableActions.Add(action);
        if (phaseKey == "ambiguous-page")
        {
            project.PhaseBooks[0].Children[0].Children.Add(new PhaseNode
            {
                Tier = PhaseTier.Page,
                DisplayName = "Duplicate",
                PhaseKey = "ambiguous-page"
            });
            project.PhaseBooks[0].Children[0].Children[0].PhaseKey = "ambiguous-page";
        }

        var registry = new ValidationRuleRegistry();
        registry.Register(new SetPhaseTargetExistsRule());
        var result = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("ACT-023", issue.RuleId);
        Assert.Contains(phaseKey.Length == 0 ? "Choose" : phaseKey == "ambiguous-page" ? "ambiguous" : "does not resolve", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectModel BuildProject(string pageKey)
    {
        var page = new PhaseNode { Tier = PhaseTier.Page, DisplayName = "Day", PhaseKey = pageKey };
        var chapter = new PhaseNode { Tier = PhaseTier.Chapter, DisplayName = "Chapter 1", PhaseKey = "winter.chapter1", Children = { page } };
        var book = new PhaseNode { Tier = PhaseTier.Book, DisplayName = "Winter", PhaseKey = "winter", Children = { chapter } };
        var room = new Room { Name = "Room" };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "SetPhaseTests", Planets = [planet], PhaseBooks = [book] };
        ScopeHierarchy.AttachParents(project);
        return project;
    }
}
