namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record ShellWorkflowResult(
    bool Succeeded,
    bool IsCanceled,
    ShellWorkflowError? Error,
    IReadOnlyList<string> Diagnostics)
{
    public static ShellWorkflowResult Success(IEnumerable<string>? diagnostics = null)
    {
        return new ShellWorkflowResult(
            Succeeded: true,
            IsCanceled: false,
            Error: null,
            Diagnostics: diagnostics?.ToArray() ?? Array.Empty<string>());
    }

    public static ShellWorkflowResult Failure(ShellWorkflowError error, IEnumerable<string>? diagnostics = null)
    {
        return new ShellWorkflowResult(
            Succeeded: false,
            IsCanceled: false,
            Error: error,
            Diagnostics: diagnostics?.ToArray() ?? Array.Empty<string>());
    }

    public static ShellWorkflowResult Canceled(IEnumerable<string>? diagnostics = null)
    {
        return new ShellWorkflowResult(
            Succeeded: false,
            IsCanceled: true,
            Error: null,
            Diagnostics: diagnostics?.ToArray() ?? Array.Empty<string>());
    }
}