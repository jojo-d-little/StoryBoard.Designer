using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class InstanceNamingDialog : Window
{
    public InstanceNamingDialog(InstanceNamingEditRequest initialValues)
    {
        InitializeComponent();
        BaseNameTextBox.Text = initialValues.BaseName;
        BaseNameInGameTextBox.Text = initialValues.BaseNameInGame;
        BaseNameSynonymsTextBox.Text = initialValues.BaseNameSynonyms;
        InstanceNameTextBox.Text = initialValues.InstanceName;
        InstanceNameInGameTextBox.Text = initialValues.InstanceNameInGame;
        InstanceNameSynonymsTextBox.Text = initialValues.InstanceNameSynonyms;
        InstanceNameTextBox.Focus();
        InstanceNameTextBox.SelectAll();
    }

    public InstanceNamingEditRequest Values => new(
        BaseNameTextBox.Text,
        BaseNameInGameTextBox.Text,
        BaseNameSynonymsTextBox.Text,
        InstanceNameTextBox.Text.Trim(),
        InstanceNameInGameTextBox.Text.Trim(),
        InstanceNameSynonymsTextBox.Text.Trim());

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
