using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class SharedVariablesManagerDialog : Window
{
    private readonly Func<Guid, string, bool>? _renameSharedVariableAction;
    private readonly Func<Guid, bool>? _dropEmptySharedVariableAction;
    private readonly Func<IReadOnlyList<SharedVariableManagerListItem>>? _reloadItemsAction;

    public SharedVariablesManagerDialog(
        IReadOnlyList<SharedVariableManagerListItem> items,
        Guid? selectedSharedVariableId = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Func<Guid, bool>? dropEmptySharedVariableAction = null,
        Func<IReadOnlyList<SharedVariableManagerListItem>>? reloadItemsAction = null)
    {
        InitializeComponent();
        _renameSharedVariableAction = renameSharedVariableAction;
        _dropEmptySharedVariableAction = dropEmptySharedVariableAction;
        _reloadItemsAction = reloadItemsAction;

        RenameSelectedButton.Visibility = _renameSharedVariableAction is null ? Visibility.Collapsed : Visibility.Visible;
        DropEmptySelectedButton.Visibility = _dropEmptySharedVariableAction is null ? Visibility.Collapsed : Visibility.Visible;
        SelectedDisplayNameTextBox.IsEnabled = _renameSharedVariableAction is not null;

        var safeItems = (items ?? Array.Empty<SharedVariableManagerListItem>()).ToList();
        SharedVariablesDataGrid.ItemsSource = safeItems;

        RefreshSummaryAndSelection(selectedSharedVariableId);
    }

    private void RefreshSummaryAndSelection(Guid? selectedSharedVariableId)
    {
        var safeItems = SharedVariablesDataGrid.ItemsSource is IEnumerable<SharedVariableManagerListItem> source
            ? source.ToList()
            : new List<SharedVariableManagerListItem>();

        if (safeItems.Count == 0)
        {
            SummaryTextBlock.Text = "No shared variables are currently defined.";
            SelectedDisplayNameTextBox.Text = string.Empty;
            return;
        }

        var emptyCount = safeItems.Count(item => item.ParticipantCount == 0);
        var singletonCount = safeItems.Count(item => item.ParticipantCount == 1);
        SummaryTextBlock.Text = $"Total: {safeItems.Count}. Empty: {emptyCount}. Single Participant: {singletonCount}.";

        SharedVariableManagerListItem? selected = null;
        if (selectedSharedVariableId.HasValue)
        {
            selected = safeItems.FirstOrDefault(item => item.Id == selectedSharedVariableId.Value);
        }

        selected ??= safeItems.FirstOrDefault();
        if (selected is not null)
        {
            SharedVariablesDataGrid.SelectedItem = selected;
            SharedVariablesDataGrid.ScrollIntoView(selected);
            SelectedDisplayNameTextBox.Text = selected.DisplayName;
        }
    }

    private void SharedVariablesDataGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SharedVariablesDataGrid.SelectedItem is SharedVariableManagerListItem selected)
        {
            SelectedDisplayNameTextBox.Text = selected.DisplayName;
        }
    }

    private void RenameSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (_renameSharedVariableAction is null
            || SharedVariablesDataGrid.SelectedItem is not SharedVariableManagerListItem selected)
        {
            return;
        }

        if (!_renameSharedVariableAction.Invoke(selected.Id, SelectedDisplayNameTextBox.Text ?? string.Empty))
        {
            return;
        }

        Reload(selected.Id);
    }

    private void DropEmptySelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (_dropEmptySharedVariableAction is null
            || SharedVariablesDataGrid.SelectedItem is not SharedVariableManagerListItem selected)
        {
            return;
        }

        if (!_dropEmptySharedVariableAction.Invoke(selected.Id))
        {
            return;
        }

        Reload(null);
    }

    private void Reload(Guid? preferredSelectionId)
    {
        if (_reloadItemsAction is null)
        {
            return;
        }

        SharedVariablesDataGrid.ItemsSource = (_reloadItemsAction.Invoke() ?? Array.Empty<SharedVariableManagerListItem>()).ToList();
        RefreshSummaryAndSelection(preferredSelectionId);
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
