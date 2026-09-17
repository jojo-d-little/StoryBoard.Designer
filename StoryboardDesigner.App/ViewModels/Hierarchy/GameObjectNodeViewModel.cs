using System.ComponentModel;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class GameObjectNodeViewModel : HierarchyNodeViewModel
{
    public GameObjectNodeViewModel(GameObject interactiveObject, GameObjectsNodeViewModel parent)
        : base(interactiveObject.Name, parent)
    {
        GameObject = interactiveObject;
        ParentObjectsNode = parent;
        ParentObjectNode = null;
        IsNestedObject = false;
        NodeTypeLabel = "Game Object";
        GameObject.PropertyChanged += OnGameObjectPropertyChanged;
    }

    public GameObjectNodeViewModel(GameObject interactiveObject, GameObjectNodeViewModel parent)
        : base(interactiveObject.Name, parent)
    {
        GameObject = interactiveObject;
        ParentObjectsNode = parent.ParentObjectsNode;
        ParentObjectNode = parent;
        IsNestedObject = true;
        NodeTypeLabel = "Game Object";
        GameObject.PropertyChanged += OnGameObjectPropertyChanged;
    }

    public GameObject GameObject { get; }
    public GameObjectsNodeViewModel ParentObjectsNode { get; }
    public GameObjectNodeViewModel? ParentObjectNode { get; }
    public bool IsNestedObject { get; }
    public string EffectiveNameInGameDisplay => GameObject.NameInGame?.Trim() ?? string.Empty;

    public bool HasEffectiveNameInGameDisplay => !string.IsNullOrWhiteSpace(EffectiveNameInGameDisplay);

    private void OnGameObjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(GameObject.NameInGame), StringComparison.Ordinal)
            || string.IsNullOrEmpty(e.PropertyName))
        {
            OnPropertyChanged(nameof(EffectiveNameInGameDisplay));
            OnPropertyChanged(nameof(HasEffectiveNameInGameDisplay));
        }
    }

    protected override void RenameModel(string newName)
    {
        GameObject.Name = newName;
    }
}
