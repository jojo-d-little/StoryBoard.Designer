using System.Reflection;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Validation.Rules.Scripting;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class BaseObjectGamePropertyInheritanceTests
{
    [Fact]
    public void PlacedObjectPropertyView_IncludesBaseOnlyPropertyAndMarksItsSource()
    {
        var baseProperty = NumericProperty("BaseSpeed", "12");
        var (project, baseObject, instance) = BuildLinkedObjects(baseProperty);
        ScopeHierarchy.AttachParents(project);
        var parent = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", BindingFlags.NonPublic | BindingFlags.Static)!;

        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [parent, instance, instance.Variables, PropertyResolutionScope.Object, null, null]));

        var propertyNode = Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>());
        Assert.Equal("BaseSpeed", propertyNode.Variable.Name);
        Assert.True(propertyNode.IsInherited);
        Assert.Same(baseObject, propertyNode.DefinitionOwner);
        Assert.Equal("(Base: Elevator)", propertyNode.InheritedSourceDisplay);
    }

    [Fact]
    public void EffectiveProperties_InstanceDefinitionTakesPrecedenceCaseInsensitively()
    {
        var baseProperty = NumericProperty("MoveSpeed", "12");
        var localProperty = NumericProperty("movespeed", "20");
        var (project, _, instance) = BuildLinkedObjects(baseProperty, localProperty);

        var effective = EffectiveGamePropertyResolver.Resolve(instance, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id));

        var selected = Assert.Single(effective, property => string.Equals(property.Definition.Name, "MoveSpeed", StringComparison.OrdinalIgnoreCase));
        Assert.Same(localProperty, selected.Definition);
        Assert.False(selected.IsInherited);
    }

    [Fact]
    public void ObjectPropertyChoices_IncludePropertyDefinedOnlyOnLinkedBaseObject()
    {
        var (project, _, instance) = BuildLinkedObjects(NumericProperty("BaseSpeed", "12"));
        ScopeHierarchy.AttachParents(project);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            new ProjectUiServiceStub());

        var choices = viewModel.GetVariableChoicesForScopeNode(instance, PropertyResolutionScope.Object);

        Assert.Contains(choices, choice => string.Equals(choice.Value, "self.BaseSpeed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParticleEffectValidation_ResolvesPropertyDefinedOnlyOnLinkedBaseObject()
    {
        var baseProperty = NumericProperty("BaseEmissionRate", "12");
        var (project, _, instance) = BuildLinkedObjects(baseProperty);
        var catalog = new ParticleEffectAuthoringCatalogService();
        var recipe = catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment)
            .First(option => catalog.GetControls(option.EffectKey).Any(control =>
                string.Equals(control.Name, "EmissionRateMultiplier", StringComparison.OrdinalIgnoreCase)));
        instance.ParticleEffect = new ParticleEffectAttachment
        {
            EffectKey = recipe.EffectKey,
            Settings = [new ParticleEffectControlSettingDto
            {
                ControlKey = "EmissionRateMultiplier",
                Binding = new ProjectGamePropertySourceReferenceDto
                {
                    OwnerId = instance.ObjectId,
                    PropertyName = "BaseEmissionRate"
                }
            }]
        };
        ScopeHierarchy.AttachParents(project);
        var request = new ValidationExecutionRequest(project);
        var context = new ValidationRuleContext(
            project, instance, project, request, ValidationProfile.Unspecified,
            IncludeDescendants: true, IsRecursivePass: true, IsFullProject: true,
            ValidationLookupService.Build(project), new ValidationSharedState());

        var descriptions = new ParticleEffectAuthoringValidationRule(catalog).Evaluate(context)
            .Select(issue => issue.Description)
            .ToList();

        Assert.DoesNotContain(descriptions, description => description.Contains("cannot resolve variable 'BaseEmissionRate'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActionReferenceValidation_AcceptsPropertyDefinedOnlyOnLinkedBaseObject()
    {
        var (project, _, instance) = BuildLinkedObjects(NumericProperty("BaseSpeed", "12"));
        instance.AvailableActions =
        [
            new CommandAction
            {
                Name = "Move",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "ECHO {BaseSpeed}",
                Payload = new EchoPayload()
            }
        ];
        ScopeHierarchy.AttachParents(project);
        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptUnknownReferenceRule());

        var issues = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.Description.Contains("Unknown reference 'BaseSpeed'", StringComparison.OrdinalIgnoreCase));
    }

    private static (ProjectModel Project, GameObject BaseObject, GameObject Instance) BuildLinkedObjects(
        GamePropertyDefinition baseProperty,
        GamePropertyDefinition? instanceProperty = null)
    {
        var baseObject = new GameObject { Name = "Elevator", Variables = [baseProperty] };
        var instance = new GameObject
        {
            Name = "Elevator in Foyer",
            LinkedBaseObjectId = baseObject.ObjectId,
            Variables = instanceProperty is null ? [] : [instanceProperty]
        };
        var room = new Room { Name = "Foyer", GameObjects = [instance] };
        var project = new ProjectModel
        {
            Name = "Inheritance",
            BaseObjects = [baseObject],
            Planets = [new Planet { Name = "Earth", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
        };
        return (project, baseObject, instance);
    }

    private static GamePropertyDefinition NumericProperty(string name, string value) => new()
    {
        Name = name,
        DefaultValue = value,
        ValueRestriction = GamePropertyValueRestriction.Numeric,
        Lifetime = GamePropertyLifetime.Singleton
    };
}
