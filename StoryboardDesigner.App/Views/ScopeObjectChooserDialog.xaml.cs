using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class ScopeObjectChooserDialog : Window
{
    private const string AllRoomsFilterValue = "*";
    private readonly List<ScopeObjectChoiceItem> _allChoices;
    private readonly ICollectionView _choicesView;

    public ScopeObjectChooserDialog(
        IReadOnlyList<ScopeObjectChoiceItem> choices,
        string title,
        PropertyResolutionScope? expectedScope = null,
        string? selectedKey = null)
    {
        InitializeComponent();
        Title = title;

        _allChoices = (choices ?? Array.Empty<ScopeObjectChoiceItem>())
            .GroupBy(static choice => choice.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _choicesView = CollectionViewSource.GetDefaultView(_allChoices);
        _choicesView.Filter = ChoiceMatchesFilter;

        ChoicesDataGrid.ItemsSource = _choicesView;
        ConfigureRoomFilterOptions();
        ConfigureScopeTypeDefaults(expectedScope);
        ConfigureScopeDefaults();
        ApplyFilter();

        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            var match = _allChoices.FirstOrDefault(choice =>
                string.Equals(choice.Key, selectedKey, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ChoicesDataGrid.SelectedItem = match;
                ChoicesDataGrid.ScrollIntoView(match);
            }
        }

        ScopePathFilterTextBox.Focus();
        ScopePathFilterTextBox.SelectAll();
    }

    public string? SelectedKey { get; private set; }

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
        MarkScopeFiltersDirty();
    }

    private void ScopeTypeOptions_OnChanged(object sender, RoutedEventArgs e)
    {
        MarkScopeFiltersDirty();
    }

    private void RoomFilterComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        MarkScopeFiltersDirty();
    }

    private void RefreshResultsButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
        RefreshResultsButton.Content = "Refresh";
    }

    private void ApplyFilter()
    {
        var previous = ChoicesDataGrid.SelectedItem as ScopeObjectChoiceItem;
        _choicesView.Refresh();

        if (previous is not null && _choicesView.Cast<ScopeObjectChoiceItem>().Any(item => item == previous))
        {
            ChoicesDataGrid.SelectedItem = previous;
            return;
        }

        if (_choicesView.Cast<ScopeObjectChoiceItem>().Any())
        {
            ChoicesDataGrid.SelectedIndex = 0;
        }
    }

    private bool ChoiceMatchesFilter(object obj)
    {
        if (obj is not ScopeObjectChoiceItem choice)
        {
            return false;
        }

        var scopeFilter = ScopePathFilterTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(scopeFilter)
            && !choice.ScopePath.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ownerFilter = OwnerContextFilterTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(ownerFilter)
            && !choice.OwnerContext.Contains(ownerFilter, StringComparison.OrdinalIgnoreCase)
            && !choice.DisplayName.Contains(ownerFilter, StringComparison.OrdinalIgnoreCase))
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

        if (!IsScopeEnabled(choice.Scope))
        {
            return false;
        }

        var relations = (choice.Relations ?? Array.Empty<GamePropertyChoiceRelation>())
            .Distinct()
            .ToList();

        if (relations.Count == 0)
        {
            return true;
        }

        return relations.Any(IsRelationEnabled);
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
        if (ChoicesDataGrid.SelectedItem is not ScopeObjectChoiceItem choice)
        {
            return;
        }

        SelectedKey = choice.Key;
        DialogResult = true;
    }

    private void ConfigureScopeDefaults()
    {
        IncludeParentCheckBox.IsChecked = true;
        IncludeAncestorsCheckBox.IsChecked = true;
        IncludeChildrenCheckBox.IsChecked = true;
        IncludeDescendantsCheckBox.IsChecked = true;
        IncludeSiblingsCheckBox.IsChecked = true;
        IncludeTraversalLegsCheckBox.IsChecked = true;
    }

    private void ConfigureScopeTypeDefaults(PropertyResolutionScope? expectedScope)
    {
        IncludeGlobalCheckBox.IsChecked = true;
        IncludePlanetCheckBox.IsChecked = true;
        IncludeCountryCheckBox.IsChecked = true;
        IncludeAreaCheckBox.IsChecked = true;
        IncludeRoomCheckBox.IsChecked = true;
        IncludeObjectCheckBox.IsChecked = true;

        if (expectedScope is not PropertyResolutionScope scope)
        {
            return;
        }

        IncludeGlobalCheckBox.IsChecked = scope == PropertyResolutionScope.Global;
        IncludePlanetCheckBox.IsChecked = scope == PropertyResolutionScope.Planet;
        IncludeCountryCheckBox.IsChecked = scope == PropertyResolutionScope.Country;
        IncludeAreaCheckBox.IsChecked = scope == PropertyResolutionScope.Area;
        IncludeRoomCheckBox.IsChecked = scope == PropertyResolutionScope.Room;
        IncludeObjectCheckBox.IsChecked = scope == PropertyResolutionScope.Object;
    }

    private bool IsScopeEnabled(PropertyResolutionScope scope)
    {
        var anyEnabled = IncludeGlobalCheckBox.IsChecked == true
                         || IncludePlanetCheckBox.IsChecked == true
                         || IncludeCountryCheckBox.IsChecked == true
                         || IncludeAreaCheckBox.IsChecked == true
                         || IncludeRoomCheckBox.IsChecked == true
                         || IncludeObjectCheckBox.IsChecked == true;

        if (!anyEnabled)
        {
            return true;
        }

        return scope switch
        {
            PropertyResolutionScope.Global => IncludeGlobalCheckBox.IsChecked == true,
            PropertyResolutionScope.Planet => IncludePlanetCheckBox.IsChecked == true,
            PropertyResolutionScope.Country => IncludeCountryCheckBox.IsChecked == true,
            PropertyResolutionScope.Area => IncludeAreaCheckBox.IsChecked == true,
            PropertyResolutionScope.Room => IncludeRoomCheckBox.IsChecked == true,
            PropertyResolutionScope.Object => IncludeObjectCheckBox.IsChecked == true,
            _ => true
        };
    }

    private bool IsRelationEnabled(GamePropertyChoiceRelation relation)
    {
        return relation switch
        {
            GamePropertyChoiceRelation.Self => true,
            GamePropertyChoiceRelation.Parent => IncludeParentCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Ancestor => IncludeAncestorsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Child => IncludeChildrenCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Descendant => IncludeDescendantsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Sibling => IncludeSiblingsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.TraversalLeg => IncludeTraversalLegsCheckBox.IsChecked == true,
            _ => true
        };
    }

    private void MarkScopeFiltersDirty()
    {
        RefreshResultsButton.Content = "Refresh*";
    }
}
