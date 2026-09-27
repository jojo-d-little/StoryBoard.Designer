using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;
using WpfRepeatButton = System.Windows.Controls.Primitives.RepeatButton;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace StoryboardDesigner.App.Views;

public partial class ColorPickerDialog : Window
{
    private bool _draggingColorField;
    private bool _draggingHueField;
    private double _hue;
    private double _saturation;
    private double _brightness;
    private Color _color;

    public ColorPickerDialog(string? initialColor)
    {
        InitializeComponent();
        _color = TryParseHex(initialColor, out var parsed) ? parsed : Colors.White;
        UpdateHsvFromColor();
        Loaded += (_, _) => RefreshControls();
    }

    public string? SelectedColor { get; private set; }

    private void ColorField_OnMouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        _draggingColorField = true;
        ColorField.CaptureMouse();
        UpdateColorField(e.GetPosition(ColorField));
        e.Handled = true;
    }

    private void ColorField_OnMouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        _draggingColorField = false;
        ColorField.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void ColorField_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_draggingColorField)
        {
            UpdateColorField(e.GetPosition(ColorField));
        }
    }

    private void HueField_OnMouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        _draggingHueField = true;
        HueField.CaptureMouse();
        UpdateHue(e.GetPosition(HueTrack));
        e.Handled = true;
    }

    private void HueField_OnMouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        _draggingHueField = false;
        HueField.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HueField_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_draggingHueField)
        {
            UpdateHue(e.GetPosition(HueTrack));
        }
    }

    private void UpdateColorField(Point point)
    {
        if (ColorField.ActualWidth <= 0 || ColorField.ActualHeight <= 0)
        {
            return;
        }

        _saturation = Math.Clamp(point.X / ColorField.ActualWidth, 0, 1);
        _brightness = Math.Clamp(1 - (point.Y / ColorField.ActualHeight), 0, 1);
        _color = HsvToColor(_hue, _saturation, _brightness);
        RefreshControls();
    }

    private void UpdateHue(Point point)
    {
        if (HueTrack.ActualHeight <= 0)
        {
            return;
        }

        SetHue(Math.Clamp(point.Y / HueTrack.ActualHeight, 0, 1) * 360);
    }

    private void HueSwatch_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string hueText }
            && double.TryParse(hueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var hue))
        {
            SetHue(hue);
        }
    }

    private void HueNudge_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfRepeatButton { Tag: string amountText }
            && double.TryParse(amountText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            SetHue(_hue + amount);
        }
    }

    private void SetHue(double hue)
    {
        _hue = ((hue % 360) + 360) % 360;
        _color = HsvToColor(_hue, _saturation, _brightness);
        RefreshControls();
    }

    private void ColorComponentTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string channel)
        {
            return;
        }

        if (!byte.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            RefreshControls();
            return;
        }

        _color = channel switch
        {
            "Red" => Color.FromRgb(value, _color.G, _color.B),
            "Green" => Color.FromRgb(_color.R, value, _color.B),
            "Blue" => Color.FromRgb(_color.R, _color.G, value),
            _ => _color
        };
        UpdateHsvFromColor();
        RefreshControls();
    }

    private void ApplyHex_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseHex(HexTextBox.Text, out var parsed))
        {
            MessageBox.Show(this, "Enter a color in #RRGGBB format.", "Choose Color", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _color = parsed;
        UpdateHsvFromColor();
        RefreshControls();
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseHex(HexTextBox.Text, out var parsed))
        {
            MessageBox.Show(this, "Enter a color in #RRGGBB format.", "Choose Color", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _color = parsed;
        SelectedColor = FormatHex(_color);
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void RefreshControls()
    {
        ColorField.Background = new SolidColorBrush(HsvToColor(_hue, 1, 1));
        RedTextBox.Text = _color.R.ToString(CultureInfo.InvariantCulture);
        GreenTextBox.Text = _color.G.ToString(CultureInfo.InvariantCulture);
        BlueTextBox.Text = _color.B.ToString(CultureInfo.InvariantCulture);
        HexTextBox.Text = FormatHex(_color);
        ColorPreview.Background = new SolidColorBrush(_color);
        PositionColorFieldThumb();
        PositionHueThumb();
        RefreshHueSwatchSelection();
    }

    private void RefreshHueSwatchSelection()
    {
        foreach (var swatch in HueSwatchPanel.Children.OfType<WpfButton>())
        {
            if (swatch.Tag is not string hueText
                || !double.TryParse(hueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var swatchHue))
            {
                continue;
            }

            var difference = Math.Abs(_hue - swatchHue);
            var circularDifference = Math.Min(difference, 360 - difference);
            var selected = circularDifference < 0.5;
            swatch.BorderBrush = selected ? System.Windows.Media.Brushes.Black : new SolidColorBrush(Color.FromRgb(107, 114, 128));
            swatch.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
        }
    }

    private void PositionColorFieldThumb()
    {
        var x = (_saturation * ColorField.ActualWidth) - (ColorFieldThumb.Width / 2);
        var y = ((1 - _brightness) * ColorField.ActualHeight) - (ColorFieldThumb.Height / 2);
        Canvas.SetLeft(ColorFieldThumb, Math.Clamp(x, -ColorFieldThumb.Width / 2, ColorField.ActualWidth - ColorFieldThumb.Width / 2));
        Canvas.SetTop(ColorFieldThumb, Math.Clamp(y, -ColorFieldThumb.Height / 2, ColorField.ActualHeight - ColorFieldThumb.Height / 2));
    }

    private void PositionHueThumb()
    {
        var y = ((_hue % 360 + 360) % 360 / 360 * HueTrack.ActualHeight) - (HueThumb.Height / 2);
        Canvas.SetLeft(HueThumb, (HueThumbCanvas.ActualWidth - HueThumb.Width) / 2);
        Canvas.SetTop(HueThumb, Math.Clamp(y, -HueThumb.Height / 2, HueTrack.ActualHeight - HueThumb.Height / 2));
    }

    private void UpdateHsvFromColor()
    {
        var red = _color.R / 255d;
        var green = _color.G / 255d;
        var blue = _color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        _brightness = max;
        _saturation = max <= double.Epsilon ? 0 : delta / max;
        if (delta <= double.Epsilon)
        {
            return;
        }

        _hue = max switch
        {
            _ when Math.Abs(max - red) < double.Epsilon => 60 * (((green - blue) / delta) % 6),
            _ when Math.Abs(max - green) < double.Epsilon => 60 * (((blue - red) / delta) + 2),
            _ => 60 * (((red - green) / delta) + 4)
        };

        if (_hue < 0)
        {
            _hue += 360;
        }
    }

    private static Color HsvToColor(double hue, double saturation, double brightness)
    {
        var normalizedHue = ((hue % 360) + 360) % 360;
        var chroma = brightness * saturation;
        var x = chroma * (1 - Math.Abs((normalizedHue / 60 % 2) - 1));
        var match = brightness - chroma;
        var rgb = normalizedHue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return Color.FromRgb(
            ToByte(rgb.Item1 + match),
            ToByte(rgb.Item2 + match),
            ToByte(rgb.Item3 + match));
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
    }

    private static bool TryParseHex(string? text, out Color color)
    {
        color = default;
        var value = text?.Trim() ?? string.Empty;
        if (value.Length != 7 || value[0] != '#'
            || !byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
            || !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
            || !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return false;
        }

        color = Color.FromRgb(red, green, blue);
        return true;
    }

    private static string FormatHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
