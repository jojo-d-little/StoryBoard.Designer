using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedEnvironmentalEffectEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedEnvironmentalEffectEntryNodeViewModel(EnvironmentalEffectDefinition effect, ScopedEnvironmentalEffectsNodeViewModel parent)
        : base(string.IsNullOrWhiteSpace(effect.Name) ? effect.EffectKey : effect.Name, parent)
    {
        Effect = effect;
        ParentEffectsNode = parent;
        NodeTypeLabel = "Environmental Effect";
    }

    public EnvironmentalEffectDefinition Effect { get; }
    public ScopedEnvironmentalEffectsNodeViewModel ParentEffectsNode { get; }

    protected override void RenameModel(string newName) { }
}
