using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Tests;

public sealed class ProjectModelGlobalScopeFacadeParityTests
{
    [Fact]
    public void GlobalScope_GameObjects_Aliases_LegacyGameObjects_Bidirectionally()
    {
        var project = new ProjectModel();

        var viaFacade = new List<GameObject>
        {
            new() { Name = "Player" }
        };

        project.GlobalScope.GameObjects = viaFacade;

        Assert.Same(viaFacade, project.GameObjects);
        Assert.Same(project.GameObjects, project.GlobalScope.GameObjects);

        var viaLegacy = new List<GameObject>
        {
            new() { Name = "Companion" }
        };

        project.GameObjects = viaLegacy;

        Assert.Same(viaLegacy, project.GlobalScope.GameObjects);
        Assert.Same(project.GlobalScope.GameObjects, project.GameObjects);
    }

    [Fact]
    public void GlobalScope_GameProperties_Aliases_LegacyGlobalObjectVariables_Bidirectionally()
    {
        var project = new ProjectModel();

        var viaFacade = new List<GamePropertyDefinition>
        {
            new() { Name = "health", DefaultValue = "100", ValueRestriction = GamePropertyValueRestriction.Numeric }
        };

        project.GlobalScope.GameProperties = viaFacade;

        Assert.Same(viaFacade, project.GlobalObjectVariables);
        Assert.Same(project.GlobalObjectVariables, project.GlobalScope.GameProperties);

        var viaLegacy = new List<GamePropertyDefinition>
        {
            new() { Name = "stamina", DefaultValue = "50", ValueRestriction = GamePropertyValueRestriction.Numeric }
        };

        project.GlobalObjectVariables = viaLegacy;

        Assert.Same(viaLegacy, project.GlobalScope.GameProperties);
        Assert.Same(project.GlobalScope.GameProperties, project.GlobalObjectVariables);
    }

    [Fact]
    public void GlobalScope_ActionsAndIgnores_Alias_LegacyMembers_Bidirectionally()
    {
        var project = new ProjectModel();

        var actionsViaFacade = new List<CommandAction> { new() { Name = "inspect" } };
        var ignoresViaFacade = new List<string> { "RULE-1" };

        project.GlobalScope.AvailableActions = actionsViaFacade;
        project.GlobalScope.IgnoredValidationRuleIds = ignoresViaFacade;

        Assert.Same(actionsViaFacade, project.GlobalObjectAvailableActions);
        Assert.Same(ignoresViaFacade, project.GlobalObjectIgnoredValidationRuleIds);

        var actionsViaLegacy = new List<CommandAction> { new() { Name = "use" } };
        var ignoresViaLegacy = new List<string> { "RULE-2" };

        project.GlobalObjectAvailableActions = actionsViaLegacy;
        project.GlobalObjectIgnoredValidationRuleIds = ignoresViaLegacy;

        Assert.Same(actionsViaLegacy, project.GlobalScope.AvailableActions);
        Assert.Same(ignoresViaLegacy, project.GlobalScope.IgnoredValidationRuleIds);
    }

    [Fact]
    public void GlobalScope_Ignores_Alias_GlobalRootIgnoredRules_Bidirectionally()
    {
        var project = new ProjectModel();

        var viaRoot = new List<string> { "RULE-ROOT" };
        project.GlobalIgnoredValidationRuleIds = viaRoot;

        Assert.Same(viaRoot, project.GlobalScope.IgnoredValidationRuleIds);

        var viaScope = new List<string> { "RULE-SCOPE" };
        project.GlobalScope.IgnoredValidationRuleIds = viaScope;

        Assert.Same(viaScope, project.GlobalIgnoredValidationRuleIds);
    }

    [Fact]
    public void GlobalScope_NameAndProducerNotes_Alias_LegacyMembers_Bidirectionally()
    {
        var project = new ProjectModel();

        project.GlobalScope.Name = "Root Game Objects";
        project.GlobalScope.ProducerNotes = "Authoring notes";

        Assert.Equal("Root Game Objects", project.GlobalObjectScopeName);
        Assert.Equal("Authoring notes", project.GlobalObjectScopeProducerNotes);

        project.GlobalObjectScopeName = "Global Objects";
        project.GlobalObjectScopeProducerNotes = "Legacy notes";

        Assert.Equal("Global Objects", project.GlobalScope.Name);
        Assert.Equal("Legacy notes", project.GlobalScope.ProducerNotes);
    }

    [Fact]
    public void GlobalScope_EventSubscriptions_Aliases_ProjectScopeStorage()
    {
        var project = new ProjectModel();

        var viaScope = new List<EventSubscriptionDefinition>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventKey = "player.room.entered"
            }
        };

        project.GlobalScope.EventSubscriptions = viaScope;

        Assert.Same(viaScope, project.EventSubscriptions);
        Assert.Same(project.EventSubscriptions, project.GlobalScope.EventSubscriptions);
    }
}