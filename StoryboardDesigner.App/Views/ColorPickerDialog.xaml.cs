using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace StoryboardDesigner.App.Views;

public partial class ColorPickerDialog : Window
{
    private bool _updatingControls;
    private Color _color;

    public ColorPickerDialog(string? initialColor)
    {
        InitializeComponent();
        _color = TryParseHex(initialColor, out var parsed) ? parsed : Colors.White;
        RefreshControls();
    }

    public string? SelectedColor { get; private set; }

    private void ColorSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingControls || sender is not Slider slider || slider.Tag is not string channel)
        {
            return;
        }

        var value = (byte)Math.Clamp((int)Math.Round(e.NewValue), 0, 255);
        _color = channel switch
        {
            "Red" => Color.FromRgb(value, _color.G, _color.B),
            "Green" => Color.FromRgb(_color.R, value, _color.B),
            "Blue" => Color.FromRgb(_color.R, _color.G, value),
            _ => _color
        };
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

        value = Math.Clamp(value, (byte)0, (byte)255);
        _color = channel switch
        {
            "Red" => Color.FromRgb(value, _color.G, _color.B),
            "Green" => Color.FromRgb(_color.R, value, _color.B),
            "Blue" => Color.FromRgb(_color.R, _color.G, value),
            _ => _color
        };
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
        _updatingControls = true;
        RedSlider.Value = _color.R;
        GreenSlider.Value = _color.G;
        BlueSlider.Value = _color.B;
        RedTextBox.Text = _color.R.ToString(CultureInfo.InvariantCulture);
        GreenTextBox.Text = _color.G.ToString(CultureInfo.InvariantCulture);
        BlueTextBox.Text = _color.B.ToString(CultureInfo.InvariantCulture);
        HexTextBox.Text = FormatHex(_color);
        ColorPreview.Background = new SolidColorBrush(_color);
        _updatingControls = false;
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
