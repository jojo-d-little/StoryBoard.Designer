namespace StoryboardDesigner.App.Services;

/// <summary>
/// Describes one authoring-time text presentation cue option loaded from the cue catalog.
/// </summary>
public sealed record PhaseTextPresentationCueOption(
    string EffectKey,
    string DisplayName,
    string Where,
    string? How,
    int? DurationMs);
