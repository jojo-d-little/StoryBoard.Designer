namespace StoryboardDesigner.App.Models;

/// <summary>Stores the object identity targeted by a TriggerParticleEffect action.</summary>
/// <param name="ObjectId">The selected object whose attached particle effect supplies the burst recipe.</param>
public sealed record TriggerParticleEffectPayload(Guid? ObjectId) : IActionPayload
{
    /// <summary>Gets the action type represented by this payload.</summary>
    public CommandActionType ActionType => CommandActionType.TriggerParticleEffect;
}
