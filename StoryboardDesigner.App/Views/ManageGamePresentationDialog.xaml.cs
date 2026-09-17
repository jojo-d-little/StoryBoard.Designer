using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ManageGamePresentationDialog : Window
{
    private readonly ObservableCollection<string> _previewImages;

    public ManageGamePresentationDialog(string gameDisplayName, string gameSummary, IReadOnlyList<string> gamePreviewImages)
    {
        InitializeComponent();

        GameDisplayNameTextBox.Text = gameDisplayName?.Trim() ?? string.Empty;
        GameSummaryTextBox.Text = gameSummary?.Trim() ?? string.Empty;

        _previewImages = new ObservableCollection<string>(NormalizePreviewImages(gamePreviewImages));
        PreviewImagesListBox.ItemsSource = _previewImages;

        if (_previewImages.Count > 0)
        {
            PreviewImagesListBox.SelectedIndex = 0;
        }
    }

    public string GameDisplayName { get; private set; } = string.Empty;

    public string GameSummary { get; private set; } = string.Empty;

    public IReadOnlyList<string> GamePreviewImages => _previewImages.ToList();

    private void BrowseAdd_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose Preview Images",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = true,
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var fileName in dialog.FileNames)
        {
            AddPreviewImage(fileName);
        }
    }

    private void AddManual_OnClick(object sender, RoutedEventArgs e)
    {
        var input = ManualPathTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        AddPreviewImage(input);
        ManualPathTextBox.Text = string.Empty;
    }

    private void Remove_OnClick(object sender, RoutedEventArgs e)
    {
        if (PreviewImagesListBox.SelectedItem is not string selected)
        {
            return;
        }

        _previewImages.Remove(selected);
    }

    private void MoveUp_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelected(-1);
    }

    private void MoveDown_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelected(1);
    }

    private void MoveSelected(int offset)
    {
        if (PreviewImagesListBox.SelectedItem is not string selected)
        {
            return;
        }

        var fromIndex = _previewImages.IndexOf(selected);
        if (fromIndex < 0)
        {
            return;
        }

        var toIndex = fromIndex + offset;
        if (toIndex < 0 || toIndex >= _previewImages.Count)
        {
            return;
        }

        _previewImages.Move(fromIndex, toIndex);
        PreviewImagesListBox.SelectedItem = selected;
        PreviewImagesListBox.ScrollIntoView(selected);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        GameDisplayName = GameDisplayNameTextBox.Text.Trim();
        GameSummary = GameSummaryTextBox.Text.Trim();

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void AddPreviewImage(string value)
    {
        var normalized = AssetSourcePathResolver.NormalizeForPersistence(value, projectRootFolder: null);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (_previewImages.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _previewImages.Add(normalized);
        PreviewImagesListBox.SelectedItem = normalized;
        PreviewImagesListBox.ScrollIntoView(normalized);
    }

    private static IReadOnlyList<string> NormalizePreviewImages(IEnumerable<string> values)
    {
        return values
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
