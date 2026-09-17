using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class ActionEchoEditorDialog : Window
{
    private readonly ObservableCollection<ActionEchoEditorEntry> _entries;
    private readonly IReadOnlyList<string> _referenceTokens;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _variableChoices;
    private readonly PropertyResolutionScope _variableScope;

    public ActionEchoEditorDialog(
        CommandActionType actionType,
        IReadOnlyDictionary<string, string> currentOutcomeMessageMap,
        IReadOnlyList<string> referenceTokens,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        PropertyResolutionScope variableScope,
        string? initialToken = null)
    {
        InitializeComponent();
        _referenceTokens = referenceTokens;
        _variableChoices = variableChoices;
        _variableScope = variableScope;
        _entries = new ObservableCollection<ActionEchoEditorEntry>(ActionEchoEditorEntryBuilder.BuildEntries(actionType, currentOutcomeMessageMap));
        _entries.CollectionChanged += Entries_CollectionChanged;
        EntriesListView.ItemsSource = _entries;

        if (!string.IsNullOrWhiteSpace(initialToken))
        {
            var match = _entries.FirstOrDefault(entry => string.Equals(entry.Token, initialToken, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                EntriesListView.SelectedItem = match;
            }
        }

        if (EntriesListView.SelectedItem is null && _entries.Count > 0)
        {
            EntriesListView.SelectedIndex = 0;
        }

        RefreshUiState();
    }

    public IReadOnlyDictionary<string, string> OutcomeMessageMap { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private void EntriesListView_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshUiState();
    }

    private void EntriesListView_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (EntriesListView.SelectedItem is not ActionEchoEditorEntry entry)
        {
            return;
        }

        OpenScriptEditor(entry);
    }

    private void EchoScriptPreview_OnPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox { DataContext: ActionEchoEditorEntry entry })
        {
            return;
        }

        EntriesListView.SelectedItem = entry;
        OpenScriptEditor(entry);
        e.Handled = true;
    }

    private void OpenScriptEditor(ActionEchoEditorEntry entry)
    {
        try
        {
            var dialog = new TextOutputScriptEditorDialog(entry.Script, _referenceTokens, _variableChoices, _variableScope)
            {
                Owner = this,
                Title = $"Edit Echo: {entry.Token}"
            };

            if (dialog.ShowDialog() == true)
            {
                entry.Script = dialog.ScriptText ?? string.Empty;
                EntriesListView.Items.Refresh();
                RefreshUiState();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Unable to open the script editor for '{entry.Token}'.\\n\\n{ex.Message}",
                "Action Echo Editor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        OutcomeMessageMap = ActionEchoEditorEntryBuilder.ToOutcomeMessageMap(_entries);
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshUiState();
    }

    private void RefreshUiState()
    {
        var state = ActionEchoEditorDialogStatePresenter.Build(_entries.AsReadOnly(), EntriesListView.SelectedItem as ActionEchoEditorEntry);

        UnsupportedWarningBorder.Visibility = state.IsUnsupportedWarningVisible ? Visibility.Visible : Visibility.Collapsed;
        UnsupportedWarningText.Text = state.UnsupportedWarningText;
    }
}
