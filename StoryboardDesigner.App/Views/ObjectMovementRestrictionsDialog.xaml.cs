using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class ObjectMovementRestrictionsDialog : Window
{
    private readonly ObjectMovementRestrictions _working;
    private readonly ObservableCollection<DirectionRuleRow> _rows = new();

    public ObjectMovementRestrictionsDialog(ObjectMovementRestrictions? initial)
    {
        InitializeComponent();

        _working = Clone(initial) ?? new ObjectMovementRestrictions();
        MultiLegMaxTotalDistanceTextBox.Text = _working.MultiLegMaxTotalDistanceCells?.ToString() ?? string.Empty;

        FilterCategoryComboBox.ItemsSource = ViewFilterLabels();
        FilterCategoryComboBox.SelectedIndex = 0;

        RulesDataGrid.ItemsSource = _rows;
        RefreshGridRows();
    }

    public IEnumerable<string> AllowJumpOverOptions => ["(blank)", "true", "false"];

    public ObjectMovementRestrictions? Value { get; private set; }

    private static IReadOnlyList<string> CategoryLabels()
    {
        return
        [
            "First Move - Open Landing",
            "First Move - Stacked Landing",
            "Later Moves - Open Landing",
            "Later Moves - Stacked Landing"
        ];
    }

    private static IReadOnlyList<string> ViewFilterLabels()
    {
        return
        [
            "Show all categories",
            "First Move - Open Landing",
            "First Move - Stacked Landing",
            "Later Moves - Open Landing",
            "Later Moves - Stacked Landing"
        ];
    }

    private void FilterControls_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!PersistRowsToModel())
        {
            RefreshGridRows();
            return;
        }

        RefreshGridRows();
    }

    private void ApplyFillToAllDirections_OnClick(object sender, RoutedEventArgs e)
    {
        var parsedMaxDistance = ParseMaxDistance(FillMaxDistanceTextBox.Text, allowBlank: true, out var maxDistanceValid);
        if (!maxDistanceValid)
        {
            System.Windows.MessageBox.Show(this, "Max Distance must be blank or an integer from 0 to 99.", "Movement Restrictions", MessageBoxButton.OK, MessageBoxImage.Information);
            FillMaxDistanceTextBox.Focus();
            FillMaxDistanceTextBox.SelectAll();
            return;
        }

        var allowJumpOver = ParseAllowJumpOver(FillAllowJumpOverComboBox.SelectedItem as string);

        foreach (var categoryIndex in GetVisibleCategoryIndexes())
        {
            var targetCategory = GetCategoryByIndex(categoryIndex);
            foreach (var direction in Directions())
            {
                var rule = GetRuleByDirection(targetCategory, direction);
                rule.MaxDistance = parsedMaxDistance;
                rule.AllowJumpOver = allowJumpOver;
            }
        }

        RefreshGridRows();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!PersistRowsToModel())
        {
            return;
        }

        // Always return the edited snapshot; callers can normalize empty state to null.
        Value = Clone(_working);
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private bool PersistRowsToModel()
    {
        var parsedTotalDistance = ParseMaxDistance(MultiLegMaxTotalDistanceTextBox.Text, allowBlank: true, out var totalDistanceValid);
        if (!totalDistanceValid)
        {
            System.Windows.MessageBox.Show(this, "Max Total Cells must be blank or an integer from 0 to 99.", "Movement Restrictions", MessageBoxButton.OK, MessageBoxImage.Information);
            MultiLegMaxTotalDistanceTextBox.Focus();
            MultiLegMaxTotalDistanceTextBox.SelectAll();
            return false;
        }

        _working.MultiLegMaxTotalDistanceCells = parsedTotalDistance;

        foreach (var row in _rows)
        {
            var maxDistance = ParseMaxDistance(row.MaxDistanceText, allowBlank: true, out var valid);
            if (!valid)
            {
                System.Windows.MessageBox.Show(this, $"{row.CategoryLabel} / {row.Direction}: Max Distance must be blank or an integer from 0 to 99.", "Movement Restrictions", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var category = GetCategoryByIndex(row.CategoryIndex);
            var rule = GetRuleByDirection(category, row.Direction);
            rule.MaxDistance = maxDistance;
            rule.AllowJumpOver = ParseAllowJumpOver(row.AllowJumpOverText);
        }

        return true;
    }

    private void RefreshGridRows()
    {
        _rows.Clear();

        var categoryIndexes = GetVisibleCategoryIndexes();
        foreach (var categoryIndex in categoryIndexes)
        {
            var category = GetCategoryByIndex(categoryIndex);

            foreach (var direction in Directions())
            {
                var rule = GetRuleByDirection(category, direction);
                _rows.Add(new DirectionRuleRow
                {
                    CategoryIndex = categoryIndex,
                    CategoryLabel = CategoryLabelByIndex(categoryIndex),
                    Direction = direction,
                    MaxDistanceText = rule.MaxDistance.HasValue ? rule.MaxDistance.Value.ToString() : string.Empty,
                    AllowJumpOverText = rule.AllowJumpOver.HasValue ? (rule.AllowJumpOver.Value ? "true" : "false") : "(blank)"
                });
            }
        }
    }

    private IEnumerable<int> GetVisibleCategoryIndexes()
    {
        var selectedCategoryIndex = GetSelectedFilterCategoryIndex();
        if (selectedCategoryIndex.HasValue)
        {
            yield return selectedCategoryIndex.Value;
            yield break;
        }

        yield return 0;
        yield return 1;
        yield return 2;
        yield return 3;
    }

    private int? GetSelectedFilterCategoryIndex()
    {
        var selectedIndex = FilterCategoryComboBox.SelectedIndex;
        if (selectedIndex <= 0)
        {
            return null;
        }

        return Math.Clamp(selectedIndex - 1, 0, 3);
    }

    private static IEnumerable<string> Directions()
    {
        yield return "N";
        yield return "NE";
        yield return "E";
        yield return "SE";
        yield return "S";
        yield return "SW";
        yield return "W";
        yield return "NW";
    }

    private static string CategoryLabelByIndex(int index)
    {
        return index switch
        {
            0 => "First Move - Open Landing",
            1 => "First Move - Stacked Landing",
            2 => "Later Moves - Open Landing",
            3 => "Later Moves - Stacked Landing",
            _ => "First Move - Open Landing"
        };
    }

    private ObjectMovementRestrictionCategory GetCategoryByIndex(int index)
    {
        return index switch
        {
            0 => _working.FirstUnstacked,
            1 => _working.FirstStacked,
            2 => _working.SubsequentUnstacked,
            3 => _working.SubsequentStacked,
            _ => _working.FirstUnstacked
        };
    }

    private static ObjectMovementRestrictionRule GetRuleByDirection(ObjectMovementRestrictionCategory category, string direction)
    {
        return direction switch
        {
            "N" => category.N,
            "NE" => category.NE,
            "E" => category.E,
            "SE" => category.SE,
            "S" => category.S,
            "SW" => category.SW,
            "W" => category.W,
            "NW" => category.NW,
            _ => category.N
        };
    }

    private static int? ParseMaxDistance(string? text, bool allowBlank, out bool valid)
    {
        var normalized = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            valid = allowBlank;
            return null;
        }

        if (!int.TryParse(normalized, out var parsed) || parsed < 0 || parsed > 99)
        {
            valid = false;
            return null;
        }

        valid = true;
        return parsed;
    }

    private static bool? ParseAllowJumpOver(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static ObjectMovementRestrictions? Clone(ObjectMovementRestrictions? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ObjectMovementRestrictions
        {
            MultiLegMaxTotalDistanceCells = source.MultiLegMaxTotalDistanceCells,
            FirstUnstacked = CloneCategory(source.FirstUnstacked),
            FirstStacked = CloneCategory(source.FirstStacked),
            SubsequentUnstacked = CloneCategory(source.SubsequentUnstacked),
            SubsequentStacked = CloneCategory(source.SubsequentStacked)
        };
    }

    private static ObjectMovementRestrictionCategory CloneCategory(ObjectMovementRestrictionCategory source)
    {
        return new ObjectMovementRestrictionCategory
        {
            N = CloneRule(source.N),
            NE = CloneRule(source.NE),
            E = CloneRule(source.E),
            SE = CloneRule(source.SE),
            S = CloneRule(source.S),
            SW = CloneRule(source.SW),
            W = CloneRule(source.W),
            NW = CloneRule(source.NW)
        };
    }

    private static ObjectMovementRestrictionRule CloneRule(ObjectMovementRestrictionRule source)
    {
        return new ObjectMovementRestrictionRule
        {
            MaxDistance = source.MaxDistance,
            AllowJumpOver = source.AllowJumpOver
        };
    }

    private sealed class DirectionRuleRow
    {
        public int CategoryIndex { get; set; }
        public string CategoryLabel { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string MaxDistanceText { get; set; } = string.Empty;
        public string AllowJumpOverText { get; set; } = "(blank)";
    }
}
