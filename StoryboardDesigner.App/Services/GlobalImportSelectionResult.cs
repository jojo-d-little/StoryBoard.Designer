namespace StoryboardDesigner.App.Services;

public sealed class GlobalImportSelectionResult
{
    public bool ImportVerbs { get; set; }
    public bool ImportDirectionals { get; set; }
    public bool ImportGlobalActions { get; set; }
    public bool ImportGlobalSoundEffects { get; set; }
    public bool ImportGlobalEventSubscriptions { get; set; }
    public bool ImportGlobalTimers { get; set; }
    public bool ImportTemplateObjects { get; set; }
    public bool ImportRoomTemplates { get; set; }
    public bool ImportBaseObjects { get; set; }
    public bool ImportGlobalObjects { get; set; }
    public GlobalImportCollisionStrategy CollisionStrategy { get; set; } = GlobalImportCollisionStrategy.KeepExisting;
}
