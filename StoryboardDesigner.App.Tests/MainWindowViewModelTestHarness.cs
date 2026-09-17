using System.Reflection;
using System.IO;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

internal static class MainWindowViewModelTestHarness
{
    internal sealed record ViewModelBundle(
        MainWindowViewModel ViewModel,
        JsonExportServiceStub JsonExport,
        ProjectUiServiceStub ProjectUi,
        TreeContextInteractionServiceStub TreeContext,
        RoomDesignerDirectionImageDialogServiceStub RoomDesignerDirectionImageDialogService);

    internal static MainWindowViewModel CreateViewModel(
        ProjectModel project,
        TreeContextInteractionServiceStub treeContext,
        ProjectUiServiceStub projectUi,
        IGameObjectSelectionOptionDiscoveryService? discoveryService = null)
    {
        var bundle = CreateViewModelBundle(project, treeContext, projectUi, discoveryService);
        return bundle.ViewModel;
    }

    internal static ViewModelBundle CreateViewModelBundle(
        ProjectModel project,
        TreeContextInteractionServiceStub treeContext,
        ProjectUiServiceStub projectUi,
        IGameObjectSelectionOptionDiscoveryService? discoveryService = null)
    {
        var jsonExport = new JsonExportServiceStub();
        var roomDesignerDirectionImageDialogService = new RoomDesignerDirectionImageDialogServiceStub();

        var vm = new MainWindowViewModel(
            jsonExport,
            new RecentProjectsServiceStub(),
            new MainWindowDialogWorkflowServiceStub(),
            projectUi,
            roomDesignerDirectionImageDialogService,
            treeContext,
            new ActionScriptEvaluationService(),
            phaseTextPresentationCueCatalogService: null,
            gameObjectSelectionOptionDiscoveryService: discoveryService);

        var field = typeof(MainWindowViewModel).GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, project);

        return new ViewModelBundle(vm, jsonExport, projectUi, treeContext, roomDesignerDirectionImageDialogService);
    }
}

internal sealed class JsonExportServiceStub : IJsonExportService
{
    public int SaveProjectModelCallCount { get; private set; }
    public int ExportCleanProjectCallCount { get; private set; }
    public int CreateProjectSkeletonCallCount { get; private set; }
    public Exception? SaveProjectModelException { get; set; }
    public ProjectModel? ProjectModelToLoad { get; set; }
    public string LastCreateProjectBaseFolder { get; private set; } = string.Empty;
    public string LastCreateProjectName { get; private set; } = string.Empty;
    public ProjectGlobalNodeImportData? GlobalNodeImportData { get; set; }

    public string CreateProjectSkeleton(string baseFolder, string projectName)
    {
        CreateProjectSkeletonCallCount++;
        LastCreateProjectBaseFolder = baseFolder;
        LastCreateProjectName = projectName;
        return Path.Combine(baseFolder, projectName, $"{projectName}.sbe.json");
    }
    public void SaveProjectModel(string projectFilePath, ProjectModel project)
    {
        SaveProjectModelCallCount++;
        if (SaveProjectModelException is not null)
        {
            throw SaveProjectModelException;
        }
    }
    public void SaveProjectUiState(string projectFilePath, ProjectUiState uiState) { }
    public ProjectModel? TryLoadProjectModel(string projectFilePath) => ProjectModelToLoad;
    public ProjectGlobalNodeImportData? TryLoadGlobalNodeForImport(string globalNodeFilePath) => GlobalNodeImportData;
    public string ExportCleanProjectV1(string projectFilePath, ProjectModel project)
    {
        ExportCleanProjectCallCount++;
        return string.Empty;
    }

    public string ExportPhaseNarrativeReviewHtml(string projectFilePath, ProjectModel project)
    {
        var projectFolder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(projectFolder, "phase-narrative-review.html");
    }
}

internal sealed class RecentProjectsServiceStub : IRecentProjectsService
{
    public List<string> Load() => [];
    public void Save(IEnumerable<string> projectPaths) { }
}

internal sealed class MainWindowDialogWorkflowServiceStub : IMainWindowDialogWorkflowService
{
    public void OpenRoomCommandsActions(MainWindowViewModel viewModel) { }

    public bool EditScopedActions(
        string scopeLabel,
        PropertyResolutionScope variableScope,
        string scopeEntityName,
        IList<CommandAction> actions,
        IReadOnlyList<CommandAction> additionalLinkTargetActions,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        IReadOnlyList<string> echoReferenceTokens,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<string> verbSuggestions,
        IReadOnlyList<string> directionalSuggestions,
        Func<IReadOnlyList<CommandAction>, IReadOnlyList<ProjectValidationIssue>>? validateWithPipeline = null)
    {
        return false;
    }

    public bool EditSingleScopedAction(
        CommandAction action,
        PropertyResolutionScope variableScope,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        IReadOnlyList<string> echoReferenceTokens,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<CommandAction> availableActions)
    {
        return false;
    }
}

internal sealed class ProjectUiServiceStub : IProjectUiService
{
    public List<string> InformationMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public bool ContinueSaveWithValidation { get; set; } = true;
    public bool ContinueWithValidationSummary { get; set; } = true;
    public bool ConfirmResult { get; set; } = true;
    public string LastConfirmMessage { get; private set; } = string.Empty;
    public string LastConfirmTitle { get; private set; } = string.Empty;
    public int LastValidationErrorCount { get; private set; }
    public int LastValidationWarningCount { get; private set; }
    public int LastValidationIssueCount { get; private set; }
    public string LastValidationWorkflowName { get; private set; } = string.Empty;
    public string LastValidationReportPath { get; private set; } = string.Empty;
    public IReadOnlyList<ProjectValidationIssue> LastValidationIssues { get; private set; } = Array.Empty<ProjectValidationIssue>();
    public bool SaveAsInputAccepted { get; set; }
    public string SaveAsProjectFolder { get; set; } = string.Empty;
    public string SaveAsProjectName { get; set; } = string.Empty;
    public bool CreateInputAccepted { get; set; }
    public string CreateProjectFolder { get; set; } = string.Empty;
    public string CreateProjectName { get; set; } = string.Empty;
    public string? CreateStarterProjectFilePath { get; set; }
    public string LastStarterBrowseFolder { get; set; } = string.Empty;
    public bool SourceImageManagementAccepted { get; set; }
    public SourceImageManagementDialogResult SourceImageManagementResult { get; set; } = new(0, string.Empty);
    public SourceImageManagementDialogRequest? LastSourceImageManagementRequest { get; private set; }
    public bool DefaultEchoRefreshTargetAccepted { get; set; } = true;
    public DefaultEchoMessagesRefreshTarget DefaultEchoRefreshTarget { get; set; } = DefaultEchoMessagesRefreshTarget.Application;
    public string LastOpenedTextFilePath { get; private set; } = string.Empty;
    public string LastOpenedUri { get; private set; } = string.Empty;
    public bool OpenUriInDefaultBrowserResult { get; set; } = true;
    public string OpenUriInDefaultBrowserErrorMessage { get; set; } = string.Empty;
    public bool ImportGlobalNodePathAccepted { get; set; }
    public string ImportGlobalNodeProjectPath { get; set; } = string.Empty;
    public bool ImportGlobalNodeSelectionAccepted { get; set; }
    public GlobalImportSelectionResult ImportGlobalNodeSelection { get; set; } = new();

    public CreateProjectDialogRequest BuildCreateProjectDialogRequest() => new()
    {
        DefaultProjectFolder = string.Empty,
        DefaultStarterBrowseFolder = string.Empty,
        StarterProjectsRoot = string.Empty,
        BuiltInStarterProjects = Array.Empty<StarterProjectOption>()
    };

    public bool TryOpenUriInDefaultBrowser(string uri, out string errorMessage)
    {
        LastOpenedUri = uri;
        errorMessage = OpenUriInDefaultBrowserErrorMessage;
        return OpenUriInDefaultBrowserResult;
    }

    public void SaveCreateProjectDialogPreferences(CreateProjectDialogRequest request, CreateProjectDialogResult result)
    {
    }

    public bool TryGetCreateProjectInput(CreateProjectDialogRequest request, out CreateProjectDialogResult result)
    {
        result = new CreateProjectDialogResult
        {
            ProjectFolder = CreateProjectFolder,
            ProjectName = CreateProjectName,
            StarterProjectFilePath = CreateStarterProjectFilePath,
            LastStarterBrowseFolder = LastStarterBrowseFolder
        };

        return CreateInputAccepted;
    }

    public bool TryGetOpenProjectPath(out string projectFilePath)
    {
        projectFilePath = string.Empty;
        return false;
    }

    public bool TryGetSaveAsProjectInput(out string projectFolder, out string projectName)
    {
        projectFolder = SaveAsProjectFolder;
        projectName = SaveAsProjectName;
        return SaveAsInputAccepted;
    }

    public bool TryGetImportGlobalNodeProjectPath(out string projectFilePath)
    {
        projectFilePath = ImportGlobalNodeProjectPath;
        return ImportGlobalNodePathAccepted;
    }

    public bool TryGetImportGlobalNodeSelection(GlobalImportSelectionRequest request, out GlobalImportSelectionResult selection)
    {
        selection = ImportGlobalNodeSelection;
        return ImportGlobalNodeSelectionAccepted;
    }

    public bool TryGetValidationRunSelection(ValidationRunDialogRequest request, out ValidationRunDialogResult selection)
    {
        selection = new ValidationRunDialogResult
        {
            Scope = request.InitialScope,
            Completion = request.InitialCompletion
        };
        return false;
    }

    public void ShowInformation(string message, string title)
    {
        InformationMessages.Add(message);
    }

    public void ShowWarning(string message, string title)
    {
        WarningMessages.Add(message);
    }

    public void ShowError(string message, string title)
    {
        ErrorMessages.Add(message);
    }

    public bool Confirm(string message, string title)
    {
        LastConfirmMessage = message;
        LastConfirmTitle = title;
        return ConfirmResult;
    }

    public bool ConfirmContinueWithValidationSummary(string workflowName, int issueCount, int errorCount, int warningCount)
    {
        LastValidationWorkflowName = workflowName;
        LastValidationIssueCount = issueCount;
        LastValidationErrorCount = errorCount;
        LastValidationWarningCount = warningCount;
        return ContinueWithValidationSummary;
    }

    public bool ConfirmContinueSaveWithValidation(int errorCount, int warningCount, string reportFilePath)
    {
        LastValidationErrorCount = errorCount;
        LastValidationWarningCount = warningCount;
        LastValidationReportPath = reportFilePath;
        return ContinueSaveWithValidation;
    }

    public void ShowValidationReport(
        string reportTitle,
        IReadOnlyList<ProjectValidationIssue> issues,
        string reportFilePath,
        Action<ProjectValidationIssue>? navigateToIssue = null)
    {
        LastValidationIssues = issues;
        LastValidationReportPath = reportFilePath;
    }

    public AutoSaveBeforeGameStateInitResponse PromptAutoSaveBeforeGameStateInitialization() => new();

    public bool TryGetRuntimeVariableValue(string variableName, string currentValue, out string value)
    {
        value = currentValue;
        return false;
    }

    public bool TryRunSourceImageManagement(SourceImageManagementDialogRequest request, out SourceImageManagementDialogResult result)
    {
        LastSourceImageManagementRequest = request;
        result = SourceImageManagementResult;
        return SourceImageManagementAccepted;
    }

    public bool TryOpenTextFileInDefaultEditor(string filePath, out string errorMessage)
    {
        LastOpenedTextFilePath = filePath;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryGetDefaultEchoMessagesRefreshTarget(bool projectTargetAvailable, out DefaultEchoMessagesRefreshTarget target)
    {
        target = projectTargetAvailable
            ? DefaultEchoRefreshTarget
            : DefaultEchoMessagesRefreshTarget.Application;
        return DefaultEchoRefreshTargetAccepted;
    }

    public bool TryGetSaveGameSimulatorRecordingPath(out string filePath)
    {
        filePath = string.Empty;
        return false;
    }

    public bool TryGetOpenGameSimulatorRecordingPath(out string filePath)
    {
        filePath = string.Empty;
        return false;
    }

    public bool TryGetSaveOutputLogPath(out string filePath)
    {
        filePath = string.Empty;
        return false;
    }
}

internal sealed class RoomDesignerDirectionImageDialogServiceStub : IRoomDesignerDirectionImageDialogService
{
    public int OpenOrFocusCallCount { get; private set; }
    public int CloseAllEditorsCallCount { get; private set; }
    public Guid LastRoomId { get; private set; }
    public RoomDesignerImageSlotViewModel? LastSlot { get; private set; }

    public void OpenOrFocus(Guid roomId, RoomDesignerImageSlotViewModel slot)
    {
        OpenOrFocusCallCount++;
        LastRoomId = roomId;
        LastSlot = slot;
    }

    public void CloseAllEditors()
    {
        CloseAllEditorsCallCount++;
    }
}

internal sealed class TreeContextInteractionServiceStub : ITreeContextInteractionService
{
    public Func<TraversalWizardDialogRequest, TraversalWizardDialogResult>? ReviewHandler { get; init; }
    public Func<ObjectBasicPropertiesEditRequest, ObjectBasicPropertiesEditRequest>? EditObjectBasicPropertiesHandler { get; init; }
    public Func<RoomSettingsEditRequest, RoomSettingsEditRequest?>? EditRoomSettingsHandler { get; init; }
    public Func<PhaseNodeEditRequest, PhaseNodeEditRequest?>? EditPhaseNodeHandler { get; init; }
    public Func<string, string?>? TemplateNameFromObjectHandler { get; init; }
    public Func<IReadOnlyList<GameObject>, (bool Accepted, GameObject? SelectedTemplate)>? ObjectTemplateSelectionHandler { get; init; }
    public Func<IReadOnlyList<Room>, (bool Accepted, Room? SelectedTemplate)>? RoomTemplateSelectionHandler { get; init; }
    public Action<string, IList<GamePropertyDefinition>>? ShowVariableScopeReviewHandler { get; init; }
    public Func<string, string, IList<string>, IReadOnlyList<string>, IList<DirectionalTraversalMapping>?, bool>? EditScopedTokenListHandler { get; init; }
    public Func<IReadOnlyList<BaseObjectPromotionScopeOption>, BaseObjectPromotionScopeKind?>? BaseObjectPromotionScopeSelectionHandler { get; init; }
    public Func<IReadOnlyList<QuantifiableObjectPlacementCandidate>, QuantifiableObjectPlacementSelection?>? QuantifiablePlacementSelectionHandler { get; init; }
    public ObjectBasicPropertiesEditRequest? LastObjectBasicPropertiesRequest { get; private set; }
    public IReadOnlyList<BaseObjectPromotionScopeOption> LastBaseObjectPromotionScopeOptions { get; private set; } = Array.Empty<BaseObjectPromotionScopeOption>();
    public IReadOnlyList<QuantifiableObjectPlacementCandidate> LastQuantifiablePlacementCandidates { get; private set; } = Array.Empty<QuantifiableObjectPlacementCandidate>();
    public string? LastSharedPropertyPath { get; private set; }
    public IReadOnlyList<SharedPropertyRelationshipReviewItem> LastSharedPropertyRelationships { get; private set; } = Array.Empty<SharedPropertyRelationshipReviewItem>();
    public string? LastSharedPropertyCallout { get; private set; }
    public Guid? LastSharedPropertySharedVariableId { get; private set; }
    public string? LastSharedPropertyDisplayName { get; private set; }
    public Func<Guid, string, bool>? LastSharedPropertyRenameAction { get; private set; }
    public IReadOnlyList<SharedVariableManagerListItem> LastSharedVariableManagerItems { get; private set; } = Array.Empty<SharedVariableManagerListItem>();
    public Guid? LastSharedVariableManagerSelectedId { get; private set; }
    public Func<Guid, string, bool>? LastRenameSharedVariableManagerAction { get; private set; }
    public Func<Guid, bool>? LastDropEmptySharedVariableManagerAction { get; private set; }
    public Func<IReadOnlyList<SharedVariableManagerListItem>>? LastReloadSharedVariableManagerItemsAction { get; private set; }
    public bool AutoInvokeOpenSharedVariablesManagerAction { get; set; }

    public bool TryGetNewVariableInput(string initialName, GamePropertyLifetime initialLifetime, string initialDefaultValue, GamePropertyValueRestriction initialValueRestriction, out string variableName, out GamePropertyLifetime lifetime, out string defaultValue, out GamePropertyValueRestriction valueRestriction)
    {
        variableName = string.Empty;
        lifetime = initialLifetime;
        defaultValue = initialDefaultValue;
        valueRestriction = initialValueRestriction;
        return false;
    }

    public void ShowVariableScopeReview(string scopeLabel, IList<GamePropertyDefinition> variables)
    {
        ShowVariableScopeReviewHandler?.Invoke(scopeLabel, variables);
    }

    public void ShowSharedPropertyRelationships(
        string propertyPath,
        IReadOnlyList<SharedPropertyRelationshipReviewItem> relationships,
        Action? createSharedRelationshipAction = null,
        Func<SharedPropertyRelationshipReviewItem, bool>? removeSharedRelationshipAction = null,
        Func<IReadOnlyList<SharedPropertyRelationshipReviewItem>>? reloadRelationships = null,
        Guid? sharedVariableId = null,
        string? sharedVariableDisplayName = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Action? openSharedVariablesManagerAction = null,
        string? shareStateCallout = null)
    {
        LastSharedPropertyPath = propertyPath;
        LastSharedPropertyRelationships = relationships;
        LastSharedPropertyCallout = shareStateCallout;
        LastSharedPropertySharedVariableId = sharedVariableId;
        LastSharedPropertyDisplayName = sharedVariableDisplayName;
        LastSharedPropertyRenameAction = renameSharedVariableAction;
        if (AutoInvokeOpenSharedVariablesManagerAction)
        {
            openSharedVariablesManagerAction?.Invoke();
        }
    }

    public void ShowSharedVariablesManager(
        IReadOnlyList<SharedVariableManagerListItem> items,
        Guid? selectedSharedVariableId = null,
        Func<Guid, string, bool>? renameSharedVariableAction = null,
        Func<Guid, bool>? dropEmptySharedVariableAction = null,
        Func<IReadOnlyList<SharedVariableManagerListItem>>? reloadItemsAction = null)
    {
        LastSharedVariableManagerItems = items;
        LastSharedVariableManagerSelectedId = selectedSharedVariableId;
        LastRenameSharedVariableManagerAction = renameSharedVariableAction;
        LastDropEmptySharedVariableManagerAction = dropEmptySharedVariableAction;
        LastReloadSharedVariableManagerItemsAction = reloadItemsAction;
    }

    public bool TryChooseVariable(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        PropertyResolutionScope initializationScope,
        string title,
        out string selectedValue,
        string? initialSelectedValue = null)
    {
        selectedValue = string.Empty;
        return false;
    }

    public bool EditScopedTokenList(
        string scopeLabel,
        string tokenKind,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IList<DirectionalTraversalMapping>? directionalMappings = null)
    {
        return EditScopedTokenListHandler?.Invoke(scopeLabel, tokenKind, values, inheritedValues, directionalMappings) == true;
    }

    public bool EditScopedProcedureOwnership(
        string scopeLabel,
        IList<Guid> procedureIds,
        IReadOnlyList<ProcedureDefinition> availableProcedures)
    {
        return false;
    }

    public bool EditProcedureDefinitions(IList<ProcedureDefinition> procedures)
    {
        return false;
    }

    public bool EditScopedProcedureDefinitions(
        string scopeLabel,
        IList<Guid> procedureIds,
        IList<ProcedureDefinition> allProcedures,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> objectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices)
    {
        return false;
    }

    public bool EditValidationIgnoredRuleList(
        string scopeLabel,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IReadOnlyList<ValidationRuleCatalogItem> knownRules)
    {
        return false;
    }

    public bool TryEditGlobalSettings(
        GlobalSettingsEditRequest initialValues,
        IReadOnlyList<string> startingPlanetOptions,
        IReadOnlyList<string> playerCharacterObjectOptions,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectLibraryEntries,
        IReadOnlyList<string> soundEffectCategorySuggestions,
        out GlobalSettingsEditRequest updatedValues,
        out IReadOnlyList<SoundEffectLibraryEntry> updatedSoundEffectLibraryEntries)
    {
        updatedValues = initialValues;
        updatedSoundEffectLibraryEntries = soundEffectLibraryEntries.ToList();
        return false;
    }

    public bool TryEditPlanetSettings(PlanetSettingsEditRequest initialValues, IReadOnlyList<string> startingCountryOptions, out PlanetSettingsEditRequest updatedValues)
    {
        updatedValues = initialValues;
        return false;
    }

    public bool TryEditCountrySettings(CountrySettingsEditRequest initialValues, IReadOnlyList<string> startingAreaOptions, out CountrySettingsEditRequest updatedValues)
    {
        updatedValues = initialValues;
        return false;
    }

    public bool TryEditRoomSettings(RoomSettingsEditRequest initialValues, out RoomSettingsEditRequest updatedValues)
    {
        if (EditRoomSettingsHandler is not null)
        {
            var candidate = EditRoomSettingsHandler(initialValues);
            if (candidate is not null)
            {
                updatedValues = candidate;
                return true;
            }
        }

        updatedValues = initialValues;
        return false;
    }

    public bool TryEditPhaseNode(
        PhaseNodeEditRequest initialValues,
        IReadOnlyList<SoundEffectLibraryEntry> soundEffectOptions,
        IReadOnlyList<PhaseTextPresentationCueOption> textCueOptions,
        IReadOnlyList<string> ambientTimerKeyOptions,
        out PhaseNodeEditRequest updatedValues)
    {
        if (EditPhaseNodeHandler is not null)
        {
            var candidate = EditPhaseNodeHandler(initialValues);
            if (candidate is not null)
            {
                updatedValues = candidate;
                return true;
            }
        }

        updatedValues = initialValues;
        return false;
    }

    public bool TryEditSoundEffectLibrary(
        IReadOnlyList<SoundEffectLibraryEntry> initialEntries,
        IReadOnlyList<string> categorySuggestions,
        out IReadOnlyList<SoundEffectLibraryEntry> updatedEntries)
    {
        updatedEntries = initialEntries.ToList();
        return false;
    }

    public bool TryEditObjectBasicProperties(ObjectBasicPropertiesEditRequest initialValues, out ObjectBasicPropertiesEditRequest updatedValues)
    {
        LastObjectBasicPropertiesRequest = initialValues;
        if (EditObjectBasicPropertiesHandler is not null)
        {
            updatedValues = EditObjectBasicPropertiesHandler(initialValues);
            return true;
        }

        updatedValues = initialValues;
        return false;
    }

    public bool TryEditAreaBasicProperties(AreaBasicPropertiesEditRequest initialValues, IReadOnlyList<AreaStartingRoomOption> startingRoomOptions, out AreaBasicPropertiesEditRequest updatedValues)
    {
        updatedValues = initialValues;
        return false;
    }

    public bool TrySelectObjectTemplate(IReadOnlyList<GameObject> templates, out GameObject? selectedTemplate)
    {
        if (ObjectTemplateSelectionHandler is not null)
        {
            var selection = ObjectTemplateSelectionHandler(templates);
            selectedTemplate = selection.SelectedTemplate;
            return selection.Accepted;
        }

        selectedTemplate = null;
        return false;
    }

    public bool TrySelectRoomTemplate(IReadOnlyList<Room> templates, out Room? selectedTemplate)
    {
        if (RoomTemplateSelectionHandler is not null)
        {
            var selection = RoomTemplateSelectionHandler(templates);
            selectedTemplate = selection.SelectedTemplate;
            return selection.Accepted;
        }

        selectedTemplate = null;
        return false;
    }

    public bool TryGetTemplateNameFromObject(string initialName, out string templateName)
    {
        if (TemplateNameFromObjectHandler is null)
        {
            templateName = initialName;
            return true;
        }

        var candidate = TemplateNameFromObjectHandler(initialName);
        if (candidate is null)
        {
            templateName = initialName;
            return false;
        }

        templateName = candidate;
        return true;
    }

    public bool TrySelectBaseObjectPromotionScope(
        IReadOnlyList<BaseObjectPromotionScopeOption> options,
        out BaseObjectPromotionScopeKind selectedScopeKind)
    {
        LastBaseObjectPromotionScopeOptions = options.ToList();
        var selection = BaseObjectPromotionScopeSelectionHandler?.Invoke(options);
        if (!selection.HasValue)
        {
            selectedScopeKind = BaseObjectPromotionScopeKind.Area;
            return false;
        }

        selectedScopeKind = selection.Value;
        return true;
    }

    public bool TrySelectQuantifiableObjectPlacement(IReadOnlyList<QuantifiableObjectPlacementCandidate> candidates, out QuantifiableObjectPlacementSelection? selection)
    {
        LastQuantifiablePlacementCandidates = candidates.ToList();
        selection = QuantifiablePlacementSelectionHandler?.Invoke(candidates);
        return selection is not null;
    }

    public bool TryReviewTraversalWizard(TraversalWizardDialogRequest request, out TraversalWizardDialogResult result)
    {
        result = ReviewHandler?.Invoke(request) ?? TraversalWizardDialogResult.Empty;
        return true;
    }
}
