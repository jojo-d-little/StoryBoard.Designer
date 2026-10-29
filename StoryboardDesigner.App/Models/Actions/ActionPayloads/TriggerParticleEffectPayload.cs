using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

/// <summary>Stores the target and optional gameplay quantity controls for a TriggerParticleEffect action.</summary>
/// <param name="ObjectId">The selected object whose attached particle effect supplies the burst recipe.</param>
/// <param name="QuantityMultiplier">The literal fallback multiplier; omitted bindings resolve to this value, defaulting to one.</param>
/// <param name="QuantityMultiplierBinding">An optional direct numeric source sampled when the action executes.</param>
public sealed record TriggerParticleEffectPayload(
    Guid? ObjectId,
    double? QuantityMultiplier = 1,
    ProjectGamePropertySourceReferenceDto? QuantityMultiplierBinding = null) : IActionPayload
{
    /// <summary>Gets the action type represented by this payload.</summary>
    public CommandActionType ActionType => CommandActionType.TriggerParticleEffect;
}
