using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedSoundEffectEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedSoundEffectEntryNodeViewModel(SoundEffectLibraryEntry entry, ScopedSoundEffectsNodeViewModel parent)
        : base(string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.SoundEffectKey : entry.DisplayName, parent)
    {
        Entry = entry;
        ParentSoundEffectsNode = parent;
        NodeTypeLabel = "Sound Effect";
    }

    public SoundEffectLibraryEntry Entry { get; }

    public ScopedSoundEffectsNodeViewModel ParentSoundEffectsNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
