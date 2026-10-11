using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GoToBaseObjectNodeViewModel : HierarchyNodeViewModel
{
    public GoToBaseObjectNodeViewModel(GameObject instance, HierarchyNodeViewModel parent)
        : base("Go to My Base", parent)
    {
        Instance = instance;
        NodeTypeLabel = "Base Object Link";
    }

    public GameObject Instance { get; }
}
