namespace StoryboardDesigner.App.ViewModels;

public sealed class ChildrenGroupNodeViewModel : GroupContainerNodeViewModel
{
    public ChildrenGroupNodeViewModel(HierarchyNodeViewModel ownerNode)
        : base(GetDisplayName(ownerNode), ownerNode)
    {
        NodeTypeLabel = GetDisplayName(ownerNode);
    }

    private static string GetDisplayName(HierarchyNodeViewModel ownerNode)
    {
        return ownerNode switch
        {
            PlanetNodeViewModel => "Countries",
            CountryNodeViewModel => "Areas",
            AreaNodeViewModel => "Rooms",
            GameObjectNodeViewModel => "Contained Objects",
            GlobalObjectNodeViewModel => "Contained Objects",
            TemplateGameObjectNodeViewModel => "Contained Objects",
            _ => "Children"
        };
    }
}
