using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

internal static class ParticleEffectControlSettingCloner
{
    public static List<ParticleEffectControlSettingDto> CloneSettings(IEnumerable<ParticleEffectControlSettingDto>? settings) =>
        (settings ?? Array.Empty<ParticleEffectControlSettingDto>()).Select(CloneSetting).ToList();

    public static ParticleEffectControlSettingDto CloneSetting(ParticleEffectControlSettingDto setting) => new()
    {
        ControlKey = setting.ControlKey,
        Value = setting.Value,
        Binding = CloneReference(setting.Binding)
    };

    public static ProjectGamePropertySourceReferenceDto? CloneReference(ProjectGamePropertySourceReferenceDto? source) => source is null
        ? null
        : new ProjectGamePropertySourceReferenceDto
        {
            OwnerId = source.OwnerId,
            AnchorKey = source.AnchorKey,
            PropertyName = source.PropertyName
        };

    public static EnvironmentalEffectDefinition CloneEnvironmentalEffect(EnvironmentalEffectDefinition source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        EffectKey = source.EffectKey,
        Applicability = source.Applicability,
        Enabled = source.Enabled,
        EnabledBinding = CloneReference(source.EnabledBinding),
        RenderOrder = source.RenderOrder,
        Settings = CloneSettings(source.Settings)
    };
}
