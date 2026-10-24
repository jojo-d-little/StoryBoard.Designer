namespace StoryboardDesigner.App.Models;

/// <summary>Project artwork override for one catalog effect image slot.</summary>
public sealed class ParticleEffectImageChoice
{
    public string EffectKey { get; set; } = string.Empty;
    public string ImageSlot { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
}
