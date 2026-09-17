using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace StoryboardDesigner.App.Views;

public partial class ActionVerbPickerDialog : Window
{
    private sealed class VerbOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Text { get; init; } = string.Empty;

        public bool IsAvailable { get; init; } = true;

        public string DisabledReason { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    private readonly ObservableCollection<VerbOption> _options = new();

    public IReadOnlyList<string> SelectedVerbs { get; private set; } = Array.Empty<string>();

    public ActionVerbPickerDialog(
        IReadOnlyList<string> availableVerbs,
        IReadOnlyList<string> selectedVerbs,
        IReadOnlyDictionary<string, string> unavailableVerbMap,
        string? directionalQualifierContext = null)
    {
        InitializeComponent();

        var qualifierText = directionalQualifierContext?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(qualifierText))
        {
            GuidanceTextBlock.Text = "Choose the verbs that trigger this action. Verbs already used without a directional qualifier are unavailable. Select one primary verb from the checked verbs.";
        }
        else
        {
            GuidanceTextBlock.Text = $"Choose the verbs that trigger this action. Verbs already used with directional qualifier '{qualifierText}' are unavailable. Select one primary verb from the checked verbs.";
        }

        var selectedSet = new HashSet<string>(
            (selectedVerbs ?? Array.Empty<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var values = (availableVerbs ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Where(static value => !string.Equals(value, "(no verb)", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var selected in selectedVerbs ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(selected))
            {
                continue;
            }

            var normalized = selected.Trim();
            if (values.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            values.Add(normalized);
        }

        values.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var isUsedByOtherAction = unavailableVerbMap.TryGetValue(value, out var usedBy)
                                     && !string.IsNullOrWhiteSpace(usedBy);
            var isCurrentlySelected = selectedSet.Contains(value);

            _options.Add(new VerbOption
            {
                Text = value,
                IsSelected = isCurrentlySelected,
                IsAvailable = !isUsedByOtherAction || isCurrentlySelected,
                DisabledReason = isUsedByOtherAction && !isCurrentlySelected
                    ? $"Already used by: {usedBy}"
                    : string.Empty
            });
        }

        VerbListBox.ItemsSource = _options;
        RefreshPrimaryVerbChoices((selectedVerbs ?? Array.Empty<string>()).FirstOrDefault());
    }

    private void VerbCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        var currentPrimary = PrimaryVerbComboBox.SelectedItem as string;
        RefreshPrimaryVerbChoices(currentPrimary);
    }

    private void PrimaryVerbComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrimaryVerbComboBox.SelectedItem is not string selectedPrimary)
        {
            return;
        }

        foreach (var option in _options.Where(option => string.Equals(option.Text, selectedPrimary, StringComparison.OrdinalIgnoreCase)))
        {
            option.IsSelected = true;
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = _options
            .Where(option => option.IsSelected)
            .Select(option => option.Text)
            .ToList();

        if (selected.Count == 0)
        {
            SelectedVerbs = Array.Empty<string>();
            DialogResult = true;
            return;
        }

        var primary = PrimaryVerbComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(primary)
            || !selected.Any(value => string.Equals(value, primary, StringComparison.OrdinalIgnoreCase)))
        {
            primary = selected[0];
        }

        var ordered = new List<string> { primary! };
        ordered.AddRange(selected.Where(value => !string.Equals(value, primary, StringComparison.OrdinalIgnoreCase)));
        SelectedVerbs = ordered;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void RefreshPrimaryVerbChoices(string? preferredPrimary)
    {
        var selectedVerbs = _options
            .Where(option => option.IsSelected)
            .Select(option => option.Text)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PrimaryVerbComboBox.ItemsSource = selectedVerbs;
        if (selectedVerbs.Count == 0)
        {
            PrimaryVerbComboBox.SelectedItem = null;
            PrimaryVerbComboBox.IsEnabled = false;
            return;
        }

        PrimaryVerbComboBox.IsEnabled = true;
        var nextPrimary = selectedVerbs.FirstOrDefault(value => string.Equals(value, preferredPrimary, StringComparison.OrdinalIgnoreCase))
                          ?? selectedVerbs[0];
        PrimaryVerbComboBox.SelectedItem = nextPrimary;
    }
}
