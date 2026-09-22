using StoryboardDesigner.App.Orchestration;
using StoryboardDesigner.App.Orchestration.Diagnostics;
using StoryboardDesigner.App.Orchestration.SaveGuard;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.GameServices;

namespace StoryboardDesigner.App.Composition;

internal static class DesignerAppCompositionFactory
{
    public static DesignerAppComposition Create()
    {
        IJsonExportService jsonExportService = new JsonExportService();
        IRecentProjectsService recentProjectsService = new RecentProjectsService();
        IProjectCreationPreferencesService projectCreationPreferencesService = new ProjectCreationPreferencesService();
        IWindowPlacementService windowPlacementService = new WindowPlacementService();
        IActionScriptEvaluationService actionScriptEvaluationService = new ActionScriptEvaluationService();
        IMainWindowDialogWorkflowService dialogWorkflowService = new MainWindowDialogWorkflowService();
        IProjectUiService projectUiService = new ProjectUiService(projectCreationPreferencesService);
        IRoomDesignerDirectionImageDialogService roomDesignerDirectionImageDialogService = new RoomDesignerDirectionImageDialogService();
        ITreeContextInteractionService treeContextInteractionService = new TreeContextInteractionService();
        IPhaseTextPresentationCueCatalogService phaseTextPresentationCueCatalogService = new PhaseTextPresentationCueCatalogService();
        IGameObjectSelectionOptionDiscoveryService gameObjectSelectionOptionDiscoveryService = new GameObjectSelectionOptionDiscoveryService();
        IEventSubscriptionActionNameSuggestionDiscoveryService eventSubscriptionActionNameSuggestionDiscoveryService = new EventSubscriptionActionNameSuggestionDiscoveryService();
        IExternalSimulatorWorkflowService externalSimulatorWorkflowService = new ExternalSimulatorWorkflowService(projectCreationPreferencesService);
        IDevelopmentGameHostWorkflowService developmentGameHostWorkflowService = new DevelopmentGameHostWorkflowService(projectCreationPreferencesService);

        var mainViewModel = new MainWindowViewModel(
            jsonExportService,
            recentProjectsService,
            dialogWorkflowService,
            projectUiService,
            roomDesignerDirectionImageDialogService,
            treeContextInteractionService,
            actionScriptEvaluationService,
            phaseTextPresentationCueCatalogService: phaseTextPresentationCueCatalogService,
            gameObjectSelectionOptionDiscoveryService: gameObjectSelectionOptionDiscoveryService,
            eventSubscriptionActionNameSuggestionDiscoveryService: eventSubscriptionActionNameSuggestionDiscoveryService,
            externalSimulatorWorkflowService: externalSimulatorWorkflowService,
            developmentGameHostWorkflowService: developmentGameHostWorkflowService,
            projectCreationPreferencesService: projectCreationPreferencesService);

        var mainWindow = new MainWindow(mainViewModel, windowPlacementService);
        IShellDiagnosticsSink diagnosticsSink = new MainWindowOutputConsoleShellDiagnosticsSink(mainViewModel);
        ISaveGuardPolicyService saveGuardPolicyService = new SaveGuardPolicyService();
        IMainWindowShellOrchestrator shellOrchestrator = new MainWindowShellOrchestrator(mainViewModel, diagnosticsSink, saveGuardPolicyService);
        mainViewModel.AttachShellOrchestrator(shellOrchestrator);
        return new DesignerAppComposition(mainWindow, mainViewModel, shellOrchestrator);
    }
}
