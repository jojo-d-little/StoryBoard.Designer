using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedActionsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedActionsNodeViewModel(PropertyResolutionScope scope, IList<CommandAction> actions, HierarchyNodeViewModel parent)
        : base("Game Actions", parent)
    {
        Scope = scope;
        Actions = actions;
        NodeTypeLabel = "Game Actions";
    }

    public PropertyResolutionScope Scope { get; }
    public IList<CommandAction> Actions { get; }

    protected override void RenameModel(string newName)
    {
    }
}
