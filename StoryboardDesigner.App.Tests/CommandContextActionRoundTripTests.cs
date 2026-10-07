using System.Text.Json;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Rules.Actions;

namespace StoryboardDesigner.App.Tests;

public sealed class CommandContextActionRoundTripTests
{
    [Fact]
    public void CommandContextPayloadValidator_AcceptsAllSupportedTypedFieldValuePairs()
    {
        AssertValid(CommandContextField.Direction, null, Direction10.Up);
        AssertValid(CommandContextField.Direction, null, Direction10.Down);
        AssertValid(CommandContextField.DirectionSpecified, true, null);
        AssertValid(CommandContextField.DirectionSpecified, false, null);
        AssertValid(CommandContextField.DestinationRoomQualifierResolved, true, null);
        AssertValid(CommandContextField.DestinationRoomQualifierResolved, false, null);

        AssertInvalid(CommandContextField.Direction, true, Direction10.Up);
        AssertInvalid(CommandContextField.DirectionSpecified, null, null);
        AssertInvalid(CommandContextField.DirectionSpecified, false, Direction10.Up);
        AssertInvalid(CommandContextField.DestinationRoomQualifierResolved, true, Direction10.Down);
        AssertInvalid((CommandContextField)int.MaxValue, true, null);
        AssertInvalid(CommandContextField.Direction, null, (Direction10)int.MaxValue);
    }

    [Fact]
    public void NewCommandContextActions_SurviveSaveLoadAndRuntimeExport()
    {
        var actions = new List<CommandAction>
        {
            CreateContextAction("Direction Up", CommandContextField.Direction, null, Direction10.Up),
            CreateContextAction("Direction Down", CommandContextField.Direction, null, Direction10.Down),
            CreateContextAction("Direction Specified True", CommandContextField.DirectionSpecified, true, null),
            CreateContextAction("Direction Specified False", CommandContextField.DirectionSpecified, false, null),
            CreateContextAction("Destination Resolved True", CommandContextField.DestinationRoomQualifierResolved, true, null),
            CreateContextAction("Destination Resolved False", CommandContextField.DestinationRoomQualifierResolved, false, null),
            new()
            {
                Name = "Direction Preflight",
                ActionType = CommandActionType.NavigateDirectionPreFlightCheck,
                NoVerbLinkage = true
            }
        };
        var project = new ProjectModel
        {
            Name = "Command Context Action Round Trip",
            GlobalObjectAvailableActions = actions
        };
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var projectFilePath = Path.Combine(tempRoot, "CommandContextActions.sbe.json");

        try
        {
            service.SaveProjectModel(projectFilePath, project);
            var loaded = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectFilePath));
            var loadedActions = loaded.GlobalObjectAvailableActions.ToDictionary(static action => action.Name);
            Assert.Equal(actions.Count, loadedActions.Count);

            AssertPayload(loadedActions["Direction Up"], CommandContextField.Direction, null, Direction10.Up);
            AssertPayload(loadedActions["Direction Down"], CommandContextField.Direction, null, Direction10.Down);
            AssertPayload(loadedActions["Direction Specified True"], CommandContextField.DirectionSpecified, true, null);
            AssertPayload(loadedActions["Direction Specified False"], CommandContextField.DirectionSpecified, false, null);
            AssertPayload(loadedActions["Destination Resolved True"], CommandContextField.DestinationRoomQualifierResolved, true, null);
            AssertPayload(loadedActions["Destination Resolved False"], CommandContextField.DestinationRoomQualifierResolved, false, null);
            Assert.Equal(CommandActionType.NavigateDirectionPreFlightCheck, loadedActions["Direction Preflight"].ActionType);
            Assert.IsType<NavigateDirectionPreFlightCheckPayload>(loadedActions["Direction Preflight"].Payload);

            var runtimeProjectPath = service.ExportCleanProjectV1(projectFilePath, loaded);
            using var runtimeDocument = JsonDocument.Parse(File.ReadAllText(runtimeProjectPath));
            var runtimeActions = runtimeDocument.RootElement.GetProperty("availableGameActions")
                .EnumerateArray()
                .ToDictionary(action => action.GetProperty("name").GetString()!, StringComparer.Ordinal);

            AssertRuntimePayload(runtimeActions["Direction Up"], "Direction", "expectedDirection", "Up");
            AssertRuntimePayload(runtimeActions["Direction Down"], "Direction", "expectedDirection", "Down");
            AssertRuntimePayload(runtimeActions["Direction Specified True"], "DirectionSpecified", "expectedBoolean", true);
            AssertRuntimePayload(runtimeActions["Direction Specified False"], "DirectionSpecified", "expectedBoolean", false);
            AssertRuntimePayload(runtimeActions["Destination Resolved True"], "DestinationRoomQualifierResolved", "expectedBoolean", true);
            AssertRuntimePayload(runtimeActions["Destination Resolved False"], "DestinationRoomQualifierResolved", "expectedBoolean", false);
            Assert.Equal("NavigateDirectionPreFlightCheck", runtimeActions["Direction Preflight"].GetProperty("actionType").GetString());
            Assert.NotEqual(JsonValueKind.Null, runtimeActions["Direction Preflight"].GetProperty("payload").ValueKind);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static CommandAction CreateContextAction(
        string name,
        CommandContextField field,
        bool? expectedBoolean,
        Direction10? expectedDirection)
    {
        return new CommandAction
        {
            Name = name,
            ActionType = CommandActionType.CheckCommandContext,
            NoVerbLinkage = true,
            Payload = new CheckCommandContextPayload(field, expectedBoolean, expectedDirection)
        };
    }

    private static void AssertPayload(
        CommandAction action,
        CommandContextField field,
        bool? expectedBoolean,
        Direction10? expectedDirection)
    {
        Assert.Equal(CommandActionType.CheckCommandContext, action.ActionType);
        var payload = Assert.IsType<CheckCommandContextPayload>(action.Payload);
        Assert.Equal(field, payload.Field);
        Assert.Equal(expectedBoolean, payload.ExpectedBoolean);
        Assert.Equal(expectedDirection, payload.ExpectedDirection);
    }

    private static void AssertRuntimePayload(JsonElement action, string field, string valueProperty, string expected)
    {
        var payload = action.GetProperty("payload");
        Assert.Equal(field, payload.GetProperty("field").GetString());
        Assert.Equal(expected, payload.GetProperty(valueProperty).GetString());
        Assert.False(payload.TryGetProperty(valueProperty == "expectedBoolean" ? "expectedDirection" : "expectedBoolean", out _));
    }

    private static void AssertRuntimePayload(JsonElement action, string field, string valueProperty, bool expected)
    {
        var payload = action.GetProperty("payload");
        Assert.Equal(field, payload.GetProperty("field").GetString());
        Assert.Equal(expected, payload.GetProperty(valueProperty).GetBoolean());
        Assert.False(payload.TryGetProperty(valueProperty == "expectedBoolean" ? "expectedDirection" : "expectedBoolean", out _));
    }

    private static void AssertValid(CommandContextField field, bool? expectedBoolean, Direction10? expectedDirection)
    {
        Assert.True(CommandContextPayloadValidator.TryValidate(field, expectedBoolean, expectedDirection, out var message), message);
    }

    private static void AssertInvalid(CommandContextField field, bool? expectedBoolean, Direction10? expectedDirection)
    {
        Assert.False(CommandContextPayloadValidator.TryValidate(field, expectedBoolean, expectedDirection, out _));
    }
}
