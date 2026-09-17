using System.Windows;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for AreaMapToolbar.xaml
/// </summary>
public partial class AreaMapToolbar : System.Windows.Controls.UserControl
{
    public AreaMapToolbar()
    {
        InitializeComponent();
    }

    private void AreaMapFit_OnClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow hostWindow)
        {
            hostWindow.AreaMapFit_OnClick(sender, e);
        }
    }

    private void AreaMapFloorDown_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AreaNavigationEditorTabViewModel editor)
        {
            editor.DecreaseSelectedFloorElevation();
        }
    }

    private void AreaMapFloorUp_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AreaNavigationEditorTabViewModel editor)
        {
            editor.IncreaseSelectedFloorElevation();
        }
    }
}
