using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class PhaseNodeSettingsDialog : Window
{
    private sealed record SoundOption(Guid? Id, string Display);
    private sealed record AmbienceModeOption(string Label, PhaseAmbienceMode? Value);
    private sealed record TextCueOption(string? EffectKey, string Display);
    private readonly PhaseTier _tier;

    public PhaseNodeSettingsDialog(
        PhaseNodeEditRequest initialValues,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectOptions,
        IReadOnlyList<PhaseTextPresentationCueOption> textCueOptions,
        IReadOnlyList<string>? ambientTimerKeyOptions = null)
    {
        InitializeComponent();

        _tier = initialValues.Tier;
        TierTextBlock.Text = initialValues.Tier.ToString();
        DisplayNameTextBox.Text = initialValues.DisplayName;
        PhaseKeyTextBox.Text = initialValues.PhaseKey;
        TitleTextBox.Text = initialValues.Title ?? string.Empty;
        PrologueTextBox.Text = initialValues.Prologue ?? string.Empty;
        NarrativeTextBox.Text = initialValues.Narrative ?? string.Empty;
        var timerKeyOptions = NormalizeTimerKeyOptions(ambientTimerKeyOptions, initialValues.PhaseAmbientTimerKey);
        AmbientTimerKeyComboBox.ItemsSource = timerKeyOptions;
        AmbientTimerKeyComboBox.Text = initialValues.PhaseAmbientTimerKey ?? string.Empty;

        var cueOptions = new List<TextCueOption>
        {
            new(null, "(None)")
        };
        cueOptions.AddRange(textCueOptions.Select(option =>
            new TextCueOption(
                option.EffectKey,
                BuildCueDisplay(option))));

        TitleCueComboBox.ItemsSource = cueOptions;
        TitleCueComboBox.SelectedValue = initialValues.TitlePresentationCueEffectKey;
        PrologueCueComboBox.ItemsSource = cueOptions;
        PrologueCueComboBox.SelectedValue = initialValues.ProloguePresentationCueEffectKey;
        NarrativeCueComboBox.ItemsSource = cueOptions;
        NarrativeCueComboBox.SelectedValue = initialValues.NarrativePresentationCueEffectKey;

        TitleCueComboBox.SelectionChanged += (_, _) => SyncPrologueCueFromTitleWhenPaired();
        TitleCueComboBox.LostKeyboardFocus += (_, _) => SyncPrologueCueFromTitleWhenPaired();

        PairTitleAndPrologueCueCheckBox.IsChecked = AreEqualCueKeys(
            initialValues.TitlePresentationCueEffectKey,
            initialValues.ProloguePresentationCueEffectKey);
        UpdateTitleProloguePairingState();

        var soundOptions = new List<SoundOption>
        {
            new(null, "(None)")
        };

        soundOptions.AddRange(soundEffectOptions
            .OrderBy(static option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(static option =>
                new SoundOption(
                    option.SoundEffectId,
                    string.IsNullOrWhiteSpace(option.DisplayName)
                        ? option.SoundEffectKey
                        : $"{option.DisplayName} ({option.SoundEffectKey})")));

        AmbientSoundComboBox.ItemsSource = soundOptions;
        AmbientSoundComboBox.SelectedValue = initialValues.PhaseAmbientSoundEffectId;

        AmbienceModeComboBox.ItemsSource = new[]
        {
            new AmbienceModeOption("(Inherit)", null),
            new AmbienceModeOption("Overlay", PhaseAmbienceMode.Overlay),
            new AmbienceModeOption("Replace", PhaseAmbienceMode.Replace)
        };
        AmbienceModeComboBox.SelectedValue = initialValues.PhaseAmbienceMode;
    }

    public PhaseNodeEditRequest Values => new(
        _tier,
        DisplayNameTextBox.Text.Trim(),
        PhaseKeyTextBox.Text.Trim(),
        NormalizeOptionalText(TitleTextBox.Text),
        ResolveCueEffectKey(TitleCueComboBox),
        NormalizeOptionalText(PrologueTextBox.Text),
        ResolvePrologueCueEffectKey(),
        NormalizeOptionalText(NarrativeTextBox.Text),
        ResolveCueEffectKey(NarrativeCueComboBox),
        AmbientSoundComboBox.SelectedValue as Guid?,
        NormalizeOptionalText(AmbientTimerKeyComboBox.Text),
        AmbienceModeComboBox.SelectedValue as PhaseAmbienceMode?);

    private static IReadOnlyList<string> NormalizeTimerKeyOptions(
        IReadOnlyList<string>? options,
        string? currentValue)
    {
        var values = new List<string>();
        if (options is not null)
        {
            values.AddRange(options);
        }

        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            values.Add(currentValue);
        }

        return values
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string BuildCueDisplay(PhaseTextPresentationCueOption option)
    {
        var where = string.IsNullOrWhiteSpace(option.Where) ? "unspecified" : option.Where;
        var howSegment = string.IsNullOrWhiteSpace(option.How) ? string.Empty : $" / {option.How}";
        var timingSegment = option.DurationMs switch
        {
            null => string.Empty,
            <= 0 => " / manual dismiss",
            _ => $" / {option.DurationMs.Value}ms"
        };

        return $"{option.DisplayName} [{where}{howSegment}{timingSegment}]";
    }

    private static bool AreEqualCueKeys(string? left, string? right)
    {
        var normalizedLeft = NormalizeOptionalText(left) ?? string.Empty;
        var normalizedRight = NormalizeOptionalText(right) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(normalizedLeft)
               && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolvePrologueCueEffectKey()
    {
        if (PairTitleAndPrologueCueCheckBox.IsChecked == true)
        {
            return ResolveCueEffectKey(TitleCueComboBox);
        }

        return ResolveCueEffectKey(PrologueCueComboBox);
    }

    private void PairTitleAndPrologueCueCheckBox_OnCheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateTitleProloguePairingState();
    }

    private void UpdateTitleProloguePairingState()
    {
        var pairEnabled = PairTitleAndPrologueCueCheckBox.IsChecked == true;
        PrologueCueComboBox.IsEnabled = !pairEnabled;
        if (!pairEnabled)
        {
            return;
        }

        SyncPrologueCueFromTitleWhenPaired();
    }

    private void SyncPrologueCueFromTitleWhenPaired()
    {
        if (PairTitleAndPrologueCueCheckBox.IsChecked != true)
        {
            return;
        }

        var titleCue = ResolveCueEffectKey(TitleCueComboBox);
        PrologueCueComboBox.SelectedValue = titleCue;
        PrologueCueComboBox.Text = titleCue ?? string.Empty;
    }

    private static string? ResolveCueEffectKey(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedValue is string selectedValue)
        {
            return NormalizeOptionalText(selectedValue);
        }

        return NormalizeOptionalText(comboBox.Text);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter a display name.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(PhaseKeyTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter a phase key.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
