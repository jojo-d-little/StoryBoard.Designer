using System.Windows;
using System.Windows.Controls;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class SourceImageManagementDialog : Window
{
    private readonly SourceImageManagementDialogRequest _request;
    private PreviewResult? _lastPreview;

    public SourceImageManagementDialog(SourceImageManagementDialogRequest request)
    {
        InitializeComponent();
        _request = request;
    }

    public SourceImageManagementDialogResult? Result { get; private set; }

    private void Preview_OnClick(object sender, RoutedEventArgs e)
    {
        var mode = GetSelectedMode();
        var repoint = RepointPathsCheckBox.IsChecked == true;

        _lastPreview = SourceImageManagementService.BuildPreview(_request.Project, _request.ProjectFilePath, mode, repoint);
        PreviewTextBox.Text = SourceImageManagementService.BuildPreviewText(_lastPreview, _request.ProjectFilePath, mode);
        ApplyButton.IsEnabled = _lastPreview.ActionableCount > 0;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastPreview is null)
        {
            System.Windows.MessageBox.Show(this, "Run Preview before Apply.", "Source Image Management", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var mode = GetSelectedMode();
        Result = SourceImageManagementService.Apply(_lastPreview, _request.ProjectFilePath, mode);
        DialogResult = true;
        Close();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private SourceImageManagementMode GetSelectedMode()
    {
        if (ModeComboBox.SelectedItem is not ComboBoxItem item)
        {
            return SourceImageManagementMode.RepairMissingSources;
        }

        return string.Equals(item.Tag?.ToString(), nameof(SourceImageManagementMode.ConsolidateSourceImages), StringComparison.Ordinal)
            ? SourceImageManagementMode.ConsolidateSourceImages
            : SourceImageManagementMode.RepairMissingSources;
    }
}
