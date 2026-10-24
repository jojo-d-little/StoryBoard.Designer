namespace StoryboardDesigner.App.Models;

using Storyboard.Shared.DesignerContracts;

public enum EnvironmentalEffectApplicability
{
    Outdoor,
    Indoor,
    Both
}

/// <summary>A named, scope-owned particle effect resolved for rooms in that scope.</summary>
public sealed class EnvironmentalEffectDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string EffectKey { get; set; } = string.Empty;
    public EnvironmentalEffectApplicability Applicability { get; set; } = EnvironmentalEffectApplicability.Outdoor;
    public bool Enabled { get; set; } = true;
    public ProjectGamePropertySourceReferenceDto? EnabledBinding { get; set; }
    public int RenderOrder { get; set; }
    public List<ParticleEffectControlSettingDto> Settings { get; set; } = new();
}
