using System.Windows;

namespace StoryboardDesigner.App.Views;

public partial class TemplateNameDialog : Window
{
    public TemplateNameDialog(string initialName)
    {
        InitializeComponent();
        TemplateNameTextBox.Text = initialName ?? string.Empty;
        Loaded += (_, _) =>
        {
            TemplateNameTextBox.Focus();
            TemplateNameTextBox.SelectAll();
        };
    }

    public string TemplateName => TemplateNameTextBox.Text?.Trim() ?? string.Empty;

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            System.Windows.MessageBox.Show(this, "Template name is required.", "Create Template From", MessageBoxButton.OK, MessageBoxImage.Information);
            TemplateNameTextBox.Focus();
            TemplateNameTextBox.SelectAll();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
