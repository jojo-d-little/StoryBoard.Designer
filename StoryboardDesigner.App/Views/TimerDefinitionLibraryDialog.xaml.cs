using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using MessageBox = System.Windows.MessageBox;

namespace StoryboardDesigner.App.Views;

public partial class TimerDefinitionLibraryDialog : Window
{
    private readonly ObservableCollection<RuntimeTimerDefinitionDto> _entries;
    private readonly IReadOnlyList<string> _actionRefSuggestions;

    public TimerDefinitionLibraryDialog(IReadOnlyList<RuntimeTimerDefinitionDto> initialEntries, IReadOnlyList<string>? actionRefSuggestions = null)
    {
        InitializeComponent();

        _actionRefSuggestions = (actionRefSuggestions ?? Array.Empty<string>())
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _entries = new ObservableCollection<RuntimeTimerDefinitionDto>(
            initialEntries.Select(CloneEntry));

        EntriesDataGrid.ItemsSource = _entries;

        if (_entries.Count > 0)
        {
            EntriesDataGrid.SelectedIndex = 0;
        }
    }

    public List<RuntimeTimerDefinitionDto> Entries => _entries.Select(CloneEntry).ToList();

    private void AddEntry_OnClick(object sender, RoutedEventArgs e)
    {
        var entry = new RuntimeTimerDefinitionDto
        {
            TimerKey = BuildDefaultTimerKey(_entries.Count + 1),
            ScheduleAfterMs = 1000,
            FireMode = TimerFireMode.OneShot,
            TargetActionRef = "inspect",
            LifetimeOwnerType = TimerOwnerType.Room,
            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
            Enabled = true
        };

        if (!TryEditEntry(entry, out var updatedEntry))
        {
            return;
        }

        _entries.Add(updatedEntry);
        EntriesDataGrid.SelectedItem = updatedEntry;
        EntriesDataGrid.ScrollIntoView(updatedEntry);
    }

    private void EditEntry_OnClick(object sender, RoutedEventArgs e)
    {
        EditSelectedEntry();
    }

    private void EntriesDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditSelectedEntry();
    }

    private void EditSelectedEntry()
    {
        if (EntriesDataGrid.SelectedItem is not RuntimeTimerDefinitionDto selected)
        {
            return;
        }

        if (!TryEditEntry(selected, out var updatedEntry))
        {
            return;
        }

        var selectedIndex = _entries.IndexOf(selected);
        if (selectedIndex < 0)
        {
            return;
        }

        _entries[selectedIndex] = updatedEntry;
        EntriesDataGrid.SelectedItem = updatedEntry;
        EntriesDataGrid.ScrollIntoView(updatedEntry);
    }

    private bool TryEditEntry(RuntimeTimerDefinitionDto sourceEntry, out RuntimeTimerDefinitionDto updatedEntry)
    {
        var dialog = new TimerDefinitionEditorDialog(CloneEntry(sourceEntry), _actionRefSuggestions)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            updatedEntry = sourceEntry;
            return false;
        }

        updatedEntry = CloneEntry(dialog.Entry);
        return true;
    }

    private void RemoveEntry_OnClick(object sender, RoutedEventArgs e)
    {
        if (EntriesDataGrid.SelectedItem is not RuntimeTimerDefinitionDto selected)
        {
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(selected.TimerKey)
            ? "(unnamed timer)"
            : selected.TimerKey.Trim();

        var confirmation = MessageBox.Show(
            this,
            $"Remove timer definition '{displayName}'?",
            "Timer Definitions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _entries.Remove(selected);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var errors = ValidateEntries(_entries);
        if (errors.Count > 0)
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, errors),
                "Timer Definitions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static List<string> ValidateEntries(IEnumerable<RuntimeTimerDefinitionDto> entries)
    {
        var errors = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowNumber = 0;

        foreach (var entry in entries)
        {
            rowNumber++;

            var key = (entry.TimerKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"Row {rowNumber}: Timer Key is required.");
            }
            else if (!seenKeys.Add(key))
            {
                errors.Add($"Row {rowNumber}: Timer Key duplicates another entry.");
            }

            if (entry.ScheduleAfterMs < 0)
            {
                errors.Add($"Row {rowNumber}: Delay must be greater than or equal to 0.");
            }

            if (string.IsNullOrWhiteSpace((entry.TargetActionRef ?? string.Empty).Trim()))
            {
                errors.Add($"Row {rowNumber}: Target Action is required.");
            }

            if (entry.FireMode == TimerFireMode.Repeating)
            {
                if (!entry.RepeatMode.HasValue)
                {
                    errors.Add($"Row {rowNumber}: Repeat Mode is required when Fire Mode is Repeating.");
                }

                if (!entry.RepeatIntervalMs.HasValue || entry.RepeatIntervalMs.Value <= 0)
                {
                    errors.Add($"Row {rowNumber}: Repeat Interval (ms) must be greater than 0 for repeating timers.");
                }

                if (entry.RepeatMode is TimerRepeatMode.GrowingInterval or TimerRepeatMode.ShrinkingInterval)
                {
                    if (!entry.RepeatProgressionMode.HasValue)
                    {
                        errors.Add($"Row {rowNumber}: Repeat Progression is required for growing or shrinking modes.");
                    }

                    if (!entry.RepeatIntervalStepMs.HasValue || entry.RepeatIntervalStepMs.Value <= 0)
                    {
                        errors.Add($"Row {rowNumber}: Repeat Interval Step (ms) must be greater than 0 for growing or shrinking modes.");
                    }

                    if (entry.RepeatProgressionMode == TimerRepeatProgressionMode.Exponential
                        && (!entry.RepeatProgressionRate.HasValue || entry.RepeatProgressionRate.Value <= 1d))
                    {
                        errors.Add($"Row {rowNumber}: Repeat Progression Rate must be greater than 1 for exponential progression.");
                    }
                }
            }
        }

        return errors;
    }

    private static string BuildDefaultTimerKey(int sequence)
    {
        return $"timer.{sequence}";
    }

    private static RuntimeTimerDefinitionDto CloneEntry(RuntimeTimerDefinitionDto source)
    {
        return new RuntimeTimerDefinitionDto
        {
            TimerKey = source.TimerKey,
            ScheduleAfterMs = source.ScheduleAfterMs,
            FireMode = source.FireMode,
            RepeatMode = source.RepeatMode,
            RepeatProgressionMode = source.RepeatProgressionMode,
            RepeatIntervalMs = source.RepeatIntervalMs,
            RepeatIntervalStepMs = source.RepeatIntervalStepMs,
            RepeatProgressionRate = source.RepeatProgressionRate,
            RepeatIntervalMinMs = source.RepeatIntervalMinMs,
            ShrinkingExpiresUnderMs = source.ShrinkingExpiresUnderMs,
            TargetActionRef = source.TargetActionRef,
            OnShrinkExpiryActionRef = source.OnShrinkExpiryActionRef,
            LifetimeOwnerType = source.LifetimeOwnerType,
            ConflictBehavior = source.ConflictBehavior,
            Enabled = source.Enabled
        };
    }
}
