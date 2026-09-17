using System.Windows;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class SelectLinkedActionDialog : Window
{
    private sealed class Option
    {
        public CommandAction Action { get; init; } = null!;
        public string Label { get; init; } = string.Empty;
    }

    public SelectLinkedActionDialog(IEnumerable<CommandAction> candidates)
    {
        InitializeComponent();

        var options = candidates
            .OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
            .Select(action => new Option
            {
                Action = action,
                Label = $"{action.Name} ({action.ActionType})"
            })
            .ToList();

        ActionsListBox.ItemsSource = options;
    }

    public CommandAction? SelectedAction { get; private set; }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (ActionsListBox.SelectedItem is not Option option)
        {
            System.Windows.MessageBox.Show(this, "Choose an action.", "Select Existing Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedAction = option.Action;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
