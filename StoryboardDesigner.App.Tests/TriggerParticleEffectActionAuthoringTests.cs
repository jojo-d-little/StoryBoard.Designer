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
        var json = "{\"id\":\"" + Guid.NewGuid().ToString("D") + "\",\"actionType\":\"TriggerParticleEffect\",\"name\":\"Burst\",\"payload\":{\"$payloadType\":\"triggerParticleEffect\",\"objectId\":\"" + objectId.ToString("D") + "\",\"quantityMultiplier\":0.5,\"quantityMultiplierBinding\":{\"anchorKey\":\"currentPhase\",\"propertyName\":\"BurstScale\"}}}";

        var dto = Assert.IsType<CommandActionDto>(JsonSerializer.Deserialize<CommandActionDto>(json, JsonExportService.CreateJsonOptions()));
        var action = JsonExportService.ToCommandActionModel(dto);

        Assert.Equal(CommandActionType.TriggerParticleEffect, action.ActionType);
        Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(action));
        Assert.Equal(objectId, Assert.IsType<TriggerParticleEffectPayload>(action.Payload).ObjectId);
        Assert.Equal(0.5, ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplier(action));
        Assert.Equal("currentPhase", ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(action)?.AnchorKey);
        Assert.Equal("BurstScale", ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(action)?.PropertyName);
    }

    [Fact]
    public void PayloadLifecycle_ClonesRemapsAndSwitchesWithTypedDefaults()
    {
        var objectId = Guid.NewGuid();
        var remappedObjectId = Guid.NewGuid();
        var bindingOwnerId = Guid.NewGuid();
        var remappedBindingOwnerId = Guid.NewGuid();
        var action = new CommandAction { ActionType = CommandActionType.TriggerParticleEffect };
        Assert.IsType<TriggerParticleEffectPayload>(action.CreatePayloadSnapshot());
        ActionPayloadAccessors.SetTriggerParticleEffectObjectId(action, objectId);
        ActionPayloadAccessors.SetTriggerParticleEffectQuantityMultiplier(action, 0.25);
        ActionPayloadAccessors.SetTriggerParticleEffectQuantityMultiplierBinding(action, new ProjectGamePropertySourceReferenceDto
        {
            OwnerId = bindingOwnerId,
            PropertyName = "BurstScale"
        });

        var clone = new CommandAction { ActionType = CommandActionType.TriggerParticleEffect };
        var clonedPayload = Assert.IsType<TriggerParticleEffectPayload>(action.CreatePayloadSnapshot());
        Assert.True(clone.TryApplyPayload(clonedPayload));
        Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(clone));
        Assert.Equal(0.25, ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplier(clone));
        Assert.NotNull(ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(clone));
        Assert.NotSame(ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(action), ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(clone));
        ActionPayloadAccessors.RemapTriggerParticleEffectReferences(clone, new Dictionary<Guid, Guid>
        {
            [objectId] = remappedObjectId,
            [bindingOwnerId] = remappedBindingOwnerId
        });
        Assert.Equal(remappedObjectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(clone));
        Assert.Equal(remappedBindingOwnerId, ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(clone)?.OwnerId);
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
            Id = Guid.Parse("62c45da8-7acb-46f2-b1d2-19216e765b46"),
            Name = "Trigger crate spark",
            ActionType = CommandActionType.TriggerParticleEffect,
            Payload = new TriggerParticleEffectPayload(objectId, 0.5, new ProjectGamePropertySourceReferenceDto
            {
                AnchorKey = "currentPhase",
                PropertyName = "BurstScale"
            })
        };
        var linked = new CommandAction
        {
            Id = Guid.Parse("32e289da-20a0-4b9d-b08d-66a2f750b7c1"),
            Name = "Run burst flow",
            ActionType = CommandActionType.LinkedActions,
            LinkedFlowDefinition = new LinkedActionFlowDefinition
            {
                EntryNodeId = 1,
                Nodes = [new LinkedActionFlowNode { NodeId = 1, ActionId = trigger.Id }]
            }
        };
        var room = new Room { Id = Guid.Parse("f576c846-20da-4bd5-954c-32e551691814"), Name = "Room", GameObjects = [target], AvailableActions = [linked, trigger] };
        var area = new Area { Id = Guid.Parse("c31d197d-14a3-4b64-8b65-744303e9efaf"), Name = "Area", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Id = Guid.Parse("597a8e7d-1644-44de-9526-d58519ce1a7d"), Name = "Country", Areas = [area], StartingAreaName = area.Name };
        var planet = new Planet { Id = Guid.Parse("82c6b8b2-214e-4746-9aa3-3c1e93a7d2c8"), Name = "Planet", Countries = [country], StartingCountryName = country.Name };
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
                Assert.Equal(0.5, action.GetProperty("payload").GetProperty("quantityMultiplier").GetDouble());
                Assert.Equal("currentPhase", action.GetProperty("payload").GetProperty("quantityMultiplierBinding").GetProperty("anchorKey").GetString());
                Assert.Equal("BurstScale", action.GetProperty("payload").GetProperty("quantityMultiplierBinding").GetProperty("propertyName").GetString());
                var flow = Assert.Single(authoredActions, item => item.GetProperty("actionType").GetString() == "LinkedActions");
                Assert.Contains(flow.GetProperty("payload").GetProperty("linkedActions").EnumerateArray(), link => link.GetProperty("actionId").GetGuid() == trigger.Id);
            }

            var reopened = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedRoom = reopened.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedTrigger = Assert.Single(loadedRoom.AvailableActions, action => action.ActionType == CommandActionType.TriggerParticleEffect);
            Assert.Equal(objectId, ActionPayloadAccessors.GetTriggerParticleEffectObjectId(loadedTrigger));
            Assert.Equal(0.5, ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplier(loadedTrigger));
            Assert.Equal("currentPhase", ActionPayloadAccessors.GetTriggerParticleEffectQuantityMultiplierBinding(loadedTrigger)?.AnchorKey);
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
            Assert.Equal(0.5, runtimeTrigger.GetProperty("payload").GetProperty("quantityMultiplier").GetDouble());
            Assert.Equal("currentPhase", runtimeTrigger.GetProperty("payload").GetProperty("quantityMultiplierBinding").GetProperty("anchorKey").GetString());
            Assert.Equal("BurstScale", runtimeTrigger.GetProperty("payload").GetProperty("quantityMultiplierBinding").GetProperty("propertyName").GetString());
            var runtimePayload = Assert.IsType<RuntimeTriggerParticleEffectActionPayloadDto>(JsonSerializer.Deserialize<RuntimeCommandActionPayloadBaseDto>(runtimeTrigger.GetProperty("payload").GetRawText(), JsonExportService.CreateJsonOptions()));
            Assert.Equal(objectId, runtimePayload.ObjectId);
            Assert.Equal(0.5, runtimePayload.QuantityMultiplier);
            Assert.Equal("currentPhase", runtimePayload.QuantityMultiplierBinding?.AnchorKey);

        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, 1d)]
    [InlineData(0d, 0d)]
    [InlineData(0.5d, 0.5d)]
    [InlineData(2.25d, 2.25d)]
    public void QuantityMultiplier_DefaultsAndPreservesNonnegativeLiterals(double? authored, double expected)
    {
        var payload = new TriggerParticleEffectPayload(Guid.NewGuid(), authored);
        var action = new CommandAction { ActionType = CommandActionType.TriggerParticleEffect, Payload = payload };
        var dto = JsonExportService.ToCommandActionDtoForSequence(action, []);
        var exported = Assert.IsType<ProjectTriggerParticleEffectActionPayloadDto>(dto.Payload);
        Assert.Equal(expected, exported.QuantityMultiplier ?? 1d);
    }

    [Fact]
    public void DeclaredQuantityResultCodes_AreAvailableToNormalOutcomeAndSoundHandling()
    {
        var resultCodes = ActionManifestCatalog.GetSupportedResultCodes(CommandActionType.TriggerParticleEffect)
            .Select(code => code.Token)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("NoParticlesRequested", resultCodes);
        Assert.Contains("InvalidQuantityMultiplier", resultCodes);

        var defaults = ActionManifestCatalog.CreateDefaultOutcomeMessageMap(CommandActionType.TriggerParticleEffect);
        Assert.Contains("NoParticlesRequested", defaults.Keys);
        Assert.Contains("InvalidQuantityMultiplier", defaults.Keys);
    }

    [Fact]
    public void Validation_RejectsInvalidLiteralAndUnresolvedOrWrongKindBindings()
    {
        var invalidLiteral = CreateTrigger(Guid.NewGuid(), double.NaN);
        Assert.Contains(Validate(invalidLiteral, []), issue => issue.RuleId == "ACT-025" && issue.Description.Contains("finite and nonnegative", StringComparison.OrdinalIgnoreCase));

        var unresolved = CreateTrigger(Guid.NewGuid(), 1, new ProjectGamePropertySourceReferenceDto { PropertyName = "MissingScale" });
        Assert.Contains(Validate(unresolved, []), issue => issue.RuleId == "ACT-025" && issue.Description.Contains("cannot resolve", StringComparison.OrdinalIgnoreCase));

        var wrongKindOwner = new GameObject
        {
            Name = "Boolean source",
            Variables = [new GamePropertyDefinition { Name = "IsReady", ValueRestriction = GamePropertyValueRestriction.TrueFalse, DefaultValue = "false" }]
        };
        var wrongKind = CreateTrigger(Guid.NewGuid(), 1, new ProjectGamePropertySourceReferenceDto { OwnerId = wrongKindOwner.ObjectId, PropertyName = "IsReady" });
        Assert.Contains(Validate(wrongKind, [wrongKindOwner]), issue => issue.RuleId == "ACT-025" && issue.Description.Contains("numeric", StringComparison.OrdinalIgnoreCase));

        var numericOwner = new GameObject
        {
            Name = "Numeric source",
            Variables = [new GamePropertyDefinition { Name = "BurstScale", ValueRestriction = GamePropertyValueRestriction.Numeric, DefaultValue = "1" }]
        };
        var numeric = CreateTrigger(Guid.NewGuid(), 0, new ProjectGamePropertySourceReferenceDto { OwnerId = numericOwner.ObjectId, PropertyName = "BurstScale" });
        Assert.DoesNotContain(Validate(numeric, [numericOwner]), issue => issue.RuleId == "ACT-025");
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

    private static CommandAction CreateTrigger(Guid? objectId, double? multiplier, ProjectGamePropertySourceReferenceDto? binding = null) => new()
    {
        Name = "Trigger burst",
        ActionType = CommandActionType.TriggerParticleEffect,
        Payload = new TriggerParticleEffectPayload(objectId, multiplier, binding)
    };

    private static IReadOnlyList<ValidationIssue> Validate(
        CommandAction action,
        IReadOnlyList<GameObject> objects,
        IReadOnlyList<GamePropertyDefinition>? globalProperties = null)
    {
        var room = new Room { Name = "Room", GameObjects = objects.ToList(), AvailableActions = [action] };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { Name = "TriggerParticleEffectValidation", Planets = [planet], GlobalObjectVariables = globalProperties?.ToList() ?? [] };
        ScopeHierarchy.AttachParents(project);
        var registry = new ValidationRuleRegistry();
        registry.Register(new TriggerParticleEffectTargetIsBurstEligibleRule());
        registry.Register(new TriggerParticleEffectQuantityMultiplierRule());
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
