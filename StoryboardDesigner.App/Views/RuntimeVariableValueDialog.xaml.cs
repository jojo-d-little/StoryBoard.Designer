using System.Windows;

namespace StoryboardDesigner.App.Views;

public partial class RuntimeVariableValueDialog : Window
{
    public RuntimeVariableValueDialog(string variableName, string currentValue)
    {
        InitializeComponent();
        VariableNameTextBox.Text = variableName;
        ValueTextBox.Text = currentValue;
        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        };
    }

    public string ValueText => ValueTextBox.Text ?? string.Empty;

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
