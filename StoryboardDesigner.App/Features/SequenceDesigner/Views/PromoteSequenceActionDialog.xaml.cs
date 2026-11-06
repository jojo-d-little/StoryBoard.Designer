namespace StoryboardDesigner.App.Features.SequenceDesigner.Views;

public partial class PromoteSequenceActionDialog : System.Windows.Window
{
    public PromoteSequenceActionDialog(string initialName)
    {
        InitializeComponent();
        ActionNameTextBox.Text = initialName ?? string.Empty;
        Loaded += (_, _) =>
        {
            ActionNameTextBox.Focus();
            ActionNameTextBox.SelectAll();
        };
    }

    public string ActionName => ActionNameTextBox.Text?.Trim() ?? string.Empty;

    private void Promote_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ActionName))
        {
            System.Windows.MessageBox.Show(this, "Enter a name for the reusable action.", "Promote Inline Action", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            ActionNameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
