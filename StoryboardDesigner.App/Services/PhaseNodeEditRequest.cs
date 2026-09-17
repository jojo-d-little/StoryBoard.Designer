namespace StoryboardDesigner.App.Services;

public sealed record PhaseNodeEditRequest(
    PhaseTier Tier,
    string DisplayName,
    string PhaseKey,
    string? Title,
    string? TitlePresentationCueEffectKey,
    string? Prologue,
    string? ProloguePresentationCueEffectKey,
    string? Narrative,
    string? NarrativePresentationCueEffectKey,
    Guid? PhaseAmbientSoundEffectId,
    string? PhaseAmbientTimerKey,
    PhaseAmbienceMode? PhaseAmbienceMode);
