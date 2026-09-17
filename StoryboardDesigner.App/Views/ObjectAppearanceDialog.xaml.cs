using System.Globalization;
using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ObjectAppearanceDialog : Window
{
    private const string SourceBucket = "objects";
    private readonly string _projectFilePath;
    private readonly string _sourceBucket;

    public ObjectAppearanceDialog(ObjectAppearanceEditRequest initial, string projectFilePath = "", string preferredSourceBucket = SourceBucket)
    {
        InitializeComponent();
        _projectFilePath = projectFilePath;
        _sourceBucket = string.IsNullOrWhiteSpace(preferredSourceBucket) ? SourceBucket : preferredSourceBucket;

        FullPathTextBox.Text = initial.FullImagePath;
        GrayPathTextBox.Text = initial.GrayMapImagePath;
        NormalPathTextBox.Text = initial.NormalMapImagePath;
        RotationTextBox.Text = initial.RotationDegrees.ToString(CultureInfo.InvariantCulture);
        ScaleTextBox.Text = (initial.Scale <= 0 ? 1 : initial.Scale).ToString(CultureInfo.InvariantCulture);
        LocalOffsetXTextBox.Text = initial.LocalOffsetX.ToString(CultureInfo.InvariantCulture);
        LocalOffsetYTextBox.Text = initial.LocalOffsetY.ToString(CultureInfo.InvariantCulture);

        FullPathTextBox.TextChanged += AnyPathTextBox_OnTextChanged;
        GrayPathTextBox.TextChanged += AnyPathTextBox_OnTextChanged;
        NormalPathTextBox.TextChanged += AnyPathTextBox_OnTextChanged;
        UpdatePathHealthStatus();
    }

    public ObjectAppearanceEditRequest Values => new(
        NormalizePathForPersistence(FullPathTextBox.Text),
        NormalizePathForPersistence(GrayPathTextBox.Text),
        NormalizePathForPersistence(NormalPathTextBox.Text),
        ParseDoubleOrDefault(RotationTextBox.Text, 0),
        Math.Max(0.01, ParseDoubleOrDefault(ScaleTextBox.Text, 1)),
        ParseDoubleOrDefault(LocalOffsetXTextBox.Text, 0),
        ParseDoubleOrDefault(LocalOffsetYTextBox.Text, 0));

    private void PickFullImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (TryPickImagePath(out var path))
        {
            FullPathTextBox.Text = path;
            UpdatePathHealthStatus();
        }
    }

    private void PickGrayImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (TryPickImagePath(out var path))
        {
            GrayPathTextBox.Text = path;
            UpdatePathHealthStatus();
        }
    }

    private void PickNormalImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (TryPickImagePath(out var path))
        {
            NormalPathTextBox.Text = path;
            UpdatePathHealthStatus();
        }
    }

    private void AnyPathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdatePathHealthStatus();
    }

    private void UpdatePathHealthStatus()
    {
        FullPathHealthTextBlock.Text = "Full: " + ImagePathHealthStatusFormatter.BuildStatusLabel(FullPathTextBox.Text, _projectFilePath, _sourceBucket);
        GrayPathHealthTextBlock.Text = "Gray: " + ImagePathHealthStatusFormatter.BuildStatusLabel(GrayPathTextBox.Text, _projectFilePath, _sourceBucket);
        NormalPathHealthTextBlock.Text = "Normal: " + ImagePathHealthStatusFormatter.BuildStatusLabel(NormalPathTextBox.Text, _projectFilePath, _sourceBucket);
    }

    private static bool TryPickImagePath(out string path)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            path = string.Empty;
            return false;
        }

        path = dialog.FileName;
        return true;
    }

    private static double ParseDoubleOrDefault(string raw, double fallback)
    {
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private string NormalizePathForPersistence(string? value)
    {
        var projectRootFolder = string.IsNullOrWhiteSpace(_projectFilePath)
            ? null
            : System.IO.Path.GetDirectoryName(_projectFilePath);
        return AssetSourcePathResolver.NormalizeForPersistence(value, projectRootFolder);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(RotationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            System.Windows.MessageBox.Show(this, "Rotation must be a valid number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            RotationTextBox.Focus();
            RotationTextBox.SelectAll();
            return;
        }

        if (!double.TryParse(ScaleTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) || scale <= 0)
        {
            System.Windows.MessageBox.Show(this, "Scale must be greater than 0.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            ScaleTextBox.Focus();
            ScaleTextBox.SelectAll();
            return;
        }

        if (!double.TryParse(LocalOffsetXTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            System.Windows.MessageBox.Show(this, "Local Offset X must be a valid number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            LocalOffsetXTextBox.Focus();
            LocalOffsetXTextBox.SelectAll();
            return;
        }

        if (!double.TryParse(LocalOffsetYTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            System.Windows.MessageBox.Show(this, "Local Offset Y must be a valid number.", "Object Appearance", MessageBoxButton.OK, MessageBoxImage.Information);
            LocalOffsetYTextBox.Focus();
            LocalOffsetYTextBox.SelectAll();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
