using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfBrushes = System.Windows.Media.Brushes;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class RoomSettingsDialog : Window
{
    private sealed record OrientationPresetOption(string Label, string Value);

    private const int MinimumGridCells = 4;
    private const int MaximumGridCells = 200;

    private readonly int _projectDefaultCanvasWidth;
    private readonly int _projectDefaultCanvasHeight;
    private readonly int _projectGridCellSize;
    private readonly int? _spawnPointRenderZOrder;
    private int _roomCanvasWidth;
    private int _roomCanvasHeight;
    private bool _isApplyingPreset;
    private bool _updatingAmbientControl;

    public RoomSettingsDialog(RoomSettingsEditRequest initialValues)
    {
        InitializeComponent();
        _projectDefaultCanvasWidth = initialValues.RoomCanvasWidth > 0 ? initialValues.RoomCanvasWidth : 800;
        _projectDefaultCanvasHeight = initialValues.RoomCanvasHeight > 0 ? initialValues.RoomCanvasHeight : 600;
        _roomCanvasWidth = _projectDefaultCanvasWidth;
        _roomCanvasHeight = _projectDefaultCanvasHeight;
        _projectGridCellSize = initialValues.ProjectRoomGridCellSize > 0 ? initialValues.ProjectRoomGridCellSize : 40;
        _spawnPointRenderZOrder = initialValues.SpawnPoint?.RenderZOrder;

        RoomNameTextBox.Text = initialValues.Name;
        RoomNameInGameTextBox.Text = initialValues.NameInGame;
        RoomNameSynonymsTextBox.Text = NormalizeSynonymsText(initialValues.NameSynonyms);
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        RoomCanvasWidthTextBox.Text = _roomCanvasWidth.ToString();
        RoomCanvasHeightTextBox.Text = _roomCanvasHeight.ToString();
        PopulateOrientationPresetOptions();
        UpdateOrientationPresetSelection();
        UpdateEffectiveGridSummary();
        PopulateAmbientLighting(initialValues.AmbientLighting);
        SpawnPointXTextBox.Text = (initialValues.SpawnPoint?.X ?? _roomCanvasWidth / 2d).ToString(CultureInfo.InvariantCulture);
        SpawnPointYTextBox.Text = (initialValues.SpawnPoint?.Y ?? _roomCanvasHeight / 2d).ToString(CultureInfo.InvariantCulture);
        EnvironmentTypeComboBox.SelectedItem = initialValues.EnvironmentType.ToString();
        SoundLossOverrideTextBox.Text = initialValues.SoundLossPercentOverride?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public RoomSettingsEditRequest Values => new(
        RoomNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        RoomNameInGameTextBox.Text.Trim(),
        _roomCanvasWidth,
        _roomCanvasHeight,
        _projectGridCellSize,
        ReadAmbientLighting(),
        NormalizeSynonymsText(RoomNameSynonymsTextBox.Text),
        TryReadCoordinate(SpawnPointXTextBox.Text, out var spawnX) && TryReadCoordinate(SpawnPointYTextBox.Text, out var spawnY)
            ? new RoomSpawnPoint(spawnX, spawnY, _spawnPointRenderZOrder)
            : new RoomSpawnPoint(_roomCanvasWidth / 2d, _roomCanvasHeight / 2d, _spawnPointRenderZOrder),
        Enum.TryParse<RoomEnvironmentType>(EnvironmentTypeComboBox.SelectedItem as string, out var environmentType) ? environmentType : RoomEnvironmentType.Indoor,
        TryReadPercent(SoundLossOverrideTextBox.Text, out var soundLoss) ? soundLoss : null);

    private static string NormalizeSynonymsText(string? raw)
    {
        return string.Join(", ",
            (raw ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(static token => token.Trim())
                .Where(static token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RoomNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter a room name.", "Room Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryParseDimension(RoomCanvasWidthTextBox.Text, "width", out var width)
            || !TryParseDimension(RoomCanvasHeightTextBox.Text, "height", out var height))
        {
            return;
        }

        if (width % _projectGridCellSize != 0 || height % _projectGridCellSize != 0)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Room width and height must be divisible by the project grid cell size ({_projectGridCellSize}px).",
                "Room Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var columns = width / _projectGridCellSize;
        var rows = height / _projectGridCellSize;
        if (columns is < MinimumGridCells or > MaximumGridCells || rows is < MinimumGridCells or > MaximumGridCells)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Room dimensions must produce between {MinimumGridCells} and {MaximumGridCells} columns/rows at {_projectGridCellSize}px cells.",
                "Room Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!TryReadAmbientLighting(out _, out var ambientLightingError))
        {
            MessageBox.Show(this, ambientLightingError, "Room Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(SpawnPointXTextBox.Text) != string.IsNullOrWhiteSpace(SpawnPointYTextBox.Text)
            || (!string.IsNullOrWhiteSpace(SpawnPointXTextBox.Text)
                && (!TryReadCoordinate(SpawnPointXTextBox.Text, out var spawnX)
                    || !TryReadCoordinate(SpawnPointYTextBox.Text, out var spawnY)
                    || spawnX < 0 || spawnX > width || spawnY < 0 || spawnY > height)))
        {
            MessageBox.Show(this, "Enter both spawn-point coordinates within the room bounds, or clear both fields to reset the point to the room center.", "Room Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(SoundLossOverrideTextBox.Text)
            && !TryReadPercent(SoundLossOverrideTextBox.Text, out _))
        {
            MessageBox.Show(this, "Sound loss must be a number from 0 to 100 percent, or left blank to inherit the project default.", "Room Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _roomCanvasWidth = width;
        _roomCanvasHeight = height;

        DialogResult = true;
    }

    private static bool TryReadCoordinate(string? text, out double value) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static bool TryReadPercent(string? text, out double value) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value) && value is >= 0 and <= 100;

    private void AmbientLightingEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        var isEnabled = AmbientLightingEnabledCheckBox.IsChecked == true;
        AmbientIntensityTextBox.IsEnabled = isEnabled;
        AmbientIntensitySlider.IsEnabled = isEnabled;
        AmbientColorHexTextBox.IsEnabled = isEnabled;
    }

    private void PopulateAmbientLighting(RoomAmbientLighting? lighting)
    {
        AmbientLightingEnabledCheckBox.IsChecked = lighting is not null;
        AmbientIntensityTextBox.Text = lighting?.Ambient.HasValue == true
            ? FormatAmbient(lighting.Ambient.Value)
            : string.Empty;
        AmbientIntensitySlider.Value = Math.Clamp(lighting?.Ambient ?? 1, 0, 1);
        SetAmbientColor(lighting?.AmbientColor);
        AmbientLightingEnabledCheckBox_OnChanged(this, new RoutedEventArgs());
    }

    private void AmbientIntensitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingAmbientControl)
        {
            return;
        }

        _updatingAmbientControl = true;
        AmbientIntensityTextBox.Text = FormatAmbient(e.NewValue);
        _updatingAmbientControl = false;
    }

    private void AmbientIntensityTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(AmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value))
        {
            SetAmbientIntensityValue(value);
        }
    }

    private void AmbientIntensityDecrement_OnClick(object sender, RoutedEventArgs e)
    {
        var current = double.TryParse(AmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
        SetAmbientIntensityValue(Math.Clamp(current - 0.05, 0, 1));
    }

    private void AmbientIntensityIncrement_OnClick(object sender, RoutedEventArgs e)
    {
        var current = double.TryParse(AmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
        SetAmbientIntensityValue(Math.Clamp(current + 0.05, 0, 1));
    }

    private void SetAmbientIntensityValue(double value)
    {
        var normalized = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        _updatingAmbientControl = true;
        AmbientIntensityTextBox.Text = FormatAmbient(normalized);
        AmbientIntensitySlider.Value = Math.Clamp(normalized, 0, 1);
        _updatingAmbientControl = false;
    }

    private void PickAmbientColor_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(AmbientColorHexTextBox.Text)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            SetAmbientColor(dialog.SelectedColor);
        }
    }

    private void ClearAmbientColor_OnClick(object sender, RoutedEventArgs e)
    {
        SetAmbientColor(null);
    }

    private void SetAmbientColor(string? value)
    {
        var normalized = NormalizeOptionalText(value);
        AmbientColorHexTextBox.Text = normalized ?? string.Empty;
        AmbientColorPreview.Background = TryGetColorBrush(normalized) ?? WpfBrushes.Transparent;
    }

    private RoomAmbientLighting? ReadAmbientLighting()
    {
        if (AmbientLightingEnabledCheckBox.IsChecked != true)
        {
            return null;
        }

        return new RoomAmbientLighting
        {
            Ambient = ParseOptionalDouble(AmbientIntensityTextBox.Text),
            AmbientColor = NormalizeOptionalText(AmbientColorHexTextBox.Text)
        };
    }

    private bool TryReadAmbientLighting(out RoomAmbientLighting? lighting, out string error)
    {
        lighting = null;
        error = string.Empty;
        if (AmbientLightingEnabledCheckBox.IsChecked != true)
        {
            return true;
        }

        var ambient = ParseOptionalDouble(AmbientIntensityTextBox.Text);
        if (!string.IsNullOrWhiteSpace(AmbientIntensityTextBox.Text)
            && (!ambient.HasValue || ambient.Value < 0 || ambient.Value > 1))
        {
            error = "Ambient intensity must be between 0 and 1, or blank to use the runtime default.";
            return false;
        }

        var color = NormalizeOptionalText(AmbientColorHexTextBox.Text);
        if (color is not null && (color.Length != 7 || color[0] != '#' || color.Skip(1).Any(character => !Uri.IsHexDigit(character))))
        {
            error = "Ambient color must be a six-digit #RRGGBB value, or blank to use the runtime default.";
            return false;
        }

        lighting = new RoomAmbientLighting
        {
            Ambient = ambient,
            AmbientColor = color
        };
        return true;
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

    private static string FormatAmbient(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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
            // Save validation reports malformed values.
        }

        return null;
    }

    private void OrientationPresetComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isApplyingPreset)
        {
            return;
        }

        if (OrientationPresetComboBox.SelectedValue is not string preset)
        {
            return;
        }

        switch (preset)
        {
            case "landscape":
                RoomCanvasWidthTextBox.Text = _projectDefaultCanvasWidth.ToString();
                RoomCanvasHeightTextBox.Text = _projectDefaultCanvasHeight.ToString();
                break;
            case "portrait":
                RoomCanvasWidthTextBox.Text = _projectDefaultCanvasHeight.ToString();
                RoomCanvasHeightTextBox.Text = _projectDefaultCanvasWidth.ToString();
                break;
        }

        UpdateEffectiveGridSummary();
    }

    private void RoomCanvasTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateOrientationPresetSelection();
        UpdateEffectiveGridSummary();
    }

    private void PopulateOrientationPresetOptions()
    {
        OrientationPresetComboBox.ItemsSource = new List<OrientationPresetOption>
        {
            new OrientationPresetOption($"Landscape ({_projectDefaultCanvasWidth} x {_projectDefaultCanvasHeight})", "landscape"),
            new OrientationPresetOption($"Portrait ({_projectDefaultCanvasHeight} x {_projectDefaultCanvasWidth})", "portrait"),
            new OrientationPresetOption("Custom", "custom")
        };
    }

    private void UpdateOrientationPresetSelection()
    {
        if (!int.TryParse(RoomCanvasWidthTextBox.Text, out var width)
            || !int.TryParse(RoomCanvasHeightTextBox.Text, out var height))
        {
            return;
        }

        var selectedValue = width == _projectDefaultCanvasWidth && height == _projectDefaultCanvasHeight
            ? "landscape"
            : width == _projectDefaultCanvasHeight && height == _projectDefaultCanvasWidth
                ? "portrait"
                : "custom";

        _isApplyingPreset = true;
        try
        {
            OrientationPresetComboBox.SelectedValue = selectedValue;
        }
        finally
        {
            _isApplyingPreset = false;
        }
    }

    private void UpdateEffectiveGridSummary()
    {
        if (!int.TryParse(RoomCanvasWidthTextBox.Text, out var width)
            || !int.TryParse(RoomCanvasHeightTextBox.Text, out var height)
            || width <= 0
            || height <= 0)
        {
            EffectiveGridSummaryTextBlock.Text = "Enter positive numeric width and height values.";
            return;
        }

        var columns = width / Math.Max(1, _projectGridCellSize);
        var rows = height / Math.Max(1, _projectGridCellSize);
        var divisibilityHint = width % _projectGridCellSize == 0 && height % _projectGridCellSize == 0
            ? string.Empty
            : " (not divisible by project cell size)";

        EffectiveGridSummaryTextBlock.Text =
            $"{width} x {height} at {_projectGridCellSize}px cells -> {columns} columns x {rows} rows{divisibilityHint}";
    }

    private bool TryParseDimension(string? value, string label, out int dimension)
    {
        if (!int.TryParse(value, out dimension) || dimension <= 0)
        {
            System.Windows.MessageBox.Show(this, $"Room {label} must be a positive whole number.", "Room Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            dimension = 0;
            return false;
        }

        return true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
