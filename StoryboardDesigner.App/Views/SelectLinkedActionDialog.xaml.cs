using System.ComponentModel;
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
        public LinkedActionTargetGroup Group { get; init; }
    }

    private readonly List<Option> _options = new();
    private ICollectionView? _optionsView;
    private LinkedActionTargetGroup _selectedGroup = LinkedActionTargetGroup.CurrentScope;

    public SelectLinkedActionDialog(IEnumerable<CommandAction> candidates)
        : this(candidates.Select(action => (action, "Current scope", LinkedActionTargetGroup.CurrentScope)))
    {
    }

    public SelectLinkedActionDialog(IEnumerable<(CommandAction Action, string ScopeName)> scopedCandidates)
        : this(scopedCandidates.Select(static candidate =>
            (candidate.Action, candidate.ScopeName, LinkedActionTargetGroup.CurrentScope)))
    {
    }

    public SelectLinkedActionDialog(IEnumerable<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)> scopedCandidates)
    {
        InitializeComponent();

        _options.AddRange(scopedCandidates
            .Where(static candidate => candidate.Action is not null)
            .Select(static candidate => new Option
            {
                Action = candidate.Action,
                ScopeName = candidate.ScopeName,
                Group = candidate.Group,
                Label = $"{candidate.Action.Name} ({candidate.Action.ActionType})"
            })
            .OrderBy(static option => option.ScopeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static option => option.Action.Name, StringComparer.OrdinalIgnoreCase));

        _optionsView = CollectionViewSource.GetDefaultView(_options);
        _optionsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Option.ScopeName)));
        _optionsView.Filter = item => item is Option option && option.Group == _selectedGroup;
        ActionsListBox.ItemsSource = _optionsView;

        CurrentScopeRadio.IsEnabled = _options.Any(static option => option.Group == LinkedActionTargetGroup.CurrentScope);
        AncestorsRadio.IsEnabled = _options.Any(static option => option.Group == LinkedActionTargetGroup.Ancestors);
        ChildrenRadio.IsEnabled = _options.Any(static option => option.Group == LinkedActionTargetGroup.Children);

        if (CurrentScopeRadio.IsEnabled)
        {
            CurrentScopeRadio.IsChecked = true;
        }
        else if (AncestorsRadio.IsEnabled)
        {
            AncestorsRadio.IsChecked = true;
        }
        else if (ChildrenRadio.IsEnabled)
        {
            ChildrenRadio.IsChecked = true;
        }
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

    private void TargetGroup_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender == CurrentScopeRadio)
        {
            _selectedGroup = LinkedActionTargetGroup.CurrentScope;
        }
        else if (sender == AncestorsRadio)
        {
            _selectedGroup = LinkedActionTargetGroup.Ancestors;
        }
        else if (sender == ChildrenRadio)
        {
            _selectedGroup = LinkedActionTargetGroup.Children;
        }

        if (ActionsListBox is not null)
        {
            ActionsListBox.SelectedItem = null;
        }

        _optionsView?.Refresh();
    }
}
