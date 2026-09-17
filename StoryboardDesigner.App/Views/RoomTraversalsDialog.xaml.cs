using System.Windows;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public enum RoomTraversalsDialogAction
{
    None,
    Add,
    Edit,
    Relink,
    Remove
}

public sealed class RoomTraversalReviewItem
{
    public required TraversalConnection Connection { get; init; }
    public required string DirectionLabel { get; init; }
    public required string DestinationRoomName { get; init; }
    public required string DoorLinkSummary { get; init; }
    public required string FromPassableSharedSummary { get; init; }
    public required string ToPassableSharedSummary { get; init; }
    public required string PresentationEffectSummary { get; init; }

    public TraversalAccessMode TraversalAccessMode => Connection.TraversalAccessMode;
    public OpenStateBindingMode OpenStateBindingMode => Connection.OpenStateBindingMode;
    public string TraversalConnectionId => Connection.TraversalConnectionId.ToString("N");
}

public partial class RoomTraversalsDialog : Window
{
    public RoomTraversalsDialog(string roomName, IReadOnlyList<RoomTraversalReviewItem> rows)
    {
        InitializeComponent();
        HeaderTextBlock.Text = $"Manage Traversals For '{roomName}'";
        TraversalsDataGrid.ItemsSource = rows;
        if (rows.Count > 0)
        {
            TraversalsDataGrid.SelectedIndex = 0;
        }
    }

    public RoomTraversalsDialogAction Action { get; private set; }

    public TraversalConnection? SelectedConnection =>
        (TraversalsDataGrid.SelectedItem as RoomTraversalReviewItem)?.Connection;

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        Action = RoomTraversalsDialogAction.Add;
        DialogResult = true;
    }

    private void TraversalsDataGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SelectedConnection is null)
        {
            return;
        }

        Action = RoomTraversalsDialogAction.Edit;
        DialogResult = true;
    }

    private void EditSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Select a traversal first.",
                "Review Traversals",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Action = RoomTraversalsDialogAction.Edit;
        DialogResult = true;
    }

    private void RemoveSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Select a traversal first.",
                "Review Traversals",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Action = RoomTraversalsDialogAction.Remove;
        DialogResult = true;
    }

    private void RelinkSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Select a traversal first.",
                "Review Traversals",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Action = RoomTraversalsDialogAction.Relink;
        DialogResult = true;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Action = RoomTraversalsDialogAction.None;
        DialogResult = false;
    }
}
