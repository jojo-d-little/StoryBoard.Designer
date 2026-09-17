namespace StoryboardDesigner.App.Models;

public sealed class OutcomeSoundEffectCue
{
    public Guid SoundEffectId { get; set; }

    public string? SoundEffectKeyHint { get; set; }

    public bool? Enabled { get; set; } = true;

    public Storyboard.Shared.RuntimeContracts.Enums.PresentationCueLateDeliveryPolicy? LateDeliveryPolicy { get; set; }

    public int? MaxLateMs { get; set; }
}
