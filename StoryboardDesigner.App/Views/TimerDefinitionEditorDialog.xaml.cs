using System.Globalization;
using System.Windows;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using MessageBox = System.Windows.MessageBox;

namespace StoryboardDesigner.App.Views;

public partial class TimerDefinitionEditorDialog : Window
{
    private const string NoneOption = "(none)";

    public TimerDefinitionEditorDialog(RuntimeTimerDefinitionDto initialEntry, IReadOnlyList<string>? actionRefSuggestions = null)
    {
        InitializeComponent();

        FireModeComboBox.ItemsSource = Enum.GetValues<TimerFireMode>();
        RepeatModeComboBox.ItemsSource = BuildEnumSelectionValues<TimerRepeatMode>();
        RepeatProgressionModeComboBox.ItemsSource = BuildEnumSelectionValues<TimerRepeatProgressionMode>();
        LifetimeOwnerTypeComboBox.ItemsSource = Enum.GetValues<TimerOwnerType>();
        ConflictBehaviorComboBox.ItemsSource = Enum.GetValues<TimerConflictBehavior>();
        var actionSuggestions = (actionRefSuggestions ?? Array.Empty<string>())
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        TargetActionRefComboBox.ItemsSource = actionSuggestions;
        OnShrinkExpiryActionRefComboBox.ItemsSource = actionSuggestions;

        TimerKeyTextBox.Text = initialEntry.TimerKey ?? string.Empty;
        ScheduleAfterMsTextBox.Text = initialEntry.ScheduleAfterMs.ToString(CultureInfo.InvariantCulture);
        FireModeComboBox.SelectedItem = initialEntry.FireMode;
        RepeatModeComboBox.SelectedItem = ToSelectionValue(initialEntry.RepeatMode);
        RepeatProgressionModeComboBox.SelectedItem = ToSelectionValue(initialEntry.RepeatProgressionMode);
        RepeatIntervalMsTextBox.Text = FormatNullableInt(initialEntry.RepeatIntervalMs);
        RepeatIntervalStepMsTextBox.Text = FormatNullableInt(initialEntry.RepeatIntervalStepMs);
        RepeatProgressionRateTextBox.Text = FormatNullableDouble(initialEntry.RepeatProgressionRate);
        RepeatIntervalMinMsTextBox.Text = FormatNullableInt(initialEntry.RepeatIntervalMinMs);
        ShrinkingExpiresUnderMsTextBox.Text = FormatNullableInt(initialEntry.ShrinkingExpiresUnderMs);
        TargetActionRefComboBox.Text = initialEntry.TargetActionRef ?? string.Empty;
        OnShrinkExpiryActionRefComboBox.Text = initialEntry.OnShrinkExpiryActionRef ?? string.Empty;
        LifetimeOwnerTypeComboBox.SelectedItem = initialEntry.LifetimeOwnerType;
        ConflictBehaviorComboBox.SelectedItem = initialEntry.ConflictBehavior;
        EnabledCheckBox.IsChecked = initialEntry.Enabled;

        RefreshRepeatFieldsEnabledState();
    }

    public RuntimeTimerDefinitionDto Entry { get; private set; } = new();

    private static IReadOnlyList<string> BuildEnumSelectionValues<TEnum>()
        where TEnum : struct, Enum
    {
        return [NoneOption, .. Enum.GetNames<TEnum>()];
    }

    private static string ToSelectionValue<TEnum>(TEnum? value)
        where TEnum : struct, Enum
    {
        return value.HasValue ? value.Value.ToString() : NoneOption;
    }

    private static TEnum? FromSelectionValue<TEnum>(object? value)
        where TEnum : struct, Enum
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text)
            || string.Equals(text, NoneOption, StringComparison.Ordinal))
        {
            return null;
        }

        return Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var timerKey = TimerKeyTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(timerKey))
        {
            MessageBox.Show(this, "Timer Key is required.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(ScheduleAfterMsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var scheduleAfterMs)
            || scheduleAfterMs < 0)
        {
            MessageBox.Show(this, "Delay (ms) must be a number greater than or equal to 0.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (FireModeComboBox.SelectedItem is not TimerFireMode fireMode)
        {
            MessageBox.Show(this, "Fire Mode is required.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var repeatMode = FromSelectionValue<TimerRepeatMode>(RepeatModeComboBox.SelectedItem);
        var repeatProgressionMode = FromSelectionValue<TimerRepeatProgressionMode>(RepeatProgressionModeComboBox.SelectedItem);

        if (!TryParseNullableInt(RepeatIntervalMsTextBox.Text, out var repeatIntervalMs)
            || !TryParseNullableInt(RepeatIntervalStepMsTextBox.Text, out var repeatIntervalStepMs)
            || !TryParseNullableDouble(RepeatProgressionRateTextBox.Text, out var repeatProgressionRate)
            || !TryParseNullableInt(RepeatIntervalMinMsTextBox.Text, out var repeatIntervalMinMs)
            || !TryParseNullableInt(ShrinkingExpiresUnderMsTextBox.Text, out var shrinkingExpiresUnderMs))
        {
            MessageBox.Show(this, "One or more repeat numeric fields are invalid.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var targetActionRef = TargetActionRefComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetActionRef))
        {
            MessageBox.Show(this, "Target Action Ref is required.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (LifetimeOwnerTypeComboBox.SelectedItem is not TimerOwnerType lifetimeOwnerType)
        {
            MessageBox.Show(this, "Owner Type is required.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ConflictBehaviorComboBox.SelectedItem is not TimerConflictBehavior conflictBehavior)
        {
            MessageBox.Show(this, "Conflict Behavior is required.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (fireMode == TimerFireMode.Repeating)
        {
            if (!repeatMode.HasValue)
            {
                MessageBox.Show(this, "Repeat Mode is required when Fire Mode is Repeating.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!repeatIntervalMs.HasValue || repeatIntervalMs.Value <= 0)
            {
                MessageBox.Show(this, "Repeat Interval (ms) must be greater than 0 for repeating timers.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (repeatMode is TimerRepeatMode.GrowingInterval or TimerRepeatMode.ShrinkingInterval)
            {
                if (!repeatProgressionMode.HasValue)
                {
                    MessageBox.Show(this, "Repeat Progression is required for growing or shrinking repeat modes.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!repeatIntervalStepMs.HasValue || repeatIntervalStepMs.Value <= 0)
                {
                    MessageBox.Show(this, "Repeat Interval Step (ms) must be greater than 0 for growing or shrinking repeat modes.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (repeatProgressionMode == TimerRepeatProgressionMode.Exponential)
                {
                    if (!repeatProgressionRate.HasValue || repeatProgressionRate.Value <= 1d)
                    {
                        MessageBox.Show(this, "Repeat Progression Rate must be greater than 1 for exponential progression.", "Timer Definition", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
            }

            NormalizeFieldsForMode(ref repeatMode, ref repeatProgressionMode, ref repeatIntervalStepMs, ref repeatProgressionRate, ref repeatIntervalMinMs, ref shrinkingExpiresUnderMs);
        }
        else
        {
            repeatMode = null;
            repeatProgressionMode = null;
            repeatIntervalMs = null;
            repeatIntervalStepMs = null;
            repeatProgressionRate = null;
            repeatIntervalMinMs = null;
            shrinkingExpiresUnderMs = null;
        }

        var onShrinkExpiryActionRef = NormalizeNullableText(OnShrinkExpiryActionRefComboBox.Text);
        if (fireMode != TimerFireMode.Repeating || repeatMode != TimerRepeatMode.ShrinkingInterval)
        {
            onShrinkExpiryActionRef = null;
        }

        Entry = new RuntimeTimerDefinitionDto
        {
            TimerKey = timerKey,
            ScheduleAfterMs = scheduleAfterMs,
            FireMode = fireMode,
            RepeatMode = repeatMode,
            RepeatProgressionMode = repeatProgressionMode,
            RepeatIntervalMs = repeatIntervalMs,
            RepeatIntervalStepMs = repeatIntervalStepMs,
            RepeatProgressionRate = repeatProgressionRate,
            RepeatIntervalMinMs = repeatIntervalMinMs,
            ShrinkingExpiresUnderMs = shrinkingExpiresUnderMs,
            TargetActionRef = targetActionRef,
            OnShrinkExpiryActionRef = onShrinkExpiryActionRef,
            LifetimeOwnerType = lifetimeOwnerType,
            ConflictBehavior = conflictBehavior,
            Enabled = EnabledCheckBox.IsChecked == true
        };

        DialogResult = true;
    }

    private static bool TryParseNullableInt(string text, out int? value)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = null;
            return true;
        }

        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseNullableDouble(string text, out double? value)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = null;
            return true;
        }

        if (!double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? NormalizeNullableText(string text)
    {
        var normalized = text.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string FormatNullableInt(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static void NormalizeFieldsForMode(
        ref TimerRepeatMode? repeatMode,
        ref TimerRepeatProgressionMode? repeatProgressionMode,
        ref int? repeatIntervalStepMs,
        ref double? repeatProgressionRate,
        ref int? repeatIntervalMinMs,
        ref int? shrinkingExpiresUnderMs)
    {
        if (!repeatMode.HasValue)
        {
            repeatProgressionMode = null;
            repeatIntervalStepMs = null;
            repeatProgressionRate = null;
            repeatIntervalMinMs = null;
            shrinkingExpiresUnderMs = null;
            return;
        }

        if (repeatMode == TimerRepeatMode.FixedInterval)
        {
            repeatProgressionMode = null;
            repeatIntervalStepMs = null;
            repeatProgressionRate = null;
            repeatIntervalMinMs = null;
            shrinkingExpiresUnderMs = null;
            return;
        }

        if (repeatProgressionMode == TimerRepeatProgressionMode.Linear)
        {
            repeatProgressionRate = null;
        }
        else if (repeatProgressionMode != TimerRepeatProgressionMode.Exponential)
        {
            repeatProgressionRate = null;
        }

        if (repeatMode != TimerRepeatMode.ShrinkingInterval)
        {
            repeatIntervalMinMs = null;
            shrinkingExpiresUnderMs = null;
        }
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void FireModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshRepeatFieldsEnabledState();
    }

    private void RepeatModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshRepeatFieldsEnabledState();
    }

    private void RepeatProgressionModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshRepeatFieldsEnabledState();
    }

    private void RefreshRepeatFieldsEnabledState()
    {
        var isRepeating = FireModeComboBox.SelectedItem is TimerFireMode.Repeating;
        var repeatMode = FromSelectionValue<TimerRepeatMode>(RepeatModeComboBox.SelectedItem);
        var progressionMode = FromSelectionValue<TimerRepeatProgressionMode>(RepeatProgressionModeComboBox.SelectedItem);
        var modeSupportsProgression = isRepeating && repeatMode is TimerRepeatMode.GrowingInterval or TimerRepeatMode.ShrinkingInterval;
        var usesStep = modeSupportsProgression;
        var usesRate = modeSupportsProgression && progressionMode == TimerRepeatProgressionMode.Exponential;
        var isShrinkingMode = isRepeating && repeatMode == TimerRepeatMode.ShrinkingInterval;

        RepeatModeComboBox.IsEnabled = isRepeating;
        RepeatProgressionModeComboBox.IsEnabled = modeSupportsProgression;
        RepeatIntervalMsTextBox.IsEnabled = isRepeating;
        RepeatIntervalStepMsTextBox.IsEnabled = usesStep;
        RepeatProgressionRateTextBox.IsEnabled = usesRate;
        RepeatIntervalMinMsTextBox.IsEnabled = isShrinkingMode;
        ShrinkingExpiresUnderMsTextBox.IsEnabled = isShrinkingMode;
        OnShrinkExpiryActionRefComboBox.IsEnabled = isShrinkingMode;

        if (!RepeatProgressionModeComboBox.IsEnabled)
        {
            RepeatProgressionModeComboBox.SelectedItem = NoneOption;
        }

        RepeatIntervalStepMsLabel.Opacity = RepeatIntervalStepMsTextBox.IsEnabled ? 1d : 0.6d;
        RepeatProgressionRateLabel.Opacity = RepeatProgressionRateTextBox.IsEnabled ? 1d : 0.6d;
        RepeatIntervalMinMsLabel.Opacity = RepeatIntervalMinMsTextBox.IsEnabled ? 1d : 0.6d;
        ShrinkingExpiresUnderMsLabel.Opacity = ShrinkingExpiresUnderMsTextBox.IsEnabled ? 1d : 0.6d;
        OnShrinkExpiryActionRefLabel.Opacity = OnShrinkExpiryActionRefComboBox.IsEnabled ? 1d : 0.6d;
        RepeatProgressionModeLabel.Opacity = RepeatProgressionModeComboBox.IsEnabled ? 1d : 0.6d;
    }
}
