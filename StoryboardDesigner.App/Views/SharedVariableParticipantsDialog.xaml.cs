using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class SharedVariableParticipantsDialog : Window
{
    public SharedVariableParticipantsDialog(SharedVariableManagerListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        InitializeComponent();

        DisplayNameTextBlock.Text = item.DisplayName;
        IdTextBlock.Text = $"ID: {item.Id:D} · Status: {item.Status} · Participants: {item.ParticipantCount}";
        DefaultValueTextBlock.Text = item.DefaultValue;
        ValueRestrictionTextBlock.Text = item.ValueRestriction;
        var participants = item.Participants ?? Array.Empty<SharedVariableManagerParticipantListItem>();
        ParticipantsDataGrid.ItemsSource = participants;
        EmptyParticipantsTextBlock.Visibility = participants.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
