using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StoryboardDesigner.App.Models;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace StoryboardDesigner.App.Views;

public partial class ManagePointLightDefaultsDialog : Window
{
    private bool _updatingNumericControl;

    public ManagePointLightDefaultsDialog(PointLightDefaults? initialValues)
    {
        InitializeComponent();

        PointLightFlickerStyleComboBox.ItemsSource = new[] { string.Empty, "swell", "flame" };
        PointLightDefaultsEnabledCheckBox.IsChecked = initialValues is not null;
        SetNumericValue("Radius", initialValues?.RadiusPx);
        SetNumericValue("Intensity", initialValues?.IntensityScale);
        SetColorValue(PointLightColorHexTextBox, PointLightColorPreview, initialValues?.Color);
        SetColorValue(PointLightOuterColorHexTextBox, PointLightOuterColorPreview, initialValues?.OuterColor);
        SetNumericValue("GradientExponent", initialValues?.GradientExponent);
        SetNumericValue("Height", initialValues?.LightHeightCells);
        SetNumericValue("SwayAmount", initialValues?.SwayAmountPx);
        SetNumericValue("SwayHz", initialValues?.SwayHz);
        SetNumericValue("SwayDirection", initialValues?.SwayDirectionDeg);
        SetNumericValue("FlickerAmount", initialValues?.FlickerAmount);
        SetNumericValue("FlickerHz", initialValues?.FlickerHz);
        PointLightFlickerStyleComboBox.SelectedItem = initialValues?.FlickerStyle?.Trim() ?? string.Empty;
        UpdateEditorEnabledState();
    }

    public PointLightDefaults? PointLightDefaults { get; private set; }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPointLightDefaults(out var defaults, out var error))
        {
            MessageBox.Show(this, error, "Point-Light Defaults", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PointLightDefaults = defaults;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void PointLightDefaultsEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateEditorEnabledState();
    }

    private void UpdateEditorEnabledState()
    {
        PointLightEditorGrid.IsEnabled = PointLightDefaultsEnabledCheckBox.IsChecked == true;
    }

    private void NumericSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingNumericControl || sender is not Slider slider || slider.Tag is not string key)
        {
            return;
        }

        _updatingNumericControl = true;
        GetTextBox(key).Text = FormatDouble(e.NewValue);
        _updatingNumericControl = false;
    }

    private void NumericTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is string key)
        {
            UpdateSliderFromText(key);
        }
    }

    private void NumericTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox || textBox.Tag is not string key)
        {
            return;
        }

        UpdateSliderFromText(key);
        e.Handled = true;
    }

    private void NumericDecrement_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeNumericValue((sender as FrameworkElement)?.Tag as string, -1);
    }

    private void NumericIncrement_OnClick(object sender, RoutedEventArgs e)
    {
        NudgeNumericValue((sender as FrameworkElement)?.Tag as string, 1);
    }

    private void NudgeNumericValue(string? key, int direction)
    {
        if (key is null)
        {
            return;
        }

        var slider = GetSlider(key);
        var current = ParseOptionalDouble(GetTextBox(key).Text) ?? slider.Value;
        var next = current + (direction * slider.SmallChange);
        if (next < slider.Minimum)
        {
            next = slider.Minimum;
        }

        SetNumericValue(key, next);
    }

    private void UpdateSliderFromText(string key)
    {
        var value = ParseOptionalDouble(GetTextBox(key).Text);
        if (!value.HasValue)
        {
            return;
        }

        SetNumericValue(key, value);
    }

    private void SetNumericValue(string key, double? value)
    {
        var textBox = GetTextBox(key);
        var slider = GetSlider(key);
        _updatingNumericControl = true;
        textBox.Text = FormatOptionalDouble(value);
        if (value.HasValue)
        {
            slider.Value = Math.Clamp(value.Value, slider.Minimum, slider.Maximum);
        }
        else
        {
            slider.Value = slider.Minimum;
        }

        _updatingNumericControl = false;
    }

    private bool TryReadPointLightDefaults(out PointLightDefaults? defaults, out string error)
    {
        defaults = null;
        error = string.Empty;
        if (PointLightDefaultsEnabledCheckBox.IsChecked != true)
        {
            return true;
        }

        if (!TryReadOptionalRange("Radius", "Radius", 0, null, out var radius, out error)
            || !TryReadOptionalRange("Intensity", "Intensity scale", 0, null, out var intensity, out error)
            || !TryReadOptionalColor(PointLightColorHexTextBox.Text, "Inner color", out var color, out error)
            || !TryReadOptionalColor(PointLightOuterColorHexTextBox.Text, "Outer color", out var outerColor, out error)
            || !TryReadOptionalRange("GradientExponent", "Gradient exponent", 0, null, out var gradientExponent, out error)
            || !TryReadOptionalRange("Height", "Light height", 0, null, out var height, out error)
            || !TryReadOptionalRange("SwayAmount", "Sway amount", 0, null, out var swayAmount, out error)
            || !TryReadOptionalRange("SwayHz", "Sway frequency", 0, null, out var swayHz, out error)
            || !TryReadOptionalRange("SwayDirection", "Sway direction", null, null, out var swayDirection, out error)
            || !TryReadOptionalRange("FlickerAmount", "Flicker amount", 0, 1, out var flickerAmount, out error)
            || !TryReadOptionalRange("FlickerHz", "Flicker frequency", 0, null, out var flickerHz, out error))
        {
            return false;
        }

        var flickerStyle = NormalizeOptionalText(PointLightFlickerStyleComboBox.SelectedItem as string);
        if (flickerStyle is not null && flickerStyle is not ("swell" or "flame"))
        {
            error = "Flicker style must be swell or flame.";
            return false;
        }

        defaults = new PointLightDefaults
        {
            RadiusPx = radius,
            IntensityScale = intensity,
            Color = color,
            OuterColor = outerColor,
            GradientExponent = gradientExponent,
            LightHeightCells = height,
            SwayAmountPx = swayAmount,
            SwayHz = swayHz,
            SwayDirectionDeg = swayDirection,
            FlickerAmount = flickerAmount,
            FlickerHz = flickerHz,
            FlickerStyle = flickerStyle
        };
        return true;
    }

    private bool TryReadOptionalRange(string key, string fieldName, double? minimum, double? maximum, out double? value, out string error)
    {
        value = ParseOptionalDouble(GetTextBox(key).Text);
        error = string.Empty;
        var text = GetTextBox(key).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!value.HasValue
            || minimum.HasValue && value.Value < minimum.Value
            || maximum.HasValue && value.Value > maximum.Value)
        {
            var range = minimum.HasValue && maximum.HasValue
                ? $"{minimum:0.##} to {maximum:0.##}"
                : minimum.HasValue
                    ? $"{minimum:0.##} or greater"
                    : "a finite number";
            error = $"{fieldName} must be {range}, or blank to inherit the engine default.";
            return false;
        }

        return true;
    }

    private static bool TryReadOptionalColor(string text, string fieldName, out string? value, out string error)
    {
        value = NormalizeOptionalText(text);
        error = string.Empty;
        if (value is null)
        {
            return true;
        }

        if (value.Length != 7 || value[0] != '#' || value.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            error = $"{fieldName} must be a six-digit #RRGGBB value, or blank to inherit the engine default.";
            return false;
        }

        return true;
    }

    private void PickInnerColor_OnClick(object sender, RoutedEventArgs e)
    {
        PickColor(PointLightColorHexTextBox, PointLightColorPreview);
    }

    private void PickOuterColor_OnClick(object sender, RoutedEventArgs e)
    {
        PickColor(PointLightOuterColorHexTextBox, PointLightOuterColorPreview);
    }

    private void ClearInnerColor_OnClick(object sender, RoutedEventArgs e)
    {
        SetColorValue(PointLightColorHexTextBox, PointLightColorPreview, null);
    }

    private void ClearOuterColor_OnClick(object sender, RoutedEventArgs e)
    {
        SetColorValue(PointLightOuterColorHexTextBox, PointLightOuterColorPreview, null);
    }

    private void PickColor(TextBox textBox, Border preview)
    {
        var dialog = new ColorPickerDialog(textBox.Text)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            SetColorValue(textBox, preview, dialog.SelectedColor);
        }
    }

    private static void SetColorValue(TextBox textBox, Border preview, string? value)
    {
        var normalized = NormalizeOptionalText(value);
        textBox.Text = normalized ?? string.Empty;
        preview.Background = TryGetColorBrush(normalized) ?? Brushes.Transparent;
    }

    private static SolidColorBrush? TryGetColorBrush(string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            if (ColorConverter.ConvertFromString(value) is Color color)
            {
                return new SolidColorBrush(color);
            }
        }
        catch (FormatException)
        {
            // Validation on Save reports malformed values.
        }

        return null;
    }

    private TextBox GetTextBox(string key)
    {
        return key switch
        {
            "Radius" => PointLightRadiusTextBox,
            "Intensity" => PointLightIntensityTextBox,
            "GradientExponent" => PointLightGradientExponentTextBox,
            "Height" => PointLightHeightTextBox,
            "SwayAmount" => PointLightSwayAmountTextBox,
            "SwayHz" => PointLightSwayHzTextBox,
            "SwayDirection" => PointLightSwayDirectionTextBox,
            "FlickerAmount" => PointLightFlickerAmountTextBox,
            "FlickerHz" => PointLightFlickerHzTextBox,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown point-light numeric field.")
        };
    }

    private Slider GetSlider(string key)
    {
        return key switch
        {
            "Radius" => PointLightRadiusSlider,
            "Intensity" => PointLightIntensitySlider,
            "GradientExponent" => PointLightGradientExponentSlider,
            "Height" => PointLightHeightSlider,
            "SwayAmount" => PointLightSwayAmountSlider,
            "SwayHz" => PointLightSwayHzSlider,
            "SwayDirection" => PointLightSwayDirectionSlider,
            "FlickerAmount" => PointLightFlickerAmountSlider,
            "FlickerHz" => PointLightFlickerHzSlider,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown point-light numeric field.")
        };
    }

    private static double? ParseOptionalDouble(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && double.IsFinite(value)
            ? value
            : null;
    }

    private static string FormatOptionalDouble(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
