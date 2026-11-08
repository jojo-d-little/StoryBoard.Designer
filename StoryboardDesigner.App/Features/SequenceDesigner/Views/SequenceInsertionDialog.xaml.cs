namespace StoryboardDesigner.App.Features.SequenceDesigner.Views;

public partial class SequenceInsertionDialog : System.Windows.Window
{
    private readonly IReadOnlyList<StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceInsertionChoice> _allChoices;
    private readonly IReadOnlyList<StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceInsertionChoice> _suggestedChoices;

    public SequenceInsertionDialog(IReadOnlyList<StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceInsertionChoice> choices)
    {
        InitializeComponent();
        _allChoices = choices;
        _suggestedChoices = choices.Take(6).ToList();
        ShowAllConnectionsCheckBox.Visibility = choices.Count > _suggestedChoices.Count
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        InsertionChoiceComboBox.ItemsSource = _suggestedChoices;
        InsertionChoiceComboBox.SelectedItem = _suggestedChoices.FirstOrDefault();
    }

    public StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceInsertionChoice? SelectedChoice
        => InsertionChoiceComboBox.SelectedItem as StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceInsertionChoice;

    private void InsertionChoiceComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        FlowPreviewTextBlock.Text = SelectedChoice?.Preview ?? string.Empty;
    }

    private void ShowAllConnectionsCheckBox_OnChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        var selected = SelectedChoice;
        InsertionChoiceComboBox.ItemsSource = ShowAllConnectionsCheckBox.IsChecked == true ? _allChoices : _suggestedChoices;
        InsertionChoiceComboBox.SelectedItem = selected is not null && InsertionChoiceComboBox.Items.Contains(selected)
            ? selected
            : InsertionChoiceComboBox.Items.Cast<object>().FirstOrDefault();
    }

    private void Insert_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SelectedChoice is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Choose a connection for this step.",
                "Insert into flow",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
