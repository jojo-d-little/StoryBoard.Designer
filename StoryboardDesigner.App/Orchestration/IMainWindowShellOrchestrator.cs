using StoryboardDesigner.App.Orchestration.Workflows;

namespace StoryboardDesigner.App.Orchestration;

public interface IMainWindowShellOrchestrator
{
    ShellStateSnapshot CurrentState { get; }

    event EventHandler<ShellStateSnapshot>? StateChanged;

    Task<ShellWorkflowResult<CreateProjectWorkflowResponse>> CreateProjectAsync(CreateProjectWorkflowRequest request, CancellationToken cancellationToken);

    Task<ShellWorkflowResult<OpenProjectWorkflowResponse>> OpenProjectAsync(OpenProjectWorkflowRequest request, CancellationToken cancellationToken);

    Task<ShellWorkflowResult<SaveProjectWorkflowResponse>> SaveProjectAsync(SaveProjectWorkflowRequest request, CancellationToken cancellationToken);

    Task<ShellWorkflowResult<SaveProjectAsWorkflowResponse>> SaveProjectAsAsync(SaveProjectAsWorkflowRequest request, CancellationToken cancellationToken);

    Task<ShellWorkflowResult<CloseProjectWorkflowResponse>> CloseProjectAsync(CloseProjectWorkflowRequest request, CancellationToken cancellationToken);
}