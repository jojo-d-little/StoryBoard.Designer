using System.Windows;
using System.IO;
using Microsoft.Win32;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class RoomDesignerDirectionImageDialog : Window
{
    private const string SourceBucket = "rooms";
    private readonly RoomDesignerImageSlotViewModel _slot;
    private readonly bool _initialIsPreviewVisible;
    private readonly string _initialFullImagePath;
    private readonly string _initialGrayMapImagePath;
    private readonly string _initialNormalMapImagePath;
    private readonly double _initialOverlayOffsetX;
    private readonly double _initialOverlayOffsetY;
    private readonly double _initialOverlayRotationDegrees;
    private readonly int _initialOverlayRenderOrder;

    private bool _skipCloseConfirmation;
    private bool _discardChanges;

    public RoomDesignerDirectionImageDialog(RoomDesignerImageSlotViewModel slot)
    {
        InitializeComponent();
        _slot = slot;

        _initialIsPreviewVisible = slot.IsPreviewVisible;
        _initialFullImagePath = slot.FullImagePath;
        _initialGrayMapImagePath = slot.GrayMapImagePath;
        _initialNormalMapImagePath = slot.NormalMapImagePath;
        _initialOverlayOffsetX = slot.OverlayOffsetX;
        _initialOverlayOffsetY = slot.OverlayOffsetY;
        _initialOverlayRotationDegrees = slot.OverlayRotationDegrees;
        _initialOverlayRenderOrder = slot.OverlayRenderOrder;

        DataContext = slot;

        FullPathTextBox.TextChanged += AnyPathTextBox_OnTextChanged;
        GrayPathTextBox.TextChanged += AnyPathTextBox_OnTextChanged;
        NormalPathTextBox.TextChanged += AnyPathTextBox_OnTextChanged;
        UpdatePathHealthStatus();
    }

    private void BrowseFull_OnClick(object sender, RoutedEventArgs e)
    {
        var path = BrowseForImagePath();
        if (path is not null)
        {
            _slot.FullImagePath = path;
            UpdatePathHealthStatus();
        }
    }

    private void BrowseGray_OnClick(object sender, RoutedEventArgs e)
    {
        var path = BrowseForImagePath();
        if (path is not null)
        {
            _slot.GrayMapImagePath = path;
            UpdatePathHealthStatus();
        }
    }

    private void BrowseNormal_OnClick(object sender, RoutedEventArgs e)
    {
        var path = BrowseForImagePath();
        if (path is not null)
        {
            _slot.NormalMapImagePath = path;
            UpdatePathHealthStatus();
        }
    }

    private void AnyPathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdatePathHealthStatus();
    }

    private void UpdatePathHealthStatus()
    {
        var projectFilePath = ResolveProjectFilePath();
        FullPathHealthTextBlock.Text = "Full: " + ImagePathHealthStatusFormatter.BuildStatusLabel(_slot.FullImagePath, projectFilePath, SourceBucket);
        GrayPathHealthTextBlock.Text = "Gray: " + ImagePathHealthStatusFormatter.BuildStatusLabel(_slot.GrayMapImagePath, projectFilePath, SourceBucket);
        NormalPathHealthTextBlock.Text = "Normal: " + ImagePathHealthStatusFormatter.BuildStatusLabel(_slot.NormalMapImagePath, projectFilePath, SourceBucket);
    }

    private string? ResolveProjectFilePath()
    {
        if (System.Windows.Application.Current?.MainWindow?.DataContext is MainWindowViewModel vm)
        {
            return vm.ProjectFilePath;
        }

        return null;
    }

    private string? BrowseForImagePath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
            Title = "Select Image File"
        };

        if (dialog.ShowDialog() == true)
        {
            var projectRootFolder = ResolveProjectRootFolder();
            return AssetSourcePathResolver.NormalizeForPersistence(dialog.FileName, projectRootFolder);
        }

        return null;
    }

    private string? ResolveProjectRootFolder()
    {
        var projectFilePath = ResolveProjectFilePath();
        return string.IsNullOrWhiteSpace(projectFilePath)
            ? null
            : Path.GetDirectoryName(projectFilePath);
    }

    private void Nudge_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "X:-1":
                _slot.OverlayOffsetX -= 1;
                break;
            case "X:+1":
                _slot.OverlayOffsetX += 1;
                break;
            case "Y:-1":
                _slot.OverlayOffsetY -= 1;
                break;
            case "Y:+1":
                _slot.OverlayOffsetY += 1;
                break;
            case "R:-1":
                _slot.OverlayRotationDegrees -= 1;
                break;
            case "R:+1":
                _slot.OverlayRotationDegrees += 1;
                break;
            case "O:-1":
                _slot.OverlayRenderOrder -= 1;
                break;
            case "O:+1":
                _slot.OverlayRenderOrder += 1;
                break;
        }
    }

    private void ResetRenderOrder_OnClick(object sender, RoutedEventArgs e)
    {
        _slot.ResetOverlayRenderOrderToDefault();
    }

    private void PresetRotation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
        {
            return;
        }

        if (double.TryParse(tag, out var value))
        {
            _slot.OverlayRotationDegrees = value;
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_skipCloseConfirmation)
        {
            var prompt = System.Windows.MessageBox.Show(
                this,
                "Save your changes before closing?",
                "Directional Image Editor",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            if (prompt == System.Windows.MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            _discardChanges = prompt == System.Windows.MessageBoxResult.No;
            _skipCloseConfirmation = true;
        }

        if (_discardChanges)
        {
            RestoreInitialValues();
            return;
        }

        if (Math.Abs(_slot.OverlayOffsetX) <= 1000
            && Math.Abs(_slot.OverlayOffsetY) <= 1000
            && Math.Abs(_slot.OverlayRotationDegrees) <= 1000)
        {
            return;
        }

        System.Windows.MessageBox.Show(
            this,
            "One or more transform values are over 1000. This is allowed, but verify preview output.",
            "Large Transform Value",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private void SaveAndClose_OnClick(object sender, RoutedEventArgs e)
    {
        _discardChanges = false;
        _skipCloseConfirmation = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        _discardChanges = true;
        _skipCloseConfirmation = true;
        Close();
    }

    private void RestoreInitialValues()
    {
        _slot.IsPreviewVisible = _initialIsPreviewVisible;
        _slot.FullImagePath = _initialFullImagePath;
        _slot.GrayMapImagePath = _initialGrayMapImagePath;
        _slot.NormalMapImagePath = _initialNormalMapImagePath;
        _slot.OverlayOffsetX = _initialOverlayOffsetX;
        _slot.OverlayOffsetY = _initialOverlayOffsetY;
        _slot.OverlayRotationDegrees = _initialOverlayRotationDegrees;
        _slot.OverlayRenderOrder = _initialOverlayRenderOrder;
    }
}
