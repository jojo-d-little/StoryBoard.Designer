namespace StoryboardDesigner.App.ViewModels;

public sealed class GameObjectSettingsNodeViewModel : HierarchyNodeViewModel
{
    public GameObjectSettingsNodeViewModel(GameObjectNodeViewModel objectNode)
        : base("Game Object Settings", objectNode)
    {
        ObjectNode = objectNode;
        NodeTypeLabel = "Game Object Settings";
    }

    public GameObjectSettingsNodeViewModel(GlobalObjectNodeViewModel objectNode)
        : base("Game Object Settings", objectNode)
    {
        ObjectNode = objectNode;
        NodeTypeLabel = "Game Object Settings";
    }

    public GameObjectSettingsNodeViewModel(TemplateGameObjectNodeViewModel objectNode)
        : base("Game Object Settings", objectNode)
    {
        ObjectNode = objectNode;
        NodeTypeLabel = "Game Object Settings";
    }

    public HierarchyNodeViewModel ObjectNode { get; }

    protected override void RenameModel(string newName)
    {
    }
}
