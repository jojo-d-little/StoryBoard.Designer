using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedSoundEffectsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedSoundEffectsNodeViewModel(PropertyResolutionScope scope, IList<SoundEffectLibraryEntry> soundEffects, HierarchyNodeViewModel parent)
        : base("Sound Effects", parent)
    {
        Scope = scope;
        SoundEffects = soundEffects;
        NodeTypeLabel = "Sound Effects";
    }

    public PropertyResolutionScope Scope { get; }

    public IList<SoundEffectLibraryEntry> SoundEffects { get; }

    protected override void RenameModel(string newName)
    {
    }
}
