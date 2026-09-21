using System.ComponentModel;

namespace StoryboardDesigner.App.ViewModels;

public sealed class HierarchyQuickAccessItemViewModel : ViewModelBase, IDisposable
{
    private readonly HierarchyNodeViewModel? _node;
    private readonly Action<string>? _displayNameChanged;
    private string _displayName;

    public HierarchyQuickAccessItemViewModel(
        string nodePath,
        string displayName,
        string nodeTypeLabel,
        string context,
        bool isAvailable)
    {
        NodePath = nodePath;
        _displayName = displayName;
        NodeTypeLabel = nodeTypeLabel;
        Context = context;
        IsAvailable = isAvailable;
    }

    internal HierarchyQuickAccessItemViewModel(
        string nodePath,
        string displayName,
        string nodeTypeLabel,
        string context,
        HierarchyNodeViewModel node,
        Action<string> displayNameChanged)
        : this(nodePath, displayName, nodeTypeLabel, context, isAvailable: true)
    {
        _node = node;
        _displayNameChanged = displayNameChanged;
        _node.PropertyChanged += Node_OnPropertyChanged;
    }

    public string NodePath { get; }
    public string DisplayName => _displayName;
    public string NodeTypeLabel { get; }
    public string Context { get; }
    public bool IsAvailable { get; }
    public string AvailabilityToolTip => IsAvailable
        ? $"Go to {DisplayName} in the Project Hierarchy"
        : "This hierarchy node is no longer available in the project.";

    public void Dispose()
    {
        if (_node is not null)
        {
            _node.PropertyChanged -= Node_OnPropertyChanged;
        }
    }

    private void Node_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_node is null
            || (!string.IsNullOrEmpty(e.PropertyName)
                && !string.Equals(e.PropertyName, nameof(HierarchyNodeViewModel.EditableName), StringComparison.Ordinal)))
        {
            return;
        }

        var updatedName = _node.EditableName;
        if (string.Equals(_displayName, updatedName, StringComparison.Ordinal))
        {
            return;
        }

        _displayName = updatedName;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AvailabilityToolTip));
        _displayNameChanged?.Invoke(updatedName);
    }
}
