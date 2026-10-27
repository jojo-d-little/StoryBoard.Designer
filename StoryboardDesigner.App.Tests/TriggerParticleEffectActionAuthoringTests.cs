using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class TriggerParticleEffectActionAuthoringTests
{
    [Fact]
    public void RealContractJson_ImportsTypedPayloadAndPreservesObjectId()
    {
        var objectId = Guid.NewGuid();
        var json = "{\"id\":\"" + Guid.NewGuid().ToString("D") + "\",\"actionType\":\"TriggerParticleEffect\",\"name\":\"Burst\",\"payload\":{\"$payloadType\":\"triggerParticleEffect\",\"objectId\":\"" + objectId.ToString("D") + "\"}}";

        var dto = Assert.IsType<CommandActionDto>(JsonSerializer.Deserialize<CommandActionDto>(json, JsonExportService.CreateJsonOptions()));
        var action = JsonExportService.ToCommandActionModel(dto);

        Assert.Equal(CommandActionType.TriggerParticleEffect, action.ActionType);
        Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(action));
        Assert.Equal(objectId, Assert.IsType<TriggerParticleEffectPayload>(action.Payload).ObjectId);
    }

    [Fact]
    public void PayloadLifecycle_ClonesRemapsAndSwitchesWithTypedDefaults()
    {
        var objectId = Guid.NewGuid();
        var remappedObjectId = Guid.NewGuid();
        var action = new CommandAction { ActionType = CommandActionType.TriggerParticleEffect };
        Assert.IsType<TriggerParticleEffectPayload>(action.CreatePayloadSnapshot());
        ActionPayloadAccessors.SetTriggerParticleEffectObjectId(action, objectId);

        var clone = new CommandAction { ActionType = CommandActionType.TriggerParticleEffect };
        var clonedPayload = Assert.IsType<TriggerParticleEffectPayload>(action.CreatePayloadSnapshot());
        Assert.True(clone.TryApplyPayload(clonedPayload));
        Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(clone));
        ActionPayloadAccessors.SetTriggerParticleEffectObjectId(clone, remappedObjectId);
        Assert.Equal(remappedObjectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(clone));
        Assert.Contains("Trigger particle burst", CommandActionSummaryPresenter.BuildSummary(clone), StringComparison.Ordinal);
        Assert.Contains(objectId.ToString("D"), CommandActionSummaryPresenter.BuildSummary(action), StringComparison.Ordinal);

        action.ActionType = CommandActionType.EchoMessage;
        Assert.IsType<EchoPayload>(action.CreatePayloadSnapshot());
        action.ActionType = CommandActionType.TriggerParticleEffect;
        Assert.Null(ActionPayloadAccessors.GetTriggerParticleEffectObjectId(action));
        Assert.Contains(CommandActionType.TriggerParticleEffect, CommandActionTypeValues.All);
        Assert.Contains(CommandActionType.TriggerParticleEffect, CommandActionTypeValues.LinkedFlowAllowed);
    }

    [Fact]
    public void SaveReopenAndCleanExport_PreserveSingleTriggerActionAndExactObjectId()
    {
        var service = new JsonExportService();
        var objectId = Guid.Parse("c4702ef7-45ac-4abf-b1ef-8141a35c9d88");
        var target = new GameObject { Name = "Sparking Crate", ObjectId = objectId, ParticleEffect = new ParticleEffectAttachment { EffectKey = GetBurstEligibleRecipeKey() } };
        var trigger = new CommandAction
        {
            Name = "Trigger crate spark",
            ActionType = CommandActionType.TriggerParticleEffect,
            Payload = new TriggerParticleEffectPayload(objectId)
        };
        var linked = new CommandAction
        {
            Name = "Run burst flow",
            ActionType = CommandActionType.LinkedActions,
            LinkedFlowDefinition = new LinkedActionFlowDefinition
            {
                EntryNodeId = 1,
                Nodes = [new LinkedActionFlowNode { NodeId = 1, ActionId = trigger.Id }]
            }
        };
        var room = new Room { Name = "Room", GameObjects = [target], AvailableActions = [linked, trigger] };
        var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };
        var project = new ProjectModel { Name = "TriggerParticleEffectPersistence", Planets = [planet] };
        ScopeHierarchy.AttachParents(project);

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var projectPath = Path.Combine(tempRoot, "TriggerParticleEffectPersistence.sbe.json");
            service.SaveProjectModel(projectPath, project);
            var authoredRoomPath = Path.Combine(tempRoot, "Room", $"{room.Id:N}.room.json");
            using (var authored = JsonDocument.Parse(File.ReadAllText(authoredRoomPath)))
            {
                var authoredActions = authored.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
                var action = Assert.Single(authoredActions, item => item.GetProperty("actionType").GetString() == "TriggerParticleEffect");
                Assert.Equal("triggerParticleEffect", action.GetProperty("payload").GetProperty("$payloadType").GetString());
                Assert.Equal(objectId.ToString("D"), action.GetProperty("payload").GetProperty("objectId").GetString());
                var flow = Assert.Single(authoredActions, item => item.GetProperty("actionType").GetString() == "LinkedActions");
                Assert.Contains(flow.GetProperty("payload").GetProperty("linkedActions").EnumerateArray(), link => link.GetProperty("actionId").GetGuid() == trigger.Id);
            }

            var reopened = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedRoom = reopened.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedTrigger = Assert.Single(loadedRoom.AvailableActions, action => action.ActionType == CommandActionType.TriggerParticleEffect);
            Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(loadedTrigger));
            Assert.IsType<TriggerParticleEffectPayload>(loadedTrigger.Payload);
            var loadedFlow = Assert.Single(loadedRoom.AvailableActions, action => action.ActionType == CommandActionType.LinkedActions);
            Assert.Contains(loadedFlow.LinkedFlowDefinition.Nodes, node => node.ActionId == loadedTrigger.Id);

            service.ExportCleanProjectV1(projectPath, reopened);
            var runtimePath = Path.Combine(tempRoot, "GameRuntimeJson", "Room", $"{room.Id:D}".ToUpperInvariant() + ".runtime.json");
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath));
            var runtimeActions = runtime.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();
            var runtimeTrigger = Assert.Single(runtimeActions, item => item.GetProperty("actionType").GetString() == "TriggerParticleEffect");
            Assert.Equal("triggerParticleEffect", runtimeTrigger.GetProperty("payload").GetProperty("$payloadType").GetString());
            Assert.Equal(objectId.ToString("D"), runtimeTrigger.GetProperty("payload").GetProperty("objectId").GetString());
            var runtimePayload = Assert.IsType<RuntimeTriggerParticleEffectActionPayloadDto>(JsonSerializer.Deserialize<RuntimeCommandActionPayloadBaseDto>(runtimeTrigger.GetProperty("payload").GetRawText(), JsonExportService.CreateJsonOptions()));
            Assert.Equal(objectId, runtimePayload.ObjectId);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Validation_ReportsMissingTarget()
    {
        var action = CreateTrigger(null);
        var issues = Validate(action, []);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-024" && issue.Description.Contains("requires a target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_ReportsNonexistentTargetAndPreservesIt()
    {
        var objectId = Guid.NewGuid();
        var action = CreateTrigger(objectId);
        var loaded = JsonExportService.ToCommandActionModel(JsonExportService.ToCommandActionDtoForSequence(action, []));
        Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(loaded));
        var issues = Validate(action, []);
        Assert.Contains(issues, issue => issue.Description.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_ReportsMissingAttachmentAndIneligibleRecipeFromCatalogMetadata()
    {
        var attachmentless = new GameObject { Name = "No effect" };
        var attachmentlessIssues = Validate(CreateTrigger(attachmentless.ObjectId), [attachmentless]);
        Assert.Contains(attachmentlessIssues, issue => issue.Description.Contains("no particle effect attachment", StringComparison.OrdinalIgnoreCase));

        var ineligible = new GameObject { Name = "Continuous effect", ParticleEffect = new ParticleEffectAttachment { EffectKey = GetIneligibleRecipeKey() } };
        var ineligibleIssues = Validate(CreateTrigger(ineligible.ObjectId), [ineligible]);
        Assert.Contains(ineligibleIssues, issue => issue.Description.Contains("no surgeOnly streams", StringComparison.OrdinalIgnoreCase));

        var burstEligible = new GameObject { Name = "Burst effect", ParticleEffect = new ParticleEffectAttachment { EffectKey = GetBurstEligibleRecipeKey() } };
        Assert.Empty(Validate(CreateTrigger(burstEligible.ObjectId), [burstEligible]));
    }

    private static CommandAction CreateTrigger(Guid? objectId) => new()
    {
        Name = "Trigger burst",
        ActionType = CommandActionType.TriggerParticleEffect,
        Payload = new TriggerParticleEffectPayload(objectId)
    };

    private static IReadOnlyList<ValidationIssue> Validate(CommandAction action, IReadOnlyList<GameObject> objects)
    {
        var room = new Room { Name = "Room", GameObjects = objects.ToList(), AvailableActions = [action] };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "TriggerParticleEffectValidation", Planets = [planet] };
        ScopeHierarchy.AttachParents(project);
        var registry = new ValidationRuleRegistry();
        registry.Register(new TriggerParticleEffectTargetIsBurstEligibleRule());
        return new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues;
    }

    private static string GetBurstEligibleRecipeKey()
    {
        var catalog = new ParticleEffectAuthoringCatalogService();
        return catalog.GetRecipes().First(recipe => catalog.HasBurstEligibleStreams(recipe.EffectKey)).EffectKey;
    }

    private static string GetIneligibleRecipeKey()
    {
        var catalog = new ParticleEffectAuthoringCatalogService();
        return catalog.GetRecipes().First(recipe => !catalog.HasBurstEligibleStreams(recipe.EffectKey)).EffectKey;
    }
}
