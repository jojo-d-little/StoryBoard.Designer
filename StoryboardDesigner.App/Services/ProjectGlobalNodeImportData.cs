using StoryboardDesigner.App.Models;
using Storyboard.Shared.RuntimeContracts.Dtos;

namespace StoryboardDesigner.App.Services;

public sealed class ProjectGlobalNodeImportData
{
    public List<string> CommandVerbs { get; set; } = new();
    public List<string> Directionals { get; set; } = new();
    public List<DirectionalTraversalMapping> DirectionalTraversalMappings { get; set; } = new();
    public List<CommandAction> GlobalAvailableActions { get; set; } = new();
    public List<SoundEffectLibraryEntry> GlobalSoundEffectLibraryEntries { get; set; } = new();
    public List<EventSubscriptionDefinition> GlobalEventSubscriptions { get; set; } = new();
    public List<RuntimeTimerDefinitionDto> GlobalTimerDefinitions { get; set; } = new();
    public List<GameObject> ObjectTemplates { get; set; } = new();
    public List<Room> RoomTemplates { get; set; } = new();
    public List<GameObject> BaseObjects { get; set; } = new();
    public List<GameObject> GlobalObjects { get; set; } = new();
    public List<string> Diagnostics { get; set; } = new();
}
