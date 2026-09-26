using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfBrushes = System.Windows.Media.Brushes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class PhaseNodeSettingsDialog : Window
{
    private sealed record SoundOption(Guid? Id, string Display);
    private sealed record AmbienceModeOption(string Label, PhaseAmbienceMode? Value);
    private sealed record PhaseAmbientColorModeOption(string Label, PhaseAmbientColorMode? Value);
    private sealed record TextCueOption(string? EffectKey, string Display);
    private readonly PhaseTier _tier;
    private bool _updatingPhaseAmbientControls;

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

        PhaseAmbientColorModeComboBox.ItemsSource = new[]
        {
            new PhaseAmbientColorModeOption("(Default: Override)", null),
            new PhaseAmbientColorModeOption("Override", PhaseAmbientColorMode.Override),
            new PhaseAmbientColorModeOption("Blend", PhaseAmbientColorMode.Blend)
        };

        var overlay = initialValues.PhaseAmbientOverlay;
        PhaseAmbientColorModeComboBox.SelectedValue = overlay?.ColorMode;
        PhaseAmbientLightingEnabledCheckBox.IsChecked = overlay is not null;
        PhaseAmbientScaleTextBox.Text = FormatOptionalNumber(overlay?.AmbientScale, "0.##");
        PhaseAmbientColorBlendTextBox.Text = FormatOptionalNumber(overlay?.ColorBlend, "0.##");
        if (overlay?.AmbientScale is double ambientScale)
        {
            SetPhaseAmbientScaleValue(ambientScale);
        }

        if (overlay?.ColorBlend is double colorBlend)
        {
            SetPhaseAmbientColorBlendValue(colorBlend);
        }

        SetPhaseAmbientColor(overlay?.Color);

        if (_tier == PhaseTier.Book && overlay is null)
        {
            PhaseAmbientLightingEnabledCheckBox.IsEnabled = false;
            PhaseAmbientLightingHelpTextBlock.Text = "Ambient overlays are available only on chapter and page phases. This book phase inherits room ambient lighting.";
        }
        else if (_tier == PhaseTier.Book)
        {
            PhaseAmbientLightingHelpTextBlock.Text = "This book phase contains an unsupported ambient overlay. Clear the checkbox to remove it before saving.";
        }

        UpdatePhaseAmbientControlState();
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
        AmbienceModeComboBox.SelectedValue as PhaseAmbienceMode?,
        ReadPhaseAmbientOverlayForValues());

    private PhaseAmbientOverlay? ReadPhaseAmbientOverlayForValues()
    {
        if (PhaseAmbientLightingEnabledCheckBox.IsChecked != true)
        {
            return null;
        }

        return new PhaseAmbientOverlay
        {
            AmbientScale = ParseOptionalNumber(PhaseAmbientScaleTextBox.Text),
            Color = NormalizeOptionalColor(PhaseAmbientColorHexTextBox.Text),
            ColorMode = PhaseAmbientColorModeComboBox.SelectedValue as PhaseAmbientColorMode?,
            ColorBlend = PhaseAmbientColorModeComboBox.SelectedValue is PhaseAmbientColorMode.Blend
                ? ParseOptionalNumber(PhaseAmbientColorBlendTextBox.Text)
                : null
        };
    }

    private void PhaseAmbientLightingEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdatePhaseAmbientControlState();
    }

    private void UpdatePhaseAmbientControlState()
    {
        var enabled = PhaseAmbientLightingEnabledCheckBox.IsChecked == true;
        var blendEnabled = enabled && PhaseAmbientColorModeComboBox.SelectedValue is PhaseAmbientColorMode.Blend;

        PhaseAmbientScaleTextBox.IsEnabled = enabled;
        PhaseAmbientScaleSlider.IsEnabled = enabled;
        PhaseAmbientColorModeComboBox.IsEnabled = enabled;
        PhaseAmbientColorHexTextBox.IsEnabled = enabled;
        PickPhaseAmbientColorButton.IsEnabled = enabled;
        ClearPhaseAmbientColorButton.IsEnabled = enabled;
        PhaseAmbientColorBlendTextBox.IsEnabled = blendEnabled;
        PhaseAmbientColorBlendSlider.IsEnabled = blendEnabled;
        PhaseAmbientColorPreview.Opacity = enabled && !string.IsNullOrWhiteSpace(PhaseAmbientColorHexTextBox.Text) ? 1 : 0.45;
    }

    private void PhaseAmbientColorModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        UpdatePhaseAmbientControlState();
    }

    private void PhaseAmbientScaleSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingPhaseAmbientControls || !IsInitialized)
        {
            return;
        }

        SetPhaseAmbientScaleValue(e.NewValue);
    }

    private void PhaseAmbientScaleTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (TryParseNumber(PhaseAmbientScaleTextBox.Text, out var value))
        {
            SetPhaseAmbientScaleValue(Math.Clamp(value, 0, 2));
        }
    }

    private void PhaseAmbientScaleDecrement_OnClick(object sender, RoutedEventArgs e)
    {
        SetPhaseAmbientScaleValue(Math.Clamp(ParseOptionalNumber(PhaseAmbientScaleTextBox.Text) ?? 0 - 0.05, 0, 2));
    }

    private void PhaseAmbientScaleIncrement_OnClick(object sender, RoutedEventArgs e)
    {
        SetPhaseAmbientScaleValue(Math.Clamp((ParseOptionalNumber(PhaseAmbientScaleTextBox.Text) ?? 0) + 0.05, 0, 2));
    }

    private void SetPhaseAmbientScaleValue(double value)
    {
        _updatingPhaseAmbientControls = true;
        try
        {
            var normalized = Math.Round(Math.Clamp(value, 0, 2), 2);
            PhaseAmbientScaleTextBox.Text = normalized.ToString("0.##", CultureInfo.InvariantCulture);
            PhaseAmbientScaleSlider.Value = normalized;
        }
        finally
        {
            _updatingPhaseAmbientControls = false;
        }
    }

    private void PhaseAmbientColorBlendSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingPhaseAmbientControls || !IsInitialized)
        {
            return;
        }

        SetPhaseAmbientColorBlendValue(e.NewValue);
    }

    private void PhaseAmbientColorBlendTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (TryParseNumber(PhaseAmbientColorBlendTextBox.Text, out var value))
        {
            SetPhaseAmbientColorBlendValue(Math.Clamp(value, 0, 1));
        }
    }

    private void PhaseAmbientColorBlendDecrement_OnClick(object sender, RoutedEventArgs e)
    {
        SetPhaseAmbientColorBlendValue(Math.Clamp((ParseOptionalNumber(PhaseAmbientColorBlendTextBox.Text) ?? 0) - 0.05, 0, 1));
    }

    private void PhaseAmbientColorBlendIncrement_OnClick(object sender, RoutedEventArgs e)
    {
        SetPhaseAmbientColorBlendValue(Math.Clamp((ParseOptionalNumber(PhaseAmbientColorBlendTextBox.Text) ?? 0) + 0.05, 0, 1));
    }

    private void SetPhaseAmbientColorBlendValue(double value)
    {
        _updatingPhaseAmbientControls = true;
        try
        {
            var normalized = Math.Round(Math.Clamp(value, 0, 1), 2);
            PhaseAmbientColorBlendTextBox.Text = normalized.ToString("0.##", CultureInfo.InvariantCulture);
            PhaseAmbientColorBlendSlider.Value = normalized;
        }
        finally
        {
            _updatingPhaseAmbientControls = false;
        }
    }

    private void PickPhaseAmbientColor_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(PhaseAmbientColorHexTextBox.Text)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            SetPhaseAmbientColor(dialog.SelectedColor);
        }
    }

    private void ClearPhaseAmbientColor_OnClick(object sender, RoutedEventArgs e)
    {
        SetPhaseAmbientColor(null);
    }

    private void SetPhaseAmbientColor(string? color)
    {
        var normalized = NormalizeOptionalColor(color);
        PhaseAmbientColorHexTextBox.Text = normalized ?? string.Empty;
        PhaseAmbientColorPreview.Background = TryParseHexColor(normalized, out var parsed)
            ? new SolidColorBrush(parsed)
            : WpfBrushes.Transparent;
        if (IsInitialized)
        {
            UpdatePhaseAmbientControlState();
        }
    }

    private bool TryReadPhaseAmbientOverlay(out PhaseAmbientOverlay? overlay)
    {
        overlay = null;
        if (PhaseAmbientLightingEnabledCheckBox.IsChecked != true)
        {
            return true;
        }

        if (!TryReadOptionalNumber(PhaseAmbientScaleTextBox, "Ambient scale", 0, 2, out var ambientScale))
        {
            return false;
        }

        var color = NormalizeOptionalColor(PhaseAmbientColorHexTextBox.Text);
        if (!string.IsNullOrWhiteSpace(PhaseAmbientColorHexTextBox.Text) && color is null)
        {
            MessageBox.Show(this, "Overlay color must use #RRGGBB format.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var colorMode = PhaseAmbientColorModeComboBox.SelectedValue as PhaseAmbientColorMode?;
        double? colorBlend = null;
        if (colorMode == PhaseAmbientColorMode.Blend
            && !TryReadOptionalNumber(PhaseAmbientColorBlendTextBox, "Color blend", 0, 1, out colorBlend))
        {
            return false;
        }

        if (colorMode == PhaseAmbientColorMode.Blend && string.IsNullOrWhiteSpace(color))
        {
            MessageBox.Show(this, "Blend mode requires an overlay color.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (_tier == PhaseTier.Book)
        {
            MessageBox.Show(this, "Ambient overlays are supported only on chapter and page phases. Clear the checkbox to remove this overlay.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        overlay = new PhaseAmbientOverlay
        {
            AmbientScale = ambientScale,
            Color = color,
            ColorMode = colorMode,
            ColorBlend = colorMode == PhaseAmbientColorMode.Blend ? colorBlend : null
        };
        return true;
    }

    private static bool TryReadOptionalNumber(WpfTextBox textBox, string label, double minimum, double maximum, out double? value)
    {
        value = null;
        var text = textBox.Text.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            MessageBox.Show($"{label} must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}, or blank.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            textBox.Focus();
            return false;
        }

        value = Math.Round(parsed, 2);
        return true;
    }

    private static bool TryParseNumber(string? text, out double value)
    {
        return double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }

    private static double? ParseOptionalNumber(string? text)
    {
        return TryParseNumber(text, out var value) ? value : null;
    }

    private static string FormatOptionalNumber(double? value, string format)
    {
        return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string? NormalizeOptionalColor(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length == 7
               && normalized[0] == '#'
               && normalized.Skip(1).All(static character => Uri.IsHexDigit(character))
            ? normalized.ToUpperInvariant()
            : null;
    }

    private static bool TryParseHexColor(string? value, out System.Windows.Media.Color color)
    {
        color = default;
        var normalized = NormalizeOptionalColor(value);
        if (normalized is null)
        {
            return false;
        }

        color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(normalized)!;
        return true;
    }

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
            MessageBox.Show(this, "Please enter a phase key.", "Phase Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryReadPhaseAmbientOverlay(out _))
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
