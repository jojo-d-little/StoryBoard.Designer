using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class SoundEffectLibraryDialog : Window
{
    private readonly ObservableCollection<SoundEffectLibraryEntry> _entries;
    private readonly IReadOnlyList<string> _categorySuggestions;

    public SoundEffectLibraryDialog(
        IReadOnlyList<SoundEffectLibraryEntry> initialEntries,
        IReadOnlyList<string>? categorySuggestions = null)
    {
        InitializeComponent();

        _entries = new ObservableCollection<SoundEffectLibraryEntry>(
            initialEntries.Select(CloneEntry));
        _categorySuggestions = BuildCategorySuggestions(_entries, categorySuggestions);

        EntriesDataGrid.ItemsSource = _entries;

        if (_entries.Count > 0)
        {
            EntriesDataGrid.SelectedIndex = 0;
        }
    }

    public List<SoundEffectLibraryEntry> Entries => _entries.Select(CloneEntry).ToList();

    private void AddEntry_OnClick(object sender, RoutedEventArgs e)
    {
        var entry = new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = BuildDefaultKey(_entries.Count + 1),
            DisplayName = "New Sound",
            Category = "General",
            AssetRef = string.Empty,
            RepeatMode = "None",
            ReplayPolicy = "PlayAgain"
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
        if (EntriesDataGrid.SelectedItem is not SoundEffectLibraryEntry selected)
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

    private bool TryEditEntry(SoundEffectLibraryEntry sourceEntry, out SoundEffectLibraryEntry updatedEntry)
    {
        var categorySuggestions = BuildCategorySuggestions(_entries, _categorySuggestions);

        var dialog = new SoundEffectEntryEditorDialog(CloneEntry(sourceEntry), categorySuggestions)
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
        if (EntriesDataGrid.SelectedItem is not SoundEffectLibraryEntry selected)
        {
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(selected.DisplayName)
            ? selected.SoundEffectId.ToString("D")
            : selected.DisplayName.Trim();

        var confirmation = System.Windows.MessageBox.Show(
            this,
            $"Remove sound effect '{displayName}'?\n\nThis may break existing action result-code cue references that use this sound effect id.",
            "Sound Effects",
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
            System.Windows.MessageBox.Show(
                this,
                string.Join(Environment.NewLine, errors),
                "Sound Effects",
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

    private static List<string> ValidateEntries(IEnumerable<SoundEffectLibraryEntry> entries)
    {
        var errors = new List<string>();
        var seenIds = new HashSet<Guid>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowNumber = 0;

        foreach (var entry in entries)
        {
            rowNumber++;

            if (entry.SoundEffectId == Guid.Empty)
            {
                errors.Add($"Row {rowNumber}: SoundEffectId must be a valid GUID.");
            }
            else if (!seenIds.Add(entry.SoundEffectId))
            {
                errors.Add($"Row {rowNumber}: SoundEffectId duplicates another entry.");
            }

            var key = (entry.SoundEffectKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"Row {rowNumber}: SoundEffectKey is required.");
            }
            else if (!seenKeys.Add(key))
            {
                errors.Add($"Row {rowNumber}: SoundEffectKey duplicates another entry.");
            }

            if (string.IsNullOrWhiteSpace((entry.DisplayName ?? string.Empty).Trim()))
            {
                errors.Add($"Row {rowNumber}: Display Name is required.");
            }

            if (string.IsNullOrWhiteSpace((entry.AssetRef ?? string.Empty).Trim()))
            {
                errors.Add($"Row {rowNumber}: Asset Ref is required.");
            }
        }

        return errors;
    }

    private static string BuildDefaultKey(int sequence)
    {
        return $"sound.effect.{sequence}";
    }

    private static IReadOnlyList<string> BuildCategorySuggestions(
        IEnumerable<SoundEffectLibraryEntry> entries,
        IReadOnlyList<string>? additionalSuggestions)
    {
        var values = new List<string>();

        void Add(string? category)
        {
            var normalized = category?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (values.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            values.Add(normalized);
        }

        Add("General");

        foreach (var entry in entries)
        {
            Add(entry.Category);
        }

        foreach (var suggestion in additionalSuggestions ?? Array.Empty<string>())
        {
            Add(suggestion);
        }

        return values
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => string.Equals(value, "General", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
    }

    private static SoundEffectLibraryEntry CloneEntry(SoundEffectLibraryEntry source)
    {
        return new SoundEffectLibraryEntry
        {
            SoundEffectId = source.SoundEffectId == Guid.Empty ? Guid.NewGuid() : source.SoundEffectId,
            SoundEffectKey = source.SoundEffectKey,
            DisplayName = source.DisplayName,
            Category = source.Category,
            AssetRef = source.AssetRef,
            BaseVolumeDb = source.BaseVolumeDb,
            FadeInMs = source.FadeInMs,
            FadeOutMs = source.FadeOutMs,
            RepeatMode = source.RepeatMode,
            ReplayPolicy = source.ReplayPolicy,
            RepeatCount = source.RepeatCount,
            StartDelayMs = source.StartDelayMs,
            RepeatIntervalMs = source.RepeatIntervalMs,
            DurationMs = source.DurationMs,
            MaxPlayDurationMs = source.MaxPlayDurationMs,
            RepeatDurationMs = source.RepeatDurationMs,
            RepeatCooldownMs = source.RepeatCooldownMs,
            ConcurrencyGroup = source.ConcurrencyGroup,
            ConcurrencyGroupImportance = source.ConcurrencyGroupImportance,
            Importance = source.Importance
        };
    }
}