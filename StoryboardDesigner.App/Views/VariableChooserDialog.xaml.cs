using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class VariableChooserDialog : Window
{
    private const string AllRoomsFilterValue = "*";
    private readonly List<GamePropertyChoiceItem> _allChoices;
    private readonly ICollectionView _choicesView;
    private readonly PropertyResolutionScope _initializationScope;

    public VariableChooserDialog(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        PropertyResolutionScope initializationScope,
        string title,
        string? selectedValue = null)
    {
        InitializeComponent();
        Title = title;
        _initializationScope = initializationScope;
        _allChoices = BuildDistinctDisplayChoices(choices, selectedValue);
        _choicesView = CollectionViewSource.GetDefaultView(_allChoices);
        _choicesView.Filter = ChoiceMatchesFilter;

        ChoicesDataGrid.ItemsSource = _choicesView;
        ConfigureRoomFilterOptions();

        ConfigureScopeDefaults();

        ApplyFilter();

        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            var match = _allChoices.FirstOrDefault(choice => string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ChoicesDataGrid.SelectedItem = match;
                ChoicesDataGrid.ScrollIntoView(match);
            }
        }

        ScopePathFilterTextBox.Focus();
        ScopePathFilterTextBox.SelectAll();
    }

    public string? SelectedValue { get; private set; }

    private void Filters_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void FilterTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Down)
        {
            ChoicesDataGrid.Focus();
            if (ChoicesDataGrid.Items.Count > 0 && ChoicesDataGrid.SelectedIndex < 0)
            {
                ChoicesDataGrid.SelectedIndex = 0;
            }

            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (ChoicesDataGrid.SelectedItem is null && ChoicesDataGrid.Items.Count > 0)
            {
                ChoicesDataGrid.SelectedIndex = 0;
            }

            TrySelectCurrent();
            e.Handled = true;
        }
    }

    private void ChoicesDataGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TrySelectCurrent();
    }

    private void ChoicesDataGrid_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            TrySelectCurrent();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        TrySelectCurrent();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ScopeOptions_OnChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void RoomFilterComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var previous = ChoicesDataGrid.SelectedItem as GamePropertyChoiceItem;
        _choicesView.Refresh();

        if (previous is not null && _choicesView.Cast<GamePropertyChoiceItem>().Any(item => item == previous))
        {
            ChoicesDataGrid.SelectedItem = previous;
            return;
        }

        if (_choicesView.Cast<GamePropertyChoiceItem>().Any())
        {
            ChoicesDataGrid.SelectedIndex = 0;
        }
    }

    private bool ChoiceMatchesFilter(object obj)
    {
        if (obj is not GamePropertyChoiceItem choice)
        {
            return false;
        }

        var scopeFilter = ScopePathFilterTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(scopeFilter)
            && !choice.ScopePath.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ownerFilter = OwnerVariableFilterTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(ownerFilter)
            && !choice.OwnerVariable.Contains(ownerFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selectedRoom = RoomFilterComboBox.SelectedValue as string;
        if (!string.IsNullOrWhiteSpace(selectedRoom)
            && !string.Equals(selectedRoom, AllRoomsFilterValue, StringComparison.Ordinal)
            && !string.Equals(ExtractRoomName(choice.ScopePath), selectedRoom, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsRelationEnabled(choice.Relation))
        {
            return false;
        }

        return true;
    }

    private void ConfigureRoomFilterOptions()
    {
        var roomNames = _allChoices
            .Select(choice => ExtractRoomName(choice.ScopePath))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var options = new List<RoomFilterOption>
        {
            new("All rooms", AllRoomsFilterValue)
        };

        options.AddRange(roomNames.Select(roomName => new RoomFilterOption(roomName, roomName)));
        RoomFilterComboBox.ItemsSource = options;
        RoomFilterComboBox.SelectedValue = AllRoomsFilterValue;
    }

    private static string ExtractRoomName(string scopePath)
    {
        if (string.IsNullOrWhiteSpace(scopePath))
        {
            return string.Empty;
        }

        var parts = scopePath.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return string.Empty;
        }

        return parts[3];
    }

    private sealed record RoomFilterOption(string Label, string Value);

    private void TrySelectCurrent()
    {
        if (ChoicesDataGrid.SelectedItem is not GamePropertyChoiceItem choice)
        {
            return;
        }

        SelectedValue = choice.Value;
        DialogResult = true;
    }

    private void ConfigureScopeDefaults()
    {
        var isObjectScope = _initializationScope == PropertyResolutionScope.Object;
        ScopeOptionsPanel.Visibility = isObjectScope ? Visibility.Visible : Visibility.Collapsed;

        if (isObjectScope)
        {
            IncludeParentCheckBox.IsChecked = false;
            IncludeAncestorsCheckBox.IsChecked = false;
            IncludeSiblingsCheckBox.IsChecked = false;
            IncludeTraversalLegsCheckBox.IsChecked = true;
            return;
        }

        IncludeParentCheckBox.IsChecked = true;
        IncludeAncestorsCheckBox.IsChecked = true;
        IncludeSiblingsCheckBox.IsChecked = true;
        IncludeTraversalLegsCheckBox.IsChecked = true;
    }

    private bool IsRelationEnabled(GamePropertyChoiceRelation relation)
    {
        return relation switch
        {
            GamePropertyChoiceRelation.Self => true,
            GamePropertyChoiceRelation.Parent => IncludeParentCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Ancestor => IncludeAncestorsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Child => true,
            GamePropertyChoiceRelation.Descendant => true,
            GamePropertyChoiceRelation.Sibling => IncludeSiblingsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.TraversalLeg => IncludeTraversalLegsCheckBox.IsChecked == true,
            _ => true
        };
    }

    private static List<GamePropertyChoiceItem> BuildDistinctDisplayChoices(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        string? selectedValue)
    {
        return choices
            .GroupBy(
                static choice => BuildDisplayKey(choice),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => SelectPreferredChoice(group, selectedValue))
            .OrderBy(static choice => choice.Priority)
            .ThenBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDisplayKey(GamePropertyChoiceItem choice)
    {
        return $"{choice.ScopePath}|{choice.OwnerVariable}";
    }

    private static GamePropertyChoiceItem SelectPreferredChoice(
        IEnumerable<GamePropertyChoiceItem> choices,
        string? selectedValue)
    {
        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            var selectedMatch = choices.FirstOrDefault(choice =>
                string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (selectedMatch is not null)
            {
                return selectedMatch;
            }
        }

        return choices
            .OrderBy(static choice => choice.Priority)
            .ThenBy(static choice => choice.Value, StringComparer.OrdinalIgnoreCase)
            .First();
    }
}
