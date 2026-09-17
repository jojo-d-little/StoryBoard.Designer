using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;
using Storyboard.Shared.GameStateData;
using System.Collections.ObjectModel;

namespace StoryboardDesigner.App.Tests;

public sealed class ActionIntegrityRulesTests
{
    [Fact]
    public void SynonymRequiresTargetRule_ReportsIssue_WhenTargetMissing()
    {
        var synonym = new CommandAction { Name = "alias", ActionType = CommandActionType.Synonym };
        var issues = Evaluate(new SynonymRequiresTargetRule(), synonym);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-001");
        Assert.Contains(issues, issue => issue.Path.EndsWith(" / Action:alias", StringComparison.Ordinal));
    }

    [Fact]
    public void SynonymSelfTargetRule_ReportsIssue_WhenTargetIsSelf()
    {
        var synonym = new CommandAction { Name = "alias", ActionType = CommandActionType.Synonym };
        synonym.SynonymTargetActionId = synonym.Id;
        var issues = Evaluate(new SynonymSelfTargetRule(), synonym);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-002");
    }

    [Fact]
    public void SynonymMissingTargetRule_ReportsIssue_WhenTargetNotFound()
    {
        var synonym = new CommandAction { Name = "alias", ActionType = CommandActionType.Synonym, SynonymTargetActionId = Guid.NewGuid() };
        var issues = Evaluate(new SynonymMissingTargetRule(), synonym);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-003");
    }

    [Fact]
    public void SynonymMissingTargetRule_UsesPayloadTargetId_WhenPresent()
    {
        var payloadTargetId = Guid.NewGuid();
        var synonym = new CommandAction
        {
            Name = "alias",
            ActionType = CommandActionType.Synonym,
            SynonymTargetActionId = Guid.Empty,
            Payload = new SynonymPayload(payloadTargetId)
        };

        var issues = Evaluate(new SynonymMissingTargetRule(), synonym);
        var matchingIssues = issues.Where(issue => issue.RuleId == "ACT-003").ToList();

        var issue = Assert.Single(matchingIssues);
        Assert.Contains(payloadTargetId.ToString(), issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinkedActionSelfTargetRule_ReportsIssue_WhenActionLinksToSelf()
    {
        var action = new CommandAction { Name = "root", ActionType = CommandActionType.EchoMessage };
        action.LinkedActions.Add(new LinkedActionReference { ActionId = action.Id, RunWhen = LinkedActionRunWhen.Always });
        var issues = Evaluate(new LinkedActionSelfTargetRule(), action);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-004");
    }

    [Fact]
    public void LinkedActionMissingTargetRule_ReportsIssue_WhenLinkedTargetNotFound()
    {
        var action = new CommandAction { Name = "root", ActionType = CommandActionType.EchoMessage };
        action.LinkedActions.Add(new LinkedActionReference { ActionId = Guid.NewGuid(), RunWhen = LinkedActionRunWhen.Always });
        var issues = Evaluate(new LinkedActionMissingTargetRule(), action);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-005");
    }

    [Fact]
    public void LinkedFlowTargetsSynonymRule_ReportsIssue_WhenLinkedFlowTargetsSynonym()
    {
        var source = new CommandAction { Name = "source", ActionType = CommandActionType.EchoMessage };
        var synonym = new CommandAction { Name = "alias", ActionType = CommandActionType.Synonym, SynonymTargetActionId = Guid.NewGuid() };
        source.LinkedActions.Add(new LinkedActionReference { ActionId = synonym.Id, RunWhen = LinkedActionRunWhen.Always });

        var issues = Evaluate(new LinkedFlowTargetsSynonymRule(), source, synonym);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-006");
    }

    [Fact]
    public void SynonymUsedInLinkedFlowRule_ReportsIssue_WhenSynonymIsLinkedTarget()
    {
        var source = new CommandAction { Name = "source", ActionType = CommandActionType.EchoMessage };
        var synonym = new CommandAction { Name = "alias", ActionType = CommandActionType.Synonym, SynonymTargetActionId = Guid.NewGuid() };
        source.LinkedActions.Add(new LinkedActionReference { ActionId = synonym.Id, RunWhen = LinkedActionRunWhen.Always });

        var issues = Evaluate(new SynonymUsedInLinkedFlowRule(), source, synonym);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-007");
    }

    [Fact]
    public void LinkedActionCycleRule_ReportsIssue_WhenCycleExists()
    {
        var a = new CommandAction { Name = "A", ActionType = CommandActionType.EchoMessage };
        var b = new CommandAction { Name = "B", ActionType = CommandActionType.EchoMessage };
        a.LinkedActions.Add(new LinkedActionReference { ActionId = b.Id, RunWhen = LinkedActionRunWhen.Always });
        b.LinkedActions.Add(new LinkedActionReference { ActionId = a.Id, RunWhen = LinkedActionRunWhen.Always });

        var issues = Evaluate(new LinkedActionCycleRule(), a, b);
        Assert.Contains(issues, issue => issue.RuleId == "ACT-008");
    }

    [Fact]
    public void CompositeActionMissingTargetRule_ReportsIssue_WhenCompositeRecipeTargetMissing()
    {
        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeRecipeId = Guid.NewGuid(),
            CompositeTargetObjectId = Guid.NewGuid(),
            CompositeRequiredPartObjectIds = [Guid.NewGuid()]
        };

        var issues = Evaluate(new CompositeActionMissingTargetRule(), action);

        Assert.Contains(issues, issue => issue.RuleId == "ACT-009");
    }

    [Fact]
    public void CompositeActionMissingTargetRule_DoesNotReport_WhenCompositeTargetExists()
    {
        var target = new GameObject
        {
            Name = "Potion",
            IsCompositeTarget = true,
            CompositeRecipeId = Guid.NewGuid()
        };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeRecipeId = target.CompositeRecipeId,
            CompositeRequiredPartObjectIds = [Guid.NewGuid()]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [target],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeActionMissingTargetRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-009");
    }

    [Fact]
    public void CompositeActionMissingTargetRule_DoesNotReport_WhenCompositeTargetObjectIdUsesAuthoredId()
    {
        var target = new GameObject
        {
            Name = "Potion",
            IsCompositeTarget = true,
            CompositeRecipeId = Guid.NewGuid()
        };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = target.ObjectId,
            CompositeRecipeId = target.CompositeRecipeId,
            CompositeRequiredPartObjectIds = [Guid.NewGuid()]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [target],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country A", Areas = [area], StartingAreaName = "Area A" };
        var planet = new Planet { Name = "Planet A", Countries = [country], StartingCountryName = "Country A" };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeActionMissingTargetRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-009");
    }

    [Fact]
    public void CompositeActionMissingTargetRule_ReportsIssue_WhenCompositeTargetObjectIdUsesLegacyObjectId()
    {
        var target = new GameObject
        {
            Name = "Potion",
            IsCompositeTarget = true,
            CompositeRecipeId = Guid.NewGuid()
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [target]
        };

        var legacyObjectId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        Assert.NotEqual(target.ObjectId, legacyObjectId);

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = legacyObjectId,
            CompositeRecipeId = target.CompositeRecipeId,
            CompositeRequiredPartObjectIds = [Guid.NewGuid()]
        };

        room.AvailableActions.Add(action);

        var area = new Area { Name = "Area A", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country A", Areas = [area], StartingAreaName = "Area A" };
        var planet = new Planet { Name = "Planet A", Countries = [country], StartingCountryName = "Country A" };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeActionMissingTargetRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.Contains(issues, issue => issue.RuleId == "ACT-009");
    }

    [Fact]
    public void CompositeActionMissingRequiredPartRule_ReportsWarning_WhenRequiredPartMissing()
    {
        var knownPart = new GameObject { Name = "Froghair" };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeRequiredPartObjectIds = [knownPart.ObjectId, Guid.NewGuid()]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [knownPart],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeActionMissingRequiredPartRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        var issue = Assert.Single(issues, issue => issue.RuleId == "ACT-010");
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void CompositeActionMissingRequiredPartRule_DoesNotReport_WhenAllRequiredPartsExist()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeActionMissingRequiredPartRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-010");
    }

    [Fact]
    public void CompositeActionMissingRequiredPartRule_DoesNotReport_WhenRequiredPartExistsInAreaScopeObjects()
    {
        var areaScopedPart = new GameObject { Name = "AreaScopedPart" };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeRequiredPartObjectIds = [areaScopedPart.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room], GameObjects = [areaScopedPart] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new CompositeActionMissingRequiredPartRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-010");
    }

    [Fact]
    public void BuildCompositeByPartsMissingTargetObjectRule_ReportsWarning_WhenTargetMissing()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = Guid.NewGuid(),
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new BuildCompositeByPartsMissingTargetObjectRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        var issue = Assert.Single(issues, issue => issue.RuleId == "ACT-011");
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void BuildCompositeByPartsMissingTargetObjectRule_DoesNotReport_WhenTargetExists()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };
        var target = new GameObject { Name = "SmokeBall", IsCompositeTarget = true };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = target.ObjectId,
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB, target],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new BuildCompositeByPartsMissingTargetObjectRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-011");
    }

    [Fact]
    public void BuildCompositeByPartsMissingTargetObjectRule_DoesNotReport_WhenTargetExistsInCountryScopeObjects()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };
        var countryScopedTarget = new GameObject { Name = "Country Target", IsCompositeTarget = true };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = countryScopedTarget.ObjectId,
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area], GameObjects = [countryScopedTarget] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new BuildCompositeByPartsMissingTargetObjectRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-011");
    }

    [Fact]
    public void BuildCompositeByPartsTargetRecipeMismatchRule_ReportsWarning_WhenTargetRecipeDiffers()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };
        var target = new GameObject
        {
            Name = "SmokeBall",
            IsCompositeTarget = true,
            CompositeRecipeId = Guid.NewGuid()
        };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = target.ObjectId,
            CompositeRecipeId = Guid.NewGuid(),
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB, target],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new BuildCompositeByPartsTargetRecipeMismatchRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        var issue = Assert.Single(issues, issue => issue.RuleId == "ACT-012");
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void BuildCompositeByPartsTargetRecipeMismatchRule_DoesNotReport_WhenTargetRecipeMatches()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };
        var recipeId = Guid.NewGuid();
        var target = new GameObject
        {
            Name = "SmokeBall",
            IsCompositeTarget = true,
            CompositeRecipeId = recipeId
        };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = target.ObjectId,
            CompositeRecipeId = recipeId,
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB, target],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new BuildCompositeByPartsTargetRecipeMismatchRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-012");
    }

    [Fact]
    public void BuildCompositeByPartsTargetRecipeMismatchRule_DoesNotReport_WhenTargetInPlanetScopeMatchesRecipe()
    {
        var partA = new GameObject { Name = "Froghair" };
        var partB = new GameObject { Name = "TeaLeaves" };
        var recipeId = Guid.NewGuid();
        var planetScopedTarget = new GameObject
        {
            Name = "Planet Target",
            IsCompositeTarget = true,
            CompositeRecipeId = recipeId
        };

        var action = new CommandAction
        {
            Name = "use_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            CompositeTargetObjectId = planetScopedTarget.ObjectId,
            CompositeRecipeId = recipeId,
            CompositeRequiredPartObjectIds = [partA.ObjectId, partB.ObjectId]
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [partA, partB],
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room] };
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country], GameObjects = [planetScopedTarget] };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(new BuildCompositeByPartsTargetRecipeMismatchRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-012");
    }

    [Fact]
    public void InvokeProcedureMissingTargetRule_ReportsIssue_WhenProcedureMissing()
    {
        var action = new CommandAction
        {
            Name = "invoke_missing",
            ActionType = CommandActionType.InvokeProcedure
        };

        ActionPayloadAccessors.SetProcedureId(action, Guid.NewGuid());

        var issues = Evaluate(new InvokeProcedureMissingTargetRule(), action);

        var issue = Assert.Single(issues, issue => issue.RuleId == "ACT-013");
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Error, issue.Severity);
    }

    [Fact]
    public void InvokeProcedureMissingTargetRule_DoesNotReport_WhenProcedureExists()
    {
        var procedureId = Guid.NewGuid();
        var action = new CommandAction
        {
            Name = "invoke_existing",
            ActionType = CommandActionType.InvokeProcedure
        };

        ActionPayloadAccessors.SetProcedureId(action, procedureId);

        var room = new Room
        {
            Name = "Room A",
            AvailableActions = [action]
        };

        var area = new Area { Name = "Area A", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country A", Areas = [area], StartingAreaName = "Area A" };
        var planet = new Planet { Name = "Planet A", Countries = [country], StartingCountryName = "Country A" };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet],
            Procedures =
            [
                new ProcedureDefinition
                {
                    Id = procedureId,
                    Name = "Procedure A"
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new InvokeProcedureMissingTargetRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-013");
    }

    [Fact]
    public void MaterializeSourceObjectReferenceExistsRule_ReportsIssue_WhenSourceObjectMissing()
    {
        var action = new CommandAction
        {
            Name = "materialize_missing",
            ActionType = CommandActionType.MaterializeObjectCopy
        };

        ActionPayloadAccessors.SetMaterializeSourceObjectId(action, Guid.NewGuid());

        var issues = Evaluate(new MaterializeSourceObjectReferenceExistsRule(), action);

        var issue = Assert.Single(issues, issue => issue.RuleId == "ACT-014");
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Error, issue.Severity);
    }

    [Fact]
    public void MaterializeSourceObjectReferenceExistsRule_DoesNotReport_WhenSourceObjectExists()
    {
        var sourceObject = new GameObject { Name = "Source" };
        var action = new CommandAction
        {
            Name = "materialize_existing",
            ActionType = CommandActionType.MaterializeObjectCopy
        };

        ActionPayloadAccessors.SetMaterializeSourceObjectId(action, sourceObject.ObjectId);

        var room = new Room
        {
            Name = "Room A",
            AvailableActions = [action],
            GameObjects = [sourceObject]
        };

        var area = new Area { Name = "Area A", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country A", Areas = [area], StartingAreaName = "Area A" };
        var planet = new Planet { Name = "Planet A", Countries = [country], StartingCountryName = "Country A" };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new MaterializeSourceObjectReferenceExistsRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-014");
    }

    [Fact]
    public void OutcomeSoundEffectReferenceExistsRule_ReportsIssue_WhenCueReferencesMissingSoundEffect()
    {
        var missingId = Guid.NewGuid();
        var action = new CommandAction
        {
            Name = "sound_missing",
            ActionType = CommandActionType.EchoMessage,
            OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ok"] =
                [
                    new OutcomeSoundEffectCue
                    {
                        SoundEffectId = missingId,
                        SoundEffectKeyHint = "missing.sound",
                        Enabled = true
                    }
                ]
            }
        };

        var issues = Evaluate(new OutcomeSoundEffectReferenceExistsRule(), action);

        var issue = Assert.Single(issues, issue => issue.RuleId == "ACT-015");
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Warning, issue.Severity);
        Assert.Contains(missingId.ToString("D"), issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("result code 'ok'", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutcomeSoundEffectReferenceExistsRule_DoesNotReport_WhenCueReferencesExistingSoundEffect()
    {
        var soundId = Guid.NewGuid();
        var action = new CommandAction
        {
            Name = "sound_present",
            ActionType = CommandActionType.EchoMessage,
            OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ok"] =
                [
                    new OutcomeSoundEffectCue
                    {
                        SoundEffectId = soundId,
                        SoundEffectKeyHint = "ui.click",
                        Enabled = true
                    }
                ]
            }
        };

        var room = new Room
        {
            Name = "Room A",
            AvailableActions = [action],
            SoundEffectLibraryEntries =
            [
                new SoundEffectLibraryEntry
                {
                    SoundEffectId = soundId,
                    SoundEffectKey = "ui.click",
                    DisplayName = "UI Click",
                    AssetRef = "SFX/UI_Click.wav",
                    RepeatMode = "None"
                }
            ]
        };

        var area = new Area { Name = "Area A", Rooms = [room], StartingRoomId = room.Id };
        var country = new Country { Name = "Country A", Areas = [area], StartingAreaName = "Area A" };
        var planet = new Planet { Name = "Planet A", Countries = [country], StartingCountryName = "Country A" };
        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new OutcomeSoundEffectReferenceExistsRule());
        var engine = new ValidationEngine(registry);
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues;

        Assert.DoesNotContain(issues, issue => issue.RuleId == "ACT-015");
    }

    private static IReadOnlyList<StoryboardDesigner.App.Validation.Contracts.ValidationIssue> Evaluate(
        StoryboardDesigner.App.Validation.Contracts.IValidationRule rule,
        params CommandAction[] actions)
    {
        var room = new Room
        {
            Name = "Room A",
            AvailableActions = new ObservableCollection<CommandAction>(actions)
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area]
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country]
        };

        var project = new ProjectModel
        {
            StartingPlanetName = "Planet A",
            Planets = [planet]
        };

        planet.StartingCountryName = "Country A";
        country.StartingAreaName = "Area A";
        area.StartingRoomId = room.Id;

        var registry = new ValidationRuleRegistry();
        registry.Register(rule);
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }
}
