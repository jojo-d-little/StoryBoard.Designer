using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class DevelopmentLaunchSetupDialog : Window
{
    public DevelopmentLaunchSetupDialog(string? developmentUsername)
    {
        InitializeComponent();
        DevelopmentUsernameTextBox.Text = string.IsNullOrWhiteSpace(developmentUsername)
            ? DevelopmentLaunchRegistration.DefaultDevelopmentUsername
            : developmentUsername.Trim();
    }

    public string DevelopmentUsername => DevelopmentUsernameTextBox.Text?.Trim() ?? string.Empty;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DevelopmentUsername))
        {
            System.Windows.MessageBox.Show(this, "Development username is required.", "Development WebPortal Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            DevelopmentUsernameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
