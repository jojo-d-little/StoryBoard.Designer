namespace StoryboardDesigner.App.Features.SequenceDesigner.Views;

public partial class RenameSequenceBranchDialog : System.Windows.Window
{
    public RenameSequenceBranchDialog(string currentName)
    {
        InitializeComponent();
        BranchNameTextBox.Text = currentName;
        Loaded += (_, _) =>
        {
            BranchNameTextBox.Focus();
            BranchNameTextBox.SelectAll();
        };
    }

    public string BranchDisplayName => BranchNameTextBox.Text;

    private void Rename_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BranchNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(
                this,
                "Enter a branch name.",
                "Rename branch",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            BranchNameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
