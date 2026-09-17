using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.Commands;
using Storyboard.Shared.GameServices.RuntimeContext;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class RoomActionEditorDialog : Window
{
    private const string EchoPropertyChooserEntry = "Property Chooser...";
    private const string PlaySoundEffectDefaultResultCodeToken = "Success";

    private sealed class ForwardingModeOption
    {
        public required ChildCommandForwardingMode Mode { get; init; }
        public required string Label { get; init; }
    }

    private sealed class SimilarDispatchModeOption
    {
        public required SimilarChildDispatchMode Mode { get; init; }
        public required string Label { get; init; }
    }

    private sealed class CompositeObjectDisplayItem
    {
        public required string Name { get; init; }
        public required string GuidText { get; init; }
    }

    private sealed class BreakCompositeTargetChoiceItem
    {
        public Guid? TargetObjectId { get; init; }
        public required string DisplayName { get; init; }
    }

    private sealed class TimerOwnerTypeChoice
    {
        public TimerOwnerType? OwnerType { get; init; }
        public required string Label { get; init; }
    }

    private enum OutcomeSoundScopeFilterOption
    {
        CurrentScope,
        UpScope,
        DownScope,
        AllScopes
    }

    private sealed class OutcomeSoundCueDisplayEntry
    {
        public int Index { get; init; }
        public Guid SoundEffectId { get; init; }
        public SoundEffectLane Lane { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string ScopeCategoryText { get; init; } = string.Empty;
        public string LaneText { get; init; } = string.Empty;
        public string HintText { get; init; } = string.Empty;
        public string EnabledText { get; init; } = string.Empty;
    }

    private readonly CommandAction _sourceAction;
    private readonly CommandAction _workingCopy;
    private readonly IReadOnlyList<string> _echoReferenceTokens;
    private readonly IReadOnlyList<CompositeRecipeChoiceItem> _compositeRecipeChoices;
    private readonly IReadOnlyList<BreakCompositeTargetChoiceItem> _breakCompositeTargetChoices;
    private readonly Dictionary<Guid, string> _compositeObjectNamesById;
    private readonly IReadOnlyList<MaterializeSourceObjectChoiceItem> _materializeSourceObjectChoices;
    private readonly Dictionary<Guid, MaterializeSourceObjectChoiceItem> _materializeSourceObjectChoicesById;
    private readonly IReadOnlyList<ProcedureChoiceItem> _procedureChoices;
    private readonly Dictionary<Guid, ProcedureChoiceItem> _procedureChoicesById;
    private readonly IReadOnlyList<SoundEffectChoiceItem> _soundEffectChoices;
    private readonly Dictionary<Guid, SoundEffectChoiceItem> _soundEffectChoicesById;
    private readonly IReadOnlyList<string> _timerKeySuggestions;
    private readonly IReadOnlyList<string> _variableReferenceTokens;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _variableChoices;
    private readonly Dictionary<string, GamePropertyChoiceItem> _variableChoiceMap;
    private readonly IReadOnlyList<CommandAction> _availableActions;
    private readonly Dictionary<Guid, CommandAction> _availableActionsById;
    private readonly PropertyResolutionScope _variableScope;
    private readonly PresentationEffectsCatalogService _presentationEffectsCatalogService;
    private readonly IReadOnlyList<ForwardingModeOption> _forwardingModeOptions;
    private readonly IReadOnlyList<SimilarDispatchModeOption> _similarDispatchModeOptions;
    private readonly ActionEchoReferenceTokenProviderRegistry _actionEchoTokenProviderRegistry;
    private readonly ObservableCollection<ActionEchoEditorEntry> _outcomeEchoEntries;
    private readonly ObservableCollection<OutcomeSoundCueDisplayEntry> _outcomeSoundCueEntries;
    private ICollectionView? _outcomeSoundChoicesView;
    private static readonly IReadOnlyList<string> BuiltInFunctions =
    [
        "@ALL(condition1, condition2)",
        "@ANY(condition1, condition2)",
        "@NOT(condition)",
        "@hasItem(\"itemName\")"
    ];
    private int? _echoCompletionStart;
    private int? _echoReturnCompletionStart;
    private int? _variableCompletionStart;
    private int? _echoFunctionCompletionStart;
    private int? _variableFunctionCompletionStart;
    private bool _isInitializingCompositeRecipeSelection;
    private static readonly Regex NumericPartialPattern = new(@"^-?(\d+)?(\.\d*)?$", RegexOptions.Compiled);
    private static readonly Regex QuantitySuffixPattern = new(@"^(?<name>.+?)\s+x(?<qty>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly IReadOnlyList<string> EchoReturnSuggestions =
    [
        "RETURN SUCCESS",
        "RETURN SUCCESS BUBBLE",
        "RETURN FAILURE",
        "RETURN FAILURE BUBBLE"
    ];
    private static readonly IReadOnlyList<string> MoveByPointsTargetResolutionIntentChoices =
    [
        "PrimarySelection",
        "ObjectAtFirstPoint",
        "Player"
    ];
    public RoomActionEditorDialog(
        CommandAction sourceAction,
        PropertyResolutionScope variableScope,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        IReadOnlyList<string> echoReferenceTokens,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem>? compositeRecipeChoices = null,
        IReadOnlyList<SoundEffectChoiceItem>? soundEffectChoices = null,
        IReadOnlyList<string>? timerKeySuggestions = null,
        IReadOnlyList<CommandAction>? availableActions = null)
    {
        InitializeComponent();
        _sourceAction = sourceAction;
        _presentationEffectsCatalogService = new PresentationEffectsCatalogService();
        _variableScope = variableScope;
        _variableChoices = variableChoices;
        _echoReferenceTokens = OrderReferenceTokensPreferSelf(echoReferenceTokens);
        _compositeRecipeChoices = (compositeRecipeChoices ?? Array.Empty<CompositeRecipeChoiceItem>())
            .Where(choice => choice.RecipeId != Guid.Empty)
            .ToList();
        _breakCompositeTargetChoices = BuildBreakCompositeTargetChoices(_compositeRecipeChoices);
        _compositeObjectNamesById = BuildCompositeObjectNameMap(_compositeRecipeChoices);
        _materializeSourceObjectChoices = (materializeSourceObjectChoices ?? Array.Empty<MaterializeSourceObjectChoiceItem>())
            .Where(static choice => choice.ObjectId != Guid.Empty)
            .GroupBy(static choice => choice.ObjectId)
            .Select(static group => group.First())
            .ToList();
        _materializeSourceObjectChoicesById = _materializeSourceObjectChoices
            .ToDictionary(choice => choice.ObjectId, choice => choice);
        _procedureChoices = (procedureChoices ?? Array.Empty<ProcedureChoiceItem>())
            .Where(static choice => choice.ProcedureId != Guid.Empty)
            .GroupBy(static choice => choice.ProcedureId)
            .Select(static group => group.First())
            .ToList();
        _procedureChoicesById = _procedureChoices
            .ToDictionary(choice => choice.ProcedureId, choice => choice);
        _soundEffectChoices = (soundEffectChoices ?? Array.Empty<SoundEffectChoiceItem>())
            .Where(static choice => choice.SoundEffectId != Guid.Empty)
            .OrderBy(static choice => choice.ScopeRelation)
            .ThenBy(static choice => choice.SourceCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _soundEffectChoicesById = _soundEffectChoices
            .GroupBy(static choice => choice.SoundEffectId)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static choice => choice.ScopeRelation)
                    .ThenBy(static choice => choice.SourceCategory, StringComparer.OrdinalIgnoreCase)
                    .First());
        _timerKeySuggestions = (timerKeySuggestions ?? Array.Empty<string>())
            .Select(static key => key?.Trim() ?? string.Empty)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _variableReferenceTokens = OrderReferenceTokensPreferSelf(variableChoices.Select(choice => choice.Value));
        _variableChoiceMap = variableChoices
            .GroupBy(choice => choice.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _actionEchoTokenProviderRegistry = ActionEchoReferenceTokenProviderRegistry.CreateDefault();
        _availableActions = (availableActions ?? Array.Empty<CommandAction>()).ToList();
        _availableActionsById = _availableActions.ToDictionary(action => action.Id, action => action);
        var sourceEchoMessage = ActionPayloadAccessors.GetEchoMessage(sourceAction);
        var sourceCheckPropertyName = ActionPayloadAccessors.GetCheckPropertyName(sourceAction);
        var sourceCheckExpectedValue = ActionPayloadAccessors.GetCheckExpectedValue(sourceAction);
        var sourceSetPropertyName = ActionPayloadAccessors.GetSetPropertyName(sourceAction);
        var sourceSetPropertyValue = ActionPayloadAccessors.GetSetPropertyValue(sourceAction);
        var sourceContainerTransfer = ActionPayloadAccessors.GetContainerTransfer(sourceAction);
        var sourceMovePayload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(sourceAction);
        var sourceMoveByPointsPayload = ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(sourceAction);
        var sourceRotatePayload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(sourceAction);
        var sourceStackPayload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(sourceAction);
        var sourceSetActivePayload = ActionPayloadAccessors.GetSetActiveRoomObjectPayload(sourceAction);
        var sourceSelectByPointPayload = ActionPayloadAccessors.GetSelectRoomObjectByPointPayload(sourceAction);
        var sourceClearActivePayload = ActionPayloadAccessors.GetClearActiveRoomObjectsPayload(sourceAction);
        var sourceClearSelectionsPayload = ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(sourceAction);
        var sourceSynonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(sourceAction);
        var sourceMaterializeSourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(sourceAction);
        var sourceProcedureId = ActionPayloadAccessors.GetProcedureId(sourceAction);
        var sourceStartTimerKey = ActionPayloadAccessors.GetStartTimerKey(sourceAction);
        var sourceStartTimerOwnerScopeKindOverride = ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(sourceAction);
        var sourceCancelTimerKey = ActionPayloadAccessors.GetCancelTimerKey(sourceAction);
        var sourceCancelTimerScopeQualifierKind = ActionPayloadAccessors.GetCancelTimerScopeQualifierKind(sourceAction);
        var sourceCancelTimerScopeQualifierId = ActionPayloadAccessors.GetCancelTimerScopeQualifierId(sourceAction);
        var sourceCompositeTargetObjectId = sourceAction.CompositeTargetObjectId;
        var sourceCompositeRecipeId = sourceAction.CompositeRecipeId;
        var sourceCompositeRequiredPartObjectIds = sourceAction.CompositeRequiredPartObjectIds.ToList();
        var sourceCompositeStrictPartCountEnforcement = sourceAction.CompositeStrictPartCountEnforcement;
        var sourceCompositeMinimumRequiredPartCount = sourceAction.CompositeMinimumRequiredPartCount;
        var sourceCompositeMatchMode = sourceAction.CompositeMatchMode;
        var sourceCompositeAmbiguityPolicy = sourceAction.CompositeAmbiguityPolicy;
        var sourceCompositePartConsumptionMode = sourceAction.CompositePartConsumptionMode;
        var sourceCompositeResolvedTargetOutputTemplate = sourceAction.CompositeResolvedTargetOutputTemplate;

        if (sourceAction.ActionType == CommandActionType.BuildCompositeByTarget)
        {
            var payload = ActionPayloadAccessors.GetCompositeByTargetPayload(sourceAction);
            sourceCompositeTargetObjectId = payload.CompositeTargetObjectId;
            sourceCompositeRecipeId = payload.CompositeRecipeId;
            sourceCompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            sourceCompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            sourceCompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            sourceCompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }
        else if (sourceAction.ActionType == CommandActionType.BuildCompositeByParts)
        {
            var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(sourceAction);
            sourceCompositeTargetObjectId = payload.CompositeTargetObjectId;
            sourceCompositeRecipeId = payload.CompositeRecipeId;
            sourceCompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            sourceCompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            sourceCompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            sourceCompositeMatchMode = payload.CompositeMatchMode;
            sourceCompositeAmbiguityPolicy = payload.CompositeAmbiguityPolicy;
            sourceCompositePartConsumptionMode = payload.CompositePartConsumptionMode;
            sourceCompositeResolvedTargetOutputTemplate = payload.CompositeResolvedTargetOutputTemplate;
        }
        else if (sourceAction.ActionType == CommandActionType.BreakCompositeItem)
        {
            var payload = ActionPayloadAccessors.GetBreakCompositePayload(sourceAction);
            sourceCompositeTargetObjectId = payload.CompositeTargetObjectId;
            sourceCompositeRecipeId = payload.CompositeRecipeId;
            sourceCompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            sourceCompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            sourceCompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            sourceCompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }

        _workingCopy = new CommandAction
        {
            Id = sourceAction.Id,
            Name = sourceAction.Name,
            ActionType = sourceAction.ActionType == CommandActionType.SetFlag ? CommandActionType.SetGameProperty : sourceAction.ActionType,
            FlagName = sourceCheckPropertyName,
            FlagValue = sourceCheckExpectedValue,
            GamePropertyName = sourceAction.ActionType == CommandActionType.SetFlag ? sourceCheckPropertyName : sourceSetPropertyName,
            GamePropertyValue = sourceAction.ActionType == CommandActionType.SetFlag ? sourceCheckExpectedValue.ToString().ToLowerInvariant() : sourceSetPropertyValue,
            TargetContainerId = sourceContainerTransfer.TargetContainerId,
            MoveDirectionToken = sourceMovePayload.DirectionToken,
            MoveDistanceInCells = sourceMovePayload.DistanceInCells,
            MoveAllowPartialMove = sourceAction.ActionType == CommandActionType.MoveRoomObjectByPoints
                ? sourceMoveByPointsPayload.AllowPartialMove
                : sourceMovePayload.AllowPartialMove,
            MoveAllowJumpOver = sourceAction.ActionType == CommandActionType.MoveRoomObjectByPoints
                ? sourceMoveByPointsPayload.AllowJumpOver
                : sourceMovePayload.AllowJumpOver,
            MoveVisualTransitionHint = sourceAction.ActionType == CommandActionType.MoveRoomObjectByPoints
                ? sourceMoveByPointsPayload.VisualTransitionHint
                : sourceMovePayload.VisualTransitionHint,
            MoveTravelVisualizationMode = sourceAction.ActionType == CommandActionType.MoveRoomObjectByPoints
                ? sourceMoveByPointsPayload.TravelVisualizationMode
                : sourceMovePayload.TravelVisualizationMode,
            RotateMode = sourceRotatePayload.Mode,
            RotateTurnDegrees = sourceRotatePayload.TurnDegrees,
            RotateFacingDirectionToken = sourceRotatePayload.FacingDirectionToken,
            RotateVisualTransitionHint = sourceRotatePayload.VisualTransitionHint,
            StackVisualTransitionHint = sourceStackPayload.VisualTransitionHint,
            SelectionCueEffectKey = sourceAction.ActionType == CommandActionType.SelectRoomObjectByPoint
                ? sourceSelectByPointPayload.SelectionCueEffectKey
                : sourceSetActivePayload.SelectionCueEffectKey,
            ClearActiveRoomObjectsScope = sourceClearActivePayload.ClearScope,
            SynonymTargetActionId = sourceSynonymTargetActionId,
            MaterializeSourceObjectId = sourceMaterializeSourceObjectId,
            ProcedureId = sourceProcedureId,
            CompositeTargetObjectId = sourceCompositeTargetObjectId,
            CompositeRecipeId = sourceCompositeRecipeId,
            CompositeRequiredPartObjectIds = sourceCompositeRequiredPartObjectIds,
            CompositeStrictPartCountEnforcement = sourceCompositeStrictPartCountEnforcement,
            CompositeMinimumRequiredPartCount = sourceCompositeMinimumRequiredPartCount,
            CompositeMatchMode = sourceCompositeMatchMode,
            CompositeAmbiguityPolicy = sourceCompositeAmbiguityPolicy,
            CompositePartConsumptionMode = sourceCompositePartConsumptionMode,
            CompositeResolvedTargetOutputTemplate = sourceCompositeResolvedTargetOutputTemplate,
            ChildCommandForwardingMode = sourceAction.ChildCommandForwardingMode,
            SimilarChildDispatchMode = sourceAction.SimilarChildDispatchMode,
            OutcomeMessageMap = sourceAction.OutcomeMessageMap,
            OutcomeSoundEffectsMap = CommandAction.CloneOutcomeSoundEffectsMap(sourceAction.OutcomeSoundEffectsMap)
        };

        _workingCopy.Payload = sourceAction.CreatePayloadSnapshot();

        ActionPayloadAccessors.SetEchoMessage(_workingCopy, ActionPayloadAccessors.GetEchoMessage(sourceAction));
        ActionPayloadAccessors.SetCheckGameProperty(
            _workingCopy,
            ActionPayloadAccessors.GetCheckPropertyName(sourceAction),
            ActionPayloadAccessors.GetCheckExpectedValue(sourceAction));
        ActionPayloadAccessors.SetSetGameProperty(
            _workingCopy,
            sourceAction.ActionType == CommandActionType.SetFlag
                ? sourceCheckPropertyName
                : sourceSetPropertyName,
            sourceAction.ActionType == CommandActionType.SetFlag
                ? sourceCheckExpectedValue.ToString().ToLowerInvariant()
                : sourceSetPropertyValue);
        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(
            _workingCopy,
            sourceMovePayload.DirectionToken,
            sourceMovePayload.DistanceInCells,
            sourceMovePayload.AllowPartialMove,
            sourceMovePayload.VisualTransitionHint,
            sourceMovePayload.AllowJumpOver,
            sourceMovePayload.TravelVisualizationMode);
        ActionPayloadAccessors.SetMoveRoomObjectByPoints(
            _workingCopy,
            sourceMoveByPointsPayload.TargetResolutionIntent,
            sourceMoveByPointsPayload.AllowPartialMove,
            sourceMoveByPointsPayload.AllowJumpOver,
            sourceMoveByPointsPayload.VisualTransitionHint,
            sourceMoveByPointsPayload.TravelVisualizationMode);
        ActionPayloadAccessors.SetRotateRoomObjectOnGrid(
            _workingCopy,
            sourceRotatePayload.Mode,
            sourceRotatePayload.TurnDegrees,
            sourceRotatePayload.FacingDirectionToken,
            sourceRotatePayload.VisualTransitionHint);
        ActionPayloadAccessors.SetStackRoomObjectOnAnother(
            _workingCopy,
            sourceStackPayload.VisualTransitionHint);
        ActionPayloadAccessors.SetSetActiveRoomObject(
            _workingCopy,
            sourceSetActivePayload.SelectionCueEffectKey);
        ActionPayloadAccessors.SetSelectRoomObjectByPoint(
            _workingCopy,
            sourceSelectByPointPayload.SelectionCueEffectKey);
        ActionPayloadAccessors.SetClearActiveRoomObjects(
            _workingCopy,
            sourceClearActivePayload.ClearScope);
        ActionPayloadAccessors.SetClearRoomObjectSelections(
            _workingCopy,
            sourceClearSelectionsPayload.ClearScope);
        ActionPayloadAccessors.SetStartTimer(
            _workingCopy,
            sourceStartTimerKey,
            sourceStartTimerOwnerScopeKindOverride);
        ActionPayloadAccessors.SetCancelTimer(
            _workingCopy,
            sourceCancelTimerKey,
            sourceCancelTimerScopeQualifierKind,
            sourceCancelTimerScopeQualifierId);

        if (_workingCopy.ActionType == CommandActionType.SetGameProperty && string.IsNullOrWhiteSpace(_workingCopy.GamePropertyName) && !string.IsNullOrWhiteSpace(_workingCopy.FlagName))
        {
            _workingCopy.GamePropertyName = _workingCopy.FlagName;
        }

        EchoReferenceListBox.ItemsSource = GetEchoReferenceTokensForCurrentAction();
        VariableReferenceListBox.ItemsSource = _variableReferenceTokens;
        VariableValueBooleanComboBox.ItemsSource = new[] { "true", "false" };
        MoveDirectionOverrideComboBox.ItemsSource = BuildMoveDirectionChoices();
        var configuredMovementHints = _presentationEffectsCatalogService.GetConfiguredMovementHints();
        MoveVisualTransitionHintComboBox.ItemsSource = configuredMovementHints;
        MoveTravelVisualizationModeComboBox.ItemsSource = Enum.GetValues<RuntimeMovementTravelVisualizationMode>();
        MoveByPointsTargetResolutionIntentComboBox.ItemsSource = MoveByPointsTargetResolutionIntentChoices;
        RotateModeComboBox.ItemsSource = Enum.GetValues<RuntimeRotateRoomObjectOnGridAttemptMode>();
        RotateFacingDirectionComboBox.ItemsSource = BuildRotateFacingDirectionChoices();
        RotateVisualTransitionHintComboBox.ItemsSource = configuredMovementHints;
        StackVisualTransitionHintComboBox.ItemsSource = configuredMovementHints;
        SetActiveSelectionCueEffectKeyComboBox.ItemsSource = BuildSelectionCueEffectKeyChoices();
        SelectByPointSelectionCueEffectKeyComboBox.ItemsSource = BuildSelectionCueEffectKeyChoices();
        ClearActiveScopeComboBox.ItemsSource = Enum.GetValues<RuntimeClearActiveRoomObjectsScope>();
        MoveByPointsVisualTransitionHintComboBox.ItemsSource = configuredMovementHints;
        MoveByPointsTravelVisualizationModeComboBox.ItemsSource = Enum.GetValues<RuntimeMovementTravelVisualizationMode>();
        ClearRoomObjectSelectionsScopeComboBox.ItemsSource = Enum.GetValues<ClearRoomObjectSelectionsScope>();
        LoadMoveByPointsEditorInputs();
        LoadSelectByPointEditorInputs();
        LoadClearRoomObjectSelectionsEditorInputs();
        StartTimerOwnerScopeKindOverrideComboBox.ItemsSource = BuildTimerOwnerTypeChoices(includeBlank: true);
        StartTimerOwnerScopeKindOverrideComboBox.DisplayMemberPath = nameof(TimerOwnerTypeChoice.Label);
        StartTimerKeyComboBox.ItemsSource = _timerKeySuggestions;
        CancelTimerScopeQualifierKindComboBox.ItemsSource = BuildTimerOwnerTypeChoices(includeBlank: true);
        CancelTimerScopeQualifierKindComboBox.DisplayMemberPath = nameof(TimerOwnerTypeChoice.Label);
        CancelTimerKeyComboBox.ItemsSource = _timerKeySuggestions;
        EchoFunctionListBox.ItemsSource = BuiltInFunctions;
        VariableFunctionListBox.ItemsSource = BuiltInFunctions;
        _outcomeEchoEntries = new ObservableCollection<ActionEchoEditorEntry>();
        OutcomeEchoEntriesListView.ItemsSource = _outcomeEchoEntries;
        _outcomeSoundCueEntries = new ObservableCollection<OutcomeSoundCueDisplayEntry>();
        OutcomeSoundCueEntriesListView.ItemsSource = _outcomeSoundCueEntries;
        _outcomeSoundChoicesView = CollectionViewSource.GetDefaultView(_soundEffectChoices);
        if (_outcomeSoundChoicesView is not null)
        {
            _outcomeSoundChoicesView.Filter = OutcomeSoundChoiceMatchesFilter;
        }

        OutcomeSoundChoicesListView.ItemsSource = _outcomeSoundChoicesView;
        OutcomeSoundScopeFilterComboBox.ItemsSource = Enum.GetValues<OutcomeSoundScopeFilterOption>();
        OutcomeSoundScopeFilterComboBox.SelectedItem = OutcomeSoundScopeFilterOption.AllScopes;
        OutcomeSoundCueLaneTextBlock.Text = "sfx";
        _forwardingModeOptions =
        [
            new ForwardingModeOption
            {
                Mode = ChildCommandForwardingMode.ChildrenAfterParent,
                Label = "Run children after parent action"
            },
            new ForwardingModeOption
            {
                Mode = ChildCommandForwardingMode.ChildrenBeforeParent,
                Label = "Run children before parent action"
            }
        ];
        _similarDispatchModeOptions =
        [
            new SimilarDispatchModeOption
            {
                Mode = SimilarChildDispatchMode.SingleMatchingChild,
                Label = "Dispatch to first similar child"
            },
            new SimilarDispatchModeOption
            {
                Mode = SimilarChildDispatchMode.AllMatchingChildren,
                Label = "Dispatch to all similar children"
            }
        ];

        ForwardingModeComboBox.ItemsSource = _forwardingModeOptions;
        ForwardingModeComboBox.DisplayMemberPath = nameof(ForwardingModeOption.Label);
        SimilarChildDispatchModeComboBox.ItemsSource = _similarDispatchModeOptions;
        SimilarChildDispatchModeComboBox.DisplayMemberPath = nameof(SimilarDispatchModeOption.Label);
        var selectedMode = _workingCopy.ChildCommandForwardingMode == ChildCommandForwardingMode.None
            ? ChildCommandForwardingMode.ChildrenAfterParent
            : _workingCopy.ChildCommandForwardingMode;
        ForwardingModeComboBox.SelectedItem = _forwardingModeOptions.First(option => option.Mode == selectedMode);
        SimilarChildDispatchModeComboBox.SelectedItem = _similarDispatchModeOptions.First(option => option.Mode == _workingCopy.SimilarChildDispatchMode);
        ForwardToChildrenCheckBox.IsChecked = _workingCopy.ChildCommandForwardingMode != ChildCommandForwardingMode.None;
        ForwardingModeComboBox.IsEnabled = ForwardToChildrenCheckBox.IsChecked == true;
        SimilarChildDispatchModeComboBox.IsEnabled = ForwardToChildrenCheckBox.IsChecked == true;

        System.Windows.DataObject.AddPastingHandler(VariableValueEditorTextBox, VariableValueEditorTextBox_OnPasting);

        _workingCopy.PropertyChanged += WorkingCopy_OnPropertyChanged;
        DataContext = _workingCopy;
        CompositeTargetObjectIdTextBox.Text = _workingCopy.CompositeTargetObjectId?.ToString() ?? string.Empty;
        CompositeRequiredPartIdsTextBox.Text = string.Join(Environment.NewLine, _workingCopy.CompositeRequiredPartObjectIds.Select(id => id.ToString()));
        RefreshCompositeTargetFriendlyDisplay();
        CompositePartsTargetObjectIdTextBox.Text = _workingCopy.CompositeTargetObjectId?.ToString() ?? string.Empty;
        CompositePartsRequiredPartIdsTextBox.Text = string.Join(Environment.NewLine, _workingCopy.CompositeRequiredPartObjectIds.Select(id => id.ToString()));
        RefreshCompositePartsFriendlyDisplay();
        _isInitializingCompositeRecipeSelection = true;
        CompositeRecipeComboBox.ItemsSource = _compositeRecipeChoices;
        CompositeRecipeComboBox.DisplayMemberPath = nameof(CompositeRecipeChoiceItem.DisplayName);
        if (_workingCopy.CompositeRecipeId.HasValue)
        {
            var matchedRecipe = _compositeRecipeChoices.FirstOrDefault(choice => choice.RecipeId == _workingCopy.CompositeRecipeId.Value);
            if (matchedRecipe is not null)
            {
                CompositeRecipeComboBox.SelectedItem = matchedRecipe;
            }
        }
        if (CompositeRecipeComboBox.SelectedItem is null && _compositeRecipeChoices.Count > 0)
        {
            CompositeRecipeComboBox.SelectedIndex = 0;
        }
        _isInitializingCompositeRecipeSelection = false;
        SelectComboBoxString(CompositeMatchModeComboBox, string.IsNullOrWhiteSpace(_workingCopy.CompositeMatchMode) ? "ExactPartSet" : _workingCopy.CompositeMatchMode);
        SelectComboBoxString(CompositeAmbiguityPolicyComboBox, string.IsNullOrWhiteSpace(_workingCopy.CompositeAmbiguityPolicy) ? "FailWithHint" : _workingCopy.CompositeAmbiguityPolicy);
        SelectComboBoxString(CompositePartConsumptionModeComboBox, string.IsNullOrWhiteSpace(_workingCopy.CompositePartConsumptionMode) ? "ContainedInComposite" : _workingCopy.CompositePartConsumptionMode);
        CompositeStrictPartMentionsCheckBox.IsChecked = _workingCopy.CompositeStrictPartCountEnforcement == true;
        CompositeMinimumRequiredCountTextBox.Text = _workingCopy.CompositeMinimumRequiredPartCount?.ToString(CultureInfo.InvariantCulture) ?? "1";
        RefreshCompositeMinimumCountInputState();
        BreakCompositeTargetComboBox.ItemsSource = _breakCompositeTargetChoices;
        BreakCompositeTargetComboBox.DisplayMemberPath = nameof(BreakCompositeTargetChoiceItem.DisplayName);
        BreakCompositeTargetComboBox.SelectedItem = _breakCompositeTargetChoices.FirstOrDefault(choice => choice.TargetObjectId == _workingCopy.CompositeTargetObjectId)
            ?? _breakCompositeTargetChoices.FirstOrDefault(choice => !choice.TargetObjectId.HasValue);
        RefreshOutcomeEchoEntries();
        RefreshOutcomeSoundResultCodeChoices();
        RefreshOutcomeSoundChoiceFilter();
        RefreshOutcomeSoundEditorStates();
        RefreshRotateInputState();
        RefreshVisibleEditorPanel();
    }

    private static void SelectComboBoxString(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private void CompositeMatchModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshCompositeMinimumCountInputState();
    }

    private void RefreshCompositeMinimumCountInputState()
    {
        var isMinimumCountMode = string.Equals(GetComboBoxString(CompositeMatchModeComboBox, "ExactPartSet"), "MinimumCount", StringComparison.OrdinalIgnoreCase);
        CompositeMinimumRequiredCountTextBox.IsEnabled = isMinimumCountMode;
    }

    private void WorkingCopy_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandAction.ActionType))
        {
            // Reinitialize result-code scripts for the newly selected action type.
            _workingCopy.NormalizeOutcomeMapForActionType(_workingCopy.ActionType, preserveUnsupportedTokens: false);
            RefreshVisibleEditorPanel();
            return;
        }

        if (e.PropertyName == nameof(CommandAction.GamePropertyName) || e.PropertyName == nameof(CommandAction.GamePropertyValue))
        {
            RefreshVariableValueEditorMode();
            return;
        }

        if (e.PropertyName == nameof(CommandAction.RotateMode))
        {
            RefreshRotateInputState();
            return;
        }

        if (e.PropertyName == nameof(CommandAction.OutcomeMessageMap))
        {
            RefreshOutcomeEchoEntries();
            RefreshOutcomeSoundResultCodeChoices();
            return;
        }

        if (e.PropertyName == nameof(CommandAction.OutcomeSoundEffectsMap))
        {
            RefreshOutcomeSoundCueEntries();
        }
    }

    private void RefreshVisibleEditorPanel()
    {
        EchoTextEditorPanel.Visibility = Visibility.Collapsed;
        FlagEditorPanel.Visibility = Visibility.Collapsed;
        VariableEditorPanel.Visibility = Visibility.Collapsed;
        PutObjectInContainerEditorPanel.Visibility = Visibility.Collapsed;
        MoveRoomObjectOnGridEditorPanel.Visibility = Visibility.Collapsed;
        MoveRoomObjectByPointsEditorPanel.Visibility = Visibility.Collapsed;
        RotateRoomObjectOnGridEditorPanel.Visibility = Visibility.Collapsed;
        StackRoomObjectOnAnotherEditorPanel.Visibility = Visibility.Collapsed;
        SetActiveRoomObjectEditorPanel.Visibility = Visibility.Collapsed;
        SelectRoomObjectByPointEditorPanel.Visibility = Visibility.Collapsed;
        ClearActiveRoomObjectsEditorPanel.Visibility = Visibility.Collapsed;
        ClearRoomObjectSelectionsEditorPanel.Visibility = Visibility.Collapsed;
        StartTimerEditorPanel.Visibility = Visibility.Collapsed;
        CancelTimerEditorPanel.Visibility = Visibility.Collapsed;
        SynonymEditorPanel.Visibility = Visibility.Collapsed;
        MaterializeObjectCopyEditorPanel.Visibility = Visibility.Collapsed;
        InvokeProcedureEditorPanel.Visibility = Visibility.Collapsed;
        BuildCompositeByTargetEditorPanel.Visibility = Visibility.Collapsed;
        BuildCompositeByPartsEditorPanel.Visibility = Visibility.Collapsed;
        BreakCompositeEditorPanel.Visibility = Visibility.Collapsed;
        PlaySoundEffectEditorPanel.Visibility = Visibility.Collapsed;
        NotImplementedPanel.Visibility = Visibility.Collapsed;

        switch (_workingCopy.ActionType)
        {
            case CommandActionType.EchoMessage:
                EchoTextEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.CheckGameProperty:
                FlagEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.SetGameProperty:
                VariableEditorPanel.Visibility = Visibility.Visible;
                RefreshVariableValueEditorMode();
                break;
            case CommandActionType.PutObjectInContainer:
            case CommandActionType.RemoveObjectFromContainer:
                PutObjectInContainerEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.MoveRoomObjectOnGrid:
                MoveRoomObjectOnGridEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.MoveRoomObjectByPoints:
                LoadMoveByPointsEditorInputs();
                MoveRoomObjectByPointsEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.RotateRoomObjectOnGrid:
                RotateRoomObjectOnGridEditorPanel.Visibility = Visibility.Visible;
                RefreshRotateInputState();
                break;
            case CommandActionType.StackRoomObjectOnAnother:
                StackRoomObjectOnAnotherEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.SetActiveRoomObject:
                SetActiveRoomObjectEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.SelectRoomObjectByPoint:
                LoadSelectByPointEditorInputs();
                SelectRoomObjectByPointEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.ClearActiveRoomObjects:
                ClearActiveRoomObjectsEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.ClearRoomObjectSelections:
                LoadClearRoomObjectSelectionsEditorInputs();
                ClearRoomObjectSelectionsEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.StartTimer:
                StartTimerEditorPanel.Visibility = Visibility.Visible;
                RefreshStartTimerDisplay();
                break;
            case CommandActionType.CancelTimer:
                CancelTimerEditorPanel.Visibility = Visibility.Visible;
                RefreshCancelTimerDisplay();
                break;
            case CommandActionType.OpenObject:
            case CommandActionType.CloseObject:
            case CommandActionType.UnlockObject:
            case CommandActionType.LockObject:
            case CommandActionType.NavigateDirection:
            case CommandActionType.NavigateToAdjacent:
            case CommandActionType.ManageNarrativePhaseAmbientSounds:
                break;
            case CommandActionType.Synonym:
                SynonymEditorPanel.Visibility = Visibility.Visible;
                RefreshSynonymTargetDisplay();
                break;
            case CommandActionType.MaterializeObjectCopy:
                MaterializeObjectCopyEditorPanel.Visibility = Visibility.Visible;
                RefreshMaterializeSourceObjectDisplay();
                break;
            case CommandActionType.InvokeProcedure:
                InvokeProcedureEditorPanel.Visibility = Visibility.Visible;
                RefreshInvokeProcedureDisplay();
                break;
            case CommandActionType.BuildCompositeByTarget:
                BuildCompositeByTargetEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.BuildCompositeByParts:
                BuildCompositeByPartsEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.BreakCompositeItem:
                BreakCompositeEditorPanel.Visibility = Visibility.Visible;
                break;
            case CommandActionType.PlaySoundEffect:
                PlaySoundEffectEditorPanel.Visibility = Visibility.Visible;
                RefreshPlaySoundEffectDisplay();
                break;
            default:
                NotImplementedPanel.Visibility = Visibility.Visible;
                break;
        }

        var hasVisibleActionSpecificPanel =
            EchoTextEditorPanel.Visibility == Visibility.Visible
            || FlagEditorPanel.Visibility == Visibility.Visible
            || VariableEditorPanel.Visibility == Visibility.Visible
            || PutObjectInContainerEditorPanel.Visibility == Visibility.Visible
            || MoveRoomObjectOnGridEditorPanel.Visibility == Visibility.Visible
            || MoveRoomObjectByPointsEditorPanel.Visibility == Visibility.Visible
            || RotateRoomObjectOnGridEditorPanel.Visibility == Visibility.Visible
            || StackRoomObjectOnAnotherEditorPanel.Visibility == Visibility.Visible
            || SetActiveRoomObjectEditorPanel.Visibility == Visibility.Visible
            || SelectRoomObjectByPointEditorPanel.Visibility == Visibility.Visible
            || ClearActiveRoomObjectsEditorPanel.Visibility == Visibility.Visible
            || ClearRoomObjectSelectionsEditorPanel.Visibility == Visibility.Visible
            || StartTimerEditorPanel.Visibility == Visibility.Visible
            || CancelTimerEditorPanel.Visibility == Visibility.Visible
            || SynonymEditorPanel.Visibility == Visibility.Visible
            || MaterializeObjectCopyEditorPanel.Visibility == Visibility.Visible
            || InvokeProcedureEditorPanel.Visibility == Visibility.Visible
            || BuildCompositeByTargetEditorPanel.Visibility == Visibility.Visible
            || BuildCompositeByPartsEditorPanel.Visibility == Visibility.Visible
            || BreakCompositeEditorPanel.Visibility == Visibility.Visible
            || PlaySoundEffectEditorPanel.Visibility == Visibility.Visible
            || NotImplementedPanel.Visibility == Visibility.Visible;

        ActionSpecificEditorBorder.Visibility = hasVisibleActionSpecificPanel
            ? Visibility.Visible
            : Visibility.Collapsed;

        EchoReferenceListBox.ItemsSource = GetEchoReferenceTokensForCurrentAction();
        RefreshOutcomeEchoEntries();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        PersistOutcomeEchoEntriesToWorkingCopy();

        _sourceAction.Name = _workingCopy.Name.Trim();
        var isCheckGamePropertyAction = ActionPayloadSchemaHelpers.OwnsAllFields(
            _workingCopy.ActionType,
            nameof(CommandAction.FlagName),
            nameof(CommandAction.FlagValue));
        var isSetGamePropertyAction = ActionPayloadSchemaHelpers.OwnsAllFields(
            _workingCopy.ActionType,
            nameof(CommandAction.GamePropertyName),
            nameof(CommandAction.GamePropertyValue));
        var isEchoMessageAction = ActionPayloadSchemaHelpers.OwnsField(_workingCopy.ActionType, nameof(CommandAction.EchoMessage));

        var checkPropertyName = ActionPayloadAccessors.GetCheckPropertyName(_workingCopy);
        if (isCheckGamePropertyAction
            && string.IsNullOrWhiteSpace(checkPropertyName))
        {
            System.Windows.MessageBox.Show(this, "Select a defined flag variable before saving.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (isCheckGamePropertyAction
            && !IsFlagTargetValid(checkPropertyName))
        {
            return;
        }

        if (isSetGamePropertyAction && string.IsNullOrWhiteSpace(_workingCopy.GamePropertyName))
        {
            System.Windows.MessageBox.Show(this, "Select a defined game property before saving.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (isEchoMessageAction && !ValidateScriptOrShowErrors(ActionPayloadAccessors.GetEchoMessage(_workingCopy), "Echo message"))
        {
            return;
        }

        var successOutcomeScript = GetOutcomeMessage(_workingCopy, "Success");
        if (!string.IsNullOrWhiteSpace(successOutcomeScript)
            && !ValidateScriptOrShowErrors(successOutcomeScript, "Success echo message", GetEchoReferenceTokensForCurrentAction()))
        {
            return;
        }

        var failureOutcomeScript = GetOutcomeMessage(_workingCopy, "Failure");
        if (!string.IsNullOrWhiteSpace(failureOutcomeScript)
            && !ValidateScriptOrShowErrors(failureOutcomeScript, "Failure echo message", GetEchoReferenceTokensForCurrentAction()))
        {
            return;
        }

        var setPropertyName = ActionPayloadAccessors.GetSetPropertyName(_workingCopy);
        var setPropertyValue = ActionPayloadAccessors.GetSetPropertyValue(_workingCopy);

        if (isSetGamePropertyAction && !ValidateScriptOrShowErrors(setPropertyValue, "Game property value"))
        {
            return;
        }

        if (isSetGamePropertyAction
            && !IsSetVariableValueAllowed(setPropertyName, setPropertyValue))
        {
            return;
        }

        var containerTransfer = ActionPayloadAccessors.GetContainerTransfer(_workingCopy);

        if ((_workingCopy.ActionType == CommandActionType.PutObjectInContainer
            || _workingCopy.ActionType == CommandActionType.RemoveObjectFromContainer)
            && !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "Success"), "Put in container success message", GetEchoReferenceTokensForCurrentAction()))
        {
            return;
        }

        if ((_workingCopy.ActionType == CommandActionType.PutObjectInContainer
            || _workingCopy.ActionType == CommandActionType.RemoveObjectFromContainer)
            && !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "Failure"), "Put in container failure message", GetEchoReferenceTokensForCurrentAction()))
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.MoveRoomObjectOnGrid
            && !TryApplyMoveRoomObjectOnGridInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.MoveRoomObjectByPoints
            && !TryApplyMoveRoomObjectByPointsInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.RotateRoomObjectOnGrid
            && !TryApplyRotateRoomObjectOnGridInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.StackRoomObjectOnAnother
            && !TryApplyStackRoomObjectOnAnotherInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.SelectRoomObjectByPoint
            && !TryApplySelectRoomObjectByPointInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.ClearRoomObjectSelections
            && !TryApplyClearRoomObjectSelectionsInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.NavigateDirection
            || _workingCopy.ActionType == CommandActionType.NavigateToAdjacent)
        {
            if (!ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "Moved"), "Navigate moved echo", GetEchoReferenceTokensForCurrentAction())
                || !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "DirectionRequired"), "Navigate direction required echo", GetEchoReferenceTokensForCurrentAction())
                || !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "NoExitInDirection"), "Navigate no-exit-in-direction echo", GetEchoReferenceTokensForCurrentAction())
                || !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "NoTraversableExit"), "Navigate no-traversable-exit echo", GetEchoReferenceTokensForCurrentAction())
                || !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "AmbiguousTraversal"), "Navigate ambiguous-traversal echo", GetEchoReferenceTokensForCurrentAction())
                || !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "TraversalBlocked"), "Navigate traversal-blocked echo", GetEchoReferenceTokensForCurrentAction())
                || !ValidateScriptOrShowErrors(GetOutcomeMessage(_workingCopy, "MoveFailed"), "Navigate move-failed echo", GetEchoReferenceTokensForCurrentAction()))
            {
                return;
            }
        }

        if (_workingCopy.ActionType == CommandActionType.Synonym
            && !ValidateSynonymTarget())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.MaterializeObjectCopy
            && !ValidateMaterializeSourceObject())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.InvokeProcedure
            && !ValidateInvokeProcedureSelection())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.StartTimer
            && !TryApplyStartTimerInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.CancelTimer
            && !TryApplyCancelTimerInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.BuildCompositeByTarget
            && !TryApplyBuildCompositeByTargetInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.BuildCompositeByParts
            && !TryApplyBuildCompositeByPartsInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.BreakCompositeItem
            && !TryApplyBreakCompositeInputs())
        {
            return;
        }

        if (_workingCopy.ActionType == CommandActionType.PlaySoundEffect
            && !TryApplyPlaySoundEffectInputs())
        {
            return;
        }

        _sourceAction.ActionType = _workingCopy.ActionType;
        var workingMoveByPointsPayload = ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(_workingCopy);
        var workingSelectByPointPayload = ActionPayloadAccessors.GetSelectRoomObjectByPointPayload(_workingCopy);
        ActionPayloadAccessors.SetEchoMessage(_sourceAction, ActionPayloadAccessors.GetEchoMessage(_workingCopy));
        ActionPayloadAccessors.SetCheckGameProperty(_sourceAction, _workingCopy.FlagName, _workingCopy.FlagValue);
        ActionPayloadAccessors.SetSetGameProperty(_sourceAction, setPropertyName, setPropertyValue);
        var workingContainerTransfer = ActionPayloadAccessors.GetContainerTransfer(_workingCopy);
        _sourceAction.TargetContainerId = workingContainerTransfer.TargetContainerId;
        ActionPayloadAccessors.SetMoveRoomObjectOnGrid(
            _sourceAction,
            _workingCopy.MoveDirectionToken,
            _workingCopy.MoveDistanceInCells,
            _workingCopy.MoveAllowPartialMove,
            _workingCopy.MoveVisualTransitionHint,
            _workingCopy.MoveAllowJumpOver,
            _workingCopy.MoveTravelVisualizationMode);
        ActionPayloadAccessors.SetMoveRoomObjectByPoints(
            _sourceAction,
            workingMoveByPointsPayload.TargetResolutionIntent,
            workingMoveByPointsPayload.AllowPartialMove,
            workingMoveByPointsPayload.AllowJumpOver,
            workingMoveByPointsPayload.VisualTransitionHint,
            workingMoveByPointsPayload.TravelVisualizationMode);
        ActionPayloadAccessors.SetRotateRoomObjectOnGrid(
            _sourceAction,
            _workingCopy.RotateMode,
            _workingCopy.RotateTurnDegrees,
            _workingCopy.RotateFacingDirectionToken,
            _workingCopy.RotateVisualTransitionHint);
        ActionPayloadAccessors.SetStackRoomObjectOnAnother(
            _sourceAction,
            _workingCopy.StackVisualTransitionHint);
        ActionPayloadAccessors.SetSetActiveRoomObject(
            _sourceAction,
            _workingCopy.SelectionCueEffectKey);
        ActionPayloadAccessors.SetSelectRoomObjectByPoint(
            _sourceAction,
            workingSelectByPointPayload.SelectionCueEffectKey);
        ActionPayloadAccessors.SetClearActiveRoomObjects(
            _sourceAction,
            _workingCopy.ClearActiveRoomObjectsScope);
        ActionPayloadAccessors.SetClearRoomObjectSelections(
            _sourceAction,
            ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(_workingCopy).ClearScope);
        _sourceAction.SetOutcomeScript("Success", _workingCopy.GetOutcomeScript("Success"));
        _sourceAction.SetOutcomeScript("Failure", _workingCopy.GetOutcomeScript("Failure"));
        _sourceAction.SetOutcomeScript("Moved", _workingCopy.GetOutcomeScript("Moved"));
        _sourceAction.SetOutcomeScript("DirectionRequired", _workingCopy.GetOutcomeScript("DirectionRequired"));
        _sourceAction.SetOutcomeScript("NoExitInDirection", _workingCopy.GetOutcomeScript("NoExitInDirection"));
        _sourceAction.SetOutcomeScript("NoTraversableExit", _workingCopy.GetOutcomeScript("NoTraversableExit"));
        _sourceAction.SetOutcomeScript("AmbiguousTraversal", _workingCopy.GetOutcomeScript("AmbiguousTraversal"));
        _sourceAction.SetOutcomeScript("TraversalBlocked", _workingCopy.GetOutcomeScript("TraversalBlocked"));
        _sourceAction.SetOutcomeScript("MoveFailed", _workingCopy.GetOutcomeScript("MoveFailed"));

        _sourceAction.SynonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(_workingCopy);
        ActionPayloadAccessors.SetMaterializeSourceObjectId(_sourceAction, ActionPayloadAccessors.GetMaterializeSourceObjectId(_workingCopy));
        ActionPayloadAccessors.SetProcedureId(_sourceAction, ActionPayloadAccessors.GetProcedureId(_workingCopy));
        ActionPayloadAccessors.SetStartTimer(
            _sourceAction,
            ActionPayloadAccessors.GetStartTimerKey(_workingCopy),
            ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(_workingCopy));
        ActionPayloadAccessors.SetCancelTimer(
            _sourceAction,
            ActionPayloadAccessors.GetCancelTimerKey(_workingCopy),
            ActionPayloadAccessors.GetCancelTimerScopeQualifierKind(_workingCopy),
            ActionPayloadAccessors.GetCancelTimerScopeQualifierId(_workingCopy));

        _sourceAction.CompositeTargetObjectId = _workingCopy.CompositeTargetObjectId;
        _sourceAction.CompositeRecipeId = _workingCopy.CompositeRecipeId;
        _sourceAction.CompositeRequiredPartObjectIds = _workingCopy.CompositeRequiredPartObjectIds.ToList();
        _sourceAction.CompositeStrictPartCountEnforcement = _workingCopy.CompositeStrictPartCountEnforcement;
        _sourceAction.CompositeMinimumRequiredPartCount = _workingCopy.CompositeMinimumRequiredPartCount;
        _sourceAction.CompositeMatchMode = _workingCopy.CompositeMatchMode;
        _sourceAction.CompositeAmbiguityPolicy = _workingCopy.CompositeAmbiguityPolicy;
        _sourceAction.CompositePartConsumptionMode = _workingCopy.CompositePartConsumptionMode;
        _sourceAction.CompositeResolvedTargetOutputTemplate = _workingCopy.CompositeResolvedTargetOutputTemplate;
        _sourceAction.SetOutcomeScript("IncompleteRecipe", _workingCopy.GetOutcomeScript("IncompleteRecipe"));
        _sourceAction.SetOutcomeScript("SpecifyTarget", _workingCopy.GetOutcomeScript("SpecifyTarget"));
        _sourceAction.SetOutcomeScript("TargetMismatch", _workingCopy.GetOutcomeScript("TargetMismatch"));
        _sourceAction.SetOutcomeScript("NoMatchingRecipe", _workingCopy.GetOutcomeScript("NoMatchingRecipe"));

        if (_workingCopy.ActionType == CommandActionType.BuildCompositeByTarget)
        {
            var payload = ActionPayloadAccessors.GetCompositeByTargetPayload(_workingCopy);
            _sourceAction.CompositeTargetObjectId = payload.CompositeTargetObjectId;
            _sourceAction.CompositeRecipeId = payload.CompositeRecipeId;
            _sourceAction.CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            _sourceAction.CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            _sourceAction.CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            _sourceAction.CompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }
        else if (_workingCopy.ActionType == CommandActionType.BuildCompositeByParts)
        {
            var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(_workingCopy);
            _sourceAction.CompositeTargetObjectId = payload.CompositeTargetObjectId;
            _sourceAction.CompositeRecipeId = payload.CompositeRecipeId;
            _sourceAction.CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            _sourceAction.CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            _sourceAction.CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            _sourceAction.CompositeMatchMode = payload.CompositeMatchMode;
            _sourceAction.CompositeAmbiguityPolicy = payload.CompositeAmbiguityPolicy;
            _sourceAction.CompositePartConsumptionMode = payload.CompositePartConsumptionMode;
            _sourceAction.CompositeResolvedTargetOutputTemplate = payload.CompositeResolvedTargetOutputTemplate;
        }
        else if (_workingCopy.ActionType == CommandActionType.BreakCompositeItem)
        {
            var payload = ActionPayloadAccessors.GetBreakCompositePayload(_workingCopy);
            _sourceAction.CompositeTargetObjectId = payload.CompositeTargetObjectId;
            _sourceAction.CompositeRecipeId = payload.CompositeRecipeId;
            _sourceAction.CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            _sourceAction.CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            _sourceAction.CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            _sourceAction.CompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }

        _sourceAction.ChildCommandForwardingMode = _workingCopy.ChildCommandForwardingMode;
        _sourceAction.SimilarChildDispatchMode = _workingCopy.SimilarChildDispatchMode;
        _sourceAction.OutcomeMessageMap = _workingCopy.OutcomeMessageMap;
        _sourceAction.OutcomeSoundEffectsMap = CommandAction.CloneOutcomeSoundEffectsMap(_workingCopy.OutcomeSoundEffectsMap);
        _sourceAction.Payload = _sourceAction.CreatePayloadSnapshot();

        DialogResult = true;
    }

    private IReadOnlyList<string> BuildSelectionCueEffectKeyChoices()
    {
        var configuredKeys = _presentationEffectsCatalogService.GetCatalog().Effects
            .Where(static effect => effect.Category == HostCommandPresentationCueCategory.Appearance)
            .Select(static effect => effect.EffectKey?.Trim() ?? string.Empty)
            .Where(static effectKey => !string.IsNullOrWhiteSpace(effectKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static effectKey => effectKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defaultKey = GameCommandPresentationEffectKeyCatalog.GetDefaultSelectionOutlineEffectKey();
        if (!configuredKeys.Contains(defaultKey, StringComparer.OrdinalIgnoreCase))
        {
            configuredKeys.Insert(0, defaultKey);
        }

        return configuredKeys;
    }

    private static IReadOnlyList<TimerOwnerTypeChoice> BuildTimerOwnerTypeChoices(bool includeBlank)
    {
        var choices = new List<TimerOwnerTypeChoice>();
        if (includeBlank)
        {
            choices.Add(new TimerOwnerTypeChoice
            {
                OwnerType = null,
                Label = "(None)"
            });
        }

        choices.AddRange(Enum.GetValues<TimerOwnerType>()
            .Select(ownerType => new TimerOwnerTypeChoice
            {
                OwnerType = ownerType,
                Label = ownerType.ToString()
            }));

        return choices;
    }

    private static void SelectTimerOwnerTypeChoice(System.Windows.Controls.ComboBox comboBox, TimerOwnerType? ownerType)
    {
        foreach (var item in comboBox.Items.OfType<TimerOwnerTypeChoice>())
        {
            if (item.OwnerType == ownerType)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private void RefreshPlaySoundEffectDisplay()
    {
        PlaySoundEffectChoiceComboBox.ItemsSource = _soundEffectChoices;
        PlaySoundEffectChoiceComboBox.DisplayMemberPath = nameof(SoundEffectChoiceItem.DisplayName);

        var selectedSoundEffectId = ResolveSelectedPlaySoundEffectId();
        if (selectedSoundEffectId.HasValue
            && _soundEffectChoicesById.TryGetValue(selectedSoundEffectId.Value, out var selectedChoice))
        {
            PlaySoundEffectChoiceComboBox.SelectedItem = selectedChoice;
        }
        else if (_soundEffectChoices.Count > 0)
        {
            PlaySoundEffectChoiceComboBox.SelectedIndex = 0;
        }
        else
        {
            PlaySoundEffectChoiceComboBox.SelectedItem = null;
        }

        PlaySoundEffectSelectionHintTextBlock.Text = _soundEffectChoices.Count == 0
            ? "No scoped sound effects are available here. Add one in the scope sound library first."
            : "The selected sound is stored on the Success result-code cue for this action.";
    }

    private Guid? ResolveSelectedPlaySoundEffectId()
    {
        if (!_workingCopy.OutcomeSoundEffectsMap.TryGetValue(PlaySoundEffectDefaultResultCodeToken, out var successCues)
            || successCues.Count == 0)
        {
            return null;
        }

        var activeCue = successCues.FirstOrDefault(static cue => cue.Enabled == true && cue.SoundEffectId != Guid.Empty)
            ?? successCues.FirstOrDefault(static cue => cue.SoundEffectId != Guid.Empty);
        return activeCue?.SoundEffectId;
    }

    private bool TryApplyPlaySoundEffectInputs()
    {
        if (PlaySoundEffectChoiceComboBox.SelectedItem is not SoundEffectChoiceItem selectedChoice)
        {
            System.Windows.MessageBox.Show(
                this,
                "Select a sound effect for PlaySoundEffect before saving.",
                "Action",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            PlaySoundEffectChoiceComboBox.Focus();
            return false;
        }

        var map = CommandAction.CloneOutcomeSoundEffectsMap(_workingCopy.OutcomeSoundEffectsMap);
        map[PlaySoundEffectDefaultResultCodeToken] =
        [
            new OutcomeSoundEffectCue
            {
                SoundEffectId = selectedChoice.SoundEffectId,
                SoundEffectKeyHint = string.IsNullOrWhiteSpace(selectedChoice.SoundEffectKey)
                    ? selectedChoice.DisplayName
                    : selectedChoice.SoundEffectKey,
                Enabled = true
            }
        ];

        _workingCopy.OutcomeSoundEffectsMap = map;
        return true;
    }

    private void LoadMoveByPointsEditorInputs()
    {
        var payload = ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(_workingCopy);
        var normalizedTargetResolutionIntent = NormalizeMoveByPointsTargetResolutionIntent(payload.TargetResolutionIntent);
        MoveByPointsTargetResolutionIntentComboBox.SelectedItem = MoveByPointsTargetResolutionIntentChoices.Contains(normalizedTargetResolutionIntent, StringComparer.Ordinal)
            ? normalizedTargetResolutionIntent
            : MoveByPointsTargetResolutionIntentChoices[0];
        MoveByPointsAllowPartialCheckBox.IsChecked = payload.AllowPartialMove;
        MoveByPointsAllowJumpOverCheckBox.IsChecked = payload.AllowJumpOver;
        MoveByPointsVisualTransitionHintComboBox.SelectedItem = payload.VisualTransitionHint;
        MoveByPointsTravelVisualizationModeComboBox.SelectedItem = payload.TravelVisualizationMode;
    }

    private bool TryApplyMoveRoomObjectByPointsInputs()
    {
        var targetResolutionIntent = NormalizeMoveByPointsTargetResolutionIntent(MoveByPointsTargetResolutionIntentComboBox.SelectedItem as string);
        if (!MoveByPointsTargetResolutionIntentChoices.Contains(targetResolutionIntent, StringComparer.Ordinal))
        {
            System.Windows.MessageBox.Show(this, "Select a valid Target Intent for MoveRoomObjectByPoints.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            MoveByPointsTargetResolutionIntentComboBox.Focus();
            return false;
        }

        var visualTransitionHint = MoveByPointsVisualTransitionHintComboBox.SelectedItem is RuntimeMovementVisualTransitionHint selectedHint
            ? selectedHint
            : RuntimeMovementVisualTransitionHint.Medium;
        var travelVisualizationMode = MoveByPointsTravelVisualizationModeComboBox.SelectedItem is RuntimeMovementTravelVisualizationMode selectedTravelMode
            ? selectedTravelMode
            : RuntimeMovementTravelVisualizationMode.LegByLeg;

        ActionPayloadAccessors.SetMoveRoomObjectByPoints(
            _workingCopy,
            targetResolutionIntent,
            MoveByPointsAllowPartialCheckBox.IsChecked == true,
            MoveByPointsAllowJumpOverCheckBox.IsChecked == true,
            visualTransitionHint,
            travelVisualizationMode);

        return true;
    }

    private static string NormalizeMoveByPointsTargetResolutionIntent(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized switch
        {
            "ActiveObject" => "PrimarySelection",
            "ObjectAtFirstWaypoint" => "ObjectAtFirstPoint",
            _ => normalized
        };
    }

    private void LoadSelectByPointEditorInputs()
    {
        var payload = ActionPayloadAccessors.GetSelectRoomObjectByPointPayload(_workingCopy);
        SelectByPointSelectionCueEffectKeyComboBox.Text = payload.SelectionCueEffectKey ?? string.Empty;
    }

    private bool TryApplySelectRoomObjectByPointInputs()
    {
        ActionPayloadAccessors.SetSelectRoomObjectByPoint(
            _workingCopy,
            SelectByPointSelectionCueEffectKeyComboBox.Text ?? string.Empty);
        return true;
    }

    private void LoadClearRoomObjectSelectionsEditorInputs()
    {
        var payload = ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(_workingCopy);
        ClearRoomObjectSelectionsScopeComboBox.SelectedItem = payload.ClearScope;
    }

    private bool TryApplyClearRoomObjectSelectionsInputs()
    {
        var clearScope = ClearRoomObjectSelectionsScopeComboBox.SelectedItem is ClearRoomObjectSelectionsScope selectedScope
            ? selectedScope
            : ClearRoomObjectSelectionsScope.All;

        ActionPayloadAccessors.SetClearRoomObjectSelections(_workingCopy, clearScope);
        return true;
    }

    private bool TryApplyMoveRoomObjectOnGridInputs()
    {
        var distanceText = MoveDistanceTextBox.Text?.Trim() ?? string.Empty;
        if (!int.TryParse(distanceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDistance)
            || parsedDistance < 1)
        {
            System.Windows.MessageBox.Show(this, "Move distance must be a whole number greater than or equal to 1.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            MoveDistanceTextBox.Focus();
            MoveDistanceTextBox.SelectAll();
            return false;
        }

        var directionToken = _workingCopy.MoveDirectionToken?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directionToken)
            && !IsMoveDirectionTokenValid(directionToken))
        {
            System.Windows.MessageBox.Show(this, "Move direction override must be a recognized direction token (for example: north, east, up) or left blank.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            MoveDirectionOverrideComboBox.Focus();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(directionToken)
            && GameCommandDirectionFormatting.TryParseToken(directionToken, out var parsedDirection)
            && parsedDirection != GameCommandDirection.Unknown)
        {
            directionToken = parsedDirection.ToToken();
        }

        _workingCopy.MoveDirectionToken = directionToken;
        _workingCopy.MoveDistanceInCells = parsedDistance;
        _workingCopy.MoveAllowPartialMove = MoveAllowPartialCheckBox.IsChecked == true;
        _workingCopy.MoveAllowJumpOver = MoveAllowJumpOverCheckBox.IsChecked == true;
        _workingCopy.MoveVisualTransitionHint = MoveVisualTransitionHintComboBox.SelectedItem is RuntimeMovementVisualTransitionHint selectedHint
            ? selectedHint
            : RuntimeMovementVisualTransitionHint.Medium;
        _workingCopy.MoveTravelVisualizationMode = MoveTravelVisualizationModeComboBox.SelectedItem is RuntimeMovementTravelVisualizationMode selectedTravelMode
            ? selectedTravelMode
            : RuntimeMovementTravelVisualizationMode.LegByLeg;
        return true;
    }

    private bool TryApplyRotateRoomObjectOnGridInputs()
    {
        var mode = RotateModeComboBox.SelectedItem is RuntimeRotateRoomObjectOnGridAttemptMode selectedMode
            ? selectedMode
            : _workingCopy.RotateMode;
        var turnDegreesText = RotateTurnDegreesTextBox.Text?.Trim() ?? string.Empty;
        var facingToken = RotateFacingDirectionComboBox.Text?.Trim() ?? string.Empty;
        int? turnDegrees = null;

        if (mode == RuntimeRotateRoomObjectOnGridAttemptMode.Turn)
        {
            if (!string.IsNullOrWhiteSpace(turnDegreesText)
                && !int.TryParse(turnDegreesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTurnDegrees))
            {
                System.Windows.MessageBox.Show(this, "Turn degrees must be a whole number in Turn mode.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
                RotateTurnDegreesTextBox.Focus();
                RotateTurnDegreesTextBox.SelectAll();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(turnDegreesText)
                && int.TryParse(turnDegreesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStep)
                && parsedStep % 45 != 0)
            {
                System.Windows.MessageBox.Show(this, "Turn degrees must be a multiple of 45.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
                RotateTurnDegreesTextBox.Focus();
                RotateTurnDegreesTextBox.SelectAll();
                return false;
            }

            turnDegrees = string.IsNullOrWhiteSpace(turnDegreesText)
                ? null
                : int.Parse(turnDegreesText, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(facingToken)
                && !TryParseRotateFacingToken(facingToken, out var normalizedFacingToken))
            {
                System.Windows.MessageBox.Show(this, "Facing direction must be one of N, NE, E, SE, S, SW, W, NW in Face mode.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
                RotateFacingDirectionComboBox.Focus();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(facingToken)
                && TryParseRotateFacingToken(facingToken, out var parsedFacingToken))
            {
                facingToken = parsedFacingToken;
            }
            else
            {
                facingToken = string.Empty;
            }
        }

        _workingCopy.RotateMode = mode;
        _workingCopy.RotateTurnDegrees = turnDegrees;
        _workingCopy.RotateFacingDirectionToken = mode == RuntimeRotateRoomObjectOnGridAttemptMode.Face
            ? facingToken
            : string.Empty;
        _workingCopy.RotateVisualTransitionHint = RotateVisualTransitionHintComboBox.SelectedItem is RuntimeMovementVisualTransitionHint selectedHint
            ? selectedHint
            : RuntimeMovementVisualTransitionHint.Medium;
        return true;
    }

    private bool TryApplyStackRoomObjectOnAnotherInputs()
    {
        _workingCopy.StackVisualTransitionHint = StackVisualTransitionHintComboBox.SelectedItem is RuntimeMovementVisualTransitionHint selectedHint
            ? selectedHint
            : RuntimeMovementVisualTransitionHint.Medium;
        return true;
    }

    private static bool IsMoveDirectionTokenValid(string token)
    {
        if (GameCommandDirectionFormatting.TryParseToken(token, out _))
        {
            return true;
        }

        return Enum.TryParse<GameCommandDirection>(token, true, out _);
    }

    private static IReadOnlyList<string> BuildMoveDirectionChoices()
    {
        var values = new List<string> { string.Empty };
        values.AddRange(Enum.GetValues<GameCommandDirection>()
            .Where(static direction => direction != GameCommandDirection.Unknown)
            .Select(static direction => direction.ToToken()));
        return values;
    }

    private static IReadOnlyList<string> BuildRotateFacingDirectionChoices()
    {
        return new List<string>
        {
            string.Empty,
            "N",
            "NE",
            "E",
            "SE",
            "S",
            "SW",
            "W",
            "NW"
        };
    }

    private static bool TryParseRotateFacingToken(string? token, out string normalized)
    {
        normalized = string.Empty;
        var value = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!GameCommandDirectionFormatting.TryParseToken(value, out var parsed))
        {
            if (!Enum.TryParse<GameCommandDirection>(value, true, out parsed))
            {
                return false;
            }
        }

        if (!IsEightWayDirection(parsed))
        {
            return false;
        }

        normalized = parsed.ToToken();
        return true;
    }

    private static bool IsEightWayDirection(GameCommandDirection direction)
    {
        return direction is GameCommandDirection.North
            or GameCommandDirection.NorthEast
            or GameCommandDirection.East
            or GameCommandDirection.SouthEast
            or GameCommandDirection.South
            or GameCommandDirection.SouthWest
            or GameCommandDirection.West
            or GameCommandDirection.NorthWest;
    }

    private void RotateModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshRotateInputState();
    }

    private void RefreshRotateInputState()
    {
        var mode = RotateModeComboBox.SelectedItem is RuntimeRotateRoomObjectOnGridAttemptMode selected
            ? selected
            : _workingCopy.RotateMode;
        var isTurn = mode == RuntimeRotateRoomObjectOnGridAttemptMode.Turn;

        RotateTurnDegreesTextBox.IsEnabled = isTurn;
        RotateFacingDirectionComboBox.IsEnabled = !isTurn;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ChooseFlagVariable_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = OpenVariableChooser("Choose Flag Variable", _workingCopy.FlagName);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            ActionPayloadAccessors.SetCheckGameProperty(_workingCopy, selected, _workingCopy.FlagValue);
        }
    }

    private void ChooseSetVariable_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = OpenVariableChooser("Choose Game Property", ActionPayloadAccessors.GetSetPropertyName(_workingCopy));
        if (!string.IsNullOrWhiteSpace(selected))
        {
            ActionPayloadAccessors.SetSetGameProperty(_workingCopy, selected, ActionPayloadAccessors.GetSetPropertyValue(_workingCopy));
        }
    }

    private string? OpenVariableChooser(string title, string? selectedValue)
    {
        var dialog = new VariableChooserDialog(_variableChoices, _variableScope, title, selectedValue)
        {
            Owner = this
        };

        return dialog.ShowDialog() == true ? dialog.SelectedValue : null;
    }

    private void ChooseSynonymTargetAction_OnClick(object sender, RoutedEventArgs e)
    {
        var candidates = _availableActions
            .Where(action => action.Id != _workingCopy.Id)
            .ToList();

        if (candidates.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No other actions are available in this scope.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var picker = new SelectLinkedActionDialog(candidates)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true && picker.SelectedAction is not null)
        {
            SetWorkingSynonymTargetActionId(picker.SelectedAction.Id);
            RefreshSynonymTargetDisplay();
        }
    }

    private bool ValidateSynonymTarget()
    {
        var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(_workingCopy);

        if (!synonymTargetActionId.HasValue)
        {
            System.Windows.MessageBox.Show(this, "Choose a target action for Synonym action type.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (synonymTargetActionId.Value == _workingCopy.Id)
        {
            System.Windows.MessageBox.Show(this, "Synonym action cannot target itself.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (_availableActions.Count > 0 && !_availableActionsById.ContainsKey(synonymTargetActionId.Value))
        {
            System.Windows.MessageBox.Show(this, "Selected synonym target action is not valid in this scope.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private void ChooseMaterializeSourceObject_OnClick(object sender, RoutedEventArgs e)
    {
        if (_materializeSourceObjectChoices.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No materialize source objects are available in this scope.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SelectGameObjectDialog(
            _materializeSourceObjectChoices,
            ActionPayloadAccessors.GetMaterializeSourceObjectId(_workingCopy),
            new SelectGameObjectDialog.Options(
                DefaultScopeSearchType: GameObjectOptionSourceTarget.RealObjects,
                DefaultScopeSearchDepth: ScopeSearchDepth.Project,
                DefaultRequiredFeatures: GameObjectFeatureRequirements.None,
                DefaultExcludeCurrentObject: false))
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || !dialog.SelectedObjectId.HasValue)
        {
            return;
        }

        ActionPayloadAccessors.SetMaterializeSourceObjectId(_workingCopy, dialog.SelectedObjectId);
        RefreshMaterializeSourceObjectDisplay();
    }

    private bool ValidateMaterializeSourceObject()
    {
        var sourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(_workingCopy);
        if (!sourceObjectId.HasValue || sourceObjectId.Value == Guid.Empty)
        {
            System.Windows.MessageBox.Show(this, "Choose a source object for MaterializeObjectCopy.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (_materializeSourceObjectChoices.Count > 0 && !_materializeSourceObjectChoicesById.ContainsKey(sourceObjectId.Value))
        {
            System.Windows.MessageBox.Show(this, "Selected materialize source object is not valid in this scope.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private void ChooseInvokeProcedure_OnClick(object sender, RoutedEventArgs e)
    {
        if (_procedureChoices.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No procedures are available in this project.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SelectProcedureDialog(_procedureChoices, ActionPayloadAccessors.GetProcedureId(_workingCopy))
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ActionPayloadAccessors.SetProcedureId(_workingCopy, dialog.SelectedProcedureId);
        RefreshInvokeProcedureDisplay();
    }

    private bool ValidateInvokeProcedureSelection()
    {
        var procedureId = ActionPayloadAccessors.GetProcedureId(_workingCopy);
        if (!procedureId.HasValue || procedureId.Value == Guid.Empty)
        {
            System.Windows.MessageBox.Show(this, "Choose a procedure for InvokeProcedure action type.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (_procedureChoices.Count > 0 && !_procedureChoicesById.ContainsKey(procedureId.Value))
        {
            System.Windows.MessageBox.Show(this, "Selected procedure is not valid in this scope.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private bool TryApplyBuildCompositeByTargetInputs()
    {
        if (!TryParseGuid(CompositeTargetObjectIdTextBox.Text, out var targetId))
        {
            System.Windows.MessageBox.Show(this, "BuildCompositeByTarget requires a valid target object GUID.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            CompositeTargetObjectIdTextBox.Focus();
            CompositeTargetObjectIdTextBox.SelectAll();
            return false;
        }

        var partIds = ParseGuidList(CompositeRequiredPartIdsTextBox.Text);
        if (partIds.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "BuildCompositeByTarget requires at least one required part GUID.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            CompositeRequiredPartIdsTextBox.Focus();
            return false;
        }

        _workingCopy.CompositeTargetObjectId = targetId;
        _workingCopy.CompositeRequiredPartObjectIds = partIds;
        return true;
    }

    private bool TryApplyBuildCompositeByPartsInputs()
    {
        if (CompositeRecipeComboBox.SelectedItem is CompositeRecipeChoiceItem selectedRecipe)
        {
            CompositePartsTargetObjectIdTextBox.Text = selectedRecipe.TargetObjectId.ToString();
            CompositePartsRequiredPartIdsTextBox.Text = string.Join(Environment.NewLine, selectedRecipe.RequiredPartObjectIds.Select(id => id.ToString()));
            _workingCopy.CompositeRecipeId = selectedRecipe.RecipeId;
        }

        var targetText = CompositePartsTargetObjectIdTextBox.Text;
        if (!TryParseGuid(targetText, out var targetId))
        {
            System.Windows.MessageBox.Show(this, "BuildCompositeByParts requires a valid target object GUID.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            CompositePartsTargetObjectIdTextBox.Focus();
            CompositePartsTargetObjectIdTextBox.SelectAll();
            return false;
        }

        var partIds = ParseGuidList(CompositePartsRequiredPartIdsTextBox.Text);
        if (partIds.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "BuildCompositeByParts requires at least one required part GUID.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            CompositePartsRequiredPartIdsTextBox.Focus();
            return false;
        }

        _workingCopy.CompositeTargetObjectId = targetId;
        _workingCopy.CompositeRequiredPartObjectIds = partIds;
        _workingCopy.CompositeMatchMode = GetComboBoxString(CompositeMatchModeComboBox, "ExactPartSet");
        _workingCopy.CompositeAmbiguityPolicy = GetComboBoxString(CompositeAmbiguityPolicyComboBox, "FailWithHint");
        _workingCopy.CompositePartConsumptionMode = GetComboBoxString(CompositePartConsumptionModeComboBox, "ContainedInComposite");
        _workingCopy.CompositeStrictPartCountEnforcement = CompositeStrictPartMentionsCheckBox.IsChecked == true;

        if (string.Equals(_workingCopy.CompositeMatchMode, "MinimumCount", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(CompositeMinimumRequiredCountTextBox.Text.Trim(), out var minimumRequiredCount) || minimumRequiredCount < 1)
            {
                System.Windows.MessageBox.Show(this, "Minimum Count must be a number greater than or equal to 1 when Match Mode is MinimumCount.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
                CompositeMinimumRequiredCountTextBox.Focus();
                CompositeMinimumRequiredCountTextBox.SelectAll();
                return false;
            }

            _workingCopy.CompositeMinimumRequiredPartCount = minimumRequiredCount;
        }
        else
        {
            _workingCopy.CompositeMinimumRequiredPartCount = null;
        }

        return true;
    }

    private void ApplyCompositeRecipeSelection_OnClick(object sender, RoutedEventArgs e)
    {
        ApplySelectedCompositeRecipe();
    }

    private void CompositeRecipeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingCompositeRecipeSelection)
        {
            return;
        }

        ApplySelectedCompositeRecipe();
    }

    private void ApplySelectedCompositeRecipe()
    {
        if (CompositeRecipeComboBox.SelectedItem is not CompositeRecipeChoiceItem selected)
        {
            return;
        }

        CompositePartsTargetObjectIdTextBox.Text = selected.TargetObjectId.ToString();
        CompositePartsRequiredPartIdsTextBox.Text = string.Join(Environment.NewLine, selected.RequiredPartObjectIds.Select(id => id.ToString()));
        RefreshCompositePartsFriendlyDisplay();
        SelectComboBoxString(CompositeMatchModeComboBox, string.IsNullOrWhiteSpace(selected.PartRequirementMode) ? "AllRequired" : selected.PartRequirementMode);
        CompositeMinimumRequiredCountTextBox.Text = (selected.MinimumRequiredPartCount ?? 1).ToString(CultureInfo.InvariantCulture);
        RefreshCompositeMinimumCountInputState();

        _workingCopy.CompositeRecipeId = selected.RecipeId;
    }

    private bool TryApplyBreakCompositeInputs()
    {
        if (BreakCompositeTargetComboBox.SelectedItem is BreakCompositeTargetChoiceItem selectedChoice)
        {
            _workingCopy.CompositeTargetObjectId = selectedChoice.TargetObjectId;
            _workingCopy.CompositeRecipeId = null;
            return true;
        }

        var typedText = BreakCompositeTargetComboBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(typedText))
        {
            _workingCopy.CompositeTargetObjectId = null;
            _workingCopy.CompositeRecipeId = null;
            return true;
        }

        if (TryParseGuid(typedText, out var targetId))
        {
            _workingCopy.CompositeTargetObjectId = targetId;
            _workingCopy.CompositeRecipeId = null;
            return true;
        }

        System.Windows.MessageBox.Show(this, "BreakCompositeItem target must be selected from the list, be a valid GUID, or be left blank to use the command target object.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
        BreakCompositeTargetComboBox.Focus();
        BreakCompositeTargetComboBox.Text = typedText;
        return false;
    }

    private static string GetComboBoxString(System.Windows.Controls.ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selected
            ? selected.Content?.ToString()?.Trim() ?? fallback
            : fallback;
    }

    private static bool TryParseGuid(string? text, out Guid value)
    {
        return Guid.TryParse(text?.Trim(), out value);
    }

    private static List<Guid> ParseGuidList(string? text)
    {
        var values = new List<Guid>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return values;
        }

        foreach (var token in text
                     .Split(new[] { ',', ';', '\r', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(token, out var parsed))
            {
                continue;
            }

            values.Add(parsed);
        }

        return values;
    }

    private static Dictionary<Guid, string> BuildCompositeObjectNameMap(IReadOnlyList<CompositeRecipeChoiceItem> choices)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var choice in choices)
        {
            if (choice.TargetObjectId != Guid.Empty && !string.IsNullOrWhiteSpace(choice.TargetName))
            {
                map[choice.TargetObjectId] = choice.TargetName.Trim();
            }

            var partNames = (choice.RequiredPartsDisplay ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            var expandedPartNames = ExpandQuantityDecoratedPartNames(partNames);

            for (var i = 0; i < choice.RequiredPartObjectIds.Count; i++)
            {
                var partId = choice.RequiredPartObjectIds[i];
                if (partId == Guid.Empty || map.ContainsKey(partId))
                {
                    continue;
                }

                if (i < expandedPartNames.Count && !string.IsNullOrWhiteSpace(expandedPartNames[i]))
                {
                    map[partId] = expandedPartNames[i];
                }
            }
        }

        return map;
    }

    private static List<string> ExpandQuantityDecoratedPartNames(IReadOnlyList<string> compactPartNames)
    {
        var expanded = new List<string>();
        foreach (var compactName in compactPartNames)
        {
            if (string.IsNullOrWhiteSpace(compactName))
            {
                continue;
            }

            var match = QuantitySuffixPattern.Match(compactName.Trim());
            if (match.Success
                && int.TryParse(match.Groups["qty"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
                && quantity > 1)
            {
                var baseName = match.Groups["name"].Value.Trim();
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    expanded.Add(compactName.Trim());
                    continue;
                }

                for (var i = 0; i < quantity; i++)
                {
                    expanded.Add(baseName);
                }

                continue;
            }

            expanded.Add(compactName.Trim());
        }

        return expanded;
    }

    private static IReadOnlyList<BreakCompositeTargetChoiceItem> BuildBreakCompositeTargetChoices(IReadOnlyList<CompositeRecipeChoiceItem> choices)
    {
        var items = new List<BreakCompositeTargetChoiceItem>
        {
            new()
            {
                TargetObjectId = null,
                DisplayName = "(Use command target object)"
            }
        };

        var targets = choices
            .Where(choice => choice.TargetObjectId != Guid.Empty)
            .GroupBy(choice => choice.TargetObjectId)
            .Select(group => group.First())
            .OrderBy(choice => choice.TargetName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var target in targets)
        {
            items.Add(new BreakCompositeTargetChoiceItem
            {
                TargetObjectId = target.TargetObjectId,
                DisplayName = $"{target.TargetName} ({target.TargetObjectId})"
            });
        }

        return items;
    }

    private void RefreshCompositePartsFriendlyDisplay()
    {
        if (TryParseGuid(CompositePartsTargetObjectIdTextBox.Text, out var targetId))
        {
            CompositePartsTargetNameTextBlock.Text = ResolveCompositeObjectName(targetId);
            CompositePartsTargetGuidTextBlock.Text = targetId.ToString();
        }
        else
        {
            CompositePartsTargetNameTextBlock.Text = "(not set)";
            CompositePartsTargetGuidTextBlock.Text = string.Empty;
        }

        var partItems = BuildGroupedCompositeObjectDisplayItems(ParseGuidList(CompositePartsRequiredPartIdsTextBox.Text));

        CompositePartsRequiredPartsListBox.ItemsSource = partItems;
    }

    private void RefreshCompositeTargetFriendlyDisplay()
    {
        if (TryParseGuid(CompositeTargetObjectIdTextBox.Text, out var targetId))
        {
            CompositeTargetNameTextBlock.Text = ResolveCompositeObjectName(targetId);
            CompositeTargetGuidTextBlock.Text = targetId.ToString();
        }
        else
        {
            CompositeTargetNameTextBlock.Text = "(not set)";
            CompositeTargetGuidTextBlock.Text = string.Empty;
        }

        var partItems = BuildGroupedCompositeObjectDisplayItems(ParseGuidList(CompositeRequiredPartIdsTextBox.Text));

        CompositeTargetRequiredPartsListBox.ItemsSource = partItems;
    }

    private List<CompositeObjectDisplayItem> BuildGroupedCompositeObjectDisplayItems(IEnumerable<Guid> partIds)
    {
        return partIds
            .Where(id => id != Guid.Empty)
            .GroupBy(id => id)
            .Select(group =>
            {
                var baseName = ResolveCompositeObjectName(group.Key);
                var quantity = group.Count();
                return new CompositeObjectDisplayItem
                {
                    Name = quantity > 1 ? $"{baseName} x{quantity}" : baseName,
                    GuidText = group.Key.ToString()
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveCompositeObjectName(Guid objectId)
    {
        return _compositeObjectNamesById.TryGetValue(objectId, out var name)
            && !string.IsNullOrWhiteSpace(name)
            ? name
            : "(unknown object)";
    }

    private void RefreshSynonymTargetDisplay()
    {
        var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(_workingCopy);

        if (!synonymTargetActionId.HasValue)
        {
            SynonymTargetActionTextBox.Text = string.Empty;
            return;
        }

        if (_availableActionsById.TryGetValue(synonymTargetActionId.Value, out var target))
        {
            SynonymTargetActionTextBox.Text = $"{target.Name} ({target.ActionType})";
            return;
        }

        SynonymTargetActionTextBox.Text = synonymTargetActionId.Value.ToString("N");
    }

    private void RefreshMaterializeSourceObjectDisplay()
    {
        var sourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(_workingCopy);
        if (!sourceObjectId.HasValue)
        {
            MaterializeSourceObjectTextBox.Text = string.Empty;
            return;
        }

        if (_materializeSourceObjectChoicesById.TryGetValue(sourceObjectId.Value, out var choice))
        {
            MaterializeSourceObjectTextBox.Text = $"{choice.DisplayName} [{choice.SourceCategory}] - {choice.ScopePath}";
            return;
        }

        MaterializeSourceObjectTextBox.Text = sourceObjectId.Value.ToString("N");
    }

    private void RefreshInvokeProcedureDisplay()
    {
        var procedureId = ActionPayloadAccessors.GetProcedureId(_workingCopy);
        if (!procedureId.HasValue || procedureId.Value == Guid.Empty)
        {
            InvokeProcedureTextBox.Text = string.Empty;
            return;
        }

        if (_procedureChoicesById.TryGetValue(procedureId.Value, out var choice))
        {
            InvokeProcedureTextBox.Text = $"{choice.DisplayName} [{choice.OwnerLabel}] - {choice.ScopePath}";
            return;
        }

        InvokeProcedureTextBox.Text = procedureId.Value.ToString("N");
    }

    private void RefreshStartTimerDisplay()
    {
        StartTimerKeyComboBox.Text = ActionPayloadAccessors.GetStartTimerKey(_workingCopy);
        SelectTimerOwnerTypeChoice(
            StartTimerOwnerScopeKindOverrideComboBox,
            ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(_workingCopy));
    }

    private void RefreshCancelTimerDisplay()
    {
        CancelTimerKeyComboBox.Text = ActionPayloadAccessors.GetCancelTimerKey(_workingCopy);
        CancelTimerScopeQualifierIdTextBox.Text = ActionPayloadAccessors.GetCancelTimerScopeQualifierId(_workingCopy)?.ToString("D") ?? string.Empty;
        SelectTimerOwnerTypeChoice(
            CancelTimerScopeQualifierKindComboBox,
            ActionPayloadAccessors.GetCancelTimerScopeQualifierKind(_workingCopy));
    }

    private static TimerOwnerType? GetSelectedTimerOwnerType(System.Windows.Controls.ComboBox comboBox)
    {
        return comboBox.SelectedItem is TimerOwnerTypeChoice choice
            ? choice.OwnerType
            : null;
    }

    private bool TryApplyStartTimerInputs()
    {
        var timerKey = StartTimerKeyComboBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(timerKey))
        {
            System.Windows.MessageBox.Show(this, "StartTimer requires a timer key.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            StartTimerKeyComboBox.Focus();
            return false;
        }

        var ownerScopeKindOverride = GetSelectedTimerOwnerType(StartTimerOwnerScopeKindOverrideComboBox);
        ActionPayloadAccessors.SetStartTimer(_workingCopy, timerKey, ownerScopeKindOverride);
        return true;
    }

    private bool TryApplyCancelTimerInputs()
    {
        var timerKey = CancelTimerKeyComboBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(timerKey))
        {
            System.Windows.MessageBox.Show(this, "CancelTimer requires a timer key.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            CancelTimerKeyComboBox.Focus();
            return false;
        }

        var scopeQualifierKind = GetSelectedTimerOwnerType(CancelTimerScopeQualifierKindComboBox);
        var scopeQualifierIdText = CancelTimerScopeQualifierIdTextBox.Text?.Trim() ?? string.Empty;
        Guid? scopeQualifierId = null;
        if (!string.IsNullOrWhiteSpace(scopeQualifierIdText))
        {
            if (!Guid.TryParse(scopeQualifierIdText, out var parsedScopeQualifierId))
            {
                System.Windows.MessageBox.Show(this, "CancelTimer scope qualifier id must be a valid GUID when provided.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
                CancelTimerScopeQualifierIdTextBox.Focus();
                CancelTimerScopeQualifierIdTextBox.SelectAll();
                return false;
            }

            scopeQualifierId = parsedScopeQualifierId;
        }

        ActionPayloadAccessors.SetCancelTimer(_workingCopy, timerKey, scopeQualifierKind, scopeQualifierId);
        return true;
    }

    private void StartTimerInputs_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_workingCopy.ActionType != CommandActionType.StartTimer)
        {
            return;
        }

        ActionPayloadAccessors.SetStartTimer(
            _workingCopy,
            StartTimerKeyComboBox.Text?.Trim() ?? string.Empty,
            GetSelectedTimerOwnerType(StartTimerOwnerScopeKindOverrideComboBox));
    }

    private void CancelTimerInputs_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_workingCopy.ActionType != CommandActionType.CancelTimer)
        {
            return;
        }

        Guid? scopeQualifierId = null;
        var scopeQualifierIdText = CancelTimerScopeQualifierIdTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(scopeQualifierIdText)
            && Guid.TryParse(scopeQualifierIdText, out var parsedScopeQualifierId))
        {
            scopeQualifierId = parsedScopeQualifierId;
        }

        ActionPayloadAccessors.SetCancelTimer(
            _workingCopy,
            CancelTimerKeyComboBox.Text?.Trim() ?? string.Empty,
            GetSelectedTimerOwnerType(CancelTimerScopeQualifierKindComboBox),
            scopeQualifierId);
    }

    private void SetWorkingSynonymTargetActionId(Guid? synonymTargetActionId)
    {
        _workingCopy.SynonymTargetActionId = synonymTargetActionId;
        if (_workingCopy.ActionType == CommandActionType.Synonym)
        {
            _workingCopy.Payload = new SynonymPayload(synonymTargetActionId);
        }
    }

    private void ApplyWorkingNavigatePayload(NavigatePayload payload)
    {
        if (_workingCopy.ActionType == CommandActionType.NavigateDirection
            || _workingCopy.ActionType == CommandActionType.NavigateToAdjacent)
        {
            _workingCopy.Payload = payload;
        }
    }

    private bool IsFlagTargetValid(string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return true;
        }

        if (!_variableChoiceMap.TryGetValue(variableName.Trim(), out var choice))
        {
            return true;
        }

        if (choice.ValueRestriction == GamePropertyValueRestriction.TrueFalse)
        {
            return true;
        }

        System.Windows.MessageBox.Show(
            this,
            "Set Flag requires a variable with the True False restriction.",
            "Action",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private void ForwardToChildrenCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        ForwardingModeComboBox.IsEnabled = ForwardToChildrenCheckBox.IsChecked == true;
        SimilarChildDispatchModeComboBox.IsEnabled = ForwardToChildrenCheckBox.IsChecked == true;

        if (ForwardToChildrenCheckBox.IsChecked == true)
        {
            var selectedMode = (ForwardingModeComboBox.SelectedItem as ForwardingModeOption)?.Mode
                ?? ChildCommandForwardingMode.ChildrenAfterParent;
            _workingCopy.ChildCommandForwardingMode = selectedMode;
            _workingCopy.SimilarChildDispatchMode = (SimilarChildDispatchModeComboBox.SelectedItem as SimilarDispatchModeOption)?.Mode
                ?? SimilarChildDispatchMode.SingleMatchingChild;
            return;
        }

        _workingCopy.ChildCommandForwardingMode = ChildCommandForwardingMode.None;
    }

    private void ForwardingModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ForwardToChildrenCheckBox.IsChecked != true)
        {
            return;
        }

        if (ForwardingModeComboBox.SelectedItem is ForwardingModeOption option)
        {
            _workingCopy.ChildCommandForwardingMode = option.Mode;
        }
    }

    private void SimilarChildDispatchModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ForwardToChildrenCheckBox.IsChecked != true)
        {
            return;
        }

        if (SimilarChildDispatchModeComboBox.SelectedItem is SimilarDispatchModeOption option)
        {
            _workingCopy.SimilarChildDispatchMode = option.Mode;
        }
    }

    private void OutcomeEchoEntriesListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshOutcomeEchoUnsupportedWarning();
    }

    private void OutcomeEchoEntriesListView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (OutcomeEchoEntriesListView.SelectedItem is not ActionEchoEditorEntry entry)
        {
            return;
        }

        OpenOutcomeEchoEntryEditor(entry);
    }

    private void OutcomeEchoScriptPreview_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox { DataContext: ActionEchoEditorEntry entry })
        {
            return;
        }

        OutcomeEchoEntriesListView.SelectedItem = entry;
        OpenOutcomeEchoEntryEditor(entry);
        e.Handled = true;
    }

    private void OpenOutcomeEchoEntryEditor(ActionEchoEditorEntry entry)
    {
        try
        {
            var dialog = new TextOutputScriptEditorDialog(entry.Script, GetEchoReferenceTokensForCurrentAction(), _variableChoices, _variableScope)
            {
                Owner = this,
                Title = $"Edit Echo: {entry.Token}"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            entry.Script = dialog.ScriptText ?? string.Empty;
            OutcomeEchoEntriesListView.Items.Refresh();
            PersistOutcomeEchoEntriesToWorkingCopy();
            RefreshOutcomeEchoUnsupportedWarning();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Unable to open the script editor for '{entry.Token}'.\\n\\n{ex.Message}", "Action", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshOutcomeEchoEntries()
    {
        var selectedToken = (OutcomeEchoEntriesListView.SelectedItem as ActionEchoEditorEntry)?.Token;
        var entries = ActionEchoEditorEntryBuilder.BuildEntries(_workingCopy.ActionType, BuildOutcomeEchoMapForEditor());

        _outcomeEchoEntries.Clear();
        foreach (var entry in entries)
        {
            _outcomeEchoEntries.Add(entry);
        }

        if (_outcomeEchoEntries.Count > 0)
        {
            var selected = _outcomeEchoEntries.FirstOrDefault(entry => string.Equals(entry.Token, selectedToken, StringComparison.OrdinalIgnoreCase));
            OutcomeEchoEntriesListView.SelectedItem = selected ?? _outcomeEchoEntries[0];
        }

        RefreshOutcomeEchoUnsupportedWarning();
    }

    private void OutcomeSoundResultCodeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshOutcomeSoundCueEntries();
        RefreshOutcomeSoundEditorStates();
    }

    private void OutcomeSoundCueEntriesListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutcomeSoundCueEntriesListView.SelectedItem is not OutcomeSoundCueDisplayEntry selectedCue)
        {
            OutcomeSoundCueHintTextBox.Text = string.Empty;
            OutcomeSoundCueLaneTextBlock.Text = "sfx";
            OutcomeSoundCueEnabledCheckBox.IsChecked = true;
            RefreshOutcomeSoundEditorStates();
            return;
        }

        OutcomeSoundCueHintTextBox.Text = selectedCue.HintText;
        OutcomeSoundCueLaneTextBlock.Text = selectedCue.LaneText;
        OutcomeSoundCueEnabledCheckBox.IsChecked = string.Equals(selectedCue.EnabledText, "Yes", StringComparison.OrdinalIgnoreCase);
        RefreshOutcomeSoundEditorStates();
    }

    private void OutcomeSoundChoiceFilter_OnChanged(object sender, RoutedEventArgs e)
    {
        RefreshOutcomeSoundChoiceFilter();
        RefreshOutcomeSoundEditorStates();
    }

    private void OutcomeSoundChoicesListView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AddSelectedOutcomeSoundChoice();
    }

    private void OutcomeSoundAddSelectedChoice_OnClick(object sender, RoutedEventArgs e)
    {
        AddSelectedOutcomeSoundChoice();
    }

    private void OutcomeSoundApplyCueChanges_OnClick(object sender, RoutedEventArgs e)
    {
        if (OutcomeSoundCueEntriesListView.SelectedItem is not OutcomeSoundCueDisplayEntry selectedCue)
        {
            return;
        }

        if (!TryGetSelectedOutcomeSoundToken(out var token))
        {
            return;
        }

        var map = CommandAction.CloneOutcomeSoundEffectsMap(_workingCopy.OutcomeSoundEffectsMap);
        if (!map.TryGetValue(token, out var cues))
        {
            return;
        }

        var index = selectedCue.Index - 1;
        if (index < 0 || index >= cues.Count)
        {
            return;
        }

        cues[index] = new OutcomeSoundEffectCue
        {
            SoundEffectId = cues[index].SoundEffectId,
            SoundEffectKeyHint = OutcomeSoundCueHintTextBox.Text?.Trim() ?? string.Empty,
            Enabled = OutcomeSoundCueEnabledCheckBox.IsChecked == true
        };

        _workingCopy.OutcomeSoundEffectsMap = map;
        RefreshOutcomeSoundCueEntries(selectedCue.Index);
        RefreshOutcomeSoundEditorStates();
    }

    private void OutcomeSoundMoveCueUp_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedOutcomeSoundCue(-1);
    }

    private void OutcomeSoundMoveCueDown_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedOutcomeSoundCue(1);
    }

    private void OutcomeSoundRemoveCue_OnClick(object sender, RoutedEventArgs e)
    {
        if (OutcomeSoundCueEntriesListView.SelectedItem is not OutcomeSoundCueDisplayEntry selectedCue)
        {
            return;
        }

        if (!TryGetSelectedOutcomeSoundToken(out var token))
        {
            return;
        }

        var map = CommandAction.CloneOutcomeSoundEffectsMap(_workingCopy.OutcomeSoundEffectsMap);
        if (!map.TryGetValue(token, out var cues))
        {
            return;
        }

        var index = selectedCue.Index - 1;
        if (index < 0 || index >= cues.Count)
        {
            return;
        }

        cues.RemoveAt(index);
        _workingCopy.OutcomeSoundEffectsMap = map;
        RefreshOutcomeSoundCueEntries(Math.Max(1, selectedCue.Index - 1));
        RefreshOutcomeSoundEditorStates();
    }

    private void MoveSelectedOutcomeSoundCue(int offset)
    {
        if (OutcomeSoundCueEntriesListView.SelectedItem is not OutcomeSoundCueDisplayEntry selectedCue)
        {
            return;
        }

        if (!TryGetSelectedOutcomeSoundToken(out var token))
        {
            return;
        }

        var map = CommandAction.CloneOutcomeSoundEffectsMap(_workingCopy.OutcomeSoundEffectsMap);
        if (!map.TryGetValue(token, out var cues))
        {
            return;
        }

        var currentIndex = selectedCue.Index - 1;
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || currentIndex >= cues.Count || targetIndex >= cues.Count)
        {
            return;
        }

        (cues[currentIndex], cues[targetIndex]) = (cues[targetIndex], cues[currentIndex]);
        _workingCopy.OutcomeSoundEffectsMap = map;
        RefreshOutcomeSoundCueEntries(targetIndex + 1);
        RefreshOutcomeSoundEditorStates();
    }

    private void AddSelectedOutcomeSoundChoice()
    {
        if (OutcomeSoundChoicesListView.SelectedItem is not SoundEffectChoiceItem choice)
        {
            return;
        }

        if (!TryGetSelectedOutcomeSoundToken(out var token))
        {
            System.Windows.MessageBox.Show(this, "Select a result code before adding a sound cue.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var map = CommandAction.CloneOutcomeSoundEffectsMap(_workingCopy.OutcomeSoundEffectsMap);
        if (!map.TryGetValue(token, out var cues))
        {
            cues = new List<OutcomeSoundEffectCue>();
            map[token] = cues;
        }

        if (cues.Any(cue => cue.SoundEffectId == choice.SoundEffectId))
        {
            System.Windows.MessageBox.Show(this, "This result code already has that sound effect assigned.", "Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        cues.Add(new OutcomeSoundEffectCue
        {
            SoundEffectId = choice.SoundEffectId,
            SoundEffectKeyHint = string.IsNullOrWhiteSpace(choice.SoundEffectKey)
                ? choice.DisplayName
                : choice.SoundEffectKey,
            Enabled = true
        });

        _workingCopy.OutcomeSoundEffectsMap = map;
        RefreshOutcomeSoundCueEntries(cues.Count);
        RefreshOutcomeSoundEditorStates();
    }

    private void RefreshOutcomeSoundResultCodeChoices()
    {
        var previousToken = OutcomeSoundResultCodeComboBox.SelectedItem as string;
        var tokens = _outcomeEchoEntries
            .Select(static entry => entry.Token)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        OutcomeSoundResultCodeComboBox.ItemsSource = tokens;
        if (tokens.Count == 0)
        {
            OutcomeSoundResultCodeComboBox.SelectedItem = null;
            RefreshOutcomeSoundCueEntries();
            RefreshOutcomeSoundEditorStates();
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousToken)
            && tokens.Contains(previousToken, StringComparer.OrdinalIgnoreCase))
        {
            OutcomeSoundResultCodeComboBox.SelectedItem = tokens.First(token => string.Equals(token, previousToken, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            OutcomeSoundResultCodeComboBox.SelectedIndex = 0;
        }

        RefreshOutcomeSoundCueEntries();
        RefreshOutcomeSoundEditorStates();
    }

    private void RefreshOutcomeSoundChoiceFilter()
    {
        _outcomeSoundChoicesView?.Refresh();

        if (_outcomeSoundChoicesView is not null
            && OutcomeSoundChoicesListView.SelectedItem is SoundEffectChoiceItem selected
            && !_outcomeSoundChoicesView.Cast<SoundEffectChoiceItem>().Contains(selected))
        {
            OutcomeSoundChoicesListView.SelectedItem = null;
        }

        if (OutcomeSoundChoicesListView.SelectedItem is null
            && _outcomeSoundChoicesView is not null
            && _outcomeSoundChoicesView.Cast<SoundEffectChoiceItem>().Any())
        {
            OutcomeSoundChoicesListView.SelectedIndex = 0;
        }

        RefreshOutcomeSoundEditorStates();
    }

    private bool OutcomeSoundChoiceMatchesFilter(object value)
    {
        if (value is not SoundEffectChoiceItem choice)
        {
            return false;
        }

        if (OutcomeSoundScopeFilterComboBox.SelectedItem is OutcomeSoundScopeFilterOption scopeFilter)
        {
            var matchesScope = scopeFilter switch
            {
                OutcomeSoundScopeFilterOption.CurrentScope => choice.ScopeRelation == SoundEffectScopeRelation.Current,
                OutcomeSoundScopeFilterOption.UpScope => choice.ScopeRelation == SoundEffectScopeRelation.UpScope,
                OutcomeSoundScopeFilterOption.DownScope => choice.ScopeRelation == SoundEffectScopeRelation.DownScope,
                _ => true
            };

            if (!matchesScope)
            {
                return false;
            }
        }

        var searchText = OutcomeSoundSearchTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return (choice.DisplayName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || (choice.SoundEffectKey?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || (choice.ScopePath?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || (choice.SourceCategory?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || (choice.AssetRef?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
               || choice.SoundEffectId.ToString("N").Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetSelectedOutcomeSoundToken(out string token)
    {
        token = OutcomeSoundResultCodeComboBox.SelectedItem as string ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }

    private void RefreshOutcomeSoundCueEntries(int? preferredSelectionIndexOneBased = null)
    {
        var selectedCueId = (OutcomeSoundCueEntriesListView.SelectedItem as OutcomeSoundCueDisplayEntry)?.SoundEffectId;
        _outcomeSoundCueEntries.Clear();

        if (!TryGetSelectedOutcomeSoundToken(out var token))
        {
            OutcomeSoundUnresolvedWarningTextBlock.Visibility = Visibility.Collapsed;
            OutcomeSoundCueStatusTextBlock.Text = "Select a result code to edit cues.";
            return;
        }

        if (!_workingCopy.OutcomeSoundEffectsMap.TryGetValue(token, out var cues) || cues.Count == 0)
        {
            OutcomeSoundUnresolvedWarningTextBlock.Visibility = Visibility.Collapsed;
            OutcomeSoundCueStatusTextBlock.Text = $"No cues configured for result code '{token}'.";
            return;
        }

        var unresolvedCount = 0;

        for (var index = 0; index < cues.Count; index++)
        {
            var cue = cues[index];
            _soundEffectChoicesById.TryGetValue(cue.SoundEffectId, out var resolvedChoice);
            if (resolvedChoice is null)
            {
                unresolvedCount++;
            }

            var displayName = resolvedChoice?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = !string.IsNullOrWhiteSpace(cue.SoundEffectKeyHint)
                    ? cue.SoundEffectKeyHint
                    : cue.SoundEffectId.ToString("N");
            }

            var sourceCategory = resolvedChoice?.SourceCategory;
            var lane = resolvedChoice?.SoundEffectLane ?? SoundEffectLane.Sfx;

            _outcomeSoundCueEntries.Add(new OutcomeSoundCueDisplayEntry
            {
                Index = index + 1,
                SoundEffectId = cue.SoundEffectId,
                Lane = lane,
                DisplayName = displayName ?? cue.SoundEffectId.ToString("N"),
                ScopeCategoryText = string.IsNullOrWhiteSpace(sourceCategory)
                    ? "(unresolved)"
                    : sourceCategory,
                LaneText = lane == SoundEffectLane.Ambient ? "ambient" : "sfx",
                HintText = cue.SoundEffectKeyHint ?? string.Empty,
                EnabledText = cue.Enabled == false ? "No" : "Yes"
            });
        }

        OutcomeSoundCueStatusTextBlock.Text = $"{_outcomeSoundCueEntries.Count} cue(s) configured for '{token}'.";
        if (unresolvedCount > 0)
        {
            OutcomeSoundUnresolvedWarningTextBlock.Text = $"{unresolvedCount} cue(s) reference missing sound effects. Re-select a valid sound effect or remove stale cues.";
            OutcomeSoundUnresolvedWarningTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            OutcomeSoundUnresolvedWarningTextBlock.Visibility = Visibility.Collapsed;
        }

        if (_outcomeSoundCueEntries.Count == 0)
        {
            return;
        }

        if (preferredSelectionIndexOneBased.HasValue)
        {
            var bounded = Math.Max(1, Math.Min(preferredSelectionIndexOneBased.Value, _outcomeSoundCueEntries.Count));
            OutcomeSoundCueEntriesListView.SelectedItem = _outcomeSoundCueEntries[bounded - 1];
            return;
        }

        var previouslySelected = _outcomeSoundCueEntries.FirstOrDefault(entry => entry.SoundEffectId == selectedCueId);
        OutcomeSoundCueEntriesListView.SelectedItem = previouslySelected ?? _outcomeSoundCueEntries[0];
    }

    private void RefreshOutcomeSoundEditorStates()
    {
        var hasToken = TryGetSelectedOutcomeSoundToken(out _);
        var hasSelectedCue = OutcomeSoundCueEntriesListView.SelectedItem is OutcomeSoundCueDisplayEntry;
        var hasSelectedChoice = OutcomeSoundChoicesListView.SelectedItem is SoundEffectChoiceItem;
        var selectedCueIndex = OutcomeSoundCueEntriesListView.SelectedIndex;
        var cueCount = _outcomeSoundCueEntries.Count;

        OutcomeSoundCueHintTextBox.IsEnabled = hasToken && hasSelectedCue;
        OutcomeSoundCueEnabledCheckBox.IsEnabled = hasToken && hasSelectedCue;

        OutcomeSoundAddSelectedChoiceButton.IsEnabled = hasToken && hasSelectedChoice;
        OutcomeSoundApplyCueButton.IsEnabled = hasToken && hasSelectedCue;
        OutcomeSoundRemoveCueButton.IsEnabled = hasToken && hasSelectedCue;
        OutcomeSoundMoveCueUpButton.IsEnabled = hasToken && hasSelectedCue && selectedCueIndex > 0;
        OutcomeSoundMoveCueDownButton.IsEnabled = hasToken && hasSelectedCue && selectedCueIndex >= 0 && selectedCueIndex < cueCount - 1;
    }

    private void PersistOutcomeEchoEntriesToWorkingCopy()
    {
        _workingCopy.OutcomeMessageMap = ActionEchoEditorEntryBuilder.ToOutcomeMessageMap(_outcomeEchoEntries)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> BuildOutcomeEchoMapForEditor()
    {
        return _workingCopy.OutcomeMessageMap
            .ToDictionary(static pair => pair.Key, static pair => pair.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetOutcomeMessage(CommandAction action, string token)
    {
        return action.OutcomeMessageMap.TryGetValue(token, out var script)
            ? script ?? string.Empty
            : string.Empty;
    }


    private void RefreshOutcomeEchoUnsupportedWarning()
    {
        var state = ActionEchoEditorDialogStatePresenter.Build(_outcomeEchoEntries.ToList().AsReadOnly(), OutcomeEchoEntriesListView.SelectedItem as ActionEchoEditorEntry);
        OutcomeEchoUnsupportedWarningBorder.Visibility = state.IsUnsupportedWarningVisible ? Visibility.Visible : Visibility.Collapsed;
        OutcomeEchoUnsupportedWarningText.Text = state.UnsupportedWarningText;
    }


    private IReadOnlyList<string> GetEchoReferenceTokensForCurrentAction()
    {
        return GetEchoReferenceTokensForActionType(_workingCopy.ActionType);
    }

    private IReadOnlyList<string> GetEchoReferenceTokensForActionType(CommandActionType actionType)
    {
        var tokens = _echoReferenceTokens.Concat(_actionEchoTokenProviderRegistry.GetTokens(actionType));
        return OrderReferenceTokensPreferSelf(ActionOutputReferenceTokenNormalizer.ExpandCurrentActionAnchors(tokens));
    }

    private bool IsSetVariableValueAllowed(string? variableName, string variableValue)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return true;
        }

        if (!_variableChoiceMap.TryGetValue(variableName.Trim(), out var choice))
        {
            return true;
        }

        var trimmedValue = variableValue.Trim();
        if (choice.ValueRestriction == GamePropertyValueRestriction.Unrestricted && ContainsDynamicExpression(trimmedValue))
        {
            return true;
        }

        if (VariableValueRestrictionValidator.IsAllowed(choice.ValueRestriction, trimmedValue))
        {
            return true;
        }

        System.Windows.MessageBox.Show(
            this,
            VariableValueRestrictionValidator.BuildInvalidValueMessage(choice.ValueRestriction, "Game property value"),
            "Action",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private static bool ContainsDynamicExpression(string value)
    {
        return value.Contains('{') || value.Contains('}') || value.Contains('@');
    }

    private void RefreshVariableValueEditorMode()
    {
        if (!ActionPayloadSchemaHelpers.OwnsAllFields(_workingCopy.ActionType, nameof(CommandAction.GamePropertyName), nameof(CommandAction.GamePropertyValue)))
        {
            VariableValueEditorTextBox.Visibility = Visibility.Visible;
            VariableValueBooleanComboBox.Visibility = Visibility.Collapsed;
            return;
        }

        var restriction = GetRestrictionForVariable(_workingCopy.GamePropertyName);
        switch (restriction)
        {
            case GamePropertyValueRestriction.TrueFalse:
                VariableValueEditorTextBox.Visibility = Visibility.Collapsed;
                VariableValueBooleanComboBox.Visibility = Visibility.Visible;
                var setPropertyName = ActionPayloadAccessors.GetSetPropertyName(_workingCopy);
                var setPropertyValue = ActionPayloadAccessors.GetSetPropertyValue(_workingCopy);

                if (!string.Equals(setPropertyValue, "true", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(setPropertyValue, "false", StringComparison.OrdinalIgnoreCase))
                {
                    ActionPayloadAccessors.SetSetGameProperty(_workingCopy, setPropertyName, "false");
                    setPropertyValue = "false";
                }

                VariableValueBooleanComboBox.SelectedItem = setPropertyValue.ToLowerInvariant();
                break;

            case GamePropertyValueRestriction.Numeric:
                VariableValueBooleanComboBox.Visibility = Visibility.Collapsed;
                VariableValueEditorTextBox.Visibility = Visibility.Visible;
                CancelVariableCompletion();
                CancelVariableFunctionCompletion();
                break;

            default:
                VariableValueBooleanComboBox.Visibility = Visibility.Collapsed;
                VariableValueEditorTextBox.Visibility = Visibility.Visible;
                break;
        }
    }

    private GamePropertyValueRestriction GetRestrictionForVariable(string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return GamePropertyValueRestriction.Unrestricted;
        }

        return _variableChoiceMap.TryGetValue(variableName.Trim(), out var choice)
            ? choice.ValueRestriction
            : GamePropertyValueRestriction.Unrestricted;
    }

    private void EchoTextEditorTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == "{")
        {
            CancelEchoFunctionCompletion();
            CancelEchoReturnCompletion();

            if (GetEchoReferenceTokensForCurrentAction().Count == 0)
            {
                return;
            }

            var textBox = EchoTextEditorTextBox;
            var start = textBox.CaretIndex;
            textBox.Text = textBox.Text.Insert(start, "{");
            textBox.CaretIndex = start + 1;
            _echoCompletionStart = textBox.CaretIndex;
            RefreshEchoReferenceFilter();
            e.Handled = true;
            return;
        }

        if (e.Text != "@")
        {
            return;
        }

        var functionTextBox = EchoTextEditorTextBox;
        var functionStart = functionTextBox.CaretIndex;
        functionTextBox.Text = functionTextBox.Text.Insert(functionStart, "@");
        functionTextBox.CaretIndex = functionStart + 1;
        _echoFunctionCompletionStart = functionStart;
        CancelEchoCompletion();
        CancelEchoReturnCompletion();
        RefreshEchoFunctionFilter();
        e.Handled = true;
    }

    private void EchoTextEditorTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_echoCompletionStart is not null)
        {
            RefreshEchoReferenceFilter();
        }

        if (_echoFunctionCompletionStart is not null)
        {
            RefreshEchoFunctionFilter();
        }

        if (_echoCompletionStart is null && _echoFunctionCompletionStart is null)
        {
            RefreshEchoReturnFilter();
        }
        else
        {
            CancelEchoReturnCompletion();
        }
    }

    private void EchoTextEditorTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_echoCompletionStart is not null)
        {
            if (e.Key == Key.Tab)
            {
                InsertTopEchoReference();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelEchoCompletion();
                e.Handled = true;
                return;
            }
        }

        if (_echoFunctionCompletionStart is not null)
        {
            if (e.Key == Key.Tab)
            {
                InsertTopEchoFunction();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelEchoFunctionCompletion();
                e.Handled = true;
            }
        }

        if (_echoReturnCompletionStart is not null)
        {
            if (e.Key is Key.Tab or Key.Enter or Key.Return)
            {
                InsertTopEchoReturnStatement();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelEchoReturnCompletion();
                e.Handled = true;
            }
        }
    }

    private void VariableValueEditorTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var restriction = GetRestrictionForVariable(_workingCopy.GamePropertyName);
        if (restriction == GamePropertyValueRestriction.Numeric)
        {
            var textBox = VariableValueEditorTextBox;
            var candidate = BuildCandidateText(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, e.Text);
            if (!IsValidNumericPartial(candidate))
            {
                e.Handled = true;
            }

            return;
        }

        if (restriction == GamePropertyValueRestriction.TrueFalse)
        {
            e.Handled = true;
            return;
        }

        if (e.Text == "{")
        {
            CancelVariableFunctionCompletion();

            if (VariableReferenceListBox.Items.Count == 0)
            {
                return;
            }

            var textBox = VariableValueEditorTextBox;
            var start = textBox.CaretIndex;
            textBox.Text = textBox.Text.Insert(start, "{");
            textBox.CaretIndex = start + 1;
            _variableCompletionStart = textBox.CaretIndex;
            RefreshVariableReferenceFilter();
            e.Handled = true;
            return;
        }

        if (e.Text != "@")
        {
            return;
        }

        var functionTextBox = VariableValueEditorTextBox;
        var functionStart = functionTextBox.CaretIndex;
        functionTextBox.Text = functionTextBox.Text.Insert(functionStart, "@");
        functionTextBox.CaretIndex = functionStart + 1;
        _variableFunctionCompletionStart = functionStart;
        CancelVariableCompletion();
        RefreshVariableFunctionFilter();
        e.Handled = true;
    }

    private void VariableValueEditorTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Unrestricted)
        {
            return;
        }

        if (_variableCompletionStart is not null)
        {
            RefreshVariableReferenceFilter();
        }

        if (_variableFunctionCompletionStart is not null)
        {
            RefreshVariableFunctionFilter();
        }
    }

    private void VariableValueEditorTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Unrestricted)
        {
            return;
        }

        if (_variableCompletionStart is not null)
        {
            if (e.Key == Key.Tab)
            {
                InsertTopVariableReference();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelVariableCompletion();
                e.Handled = true;
                return;
            }
        }

        if (_variableFunctionCompletionStart is not null)
        {
            if (e.Key == Key.Tab)
            {
                InsertTopVariableFunction();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelVariableFunctionCompletion();
                e.Handled = true;
            }
        }
    }

    private void EchoReferenceListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedEchoReference();
    }

    private void EchoReferenceListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedEchoReference();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelEchoCompletion();
            EchoTextEditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void VariableReferenceListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedVariableReference();
    }

    private void VariableReferenceListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedVariableReference();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelVariableCompletion();
            VariableValueEditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void EchoFunctionListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedEchoFunction();
    }

    private void EchoFunctionListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedEchoFunction();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelEchoFunctionCompletion();
            EchoTextEditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void EchoReturnListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedEchoReturnStatement();
    }

    private void EchoReturnListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            InsertSelectedEchoReturnStatement();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelEchoReturnCompletion();
            EchoTextEditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void VariableFunctionListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedVariableFunction();
    }

    private void VariableFunctionListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedVariableFunction();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelVariableFunctionCompletion();
            VariableValueEditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void InsertSelectedEchoReference()
    {
        if (EchoReferenceListBox.SelectedItem is not string token)
        {
            return;
        }

        if (string.Equals(token, EchoPropertyChooserEntry, StringComparison.Ordinal))
        {
            OpenEchoPropertyChooser();
            return;
        }

        InsertEchoReferenceToken(token);
    }

    private void InsertSelectedVariableReference()
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Unrestricted)
        {
            return;
        }

        if (VariableReferenceListBox.SelectedItem is not string token)
        {
            return;
        }

        InsertVariableReferenceToken(token);
    }

    private void InsertSelectedEchoFunction()
    {
        if (EchoFunctionListBox.SelectedItem is not string token)
        {
            return;
        }

        InsertEchoFunctionToken(token);
    }

    private void InsertSelectedVariableFunction()
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Unrestricted)
        {
            return;
        }

        if (VariableFunctionListBox.SelectedItem is not string token)
        {
            return;
        }

        InsertVariableFunctionToken(token);
    }

    private void RefreshEchoReferenceFilter()
    {
        if (_echoCompletionStart is null)
        {
            return;
        }

        var prefix = GetCompletionPrefix(EchoTextEditorTextBox, _echoCompletionStart.Value);
        if (prefix is null)
        {
            CancelEchoCompletion();
            return;
        }

        var filtered = BuildEchoReferenceSuggestions(prefix);
        EchoReferenceListBox.ItemsSource = filtered;
        EchoReferenceListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        EchoReferencePopup.IsOpen = filtered.Count > 0;
    }

    private void RefreshVariableReferenceFilter()
    {
        if (_variableCompletionStart is null)
        {
            return;
        }

        var prefix = GetCompletionPrefix(VariableValueEditorTextBox, _variableCompletionStart.Value);
        if (prefix is null)
        {
            CancelVariableCompletion();
            return;
        }

        var filtered = FilterReferences(_variableReferenceTokens, prefix);
        VariableReferenceListBox.ItemsSource = filtered;
        VariableReferenceListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        VariableReferencePopup.IsOpen = filtered.Count > 0;
    }

    private void RefreshEchoFunctionFilter()
    {
        if (_echoFunctionCompletionStart is null)
        {
            return;
        }

        var prefix = GetFunctionCompletionPrefix(EchoTextEditorTextBox, _echoFunctionCompletionStart.Value);
        if (prefix is null)
        {
            CancelEchoFunctionCompletion();
            return;
        }

        var filtered = FilterFunctionReferences(prefix);
        EchoFunctionListBox.ItemsSource = filtered;
        EchoFunctionListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        EchoFunctionPopup.IsOpen = filtered.Count > 0;
    }

    private void RefreshEchoReturnFilter()
    {
        var context = GetReturnCompletionContext(EchoTextEditorTextBox);
        if (context is null)
        {
            CancelEchoReturnCompletion();
            return;
        }

        _echoReturnCompletionStart = context.Value.CompletionStart;

        var normalizedInput = context.Value.CurrentText.TrimEnd();
        var filtered = EchoReturnSuggestions
            .Where(suggestion => suggestion.StartsWith(normalizedInput, StringComparison.OrdinalIgnoreCase))
            .ToList();

        EchoReturnListBox.ItemsSource = filtered;
        EchoReturnListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        EchoReturnPopup.IsOpen = filtered.Count > 0;

        if (filtered.Count == 0)
        {
            CancelEchoReturnCompletion();
        }
    }

    private void RefreshVariableFunctionFilter()
    {
        if (_variableFunctionCompletionStart is null)
        {
            return;
        }

        var prefix = GetFunctionCompletionPrefix(VariableValueEditorTextBox, _variableFunctionCompletionStart.Value);
        if (prefix is null)
        {
            CancelVariableFunctionCompletion();
            return;
        }

        var filtered = FilterFunctionReferences(prefix);
        VariableFunctionListBox.ItemsSource = filtered;
        VariableFunctionListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        VariableFunctionPopup.IsOpen = filtered.Count > 0;
    }

    private void InsertTopEchoReference()
    {
        var token = EchoReferenceListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (string.Equals(token, EchoPropertyChooserEntry, StringComparison.Ordinal))
        {
            OpenEchoPropertyChooser();
            return;
        }

        InsertEchoReferenceToken(token);
    }

    private void OpenEchoPropertyChooser()
    {
        var selectedValue = OpenVariableChooser("Choose Property", null);
        if (string.IsNullOrWhiteSpace(selectedValue))
        {
            EchoTextEditorTextBox.Focus();
            return;
        }

        InsertEchoReferenceToken(selectedValue);
    }

    private void InsertTopVariableReference()
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Unrestricted)
        {
            return;
        }

        var token = VariableReferenceListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        InsertVariableReferenceToken(token);
    }

    private void InsertTopEchoFunction()
    {
        var token = EchoFunctionListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        InsertEchoFunctionToken(token);
    }

    private void InsertSelectedEchoReturnStatement()
    {
        if (EchoReturnListBox.SelectedItem is not string token)
        {
            return;
        }

        InsertEchoReturnToken(token);
    }

    private void InsertTopEchoReturnStatement()
    {
        var token = EchoReturnListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        InsertEchoReturnToken(token);
    }

    private void InsertTopVariableFunction()
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Unrestricted)
        {
            return;
        }

        var token = VariableFunctionListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        InsertVariableFunctionToken(token);
    }

    private void InsertEchoReferenceToken(string token)
    {
        if (_echoCompletionStart is null)
        {
            return;
        }

        ReplaceCompletionText(EchoTextEditorTextBox, _echoCompletionStart.Value, token);
        CancelEchoCompletion();
        EchoTextEditorTextBox.Focus();
    }

    private void InsertVariableReferenceToken(string token)
    {
        if (_variableCompletionStart is null)
        {
            return;
        }

        ReplaceCompletionText(VariableValueEditorTextBox, _variableCompletionStart.Value, token);
        CancelVariableCompletion();
        VariableValueEditorTextBox.Focus();
    }

    private void InsertEchoFunctionToken(string token)
    {
        if (_echoFunctionCompletionStart is null)
        {
            return;
        }

        ReplaceFunctionCompletionText(EchoTextEditorTextBox, _echoFunctionCompletionStart.Value, token);
        CancelEchoFunctionCompletion();
        EchoTextEditorTextBox.Focus();
    }

    private void InsertEchoReturnToken(string token)
    {
        if (_echoReturnCompletionStart is null)
        {
            return;
        }

        ReplaceReturnCompletionText(EchoTextEditorTextBox, _echoReturnCompletionStart.Value, token);
        CancelEchoReturnCompletion();
        EchoTextEditorTextBox.Focus();
    }

    private void InsertVariableFunctionToken(string token)
    {
        if (_variableFunctionCompletionStart is null)
        {
            return;
        }

        ReplaceFunctionCompletionText(VariableValueEditorTextBox, _variableFunctionCompletionStart.Value, token);
        CancelVariableFunctionCompletion();
        VariableValueEditorTextBox.Focus();
    }

    private static void ReplaceCompletionText(System.Windows.Controls.TextBox textBox, int completionStart, string token)
    {
        if (completionStart < 0 || completionStart > textBox.Text.Length)
        {
            return;
        }

        var caret = textBox.CaretIndex;
        if (caret < completionStart)
        {
            return;
        }

        var length = caret - completionStart;
        var replacement = token + "}";
        textBox.Text = textBox.Text.Remove(completionStart, length).Insert(completionStart, replacement);
        textBox.CaretIndex = completionStart + replacement.Length;
    }

    private static List<string> FilterReferences(IReadOnlyList<string> tokens, string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return tokens.ToList();
        }

        return tokens
            .Where(token => token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<string> BuildEchoReferenceSuggestions(string prefix)
    {
        var items = FilterReferences(GetEchoReferenceTokensForCurrentAction(), prefix);
        items.Insert(0, EchoPropertyChooserEntry);
        return items;
    }

    private static List<string> FilterFunctionReferences(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return BuiltInFunctions.ToList();
        }

        return BuiltInFunctions
            .Where(token => token.StartsWith("@" + prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? GetCompletionPrefix(System.Windows.Controls.TextBox textBox, int completionStart)
    {
        var text = textBox.Text;
        if (completionStart < 0 || completionStart > text.Length)
        {
            return null;
        }

        var caret = textBox.CaretIndex;
        if (caret < completionStart || caret > text.Length)
        {
            return null;
        }

        var prefix = text.Substring(completionStart, caret - completionStart);
        if (prefix.IndexOf('}') >= 0 || prefix.IndexOf('{') >= 0 || prefix.Any(char.IsWhiteSpace))
        {
            return null;
        }

        return prefix;
    }

    private static string? GetFunctionCompletionPrefix(System.Windows.Controls.TextBox textBox, int completionStart)
    {
        var text = textBox.Text;
        if (completionStart < 0 || completionStart >= text.Length)
        {
            return null;
        }

        if (text[completionStart] != '@')
        {
            return null;
        }

        var caret = textBox.CaretIndex;
        if (caret <= completionStart || caret > text.Length)
        {
            return null;
        }

        var prefix = text.Substring(completionStart + 1, caret - completionStart - 1);
        if (prefix.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
        {
            return null;
        }

        return prefix;
    }

    private static void ReplaceFunctionCompletionText(System.Windows.Controls.TextBox textBox, int completionStart, string token)
    {
        if (completionStart < 0 || completionStart >= textBox.Text.Length)
        {
            return;
        }

        var caret = textBox.CaretIndex;
        if (caret < completionStart)
        {
            return;
        }

        var length = caret - completionStart;
        textBox.Text = textBox.Text.Remove(completionStart, length).Insert(completionStart, token);
        textBox.CaretIndex = completionStart + token.Length;
    }

    private static void ReplaceReturnCompletionText(System.Windows.Controls.TextBox textBox, int completionStart, string token)
    {
        if (completionStart < 0 || completionStart > textBox.Text.Length)
        {
            return;
        }

        var caret = textBox.CaretIndex;
        if (caret < completionStart)
        {
            return;
        }

        var length = caret - completionStart;
        textBox.Text = textBox.Text.Remove(completionStart, length).Insert(completionStart, token);
        textBox.CaretIndex = completionStart + token.Length;
    }

    private static (int CompletionStart, string CurrentText)? GetReturnCompletionContext(System.Windows.Controls.TextBox textBox)
    {
        var caret = textBox.CaretIndex;
        var text = textBox.Text;
        if (caret < 0 || caret > text.Length)
        {
            return null;
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var linePrefix = text.Substring(lineStart, caret - lineStart);
        var leadingWhitespaceCount = linePrefix.TakeWhile(char.IsWhiteSpace).Count();
        var trimmedPrefix = linePrefix.Substring(leadingWhitespaceCount);
        if (trimmedPrefix.Length == 0)
        {
            return null;
        }

        if (trimmedPrefix.Any(c => !(char.IsLetter(c) || char.IsWhiteSpace(c))))
        {
            return null;
        }

        if (!"RETURN".StartsWith(trimmedPrefix, StringComparison.OrdinalIgnoreCase)
            && !trimmedPrefix.StartsWith("RETURN", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return (lineStart + leadingWhitespaceCount, trimmedPrefix);
    }

    private void CancelEchoCompletion()
    {
        _echoCompletionStart = null;
        EchoReferencePopup.IsOpen = false;
    }

    private void CancelVariableCompletion()
    {
        _variableCompletionStart = null;
        VariableReferencePopup.IsOpen = false;
    }

    private void CancelEchoFunctionCompletion()
    {
        _echoFunctionCompletionStart = null;
        EchoFunctionPopup.IsOpen = false;
    }

    private void CancelEchoReturnCompletion()
    {
        _echoReturnCompletionStart = null;
        EchoReturnPopup.IsOpen = false;
    }

    private void CancelVariableFunctionCompletion()
    {
        _variableFunctionCompletionStart = null;
        VariableFunctionPopup.IsOpen = false;
    }

    private void VariableValueEditorTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (GetRestrictionForVariable(_workingCopy.GamePropertyName) != GamePropertyValueRestriction.Numeric)
        {
            return;
        }

        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) as string ?? string.Empty;
        var textBox = VariableValueEditorTextBox;
        var candidate = BuildCandidateText(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, pasted);
        if (!IsValidNumericPartial(candidate))
        {
            e.CancelCommand();
        }
    }

    private static string BuildCandidateText(string existing, int selectionStart, int selectionLength, string incoming)
    {
        if (selectionStart < 0 || selectionStart > existing.Length)
        {
            return existing;
        }

        var safeLength = Math.Clamp(selectionLength, 0, existing.Length - selectionStart);
        return existing.Remove(selectionStart, safeLength).Insert(selectionStart, incoming);
    }

    private static bool IsValidNumericPartial(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return NumericPartialPattern.IsMatch(trimmed)
               && (trimmed == "-" || trimmed == "." || trimmed == "-." || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out _));
    }

    private static IReadOnlyList<string> OrderReferenceTokensPreferSelf(IEnumerable<string> tokens)
    {
        return tokens
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Select(static token => token.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static token => token.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase)
                ? 0
                : token.StartsWith("currentAction::action.", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : token.StartsWith("action.", StringComparison.OrdinalIgnoreCase)
                        ? 2
                        : token.StartsWith("self.", StringComparison.OrdinalIgnoreCase)
                            ? 3
                            : 4)
            .ThenBy(static token => token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool ValidateScriptOrShowErrors(string scriptText, string fieldLabel, IReadOnlyList<string>? referenceTokensOverride = null)
    {
        var scopeReferences = referenceTokensOverride ??
            (_workingCopy.ActionType == CommandActionType.EchoMessage
                ? GetEchoReferenceTokensForCurrentAction()
                : _variableReferenceTokens);

        var diagnostics = ActionScriptEditorDiagnosticsAnalyzer.Analyze(scriptText ?? string.Empty, scopeReferences);
        if (!diagnostics.HasBlockingErrors)
        {
            if (diagnostics.Warnings.Count > 0)
            {
                var warningText = string.Join(Environment.NewLine, diagnostics.Warnings.Take(8));
                var warningSuffix = diagnostics.Warnings.Count > 8 ? Environment.NewLine + "..." : string.Empty;
                System.Windows.MessageBox.Show(
                    this,
                    $"{fieldLabel} has non-blocking diagnostics:{Environment.NewLine}{warningText}{warningSuffix}",
                    "Script Diagnostics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return true;
        }

        var errorText = string.Join(Environment.NewLine, diagnostics.Errors.Take(8));
        var suffix = diagnostics.Errors.Count > 8 ? Environment.NewLine + "..." : string.Empty;
        System.Windows.MessageBox.Show(
            this,
            $"{fieldLabel} has script syntax issues:{Environment.NewLine}{errorText}{suffix}",
            "Script Syntax",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }
}

