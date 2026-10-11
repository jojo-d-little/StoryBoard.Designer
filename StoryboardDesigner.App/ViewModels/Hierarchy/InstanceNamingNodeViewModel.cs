using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class InstanceNamingNodeViewModel : HierarchyNodeViewModel
{
    public InstanceNamingNodeViewModel(GameObject instance, HierarchyNodeViewModel parent)
        : base("Instance Naming", parent)
    {
        Instance = instance;
        NodeTypeLabel = "Instance Naming Link";
    }

    public GameObject Instance { get; }
}
