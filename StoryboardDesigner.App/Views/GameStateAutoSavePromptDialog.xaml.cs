using System.Windows;

namespace StoryboardDesigner.App.Views;

public partial class GameStateAutoSavePromptDialog : Window
{
    public GameStateAutoSavePromptDialog()
    {
        InitializeComponent();
    }

    public bool ShouldAutoSave { get; private set; }
    public bool DoNotAskAgainThisRun => DoNotAskAgainCheckBox.IsChecked == true;

    private void AutoSave_OnClick(object sender, RoutedEventArgs e)
    {
        ShouldAutoSave = true;
        DialogResult = true;
    }

    private void WithoutSave_OnClick(object sender, RoutedEventArgs e)
    {
        ShouldAutoSave = false;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
