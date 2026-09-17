using System.Collections.ObjectModel;
using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ValidationIgnoredRulesDialog : Window
{
    private sealed class KnownRuleListItem
    {
        public required string RuleId { get; init; }
        public required string DisplayText { get; init; }
    }

    private readonly IList<string> _targetValues;
    private readonly ObservableCollection<string> _workingValues;
    private readonly List<KnownRuleListItem> _knownRules;

    public ValidationIgnoredRulesDialog(
        string scopeLabel,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IReadOnlyList<ValidationRuleCatalogItem> knownRules)
    {
        InitializeComponent();

        _targetValues = values;
        _workingValues = new ObservableCollection<string>(values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase));

        var inherited = inheritedValues
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inherited.Count == 0)
        {
            inherited.Add("(none)");
        }

        _knownRules = knownRules
            .OrderBy(static rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(rule => new KnownRuleListItem
            {
                RuleId = rule.RuleId,
                DisplayText = $"{rule.RuleId} - {rule.Title} ({rule.Category}, {rule.DefaultSeverity})"
            })
            .ToList();

        HeaderText.Text = $"{scopeLabel} - Ignored Validation Rules";
        InheritedRulesListBox.ItemsSource = inherited;
        IgnoredRulesListBox.ItemsSource = _workingValues;
        KnownFilterTextBox.Text = string.Empty;

        RefreshKnownRules();
    }

    private void KnownFilterTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshKnownRules();
    }

    private void KnownRulesListBox_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        AddSelectedKnownRule();
    }

    private void AddSelectedKnownRule_OnClick(object sender, RoutedEventArgs e)
    {
        AddSelectedKnownRule();
    }

    private void AddRule_OnClick(object sender, RoutedEventArgs e)
    {
        AddRuleById(RuleInputTextBox.Text);
        RuleInputTextBox.Clear();
        RuleInputTextBox.Focus();
    }

    private void RemoveRule_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not string value)
        {
            return;
        }

        var existing = _workingValues.FirstOrDefault(entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _workingValues.Remove(existing);
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        _targetValues.Clear();

        foreach (var value in _workingValues)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (_targetValues.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _targetValues.Add(normalized);
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void RefreshKnownRules()
    {
        var filter = KnownFilterTextBox.Text?.Trim() ?? string.Empty;

        IEnumerable<KnownRuleListItem> filtered = _knownRules;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = filtered.Where(item => item.DisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        KnownRulesListBox.ItemsSource = filtered.ToList();
    }

    private void AddSelectedKnownRule()
    {
        if (KnownRulesListBox.SelectedItem is not KnownRuleListItem selected)
        {
            return;
        }

        AddRuleById(selected.RuleId);
    }

    private void AddRuleById(string? candidate)
    {
        var normalized = candidate?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (_workingValues.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _workingValues.Add(normalized);
        var ordered = _workingValues.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToList();
        _workingValues.Clear();
        foreach (var value in ordered)
        {
            _workingValues.Add(value);
        }
    }
}
