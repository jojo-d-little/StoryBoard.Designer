using System.Globalization;
using System.Windows;
using System.Windows.Media;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.Config;

namespace StoryboardDesigner.App.Views;

public partial class GlobalSettingsDialog : Window
{
    private const int PreferredDefaultGridCellSize = 40;
    private List<SoundEffectLibraryEntry> _soundEffectLibraryEntries;
    private string _gameDisplayName;
    private string _gameSummary;
    private List<string> _gamePreviewImages;
    private PointLightDefaults? _pointLightDefaults;
    private RoomAmbientLighting _defaultRoomAmbientLighting = new() { Ambient = 1, AmbientColor = "#FFFFFF" };
    private bool _updatingAmbientControl;
    private readonly IReadOnlyList<string> _soundEffectCategorySuggestions;

    public GlobalSettingsDialog(
        GlobalSettingsEditRequest initialValues,
        IReadOnlyList<string> startingPlanetOptions,
        IReadOnlyList<string> playerCharacterObjectOptions,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectLibraryEntries,
        IReadOnlyList<string>? soundEffectCategorySuggestions = null)
    {
        InitializeComponent();
        AutoSaveSecondsTextBox.Text = initialValues.AutoSaveSeconds.ToString();
        StartingPlanetComboBox.ItemsSource = startingPlanetOptions;
        StartingPlanetComboBox.SelectedItem = initialValues.StartingPlanetName;
        PlayerCharacterComboBox.ItemsSource = playerCharacterObjectOptions;
        PlayerCharacterComboBox.SelectedItem = initialValues.PlayerCharacterObjectName;
        RoomCanvasWidthTextBox.Text = initialValues.RoomImageCanvasWidth.ToString();
        RoomCanvasHeightTextBox.Text = initialValues.RoomImageCanvasHeight.ToString();
        RefreshGridCellSizeOptions(initialValues.RoomDesignerGridCellSize);
        StackScaleStepDefaultTextBox.Text = initialValues.StackScaleStepDefault.ToString("0.###", CultureInfo.InvariantCulture);
        MinStackScaleDefaultTextBox.Text = initialValues.MinStackScaleDefault.ToString("0.###", CultureInfo.InvariantCulture);
        _pointLightDefaults = LightingModelCloner.ClonePointLightDefaults(initialValues.PointLightDefaults);
        _defaultRoomAmbientLighting = LightingModelCloner.CloneRoomAmbientLighting(initialValues.DefaultRoomAmbientLighting)
            ?? new RoomAmbientLighting { Ambient = 1, AmbientColor = "#FFFFFF" };
        SetDefaultRoomAmbientIntensity(_defaultRoomAmbientLighting.Ambient ?? 1);
        SetDefaultRoomAmbientColor(_defaultRoomAmbientLighting.AmbientColor ?? "#FFFFFF");
        IndoorSoundLossPercentTextBox.Text = (initialValues.RoomSoundLossDefaults?.IndoorSoundLossPercent ?? 0).ToString(CultureInfo.InvariantCulture);
        OutdoorSoundLossPercentTextBox.Text = (initialValues.RoomSoundLossDefaults?.OutdoorSoundLossPercent ?? 0).ToString(CultureInfo.InvariantCulture);
        OpenSpaceSoundLossPercentPerCellTextBox.Text = (initialValues.RoomSoundLossDefaults?.OpenSpaceSoundLossPercentPerCell ?? 0).ToString(CultureInfo.InvariantCulture);
        RefreshLightingSummary();
        _gameDisplayName = initialValues.GameDisplayName?.Trim() ?? string.Empty;
        _gameSummary = initialValues.GameSummary?.Trim() ?? string.Empty;
        _gamePreviewImages = NormalizePreviewImages(initialValues.GamePreviewImages);
        _soundEffectLibraryEntries = soundEffectLibraryEntries.Select(CloneSoundEffectEntry).ToList();
        _soundEffectCategorySuggestions = soundEffectCategorySuggestions is null
            ? Array.Empty<string>()
            : soundEffectCategorySuggestions.ToList();
    }

    public GlobalSettingsEditRequest Values
    {
        get
        {
            var roomGridCellSize = RoomGridCellSizeComboBox.SelectedItem is int selectedSize
                ? selectedSize
                : -1;

            return new GlobalSettingsEditRequest(
                int.TryParse(AutoSaveSecondsTextBox.Text.Trim(), out var seconds) ? seconds : -1,
                StartingPlanetComboBox.SelectedItem as string ?? string.Empty,
                PlayerCharacterComboBox.SelectedItem as string ?? string.Empty,
                int.TryParse(RoomCanvasWidthTextBox.Text.Trim(), out var roomCanvasWidth) ? roomCanvasWidth : -1,
                int.TryParse(RoomCanvasHeightTextBox.Text.Trim(), out var roomCanvasHeight) ? roomCanvasHeight : -1,
                roomGridCellSize,
                TryParseStackScaleStep(StackScaleStepDefaultTextBox.Text.Trim(), out var stackScaleStepDefault) ? stackScaleStepDefault : -1,
                TryParseMinStackScale(MinStackScaleDefaultTextBox.Text.Trim(), out var minStackScaleDefault) ? minStackScaleDefault : -1,
                _gameDisplayName,
                _gameSummary,
                _gamePreviewImages.ToList(),
                LightingModelCloner.ClonePointLightDefaults(_pointLightDefaults),
                new RoomAmbientLighting
                {
                    Ambient = double.TryParse(DefaultRoomAmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ambient) ? ambient : null,
                    AmbientColor = DefaultRoomAmbientColorHexTextBox.Text.Trim()
                },
                new RoomSoundLossDefaults
                {
                    IndoorSoundLossPercent = TryParseSoundLossPercent(IndoorSoundLossPercentTextBox.Text, out var indoorSoundLoss) ? indoorSoundLoss : -1,
                    OutdoorSoundLossPercent = TryParseSoundLossPercent(OutdoorSoundLossPercentTextBox.Text, out var outdoorSoundLoss) ? outdoorSoundLoss : -1,
                    OpenSpaceSoundLossPercentPerCell = TryParseSoundLossPercent(OpenSpaceSoundLossPercentPerCellTextBox.Text, out var openSpaceSoundLoss) ? openSpaceSoundLoss : -1
                });
        }
    }

    public IReadOnlyList<SoundEffectLibraryEntry> SoundEffectLibraryEntries => _soundEffectLibraryEntries;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AutoSaveSecondsTextBox.Text.Trim(), out var seconds) || seconds < 0)
        {
            System.Windows.MessageBox.Show(this, "Please enter a non-negative whole number for auto save seconds.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(RoomCanvasWidthTextBox.Text.Trim(), out var roomCanvasWidth) || roomCanvasWidth <= 0)
        {
            System.Windows.MessageBox.Show(this, "Please enter a positive whole number for room canvas width.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse(RoomCanvasHeightTextBox.Text.Trim(), out var roomCanvasHeight) || roomCanvasHeight <= 0)
        {
            System.Windows.MessageBox.Show(this, "Please enter a positive whole number for room canvas height.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (RoomGridCellSizeComboBox.SelectedItem is not int roomGridCellSize || roomGridCellSize <= 0)
        {
            System.Windows.MessageBox.Show(this, "Please choose a valid room grid cell size from the list.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryParseStackScaleStep(StackScaleStepDefaultTextBox.Text.Trim(), out _))
        {
            System.Windows.MessageBox.Show(this, $"Please enter a decimal number from {StackScalePolicy.MinStackScaleStep:0.##} to {StackScalePolicy.MaxStackScaleStep:0.##} for stack scale step.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryParseMinStackScale(MinStackScaleDefaultTextBox.Text.Trim(), out _))
        {
            System.Windows.MessageBox.Show(this, $"Please enter a decimal number from {StackScalePolicy.MinMinStackScale:0.##} to {StackScalePolicy.MaxMinStackScale:0.##} for min stack scale.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryParseSoundLossPercent(IndoorSoundLossPercentTextBox.Text, out _)
            || !TryParseSoundLossPercent(OutdoorSoundLossPercentTextBox.Text, out _)
            || !TryParseSoundLossPercent(OpenSpaceSoundLossPercentPerCellTextBox.Text, out _))
        {
            System.Windows.MessageBox.Show(this, "Indoor, outdoor, and open-space sound loss must each be a number from 0 to 100 percent.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!double.TryParse(DefaultRoomAmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var roomAmbient)
            || !double.IsFinite(roomAmbient)
            || roomAmbient < 0
            || roomAmbient > 1)
        {
            System.Windows.MessageBox.Show(this, "Default room ambient intensity must be between 0 and 1.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!IsValidAmbientColor(DefaultRoomAmbientColorHexTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Default room ambient color must be a six-digit #RRGGBB value.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (roomCanvasWidth % roomGridCellSize != 0 || roomCanvasHeight % roomGridCellSize != 0)
        {
            System.Windows.MessageBox.Show(this, "The selected room grid cell size must divide both room canvas width and height.", "Global Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private static bool TryParseSoundLossPercent(string? text, out double value) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value) && value is >= 0 and <= 100;

    private void RoomCanvasDimensionTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var preferred = RoomGridCellSizeComboBox.SelectedItem is int selected
            ? selected
            : (int?)null;

        RefreshGridCellSizeOptions(preferred);
    }

    private void RefreshGridCellSizeOptions(int? preferred)
    {
        if (!TryParsePositiveWholeNumber(RoomCanvasWidthTextBox.Text, out var width)
            || !TryParsePositiveWholeNumber(RoomCanvasHeightTextBox.Text, out var height))
        {
            RoomGridCellSizeComboBox.ItemsSource = null;
            RoomGridCellSizeComboBox.SelectedItem = null;
            RoomGridCellSizeComboBox.IsEnabled = false;
            return;
        }

        var values = GetValidGridCellSizes(width, height);
        RoomGridCellSizeComboBox.ItemsSource = values;
        RoomGridCellSizeComboBox.IsEnabled = values.Count > 0;

        var target = preferred ?? PreferredDefaultGridCellSize;
        RoomGridCellSizeComboBox.SelectedItem = ResolveBestFitGridCellSize(values, target);
    }

    private void ManageLighting_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ManagePointLightDefaultsDialog(_pointLightDefaults)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _pointLightDefaults = LightingModelCloner.ClonePointLightDefaults(dialog.PointLightDefaults);
        RefreshLightingSummary();
    }

    private void DefaultRoomAmbientIntensitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingAmbientControl)
        {
            DefaultRoomAmbientIntensityTextBox.Text = e.NewValue.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private void DefaultRoomAmbientIntensityTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(DefaultRoomAmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) && value is >= 0 and <= 1)
        {
            SetDefaultRoomAmbientIntensity(value);
        }
    }

    private void DefaultRoomAmbientIntensityDecrement_OnClick(object sender, RoutedEventArgs e)
    {
        SetDefaultRoomAmbientIntensity(Math.Clamp(ReadDefaultRoomAmbientIntensity() - 0.05, 0, 1));
    }

    private void DefaultRoomAmbientIntensityIncrement_OnClick(object sender, RoutedEventArgs e)
    {
        SetDefaultRoomAmbientIntensity(Math.Clamp(ReadDefaultRoomAmbientIntensity() + 0.05, 0, 1));
    }

    private double ReadDefaultRoomAmbientIntensity()
    {
        return double.TryParse(DefaultRoomAmbientIntensityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) ? value : 1;
    }

    private void SetDefaultRoomAmbientIntensity(double value)
    {
        _updatingAmbientControl = true;
        DefaultRoomAmbientIntensityTextBox.Text = Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
        DefaultRoomAmbientIntensitySlider.Value = Math.Clamp(value, 0, 1);
        _updatingAmbientControl = false;
    }

    private void PickDefaultRoomAmbientColor_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(DefaultRoomAmbientColorHexTextBox.Text) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            SetDefaultRoomAmbientColor(dialog.SelectedColor ?? "#FFFFFF");
        }
    }

    private void SetDefaultRoomAmbientColor(string color)
    {
        DefaultRoomAmbientColorHexTextBox.Text = color;
        if (System.Windows.Media.ColorConverter.ConvertFromString(color) is System.Windows.Media.Color parsedColor)
        {
            DefaultRoomAmbientColorPreview.Background = new SolidColorBrush(parsedColor);
        }
    }

    private static bool IsValidAmbientColor(string value)
    {
        return value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    }

    private void RefreshLightingSummary()
    {
        LightingSummaryTextBlock.Text = _pointLightDefaults is null
            ? "Not configured"
            : "Configured fallback values";
    }

    private static bool TryParsePositiveWholeNumber(string text, out int value)
    {
        return int.TryParse(text.Trim(), out value) && value > 0;
    }

    private static bool TryParsePositiveFiniteDouble(string text, out double value)
    {
        var normalized = text.Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value)
               && value > 0;
    }

    private static bool TryParseStackScaleStep(string text, out double value)
    {
        return TryParsePositiveFiniteDouble(text, out value)
               && StackScalePolicy.IsStackScaleStepWithinBounds(value);
    }

    private static bool TryParseMinStackScale(string text, out double value)
    {
        return TryParsePositiveFiniteDouble(text, out value)
               && StackScalePolicy.IsMinStackScaleWithinBounds(value);
    }

    private static List<int> GetValidGridCellSizes(int width, int height)
    {
        var common = GreatestCommonDivisor(width, height);
        var values = new List<int>();
        var root = (int)Math.Sqrt(common);

        for (var divisor = 1; divisor <= root; divisor++)
        {
            if (common % divisor != 0)
            {
                continue;
            }

            values.Add(divisor);
            var pair = common / divisor;
            if (pair != divisor)
            {
                values.Add(pair);
            }
        }

        values.Sort();
        return values;
    }

    private static int ResolveBestFitGridCellSize(IReadOnlyCollection<int> validValues, int preferred)
    {
        if (validValues.Count == 0)
        {
            return -1;
        }

        if (validValues.Contains(preferred))
        {
            return preferred;
        }

        return validValues.OrderBy(value => Math.Abs(value - preferred)).ThenBy(value => value).First();
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        var left = Math.Abs(a);
        var right = Math.Abs(b);
        while (right != 0)
        {
            var next = left % right;
            left = right;
            right = next;
        }

        return left == 0 ? 1 : left;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ManageSoundEffects_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SoundEffectLibraryDialog(_soundEffectLibraryEntries, _soundEffectCategorySuggestions)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _soundEffectLibraryEntries = dialog.Entries.Select(CloneSoundEffectEntry).ToList();
    }

    private void ManagePresentation_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ManageGamePresentationDialog(_gameDisplayName, _gameSummary, _gamePreviewImages)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _gameDisplayName = dialog.GameDisplayName;
        _gameSummary = dialog.GameSummary;
        _gamePreviewImages = dialog.GamePreviewImages.ToList();
    }

    private static List<string> NormalizePreviewImages(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SoundEffectLibraryEntry CloneSoundEffectEntry(SoundEffectLibraryEntry source)
    {
        return new SoundEffectLibraryEntry
        {
            SoundEffectId = source.SoundEffectId,
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
            RepeatIntervalMinMs = source.RepeatIntervalMinMs,
            RepeatIntervalMaxMs = source.RepeatIntervalMaxMs,
            MaxInRoomDistancePixels = source.MaxInRoomDistancePixels,
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
