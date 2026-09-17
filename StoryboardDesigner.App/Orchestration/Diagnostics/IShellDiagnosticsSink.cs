namespace StoryboardDesigner.App.Orchestration.Diagnostics;

public interface IShellDiagnosticsSink
{
    void Publish(ShellDiagnosticMessage message);
}