using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.References;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Orchestration;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const string DesignerProjectFileSuffix = ".sbe.json";
    private const string DesignerLoadDiagnosticsReportSuffix = ".designer-load-diagnostics.csv";

    private enum SaveValidationInteractionMode
    {
        FullReport,
        LightweightSummary
    }
    
    private readonly IJsonExportService _jsonExportService;
    private readonly IRecentProjectsService _recentProjectsService;
    private readonly IMainWindowDialogWorkflowService _dialogWorkflowService;
    private readonly IProjectUiService _projectUiService;
    private readonly IRoomDesignerDirectionImageDialogService _roomDesignerDirectionImageDialogService;
    private readonly ITreeContextInteractionService _treeContextInteractionService;
    private readonly IPhaseTextPresentationCueCatalogService _phaseTextPresentationCueCatalogService;
    private readonly IGameObjectSelectionOptionDiscoveryService _gameObjectSelectionOptionDiscoveryService;
    private readonly IEventSubscriptionActionNameSuggestionDiscoveryService _eventSubscriptionActionNameSuggestionDiscoveryService;
    private readonly IExternalSimulatorWorkflowService _externalSimulatorWorkflowService;
    private readonly IActionScriptEvaluationService _actionScriptEvaluationService;
    private IMainWindowShellOrchestrator? _shellOrchestrator;
    private bool _isShellWorkflowBusy;
    private ProjectModel _project;

    private HierarchyNodeViewModel? _selectedNode;
    private Planet? _selectedPlanet;
    private Country? _selectedCountry;
    private Room? _selectedRoom;
    private GameObject? _selectedInteractiveObject;
    private Area? _selectedArea;
    private string _selectedAreaPath = string.Empty;
    private string _exportStatus = "Ready";
    private string _projectSummary = "No project loaded";
    private string _windowTitle = "Storyboard Designer - No Project Loaded";
    private string? _projectFilePath;
    private RoomDesignerTabViewModel? _selectedRoomEditor;
    private AreaNavigationEditorTabViewModel? _selectedAreaEditor;
    private int _selectedWorkspaceTabIndex;
    private readonly RelayCommand _addNavigationLinkCommand;
    private readonly RelayCommandOfT<TraversalConnection> _removeNavigationLinkCommand;
    private readonly RelayCommand _zoomInSelectedAreaMapCommand;
    private readonly RelayCommand _zoomOutSelectedAreaMapCommand;
    private readonly RelayCommand _resetSelectedAreaMapZoomCommand;
    private readonly RelayCommand _growSelectedAreaMapGridCommand;
    private readonly RelayCommandOfT<Room> _removeRoomFromSelectedAreaMapCommand;
    private readonly RelayCommand _refreshSelectedRoomEditorCommand;
    private readonly RelayCommandOfT<RoomDesignerTabViewModel> _closeRoomEditorCommand;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly ObservableCollection<string> _outputConsoleLines = new();
    private readonly ObservableCollection<string> _projectSoundEffectCategories = new();
    private RelayCommand _clearOutputConsoleCommand = null!;
    private RelayCommand _saveOutputConsoleCommand = null!;
    private string? _lastPublishedRoomDesignerDiagnosticsSignature;
    private GameDiagnosticsLevel _selectedOutputRoomDesignerDiagnosticsLevel = GameDiagnosticsLevel.Medium;
    private bool _isProjectDirty;
    private bool _suspendUiStatePersistence;
    private string _lastUiRestoreDiagnostic = string.Empty;
    private IReadOnlyList<ProjectValidationIssue> _latestValidationIssues = Array.Empty<ProjectValidationIssue>();
    private string _latestValidationRunSummary = "Not yet validated.";
    private DateTime _latestValidationTimestamp = DateTime.MinValue;
    private readonly HashSet<string> _staleValidationNodePaths = new(StringComparer.OrdinalIgnoreCase);

    private static readonly RoomImageSlot[] AllImageSlots =
    {
        RoomImageSlot.Default,
        RoomImageSlot.North,
        RoomImageSlot.NorthEast,
        RoomImageSlot.East,
        RoomImageSlot.SouthEast,
        RoomImageSlot.South,
        RoomImageSlot.SouthWest,
        RoomImageSlot.West,
        RoomImageSlot.NorthWest,
        RoomImageSlot.Up,
        RoomImageSlot.Down
    };

    public MainWindowViewModel(
        IJsonExportService jsonExportService,
        IRecentProjectsService recentProjectsService,
        IMainWindowDialogWorkflowService dialogWorkflowService,
        IProjectUiService projectUiService,
        IRoomDesignerDirectionImageDialogService roomDesignerDirectionImageDialogService,
        ITreeContextInteractionService treeContextInteractionService,
        IActionScriptEvaluationService actionScriptEvaluationService,
        IPhaseTextPresentationCueCatalogService? phaseTextPresentationCueCatalogService = null,
        IGameObjectSelectionOptionDiscoveryService? gameObjectSelectionOptionDiscoveryService = null,
        IEventSubscriptionActionNameSuggestionDiscoveryService? eventSubscriptionActionNameSuggestionDiscoveryService = null,
        IExternalSimulatorWorkflowService? externalSimulatorWorkflowService = null)
    {
        _jsonExportService = jsonExportService;
        _recentProjectsService = recentProjectsService;
        _dialogWorkflowService = dialogWorkflowService;
        _projectUiService = projectUiService;
        _roomDesignerDirectionImageDialogService = roomDesignerDirectionImageDialogService;
        _treeContextInteractionService = treeContextInteractionService;
        _phaseTextPresentationCueCatalogService = phaseTextPresentationCueCatalogService ?? new PhaseTextPresentationCueCatalogService();
        _gameObjectSelectionOptionDiscoveryService = gameObjectSelectionOptionDiscoveryService ?? new GameObjectSelectionOptionDiscoveryService();
        _eventSubscriptionActionNameSuggestionDiscoveryService = eventSubscriptionActionNameSuggestionDiscoveryService ?? new EventSubscriptionActionNameSuggestionDiscoveryService();
        _externalSimulatorWorkflowService = externalSimulatorWorkflowService ?? new ExternalSimulatorWorkflowService(new ProjectCreationPreferencesService());
        _actionScriptEvaluationService = actionScriptEvaluationService;
        _project = new ProjectModel { Name = "Untitled Project" };

        HierarchyRoots = new ObservableCollection<HierarchyNodeViewModel>();
        RecentProjects = new ObservableCollection<string>();
        RoomImageSlots = new ObservableCollection<RoomDesignerImageSlotViewModel>();
        ProjectCommandVerbs = new ObservableCollection<string>();
        ProjectDirectionals = new ObservableCollection<string>();
        ProjectCommandQualifierOptions = new ObservableCollection<string>();
        OpenRoomEditors = new ObservableCollection<RoomDesignerTabViewModel>();
        OpenAreaEditors = new ObservableCollection<AreaNavigationEditorTabViewModel>();
        _addNavigationLinkCommand = new RelayCommand(AddNavigationLinkToSelectedArea, () => SelectedAreaEditor is not null);
        _removeNavigationLinkCommand = new RelayCommandOfT<TraversalConnection>(RemoveNavigationLinkCommandExecute, link => SelectedAreaEditor is not null && link is not null);
        _zoomInSelectedAreaMapCommand = new RelayCommand(ZoomInSelectedAreaMap, () => SelectedAreaEditor is not null);
        _zoomOutSelectedAreaMapCommand = new RelayCommand(ZoomOutSelectedAreaMap, () => SelectedAreaEditor is not null);
        _resetSelectedAreaMapZoomCommand = new RelayCommand(ResetSelectedAreaMapZoom, () => SelectedAreaEditor is not null);
        _growSelectedAreaMapGridCommand = new RelayCommand(GrowSelectedAreaMapGrid, () => SelectedAreaEditor is not null);
        _removeRoomFromSelectedAreaMapCommand = new RelayCommandOfT<Room>(RemoveRoomFromSelectedAreaMapCommandExecute, room => SelectedAreaEditor is not null && room is not null);
        _refreshSelectedRoomEditorCommand = new RelayCommand(RefreshSelectedRoomEditor, () => SelectedRoomEditor is not null);
        _closeRoomEditorCommand = new RelayCommandOfT<RoomDesignerTabViewModel>(CloseRoomEditor, editor => editor is not null && OpenRoomEditors.Contains(editor));
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _autoSaveTimer.Tick += AutoSaveTimer_OnTick;
        InitializeProjectExplorerCommands();
        InitializeProjectVocabularyCommands();
        InitializeDialogWorkflowCommands();
        InitializeFileCommands();
        InitializeRoomImageCommands();
        InitializeOutputConsoleCommands();
        InitializeHierarchyQuickAccessCommands();

        EnsureProjectCommandCatalogs();
        RebuildProjectSoundEffectCategorySuggestions();
        LoadRecentProjects();
        AppendOutputConsoleLine(_exportStatus);
    }

    public ObservableCollection<HierarchyNodeViewModel> HierarchyRoots { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<RoomDesignerImageSlotViewModel> RoomImageSlots { get; }
    public ObservableCollection<string> ProjectCommandVerbs { get; }
    public ObservableCollection<string> ProjectDirectionals { get; }
    public ObservableCollection<string> ProjectCommandQualifierOptions { get; }
    public ObservableCollection<RoomDesignerTabViewModel> OpenRoomEditors { get; }
    public ObservableCollection<AreaNavigationEditorTabViewModel> OpenAreaEditors { get; }
    public ObservableCollection<HierarchyQuickAccessItemViewModel> RecentHierarchyNodes { get; } = new();
    public ObservableCollection<HierarchyQuickAccessItemViewModel> HierarchyBookmarks { get; } = new();
    public ObservableCollection<string> OutputConsoleLines => _outputConsoleLines;
    public IReadOnlyList<string> ProjectSoundEffectCategories => _projectSoundEffectCategories;
    public IReadOnlyList<GameDiagnosticsLevel> OutputRoomDesignerDiagnosticsLevelOptions { get; } =
    [
        GameDiagnosticsLevel.None,
        GameDiagnosticsLevel.Low,
        GameDiagnosticsLevel.Medium,
        GameDiagnosticsLevel.High
    ];
    public ICommand ClearOutputConsoleCommand => _clearOutputConsoleCommand;
    public ICommand SaveOutputConsoleCommand => _saveOutputConsoleCommand;
    public ProjectModel Project => _project;
    public string? ProjectFilePath => _projectFilePath;
    public bool IsProjectDirty => _isProjectDirty;
    public double QuickAccessRecentSectionRatio
    {
        get => Math.Clamp(_project.UiState.QuickAccessRecentSectionRatio, 0.1, 0.9);
        set
        {
            var normalized = Math.Clamp(value, 0.1, 0.9);
            if (Math.Abs(_project.UiState.QuickAccessRecentSectionRatio - normalized) < 0.001)
            {
                return;
            }

            _project.UiState.QuickAccessRecentSectionRatio = normalized;
            OnPropertyChanged();
            PersistUiStateSidecarIfPossible();
        }
    }

    internal void AttachShellOrchestrator(IMainWindowShellOrchestrator shellOrchestrator)
    {
        if (_shellOrchestrator is not null)
        {
            _shellOrchestrator.StateChanged -= ShellOrchestrator_OnStateChanged;
        }

        _shellOrchestrator = shellOrchestrator;
        _isShellWorkflowBusy = shellOrchestrator.CurrentState.IsBusy;
        _shellOrchestrator.StateChanged += ShellOrchestrator_OnStateChanged;
        RefreshFileCommandCanExecuteStates();
    }

    private void ShellOrchestrator_OnStateChanged(object? sender, ShellStateSnapshot state)
    {
        if (_isShellWorkflowBusy == state.IsBusy)
        {
            return;
        }

        _isShellWorkflowBusy = state.IsBusy;
        RefreshFileCommandCanExecuteStates();
    }

    public RoomDesignerTabViewModel? SelectedRoomEditor
    {
        get => _selectedRoomEditor;
        set
        {
            if (_selectedRoomEditor == value)
            {
                return;
            }

            if (_selectedRoomEditor is not null)
            {
                _selectedRoomEditor.PropertyChanged -= SelectedRoomEditor_OnPropertyChanged;
            }

            _selectedRoomEditor = value;

            if (_selectedRoomEditor is not null)
            {
                _selectedRoomEditor.PropertyChanged += SelectedRoomEditor_OnPropertyChanged;
                if (_selectedRoomEditor.SelectedRoomDesignerDiagnosticsLevel != _selectedOutputRoomDesignerDiagnosticsLevel)
                {
                    _selectedRoomEditor.SelectedRoomDesignerDiagnosticsLevel = _selectedOutputRoomDesignerDiagnosticsLevel;
                }
            }

            _lastPublishedRoomDesignerDiagnosticsSignature = null;
            OnPropertyChanged();
            SelectedRoom = value?.Room;
            PublishSelectedRoomDesignerDiagnostics(trigger: "selected-room-editor");
        }
    }

    public GameDiagnosticsLevel SelectedOutputRoomDesignerDiagnosticsLevel
    {
        get => _selectedOutputRoomDesignerDiagnosticsLevel;
        set
        {
            if (_selectedOutputRoomDesignerDiagnosticsLevel == value)
            {
                return;
            }

            _selectedOutputRoomDesignerDiagnosticsLevel = value;
            OnPropertyChanged();

            if (SelectedRoomEditor is not null && SelectedRoomEditor.SelectedRoomDesignerDiagnosticsLevel != value)
            {
                SelectedRoomEditor.SelectedRoomDesignerDiagnosticsLevel = value;
            }

            AppendOutputConsoleLine($"[ROOM-DIAG] level={value}");
            PublishSelectedRoomDesignerDiagnostics(trigger: "output-level-change");
        }
    }

    public int SelectedWorkspaceTabIndex
    {
        get => _selectedWorkspaceTabIndex;
        set
        {
            if (_selectedWorkspaceTabIndex == value)
            {
                return;
            }

            _selectedWorkspaceTabIndex = value;
            OnPropertyChanged();
            PersistUiStateSidecarIfPossible();
        }
    }

    public AreaNavigationEditorTabViewModel? SelectedAreaEditor
    {
        get => _selectedAreaEditor;
        set
        {
            if (_selectedAreaEditor == value)
            {
                return;
            }

            _selectedAreaEditor = value;
            OnPropertyChanged();
            if (value is not null)
            {
                SelectedArea = value.Area;
            }

            _addNavigationLinkCommand.RaiseCanExecuteChanged();
            _removeNavigationLinkCommand.RaiseCanExecuteChanged();
            _zoomInSelectedAreaMapCommand.RaiseCanExecuteChanged();
            _zoomOutSelectedAreaMapCommand.RaiseCanExecuteChanged();
            _resetSelectedAreaMapZoomCommand.RaiseCanExecuteChanged();
            _growSelectedAreaMapGridCommand.RaiseCanExecuteChanged();
            _removeRoomFromSelectedAreaMapCommand.RaiseCanExecuteChanged();
            _refreshSelectedRoomEditorCommand.RaiseCanExecuteChanged();
            _closeRoomEditorCommand.RaiseCanExecuteChanged();
        }
    }

    public ICommand AddNavigationLinkCommand => _addNavigationLinkCommand;
    public ICommand RemoveNavigationLinkCommand => _removeNavigationLinkCommand;
    public ICommand ZoomInSelectedAreaMapCommand => _zoomInSelectedAreaMapCommand;
    public ICommand ZoomOutSelectedAreaMapCommand => _zoomOutSelectedAreaMapCommand;
    public ICommand ResetSelectedAreaMapZoomCommand => _resetSelectedAreaMapZoomCommand;
    public ICommand GrowSelectedAreaMapGridCommand => _growSelectedAreaMapGridCommand;
    public ICommand RemoveRoomFromSelectedAreaMapCommand => _removeRoomFromSelectedAreaMapCommand;
    public ICommand RefreshSelectedRoomEditorCommand => _refreshSelectedRoomEditorCommand;
    public ICommand CloseRoomEditorCommand => _closeRoomEditorCommand;

    public string ProjectSummary
    {
        get => _projectSummary;
        private set
        {
            if (_projectSummary == value)
            {
                return;
            }

            _projectSummary = value;
            OnPropertyChanged();
        }
    }

    public string WindowTitle
    {
        get => _windowTitle;
        private set
        {
            if (_windowTitle == value)
            {
                return;
            }

            _windowTitle = value;
            OnPropertyChanged();
        }
    }

    public HierarchyNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode == value)
            {
                return;
            }

            _selectedNode = value;
            OnPropertyChanged();
            TrackRecentHierarchyNode(value);
            ResolveSelection();
            RefreshProjectExplorerCommandStates();
            RefreshHierarchyQuickAccessCommandStates();
            PersistUiStateSidecarIfPossible();
        }
    }

    public Room? SelectedRoom
    {
        get => _selectedRoom;
        private set
        {
            if (_selectedRoom == value)
            {
                return;
            }

            _roomDesignerDirectionImageDialogService.CloseAllEditors();

            _selectedRoom = value;
            OnPropertyChanged();
            RebuildRoomImageSlots();
            RefreshDialogWorkflowCommandStates();

            if (_selectedInteractiveObject is not null
                && (_selectedRoom is null || !EnumerateGameObjectsRecursive(_selectedRoom.GameObjects).Contains(_selectedInteractiveObject)))
            {
                SelectedGameObject = null;
            }
        }
    }

    public GameObject? SelectedGameObject
    {
        get => _selectedInteractiveObject;
        private set
        {
            if (_selectedInteractiveObject == value)
            {
                return;
            }

            if (_selectedInteractiveObject is not null)
            {
                _selectedInteractiveObject.PropertyChanged -= SelectedInteractiveObject_OnPropertyChanged;
            }

            _selectedInteractiveObject = value;

            if (_selectedInteractiveObject is not null)
            {
                _selectedInteractiveObject.PropertyChanged += SelectedInteractiveObject_OnPropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedGameObject));
            RefreshProjectExplorerCommandStates();
            RefreshDialogWorkflowCommandStates();
        }
    }

    public bool HasSelectedGameObject => SelectedGameObject is not null;

    public Planet? SelectedPlanet
    {
        get => _selectedPlanet;
        private set
        {
            if (_selectedPlanet == value)
            {
                return;
            }

            _selectedPlanet = value;
            OnPropertyChanged();
        }
    }

    public Country? SelectedCountry
    {
        get => _selectedCountry;
        private set
        {
            if (_selectedCountry == value)
            {
                return;
            }

            _selectedCountry = value;
            OnPropertyChanged();
        }
    }

    private void SelectedInteractiveObject_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var shouldRefresh = string.Equals(e.PropertyName, nameof(GameObject.IsOpenable), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsInventoriable), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.InventoryPointsDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsContainer), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.ContainerPointsDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsCapacityPointShareDividerEnabled), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.CapacityPointShareDividerDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsOpenDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsLockable), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsLockedDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsActivatable), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsActiveDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsHidable), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsHiddenDefaultValue), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.IsQuantifiable), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(GameObject.Quantity), StringComparison.Ordinal);

        if (!shouldRefresh)
        {
            return;
        }

        RefreshSelectedInteractiveObjectVariableNodes();
    }

    private void RefreshSelectedInteractiveObjectVariableNodes()
    {
        if (SelectedGameObject is null)
        {
            return;
        }

        HierarchyNodeViewModel? objectNode = null;
        if (SelectedNode is not null)
        {
            objectNode = GetAncestry(SelectedNode)
                .FirstOrDefault(node => node switch
                {
                    GameObjectNodeViewModel roomObjectNode => ReferenceEquals(roomObjectNode.GameObject, SelectedGameObject),
                    GlobalObjectNodeViewModel playerObjectNode => ReferenceEquals(playerObjectNode.GameObject, SelectedGameObject),
                    TemplateGameObjectNodeViewModel templateObjectNode => ReferenceEquals(templateObjectNode.GameObject, SelectedGameObject),
                    _ => false
                });
        }

        if (objectNode is null && SelectedRoom is not null)
        {
            var roomNode = FindRoomNode(SelectedRoom);
            objectNode = roomNode?.Children
                .OfType<RoomGameObjectsNodeViewModel>()
                .SelectMany(node => node.Children.OfType<GameObjectNodeViewModel>())
                .FirstOrDefault(node => ReferenceEquals(node.GameObject, SelectedGameObject));

            if (objectNode is null)
            {
                objectNode = HierarchyRoots
                    .OfType<ProjectRootNodeViewModel>()
                    .SelectMany(root => root.Children.OfType<GlobalObjectsNodeViewModel>())
                    .SelectMany(playerNode => EnumerateHierarchyNodes(playerNode.Children).OfType<GlobalObjectNodeViewModel>())
                    .FirstOrDefault(node => ReferenceEquals(node.GameObject, SelectedGameObject));
            }

            if (objectNode is null)
            {
                objectNode = EnumerateHierarchyNodes(HierarchyRoots)
                    .OfType<TemplateGameObjectNodeViewModel>()
                    .FirstOrDefault(node => ReferenceEquals(node.GameObject, SelectedGameObject));
            }
        }

        if (objectNode is null)
        {
            return;
        }

        var variablesContainer = objectNode.Children
            .OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Object);

        if (variablesContainer is null)
        {
            return;
        }

        foreach (var variable in SelectedGameObject.Variables)
        {
            var existsInTree = variablesContainer.Children
                .OfType<GamePropertyNodeViewModel>()
                .Any(node => ReferenceEquals(node.Variable, variable));

            if (!existsInTree)
            {
                variablesContainer.Children.Add(new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Object, variablesContainer));
            }
        }

        var staleNodes = variablesContainer.Children
            .OfType<GamePropertyNodeViewModel>()
            .Where(node => !SelectedGameObject.Variables.Any(variable => ReferenceEquals(variable, node.Variable)))
            .ToList();

        foreach (var staleNode in staleNodes)
        {
            variablesContainer.Children.Remove(staleNode);
        }

        if (variablesContainer.Children.Count > 0)
        {
            objectNode.IsExpanded = true;
            variablesContainer.IsExpanded = true;
        }
    }

    public Area? SelectedArea
    {
        get => _selectedArea;
        private set
        {
            if (_selectedArea == value)
            {
                return;
            }

            _selectedArea = value;
            OnPropertyChanged();
        }
    }

    public string SelectedAreaPath
    {
        get => _selectedAreaPath;
        private set
        {
            if (_selectedAreaPath == value)
            {
                return;
            }

            _selectedAreaPath = value;
            OnPropertyChanged();
        }
    }

    public string ExportStatus
    {
        get => _exportStatus;
        private set
        {
            if (_exportStatus == value)
            {
                return;
            }

            _exportStatus = value;
            OnPropertyChanged();
            AppendOutputConsoleLine(value);
        }
    }

    private void InitializeOutputConsoleCommands()
    {
        _clearOutputConsoleCommand = new RelayCommand(ClearOutputConsoleExecute, CanClearOrSaveOutputConsoleExecute);
        _saveOutputConsoleCommand = new RelayCommand(SaveOutputConsoleExecute, CanClearOrSaveOutputConsoleExecute);
    }

    private bool CanClearOrSaveOutputConsoleExecute()
    {
        return _outputConsoleLines.Count > 0;
    }

    private void ClearOutputConsoleExecute()
    {
        _outputConsoleLines.Clear();
        _lastPublishedRoomDesignerDiagnosticsSignature = null;
        RaiseOutputConsoleCommandStateChanged();
    }

    private void SaveOutputConsoleExecute()
    {
        if (!_projectUiService.TryGetSaveOutputLogPath(out var filePath) || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        File.WriteAllLines(filePath, _outputConsoleLines);
        ExportStatus = $"Saved output log to {filePath}";
    }

    private void AppendOutputConsoleLine(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _outputConsoleLines.Add(line);

        const int maxLines = 2000;
        if (_outputConsoleLines.Count > maxLines)
        {
            _outputConsoleLines.RemoveAt(0);
        }

        RaiseOutputConsoleCommandStateChanged();
    }

    internal void AppendDiagnosticConsoleLine(string message)
    {
        AppendOutputConsoleLine(message);
    }

    private void SelectedRoomEditor_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(RoomDesignerTabViewModel.RoomDesignerDiagnosticsLines), StringComparison.Ordinal))
        {
            return;
        }

        PublishSelectedRoomDesignerDiagnostics(trigger: "room-designer-refresh");
    }

    private void PublishSelectedRoomDesignerDiagnostics(string trigger)
    {
        var roomEditor = SelectedRoomEditor;
        if (roomEditor is null)
        {
            _lastPublishedRoomDesignerDiagnosticsSignature = null;
            return;
        }

        var diagnosticsLines = roomEditor.RoomDesignerDiagnosticsLines;
        if (diagnosticsLines.Count == 0)
        {
            return;
        }

        var signature = string.Join("\n", diagnosticsLines);
        if (string.Equals(signature, _lastPublishedRoomDesignerDiagnosticsSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastPublishedRoomDesignerDiagnosticsSignature = signature;
        AppendOutputConsoleLine($"[ROOM-DIAG] room='{roomEditor.Room.ScopeName}'; trigger={trigger}");
        foreach (var line in diagnosticsLines)
        {
            AppendOutputConsoleLine($"[ROOM-DIAG] {line}");
        }
    }

    private void RaiseOutputConsoleCommandStateChanged()
    {
        _clearOutputConsoleCommand.RaiseCanExecuteChanged();
        _saveOutputConsoleCommand.RaiseCanExecuteChanged();
    }

    public bool OpenProject(string projectFilePath)
    {
        var globalNodeLoadDiagnostics = DetectGlobalNodeLoadDiagnostics(projectFilePath);

        if (globalNodeLoadDiagnostics.GlobalNodeFileMissing || globalNodeLoadDiagnostics.GlobalNodeMalformed)
        {
            WriteDesignerLoadDiagnosticsReport(
                projectFilePath,
                snappedRoomRotationCount: 0,
                globalNodeLoadDiagnostics,
                loadSucceeded: false);

            AppendDiagnosticConsoleLine("[ERROR] OpenProject: global node is required and must be valid JSON.");
            return false;
        }

        var loaded = _jsonExportService.TryLoadProjectModel(projectFilePath);
        if (loaded is null)
        {
            return false;
        }

        var snappedRoomRotationCount = SnapNonCardinalRoomObjectRotationsToQuarterTurns(loaded);

        _project = loaded;
        ScopeHierarchy.AttachParents(_project);
        OnPropertyChanged(nameof(Project));
        EnsureProjectCommandCatalogs();
        _projectFilePath = projectFilePath;
        _isProjectDirty = false;
        _autoSaveTimer.Stop();
        ExportStatus = "Ready";
        RefreshFileCommandCanExecuteStates();

        if (snappedRoomRotationCount > 0)
        {
            _jsonExportService.SaveProjectModel(projectFilePath, _project);
        }

        if (snappedRoomRotationCount > 0)
        {
            AppendDiagnosticConsoleLine($"[WARN] OpenProject: snapped {snappedRoomRotationCount} room object rotation value(s) to cardinal quarter-turn angles (0/90/180/270).");
        }

        RebuildProjectSoundEffectCategorySuggestions();

        WriteDesignerLoadDiagnosticsReport(
            projectFilePath,
            snappedRoomRotationCount,
            globalNodeLoadDiagnostics,
            loadSucceeded: true);

        LoadProjectIntoHierarchy();
        RegisterRecentProject(projectFilePath);
        return true;
    }

    private static void WriteDesignerLoadDiagnosticsReport(
        string projectFilePath,
        int snappedRoomRotationCount,
        GlobalNodeLoadDiagnostics globalNodeLoadDiagnostics,
        bool loadSucceeded)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            return;
        }

        try
        {
            var reportPath = BuildDesignerLoadDiagnosticsReportPath(projectFilePath);
            var reportFolder = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(reportFolder))
            {
                Directory.CreateDirectory(reportFolder);
            }

            using var writer = new StreamWriter(reportPath, append: false);
            writer.WriteLine("timestampUtc,severity,event,sourcePath,message");

            WriteDesignerLoadDiagnosticRow(
                writer,
                severity: "Info",
                eventName: "OpenProject",
                sourcePath: projectFilePath,
                message: loadSucceeded
                    ? "Project load completed."
                    : "Project load failed.");

            if (snappedRoomRotationCount > 0)
            {
                WriteDesignerLoadDiagnosticRow(
                    writer,
                    severity: "Warning",
                    eventName: "RotationNormalization",
                    sourcePath: projectFilePath,
                    message: $"Snapped {snappedRoomRotationCount} room object rotation value(s) to cardinal quarter-turn angles.");
            }

            if (globalNodeLoadDiagnostics.GlobalNodeFileMissing)
            {
                WriteDesignerLoadDiagnosticRow(
                    writer,
                    severity: loadSucceeded ? "Warning" : "Error",
                    eventName: "GlobalNodeMissing",
                    sourcePath: projectFilePath,
                    message: "global-node.missing.v1: Global node file was not found during load.");
            }

            if (loadSucceeded && globalNodeLoadDiagnostics.UsedRootFallbackFields)
            {
                WriteDesignerLoadDiagnosticRow(
                    writer,
                    severity: "Warning",
                    eventName: "GlobalNodeFallback",
                    sourcePath: projectFilePath,
                    message: "global-node.fallback-used.v1: Legacy root global-owned fields were used for compatibility load.");
            }

            if (loadSucceeded && globalNodeLoadDiagnostics.GlobalNodeAndRootBothContainGlobalOwnedFields)
            {
                WriteDesignerLoadDiagnosticRow(
                    writer,
                    severity: "Warning",
                    eventName: "GlobalNodeConflict",
                    sourcePath: projectFilePath,
                    message: "global-node.conflict-root-vs-global.v1: Both global node and root contain global-owned fields; global node values are authoritative.");
            }

            if (globalNodeLoadDiagnostics.GlobalNodeMalformed)
            {
                WriteDesignerLoadDiagnosticRow(
                    writer,
                    severity: "Error",
                    eventName: "GlobalNodeMalformed",
                    sourcePath: projectFilePath,
                    message: "global-node.malformed.v1: Global node file exists but is malformed JSON or has an invalid shape.");
            }
        }
        catch
        {
            // Diagnostics reporting must never block designer loading.
        }
    }

    private static void WriteDesignerLoadDiagnosticRow(StreamWriter writer, string severity, string eventName, string sourcePath, string message)
    {
        writer.WriteLine(string.Join(",",
            EscapeCsv(DateTime.UtcNow.ToString("O")),
            EscapeCsv(severity),
            EscapeCsv(eventName),
            EscapeCsv(sourcePath),
            EscapeCsv(message)));
    }

    private static string BuildDesignerLoadDiagnosticsReportPath(string projectFilePath)
    {
        if (projectFilePath.EndsWith(DesignerProjectFileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return projectFilePath[..^DesignerProjectFileSuffix.Length] + DesignerLoadDiagnosticsReportSuffix;
        }

        return projectFilePath + DesignerLoadDiagnosticsReportSuffix;
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        var escaped = text.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static int SnapNonCardinalRoomObjectRotationsToQuarterTurns(ProjectModel project)
    {
        var snappedCount = 0;

        foreach (var room in project.RoomTemplates)
        {
            snappedCount += SnapRoomObjectRotationsToQuarterTurns(room);
        }

        foreach (var room in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms))
        {
            snappedCount += SnapRoomObjectRotationsToQuarterTurns(room);
        }

        return snappedCount;
    }

    private static int SnapRoomObjectRotationsToQuarterTurns(Room room)
    {
        var snappedCount = 0;
        foreach (var obj in EnumerateGameObjectsRecursive(room.GameObjects))
        {
            var current = double.IsFinite(obj.ImageRotationDegrees) ? obj.ImageRotationDegrees : 0d;
            var snapped = SnapDegreesToQuarterTurn(current);
            if (Math.Abs(current - snapped) <= 0.0001)
            {
                continue;
            }

            obj.ImageRotationDegrees = snapped;
            snappedCount++;
        }

        return snappedCount;
    }

    private static double SnapDegreesToQuarterTurn(double degrees)
    {
        if (!double.IsFinite(degrees))
        {
            return 0;
        }

        var normalized = degrees % 360d;
        if (normalized < 0)
        {
            normalized += 360d;
        }

        var quarterTurns = (int)Math.Round(normalized / 90d, MidpointRounding.AwayFromZero);
        var snapped = quarterTurns * 90d;
        snapped %= 360d;
        return snapped < 0 ? snapped + 360d : snapped;
    }

    public bool SaveProject()
    {
        return SaveProjectInternal(isAutoSave: false, validationInteractionMode: SaveValidationInteractionMode.FullReport);
    }

    private bool SaveProjectWithValidationSummary(string workflowName)
    {
        return SaveProjectInternal(
            isAutoSave: false,
            validationInteractionMode: SaveValidationInteractionMode.LightweightSummary,
            validationSummaryWorkflowName: workflowName);
    }

    public bool SaveProjectAs(string baseFolder, string projectName)
    {
        var normalizedBaseFolder = baseFolder?.Trim() ?? string.Empty;
        var normalizedProjectName = projectName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedBaseFolder) || string.IsNullOrWhiteSpace(normalizedProjectName))
        {
            ExportStatus = "Save As requires a target folder and project name.";
            return false;
        }

        var previousProjectFilePath = _projectFilePath;
        var previousProjectName = _project.Name;

        try
        {
            var newProjectFilePath = _jsonExportService.CreateProjectSkeleton(normalizedBaseFolder, normalizedProjectName);
            _projectFilePath = newProjectFilePath;
            _project.Name = normalizedProjectName;
            RefreshFileCommandCanExecuteStates();

            if (!SaveProjectInternal(isAutoSave: false, validationInteractionMode: SaveValidationInteractionMode.FullReport))
            {
                _projectFilePath = previousProjectFilePath;
                _project.Name = previousProjectName;
                RefreshFileCommandCanExecuteStates();
                return false;
            }

            RegisterRecentProject(newProjectFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _projectFilePath = previousProjectFilePath;
            _project.Name = previousProjectName;
            RefreshFileCommandCanExecuteStates();
            ExportStatus = $"Save As failed: {ex.Message}";
            return false;
        }
    }

    internal bool CloseProjectWorkflow()
    {
        _project = CreateDefaultProjectModel("Untitled Project");
        ScopeHierarchy.AttachParents(_project);
        RebuildProjectSoundEffectCategorySuggestions();
        OnPropertyChanged(nameof(Project));

        _projectFilePath = null;
        _isProjectDirty = false;
        _autoSaveTimer.Stop();
        ExportStatus = "Ready";
        RefreshFileCommandCanExecuteStates();

        LoadProjectIntoHierarchy();
        return true;
    }

    public void PersistUiStateSnapshot()
    {
        PersistUiStateSidecarIfPossible();
    }

    public void NotifyProjectEdited()
    {
        ScopeHierarchy.AttachParents(_project);
        NormalizeStartHierarchySelections();
        MarkValidationProjectionStale(SelectedNode);
        MarkProjectDirty();
        RefreshHierarchyStartMarkers();
        RefreshSharedPropertyIndicators();
    }

    private void MarkValidationProjectionStale(HierarchyNodeViewModel? editedNode)
    {
        if (HierarchyRoots.Count == 0)
        {
            return;
        }

        if (editedNode is null)
        {
            _staleValidationNodePaths.Add("*");
        }
        else
        {
            for (HierarchyNodeViewModel? current = editedNode; current is not null; current = current.Parent)
            {
                _staleValidationNodePaths.Add(BuildNodePath(current));
            }
        }

        RefreshHierarchyValidationProjection();
    }

    private void RefreshHierarchyStartMarkers()
    {
        foreach (var root in HierarchyRoots)
        {
            RefreshHierarchyStartMarkers(root, _project);
        }
    }

    private static void RefreshHierarchyStartMarkers(HierarchyNodeViewModel node, ProjectModel project)
    {
        if (node is PlanetNodeViewModel planetNode)
        {
            planetNode.RefreshStartIndicator(project);
        }

        if (node is CountryNodeViewModel countryNode)
        {
            countryNode.RefreshStartIndicator();
        }

        if (node is AreaNodeViewModel areaNode)
        {
            areaNode.RefreshStartIndicator();
        }

        if (node is RoomNodeViewModel roomNode)
        {
            roomNode.RefreshStartIndicator();
        }

        foreach (var child in node.Children)
        {
            RefreshHierarchyStartMarkers(child, project);
        }
    }

    private void MarkProjectDirty()
    {
        _isProjectDirty = true;
        ConfigureAutoSaveTimerForCurrentSettings();
    }

    private void ConfigureAutoSaveTimerForCurrentSettings()
    {
        if (!_isProjectDirty || Project.AutoSaveSeconds <= 0)
        {
            _autoSaveTimer.Stop();
            return;
        }

        _autoSaveTimer.Stop();
        _autoSaveTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, Project.AutoSaveSeconds));
        _autoSaveTimer.Start();
    }

    private void AutoSaveTimer_OnTick(object? sender, EventArgs e)
    {
        _autoSaveTimer.Stop();
        if (!_isProjectDirty || Project.AutoSaveSeconds <= 0)
        {
            return;
        }

        if (!SaveProjectInternal(isAutoSave: true, validationInteractionMode: SaveValidationInteractionMode.FullReport) && _isProjectDirty)
        {
            ConfigureAutoSaveTimerForCurrentSettings();
        }
    }

    private bool SaveProjectInternal(
        bool isAutoSave,
        SaveValidationInteractionMode validationInteractionMode,
        string validationSummaryWorkflowName = "")
    {
        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            ExportStatus = "No project folder selected.";
            return false;
        }

        CommitAllNodeEdits();

        var validationResult = BuildProjectValidationIssues(
            new ValidationExecutionRequest(
                _project,
                ValidationExecutionKind.WholeProject,
                RootScope: null,
                IncludeDescendants: true,
                CompletionMode: ValidationCompletionMode.FullReport),
            includeImagePathAvailabilityRule: false);
        var validationIssues = validationResult.ProcessingResult.Issues.Select(MapValidationIssue).ToList();
        var validationErrorCount = validationIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var validationWarningCount = validationIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        var popupEligibleValidationIssues = validationIssues
            .Where(IsSaveTimePopupEligibleValidationIssue)
            .ToList();
        var popupEligibleErrorCount = popupEligibleValidationIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var popupEligibleWarningCount = popupEligibleValidationIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        var validationReportPath = string.Empty;

        if (validationIssues.Count > 0)
        {
            validationReportPath = WriteValidationReportFile(validationIssues, validationResult.ExecutionResult);

            if (!isAutoSave)
            {
                bool shouldContinue;
                if (popupEligibleValidationIssues.Count == 0)
                {
                    shouldContinue = true;
                }
                else if (validationInteractionMode == SaveValidationInteractionMode.LightweightSummary)
                {
                    shouldContinue = _projectUiService.ConfirmContinueWithValidationSummary(
                        validationSummaryWorkflowName,
                        popupEligibleValidationIssues.Count,
                        popupEligibleErrorCount,
                        popupEligibleWarningCount);
                }
                else
                {
                    _projectUiService.ShowValidationReport(
                        "Validation Report",
                        popupEligibleValidationIssues,
                        validationReportPath,
                        NavigateToValidationIssue);
                    shouldContinue = _projectUiService.ConfirmContinueSaveWithValidation(
                        popupEligibleErrorCount,
                        popupEligibleWarningCount,
                        validationReportPath);
                }

                if (!shouldContinue)
                {
                    ExportStatus = $"Save canceled after validation ({validationErrorCount} error(s), {validationWarningCount} warning(s)).";
                    return false;
                }
            }

            ApplyValidationErrorsToScopeNodes(validationIssues);
        }
        else
        {
            ClearScopeNodeValidationErrors();
        }

        PersistCurrentUiState();
        SyncProjectCommandCatalogsToModel();
        try
        {
            _jsonExportService.SaveProjectModel(_projectFilePath, _project);
        }
        catch (Exception ex)
        {
            ExportStatus = isAutoSave
                ? $"Auto-save failed: {ex.Message}"
                : $"Save failed: {ex.Message}";

            if (!isAutoSave)
            {
                _projectUiService.ShowError(ex.Message, "Save Failed");
            }

            return false;
        }

        string cleanProjectPath;
        try
        {
            cleanProjectPath = _jsonExportService.ExportCleanProjectV1(_projectFilePath, _project);
        }
        catch (Exception ex)
        {
            ExportStatus = $"Save succeeded but runtime export failed: {ex.Message}";
            return false;
        }

        _isProjectDirty = false;
        _autoSaveTimer.Stop();

        var baseStatus = isAutoSave
            ? $"Project auto-saved and runtime export updated: {cleanProjectPath}"
            : $"Project saved and runtime export updated: {cleanProjectPath}";

        if (validationIssues.Count == 0)
        {
            ExportStatus = baseStatus;
        }
        else
        {
            ExportStatus = baseStatus
                + $" | Validation: {validationErrorCount} error(s), {validationWarningCount} warning(s)."
                + (string.IsNullOrWhiteSpace(validationReportPath) ? string.Empty : $" Report: {validationReportPath}");
        }

        UpdateProjectSummary();
        return true;
    }

    private bool TryExportCleanProject(out string cleanProjectPath, out string failureReason, bool saveIfDirty = true)
    {
        cleanProjectPath = string.Empty;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            failureReason = "Save the project first before exporting runtime JSON.";
            ExportStatus = failureReason;
            return false;
        }

        if (saveIfDirty && _isProjectDirty && !SaveProject())
        {
            failureReason = ExportStatus;
            return false;
        }

        var validationResult = BuildProjectValidationIssues(new ValidationExecutionRequest(
            _project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport));
        var validationIssues = validationResult.ProcessingResult.Issues.Select(MapValidationIssue).ToList();
        var blockingIssues = validationIssues
            .Where(issue => issue.Severity == ValidationSeverity.Error)
            .ToList();
        if (blockingIssues.Count > 0)
        {
            var reportFilePath = WriteValidationReportFile(validationIssues, validationResult.ExecutionResult);
            _projectUiService.ShowValidationReport(
                "Validation Report",
                validationIssues,
                reportFilePath,
                NavigateToValidationIssue);
            failureReason = BuildValidationFailureStatus(blockingIssues, "runtime export") + $" Report: {reportFilePath}";
            ExportStatus = failureReason;
            return false;
        }

        try
        {
            cleanProjectPath = _jsonExportService.ExportCleanProjectV1(_projectFilePath, _project);
            ExportStatus = $"Exported runtime JSON to {cleanProjectPath}";
            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Runtime export failed: {ex.Message}";
            ExportStatus = failureReason;
            return false;
        }
    }

    private static string BuildValidationFailureStatus(IReadOnlyList<ProjectValidationIssue> validationIssues, string operationName)
    {
        var errorCount = validationIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var warningCount = validationIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);

        var preview = validationIssues
            .Take(3)
            .Select(issue => $"[{issue.Severity}] {issue.Path}: {issue.Description}")
            .ToList();

        var suffix = validationIssues.Count > preview.Count
            ? $" | ...and {validationIssues.Count - preview.Count} more issue(s)."
            : string.Empty;

        return $"Project validation found issues for {operationName} ({errorCount} error(s), {warningCount} warning(s)). {string.Join(" | ", preview)}{suffix}";
    }

    private bool TryEnsureNoBlockingValidationErrors(string operationName, out string failureReason)
    {
        failureReason = string.Empty;

        var validationResult = BuildProjectValidationIssues(new ValidationExecutionRequest(
            _project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport));

        var validationIssues = validationResult.ProcessingResult.Issues.Select(MapValidationIssue).ToList();
        var blockingIssues = validationIssues
            .Where(issue => issue.Severity == ValidationSeverity.Error)
            .ToList();

        if (blockingIssues.Count == 0)
        {
            return true;
        }

        var reportFilePath = WriteValidationReportFile(validationIssues, validationResult.ExecutionResult);
        _projectUiService.ShowValidationReport(
            "Validation Report",
            validationIssues,
            reportFilePath,
            NavigateToValidationIssue);

        failureReason = BuildValidationFailureStatus(blockingIssues, operationName) + $" Report: {reportFilePath}";
        return false;
    }

    public void ApplyImageVariant(RoomDesignerImageSlotViewModel slot, string fullPath, string grayPath, string normalPath)
    {
        slot.SetVariantPaths(fullPath, grayPath, normalPath);

        if (slot.Entry.Slot == RoomImageSlot.Default)
        {
            var ownerEditor = OpenRoomEditors.FirstOrDefault(editor => editor.RoomImageSlots.Contains(slot));
            if (ownerEditor is not null)
            {
                foreach (var roomSlot in ownerEditor.RoomImageSlots)
                {
                    roomSlot.RefreshPreview();
                }
            }
        }

        MarkProjectDirty();
        ExportStatus = $"Updated image slot '{slot.SlotLabel}'.";
    }

    public void AddProjectCommandVerb(string value)
    {
        var normalized = NormalizeCatalogValue(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (ProjectCommandVerbs.Any(v => string.Equals(v, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ProjectCommandVerbs.Add(normalized);
        SyncProjectCommandCatalogsToModel();
        MarkProjectDirty();
    }

    public void RemoveProjectCommandVerb(string value)
    {
        var existing = ProjectCommandVerbs.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        ProjectCommandVerbs.Remove(existing);
        SyncProjectCommandCatalogsToModel();
        MarkProjectDirty();
    }

    public void AddProjectCommandQualifier(string value)
    {
        var normalized = NormalizeCatalogValue(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (ProjectDirectionals.Any(v => string.Equals(v, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ProjectDirectionals.Add(normalized);
        SyncProjectCommandCatalogsToModel();
        MarkProjectDirty();
    }

    public void RemoveProjectCommandQualifier(string value)
    {
        var existing = ProjectDirectionals.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        ProjectDirectionals.Remove(existing);
        SyncProjectCommandCatalogsToModel();
        MarkProjectDirty();
    }

    public void OpenRoomEditor(Room room)
    {
        var existing = OpenRoomEditors.FirstOrDefault(tab => tab.Room.Id == room.Id);
        if (existing is not null)
        {
            SelectedRoomEditor = existing;
            SelectedWorkspaceTabIndex = 0;
            return;
        }

        EnsureRoomImageEntries(room);
        var created = new RoomDesignerTabViewModel(
            room,
            ResolveEffectiveRoomCanvasWidth(room),
            ResolveEffectiveRoomCanvasHeight(room),
            ResolveEffectiveRoomGridCellSize(),
            MarkProjectDirty,
            () => _projectFilePath,
            ResolveProjectObjectById,
            EditRoomChildObjectImageFromRoomDesigner);
        created.SelectedRoomDesignerDiagnosticsLevel = SelectedOutputRoomDesignerDiagnosticsLevel;
        OpenRoomEditors.Add(created);
        SelectedRoomEditor = created;
        SelectedWorkspaceTabIndex = 0;
        _closeRoomEditorCommand.RaiseCanExecuteChanged();
        _refreshSelectedRoomEditorCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSelectedRoomEditor()
    {
        var editor = SelectedRoomEditor;
        if (editor is null)
        {
            return;
        }

        editor.BuildRoomChildObjects();
        ExportStatus = $"Refreshed room objects for '{editor.Room.Name}'.";
    }

    private void CloseRoomEditor(RoomDesignerTabViewModel? editor)
    {
        if (editor is null)
        {
            return;
        }

        var removed = OpenRoomEditors.Remove(editor);
        if (!removed)
        {
            return;
        }

        if (ReferenceEquals(SelectedRoomEditor, editor) || SelectedRoomEditor?.Room.Id == editor.Room.Id)
        {
            SelectedRoomEditor = OpenRoomEditors.LastOrDefault();
        }

        _closeRoomEditorCommand.RaiseCanExecuteChanged();
        _refreshSelectedRoomEditorCommand.RaiseCanExecuteChanged();
    }

    private int ResolveEffectiveRoomGridCellSize()
    {
        return _project.RoomDesignerGridCellSize > 0
            ? _project.RoomDesignerGridCellSize
            : 40;
    }

    private int ResolveEffectiveRoomCanvasWidth(Room room)
    {
        return room.RoomImageCanvasWidth > 0
            ? room.RoomImageCanvasWidth
            : (_project.RoomImageCanvasWidth > 0 ? _project.RoomImageCanvasWidth : 800);
    }

    private int ResolveEffectiveRoomCanvasHeight(Room room)
    {
        return room.RoomImageCanvasHeight > 0
            ? room.RoomImageCanvasHeight
            : (_project.RoomImageCanvasHeight > 0 ? _project.RoomImageCanvasHeight : 600);
    }

    private GameObject? ResolveProjectObjectById(Guid objectId)
    {
        var objectLookup = BuildObjectLookup(_project);
        return objectLookup.TryGetValue(objectId, out var definition)
            ? definition
            : null;
    }

    private void RefreshOpenRoomEditorObjectLists(Room room)
    {
        foreach (var editor in OpenRoomEditors.Where(tab => ReferenceEquals(tab.Room, room) || tab.Room.Id == room.Id))
        {
            editor.BuildRoomChildObjects();
        }
    }

    public void OpenAreaEditor(Area area)
    {
        var existing = OpenAreaEditors.FirstOrDefault(tab => ReferenceEquals(tab.Area, area));
        if (existing is not null)
        {
            existing.SetTraversalValidationIssues(_latestValidationIssues);
            SelectedAreaEditor = existing;
            SelectedWorkspaceTabIndex = 1;
            return;
        }

        var created = new AreaNavigationEditorTabViewModel(area);
        created.SetTraversalValidationIssues(_latestValidationIssues);
        OpenAreaEditors.Add(created);
        SelectedAreaEditor = created;
        SelectedWorkspaceTabIndex = 1;
    }

    public void AddNavigationLinkToSelectedArea()
    {
        var editor = SelectedAreaEditor;
        if (editor is null)
        {
            return;
        }

        editor.AddTraversalConnection();
        RefreshTraversalLegNodesForArea(editor.Area);
    }

    public void RemoveNavigationLink(TraversalConnection link)
    {
        var editor = SelectedAreaEditor;
        if (editor is null)
        {
            return;
        }

        var isExisting = editor.TraversalConnections.Contains(link);
        var deletedConnectionId = link.TraversalConnectionId;
        editor.RemoveTraversalConnection(link);

        if (isExisting)
        {
            RemoveTraversalSharedParticipantsByConnectionIds(new[] { deletedConnectionId });
            RefreshTraversalLegNodesForArea(editor.Area);
        }
    }

    private void RemoveNavigationLinkCommandExecute(TraversalConnection? link)
    {
        if (link is null)
        {
            return;
        }

        RemoveNavigationLink(link);
    }

    public void ChangeNavigationDestination(TraversalConnection link, Guid sourceRoomId, Guid destinationRoomId)
    {
        SelectedAreaEditor?.ChangeTraversalDestination(link, sourceRoomId, destinationRoomId);
    }

    public bool TryUpdateSelectedAreaTraversal(
        TraversalConnection link,
        Guid roomAId,
        Guid roomBId,
        Direction10 baseTraversalDirectionFromA,
        TraversalAccessMode traversalAccessMode,
        OpenStateBindingMode openStateBindingMode,
        OpenablePolicy openStatePolicyFromA,
        OpenablePolicy openStatePolicyFromB,
        string? presentationEffectKey)
    {
        var editor = SelectedAreaEditor;
        if (editor is null
            || !editor.TraversalConnections.Contains(link)
            || roomAId == Guid.Empty
            || roomBId == Guid.Empty
            || roomAId == roomBId
            || editor.Area.Rooms.All(room => room.Id != roomAId)
            || editor.Area.Rooms.All(room => room.Id != roomBId))
        {
            return false;
        }

        link.RoomAId = roomAId;
        link.RoomBId = roomBId;
        link.BaseTraversalDirectionFromA = baseTraversalDirectionFromA;
        link.TraversalAccessMode = traversalAccessMode;
        link.OpenStateBindingMode = openStateBindingMode;
        link.PresentationEffectKey = string.IsNullOrWhiteSpace(presentationEffectKey)
            ? null
            : presentationEffectKey.Trim();
        link.TraversalStateFromA.OpenStatePolicy = openStatePolicyFromA;
        link.TraversalStateFromB.OpenStatePolicy = openStatePolicyFromB;
        editor.RefreshNavigationArrows();
        RefreshTraversalLegNodesForArea(editor.Area);
        return true;
    }

    public bool TryCreateSelectedAreaTraversal(
        Guid roomAId,
        Guid roomBId,
        Direction10 baseTraversalDirectionFromA,
        TraversalAccessMode traversalAccessMode,
        OpenStateBindingMode openStateBindingMode,
        OpenablePolicy openStatePolicyFromA,
        OpenablePolicy openStatePolicyFromB,
        string? presentationEffectKey,
        out TraversalConnection? createdConnection)
    {
        createdConnection = null;
        var editor = SelectedAreaEditor;
        if (editor is null
            || roomAId == Guid.Empty
            || roomBId == Guid.Empty
            || roomAId == roomBId
            || editor.Area.Rooms.All(room => room.Id != roomAId)
            || editor.Area.Rooms.All(room => room.Id != roomBId))
        {
            return false;
        }

        var duplicate = editor.TraversalConnections.Any(connection =>
            (connection.RoomAId == roomAId && connection.RoomBId == roomBId)
            || (connection.RoomAId == roomBId && connection.RoomBId == roomAId));
        if (duplicate)
        {
            return false;
        }

        var created = new TraversalConnection
        {
            RoomAId = roomAId,
            RoomBId = roomBId,
            BaseTraversalDirectionFromA = baseTraversalDirectionFromA,
            TraversalAccessMode = traversalAccessMode,
            OpenStateBindingMode = openStateBindingMode,
            PresentationEffectKey = string.IsNullOrWhiteSpace(presentationEffectKey)
                ? null
                : presentationEffectKey.Trim()
        };
        created.TraversalStateFromA.OpenStatePolicy = openStatePolicyFromA;
        created.TraversalStateFromB.OpenStatePolicy = openStatePolicyFromB;

        editor.Area.TraversalConnections.Add(created);
        editor.TraversalConnections.Add(created);
        editor.RefreshNavigationArrows();
        RefreshTraversalLegNodesForArea(editor.Area);
        createdConnection = created;
        return true;
    }

    public bool TryLinkTraversalDoors(
        TraversalConnection connection,
        Guid? fromRoomDoorObjectId,
        Guid? toRoomDoorObjectId,
        out string? warningMessage)
    {
        warningMessage = null;

        var editor = SelectedAreaEditor;
        if (editor is null || !editor.TraversalConnections.Contains(connection))
        {
            return false;
        }

        var area = editor.Area;
        var roomA = area.Rooms.FirstOrDefault(room => room.Id == connection.RoomAId);
        var roomB = area.Rooms.FirstOrDefault(room => room.Id == connection.RoomBId);
        if (roomA is null || roomB is null)
        {
            return false;
        }

        if (!TryResolveDoor(roomA, fromRoomDoorObjectId, out var fromDoor))
        {
            return false;
        }

        if (!TryResolveDoor(roomB, toRoomDoorObjectId, out var toDoor))
        {
            return false;
        }

        connection.TraversalStateFromA.OpenableObjectId = fromRoomDoorObjectId;
        connection.TraversalStateFromB.OpenableObjectId = toRoomDoorObjectId;

        if (fromDoor is null && toDoor is null)
        {
            return true;
        }

        RemoveTraversalLevelPassableBindings(connection);
        RemoveTraversalLegPassableBindings(connection);

        var chosenDoors = new[] { fromDoor, toDoor }
            .Where(door => door is not null)
            .Cast<GameObject>()
            .ToList();

        var shared = ResolveOrCreateDoorTraversalSharedVariable(chosenDoors, connection);
        if (shared is null)
        {
            return false;
        }

        foreach (var door in chosenDoors)
        {
            var doorOpenVariable = EnsureObjectBooleanVariable(door, "isOpen", door.IsOpenDefaultValue);
            DetachObjectParticipantIfLinkedElsewhere(door, doorOpenVariable, shared.Id);
            AttachObjectVariableToSharedVariable(shared, door, doorOpenVariable);
        }

        var legVariableA = EnsureTraversalLegVariable(connection.TraversalStateFromA, "isPassable", shared.DefaultValue);
        var legVariableB = EnsureTraversalLegVariable(connection.TraversalStateFromB, "isPassable", shared.DefaultValue);
        connection.TraversalStateFromA.SharedVariableId = shared.Id;
        connection.TraversalStateFromB.SharedVariableId = shared.Id;
        legVariableA.SharedVariableId = shared.Id;
        legVariableB.SharedVariableId = shared.Id;
        UpsertSharedParticipant(shared, "traversal-leg", connection.TraversalConnectionId, "isPassable", "a2b");
        UpsertSharedParticipant(shared, "traversal-leg", connection.TraversalConnectionId, "isPassable", "b2a");

        RemoveEmptySharedVariables();

        if (shared.Participants.Count > 4)
        {
            warningMessage = "Linked traversal created a shared variable group with more than 4 participants. Review shared-property relationships to confirm this is intended.";
        }

        return true;
    }

    public bool TryRelinkTraversalDoors(TraversalConnection connection, out string? warningMessage)
    {
        warningMessage = null;
        if (connection is null)
        {
            return false;
        }

        return TryLinkTraversalDoors(
            connection,
            connection.TraversalStateFromA.OpenableObjectId,
            connection.TraversalStateFromB.OpenableObjectId,
            out warningMessage);
    }

    public void SetSelectedTraversalSourceRoom(Guid roomId)
    {
        SelectedAreaEditor?.SetTraversalSourceRoom(roomId);
    }

    public void RemoveRoomFromSelectedAreaMap(Room room)
    {
        var editor = SelectedAreaEditor;
        if (editor is null)
        {
            return;
        }

        var deletedConnectionIds = editor.TraversalConnections
            .Where(connection => connection.RoomAId == room.Id || connection.RoomBId == room.Id)
            .Select(connection => connection.TraversalConnectionId)
            .Distinct()
            .ToList();

        editor.RemoveRoomFromMap(room);
        RemoveTraversalSharedParticipantsByConnectionIds(deletedConnectionIds);
        RefreshTraversalLegNodesForArea(editor.Area);
    }

    public int RemoveTraversalSharedParticipantsForDeletedTraversals(IReadOnlyCollection<Guid> deletedTraversalConnectionIds)
    {
        return RemoveTraversalSharedParticipantsByConnectionIds(deletedTraversalConnectionIds);
    }

    private void RemoveRoomFromSelectedAreaMapCommandExecute(Room? room)
    {
        if (room is null)
        {
            return;
        }

        RemoveRoomFromSelectedAreaMap(room);
    }

    public bool TryHandleEscapeAction()
    {
        if (SelectedRoomEditor?.TryUndoLastRoomChildMove() == true)
        {
            return true;
        }

        return SelectedAreaEditor?.TryUndoLastAction() ?? false;
    }

    public void ZoomInSelectedAreaMap()
    {
        SelectedAreaEditor?.ZoomIn();
    }

    public void ZoomOutSelectedAreaMap()
    {
        SelectedAreaEditor?.ZoomOut();
    }

    public void ResetSelectedAreaMapZoom()
    {
        SelectedAreaEditor?.ResetZoom();
    }

    public void FitSelectedAreaMap(double viewportWidth, double viewportHeight)
    {
        SelectedAreaEditor?.FitToViewport(viewportWidth, viewportHeight);
    }

    public void GrowSelectedAreaMapGrid()
    {
        SelectedAreaEditor?.GrowGrid();
    }

    public IReadOnlyList<string> GetVariableNamesForCurrentContext()
    {
        return GetVariableChoicesForCurrentContext(PropertyResolutionScope.Room)
            .Select(choice => choice.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<GamePropertyChoiceItem> GetVariableChoicesForCurrentContext(PropertyResolutionScope initializationScope)
    {
        if (SelectedRoom is null)
        {
            return Array.Empty<GamePropertyChoiceItem>();
        }

        var hierarchy = ResolveRoomHierarchy(SelectedRoom);
        if (hierarchy is null)
        {
            return Array.Empty<GamePropertyChoiceItem>();
        }

        var choices = new List<GamePropertyChoiceItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var includeRoom = initializationScope is PropertyResolutionScope.Object or PropertyResolutionScope.Room;
        var includeArea = initializationScope is PropertyResolutionScope.Object or PropertyResolutionScope.Room or PropertyResolutionScope.Area;
        var includeCountry = initializationScope is PropertyResolutionScope.Object or PropertyResolutionScope.Room or PropertyResolutionScope.Area or PropertyResolutionScope.Country;
        var includePlanet = initializationScope is PropertyResolutionScope.Object or PropertyResolutionScope.Room or PropertyResolutionScope.Area or PropertyResolutionScope.Country or PropertyResolutionScope.Planet;

        if (initializationScope == PropertyResolutionScope.Object && SelectedGameObject is not null)
        {
            var currentObjectOwner = ResolveObjectOwnerName(SelectedGameObject);
            var currentObjectPath = BuildObjectScopePath(hierarchy.Value.planet.ScopeName, hierarchy.Value.country.ScopeName, hierarchy.Value.area.ScopeName, SelectedRoom.ScopeName, currentObjectOwner);
            AddObjectVariableChoices(choices, seen, SelectedGameObject, 0, currentObjectPath, currentObjectOwner, GamePropertyChoiceRelation.Self, includeSelfAlias: true);
        }

        if (includeRoom)
        {
            AddSimpleVariableChoices(
                choices,
                seen,
                SelectedRoom.Variables,
                1,
                PropertyResolutionScope.Room,
                BuildScopePath(hierarchy.Value.planet.ScopeName, hierarchy.Value.country.ScopeName, hierarchy.Value.area.ScopeName, SelectedRoom.ScopeName),
                SelectedRoom.ScopeName);
            AddRoomSelfVariableChoices(
                choices,
                seen,
                SelectedRoom.Variables,
                1,
                BuildScopePath(hierarchy.Value.planet.ScopeName, hierarchy.Value.country.ScopeName, hierarchy.Value.area.ScopeName, SelectedRoom.ScopeName));

            foreach (var roomObject in EnumerateGameObjectsRecursive(SelectedRoom.GameObjects))
            {
                if (string.IsNullOrWhiteSpace(roomObject.ScopeName))
                {
                    continue;
                }

                var ownerName = ResolveObjectOwnerName(roomObject);
                var objectPath = BuildObjectScopePath(hierarchy.Value.planet.ScopeName, hierarchy.Value.country.ScopeName, hierarchy.Value.area.ScopeName, SelectedRoom.ScopeName, ownerName);
                AddObjectVariableChoices(choices, seen, roomObject, 2, objectPath, ownerName);
            }

            foreach (var planet in _project.Planets)
            {
                foreach (var country in planet.Countries)
                {
                    foreach (var area in country.Areas)
                    {
                        foreach (var room in area.Rooms)
                        {
                            foreach (var inventoriableObject in EnumerateGameObjectsRecursive(room.GameObjects).Where(static obj => obj.IsInventoriable))
                            {
                                if (string.IsNullOrWhiteSpace(inventoriableObject.ScopeName))
                                {
                                    continue;
                                }

                                var ownerName = ResolveObjectOwnerName(inventoriableObject);
                                var objectPath = BuildObjectScopePath(planet.ScopeName, country.ScopeName, area.ScopeName, room.ScopeName, ownerName);
                                AddObjectVariableChoices(choices, seen, inventoriableObject, 3, objectPath, ownerName);
                            }
                        }
                    }
                }
            }
        }

        if (includeArea)
        {
            AddSimpleVariableChoices(
                choices,
                seen,
                hierarchy.Value.area.Variables,
                4,
                PropertyResolutionScope.Area,
                BuildScopePath(hierarchy.Value.planet.ScopeName, hierarchy.Value.country.ScopeName, hierarchy.Value.area.ScopeName),
                hierarchy.Value.area.ScopeName);
        }

        if (includeCountry)
        {
            AddSimpleVariableChoices(
                choices,
                seen,
                hierarchy.Value.country.Variables,
                5,
                PropertyResolutionScope.Country,
                BuildScopePath(hierarchy.Value.planet.ScopeName, hierarchy.Value.country.ScopeName),
                hierarchy.Value.country.ScopeName);
        }

        if (includePlanet)
        {
            AddSimpleVariableChoices(
                choices,
                seen,
                hierarchy.Value.planet.Variables,
                6,
                PropertyResolutionScope.Planet,
                BuildScopePath(hierarchy.Value.planet.ScopeName),
                hierarchy.Value.planet.ScopeName);
        }

        if (initializationScope is PropertyResolutionScope.Object or PropertyResolutionScope.Room or PropertyResolutionScope.Area or PropertyResolutionScope.Country or PropertyResolutionScope.Planet)
        {
            foreach (var globalObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
            {
                if (string.IsNullOrWhiteSpace(globalObject.ScopeName))
                {
                    continue;
                }

                var ownerName = ResolveObjectOwnerName(globalObject);
                AddObjectVariableChoices(
                    choices,
                    seen,
                    globalObject,
                    7,
                    BuildScopePath("Global", ownerName),
                    ownerName,
                    GamePropertyChoiceRelation.Ancestor);
            }
        }

        return choices
            .OrderBy(choice => choice.Priority)
            .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddSimpleVariableChoices(
        List<GamePropertyChoiceItem> choices,
        HashSet<string> seen,
        IEnumerable<GamePropertyDefinition> variables,
        int priority,
        PropertyResolutionScope scope,
        string scopePath,
        string ownerName,
        GamePropertyChoiceRelation relation = GamePropertyChoiceRelation.Ancestor)
    {
        foreach (var variable in variables)
        {
            var variableName = variable.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(variableName) || !seen.Add(variableName))
            {
                continue;
            }

            choices.Add(new GamePropertyChoiceItem
            {
                Value = variableName,
                ScopePath = scopePath,
                OwnerVariable = BuildOwnerVariable(ownerName, variableName),
                Scope = scope,
                ValueRestriction = variable.ValueRestriction,
                Priority = priority,
                Relation = relation
            });
        }
    }

    private static bool IsSaveTimePopupEligibleValidationIssue(ProjectValidationIssue issue)
    {
        if (issue.Severity == ValidationSeverity.Error)
        {
            return true;
        }

        return !string.Equals(issue.RuleId, "EVT-007", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddObjectVariableChoices(
        List<GamePropertyChoiceItem> choices,
        HashSet<string> seen,
        GameObject obj,
        int priority,
        string scopePath,
        string ownerName,
        GamePropertyChoiceRelation relation = GamePropertyChoiceRelation.Ancestor,
        bool includeSelfAlias = false)
    {
        var objectTokens = GetObjectTokens(obj).ToList();
        if (objectTokens.Count == 0 && !includeSelfAlias)
        {
            return;
        }

        if (includeSelfAlias)
        {
            AddBuiltInSelfChoice(choices, seen, "self.name", scopePath, priority);
            AddBuiltInSelfChoice(choices, seen, "self.nameInGame", scopePath, priority);
            AddBuiltInSelfChoice(choices, seen, "self.objectName", scopePath, priority);

            if (obj.IsQuantifiable)
            {
                AddBuiltInSelfChoice(choices, seen, "self.nearByQuantity", scopePath, priority, GamePropertyValueRestriction.Numeric);
            }
        }

        if (obj.IsQuantifiable)
        {
            foreach (var objectToken in objectTokens)
            {
                var value = $"{objectToken}.nearByQuantity";
                if (!seen.Add(value))
                {
                    continue;
                }

                choices.Add(new GamePropertyChoiceItem
                {
                    Value = value,
                    ScopePath = scopePath,
                    OwnerVariable = BuildOwnerVariable(ownerName, "nearByQuantity"),
                    Scope = PropertyResolutionScope.Object,
                    ValueRestriction = GamePropertyValueRestriction.Numeric,
                    Priority = includeSelfAlias ? priority + 1 : priority,
                    Relation = relation
                });
            }
        }

        foreach (var variable in obj.Variables)
        {
            var variableName = variable.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            if (includeSelfAlias)
            {
                var selfValue = $"self.{variableName}";
                if (seen.Add(selfValue))
                {
                    choices.Add(new GamePropertyChoiceItem
                    {
                        Value = selfValue,
                        ScopePath = scopePath,
                        OwnerVariable = BuildOwnerVariable("self", variableName),
                        Scope = PropertyResolutionScope.Object,
                        ValueRestriction = variable.ValueRestriction,
                        Priority = priority,
                        Relation = GamePropertyChoiceRelation.Self
                    });
                }
            }

            foreach (var objectToken in objectTokens)
            {
                var value = $"{objectToken}.{variableName}";
                if (!seen.Add(value))
                {
                    continue;
                }

                choices.Add(new GamePropertyChoiceItem
                {
                    Value = value,
                    ScopePath = scopePath,
                    OwnerVariable = BuildOwnerVariable(ownerName, variableName),
                    Scope = PropertyResolutionScope.Object,
                    ValueRestriction = variable.ValueRestriction,
                    Priority = includeSelfAlias ? priority + 1 : priority,
                    Relation = relation
                });
            }
        }
    }

    private static void AddRoomSelfVariableChoices(
        List<GamePropertyChoiceItem> choices,
        HashSet<string> seen,
        IEnumerable<GamePropertyDefinition> variables,
        int priority,
        string scopePath)
    {
        AddBuiltInSelfChoice(choices, seen, "self.name", scopePath, priority);
        AddBuiltInSelfChoice(choices, seen, "self.nameInGame", scopePath, priority);

        foreach (var variable in variables)
        {
            var variableName = variable.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            var selfValue = $"self.{variableName}";
            if (!seen.Add(selfValue))
            {
                continue;
            }

            choices.Add(new GamePropertyChoiceItem
            {
                Value = selfValue,
                ScopePath = scopePath,
                OwnerVariable = BuildOwnerVariable("self", variableName),
                Scope = PropertyResolutionScope.Room,
                ValueRestriction = variable.ValueRestriction,
                Priority = priority,
                Relation = GamePropertyChoiceRelation.Self
            });
        }
    }

    private static void AddBuiltInSelfChoice(
        List<GamePropertyChoiceItem> choices,
        HashSet<string> seen,
        string value,
        string scopePath,
        int priority,
        GamePropertyValueRestriction valueRestriction = GamePropertyValueRestriction.Unrestricted)
    {
        if (!seen.Add(value))
        {
            return;
        }

        choices.Add(new GamePropertyChoiceItem
        {
            Value = value,
            ScopePath = scopePath,
            OwnerVariable = value,
            Scope = PropertyResolutionScope.Object,
            ValueRestriction = valueRestriction,
            Priority = priority,
            Relation = GamePropertyChoiceRelation.Self
        });
    }

    private static string BuildOwnerVariable(string ownerName, string variableName)
    {
        var safeOwner = string.IsNullOrWhiteSpace(ownerName) ? "owner" : ownerName.Trim();
        return $"{safeOwner}.{variableName}";
    }

    private static string BuildScopePath(params string[] parts)
    {
        return string.Join(".", parts.Select(part => string.IsNullOrWhiteSpace(part) ? "(unnamed)" : part.Trim()));
    }

    private static string BuildObjectScopePath(string planet, string country, string area, string room, string ownerName)
    {
        return BuildScopePath(planet, country, area, room, ownerName);
    }

    private static string ResolveObjectOwnerName(GameObject obj)
    {
        var name = obj.ScopeName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return "game-object";
    }

    private static IEnumerable<string> GetObjectTokens(GameObject obj)
    {
        var name = obj.ScopeName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name;
        }
    }

    public IReadOnlyList<string> GetEchoReferenceTokensForCurrentContext()
    {
        var tokens = new List<string>();
        var variableNames = GetVariableNamesForCurrentContext();
        tokens.AddRange(variableNames);
        tokens.AddRange(RuntimeAnchorReferenceTokenCatalog.BuildIntrinsicAnchorTokens());

        if (SelectedRoom is not null)
        {
            foreach (var obj in EnumerateGameObjectsRecursive(SelectedRoom.GameObjects))
            {
                foreach (var token in GetObjectTokens(obj))
                {
                    tokens.Add($"{token}.description");
                }
            }
        }

        foreach (var obj in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
        {
            foreach (var token in GetObjectTokens(obj))
            {
                tokens.Add($"{token}.description");
            }
        }

        return tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token)
            .ToList();
    }

    public IReadOnlyList<ContainerTargetChoiceItem> GetContainerTargetChoicesForScopeNode(IScopedAwareNode? scopeNode)
    {
        Room? roomContext = scopeNode switch
        {
            Room room => room,
            GameObject gameObject => gameObject.EnumerateSelfAndAncestors().OfType<Room>().FirstOrDefault(),
            _ => null
        };

        roomContext ??= SelectedRoom;

        var choices = new List<ContainerTargetChoiceItem>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (roomContext is not null)
        {
            foreach (var roomObject in EnumerateGameObjectsRecursive(roomContext.GameObjects).Where(static obj => obj.IsContainer))
            {
                var containerName = roomObject.ScopeName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(containerName))
                {
                    continue;
                }

                var id = $"room:{containerName}";
                if (!seenIds.Add(id))
                {
                    continue;
                }

                choices.Add(new ContainerTargetChoiceItem
                {
                    Id = id,
                    DisplayName = $"Room: {containerName}"
                });
            }
        }

        foreach (var globalObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects).Where(static obj => obj.IsContainer))
        {
            var containerName = globalObject.ScopeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(containerName))
            {
                continue;
            }

            var id = $"global:{containerName}";
            if (!seenIds.Add(id))
            {
                continue;
            }

            choices.Add(new ContainerTargetChoiceItem
            {
                Id = id,
                DisplayName = $"Global: {containerName}"
            });
        }

        return choices;
    }

    public IReadOnlyList<MaterializeSourceObjectChoiceItem> GetMaterializeSourceObjectChoicesForScopeNode(IScopedAwareNode? scopeNode)
    {
        var roomContext = ResolveRoomContext(scopeNode) ?? SelectedRoom;
        var areaContext = ResolveAreaContext(scopeNode) ?? roomContext?.ParentScope as Area;
        var countryContext = ResolveCountryContext(scopeNode) ?? areaContext?.ParentScope as Country;
        var planetContext = ResolvePlanetContext(scopeNode) ?? countryContext?.ParentScope as Planet;

        var choices = new List<MaterializeSourceObjectChoiceItem>();
        var seenObjectIds = new HashSet<Guid>();

        if (roomContext is not null)
        {
            foreach (var roomObject in EnumerateGameObjectsRecursive(roomContext.GameObjects))
            {
                AddMaterializeSourceChoice(choices, seenObjectIds, roomObject, "Current Room");
            }
        }

        foreach (var globalObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
        {
            AddMaterializeSourceChoice(choices, seenObjectIds, globalObject, "Current Global Objects");
        }

        if (areaContext is not null)
        {
            foreach (var baseObject in EnumerateGameObjectsRecursive(areaContext.BaseObjects))
            {
                AddMaterializeSourceChoice(choices, seenObjectIds, baseObject, "Area Base Objects");
            }
        }

        if (countryContext is not null)
        {
            foreach (var baseObject in EnumerateGameObjectsRecursive(countryContext.BaseObjects))
            {
                AddMaterializeSourceChoice(choices, seenObjectIds, baseObject, "Country Base Objects");
            }
        }

        if (planetContext is not null)
        {
            foreach (var baseObject in EnumerateGameObjectsRecursive(planetContext.BaseObjects))
            {
                AddMaterializeSourceChoice(choices, seenObjectIds, baseObject, "Planet Base Objects");
            }
        }

        foreach (var baseObject in EnumerateGameObjectsRecursive(_project.BaseObjects))
        {
            AddMaterializeSourceChoice(choices, seenObjectIds, baseObject, "Global Base Objects");
        }

        return choices
            .OrderBy(choice => choice.SourceCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<CompositeRecipeChoiceItem> GetCompositeRecipeChoicesForScopeNode(IScopedAwareNode? scopeNode)
    {
        Room? roomContext = scopeNode switch
        {
            Room room => room,
            GameObject gameObject => gameObject.EnumerateSelfAndAncestors().OfType<Room>().FirstOrDefault(),
            _ => null
        };

        roomContext ??= SelectedRoom;

        var scopedObjects = new List<GameObject>();

        if (roomContext is not null)
        {
            foreach (var root in roomContext.GameObjects)
            {
                scopedObjects.AddRange(EnumerateGameObjectsRecursive(new[] { root }));
            }
        }

        foreach (var root in _project.GlobalScope.GameObjects)
        {
            scopedObjects.AddRange(EnumerateGameObjectsRecursive(new[] { root }));
        }

        var byObjectId = scopedObjects
            .Where(static entry => entry.ObjectId != Guid.Empty)
            .GroupBy(entry => entry.ObjectId)
            .ToDictionary(group => group.Key, group => group.First());

        return scopedObjects
            .Where(static entry => entry.IsCompositeTarget)
            .Where(static entry => entry.CompositeRequiredParts.Count > 0)
            .Where(static entry => entry.ObjectId != Guid.Empty)
            .Select(targetObject =>
            {
                var requiredPartIds = targetObject.CompositeRequiredParts
                    .Where(part => part.PartObjectId != Guid.Empty)
                    .SelectMany(part => Enumerable.Repeat(part.PartObjectId, Math.Max(1, part.RequiredQuantity)))
                    .Where(byObjectId.ContainsKey)
                    .ToList();

                var requiredPartNames = targetObject.CompositeRequiredParts
                    .Where(part => part.PartObjectId != Guid.Empty)
                    .Select(part =>
                    {
                        var partName = byObjectId.TryGetValue(part.PartObjectId, out var partEntry)
                            ? partEntry.ScopeName
                            : part.PartObjectId.ToString("N");
                        var quantity = Math.Max(1, part.RequiredQuantity);
                        return quantity > 1 ? $"{partName} x{quantity}" : partName;
                    })
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                return new CompositeRecipeChoiceItem
                {
                    RecipeId = targetObject.CompositeRecipeId,
                    TargetObjectId = targetObject.ObjectId,
                    TargetName = targetObject.ScopeName,
                    RequiredPartObjectIds = requiredPartIds,
                    RequiredPartsDisplay = string.Join(", ", requiredPartNames),
                    PartRequirementMode = targetObject.CompositePartRequirementMode,
                    MinimumRequiredPartCount = string.Equals(targetObject.CompositePartRequirementMode, "MinimumCount", StringComparison.OrdinalIgnoreCase)
                        ? targetObject.CompositeMinimumRequiredPartCount
                        : null
                };
            })
            .OrderBy(choice => choice.TargetName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ProcedureChoiceItem> GetProcedureChoicesForScopeNode(IScopedAwareNode? scopeNode)
    {
        var procedureById = (_project.Procedures ?? new List<ProcedureDefinition>())
            .Where(static procedure => procedure.Id != Guid.Empty)
            .GroupBy(static procedure => procedure.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var choices = new List<ProcedureChoiceItem>();
        var seenIds = new HashSet<Guid>();

        void AddChoice(Guid procedureId, string ownerLabel, string scopePath)
        {
            if (procedureId == Guid.Empty
                || !seenIds.Add(procedureId)
                || !procedureById.TryGetValue(procedureId, out var procedure))
            {
                return;
            }

            choices.Add(new ProcedureChoiceItem
            {
                ProcedureId = procedureId,
                DisplayName = string.IsNullOrWhiteSpace(procedure.Name)
                    ? "Unnamed Procedure"
                    : procedure.Name.Trim(),
                OwnerLabel = string.IsNullOrWhiteSpace(ownerLabel) ? "Owner" : ownerLabel.Trim(),
                ScopePath = string.IsNullOrWhiteSpace(scopePath) ? "(unknown)" : scopePath.Trim()
            });
        }

        foreach (var globalProcedureId in (_project.ProcedureIds ?? new List<Guid>()))
        {
            AddChoice(globalProcedureId, "Global", "Global");
        }

        foreach (var item in BuildObjectScopePathLookup().Values)
        {
            var ownerName = string.IsNullOrWhiteSpace(item.Object.ScopeName)
                ? "Object"
                : item.Object.ScopeName.Trim();

            foreach (var procedureId in item.Object.ProcedureIds ?? new List<Guid>())
            {
                AddChoice(procedureId, ownerName, item.ScopePath);
            }
        }

        return choices
            .OrderBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.OwnerLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<SoundEffectChoiceItem> GetSoundEffectChoicesForScopeNode(IScopedAwareNode? scopeNode)
    {
        var choices = new List<SoundEffectChoiceItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddEntries(
            IEnumerable<SoundEffectLibraryEntry> entries,
            string sourceCategory,
            string scopePath,
            SoundEffectScopeRelation relation)
        {
            foreach (var entry in entries)
            {
                if (entry.SoundEffectId == Guid.Empty)
                {
                    continue;
                }

                var key = (entry.SoundEffectKey ?? string.Empty).Trim();
                var displayName = (entry.DisplayName ?? string.Empty).Trim();
                var dedupeKey = $"{entry.SoundEffectId:N}|{scopePath}";
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                choices.Add(new SoundEffectChoiceItem
                {
                    SoundEffectId = entry.SoundEffectId,
                    SoundEffectKey = key,
                    DisplayName = string.IsNullOrWhiteSpace(displayName)
                        ? key
                        : displayName,
                    AssetRef = (entry.AssetRef ?? string.Empty).Trim(),
                    SourceCategory = sourceCategory,
                    ScopePath = scopePath,
                    ScopeRelation = relation,
                    SoundEffectLane = entry.SoundEffectLane
                });
            }
        }

        var roomContext = ResolveRoomContext(scopeNode) ?? SelectedRoom;
        var areaContext = ResolveAreaContext(scopeNode) ?? roomContext?.ParentScope as Area;
        var countryContext = ResolveCountryContext(scopeNode) ?? areaContext?.ParentScope as Country;
        var planetContext = ResolvePlanetContext(scopeNode) ?? countryContext?.ParentScope as Planet;

        if (scopeNode is GameObject currentObject)
        {
            var objectName = string.IsNullOrWhiteSpace(currentObject.ScopeName)
                ? "Current Object"
                : currentObject.ScopeName.Trim();
            AddEntries(currentObject.SoundEffectLibraryEntries, "Current Object", objectName, SoundEffectScopeRelation.Current);

            foreach (var ancestorObject in currentObject.EnumerateSelfAndAncestors().OfType<GameObject>().Where(obj => !ReferenceEquals(obj, currentObject)))
            {
                var ancestorName = string.IsNullOrWhiteSpace(ancestorObject.ScopeName)
                    ? "Ancestor Object"
                    : ancestorObject.ScopeName.Trim();
                AddEntries(ancestorObject.SoundEffectLibraryEntries, "Ancestor Object", ancestorName, SoundEffectScopeRelation.UpScope);
            }

            foreach (var descendantObject in EnumerateGameObjectsRecursive(currentObject.ContainedObjects))
            {
                var descendantName = string.IsNullOrWhiteSpace(descendantObject.ScopeName)
                    ? "Child Object"
                    : descendantObject.ScopeName.Trim();
                AddEntries(descendantObject.SoundEffectLibraryEntries, "Child Object", descendantName, SoundEffectScopeRelation.DownScope);
            }
        }

        if (scopeNode is Room currentRoom)
        {
            var roomName = string.IsNullOrWhiteSpace(currentRoom.Name) ? "Room" : currentRoom.Name.Trim();
            AddEntries(currentRoom.SoundEffectLibraryEntries, "Current Room", roomName, SoundEffectScopeRelation.Current);

            foreach (var roomObject in EnumerateGameObjectsRecursive(currentRoom.GameObjects))
            {
                var roomObjectName = string.IsNullOrWhiteSpace(roomObject.ScopeName)
                    ? "Room Object"
                    : roomObject.ScopeName.Trim();
                AddEntries(roomObject.SoundEffectLibraryEntries, "Room Object", $"{roomName}/{roomObjectName}", SoundEffectScopeRelation.DownScope);
            }
        }

        if (roomContext is not null && scopeNode is not Room)
        {
            var roomName = string.IsNullOrWhiteSpace(roomContext.Name) ? "Room" : roomContext.Name.Trim();
            AddEntries(roomContext.SoundEffectLibraryEntries, "Current Room", roomName, SoundEffectScopeRelation.UpScope);
        }

        if (areaContext is not null)
        {
            var areaName = string.IsNullOrWhiteSpace(areaContext.Name) ? "Area" : areaContext.Name.Trim();
            var relation = scopeNode is Area ? SoundEffectScopeRelation.Current : SoundEffectScopeRelation.UpScope;
            AddEntries(areaContext.SoundEffectLibraryEntries, "Area", areaName, relation);
        }

        if (countryContext is not null)
        {
            var countryName = string.IsNullOrWhiteSpace(countryContext.Name) ? "Country" : countryContext.Name.Trim();
            var relation = scopeNode is Country ? SoundEffectScopeRelation.Current : SoundEffectScopeRelation.UpScope;
            AddEntries(countryContext.SoundEffectLibraryEntries, "Country", countryName, relation);
        }

        if (planetContext is not null)
        {
            var planetName = string.IsNullOrWhiteSpace(planetContext.Name) ? "Planet" : planetContext.Name.Trim();
            var relation = scopeNode is Planet ? SoundEffectScopeRelation.Current : SoundEffectScopeRelation.UpScope;
            AddEntries(planetContext.SoundEffectLibraryEntries, "Planet", planetName, relation);
        }

        var isGlobalScope = scopeNode is null || scopeNode == _project;
        AddEntries(_project.GlobalScope.SoundEffectLibraryEntries, "Global", "Global", isGlobalScope ? SoundEffectScopeRelation.Current : SoundEffectScopeRelation.UpScope);

        return choices
            .OrderBy(choice => choice.ScopeRelation)
            .ThenBy(choice => choice.SourceCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetTimerKeySuggestionsForScopeNode(IScopedAwareNode? scopeNode)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AddRange(HashSet<string> accumulator, IEnumerable<Storyboard.Shared.RuntimeContracts.Dtos.RuntimeTimerDefinitionDto> definitions)
        {
            foreach (var definition in definitions)
            {
                var timerKey = definition.TimerKey?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(timerKey))
                {
                    accumulator.Add(timerKey);
                }
            }
        }

        if (scopeNode is ScopeNodeBase scopedNode)
        {
            foreach (var ancestor in scopedNode.EnumerateSelfAndAncestors().OfType<ScopeNodeBase>())
            {
                AddRange(keys, ancestor.TimerDefinitions);
            }
        }
        else
        {
            AddRange(keys, _project.GlobalScope.TimerDefinitions);
        }

        return keys
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Room? ResolveRoomContext(IScopedAwareNode? scopeNode)
    {
        return scopeNode switch
        {
            Room room => room,
            GameObject gameObject => gameObject.EnumerateSelfAndAncestors().OfType<Room>().FirstOrDefault(),
            _ => scopeNode?.EnumerateSelfAndAncestors().OfType<Room>().FirstOrDefault()
        };
    }

    private static Area? ResolveAreaContext(IScopedAwareNode? scopeNode)
    {
        return scopeNode switch
        {
            Area area => area,
            _ => scopeNode?.EnumerateSelfAndAncestors().OfType<Area>().FirstOrDefault()
        };
    }

    private static Country? ResolveCountryContext(IScopedAwareNode? scopeNode)
    {
        return scopeNode switch
        {
            Country country => country,
            _ => scopeNode?.EnumerateSelfAndAncestors().OfType<Country>().FirstOrDefault()
        };
    }

    private static Planet? ResolvePlanetContext(IScopedAwareNode? scopeNode)
    {
        return scopeNode switch
        {
            Planet planet => planet,
            _ => scopeNode?.EnumerateSelfAndAncestors().OfType<Planet>().FirstOrDefault()
        };
    }

    private static void AddMaterializeSourceChoice(
        ICollection<MaterializeSourceObjectChoiceItem> choices,
        ISet<Guid> seenObjectIds,
        GameObject candidate,
        string sourceCategory)
    {
        if (candidate.ObjectId == Guid.Empty || !seenObjectIds.Add(candidate.ObjectId))
        {
            return;
        }

        var objectName = candidate.ScopeName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            objectName = "(unnamed object)";
        }

        var scopePath = BuildScopePath(candidate.EnumerateSelfAndAncestors().Reverse().Select(static node => node.ScopeName).ToArray());

        choices.Add(new MaterializeSourceObjectChoiceItem
        {
            ObjectId = candidate.ObjectId,
            SourceObject = candidate,
            DisplayName = objectName,
            ScopePath = scopePath,
            SourceCategory = sourceCategory
        });
    }

    public IReadOnlyList<GamePropertyChoiceItem> GetVariableChoicesForScopeNode(IScopedAwareNode scopeNode, PropertyResolutionScope initializationScope)
    {
        if (scopeNode is null)
        {
            return Array.Empty<GamePropertyChoiceItem>();
        }

        if (initializationScope == PropertyResolutionScope.Global || scopeNode is ProjectModel)
        {
            return BuildProjectWideEventSubscriptionFilterVariableChoices();
        }

        var choices = new List<GamePropertyChoiceItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = scopeNode;
        var depth = 0;

        while (current is not null)
        {
            switch (current)
            {
                case GameObject obj:
                    AddObjectVariableChoices(
                        choices,
                        seen,
                        obj,
                        depth,
                        BuildScopePath(obj.EnumerateSelfAndAncestors().Reverse().Select(static n => n.ScopeName).ToArray()),
                        ResolveObjectOwnerName(obj),
                        GamePropertyChoiceRelation.Self,
                        includeSelfAlias: true);

                    if (initializationScope == PropertyResolutionScope.Object)
                    {
                        foreach (var (descendantObject, descendantDepth) in EnumerateGameObjectsWithDepth(obj.ContainedObjects))
                        {
                            var ownerName = ResolveObjectOwnerName(descendantObject);
                            var relation = descendantDepth == 1
                                ? GamePropertyChoiceRelation.Child
                                : GamePropertyChoiceRelation.Descendant;

                            AddObjectVariableChoices(
                                choices,
                                seen,
                                descendantObject,
                                depth + descendantDepth,
                                BuildScopePath(descendantObject.EnumerateSelfAndAncestors().Reverse().Select(static n => n.ScopeName).ToArray()),
                                ownerName,
                                relation);
                        }
                    }
                    break;

                case Room room when ShouldIncludeScope(initializationScope, PropertyResolutionScope.Room):
                    AddSimpleVariableChoices(
                        choices,
                        seen,
                        room.Variables,
                        depth,
                        PropertyResolutionScope.Room,
                        BuildScopePathFromNode(room),
                        room.ScopeName,
                        depth == 1 ? GamePropertyChoiceRelation.Parent : GamePropertyChoiceRelation.Ancestor);
                    AddRoomSelfVariableChoices(
                        choices,
                        seen,
                        room.Variables,
                        depth,
                        BuildScopePathFromNode(room));

                    if (initializationScope == PropertyResolutionScope.Room)
                    {
                        foreach (var (childObject, childDepth) in EnumerateGameObjectsWithDepth(room.GameObjects))
                        {
                            var ownerName = ResolveObjectOwnerName(childObject);
                            var relation = childDepth == 1
                                ? GamePropertyChoiceRelation.Child
                                : GamePropertyChoiceRelation.Descendant;

                            AddObjectVariableChoices(
                                choices,
                                seen,
                                childObject,
                                depth + childDepth,
                                BuildScopePathFromNode(room, ownerName),
                                ownerName,
                                relation);
                        }
                    }

                    if (initializationScope == PropertyResolutionScope.Object)
                    {
                        var currentObject = scopeNode as GameObject;
                        foreach (var siblingObject in EnumerateGameObjectsRecursive(room.GameObjects))
                        {
                            if (currentObject is not null
                                && ReferenceEquals(siblingObject, currentObject))
                            {
                                continue;
                            }

                            var ownerName = ResolveObjectOwnerName(siblingObject);
                            AddObjectVariableChoices(
                                choices,
                                seen,
                                siblingObject,
                                depth + 1,
                                BuildScopePathFromNode(room, ownerName),
                                ownerName,
                                GamePropertyChoiceRelation.Sibling);
                        }
                    }
                    break;

                case Area area when ShouldIncludeScope(initializationScope, PropertyResolutionScope.Area):
                    AddSimpleVariableChoices(choices, seen, area.Variables, depth, PropertyResolutionScope.Area, BuildScopePathFromNode(area), area.ScopeName, GamePropertyChoiceRelation.Ancestor);
                    break;

                case Country country when ShouldIncludeScope(initializationScope, PropertyResolutionScope.Country):
                    AddSimpleVariableChoices(choices, seen, country.Variables, depth, PropertyResolutionScope.Country, BuildScopePathFromNode(country), country.ScopeName, GamePropertyChoiceRelation.Ancestor);
                    break;

                case Planet planet when ShouldIncludeScope(initializationScope, PropertyResolutionScope.Planet):
                    AddSimpleVariableChoices(choices, seen, planet.Variables, depth, PropertyResolutionScope.Planet, BuildScopePathFromNode(planet), planet.ScopeName, GamePropertyChoiceRelation.Ancestor);
                    break;

            }

            depth++;
            current = current.ParentScope;
        }

        if (initializationScope is PropertyResolutionScope.Object or PropertyResolutionScope.Room or PropertyResolutionScope.Area or PropertyResolutionScope.Country or PropertyResolutionScope.Planet)
        {
            foreach (var globalObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
            {
                var ownerName = ResolveObjectOwnerName(globalObject);
                AddObjectVariableChoices(
                    choices,
                    seen,
                    globalObject,
                    int.MaxValue - 1,
                    BuildScopePath("Global", ownerName),
                    ownerName,
                    GamePropertyChoiceRelation.Ancestor);
            }
        }

        return choices
            .OrderBy(choice => choice.Priority)
            .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldIncludeScope(PropertyResolutionScope initializationScope, PropertyResolutionScope scope)
    {
        return initializationScope switch
        {
            PropertyResolutionScope.Object => scope is PropertyResolutionScope.Object or PropertyResolutionScope.Room or PropertyResolutionScope.Area or PropertyResolutionScope.Country or PropertyResolutionScope.Planet,
            PropertyResolutionScope.Room => scope is PropertyResolutionScope.Room or PropertyResolutionScope.Area or PropertyResolutionScope.Country or PropertyResolutionScope.Planet,
            PropertyResolutionScope.Area => scope is PropertyResolutionScope.Area or PropertyResolutionScope.Country or PropertyResolutionScope.Planet,
            PropertyResolutionScope.Country => scope is PropertyResolutionScope.Country or PropertyResolutionScope.Planet,
            PropertyResolutionScope.Planet => scope is PropertyResolutionScope.Planet,
            _ => false
        };
    }

    private static IEnumerable<(GameObject Object, int Depth)> EnumerateGameObjectsWithDepth(
        IEnumerable<GameObject> roots,
        int startingDepth = 1)
    {
        foreach (var root in roots)
        {
            foreach (var candidate in EnumerateGameObjectsWithDepth(root, startingDepth))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<(GameObject Object, int Depth)> EnumerateGameObjectsWithDepth(
        GameObject root,
        int depth)
    {
        yield return (root, depth);

        foreach (var child in root.ContainedObjects)
        {
            foreach (var candidate in EnumerateGameObjectsWithDepth(child, depth + 1))
            {
                yield return candidate;
            }
        }
    }

    private static string BuildScopePathFromNode(IScopedAwareNode node)
    {
        var parts = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static n => n.ScopeName)
            .ToArray();
        return BuildScopePath(parts);
    }

    private static string BuildScopePathFromNode(IScopedAwareNode node, params string[] appendedParts)
    {
        var parts = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static n => n.ScopeName)
            .Concat(appendedParts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()))
            .ToArray();
        return BuildScopePath(parts);
    }

    private void ResolveSelection()
    {
        SelectedPlanet = null;
        SelectedCountry = null;
        SelectedRoom = null;
        SelectedGameObject = null;
        SelectedArea = null;
        SelectedAreaPath = string.Empty;

        var node = SelectedNode;
        if (node is null)
        {
            return;
        }

        var ancestry = GetAncestry(node);
        var planet = ancestry.OfType<PlanetNodeViewModel>().FirstOrDefault();
        var country = ancestry.OfType<CountryNodeViewModel>().FirstOrDefault();
        var area = ancestry.OfType<AreaNodeViewModel>().FirstOrDefault();

        SelectedPlanet = planet?.Planet;
        SelectedCountry = country?.Country;

        if (node is RoomNodeViewModel roomNode)
        {
            SelectedRoom = roomNode.Room;
            SelectedArea = roomNode.Area;
        }
        else if (node is TemplateRoomNodeViewModel templateRoomNode)
        {
            SelectedRoom = templateRoomNode.Room;
        }
        else if (node is RoomGameObjectsNodeViewModel objectsNode)
        {
            SelectedRoom = objectsNode.RoomNode.Room;
            SelectedArea = objectsNode.RoomNode.Area;
        }
        else if (node is GameObjectNodeViewModel objectNode)
        {
            if (objectNode.ParentObjectsNode is RoomGameObjectsNodeViewModel roomObjectsNode)
            {
                SelectedRoom = roomObjectsNode.RoomNode.Room;
                SelectedArea = roomObjectsNode.RoomNode.Area;
            }

            SelectedGameObject = objectNode.GameObject;
        }
        else if (node is GlobalObjectNodeViewModel playerObjectNode)
        {
            SelectedGameObject = playerObjectNode.GameObject;
        }
        else if (node is TemplateGameObjectNodeViewModel templateObjectNode)
        {
            SelectedGameObject = templateObjectNode.GameObject;
        }
        else if (node is AreaNodeViewModel areaNode)
        {
            SelectedArea = areaNode.Area;
            SelectedRoom = areaNode.Area.Rooms.FirstOrDefault();
        }
        else if (node is GamePropertyNodeViewModel or GamePropertiesContainerNodeViewModel)
        {
            var objectAncestor = ancestry.OfType<GameObjectNodeViewModel>().FirstOrDefault();
            if (objectAncestor is not null)
            {
                if (objectAncestor.ParentObjectsNode is RoomGameObjectsNodeViewModel roomObjectsNode)
                {
                    SelectedRoom = roomObjectsNode.RoomNode.Room;
                    SelectedArea = roomObjectsNode.RoomNode.Area;
                }

                SelectedGameObject = objectAncestor.GameObject;
            }
            else
            {
                var playerObjectAncestor = ancestry.OfType<GlobalObjectNodeViewModel>().FirstOrDefault();
                if (playerObjectAncestor is not null)
                {
                    SelectedGameObject = playerObjectAncestor.GameObject;
                }
                else
                {
                    var templateObjectAncestor = ancestry.OfType<TemplateGameObjectNodeViewModel>().FirstOrDefault();
                    if (templateObjectAncestor is not null)
                    {
                        SelectedGameObject = templateObjectAncestor.GameObject;
                        return;
                    }

                    var roomAncestor = ancestry.OfType<RoomNodeViewModel>().FirstOrDefault();
                    if (roomAncestor is not null)
                    {
                        SelectedRoom = roomAncestor.Room;
                        SelectedArea = roomAncestor.Area;
                    }
                    else
                    {
                        var templateRoomAncestor = ancestry.OfType<TemplateRoomNodeViewModel>().FirstOrDefault();
                        if (templateRoomAncestor is not null)
                        {
                            SelectedRoom = templateRoomAncestor.Room;
                        }
                    }

                    if (SelectedRoom is null && area is not null)
                    {
                        SelectedArea = area.Area;
                        SelectedRoom = area.Area.Rooms.FirstOrDefault();
                    }
                }
            }
        }

        if (planet is not null && country is not null && area is not null)
        {
            SelectedAreaPath = $"{planet.Planet.ScopeName} > {country.Country.ScopeName} > {area.Area.ScopeName}";
        }
    }

    private void LoadProjectIntoHierarchy()
    {
        OnPropertyChanged(nameof(QuickAccessRecentSectionRatio));
        _suspendUiStatePersistence = true;
        try
        {
        EnsureProjectCommandCatalogs();
        EnsureProjectHasStarterHierarchy();
        NormalizeStartHierarchySelections();
        ClearScopedNameIndexes();
        HierarchyRoots.Clear();
        OpenRoomEditors.Clear();
        OpenAreaEditors.Clear();
        SelectedRoomEditor = null;
        SelectedAreaEditor = null;

        foreach (var root in BuildHierarchy(_project))
        {
            root.PropertyChanged += (_, _) => UpdateProjectSummary();
            HierarchyRoots.Add(root);
        }

        RefreshAllScopedProcedureNodes();

        RefreshHierarchyValidationProjection();

        RefreshSharedPropertyIndicators();

        RefreshHierarchyQuickAccessItems();

        UpdateProjectSummary();
        if (!TryRestoreUiState())
        {
            SelectedNode = HierarchyRoots.FirstOrDefault();
        }
        }
        finally
        {
            _suspendUiStatePersistence = false;
        }

        PersistUiStateSidecarIfPossible();

        if (!string.IsNullOrWhiteSpace(_lastUiRestoreDiagnostic))
        {
            ExportStatus = _lastUiRestoreDiagnostic;
        }
    }

    private void PersistCurrentUiState()
    {
        var uiState = _project.UiState;
        uiState.SelectedWorkspaceTabIndex = SelectedWorkspaceTabIndex;
        uiState.MapDesignerAreaId = SelectedAreaEditor?.Area.Id;

        var selectedNode = SelectedNode;
        if (selectedNode is null)
        {
            return;
        }

        var ancestry = GetAncestry(selectedNode);
        uiState.PlanetName = ancestry.OfType<PlanetNodeViewModel>().FirstOrDefault()?.Planet.ScopeName ?? string.Empty;
        uiState.CountryName = ancestry.OfType<CountryNodeViewModel>().FirstOrDefault()?.Country.ScopeName ?? string.Empty;
        uiState.AreaName = ancestry.OfType<AreaNodeViewModel>().FirstOrDefault()?.Area.ScopeName ?? string.Empty;
        uiState.RoomId = SelectedRoom?.Id;
        uiState.LastSelectedNodePath = BuildNodePath(selectedNode);
    }

    private void PersistUiStateSidecarIfPossible()
    {
        if (_suspendUiStatePersistence)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            return;
        }

        try
        {
            PersistCurrentUiState();
            _jsonExportService.SaveProjectUiState(_projectFilePath, _project.UiState);
        }
        catch
        {
            // Keep editing responsive even if sidecar persistence fails; full save still persists UI state.
        }
    }

    private bool TryRestoreUiState()
    {
        try
        {
            var uiState = _project.UiState;
            var restoredSelection = false;
            var savedPath = uiState.LastSelectedNodePath?.Trim() ?? string.Empty;
            var matchedPath = string.Empty;
            var restoreMode = "none";

            SelectedWorkspaceTabIndex = uiState.SelectedWorkspaceTabIndex;

            if (TryRestoreLastSelectedNode(uiState, out matchedPath))
            {
                restoredSelection = true;
                restoreMode = "lastSelectedNodePath";
            }
            else
            {
                var areaNode = FindAreaNode(uiState.PlanetName, uiState.CountryName, uiState.AreaName);
                if (areaNode is null)
                {
                    SelectedWorkspaceTabIndex = 0;
                    _lastUiRestoreDiagnostic = BuildUiRestoreDiagnostic(savedPath, matchedPath, SelectedNode, "fallback-none");
                    return false;
                }

                var roomNode = uiState.RoomId.HasValue
                    ? EnumerateHierarchyNodes(areaNode.Children).OfType<RoomNodeViewModel>().FirstOrDefault(node => node.Room.Id == uiState.RoomId.Value)
                    : null;

                if (uiState.SelectedWorkspaceTabIndex == 0)
                {
                    if (roomNode is not null)
                    {
                        ExpandToNode(roomNode);
                        OpenRoomEditor(roomNode.Room);
                        SelectedNode = roomNode;
                        restoredSelection = true;
                        restoreMode = "fallback-room";
                    }
                    else
                    {
                        ExpandToNode(areaNode);
                        OpenAreaEditor(areaNode.Area);
                        SelectedNode = areaNode;
                        restoredSelection = true;
                        restoreMode = "fallback-area";
                    }
                }
                else
                {
                    ExpandToNode(areaNode);
                    OpenAreaEditor(areaNode.Area);

                    if (roomNode is not null)
                    {
                        OpenRoomEditor(roomNode.Room);
                    }

                    SelectedNode = areaNode;
                    restoredSelection = true;
                    restoreMode = "fallback-area-tab";
                }
            }

            if (uiState.SelectedWorkspaceTabIndex == 1 && TryRestoreMapDesignerArea(uiState))
            {
                _lastUiRestoreDiagnostic = BuildUiRestoreDiagnostic(savedPath, matchedPath, SelectedNode, string.Concat(restoreMode, "+map"));
                return true;
            }

            _lastUiRestoreDiagnostic = BuildUiRestoreDiagnostic(savedPath, matchedPath, SelectedNode, restoreMode);
            return restoredSelection;
        }
        catch (Exception ex)
        {
            SelectedWorkspaceTabIndex = 0;
            var fallbackNode = HierarchyRoots.FirstOrDefault();
            if (fallbackNode is not null)
            {
                SelectedNode = fallbackNode;
            }

            _lastUiRestoreDiagnostic = $"[UI restore] mode=exception-fallback; error={ex.GetType().Name}: {ex.Message}";
            return fallbackNode is not null;
        }
    }

    private bool TryRestoreMapDesignerArea(ProjectUiState uiState)
    {
        AreaNodeViewModel? areaNode = null;

        if (uiState.MapDesignerAreaId.HasValue)
        {
            areaNode = EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<AreaNodeViewModel>()
                .FirstOrDefault(node => node.Area.Id == uiState.MapDesignerAreaId.Value);
        }

        if (areaNode is null)
        {
            areaNode = FindAreaNode(uiState.PlanetName, uiState.CountryName, uiState.AreaName);
        }

        if (areaNode is null)
        {
            return false;
        }

        ExpandToNode(areaNode);
        OpenAreaEditor(areaNode.Area);
        if (SelectedNode is null)
        {
            SelectedNode = areaNode;
        }

        return true;
    }

    private bool TryRestoreLastSelectedNode(ProjectUiState uiState, out string matchedCandidate)
    {
        matchedCandidate = string.Empty;
        var path = uiState.LastSelectedNodePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var restorePathCandidates = BuildRestorePathCandidates(path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        HierarchyNodeViewModel? target = null;
        foreach (var node in EnumerateHierarchyNodes(HierarchyRoots))
        {
            var currentPath = BuildNodePath(node);
            var normalizedCurrentPath = NormalizeNodePathForGroupingCompatibility(currentPath);
            var legacyPath = BuildLegacyNodePath(node);
            var matched = restorePathCandidates.FirstOrDefault(candidate =>
                string.Equals(candidate, currentPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, normalizedCurrentPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, legacyPath, StringComparison.OrdinalIgnoreCase));
            if (matched is null)
            {
                continue;
            }

            matchedCandidate = matched;
            target = node;
            break;
        }

        if (target is null)
        {
            return false;
        }

        ExpandToNode(target);
        if (target is RoomNodeViewModel roomNode)
        {
            OpenRoomEditor(roomNode.Room);
        }
        else if (target is AreaNodeViewModel areaNode)
        {
            OpenAreaEditor(areaNode.Area);
        }

        SelectedNode = target;
        return true;
    }

    private static string BuildUiRestoreDiagnostic(
        string savedPath,
        string matchedCandidate,
        HierarchyNodeViewModel? selectedNode,
        string mode)
    {
        var savedValue = string.IsNullOrWhiteSpace(savedPath) ? "(empty)" : savedPath;
        var matchedValue = string.IsNullOrWhiteSpace(matchedCandidate) ? "(none)" : matchedCandidate;
        var finalPath = selectedNode is null ? "(none)" : BuildNodePath(selectedNode);
        return $"[UI restore] mode={mode}; saved={savedValue}; matched={matchedValue}; final={finalPath}";
    }

    private static string NormalizeNodePathForGroupingCompatibility(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static segment => !segment.Equals("group:configuration", StringComparison.OrdinalIgnoreCase)
                                     && !segment.Equals("group:children", StringComparison.OrdinalIgnoreCase));
        return string.Join('/', segments);
    }

    private static string BuildNodePath(HierarchyNodeViewModel node)
    {
        var segments = GetAncestry(node)
            .AsEnumerable()
            .Reverse()
            .Select(GetNodePathSegment)
            .Where(static segment => !string.IsNullOrWhiteSpace(segment));
        return string.Join("/", segments);
    }

    private static string BuildLegacyNodePath(HierarchyNodeViewModel node)
    {
        var segments = GetAncestry(node)
            .AsEnumerable()
            .Reverse()
            .Select(GetLegacyNodePathSegment)
            .Where(static segment => !string.IsNullOrWhiteSpace(segment));
        return string.Join("/", segments);
    }

    private static IEnumerable<string> BuildRestorePathCandidates(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        var normalized = path.Trim();
        yield return normalized;
        yield return NormalizeNodePathForGroupingCompatibility(normalized);

        var reversed = ReverseNodePath(normalized);
        if (!string.Equals(reversed, normalized, StringComparison.OrdinalIgnoreCase))
        {
            yield return reversed;
            yield return NormalizeNodePathForGroupingCompatibility(reversed);
        }
    }

    private static string ReverseNodePath(string path)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse();
        return string.Join('/', segments);
    }

    private static string GetNodePathSegment(HierarchyNodeViewModel node)
    {
        return node switch
        {
            ProjectRootNodeViewModel => "project:global",
            PlanetNodeViewModel planetNode => $"planet:{planetNode.Planet.Id:N}",
            CountryNodeViewModel countryNode => $"country:{countryNode.Country.Id:N}",
            AreaNodeViewModel areaNode => $"area:{areaNode.Area.Id:N}",
            RoomNodeViewModel roomNode => $"room:{roomNode.Room.Id:N}",
            ConfigurationGroupNodeViewModel => "group:configuration",
            ChildrenGroupNodeViewModel => "group:children",
            RoomGameObjectsNodeViewModel objectsNode => $"room-objects:{objectsNode.RoomNode.Room.Id:N}",
            PlanetGameObjectsNodeViewModel objectsNode => $"planet-objects:{objectsNode.PlanetNode.Planet.Id:N}",
            CountryGameObjectsNodeViewModel objectsNode => $"country-objects:{objectsNode.CountryNode.Country.Id:N}",
            AreaGameObjectsNodeViewModel objectsNode => $"area-objects:{objectsNode.AreaNode.Area.Id:N}",
            RoomTraversalLegsNodeViewModel legsNode => $"room-traversal-legs:{legsNode.RoomNode.Room.Id:N}",
            TraversalLegNodeViewModel legNode => $"traversal-leg:{legNode.Connection.TraversalConnectionId:N}:{legNode.Direction}",
            GlobalObjectsNodeViewModel => "player:objects",
            ObjectTemplatesNodeViewModel => "templates:objects",
            RoomTemplatesNodeViewModel => "templates:rooms",
            PhaseBooksNodeViewModel => "phases:books",
            PhaseNodeViewModel phaseNode => $"phase:{phaseNode.PhaseNode.Id:N}",
            TemplateRoomNodeViewModel roomTemplateNode => $"template-room:{roomTemplateNode.Room.Id:N}",
            GameObjectNodeViewModel objectNode => $"object:{objectNode.GameObject.ObjectId:N}",
            GlobalObjectNodeViewModel objectNode => $"player-object:{objectNode.GameObject.ObjectId:N}",
            TemplateGameObjectNodeViewModel objectNode => $"template-object:{objectNode.GameObject.ObjectId:N}",
            GamePropertiesContainerNodeViewModel propertiesNode => $"properties:{propertiesNode.Scope}",
            GamePropertyNodeViewModel variableNode => $"property:{variableNode.Variable.Id:N}",
            ScopedActionsNodeViewModel scopedActionsNode => $"scoped-actions:{scopedActionsNode.Scope}",
            ScopedActionEntryNodeViewModel scopedActionEntryNode => $"scoped-action:{scopedActionEntryNode.Action.Id:N}",
            ScopedVerbsNodeViewModel scopedVerbsNode => $"scoped-verbs:{scopedVerbsNode.Scope}",
            ScopedDirectionalsNodeViewModel scopedDirectionalsNode => $"scoped-directionals:{scopedDirectionalsNode.Scope}",
            ScopedDirectionalEntryNodeViewModel directionalNode => $"scoped-directional:{SanitizePathSegment(directionalNode.Directional)}",
            ScopedSoundEffectsNodeViewModel scopedSoundEffectsNode => $"scoped-sound-effects:{scopedSoundEffectsNode.Scope}",
            ScopedEventSubscriptionsNodeViewModel scopedEventSubscriptionsNode => $"scoped-event-subscriptions:{scopedEventSubscriptionsNode.Scope}",
            ScopedEventSubscriptionEntryNodeViewModel subscriptionNode => $"scoped-event-subscription:{subscriptionNode.Entry.Id:N}",
            ScopedSoundEffectEntryNodeViewModel scopedSoundEffectEntryNode => $"scoped-sound-effect:{scopedSoundEffectEntryNode.Entry.SoundEffectId:N}",
            ScopedProceduresNodeViewModel scopedProceduresNode => $"scoped-procedures:{scopedProceduresNode.Scope}",
            ScopedProcedureEntryNodeViewModel procedureNode => $"scoped-procedure:{procedureNode.Procedure.Id:N}",
            ScopedTimerDefinitionsNodeViewModel timerDefinitionsNode => $"scoped-timers:{timerDefinitionsNode.Scope}",
            ScopedTimerDefinitionEntryNodeViewModel timerNode => $"scoped-timer:{SanitizePathSegment(timerNode.Entry.TimerKey)}",
            ScopedVerbEntryNodeViewModel verbNode => $"scoped-verb:{SanitizePathSegment(verbNode.Verb)}",
            GlobalSettingsNodeViewModel => "settings:global",
            PlanetSettingsNodeViewModel => "settings:planet",
            CountrySettingsNodeViewModel => "settings:country",
            AreaSettingsNodeViewModel => "settings:area",
            RoomSettingsNodeViewModel => "settings:room",
            GameObjectSettingsNodeViewModel => "settings:object",
            _ => node.GetType().Name
        };
    }

    private static string GetLegacyNodePathSegment(HierarchyNodeViewModel node)
    {
        return node switch
        {
            GameObjectNodeViewModel objectNode => $"object:{BuildSiblingScopedSegment(objectNode, objectNode.GameObject.ScopeName)}",
            GlobalObjectNodeViewModel objectNode => $"player-object:{BuildSiblingScopedSegment(objectNode, objectNode.GameObject.ScopeName)}",
            TemplateGameObjectNodeViewModel objectNode => $"template-object:{BuildSiblingScopedSegment(objectNode, objectNode.GameObject.ScopeName)}",
            PhaseBooksNodeViewModel or PhaseNodeViewModel or ScopedDirectionalEntryNodeViewModel
                or ScopedEventSubscriptionEntryNodeViewModel or ScopedProcedureEntryNodeViewModel
                or ScopedTimerDefinitionsNodeViewModel or ScopedTimerDefinitionEntryNodeViewModel
                or ScopedVerbEntryNodeViewModel => node.GetType().Name,
            _ => GetNodePathSegment(node)
        };
    }

    private static string BuildSiblingScopedSegment(HierarchyNodeViewModel node, string label)
    {
        var parent = node.Parent;
        if (parent is null)
        {
            return SanitizePathSegment(label);
        }

        var siblings = parent.Children
            .Where(child => child.GetType() == node.GetType())
            .ToList();
        var index = siblings.FindIndex(child => ReferenceEquals(child, node));
        return $"{SanitizePathSegment(label)}#{Math.Max(0, index)}";
    }

    private static string SanitizePathSegment(string value)
    {
        return (value ?? string.Empty).Trim().Replace("/", "//", StringComparison.Ordinal);
    }

    private AreaNodeViewModel? FindAreaNode(string planetName, string countryName, string areaName)
    {
        if (string.IsNullOrWhiteSpace(planetName) || string.IsNullOrWhiteSpace(countryName) || string.IsNullOrWhiteSpace(areaName))
        {
            return null;
        }

        return EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<PlanetNodeViewModel>()
            .Where(planet => string.Equals(planet.Planet.ScopeName, planetName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(planet => EnumerateHierarchyNodes(planet.Children).OfType<CountryNodeViewModel>())
            .Where(country => string.Equals(country.Country.ScopeName, countryName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(country => EnumerateHierarchyNodes(country.Children).OfType<AreaNodeViewModel>())
            .FirstOrDefault(area => string.Equals(area.Area.ScopeName, areaName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<HierarchyNodeViewModel> EnumerateHierarchyNodes(IEnumerable<HierarchyNodeViewModel> roots)
    {
        foreach (var root in roots)
        {
            yield return root;

            foreach (var child in EnumerateHierarchyNodes(root.Children))
            {
                yield return child;
            }
        }
    }

    private static void ExpandToNode(HierarchyNodeViewModel node)
    {
        HierarchyNodeViewModel? current = node;
        while (current is not null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    private void EnsureProjectHasStarterHierarchy()
    {
        if (_project.Planets.Count > 0)
        {
            return;
        }

        _project = CreateDefaultProjectModel(_project.Name);
        ScopeHierarchy.AttachParents(_project);
        RebuildProjectSoundEffectCategorySuggestions();
    }

    private void NormalizeStartHierarchySelections()
    {
        if (_project.Planets.Count == 0)
        {
            _project.StartingPlanetName = string.Empty;
            return;
        }

        var startingPlanet = _project.Planets.FirstOrDefault(planet => string.Equals(planet.ScopeName, _project.StartingPlanetName, StringComparison.OrdinalIgnoreCase))
                            ?? _project.Planets.First();
        _project.StartingPlanetName = startingPlanet.ScopeName;

        foreach (var planet in _project.Planets)
        {
            if (planet.Countries.Count == 0)
            {
                planet.StartingCountryName = string.Empty;
                continue;
            }

            var startingCountry = planet.Countries.FirstOrDefault(country => string.Equals(country.ScopeName, planet.StartingCountryName, StringComparison.OrdinalIgnoreCase))
                                  ?? planet.Countries.First();
            planet.StartingCountryName = startingCountry.ScopeName;

            foreach (var country in planet.Countries)
            {
                if (country.Areas.Count == 0)
                {
                    country.StartingAreaName = string.Empty;
                    continue;
                }

                var startingArea = country.Areas.FirstOrDefault(area => string.Equals(area.ScopeName, country.StartingAreaName, StringComparison.OrdinalIgnoreCase))
                                   ?? country.Areas.First();
                country.StartingAreaName = startingArea.ScopeName;

                foreach (var area in country.Areas)
                {
                    if (area.Rooms.Count == 0)
                    {
                        area.StartingRoomId = null;
                        continue;
                    }

                    if (!area.StartingRoomId.HasValue || area.Rooms.All(room => room.Id != area.StartingRoomId.Value))
                    {
                        area.StartingRoomId = area.Rooms[0].Id;
                    }
                }
            }
        }
    }

    private ProjectModel CreateDefaultProjectModel(string projectName)
    {
        var starterRoom = new Room
        {
            Name = "New Room",
            Description = "",
            Commands = new List<string>(),
            GameObjects = new List<GameObject>(),
            RoomImageCanvasWidth = 800,
            RoomImageCanvasHeight = 600,
            Images = CreateDefaultRoomImages()
        };

        var starterArea = new Area
        {
            Name = "New Area",
            StartingRoomId = starterRoom.Id,
            Variables = new List<GamePropertyDefinition>(),
            Rooms = new List<Room> { starterRoom },
            Links = new List<RoomLink>()
        };

        var starterCountry = new Country
        {
            Name = "New Country",
            StartingAreaName = starterArea.ScopeName,
            Variables = new List<GamePropertyDefinition>(),
            Areas = new List<Area> { starterArea }
        };

        var starterPlanet = new Planet
        {
            Name = "New Planet",
            StartingCountryName = starterCountry.ScopeName,
            Variables = new List<GamePropertyDefinition>(),
            Countries = new List<Country> { starterCountry }
        };

        var project = new ProjectModel
        {
            Name = projectName,
            CommandVerbs = new List<string>(),
            Directionals = new List<string>(),
            StartingPlanetName = starterPlanet.ScopeName,
            GlobalVariables = new List<GamePropertyDefinition>(),
            GameObjects = new List<GameObject>
            {
                new()
                {
                    Name = "Player",
                    ProducerNotes = string.Empty,
                    IsInventoriable = true,
                    IsOpenable = false,
                    Description = string.Empty,
                    Commands = new List<string>(),
                    Variables = new List<GamePropertyDefinition>()
                }
            },
            ObjectTemplates = CreateDefaultObjectTemplates(),
            Planets = new List<Planet> { starterPlanet }
        };

        project.GlobalScope.Name = "Global Objects";
        project.GlobalScope.GameProperties = new List<GamePropertyDefinition>();
        project.GlobalScope.AvailableActions = new List<CommandAction>();
        return project;
    }

    private static List<GameObject> CreateDefaultObjectTemplates()
    {
        var inventoriableTemplate = new GameObject
        {
            Name = "Inventoriable Basics",
            ProducerNotes = string.Empty,
            IsInventoriable = true,
            Description = "Template with pickup/drop baseline actions.",
            Commands = new List<string>(),
            Variables = new List<GamePropertyDefinition>(),
            AvailableActions = new List<CommandAction>
            {
                new()
                {
                    Name = "pickup",
                    ActionType = CommandActionType.PutObjectInContainer,
                    Verbs = new List<string> { "pickup" },
                    OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase),
                    OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Success"] = "Picked up.",
                        ["Failure"] = "Cannot pick that up."
                    }
                },
                new()
                {
                    Name = "drop",
                    ActionType = CommandActionType.RemoveObjectFromContainer,
                    Verbs = new List<string> { "drop" },
                    OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase),
                    OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Success"] = "Dropped.",
                        ["Failure"] = "Cannot drop that right now."
                    }
                }
            }
        };

        inventoriableTemplate.ApplyFeatureVariableContract();
        return new List<GameObject> { inventoriableTemplate };
    }

    private void UpdateProjectSummary()
    {
        var firstPlanet = _project.Planets.FirstOrDefault()?.ScopeName ?? "No Planet";
        ProjectSummary = _projectFilePath is null
            ? $"Project: {_project.Name} | Planet: {firstPlanet}"
            : $"Project: {_project.Name} | Planet: {firstPlanet} ({_projectFilePath})";

        WindowTitle = _projectFilePath is null
            ? "Storyboard Designer - No Project Loaded"
            : $"Storyboard Designer - {_project.Name}";
    }

    private void CommitAllNodeEdits()
    {
        foreach (var root in HierarchyRoots)
        {
            CommitRecursive(root);
        }
    }

    private static void CommitRecursive(HierarchyNodeViewModel node)
    {
        node.CommitEditableName();
        foreach (var child in node.Children)
        {
            CommitRecursive(child);
        }
    }

    private void RebuildRoomImageSlots()
    {
        RoomImageSlots.Clear();

        if (SelectedRoom is null)
        {
            return;
        }

        EnsureRoomImageEntries(SelectedRoom);

        foreach (var entry in SelectedRoom.Images.OrderBy(e => Array.IndexOf(AllImageSlots, e.Slot)))
        {
            RoomImageSlots.Add(new RoomDesignerImageSlotViewModel(entry, MarkProjectDirty, () => _projectFilePath, preferredSourceBucket: "rooms"));
        }
    }

    private void EnsureProjectCommandCatalogs()
    {
        SyncProjectCommandCatalogsFromModel();
    }

    private static GlobalNodeLoadDiagnostics DetectGlobalNodeLoadDiagnostics(string projectFilePath)
    {
        try
        {
            var globalNodeFilePath = BuildProjectGlobalNodeFilePath(projectFilePath);
            var globalNodeFileExists = File.Exists(globalNodeFilePath);
            var globalNodeMalformed = false;
            var globalNodePresentFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var stream = File.OpenRead(projectFilePath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new GlobalNodeLoadDiagnostics(
                    GlobalNodeFileMissing: !globalNodeFileExists,
                    UsedRootFallbackFields: false,
                    GlobalNodeAndRootBothContainGlobalOwnedFields: false,
                    GlobalNodeMalformed: false);
            }

            if (globalNodeFileExists)
            {
                try
                {
                    using var globalNodeStream = File.OpenRead(globalNodeFilePath);
                    using var globalNodeDoc = JsonDocument.Parse(globalNodeStream);
                    if (globalNodeDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        globalNodePresentFields = GetPresentGlobalOwnedFieldsFromGlobalNode(globalNodeDoc.RootElement);
                    }
                    else
                    {
                        globalNodeMalformed = true;
                    }
                }
                catch
                {
                    globalNodeMalformed = true;
                }
            }

            var rootPresentFields = GetPresentGlobalOwnedFieldsFromRoot(doc.RootElement);
            var usedRootFallback = !globalNodeMalformed
                && rootPresentFields.Except(globalNodePresentFields, StringComparer.OrdinalIgnoreCase).Any();
            var hasPotentialConflict = !globalNodeMalformed
                && rootPresentFields.Intersect(globalNodePresentFields, StringComparer.OrdinalIgnoreCase).Any();

            return new GlobalNodeLoadDiagnostics(
                GlobalNodeFileMissing: !globalNodeFileExists,
                UsedRootFallbackFields: usedRootFallback,
                GlobalNodeAndRootBothContainGlobalOwnedFields: hasPotentialConflict,
                GlobalNodeMalformed: globalNodeMalformed);
        }
        catch
        {
            return new GlobalNodeLoadDiagnostics(
                GlobalNodeFileMissing: false,
                UsedRootFallbackFields: false,
                GlobalNodeAndRootBothContainGlobalOwnedFields: false,
                GlobalNodeMalformed: false);
        }
    }

    private static HashSet<string> GetPresentGlobalOwnedFieldsFromRoot(JsonElement root)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddFieldIfPresent(root, fields, "commandVerbs", "commandVerbs");
        AddFieldIfPresent(root, fields, "directionals", "directionals");
        AddFieldIfPresent(root, fields, "directionalTraversalMappings", "directionalTraversalMappings");
        AddFieldIfPresent(root, fields, "availableGameActions", "globalAvailableGameActions");
        AddFieldIfPresent(root, fields, "gameObjects", "gameObjects");
        AddFieldIfPresent(root, fields, "objectTemplates", "objectTemplates");
        AddFieldIfPresent(root, fields, "roomTemplates", "roomTemplates");
        AddFieldIfPresent(root, fields, "baseObjects", "baseObjects");
        AddFieldIfPresent(root, fields, "gameProperties", "gameProperties");
        AddFieldIfPresent(root, fields, "procedureIds", "procedureIds");
        AddFieldIfPresent(root, fields, "sharedVariables", "sharedVariables");
        AddFieldIfPresent(root, fields, "planetIds", "planetIds");

        return fields;
    }

    private static HashSet<string> GetPresentGlobalOwnedFieldsFromGlobalNode(JsonElement globalNodeRoot)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddFieldIfPresent(globalNodeRoot, fields, "commandVerbs", "commandVerbs");
        AddFieldIfPresent(globalNodeRoot, fields, "additionalVerbs", "commandVerbs");
        AddFieldIfPresent(globalNodeRoot, fields, "directionals", "directionals");
        AddFieldIfPresent(globalNodeRoot, fields, "additionalDirectionals", "directionals");
        AddFieldIfPresent(globalNodeRoot, fields, "directionalTraversalMappings", "directionalTraversalMappings");
        AddFieldIfPresent(globalNodeRoot, fields, "additionalDirectionalTraversalMappings", "directionalTraversalMappings");
        AddFieldIfPresent(globalNodeRoot, fields, "globalAvailableGameActions", "globalAvailableGameActions");
        AddFieldIfPresent(globalNodeRoot, fields, "availableGameActions", "globalAvailableGameActions");
        AddFieldIfPresent(globalNodeRoot, fields, "gameProperties", "gameProperties");
        AddFieldIfPresent(globalNodeRoot, fields, "procedureIds", "procedureIds");
        AddFieldIfPresent(globalNodeRoot, fields, "sharedVariables", "sharedVariables");
        AddFieldIfPresent(globalNodeRoot, fields, "planetIds", "planetIds");
        AddFieldIfPresent(globalNodeRoot, fields, "gameObjectIds", "gameObjects");
        AddFieldIfPresent(globalNodeRoot, fields, "gameObjects", "gameObjects");
        AddFieldIfPresent(globalNodeRoot, fields, "objectTemplateIds", "objectTemplates");
        AddFieldIfPresent(globalNodeRoot, fields, "objectTemplates", "objectTemplates");
        AddFieldIfPresent(globalNodeRoot, fields, "roomTemplateIds", "roomTemplates");
        AddFieldIfPresent(globalNodeRoot, fields, "roomTemplates", "roomTemplates");
        AddFieldIfPresent(globalNodeRoot, fields, "baseObjectIds", "baseObjects");
        AddFieldIfPresent(globalNodeRoot, fields, "baseObjects", "baseObjects");

        return fields;
    }

    private static void AddFieldIfPresent(JsonElement root, HashSet<string> fields, string propertyName, string canonicalFieldName)
    {
        if (HasNonNullProperty(root, propertyName))
        {
            fields.Add(canonicalFieldName);
        }
    }

    private static bool HasNonNullProperty(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind != JsonValueKind.Null;
            }
        }

        return false;
    }

    private static string BuildProjectGlobalNodeFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.globals.json");
    }

    private readonly record struct GlobalNodeLoadDiagnostics(
        bool GlobalNodeFileMissing,
        bool UsedRootFallbackFields,
        bool GlobalNodeAndRootBothContainGlobalOwnedFields,
        bool GlobalNodeMalformed);

    private void SyncProjectCommandCatalogsFromModel()
    {
        ProjectCommandVerbs.Clear();
        foreach (var verb in _project.CommandVerbs.Select(NormalizeCatalogValue)
                     .Where(static value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ProjectCommandVerbs.Add(verb);
        }

        ProjectDirectionals.Clear();
        foreach (var qualifier in _project.Directionals.Select(NormalizeCatalogValue)
                     .Where(static value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ProjectDirectionals.Add(qualifier);
        }

        RefreshProjectQualifierOptions();

        SyncProjectCommandCatalogsToModel();
    }

    private void RefreshProjectQualifierOptions()
    {
        ProjectCommandQualifierOptions.Clear();
        ProjectCommandQualifierOptions.Add(string.Empty);

        foreach (var qualifier in ProjectDirectionals)
        {
            ProjectCommandQualifierOptions.Add(qualifier);
        }
    }

    private void SyncProjectCommandCatalogsToModel()
    {
        SyncCatalogListInPlace(_project.CommandVerbs, ProjectCommandVerbs);
        SyncCatalogListInPlace(_project.Directionals, ProjectDirectionals);
    }

    private static void SyncCatalogListInPlace(IList<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var value in source)
        {
            target.Add(value);
        }
    }

    private static string NormalizeCatalogValue(string value)
    {
        return value.Trim();
    }

    private static void EnsureRoomImageEntries(Room room)
    {
        room.Images ??= new List<RoomImageEntry>();

        foreach (var slot in AllImageSlots)
        {
            if (room.Images.Any(i => i.Slot == slot))
            {
                continue;
            }

            room.Images.Add(new RoomImageEntry
            {
                Slot = slot,
                OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
                Image = new RoomImageVariant()
            });
        }

        foreach (var image in room.Images)
        {
            if (image.OverlayRenderOrder == 0 && image.Slot != RoomImageSlot.Down)
            {
                image.OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(image.Slot);
            }
        }
    }

    private static List<RoomImageEntry> CreateDefaultRoomImages()
    {
        return AllImageSlots.Select(slot => new RoomImageEntry
        {
            Slot = slot,
            OverlayRenderOrder = RoomImageEntry.GetDefaultOverlayRenderOrder(slot),
            Image = new RoomImageVariant()
        }).ToList();
    }

    private static string GetDefaultFullPath(Room room)
    {
        return room.Images.FirstOrDefault(i => i.Slot == RoomImageSlot.Default)?.Image.FullImagePath ?? string.Empty;
    }

    private void LoadRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var path in _recentProjectsService.Load().Take(5))
        {
            RecentProjects.Add(path);
        }
    }

    private void RegisterRecentProject(string projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(projectFilePath);
        var existing = RecentProjects.FirstOrDefault(p =>
            string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            RecentProjects.Remove(existing);
        }

        RecentProjects.Insert(0, normalizedPath);
        while (RecentProjects.Count > 5)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }

        _recentProjectsService.Save(RecentProjects);
    }

    private static List<HierarchyNodeViewModel> BuildHierarchy(ProjectModel project)
    {
        var nodes = new List<HierarchyNodeViewModel>();
        var projectRootNode = new ProjectRootNodeViewModel(project);

        var globalVariablesNode = new GamePropertiesContainerNodeViewModel(PropertyResolutionScope.Global, projectRootNode);
        foreach (var variable in project.GlobalVariables)
        {
            globalVariablesNode.Children.Add(new GamePropertyNodeViewModel(variable, PropertyResolutionScope.Global, globalVariablesNode));
        }

        projectRootNode.Children.Add(CreateGlobalSettingsNode(projectRootNode));
        projectRootNode.Children.Add(globalVariablesNode);
        projectRootNode.Children.Add(CreateActionsNode(projectRootNode, PropertyResolutionScope.Global, project.GlobalScope.AvailableActions));
        projectRootNode.Children.Add(CreateVerbsNode(projectRootNode, PropertyResolutionScope.Global, project.CommandVerbs));
        projectRootNode.Children.Add(CreateDirectionalsNode(projectRootNode, PropertyResolutionScope.Global, project.Directionals));
        projectRootNode.Children.Add(CreateSoundEffectsNode(projectRootNode, PropertyResolutionScope.Global, project.GlobalScope.SoundEffectLibraryEntries));
        projectRootNode.Children.Add(CreateEventSubscriptionsNode(projectRootNode, PropertyResolutionScope.Global, project.GlobalScope.EventSubscriptions));
        projectRootNode.Children.Add(CreateTimerDefinitionsNode(projectRootNode, PropertyResolutionScope.Global, project.GlobalScope.TimerDefinitions));
        projectRootNode.Children.Add(CreateProceduresNode(projectRootNode, PropertyResolutionScope.Global, project.ProcedureIds));

        var playerNode = new GlobalObjectsNodeViewModel(project, projectRootNode);
        foreach (var objectNode in CreateGlobalGameObjectNodes(playerNode))
        {
            playerNode.Children.Add(objectNode);
        }
        projectRootNode.Children.Add(playerNode);

        var templatesNode = new ObjectTemplatesNodeViewModel(project, projectRootNode);
        foreach (var objectNode in CreateTemplateObjectNodes(templatesNode))
        {
            templatesNode.Children.Add(objectNode);
        }
        projectRootNode.Children.Add(templatesNode);

        var roomTemplatesNode = new RoomTemplatesNodeViewModel(project, projectRootNode);
        foreach (var roomNode in CreateTemplateRoomNodes(roomTemplatesNode))
        {
            roomTemplatesNode.Children.Add(roomNode);
        }
        projectRootNode.Children.Add(roomTemplatesNode);

        var baseObjectsNode = new ObjectTemplatesNodeViewModel(project, projectRootNode, isBaseCatalog: true);
        foreach (var objectNode in CreateTemplateObjectNodes(baseObjectsNode))
        {
            baseObjectsNode.Children.Add(objectNode);
        }
        projectRootNode.Children.Add(baseObjectsNode);
        projectRootNode.Children.Add(CreatePhaseBooksNode(projectRootNode));

        foreach (var planet in project.Planets)
        {
            var planetNode = new PlanetNodeViewModel(project, planet, projectRootNode);
            var planetChildrenGroup = new ChildrenGroupNodeViewModel(planetNode);
            planetNode.Children.Add(CreatePlanetSettingsNode(planetNode));
            planetNode.Children.Add(CreateVariablesNode(planetNode, planet.Variables, PropertyResolutionScope.Planet));
            planetNode.Children.Add(CreateActionsNode(planetNode, PropertyResolutionScope.Planet, planet.AvailableActions));
            planetNode.Children.Add(CreateVerbsNode(planetNode, PropertyResolutionScope.Planet, planet.AdditionalVerbs));
            planetNode.Children.Add(CreateDirectionalsNode(planetNode, PropertyResolutionScope.Planet, planet.AdditionalDirectionals));
            planetNode.Children.Add(CreateSoundEffectsNode(planetNode, PropertyResolutionScope.Planet, planet.SoundEffectLibraryEntries));
            planetNode.Children.Add(CreateEventSubscriptionsNode(planetNode, PropertyResolutionScope.Planet, planet.EventSubscriptions));
            planetNode.Children.Add(CreateTimerDefinitionsNode(planetNode, PropertyResolutionScope.Planet, planet.TimerDefinitions));
            planetNode.Children.Add(planetChildrenGroup);

            var planetBaseObjectsNode = new ObjectTemplatesNodeViewModel(
                project,
                planetNode,
                "Base Objects",
                planet.BaseObjects,
                planet.BaseObjectsIgnoredValidationRuleIds,
                isBaseCatalog: true,
                catalogParentScope: planet);
            foreach (var objectNode in CreateTemplateObjectNodes(planetBaseObjectsNode))
            {
                planetBaseObjectsNode.Children.Add(objectNode);
            }
            planetNode.Children.Add(planetBaseObjectsNode);
            planetNode.Children.Add(CreateObjectsNode(planetNode));

            foreach (var country in planet.Countries)
            {
                var countryNode = new CountryNodeViewModel(country, planetNode);
                var countryChildrenGroup = new ChildrenGroupNodeViewModel(countryNode);
                countryNode.Children.Add(CreateCountrySettingsNode(countryNode));
                countryNode.Children.Add(CreateVariablesNode(countryNode, country.Variables, PropertyResolutionScope.Country));
                countryNode.Children.Add(CreateActionsNode(countryNode, PropertyResolutionScope.Country, country.AvailableActions));
                countryNode.Children.Add(CreateVerbsNode(countryNode, PropertyResolutionScope.Country, country.AdditionalVerbs));
                countryNode.Children.Add(CreateDirectionalsNode(countryNode, PropertyResolutionScope.Country, country.AdditionalDirectionals));
                countryNode.Children.Add(CreateSoundEffectsNode(countryNode, PropertyResolutionScope.Country, country.SoundEffectLibraryEntries));
                countryNode.Children.Add(CreateEventSubscriptionsNode(countryNode, PropertyResolutionScope.Country, country.EventSubscriptions));
                countryNode.Children.Add(CreateTimerDefinitionsNode(countryNode, PropertyResolutionScope.Country, country.TimerDefinitions));
                countryNode.Children.Add(countryChildrenGroup);

                var countryBaseObjectsNode = new ObjectTemplatesNodeViewModel(
                    project,
                    countryNode,
                    "Base Objects",
                    country.BaseObjects,
                    country.BaseObjectsIgnoredValidationRuleIds,
                    isBaseCatalog: true,
                    catalogParentScope: country);
                foreach (var objectNode in CreateTemplateObjectNodes(countryBaseObjectsNode))
                {
                    countryBaseObjectsNode.Children.Add(objectNode);
                }
                countryNode.Children.Add(countryBaseObjectsNode);
                countryNode.Children.Add(CreateObjectsNode(countryNode));

                foreach (var area in country.Areas)
                {
                    var areaNode = new AreaNodeViewModel(area, countryNode);
                    var areaChildrenGroup = new ChildrenGroupNodeViewModel(areaNode);
                    areaNode.Children.Add(CreateAreaSettingsNode(areaNode));
                    areaNode.Children.Add(CreateVariablesNode(areaNode, area.Variables, PropertyResolutionScope.Area));
                    areaNode.Children.Add(CreateActionsNode(areaNode, PropertyResolutionScope.Area, area.AvailableActions));
                    areaNode.Children.Add(CreateVerbsNode(areaNode, PropertyResolutionScope.Area, area.AdditionalVerbs));
                    areaNode.Children.Add(CreateDirectionalsNode(areaNode, PropertyResolutionScope.Area, area.AdditionalDirectionals));
                    areaNode.Children.Add(CreateSoundEffectsNode(areaNode, PropertyResolutionScope.Area, area.SoundEffectLibraryEntries));
                    areaNode.Children.Add(CreateEventSubscriptionsNode(areaNode, PropertyResolutionScope.Area, area.EventSubscriptions));
                    areaNode.Children.Add(CreateTimerDefinitionsNode(areaNode, PropertyResolutionScope.Area, area.TimerDefinitions));
                    areaNode.Children.Add(areaChildrenGroup);

                    var areaBaseObjectsNode = new ObjectTemplatesNodeViewModel(
                        project,
                        areaNode,
                        "Base Objects",
                        area.BaseObjects,
                        area.BaseObjectsIgnoredValidationRuleIds,
                        isBaseCatalog: true,
                        catalogParentScope: area);
                    foreach (var objectNode in CreateTemplateObjectNodes(areaBaseObjectsNode))
                    {
                        areaBaseObjectsNode.Children.Add(objectNode);
                    }
                    areaNode.Children.Add(areaBaseObjectsNode);
                    areaNode.Children.Add(CreateObjectsNode(areaNode));

                    var areaRoomLookup = area.Rooms.ToDictionary(room => room.Id, room => room.ScopeName);

                    foreach (var room in area.Rooms)
                    {
                        var roomNode = new RoomNodeViewModel(room, area, areaNode);
                        roomNode.Children.Add(CreateRoomSettingsNode(roomNode));
                        roomNode.Children.Add(CreateVariablesNode(roomNode, room.Variables, PropertyResolutionScope.Room));
                        roomNode.Children.Add(CreateActionsNode(roomNode, PropertyResolutionScope.Room, room.AvailableActions));
                        roomNode.Children.Add(CreateVerbsNode(roomNode, PropertyResolutionScope.Room, room.AdditionalVerbs));
                        roomNode.Children.Add(CreateDirectionalsNode(roomNode, PropertyResolutionScope.Room, room.AdditionalDirectionals));
                        roomNode.Children.Add(CreateSoundEffectsNode(roomNode, PropertyResolutionScope.Room, room.SoundEffectLibraryEntries));
                        roomNode.Children.Add(CreateEventSubscriptionsNode(roomNode, PropertyResolutionScope.Room, room.EventSubscriptions));
                        roomNode.Children.Add(CreateTimerDefinitionsNode(roomNode, PropertyResolutionScope.Room, room.TimerDefinitions));

                        var traversalLegsNode = new RoomTraversalLegsNodeViewModel(roomNode);
                        var traversalLegs = area.TraversalConnections
                            .Where(connection => connection.RoomAId == room.Id || connection.RoomBId == room.Id)
                            .Select(connection =>
                            {
                                var isFromRoomA = connection.RoomAId == room.Id;
                                var destinationRoomId = isFromRoomA ? connection.RoomBId : connection.RoomAId;
                                var direction = isFromRoomA
                                    ? connection.BaseTraversalDirectionFromA
                                    : TraversalWizardApplyUtility.OppositeDirection(connection.BaseTraversalDirectionFromA);

                                var destinationRoomName = areaRoomLookup.TryGetValue(destinationRoomId, out var name)
                                    ? name
                                    : "Unknown Room";

                                return new TraversalLegNodeViewModel(
                                    connection,
                                    roomNode,
                                    isFromRoomA,
                                    direction,
                                    destinationRoomId,
                                    destinationRoomName,
                                    traversalLegsNode);
                            })
                            .OrderBy(leg => leg.Direction switch
                            {
                                Direction10.North => 0,
                                Direction10.NorthEast => 1,
                                Direction10.East => 2,
                                Direction10.SouthEast => 3,
                                Direction10.South => 4,
                                Direction10.SouthWest => 5,
                                Direction10.West => 6,
                                Direction10.NorthWest => 7,
                                Direction10.Up => 8,
                                Direction10.Down => 9,
                                _ => int.MaxValue
                            })
                            .ThenBy(leg => leg.DestinationRoomName, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(leg => leg.Connection.TraversalConnectionId)
                            .ToList();

                        foreach (var leg in traversalLegs)
                        {
                            leg.Children.Add(CreateActionsNode(leg, PropertyResolutionScope.Room, leg.LegState.AvailableActions));
                            leg.Children.Add(CreateVariablesNode(leg, leg.LegState.Variables, PropertyResolutionScope.Room));
                            traversalLegsNode.Children.Add(leg);
                        }

                        roomNode.Children.Add(traversalLegsNode);
                        roomNode.Children.Add(CreateObjectsNode(roomNode));
                        ApplyHideEmptyConfiguration(roomNode);
                        areaChildrenGroup.Children.Add(roomNode);
                    }

                    ApplyHideEmptyConfiguration(areaNode);

                    countryChildrenGroup.Children.Add(areaNode);
                }

                ApplyHideEmptyConfiguration(countryNode);

                planetChildrenGroup.Children.Add(countryNode);
            }

            ApplyHideEmptyConfiguration(planetNode);

            projectRootNode.Children.Add(planetNode);
        }

        ApplyHideEmptyConfiguration(projectRootNode);

        nodes.Add(projectRootNode);

        return nodes;
    }

    private static Dictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        void AddObject(GameObject obj)
        {
            lookup.TryAdd(obj.ObjectId, obj);
        }

        foreach (var obj in EnumerateGameObjectsRecursive(project.GlobalScope.GameObjects))
        {
            AddObject(obj);
        }

        foreach (var obj in EnumerateGameObjectsRecursive(project.ObjectTemplates))
        {
            AddObject(obj);
        }

        foreach (var obj in EnumerateGameObjectsRecursive(project.BaseObjects))
        {
            AddObject(obj);
        }

        foreach (var obj in project.Planets
                     .SelectMany(planet => EnumerateGameObjectsRecursive(planet.BaseObjects)))
        {
            AddObject(obj);
        }

        foreach (var obj in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => EnumerateGameObjectsRecursive(country.BaseObjects)))
        {
            AddObject(obj);
        }

        foreach (var obj in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => EnumerateGameObjectsRecursive(area.BaseObjects)))
        {
            AddObject(obj);
        }

        foreach (var obj in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => EnumerateGameObjectsRecursive(room.GameObjects)))
        {
            AddObject(obj);
        }

        return lookup;
    }

    private static List<HierarchyNodeViewModel> GetAncestry(HierarchyNodeViewModel node)
    {
        var nodes = new List<HierarchyNodeViewModel>();
        HierarchyNodeViewModel? current = node;

        while (current is not null)
        {
            nodes.Add(current);
            current = current.Parent;
        }

        return nodes;
    }

    private (Planet planet, Country country, Area area)? ResolveRoomHierarchy(Room room)
    {
        foreach (var planet in _project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    if (area.Rooms.Any(existing => existing.Id == room.Id))
                    {
                        return (planet, country, area);
                    }
                }
            }
        }

        return null;
    }

    private (Planet planet, Country country)? ResolveAreaHierarchy(Area area)
    {
        foreach (var planet in _project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                if (country.Areas.Any(existing => ReferenceEquals(existing, area) || existing.ScopeName == area.ScopeName))
                {
                    return (planet, country);
                }
            }
        }

        return null;
    }

}







