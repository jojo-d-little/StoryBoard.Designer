namespace StoryboardDesigner.App.Services;

public sealed class GlobalImportSelectionRequest
{
    public string SourceProjectPath { get; set; } = string.Empty;
    public int VerbCount { get; set; }
    public int DirectionalCount { get; set; }
    public int GlobalActionCount { get; set; }
    public int GlobalSoundEffectCount { get; set; }
    public int GlobalEventSubscriptionCount { get; set; }
    public int GlobalTimerCount { get; set; }
    public int TemplateObjectCount { get; set; }
    public int RoomTemplateCount { get; set; }
    public int BaseObjectCount { get; set; }
    public int GlobalObjectCount { get; set; }
}
