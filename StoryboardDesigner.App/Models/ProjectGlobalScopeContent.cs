namespace StoryboardDesigner.App.Models;

public sealed class ProjectGlobalScopeContent
{
    private readonly ProjectModel _project;

    public ProjectGlobalScopeContent(ProjectModel project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
    }

    public List<GameObject> GameObjects
    {
        get => _project.GlobalScopeGameObjects;
        set => _project.GlobalScopeGameObjects = value ?? new List<GameObject>();
    }

    public List<GamePropertyDefinition> GameProperties
    {
        get => _project.GlobalScopeGameProperties;
        set => _project.GlobalScopeGameProperties = value ?? new List<GamePropertyDefinition>();
    }

    public List<CommandAction> AvailableActions
    {
        get => _project.GlobalScopeAvailableActions;
        set => _project.GlobalScopeAvailableActions = value ?? new List<CommandAction>();
    }

    public List<SoundEffectLibraryEntry> SoundEffectLibraryEntries
    {
        get => _project.GlobalScopeSoundEffectLibraryEntries;
        set => _project.GlobalScopeSoundEffectLibraryEntries = value ?? new List<SoundEffectLibraryEntry>();
    }

    public List<Storyboard.Shared.RuntimeContracts.Dtos.RuntimeTimerDefinitionDto> TimerDefinitions
    {
        get => _project.TimerDefinitions;
        set => _project.TimerDefinitions = value ?? new List<Storyboard.Shared.RuntimeContracts.Dtos.RuntimeTimerDefinitionDto>();
    }

    public List<string> IgnoredValidationRuleIds
    {
        get => _project.GlobalScopeIgnoredValidationRuleIds;
        set => _project.GlobalScopeIgnoredValidationRuleIds = value ?? new List<string>();
    }

    public List<EventSubscriptionDefinition> EventSubscriptions
    {
        get => _project.EventSubscriptions;
        set => _project.EventSubscriptions = value ?? new List<EventSubscriptionDefinition>();
    }

    public string Name
    {
        get => _project.GlobalScopeDisplayName;
        set => _project.GlobalScopeDisplayName = value ?? string.Empty;
    }

    public string ProducerNotes
    {
        get => _project.GlobalScopeDisplayProducerNotes;
        set => _project.GlobalScopeDisplayProducerNotes = value ?? string.Empty;
    }
}