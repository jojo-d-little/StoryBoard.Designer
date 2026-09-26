using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StoryboardDesigner.App.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace StoryboardDesigner.App.Views;

public partial class ObjectLightingDialog : Window
{
    private bool _updatingNumericControl;

    public ObjectLightingDialog(
        PointLight? pointLight,
        LightOcclusion? lightOcclusion,
        bool isPointLightEnabledDefaultValue,
        int projectRoomGridCellSize,
        int footprintWidthCells,
        int footprintHeightCells)
    {
        InitializeComponent();

        PointLightMotionModeComboBox.ItemsSource = new[] { "", "static", "sway", "flicker", "sway-flicker" };
        PointLightFlickerStyleComboBox.ItemsSource = new[] { "", "swell", "flame" };
        PointLightEnabledCheckBox.IsChecked = pointLight is not null;
        PointLightDefaultEnabledCheckBox.IsChecked = pointLight is not null && !isPointLightEnabledDefaultValue;
        PointLightMotionModeComboBox.SelectedItem = pointLight?.MotionMode?.Trim() ?? string.Empty;
        PointLightFlickerStyleComboBox.SelectedItem = pointLight?.FlickerStyle?.Trim() ?? string.Empty;

        SetNumericValue("OffsetX", pointLight?.OffsetXPx);
        SetNumericValue("OffsetY", pointLight?.OffsetYPx);
        SetNumericValue("Radius", pointLight?.RadiusPx);
        SetNumericValue("Intensity", pointLight?.IntensityScale);
        SetNumericValue("Direction", pointLight?.DirectionDeg);
        SetNumericValue("ConeAngle", pointLight?.ConeAngleDeg);
        SetNumericValue("Phase", pointLight?.Phase);
        SetColorValue(PointLightColorHexTextBox, PointLightColorPreview, pointLight?.Color);
        SetColorValue(PointLightOuterColorHexTextBox, PointLightOuterColorPreview, pointLight?.OuterColor);
        SetNumericValue("GradientExponent", pointLight?.GradientExponent);
        SetNumericValue("Height", pointLight?.LightHeightCells);
        SetNumericValue("SwayAmount", pointLight?.SwayAmountPx);
        SetNumericValue("SwayHz", pointLight?.SwayHz);
        SetNumericValue("SwayDirection", pointLight?.SwayDirectionDeg);
        SetNumericValue("FlickerAmount", pointLight?.FlickerAmount);
        SetNumericValue("FlickerHz", pointLight?.FlickerHz);
        LightOcclusionEnabledCheckBox.IsChecked = lightOcclusion is not null;
        SetNumericValue("OcclusionStrength", lightOcclusion?.Strength);
        UpdateEditorEnabledState();
    }

    public PointLight? PointLight { get; private set; }
    public LightOcclusion? LightOcclusion { get; private set; }
    public bool IsPointLightEnabledDefaultValue { get; private set; }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPointLight(out var pointLight, out var error))
        {
            MessageBox.Show(this, error, "Object Lighting", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryReadLightOcclusion(out var occlusion, out error))
        {
            MessageBox.Show(this, error, "Object Lighting", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PointLight = pointLight;
        LightOcclusion = occlusion;
        IsPointLightEnabledDefaultValue = PointLightDefaultEnabledCheckBox.IsChecked != true;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void PointLightEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e) => UpdateEditorEnabledState();
    private void LightOcclusionEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e) => UpdateEditorEnabledState();

    private void PointLightMotionModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMotionEditorEnabledState();
    }

    private void UpdateEditorEnabledState()
    {
        PointLightEditorGrid.IsEnabled = PointLightEnabledCheckBox.IsChecked == true;
        PointLightDefaultEnabledCheckBox.IsEnabled = PointLightEnabledCheckBox.IsChecked == true;
        LightOcclusionStrengthTextBox.IsEnabled = LightOcclusionEnabledCheckBox.IsChecked == true;
        LightOcclusionStrengthSlider.IsEnabled = LightOcclusionEnabledCheckBox.IsChecked == true;
        UpdateMotionEditorEnabledState();
    }

    private void UpdateMotionEditorEnabledState()
    {
        var motionMode = PointLightMotionModeComboBox.SelectedItem as string;
        var swayEnabled = motionMode is "sway" or "sway-flicker";
        var flickerEnabled = motionMode is "flicker" or "sway-flicker";
        SwayEditorGrid.IsEnabled = PointLightEnabledCheckBox.IsChecked == true && swayEnabled;
        FlickerEditorGrid.IsEnabled = PointLightEnabledCheckBox.IsChecked == true && flickerEnabled;
    }

    private void NumericSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingNumericControl || sender is not Slider slider || slider.Tag is not string key)
        {
            return;
        }

        _updatingNumericControl = true;
        GetTextBox(key).Text = FormatNumericValue(key, e.NewValue);
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
        if (e.Key == Key.Enter && sender is TextBox textBox && textBox.Tag is string key)
        {
            UpdateSliderFromText(key);
            e.Handled = true;
        }
    }

    private void NumericDecrement_OnClick(object sender, RoutedEventArgs e) => NudgeNumericValue((sender as FrameworkElement)?.Tag as string, -1);
    private void NumericIncrement_OnClick(object sender, RoutedEventArgs e) => NudgeNumericValue((sender as FrameworkElement)?.Tag as string, 1);

    private void NudgeNumericValue(string? key, int direction)
    {
        if (key is null)
        {
            return;
        }

        var current = ParseOptionalDouble(GetTextBox(key).Text) ?? 0;
        var step = key is "OffsetX" or "OffsetY"
            ? 1d
            : GetSlider(key).SmallChange;
        SetNumericValue(key, current + direction * step);
    }

    private void UpdateSliderFromText(string key)
    {
        if (ParseOptionalDouble(GetTextBox(key).Text) is double value)
        {
            SetNumericValue(key, value);
        }
    }

    private void SetNumericValue(string key, double? value)
    {
        var textBox = GetTextBox(key);
        _updatingNumericControl = true;
        textBox.Text = value.HasValue ? FormatNumericValue(key, value.Value) : string.Empty;
        if (key is "OffsetX" or "OffsetY")
        {
            _updatingNumericControl = false;
            return;
        }

        var slider = GetSlider(key);
        slider.Value = value.HasValue
            ? Math.Clamp(RoundNumericValue(key, value.Value), slider.Minimum, slider.Maximum)
            : slider.Minimum;
        _updatingNumericControl = false;
    }

    private bool TryReadPointLight(out PointLight? pointLight, out string error)
    {
        pointLight = null;
        error = string.Empty;
        if (PointLightEnabledCheckBox.IsChecked != true)
        {
            return true;
        }

        if (!TryReadOptionalRange("OffsetX", "Origin X", null, null, out var offsetX, out error)
            || !TryReadOptionalRange("OffsetY", "Origin Y", null, null, out var offsetY, out error)
            || !TryReadOptionalRange("Radius", "Radius", 0, null, out var radius, out error)
            || !TryReadOptionalRange("Intensity", "Intensity scale", 0, null, out var intensity, out error)
            || !TryReadOptionalRange("Direction", "Direction", null, null, out var direction, out error)
            || !TryReadOptionalRange("ConeAngle", "Cone angle", 1, 360, out var coneAngle, out error)
            || !TryReadOptionalRange("GradientExponent", "Gradient exponent", 0, null, out var gradient, out error)
            || !TryReadOptionalRange("Height", "Light elevation offset", 0, null, out var height, out error)
            || !TryReadOptionalRange("SwayAmount", "Sway amount", 0, null, out var swayAmount, out error)
            || !TryReadOptionalRange("SwayHz", "Sway frequency", 0, null, out var swayHz, out error)
            || !TryReadOptionalRange("SwayDirection", "Sway direction", null, null, out var swayDirection, out error)
            || !TryReadOptionalRange("FlickerAmount", "Flicker amount", 0, 1, out var flickerAmount, out error)
            || !TryReadOptionalRange("FlickerHz", "Flicker frequency", 0, null, out var flickerHz, out error)
            || !TryReadOptionalTextBox(PointLightPhaseTextBox, "Phase offset", out var phase, out error)
            || !TryReadOptionalColor(PointLightColorHexTextBox.Text, "Inner color", out var color, out error)
            || !TryReadOptionalColor(PointLightOuterColorHexTextBox.Text, "Outer color", out var outerColor, out error))
        {
            return false;
        }

        var motionMode = NormalizeOptionalText(PointLightMotionModeComboBox.SelectedItem as string);
        if (motionMode is not null && motionMode is not ("static" or "sway" or "flicker" or "sway-flicker"))
        {
            error = "Motion mode must be static, sway, flicker, or sway-flicker.";
            return false;
        }

        var flickerStyle = NormalizeOptionalText(PointLightFlickerStyleComboBox.SelectedItem as string);
        if (flickerStyle is not null && flickerStyle is not ("swell" or "flame"))
        {
            error = "Flicker style must be swell or flame.";
            return false;
        }

        pointLight = new PointLight
        {
            OffsetXPx = offsetX,
            OffsetYPx = offsetY,
            RadiusPx = radius,
            DirectionDeg = direction,
            ConeAngleDeg = coneAngle,
            MotionMode = motionMode,
            Phase = phase,
            Color = color,
            OuterColor = outerColor,
            GradientExponent = gradient,
            IntensityScale = intensity,
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

    private bool TryReadLightOcclusion(out LightOcclusion? occlusion, out string error)
    {
        occlusion = null;
        error = string.Empty;
        if (LightOcclusionEnabledCheckBox.IsChecked != true)
        {
            return true;
        }

        if (!TryReadOptionalRange("OcclusionStrength", "Occlusion strength", 0, 1, out var strength, out error))
        {
            return false;
        }

        occlusion = new LightOcclusion { Strength = strength };
        return true;
    }

    private bool TryReadOptionalRange(string key, string fieldName, double? minimum, double? maximum, out double? value, out string error)
    {
        value = ParseOptionalDouble(GetTextBox(key).Text);
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(GetTextBox(key).Text))
        {
            return true;
        }

        if (!value.HasValue || minimum.HasValue && value.Value < minimum.Value || maximum.HasValue && value.Value > maximum.Value)
        {
            error = $"{fieldName} must be {DescribeRange(minimum, maximum)}, or blank to inherit the default.";
            return false;
        }

        return true;
    }

    private static bool TryReadOptionalTextBox(TextBox textBox, string fieldName, out double? value, out string error)
    {
        value = ParseOptionalDouble(textBox.Text);
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            return true;
        }

        if (!value.HasValue)
        {
            error = $"{fieldName} must be a finite number, or blank to inherit the default.";
            return false;
        }

        return true;
    }

    private static string DescribeRange(double? minimum, double? maximum) => minimum.HasValue && maximum.HasValue
        ? $"between {minimum:0.##} and {maximum:0.##}"
        : minimum.HasValue ? $"{minimum:0.##} or greater" : "a finite number";

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
            error = $"{fieldName} must be a six-digit #RRGGBB value, or blank to inherit the default.";
            return false;
        }

        return true;
    }

    private void PickInnerColor_OnClick(object sender, RoutedEventArgs e) => PickColor(PointLightColorHexTextBox, PointLightColorPreview);
    private void PickOuterColor_OnClick(object sender, RoutedEventArgs e) => PickColor(PointLightOuterColorHexTextBox, PointLightOuterColorPreview);
    private void ClearInnerColor_OnClick(object sender, RoutedEventArgs e) => SetColorValue(PointLightColorHexTextBox, PointLightColorPreview, null);
    private void ClearOuterColor_OnClick(object sender, RoutedEventArgs e) => SetColorValue(PointLightOuterColorHexTextBox, PointLightOuterColorPreview, null);

    private void PickColor(TextBox textBox, Border preview)
    {
        var dialog = new ColorPickerDialog(textBox.Text) { Owner = this };
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
            return ColorConverter.ConvertFromString(value) is Color color ? new SolidColorBrush(color) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private TextBox GetTextBox(string key) => key switch
    {
        "OffsetX" => PointLightOffsetXTextBox,
        "OffsetY" => PointLightOffsetYTextBox,
        "Radius" => PointLightRadiusTextBox,
        "Intensity" => PointLightIntensityTextBox,
        "Direction" => PointLightDirectionTextBox,
        "ConeAngle" => PointLightConeAngleTextBox,
        "Phase" => PointLightPhaseTextBox,
        "GradientExponent" => PointLightGradientExponentTextBox,
        "Height" => PointLightHeightTextBox,
        "SwayAmount" => PointLightSwayAmountTextBox,
        "SwayHz" => PointLightSwayHzTextBox,
        "SwayDirection" => PointLightSwayDirectionTextBox,
        "FlickerAmount" => PointLightFlickerAmountTextBox,
        "FlickerHz" => PointLightFlickerHzTextBox,
        "OcclusionStrength" => LightOcclusionStrengthTextBox,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown object-lighting numeric field.")
    };

    private Slider GetSlider(string key) => key switch
    {
        "Radius" => PointLightRadiusSlider,
        "Intensity" => PointLightIntensitySlider,
        "Direction" => PointLightDirectionSlider,
        "ConeAngle" => PointLightConeAngleSlider,
        "Phase" => PointLightPhaseSlider,
        "GradientExponent" => PointLightGradientExponentSlider,
        "Height" => PointLightHeightSlider,
        "SwayAmount" => PointLightSwayAmountSlider,
        "SwayHz" => PointLightSwayHzSlider,
        "SwayDirection" => PointLightSwayDirectionSlider,
        "FlickerAmount" => PointLightFlickerAmountSlider,
        "FlickerHz" => PointLightFlickerHzSlider,
        "OcclusionStrength" => LightOcclusionStrengthSlider,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown object-lighting numeric field.")
    };

    private static double? ParseOptionalDouble(string text) => string.IsNullOrWhiteSpace(text)
        ? null
        : double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    private static string FormatOptionalDouble(double? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatNumericValue(string key, double value)
    {
        var precision = key switch
        {
            "OffsetX" or "OffsetY" or "Radius" or "Direction" or "ConeAngle" or "SwayAmount" or "SwayDirection" => 0,
            "Height" => 1,
            _ => 2
        };
        return Math.Round(value, precision, MidpointRounding.AwayFromZero).ToString(precision == 0 ? "0" : $"0.{new string('0', precision)}", CultureInfo.InvariantCulture);
    }

    private static double RoundNumericValue(string key, double value) => double.Parse(FormatNumericValue(key, value), CultureInfo.InvariantCulture);
    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
