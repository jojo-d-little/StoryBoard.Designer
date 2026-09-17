using System.ComponentModel;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class TemplateGameObjectNodeViewModel : HierarchyNodeViewModel
{
    public TemplateGameObjectNodeViewModel(GameObject interactiveObject, ObjectTemplatesNodeViewModel parent)
        : base(interactiveObject.Name, parent)
    {
        GameObject = interactiveObject;
        ParentTemplatesNode = parent;
        ParentObjectNode = null;
        IsNestedObject = false;
        NodeTypeLabel = parent.IsBaseCatalog ? "Base Object" : "Template Object";
        GameObject.PropertyChanged += OnGameObjectPropertyChanged;
    }

    public TemplateGameObjectNodeViewModel(GameObject interactiveObject, TemplateGameObjectNodeViewModel parent)
        : base(interactiveObject.Name, parent)
    {
        GameObject = interactiveObject;
        ParentTemplatesNode = parent.ParentTemplatesNode;
        ParentObjectNode = parent;
        IsNestedObject = true;
        NodeTypeLabel = parent.ParentTemplatesNode.IsBaseCatalog ? "Base Object" : "Template Object";
        GameObject.PropertyChanged += OnGameObjectPropertyChanged;
    }

    public GameObject GameObject { get; }
    public ObjectTemplatesNodeViewModel ParentTemplatesNode { get; }
    public TemplateGameObjectNodeViewModel? ParentObjectNode { get; }
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
