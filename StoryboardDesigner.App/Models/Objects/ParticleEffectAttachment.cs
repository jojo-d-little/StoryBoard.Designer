namespace StoryboardDesigner.App.Models;

using Storyboard.Shared.DesignerContracts;

/// <summary>One catalog-selected particle effect attached to a game object.</summary>
public sealed class ParticleEffectAttachment
{
    public string EffectKey { get; set; } = string.Empty;
    public double? OffsetXPx { get; set; }
    public double? OffsetYPx { get; set; }
    public List<ParticleEffectControlSettingDto> Settings { get; set; } = new();
}
