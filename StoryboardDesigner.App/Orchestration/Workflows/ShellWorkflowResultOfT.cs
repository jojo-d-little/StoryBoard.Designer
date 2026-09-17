namespace StoryboardDesigner.App.Orchestration.Workflows;

public sealed record ShellWorkflowResult<TPayload>(
    bool Succeeded,
    bool IsCanceled,
    TPayload? Payload,
    ShellWorkflowError? Error,
    IReadOnlyList<string> Diagnostics)
{
    public static ShellWorkflowResult<TPayload> Success(TPayload payload, IEnumerable<string>? diagnostics = null)
    {
        return new ShellWorkflowResult<TPayload>(
            Succeeded: true,
            IsCanceled: false,
            Payload: payload,
            Error: null,
            Diagnostics: diagnostics?.ToArray() ?? Array.Empty<string>());
    }

    public static ShellWorkflowResult<TPayload> Failure(ShellWorkflowError error, IEnumerable<string>? diagnostics = null)
    {
        return new ShellWorkflowResult<TPayload>(
            Succeeded: false,
            IsCanceled: false,
            Payload: default,
            Error: error,
            Diagnostics: diagnostics?.ToArray() ?? Array.Empty<string>());
    }

    public static ShellWorkflowResult<TPayload> Canceled(IEnumerable<string>? diagnostics = null)
    {
        return new ShellWorkflowResult<TPayload>(
            Succeeded: false,
            IsCanceled: true,
            Payload: default,
            Error: null,
            Diagnostics: diagnostics?.ToArray() ?? Array.Empty<string>());
    }
}
