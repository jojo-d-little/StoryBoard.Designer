using System.IO;
using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class DevelopmentLaunchSetupDialog : Window
{
    public DevelopmentLaunchSetupDialog(
        string? developmentUsername,
        string? gameHostExecutablePath,
        string? webPortalRootPath)
    {
        InitializeComponent();
        DevelopmentUsernameTextBox.Text = string.IsNullOrWhiteSpace(developmentUsername)
            ? DevelopmentLaunchRegistration.DefaultDevelopmentUsername
            : developmentUsername.Trim();
        GameHostExecutablePathTextBox.Text = gameHostExecutablePath?.Trim() ?? string.Empty;
        WebPortalRootPathTextBox.Text = webPortalRootPath?.Trim() ?? string.Empty;
    }

    public string DevelopmentUsername => DevelopmentUsernameTextBox.Text?.Trim() ?? string.Empty;

    public string GameHostExecutablePath => GameHostExecutablePathTextBox.Text?.Trim() ?? string.Empty;

    public string WebPortalRootPath => WebPortalRootPathTextBox.Text?.Trim() ?? string.Empty;

    private void BrowseGameHost_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "GameHost executable (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select GameHost Executable"
        };

        if (dialog.ShowDialog(this) == true)
        {
            GameHostExecutablePathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseWebPortal_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the WebPortal dist folder containing index.html.",
            UseDescriptionForTitle = true,
            SelectedPath = WebPortalRootPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            WebPortalRootPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DevelopmentUsername))
        {
            System.Windows.MessageBox.Show(this, "Development username is required.", "Development WebPortal Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            DevelopmentUsernameTextBox.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(GameHostExecutablePath))
        {
            if (!string.Equals(System.IO.Path.GetExtension(GameHostExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(GameHostExecutablePath))
            {
                System.Windows.MessageBox.Show(this, "The configured GameHost path must point to an existing .exe file.", "Development WebPortal Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                GameHostExecutablePathTextBox.Focus();
                GameHostExecutablePathTextBox.SelectAll();
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(WebPortalRootPath)
            && (!Directory.Exists(WebPortalRootPath) || !File.Exists(Path.Combine(WebPortalRootPath, "index.html"))))
        {
            System.Windows.MessageBox.Show(this, "The configured WebPortal root must be an existing folder containing index.html.", "Development WebPortal Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            WebPortalRootPathTextBox.Focus();
            WebPortalRootPathTextBox.SelectAll();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
