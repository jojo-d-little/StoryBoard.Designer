using StoryboardDesigner.App.Orchestration;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Composition;

internal sealed class DesignerAppComposition
{
    public DesignerAppComposition(
        MainWindow mainWindow,
        MainWindowViewModel mainWindowViewModel,
        IMainWindowShellOrchestrator shellOrchestrator)
    {
        MainWindow = mainWindow;
        MainWindowViewModel = mainWindowViewModel;
        ShellOrchestrator = shellOrchestrator;
    }

    public MainWindow MainWindow { get; }

    public MainWindowViewModel MainWindowViewModel { get; }

    public IMainWindowShellOrchestrator ShellOrchestrator { get; }
}