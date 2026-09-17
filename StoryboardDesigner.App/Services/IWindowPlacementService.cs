using System.Windows;

namespace StoryboardDesigner.App.Services;

public interface IWindowPlacementService
{
    void Save(Window window, double? leftPaneWidth = null, double? rightPaneWidth = null);

    bool TryApply(Window window, out double? leftPaneWidth, out double? rightPaneWidth);
}
