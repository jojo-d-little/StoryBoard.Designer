using System.Windows;
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
    private int _roomCanvasWidth;
    private int _roomCanvasHeight;
    private bool _isApplyingPreset;

    public RoomSettingsDialog(RoomSettingsEditRequest initialValues)
    {
        InitializeComponent();
        _projectDefaultCanvasWidth = initialValues.RoomCanvasWidth > 0 ? initialValues.RoomCanvasWidth : 800;
        _projectDefaultCanvasHeight = initialValues.RoomCanvasHeight > 0 ? initialValues.RoomCanvasHeight : 600;
        _roomCanvasWidth = _projectDefaultCanvasWidth;
        _roomCanvasHeight = _projectDefaultCanvasHeight;
        _projectGridCellSize = initialValues.ProjectRoomGridCellSize > 0 ? initialValues.ProjectRoomGridCellSize : 40;

        RoomNameTextBox.Text = initialValues.Name;
        RoomNameInGameTextBox.Text = initialValues.NameInGame;
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        RoomCanvasWidthTextBox.Text = _roomCanvasWidth.ToString();
        RoomCanvasHeightTextBox.Text = _roomCanvasHeight.ToString();
        PopulateOrientationPresetOptions();
        UpdateOrientationPresetSelection();
        UpdateEffectiveGridSummary();
    }

    public RoomSettingsEditRequest Values => new(
        RoomNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        RoomNameInGameTextBox.Text.Trim(),
        _roomCanvasWidth,
        _roomCanvasHeight,
        _projectGridCellSize);

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

        _roomCanvasWidth = width;
        _roomCanvasHeight = height;

        DialogResult = true;
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
