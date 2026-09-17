namespace StoryboardDesigner.App.ViewModels;

public sealed class ConfigurationGroupNodeViewModel : GroupContainerNodeViewModel
{
    public ConfigurationGroupNodeViewModel(HierarchyNodeViewModel ownerNode)
        : base("Configuration", ownerNode)
    {
        NodeTypeLabel = "Configuration";
    }
}
