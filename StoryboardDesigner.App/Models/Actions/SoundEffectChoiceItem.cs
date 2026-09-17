namespace StoryboardDesigner.App.Models;

public enum SoundEffectScopeRelation
{
    Current,
    UpScope,
    DownScope
}

public sealed class SoundEffectChoiceItem
{
    public Guid SoundEffectId { get; init; }

    public string SoundEffectKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string AssetRef { get; init; } = string.Empty;

    public string ScopePath { get; init; } = string.Empty;

    public string SourceCategory { get; init; } = string.Empty;

    public SoundEffectScopeRelation ScopeRelation { get; init; } = SoundEffectScopeRelation.Current;

    public SoundEffectLane SoundEffectLane { get; init; } = SoundEffectLane.Sfx;
}
