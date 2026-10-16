using StoryboardDesigner.App.Models;
using Storyboard.Shared.RuntimeContracts.Enums;

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
    PhaseAmbienceMode? PhaseAmbienceMode,
    PhaseAmbientOverlay? PhaseAmbientOverlay,
    TimeOfDay? TimeOfDay,
    TimeOfYear? TimeOfYear,
    SkyCondition? SkyCondition,
    Precipitation? Precipitation,
    WeatherIntensity? WeatherIntensity,
    Temperature? Temperature,
    Storming? Storming,
    int? WindDirection);
