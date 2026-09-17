namespace StoryboardDesigner.App.Models;

public sealed class SoundEffectLibraryEntry
{
    public Guid SoundEffectId { get; set; } = Guid.NewGuid();
    public string SoundEffectKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string AssetRef { get; set; } = string.Empty;
    public SoundEffectLane SoundEffectLane { get; set; } = SoundEffectLane.Sfx;
    public double? BaseVolumeDb { get; set; }
    public int? FadeInMs { get; set; }
    public int? FadeOutMs { get; set; }
    public string RepeatMode { get; set; } = "None";
    public string ReplayPolicy { get; set; } = "PlayAgain";
    public int? RepeatCount { get; set; }
    public int? StartDelayMs { get; set; }
    public int? RepeatIntervalMs { get; set; }
    public int? DurationMs { get; set; }
    public int? MaxPlayDurationMs { get; set; }
    public int? RepeatDurationMs { get; set; }
    public int? RepeatCooldownMs { get; set; }
    public string? ConcurrencyGroup { get; set; }
    public int? ConcurrencyGroupImportance { get; set; }
    public int? Importance { get; set; }
}