using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Orchestration.Diagnostics;

internal sealed class MainWindowOutputConsoleShellDiagnosticsSink : IShellDiagnosticsSink
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    public MainWindowOutputConsoleShellDiagnosticsSink(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public void Publish(ShellDiagnosticMessage message)
    {
        var line = $"[Orchestrator:{message.Source}:{message.Severity}] {message.Message}";
        _mainWindowViewModel.AppendDiagnosticConsoleLine(line);
    }
}