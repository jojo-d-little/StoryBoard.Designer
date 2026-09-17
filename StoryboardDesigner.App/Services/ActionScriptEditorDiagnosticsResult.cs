namespace StoryboardDesigner.App.Services;

public sealed class ActionScriptEditorDiagnosticsResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool HasBlockingErrors => Errors.Count > 0;
}
