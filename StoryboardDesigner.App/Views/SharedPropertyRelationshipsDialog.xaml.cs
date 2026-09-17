using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class SharedPropertyRelationshipsDialog : Window
{
    private readonly Action? _createSharedRelationshipAction;
    private readonly Func<SharedPropertyRelationshipReviewItem, bool>? _removeSharedRelationshipAction;
    private readonly Func<IReadOnlyList<SharedPropertyRelationshipReviewItem>>? _reloadRelationships;
    private readonly Guid? _sharedVariableId;
    private readonly Func<Guid, string, bool>? _renameSharedVariableAction;
    private readonly Action? _openSharedVariablesManagerAction;
    private readonly List<SharedPropertyRelationshipReviewItem> _relationships;

    public SharedPropertyRelationshipsDialog(
        string propertyPath,
        IReadOnlyList<SharedPropertyRelationshipReviewItem> relationships,
        Action? createSharedRelationshipAction = null,
        Func<SharedPropertyRelationshipReviewItem, bool>? removeSharedRelationshipAction = null,
        Func<IReadOnlyList<SharedPropertyRelationshipReviewItem>>? reloadRelationships = null,
        Guid? sharedVariableId = null,
        string? sharedVariableDisplayName = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Action? openSharedVariablesManagerAction = null,
        string? shareStateCallout = null)
    {
        InitializeComponent();
        _createSharedRelationshipAction = createSharedRelationshipAction;
        _removeSharedRelationshipAction = removeSharedRelationshipAction;
        _reloadRelationships = reloadRelationships;
        _sharedVariableId = sharedVariableId;
        _renameSharedVariableAction = renameSharedVariableAction;
        _openSharedVariablesManagerAction = openSharedVariablesManagerAction;
        _relationships = (relationships ?? Array.Empty<SharedPropertyRelationshipReviewItem>()).ToList();

        PropertyPathTextBlock.Text = $"Property: {propertyPath}";
        RelationshipsDataGrid.ItemsSource = _relationships;

        var hasRelationships = _relationships.Count > 0;
        var hasWarnings = _relationships.Any(static relationship =>
            string.Equals(relationship.EnabledState, "Warning", StringComparison.OrdinalIgnoreCase));
        EmptyStateTextBlock.Visibility = hasRelationships ? Visibility.Collapsed : Visibility.Visible;
        RelationshipsDataGrid.Visibility = hasRelationships ? Visibility.Visible : Visibility.Collapsed;
        CreateShareRelationshipButton.Visibility = _createSharedRelationshipAction is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        RemoveShareRelationshipButton.Visibility = _removeSharedRelationshipAction is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        OpenSharedVariablesManagerButton.Visibility = _openSharedVariablesManagerAction is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        SharedVariableNameEditorPanel.Visibility = _renameSharedVariableAction is not null && _sharedVariableId.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;
        SharedVariableDisplayNameTextBox.Text = sharedVariableDisplayName ?? string.Empty;
        RemoveShareRelationshipButton.IsEnabled = false;
        RuntimeDiagnosticsStatusTextBlock.Text = hasRelationships
            ? hasWarnings
                ? "Shared warnings detected. Review relationship rows for repair guidance."
                : "No runtime diagnostics yet"
            : "No runtime diagnostics yet";

        if (!string.IsNullOrWhiteSpace(shareStateCallout))
        {
            ShareStateCalloutTextBlock.Text = shareStateCallout.Trim();
            ShareStateCalloutTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CreateShareRelationship_OnClick(object sender, RoutedEventArgs e)
    {
        _createSharedRelationshipAction?.Invoke();
        RefreshRelationships();
    }

    private void RemoveShareRelationship_OnClick(object sender, RoutedEventArgs e)
    {
        if (RelationshipsDataGrid.SelectedItem is not SharedPropertyRelationshipReviewItem selected
            || _removeSharedRelationshipAction is null)
        {
            return;
        }

        if (!_removeSharedRelationshipAction.Invoke(selected))
        {
            return;
        }

        RefreshRelationships();
    }

    private void RelationshipsDataGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RemoveShareRelationshipButton.IsEnabled = RelationshipsDataGrid.SelectedItem is SharedPropertyRelationshipReviewItem;
    }

    private void OpenSharedVariablesManager_OnClick(object sender, RoutedEventArgs e)
    {
        _openSharedVariablesManagerAction?.Invoke();
    }

    private void RenameSharedVariable_OnClick(object sender, RoutedEventArgs e)
    {
        if (_renameSharedVariableAction is null || !_sharedVariableId.HasValue)
        {
            return;
        }

        _renameSharedVariableAction.Invoke(_sharedVariableId.Value, SharedVariableDisplayNameTextBox.Text ?? string.Empty);
    }

    private void RefreshRelationships()
    {
        if (_reloadRelationships is null)
        {
            return;
        }

        _relationships.Clear();
        _relationships.AddRange(_reloadRelationships.Invoke());
        RelationshipsDataGrid.Items.Refresh();

        var hasRelationships = _relationships.Count > 0;
        EmptyStateTextBlock.Visibility = hasRelationships ? Visibility.Collapsed : Visibility.Visible;
        RelationshipsDataGrid.Visibility = hasRelationships ? Visibility.Visible : Visibility.Collapsed;
    }
}
