using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedActionEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedActionEntryNodeViewModel(CommandAction action, ScopedActionsNodeViewModel parent)
        : base(string.IsNullOrWhiteSpace(action.Name) ? "Action" : action.Name, parent)
    {
        Action = action;
        ParentActionsNode = parent;
        NodeTypeLabel = "Action";
    }

    public CommandAction Action { get; }
    public ScopedActionsNodeViewModel ParentActionsNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
