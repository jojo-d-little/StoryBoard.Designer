using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedEnvironmentalEffectsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedEnvironmentalEffectsNodeViewModel(PropertyResolutionScope scope, IList<EnvironmentalEffectDefinition> effects, HierarchyNodeViewModel parent)
        : base("Environmental Effects", parent)
    {
        Scope = scope;
        Effects = effects;
        NodeTypeLabel = "Environmental Effects";
    }

    public PropertyResolutionScope Scope { get; }
    public IList<EnvironmentalEffectDefinition> Effects { get; }

    protected override void RenameModel(string newName) { }
}
