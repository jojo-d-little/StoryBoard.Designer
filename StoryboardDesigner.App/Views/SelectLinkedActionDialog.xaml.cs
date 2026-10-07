using System.Windows;
using System.Windows.Data;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class SelectLinkedActionDialog : Window
{
    private sealed class Option
    {
        public CommandAction Action { get; init; } = null!;
        public string Label { get; init; } = string.Empty;
        public string ScopeName { get; init; } = string.Empty;
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

    public SelectLinkedActionDialog(IEnumerable<(CommandAction Action, string ScopeName)> scopedCandidates)
    {
        InitializeComponent();

        var options = scopedCandidates
            .Where(static candidate => candidate.Action is not null)
            .Select(static candidate => new Option
            {
                Action = candidate.Action,
                ScopeName = candidate.ScopeName,
                Label = $"{candidate.Action.Name} ({candidate.Action.ActionType})"
            })
            .ToList();

        var view = CollectionViewSource.GetDefaultView(options);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Option.ScopeName)));
        ActionsListBox.ItemsSource = view;
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
