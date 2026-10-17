using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class ClearRoomObjectSelectionsScopeChoicesTests
{
    [Fact]
    public void ClearActions_OfferOnlyTheirSupportedScopes_AndKeepAllAsAuthoringDefault()
    {
        Assert.Equal(
            new[]
            {
                ClearRoomObjectSelectionsScope.All,
                ClearRoomObjectSelectionsScope.PrimaryOnly,
                ClearRoomObjectSelectionsScope.SecondaryOnly
            },
            ClearRoomObjectSelectionsScopeChoices.ClearActiveRoomObjects);
        Assert.Equal(
            new[]
            {
                ClearRoomObjectSelectionsScope.All,
                ClearRoomObjectSelectionsScope.LastSelected,
                ClearRoomObjectSelectionsScope.PrimaryOnly,
                ClearRoomObjectSelectionsScope.SecondaryOnly
            },
            ClearRoomObjectSelectionsScopeChoices.ClearRoomObjectSelections);

        var clearActive = new CommandAction { ActionType = CommandActionType.ClearActiveRoomObjects };
        var clearSelections = new CommandAction { ActionType = CommandActionType.ClearRoomObjectSelections };
        Assert.Equal(ClearRoomObjectSelectionsScope.All, clearActive.ClearActiveRoomObjectsScope);
        Assert.Equal(ClearRoomObjectSelectionsScope.All, ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(clearSelections).ClearScope);
    }

    [Fact]
    public void ClearActiveRoomObjects_DoesNotExportLastSelected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var action = new CommandAction
            {
                Name = "ClearActive",
                ActionType = CommandActionType.ClearActiveRoomObjects,
                ClearActiveRoomObjectsScope = ClearRoomObjectSelectionsScope.LastSelected,
                Payload = new ClearActiveRoomObjectsPayload(ClearRoomObjectSelectionsScope.LastSelected)
            };
            var project = new ProjectModel
            {
                Name = "UnsupportedClearActiveScope",
                GlobalObjectAvailableActions = [action]
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new JsonExportService().ExportCleanProjectV1(
                    Path.Combine(tempRoot, "UnsupportedClearActiveScope.sbe.json"),
                    project));

            Assert.Contains("does not support scope 'LastSelected'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }
}
