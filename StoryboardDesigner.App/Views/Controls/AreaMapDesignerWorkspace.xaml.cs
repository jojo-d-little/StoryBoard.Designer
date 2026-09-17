using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for AreaMapDesignerWorkspace.xaml
/// </summary>
public partial class AreaMapDesignerWorkspace : System.Windows.Controls.UserControl
{
    public AreaMapDesignerWorkspace()
    {
        InitializeComponent();
    }

    private MainWindow? GetHostWindow() => Window.GetWindow(this) as MainWindow;

    private void AreaMapFit_OnClick(object sender, RoutedEventArgs e)
    {
        GetHostWindow()?.AreaMapFit_OnClick(sender, e);
    }

    internal bool ClearPendingDestinationMode()
    {
        var cleared = false;
        foreach (var canvas in FindDescendants<AreaMapCanvas>(this))
        {
            cleared |= canvas.ClearPendingDestinationArrow();
        }

        return cleared;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject current) where T : DependencyObject
    {
        if (current is null)
        {
            yield break;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(current);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(current, index);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

}
