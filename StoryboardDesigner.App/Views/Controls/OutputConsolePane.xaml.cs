namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for OutputConsolePane.xaml
/// </summary>
public partial class OutputConsolePane : System.Windows.Controls.UserControl
{
    public OutputConsolePane()
    {
        InitializeComponent();
    }

    public System.Windows.Controls.ListBox OutputListBoxControl => OutputListBox;

    private void OutputListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != System.Windows.Input.ModifierKeys.Control
            || e.Key != System.Windows.Input.Key.C)
        {
            return;
        }

        CopySelectedOutputLinesToClipboard();
        e.Handled = true;
    }

    private void OutputConsoleCopySelected_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        CopySelectedOutputLinesToClipboard();
    }

    private void CopySelectedOutputLinesToClipboard()
    {
        if (OutputListBox.SelectedItems.Count == 0)
        {
            return;
        }

        var selectedLines = OutputListBox.SelectedItems
            .OfType<string>()
            .ToList();

        if (selectedLines.Count == 0)
        {
            return;
        }

        TrySetClipboardText(string.Join(Environment.NewLine, selectedLines));
    }

    private static void TrySetClipboardText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        const int maxAttempts = 3;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard can be temporarily locked by another process; retry briefly.
            }
        }
    }
}
