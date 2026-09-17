namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for RoomDesignerWorkspaceView.xaml
/// </summary>
public partial class RoomDesignerWorkspaceView : System.Windows.Controls.UserControl
{
    public RoomDesignerWorkspaceView()
    {
        InitializeComponent();
    }

    private void RoomObjectThumbnailButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
