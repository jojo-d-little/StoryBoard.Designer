using System.Windows;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class NewLinkedActionDialog : Window
{
    public NewLinkedActionDialog()
    {
        InitializeComponent();
        TypeComboBox.SelectedItem = CommandActionType.EchoMessage;
    }

    public string ActionName { get; private set; } = string.Empty;
    public CommandActionType ActionType { get; private set; } = CommandActionType.EchoMessage;

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            System.Windows.MessageBox.Show(this, "Action name is required.", "New Linked Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (TypeComboBox.SelectedItem is not CommandActionType selectedType)
        {
            System.Windows.MessageBox.Show(this, "Action type is required.", "New Linked Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ActionName = name;
        ActionType = selectedType;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
