namespace StoryboardDesigner.App.Models;

/// <summary>Located instance that plays a reusable sound definition from this object.</summary>
public sealed class ObjectSoundEmitter
{
    public Guid EmitterId { get; set; } = Guid.NewGuid();
    public Guid SoundEffectId { get; set; }
    public string? ActivationGamePropertyName { get; set; }
    public bool ActiveInRoom { get; set; } = true;
    public bool ActiveOutOfRoom { get; set; }
}
