using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class AreaPropertiesDialog : Window
{
    private readonly IReadOnlyList<AreaStartingRoomOption> _startingRoomOptions;
    private readonly IReadOnlyList<RoomDropBehaviorChoice> _roomDropBehaviorChoices;

    public AreaPropertiesDialog(AreaBasicPropertiesEditRequest initialValues, IReadOnlyList<AreaStartingRoomOption> startingRoomOptions)
    {
        InitializeComponent();

        _startingRoomOptions = startingRoomOptions;

        AreaNameTextBox.Text = initialValues.Name;
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        AdjacencyModeComboBox.ItemsSource = Enum.GetValues<AreaAdjacencyMode>();
        AdjacencyModeComboBox.SelectedItem = initialValues.AdjacencyMode;
        _roomDropBehaviorChoices =
        [
            new RoomDropBehaviorChoice(
                AreaRoomDropBehavior.AutoBasicTraversals,
                "On - Auto-connect adjacent rooms on drop"),
            new RoomDropBehaviorChoice(
                AreaRoomDropBehavior.PromptForWizard,
                "Off - Prompt to run Traversal Wizard"),
            new RoomDropBehaviorChoice(
                AreaRoomDropBehavior.KeepDisconnected,
                "Off - Keep dropped rooms disconnected")
        ];
        RoomDropBehaviorComboBox.ItemsSource = _roomDropBehaviorChoices;
        RoomDropBehaviorComboBox.SelectedItem = _roomDropBehaviorChoices
            .FirstOrDefault(choice => choice.Behavior == initialValues.RoomDropBehavior)
            ?? _roomDropBehaviorChoices.First(choice => choice.Behavior == AreaRoomDropBehavior.KeepDisconnected);

        var items = new List<StartingRoomChoice> { StartingRoomChoice.None };
        items.AddRange(_startingRoomOptions.Select(option => new StartingRoomChoice(option.Id, option.Name)));
        StartingRoomComboBox.ItemsSource = items;
        StartingRoomComboBox.SelectedValue = initialValues.StartingRoomId;
        StartingRoomComboBox.IsEnabled = _startingRoomOptions.Count > 0;
    }

    public AreaBasicPropertiesEditRequest Values => new(
        AreaNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        AdjacencyModeComboBox.SelectedItem is AreaAdjacencyMode mode ? mode : AreaAdjacencyMode.EightDirectional,
        RoomDropBehaviorComboBox.SelectedItem is RoomDropBehaviorChoice roomDropBehaviorChoice
            ? roomDropBehaviorChoice.Behavior
            : AreaRoomDropBehavior.KeepDisconnected,
        StartingRoomComboBox.SelectedValue as Guid?);

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AreaNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter an area name.", "Area Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record StartingRoomChoice(Guid? Id, string Name)
    {
        public static StartingRoomChoice None { get; } = new(null, "(None)");
    }

    private sealed record RoomDropBehaviorChoice(AreaRoomDropBehavior Behavior, string Name);
}
