using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.RuntimeContracts.Enums;
using WpfComboBox = System.Windows.Controls.ComboBox;
using MessageBox = System.Windows.MessageBox;

namespace StoryboardDesigner.App.Views;

public partial class ManageWeatherDialog : Window
{
    public sealed record WeatherValues(SkyCondition? SkyCondition, Precipitation? Precipitation, WeatherIntensity? WeatherIntensity,
        Temperature? Temperature, Storming? Storming, int? WindDirection, double? WindSpeed, double? InheritedWindSpeed);

    private readonly WeatherValues _initial;

    public ManageWeatherDialog(WeatherValues initial)
    {
        InitializeComponent();
        _initial = initial;
        Setup(SkyConditionComboBox, initial.SkyCondition);
        Setup(PrecipitationComboBox, initial.Precipitation);
        Setup(WeatherIntensityComboBox, initial.WeatherIntensity);
        Setup(TemperatureComboBox, initial.Temperature);
        Setup(StormingComboBox, initial.Storming);
        WindDirectionTextBox.Text = initial.WindDirection?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        WindSpeedTextBox.Text = initial.WindSpeed?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
        UpdateEffectiveWindSpeedText();
    }

    public WeatherValues Values => new(Read<SkyCondition>(SkyConditionComboBox), Read<Precipitation>(PrecipitationComboBox),
        Read<WeatherIntensity>(WeatherIntensityComboBox), Read<Temperature>(TemperatureComboBox), Read<Storming>(StormingComboBox),
        int.TryParse(WindDirectionTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wind) ? wind : null,
        double.TryParse(WindSpeedTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) ? speed : null,
        _initial.InheritedWindSpeed);

    private static void Setup<T>(WpfComboBox combo, T? value) where T : struct, Enum
    {
        combo.ItemsSource = new[] { "(Inherit)" }.Concat(Enum.GetNames<T>()).ToArray();
        combo.SelectedItem = value?.ToString() ?? "(Inherit)";
    }

    private static T? Read<T>(WpfComboBox combo) where T : struct, Enum =>
        combo.SelectedItem is string value && Enum.TryParse<T>(value, out var parsed) ? parsed : null;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var text = WindDirectionTextBox.Text.Trim();
        if (text.Length > 0 && (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wind) || wind is < -1 or > 359))
        {
            MessageBox.Show(this, "Wind direction must be -1, 0 through 359, or blank to inherit.", "Manage Weather", MessageBoxButton.OK, MessageBoxImage.Information);
            WindDirectionTextBox.Focus();
            return;
        }
        var speedText = WindSpeedTextBox.Text.Trim();
        if (speedText.Length > 0 && (!double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed)
            || !double.IsFinite(speed) || speed < 0))
        {
            MessageBox.Show(this, "Wind speed must be a finite nonnegative number, or blank to inherit.", "Manage Weather", MessageBoxButton.OK, MessageBoxImage.Information);
            WindSpeedTextBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void WindSpeedTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateEffectiveWindSpeedText();

    private void UpdateEffectiveWindSpeedText()
    {
        var text = WindSpeedTextBox.Text.Trim();
        var effective = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var authored) && double.IsFinite(authored)
            ? authored
            : _initial.InheritedWindSpeed;
        EffectiveWindSpeedTextBlock.Text = effective is double speed
            ? $"Effective: {speed.ToString("0.###", CultureInfo.InvariantCulture)} px/s{(text.Length == 0 ? " (inherited)" : " (phase value)")}"
            : "Effective: not set";
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
