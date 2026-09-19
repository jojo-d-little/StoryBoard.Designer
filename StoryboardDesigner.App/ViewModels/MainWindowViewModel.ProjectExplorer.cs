using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Execution;
using Storyboard.Shared.GameServices.References;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string GoToBaseObjectActionId = "go-to-base-object";
    private const string PromoteToBaseObjectActionId = "promote-to-base-object";
    private const string CreateTemplateFromObjectActionId = "create-template-from-object";
    private const string CreateTemplateFromRoomActionId = "create-template-from-room";
    private const string AddNewRoomTemplateActionId = "add-new-room-template";
    private const string DeleteRoomTemplateActionId = "delete-room-template";
    private const string AddNewPhaseBookActionId = "add-new-phase-book";
    private const string AddNewPhaseChapterActionId = "add-new-phase-chapter";
    private const string AddNewPhasePageActionId = "add-new-phase-page";
    private const string MovePhaseNodeUpActionId = "move-phase-node-up";
    private const string MovePhaseNodeDownActionId = "move-phase-node-down";
    private const string ReviewPhaseNarrativeActionId = "review-phase-narrative";
    private const string EditPhaseNodeActionId = "edit-phase-node";
    private const string DeletePhaseNodeActionId = "delete-phase-node";
    private const string SetStartingPhasePageActionId = "set-starting-phase-page";
    private const string ClearStartingPhasePageActionId = "clear-starting-phase-page";
    private static Func<GameObject, Exception?>? PromoteToBaseObjectFailureInjection = null;
    private const string EditIgnoredValidationRulesActionId = "edit-ignored-validation-rules";
    private const string EditProjectIgnoredValidationRulesActionId = "edit-project-ignored-validation-rules";
    private const string EditGlobalIgnoredValidationRulesActionId = "edit-global-ignored-validation-rules";
    private const string LeaveSharedVariableActionId = "leave-shared-variable";
    private const string EditScopedActionActionId = "edit-scoped-action";
    private const string EditScopedSoundEffectsActionId = "edit-scoped-sound-effects";
    private const string OpenAllScopedSoundEffectsActionId = "open-all-scoped-sound-effects";
    private const string AddScopedSoundEffectActionId = "add-scoped-sound-effect";
    private const string EditScopedSoundEffectEntryActionId = "edit-scoped-sound-effect-entry";
    private const string EditScopedEventSubscriptionsActionId = "edit-scoped-event-subscriptions";
    private const string AddScopedEventSubscriptionActionId = "add-scoped-event-subscription";
    private const string EditScopedEventSubscriptionEntryActionId = "edit-scoped-event-subscription-entry";
    private const string EditScopedTimerDefinitionsActionId = "edit-scoped-timer-definitions";
    private const string AddScopedTimerDefinitionActionId = "add-scoped-timer-definition";
    private const string EditScopedTimerDefinitionEntryActionId = "edit-scoped-timer-definition-entry";
    private const string EditScopedProceduresActionId = "edit-scoped-procedures";
    private const string AddScopedProcedureActionId = "add-scoped-procedure";
    private const string DesignProceduresActionId = "design-procedures";
    private const string ToggleHideEmptyConfigurationActionId = "toggle-hide-empty-configuration";
    private const string HideEmptyConfigurationGlobalActionId = "hide-empty-configuration-global";
    private const string ShowEmptyConfigurationGlobalActionId = "show-empty-configuration-global";

    internal const string RunValidationActionId = "run-validation";
    internal const string ValidateNodeOnlyActionId = "validate-node-only";
    internal const string ValidateFromHereActionId = "validate-from-here";
    internal const string ValidateFromHereStopOnFirstBlockingActionId = "validate-from-here-stop-on-first-blocking";
    internal const string ValidateWholeProjectActionId = "validate-whole-project";
    internal const string ShowNodeValidationIssuesActionId = "show-node-validation-issues";

    private readonly Dictionary<object, HashSet<string>> _scopedNameIndexes = new(ReferenceEqualityComparer.Instance);

    private RelayCommand _addObjectCommand = null!;
    private RelayCommand _removeObjectCommand = null!;
    private RelayCommandOfT<TreeContextActionRequest> _executeTreeContextActionCommand = null!;

    public ICommand AddObjectCommand => _addObjectCommand;
    public ICommand RemoveObjectCommand => _removeObjectCommand;
    public ICommand ExecuteTreeContextActionCommand => _executeTreeContextActionCommand;

    private void InitializeProjectExplorerCommands()
    {
        _addObjectCommand = new RelayCommand(AddObjectCommandExecute, CanAddObjectCommandExecute);
        _removeObjectCommand = new RelayCommand(RemoveObjectCommandExecute, CanRemoveObjectCommandExecute);
        _executeTreeContextActionCommand = new RelayCommandOfT<TreeContextActionRequest>(ExecuteTreeContextActionCommandExecute, CanExecuteTreeContextActionCommandExecute);
    }

    private void ExecuteTreeContextActionCommandExecute(TreeContextActionRequest? request)
    {
        if (request is null)
        {
            return;
        }

        if (request.ActionId == "add-new-game-property" && request.Node is GamePropertiesContainerNodeViewModel variablesNode)
        {
            if (!_treeContextInteractionService.TryGetNewVariableInput(string.Empty, GamePropertyLifetime.Singleton, "false", GamePropertyValueRestriction.TrueFalse, out var variableName, out var lifetime, out var defaultValue, out var valueRestriction))
            {
                return;
            }

            AddVariableToContainer(variablesNode, variableName, lifetime, defaultValue, valueRestriction);
            return;
        }

        if (request.ActionId == "review-game-properties" && request.Node is GamePropertiesContainerNodeViewModel reviewNode)
        {
            ReviewVariables(reviewNode);
            return;
        }

        ExecuteTreeContextAction(request.ActionId, request.Node);
    }

    private bool CanExecuteTreeContextActionCommandExecute(TreeContextActionRequest? request)
    {
        return request is not null
               && !string.IsNullOrWhiteSpace(request.ActionId)
               && request.Node is not null;
    }

    private void RefreshProjectExplorerCommandStates()
    {
        _addObjectCommand.RaiseCanExecuteChanged();
        _removeObjectCommand.RaiseCanExecuteChanged();
    }

    private void AddObjectCommandExecute()
    {
        AddObjectToCurrentRoom();
    }

    private bool CanAddObjectCommandExecute()
    {
        return SelectedNode is RoomNodeViewModel
               || SelectedNode is PlanetNodeViewModel
               || SelectedNode is CountryNodeViewModel
               || SelectedNode is AreaNodeViewModel
               || SelectedNode is TemplateRoomNodeViewModel
               || SelectedNode is RoomGameObjectsNodeViewModel
               || SelectedNode is PlanetGameObjectsNodeViewModel
               || SelectedNode is CountryGameObjectsNodeViewModel
               || SelectedNode is AreaGameObjectsNodeViewModel
               || SelectedNode is GameObjectNodeViewModel
               || SelectedNode is GlobalObjectsNodeViewModel
               || SelectedNode is GlobalObjectNodeViewModel
               || SelectedNode is ObjectTemplatesNodeViewModel
               || SelectedNode is TemplateGameObjectNodeViewModel
               || SelectedRoom is not null;
    }

    private void RemoveObjectCommandExecute()
    {
        RemoveSelectedGameObject();
    }

    private bool CanRemoveObjectCommandExecute()
    {
        return SelectedNode is GameObjectNodeViewModel
               || SelectedNode is GlobalObjectNodeViewModel
               || SelectedNode is TemplateGameObjectNodeViewModel
               || SelectedGameObject is not null;
    }

    public IReadOnlyList<TreeContextAction> GetTreeContextActions(HierarchyNodeViewModel node)
    {
        var actions = GetNodeSpecificTreeContextActions(node).ToList();
        if (node.HasValidationErrorsOnNode || node.HasValidationWarningsOnNode)
        {
            actions.Add(new TreeContextAction(ShowNodeValidationIssuesActionId, "Show Node Validation Issues"));
        }

        actions.Add(new TreeContextAction(RunValidationActionId, "Run Validation..."));

        return actions;
    }

    private IReadOnlyList<TreeContextAction> GetNodeSpecificTreeContextActions(HierarchyNodeViewModel node)
    {
        if (node is ProjectRootNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("edit-scoped-actions", "Edit Scoped Game Actions"),
                new("edit-scoped-verbs", "Edit Verbs"),
                new("edit-scoped-directionals", "Edit Directionals"),
                new(EditScopedSoundEffectsActionId, "Edit Sound Effects"),
                new(EditScopedEventSubscriptionsActionId, "Edit Event Subscriptions"),
                new(EditScopedTimerDefinitionsActionId, "Edit Timer Definitions"),
                new(AddScopedProcedureActionId, "Add New Procedure"),
                new(EditScopedProceduresActionId, "Edit Procedures"),
                BuildHideEmptyConfigurationContextAction(node),
                new(HideEmptyConfigurationGlobalActionId, "Hide Empty Configuration (Global)"),
                new(ShowEmptyConfigurationGlobalActionId, "Show Empty Configuration (Global)"),
                new(EditProjectIgnoredValidationRulesActionId, "Edit Project Ignored Validation Rules"),
                new(EditGlobalIgnoredValidationRulesActionId, "Edit Global Ignored Validation Rules")
            };
        }

        if (node is GamePropertyNodeViewModel)
        {
            var actions = new List<TreeContextAction>
            {
                new("remove-game-property", "Remove Game Property")
            };

            if (node is GamePropertyNodeViewModel variableNode
                && variableNode.Variable.SharedVariableId is Guid sharedId
                && sharedId != Guid.Empty)
            {
                actions.Add(new TreeContextAction(LeaveSharedVariableActionId, "Leave Share..."));
            }

            return actions;
        }

        if (node is GamePropertiesContainerNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("add-new-game-property", "Add New Game Property"),
                new("review-game-properties", "Edit All Game Properties")
            };
        }

        if (node is ScopedActionsNodeViewModel)
        {
            if (!ShouldShowScopedActionsContextAction(node))
            {
                var lockedActions = new List<TreeContextAction>();
                if (ShouldShowGoToBaseObjectContextAction(node))
                {
                    lockedActions.Add(new TreeContextAction(GoToBaseObjectActionId, "Go to Base Object"));
                }

                return lockedActions;
            }

            return new List<TreeContextAction>
            {
                new("edit-scoped-actions", "Edit All Game Actions")
            };
        }

        if (node is ScopedActionEntryNodeViewModel)
        {
            if (!ShouldShowScopedActionsContextAction(node))
            {
                var lockedActions = new List<TreeContextAction>();
                if (ShouldShowGoToBaseObjectContextAction(node))
                {
                    lockedActions.Add(new TreeContextAction(GoToBaseObjectActionId, "Go to Base Object"));
                }

                return lockedActions;
            }

            return new List<TreeContextAction>
            {
                new(EditScopedActionActionId, "Edit Game Action"),
                new("edit-scoped-actions", "Edit All Game Actions")
            };
        }

        if (node is ScopedVerbsNodeViewModel)
        {
            if (!ShouldShowScopedTokenContextAction(node, "Verbs"))
            {
                var lockedActions = new List<TreeContextAction>();
                if (ShouldShowGoToBaseObjectContextAction(node))
                {
                    lockedActions.Add(new TreeContextAction(GoToBaseObjectActionId, "Go to Base Object"));
                }

                return lockedActions;
            }

            return new List<TreeContextAction>
            {
                new("edit-scoped-verbs", "Edit Verbs")
            };
        }

        if (node is ScopedDirectionalsNodeViewModel)
        {
            if (!ShouldShowScopedTokenContextAction(node, "Directionals"))
            {
                var lockedActions = new List<TreeContextAction>();
                if (ShouldShowGoToBaseObjectContextAction(node))
                {
                    lockedActions.Add(new TreeContextAction(GoToBaseObjectActionId, "Go to Base Object"));
                }

                return lockedActions;
            }

            return new List<TreeContextAction>
            {
                new("edit-scoped-directionals", "Edit Directionals")
            };
        }

        if (node is ScopedSoundEffectsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(AddScopedSoundEffectActionId, "Add New Sound Effect"),
                new(OpenAllScopedSoundEffectsActionId, "Open All Sound Effects")
            };
        }

        if (node is ScopedSoundEffectEntryNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(EditScopedSoundEffectEntryActionId, "Edit Sound Effect"),
                new(OpenAllScopedSoundEffectsActionId, "Open All Sound Effects")
            };
        }

        if (node is ScopedEventSubscriptionsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(AddScopedEventSubscriptionActionId, "Add New Event Subscription"),
                new(EditScopedEventSubscriptionsActionId, "Open All Event Subscriptions")
            };
        }

        if (node is ScopedEventSubscriptionEntryNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(EditScopedEventSubscriptionEntryActionId, "Edit Event Subscription"),
                new(EditScopedEventSubscriptionsActionId, "Open All Event Subscriptions")
            };
        }

        if (node is ScopedTimerDefinitionsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(AddScopedTimerDefinitionActionId, "Add New Timer Definition"),
                new(EditScopedTimerDefinitionsActionId, "Open All Timer Definitions")
            };
        }

        if (node is ScopedTimerDefinitionEntryNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(EditScopedTimerDefinitionEntryActionId, "Edit Timer Definition"),
                new(EditScopedTimerDefinitionsActionId, "Open All Timer Definitions")
            };
        }

        if (node is ScopedProceduresNodeViewModel)
        {
            if (!ShouldShowScopedTokenContextAction(node, "Procedures"))
            {
                var lockedActions = new List<TreeContextAction>();
                if (ShouldShowGoToBaseObjectContextAction(node))
                {
                    lockedActions.Add(new TreeContextAction(GoToBaseObjectActionId, "Go to Base Object"));
                }

                return lockedActions;
            }

            if (node is ScopedProceduresNodeViewModel { Scope: PropertyResolutionScope.Global })
            {
                return new List<TreeContextAction>
                {
                    new(AddScopedProcedureActionId, "Add New Procedure"),
                    new(EditScopedProceduresActionId, "Edit Procedures")
                };
            }

            return new List<TreeContextAction>
            {
                new(AddScopedProcedureActionId, "Add New Procedure"),
                new(EditScopedProceduresActionId, "Edit Procedures")
            };
        }

        if (node is ScopedProcedureEntryNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(EditScopedProceduresActionId, "Edit Procedures")
            };
        }

        if (node is AreaNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("edit-scoped-actions", "Edit Scoped Game Actions"),
                new("edit-scoped-verbs", "Edit Verbs"),
                new("edit-scoped-directionals", "Edit Directionals"),
                new(EditScopedSoundEffectsActionId, "Edit Sound Effects"),
                new(EditScopedEventSubscriptionsActionId, "Edit Event Subscriptions"),
                new(EditScopedTimerDefinitionsActionId, "Edit Timer Definitions"),
                BuildHideEmptyConfigurationContextAction(node),
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new("add-new-room", "Add New Room"),
                new("add-new-object", "Add New Game Object")
            };
        }

        if (node is RoomNodeViewModel)
        {
            var actions = new List<TreeContextAction>
            {
                new("run-traversal-wizard", "Traversal Wizard..."),
                new("edit-scoped-actions", "Edit Scoped Game Actions"),
                new("edit-scoped-verbs", "Edit Verbs"),
                new("edit-scoped-directionals", "Edit Directionals"),
                new(EditScopedSoundEffectsActionId, "Edit Sound Effects"),
                new(EditScopedEventSubscriptionsActionId, "Edit Event Subscriptions"),
                new(EditScopedTimerDefinitionsActionId, "Edit Timer Definitions"),
                BuildHideEmptyConfigurationContextAction(node),
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new("set-starting-room", "Set Starting Room"),
                new("delete-room", "Delete Room"),
                new("add-new-object", "Add New Game Object"),
                new("add-existing-quantifiable-object", "Add Instance of Existing Object")
            };

            actions.Insert(0, new TreeContextAction(CreateTemplateFromRoomActionId, "Create Template From"));
            return actions;
        }

        if (node is TemplateRoomNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("edit-scoped-actions", "Edit Scoped Game Actions"),
                new("edit-scoped-verbs", "Edit Verbs"),
                new("edit-scoped-directionals", "Edit Directionals"),
                new(EditScopedSoundEffectsActionId, "Edit Sound Effects"),
                new(EditScopedEventSubscriptionsActionId, "Edit Event Subscriptions"),
                new(EditScopedTimerDefinitionsActionId, "Edit Timer Definitions"),
                BuildHideEmptyConfigurationContextAction(node),
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new(DeleteRoomTemplateActionId, "Delete Room Template"),
                new("add-new-object", "Add New Game Object"),
                new("add-existing-quantifiable-object", "Add Instance of Existing Object")
            };
        }

        if (node is RoomTraversalLegsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("open-traversal-map", "Open Area Map Designer")
            };
        }

        if (node is PhaseBooksNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(AddNewPhaseBookActionId, "Add New Phase Book")
            };
        }

        if (node is PhaseNodeViewModel phaseNode)
        {
            var actions = new List<TreeContextAction>
            {
                new(EditPhaseNodeActionId, "Edit Phase Details"),
                new(DeletePhaseNodeActionId, "Delete Phase")
            };

            switch (phaseNode.PhaseNode.Tier)
            {
                case PhaseTier.Book:
                    actions.Insert(0, new TreeContextAction(ReviewPhaseNarrativeActionId, "Review Narrative"));
                    actions.Insert(0, new TreeContextAction(AddNewPhaseChapterActionId, "Add New Chapter"));
                    break;
                case PhaseTier.Chapter:
                    AppendPhaseReorderActions(actions, phaseNode);
                    actions.Insert(0, new TreeContextAction(ReviewPhaseNarrativeActionId, "Review Narrative"));
                    actions.Insert(0, new TreeContextAction(AddNewPhasePageActionId, "Add New Page"));
                    break;
                case PhaseTier.Page:
                    AppendPhaseReorderActions(actions, phaseNode);
                    actions.Insert(0, _project.StartingPhasePageId == phaseNode.PhaseNode.Id
                        ? new TreeContextAction(ClearStartingPhasePageActionId, "Clear Starting Phase Page")
                        : new TreeContextAction(SetStartingPhasePageActionId, "Set As Starting Phase Page"));
                    break;
            }

            return actions;
        }

        if (node is TraversalLegNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("edit-scoped-actions", "Edit Leg Game Actions"),
                new("open-traversal-map", "Open Area Map Designer")
            };
        }

        if (node is GlobalObjectsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new("add-new-object", "Add New Game Object")
            };
        }

        if (node is ObjectTemplatesNodeViewModel templatesCatalogNode)
        {
            if (templatesCatalogNode.Parent is TemplateRoomNodeViewModel)
            {
                return new List<TreeContextAction>
                {
                    new("add-new-object", "Add New Game Object"),
                    new("add-existing-quantifiable-object", "Add Instance of Existing Object")
                };
            }

            return new List<TreeContextAction>
            {
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new("add-new-object", templatesCatalogNode.IsBaseCatalog ? "Add New Base Object" : "Add New Object Template")
            };
        }

        if (node is RoomTemplatesNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new(AddNewRoomTemplateActionId, "Add New Room Template")
            };
        }

        if (node is RoomGameObjectsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("add-new-object", "Add New Game Object"),
                new("add-existing-quantifiable-object", "Add Instance of Existing Object")
            };
        }

        if (node is ChildrenGroupNodeViewModel childrenGroup)
        {
            return GetTreeContextActions(childrenGroup.OwnerNode)
                .Where(action => action.ActionId is "add-new-room"
                    or "add-new-object"
                    or "add-existing-quantifiable-object")
                .ToList();
        }

        if (node is GameObjectNodeViewModel or GlobalObjectNodeViewModel or TemplateGameObjectNodeViewModel)
        {
            var actions = new List<TreeContextAction>
            {
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new("add-new-object", node is TemplateGameObjectNodeViewModel templateAddNode
                    ? (templateAddNode.ParentTemplatesNode.IsBaseCatalog ? "Add Contained Base Object" : "Add Contained Template Object")
                    : "Add Contained Game Object"),
                new("remove-object", node is TemplateGameObjectNodeViewModel templateRemoveNode
                    ? (templateRemoveNode.ParentTemplatesNode.IsBaseCatalog ? "Delete Base Object" : "Delete Template Object")
                    : "Delete Game Object")
            };

            if (node is GameObjectNodeViewModel roomObjectNode
                && roomObjectNode.ParentObjectsNode is RoomGameObjectsNodeViewModel)
            {
                actions.Insert(0, new TreeContextAction(CreateTemplateFromObjectActionId, "Create Template From"));

                if (CanPromoteToBaseObject(roomObjectNode))
                {
                    actions.Insert(0, new TreeContextAction(PromoteToBaseObjectActionId, "Promote to Base Object"));
                }
            }

            if (ShouldShowScopedActionsContextAction(node))
            {
                actions.Insert(0, new TreeContextAction("edit-scoped-actions", "Edit Scoped Game Actions"));
            }

            if (ShouldShowScopedTokenContextAction(node, "Directionals"))
            {
                actions.Insert(0, new TreeContextAction("edit-scoped-directionals", "Edit Directionals"));
            }

            if (ShouldShowScopedTokenContextAction(node, "Sound Effects"))
            {
                actions.Insert(0, new TreeContextAction(EditScopedSoundEffectsActionId, "Edit Sound Effects"));
            }

            if (ShouldShowScopedTokenContextAction(node, "Event Subscriptions"))
            {
                actions.Insert(0, new TreeContextAction(EditScopedEventSubscriptionsActionId, "Edit Event Subscriptions"));
            }

            if (ShouldShowScopedTokenContextAction(node, "Timer Definitions"))
            {
                actions.Insert(0, new TreeContextAction(EditScopedTimerDefinitionsActionId, "Edit Timer Definitions"));
            }

            if (ShouldShowScopedTokenContextAction(node, "Procedures"))
            {
                actions.Insert(0, new TreeContextAction(EditScopedProceduresActionId, "Edit Procedures"));
            }

            if (ShouldShowScopedTokenContextAction(node, "Verbs"))
            {
                actions.Insert(0, new TreeContextAction("edit-scoped-verbs", "Edit Verbs"));
            }

            if (ShouldShowGoToBaseObjectContextAction(node))
            {
                actions.Insert(0, new TreeContextAction(GoToBaseObjectActionId, "Go to Base Object"));
            }

            return actions;
        }

        if (node is PlanetNodeViewModel or CountryNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("edit-scoped-actions", "Edit Scoped Game Actions"),
                new("edit-scoped-verbs", "Edit Verbs"),
                new("edit-scoped-directionals", "Edit Directionals"),
                new(EditScopedSoundEffectsActionId, "Edit Sound Effects"),
                new(EditScopedEventSubscriptionsActionId, "Edit Event Subscriptions"),
                new(EditScopedTimerDefinitionsActionId, "Edit Timer Definitions"),
                BuildHideEmptyConfigurationContextAction(node),
                new(EditIgnoredValidationRulesActionId, "Edit Ignored Validation Rules"),
                new("add-new-object", "Add New Game Object")
            };
        }

        if (node is PlanetGameObjectsNodeViewModel or CountryGameObjectsNodeViewModel or AreaGameObjectsNodeViewModel)
        {
            return new List<TreeContextAction>
            {
                new("add-new-object", "Add New Game Object")
            };
        }

        return new List<TreeContextAction>();
    }

    private static TreeContextAction BuildHideEmptyConfigurationContextAction(HierarchyNodeViewModel node)
    {
        var label = IsHideEmptyConfigurationEnabled(node)
            ? "Show Empty Configuration"
            : "Hide Empty Configuration";
        return new TreeContextAction(ToggleHideEmptyConfigurationActionId, label);
    }

    private bool ToggleHideEmptyConfigurationForNode(HierarchyNodeViewModel node)
    {
        if (!SupportsPerNodeHideEmptyConfiguration(node))
        {
            return false;
        }

        var newValue = !IsHideEmptyConfigurationEnabled(node);
        SetHideEmptyConfigurationValue(node, newValue);
        RefreshHideEmptyConfigurationBranch(node);

        NotifyProjectEdited();
        SelectedNode = node;
        ExportStatus = newValue
            ? "Empty configuration groups hidden for selected node."
            : "Empty configuration groups shown for selected node.";
        return true;
    }

    private static bool SupportsPerNodeHideEmptyConfiguration(HierarchyNodeViewModel node)
    {
        return node is ProjectRootNodeViewModel
            or PlanetNodeViewModel
            or CountryNodeViewModel
            or AreaNodeViewModel
            or RoomNodeViewModel
            or TemplateRoomNodeViewModel;
    }

    private static void SetHideEmptyConfigurationValue(HierarchyNodeViewModel node, bool value)
    {
        switch (node)
        {
            case ProjectRootNodeViewModel projectRootNode:
                projectRootNode.Project.HideEmptyConfiguration = value;
                break;
            case PlanetNodeViewModel planetNode:
                planetNode.Planet.HideEmptyConfiguration = value;
                break;
            case CountryNodeViewModel countryNode:
                countryNode.Country.HideEmptyConfiguration = value;
                break;
            case AreaNodeViewModel areaNode:
                areaNode.Area.HideEmptyConfiguration = value;
                break;
            case RoomNodeViewModel roomNode:
                roomNode.Room.HideEmptyConfiguration = value;
                break;
            case TemplateRoomNodeViewModel templateRoomNode:
                templateRoomNode.Room.HideEmptyConfiguration = value;
                break;
        }
    }

    private void RefreshHideEmptyConfigurationBranch(HierarchyNodeViewModel node)
    {
        switch (node)
        {
            case ProjectRootNodeViewModel projectRootNode:
                RebuildProjectRootBranch(projectRootNode);
                break;
            case PlanetNodeViewModel planetNode:
                RebuildPlanetBranch(planetNode);
                break;
            case CountryNodeViewModel countryNode:
                RebuildCountryBranch(countryNode);
                break;
            case AreaNodeViewModel areaNode:
                RebuildAreaBranch(areaNode);
                break;
            case RoomNodeViewModel roomNode:
                RebuildRoomBranch(roomNode);
                break;
            case TemplateRoomNodeViewModel templateRoomNode:
                RebuildTemplateRoomBranch(templateRoomNode);
                break;
        }
    }

    private void RebuildProjectRootBranch(ProjectRootNodeViewModel projectRootNode)
    {
        var globalSettingsNode = projectRootNode.Children.OfType<GlobalSettingsNodeViewModel>().FirstOrDefault()
            ?? CreateGlobalSettingsNode(projectRootNode);
        var globalVariablesNode = projectRootNode.Children.OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateVariablesNode(projectRootNode, _project.GlobalVariables, PropertyResolutionScope.Global);
        var globalActionsNode = projectRootNode.Children.OfType<ScopedActionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateActionsNode(projectRootNode, PropertyResolutionScope.Global, _project.GlobalScope.AvailableActions);
        var globalVerbsNode = projectRootNode.Children.OfType<ScopedVerbsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateVerbsNode(projectRootNode, PropertyResolutionScope.Global, _project.CommandVerbs);
        var globalDirectionalsNode = projectRootNode.Children.OfType<ScopedDirectionalsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateDirectionalsNode(projectRootNode, PropertyResolutionScope.Global, _project.Directionals);
        var globalSoundEffectsNode = projectRootNode.Children.OfType<ScopedSoundEffectsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateSoundEffectsNode(projectRootNode, PropertyResolutionScope.Global, _project.GlobalScope.SoundEffectLibraryEntries);
        var globalEventSubscriptionsNode = projectRootNode.Children.OfType<ScopedEventSubscriptionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateEventSubscriptionsNode(projectRootNode, PropertyResolutionScope.Global, _project.GlobalScope.EventSubscriptions);
        var globalTimerDefinitionsNode = projectRootNode.Children.OfType<ScopedTimerDefinitionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateTimerDefinitionsNode(projectRootNode, PropertyResolutionScope.Global, _project.GlobalScope.TimerDefinitions);
        var globalProceduresNode = projectRootNode.Children.OfType<ScopedProceduresNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Global)
            ?? CreateProceduresNode(projectRootNode, PropertyResolutionScope.Global, _project.ProcedureIds);
        RefreshScopedProcedureNodes(globalProceduresNode, projectRootNode);
        var phaseBooksNode = CreatePhaseBooksNode(projectRootNode);

        var globalObjectsNode = projectRootNode.Children.OfType<GlobalObjectsNodeViewModel>().FirstOrDefault();
        if (globalObjectsNode is null)
        {
            globalObjectsNode = new GlobalObjectsNodeViewModel(_project, projectRootNode);
            foreach (var objectNode in CreateGlobalGameObjectNodes(globalObjectsNode))
            {
                globalObjectsNode.Children.Add(objectNode);
            }
        }

        var objectTemplatesNode = projectRootNode.Children
            .OfType<ObjectTemplatesNodeViewModel>()
            .FirstOrDefault(node => !node.IsBaseCatalog && node.Parent == projectRootNode);
        if (objectTemplatesNode is null)
        {
            objectTemplatesNode = new ObjectTemplatesNodeViewModel(_project, projectRootNode);
            foreach (var objectNode in CreateTemplateObjectNodes(objectTemplatesNode))
            {
                objectTemplatesNode.Children.Add(objectNode);
            }
        }

        var roomTemplatesNode = projectRootNode.Children.OfType<RoomTemplatesNodeViewModel>().FirstOrDefault();
        if (roomTemplatesNode is null)
        {
            roomTemplatesNode = new RoomTemplatesNodeViewModel(_project, projectRootNode);
            foreach (var roomNode in CreateTemplateRoomNodes(roomTemplatesNode))
            {
                roomTemplatesNode.Children.Add(roomNode);
            }
        }

        var baseObjectsNode = projectRootNode.Children
            .OfType<ObjectTemplatesNodeViewModel>()
            .FirstOrDefault(node => node.IsBaseCatalog && node.Parent == projectRootNode);
        if (baseObjectsNode is null)
        {
            baseObjectsNode = new ObjectTemplatesNodeViewModel(_project, projectRootNode, isBaseCatalog: true);
            foreach (var objectNode in CreateTemplateObjectNodes(baseObjectsNode))
            {
                baseObjectsNode.Children.Add(objectNode);
            }
        }

        var planetNodes = projectRootNode.Children.OfType<PlanetNodeViewModel>().ToList();
        projectRootNode.Children.Clear();
        projectRootNode.Children.Add(globalSettingsNode);
        projectRootNode.Children.Add(globalVariablesNode);
        projectRootNode.Children.Add(globalActionsNode);
        projectRootNode.Children.Add(globalVerbsNode);
        projectRootNode.Children.Add(globalDirectionalsNode);
        projectRootNode.Children.Add(globalSoundEffectsNode);
        projectRootNode.Children.Add(globalEventSubscriptionsNode);
        projectRootNode.Children.Add(globalTimerDefinitionsNode);
        projectRootNode.Children.Add(globalProceduresNode);
        projectRootNode.Children.Add(phaseBooksNode);
        projectRootNode.Children.Add(globalObjectsNode);
        projectRootNode.Children.Add(objectTemplatesNode);
        projectRootNode.Children.Add(roomTemplatesNode);
        projectRootNode.Children.Add(baseObjectsNode);
        foreach (var planetNode in planetNodes)
        {
            projectRootNode.Children.Add(planetNode);
        }

        ApplyHideEmptyConfiguration(projectRootNode);
    }

    private void RebuildPlanetBranch(PlanetNodeViewModel planetNode)
    {
        var settingsNode = planetNode.Children.OfType<PlanetSettingsNodeViewModel>().FirstOrDefault()
            ?? CreatePlanetSettingsNode(planetNode);
        var variablesNode = planetNode.Children.OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateVariablesNode(planetNode, planetNode.Planet.Variables, PropertyResolutionScope.Planet);
        var actionsNode = planetNode.Children.OfType<ScopedActionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateActionsNode(planetNode, PropertyResolutionScope.Planet, planetNode.Planet.AvailableActions);
        var verbsNode = planetNode.Children.OfType<ScopedVerbsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateVerbsNode(planetNode, PropertyResolutionScope.Planet, planetNode.Planet.AdditionalVerbs);
        var directionalsNode = planetNode.Children.OfType<ScopedDirectionalsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateDirectionalsNode(planetNode, PropertyResolutionScope.Planet, planetNode.Planet.AdditionalDirectionals);
        var soundEffectsNode = planetNode.Children.OfType<ScopedSoundEffectsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateSoundEffectsNode(planetNode, PropertyResolutionScope.Planet, planetNode.Planet.SoundEffectLibraryEntries);
        var eventSubscriptionsNode = planetNode.Children.OfType<ScopedEventSubscriptionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateEventSubscriptionsNode(planetNode, PropertyResolutionScope.Planet, planetNode.Planet.EventSubscriptions);
        var timerDefinitionsNode = planetNode.Children.OfType<ScopedTimerDefinitionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Planet)
            ?? CreateTimerDefinitionsNode(planetNode, PropertyResolutionScope.Planet, planetNode.Planet.TimerDefinitions);
        var childrenNode = planetNode.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault()
            ?? new ChildrenGroupNodeViewModel(planetNode);

        var baseObjectsNode = planetNode.Children.OfType<ObjectTemplatesNodeViewModel>()
            .FirstOrDefault(node => node.IsBaseCatalog && node.EditableName == "Base Objects");
        if (baseObjectsNode is null)
        {
            baseObjectsNode = new ObjectTemplatesNodeViewModel(
                _project,
                planetNode,
                "Base Objects",
                planetNode.Planet.BaseObjects,
                planetNode.Planet.BaseObjectsIgnoredValidationRuleIds,
                isBaseCatalog: true,
                catalogParentScope: planetNode.Planet);
            foreach (var objectNode in CreateTemplateObjectNodes(baseObjectsNode))
            {
                baseObjectsNode.Children.Add(objectNode);
            }
        }

        var objectsNode = planetNode.Children.OfType<PlanetGameObjectsNodeViewModel>().FirstOrDefault()
            ?? CreateObjectsNode(planetNode);

        planetNode.Children.Clear();
        planetNode.Children.Add(settingsNode);
        planetNode.Children.Add(variablesNode);
        planetNode.Children.Add(actionsNode);
        planetNode.Children.Add(verbsNode);
        planetNode.Children.Add(directionalsNode);
        planetNode.Children.Add(soundEffectsNode);
        planetNode.Children.Add(eventSubscriptionsNode);
        planetNode.Children.Add(timerDefinitionsNode);
        planetNode.Children.Add(childrenNode);
        planetNode.Children.Add(baseObjectsNode);
        planetNode.Children.Add(objectsNode);

        ApplyHideEmptyConfiguration(planetNode);
    }

    private void RebuildCountryBranch(CountryNodeViewModel countryNode)
    {
        var settingsNode = countryNode.Children.OfType<CountrySettingsNodeViewModel>().FirstOrDefault()
            ?? CreateCountrySettingsNode(countryNode);
        var variablesNode = countryNode.Children.OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateVariablesNode(countryNode, countryNode.Country.Variables, PropertyResolutionScope.Country);
        var actionsNode = countryNode.Children.OfType<ScopedActionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateActionsNode(countryNode, PropertyResolutionScope.Country, countryNode.Country.AvailableActions);
        var verbsNode = countryNode.Children.OfType<ScopedVerbsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateVerbsNode(countryNode, PropertyResolutionScope.Country, countryNode.Country.AdditionalVerbs);
        var directionalsNode = countryNode.Children.OfType<ScopedDirectionalsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateDirectionalsNode(countryNode, PropertyResolutionScope.Country, countryNode.Country.AdditionalDirectionals);
        var soundEffectsNode = countryNode.Children.OfType<ScopedSoundEffectsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateSoundEffectsNode(countryNode, PropertyResolutionScope.Country, countryNode.Country.SoundEffectLibraryEntries);
        var eventSubscriptionsNode = countryNode.Children.OfType<ScopedEventSubscriptionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateEventSubscriptionsNode(countryNode, PropertyResolutionScope.Country, countryNode.Country.EventSubscriptions);
        var timerDefinitionsNode = countryNode.Children.OfType<ScopedTimerDefinitionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Country)
            ?? CreateTimerDefinitionsNode(countryNode, PropertyResolutionScope.Country, countryNode.Country.TimerDefinitions);
        var childrenNode = countryNode.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault()
            ?? new ChildrenGroupNodeViewModel(countryNode);

        var baseObjectsNode = countryNode.Children.OfType<ObjectTemplatesNodeViewModel>()
            .FirstOrDefault(node => node.IsBaseCatalog && node.EditableName == "Base Objects");
        if (baseObjectsNode is null)
        {
            baseObjectsNode = new ObjectTemplatesNodeViewModel(
                _project,
                countryNode,
                "Base Objects",
                countryNode.Country.BaseObjects,
                countryNode.Country.BaseObjectsIgnoredValidationRuleIds,
                isBaseCatalog: true,
                catalogParentScope: countryNode.Country);
            foreach (var objectNode in CreateTemplateObjectNodes(baseObjectsNode))
            {
                baseObjectsNode.Children.Add(objectNode);
            }
        }

        var objectsNode = countryNode.Children.OfType<CountryGameObjectsNodeViewModel>().FirstOrDefault()
            ?? CreateObjectsNode(countryNode);

        countryNode.Children.Clear();
        countryNode.Children.Add(settingsNode);
        countryNode.Children.Add(variablesNode);
        countryNode.Children.Add(actionsNode);
        countryNode.Children.Add(verbsNode);
        countryNode.Children.Add(directionalsNode);
        countryNode.Children.Add(soundEffectsNode);
        countryNode.Children.Add(eventSubscriptionsNode);
        countryNode.Children.Add(timerDefinitionsNode);
        countryNode.Children.Add(childrenNode);
        countryNode.Children.Add(baseObjectsNode);
        countryNode.Children.Add(objectsNode);

        ApplyHideEmptyConfiguration(countryNode);
    }

    private void RebuildAreaBranch(AreaNodeViewModel areaNode)
    {
        var settingsNode = areaNode.Children.OfType<AreaSettingsNodeViewModel>().FirstOrDefault()
            ?? CreateAreaSettingsNode(areaNode);
        var variablesNode = areaNode.Children.OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateVariablesNode(areaNode, areaNode.Area.Variables, PropertyResolutionScope.Area);
        var actionsNode = areaNode.Children.OfType<ScopedActionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateActionsNode(areaNode, PropertyResolutionScope.Area, areaNode.Area.AvailableActions);
        var verbsNode = areaNode.Children.OfType<ScopedVerbsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateVerbsNode(areaNode, PropertyResolutionScope.Area, areaNode.Area.AdditionalVerbs);
        var directionalsNode = areaNode.Children.OfType<ScopedDirectionalsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateDirectionalsNode(areaNode, PropertyResolutionScope.Area, areaNode.Area.AdditionalDirectionals);
        var soundEffectsNode = areaNode.Children.OfType<ScopedSoundEffectsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateSoundEffectsNode(areaNode, PropertyResolutionScope.Area, areaNode.Area.SoundEffectLibraryEntries);
        var eventSubscriptionsNode = areaNode.Children.OfType<ScopedEventSubscriptionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateEventSubscriptionsNode(areaNode, PropertyResolutionScope.Area, areaNode.Area.EventSubscriptions);
        var timerDefinitionsNode = areaNode.Children.OfType<ScopedTimerDefinitionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Area)
            ?? CreateTimerDefinitionsNode(areaNode, PropertyResolutionScope.Area, areaNode.Area.TimerDefinitions);
        var childrenNode = areaNode.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault()
            ?? new ChildrenGroupNodeViewModel(areaNode);

        var baseObjectsNode = areaNode.Children.OfType<ObjectTemplatesNodeViewModel>()
            .FirstOrDefault(node => node.IsBaseCatalog && node.EditableName == "Base Objects");
        if (baseObjectsNode is null)
        {
            baseObjectsNode = new ObjectTemplatesNodeViewModel(
                _project,
                areaNode,
                "Base Objects",
                areaNode.Area.BaseObjects,
                areaNode.Area.BaseObjectsIgnoredValidationRuleIds,
                isBaseCatalog: true,
                catalogParentScope: areaNode.Area);
            foreach (var objectNode in CreateTemplateObjectNodes(baseObjectsNode))
            {
                baseObjectsNode.Children.Add(objectNode);
            }
        }

        var objectsNode = areaNode.Children.OfType<AreaGameObjectsNodeViewModel>().FirstOrDefault()
            ?? CreateObjectsNode(areaNode);

        areaNode.Children.Clear();
        areaNode.Children.Add(settingsNode);
        areaNode.Children.Add(variablesNode);
        areaNode.Children.Add(actionsNode);
        areaNode.Children.Add(verbsNode);
        areaNode.Children.Add(directionalsNode);
        areaNode.Children.Add(soundEffectsNode);
        areaNode.Children.Add(eventSubscriptionsNode);
        areaNode.Children.Add(timerDefinitionsNode);
        areaNode.Children.Add(childrenNode);
        areaNode.Children.Add(baseObjectsNode);
        areaNode.Children.Add(objectsNode);

        ApplyHideEmptyConfiguration(areaNode);
    }

    private void RebuildRoomBranch(RoomNodeViewModel roomNode)
    {
        var settingsNode = roomNode.Children.OfType<RoomSettingsNodeViewModel>().FirstOrDefault(node => node.RoomNode is not null)
            ?? CreateRoomSettingsNode(roomNode);
        var variablesNode = roomNode.Children.OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateVariablesNode(roomNode, roomNode.Room.Variables, PropertyResolutionScope.Room);
        var actionsNode = roomNode.Children.OfType<ScopedActionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateActionsNode(roomNode, PropertyResolutionScope.Room, roomNode.Room.AvailableActions);
        var verbsNode = roomNode.Children.OfType<ScopedVerbsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateVerbsNode(roomNode, PropertyResolutionScope.Room, roomNode.Room.AdditionalVerbs);
        var directionalsNode = roomNode.Children.OfType<ScopedDirectionalsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateDirectionalsNode(roomNode, PropertyResolutionScope.Room, roomNode.Room.AdditionalDirectionals);
        var soundEffectsNode = roomNode.Children.OfType<ScopedSoundEffectsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateSoundEffectsNode(roomNode, PropertyResolutionScope.Room, roomNode.Room.SoundEffectLibraryEntries);
        var eventSubscriptionsNode = roomNode.Children.OfType<ScopedEventSubscriptionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateEventSubscriptionsNode(roomNode, PropertyResolutionScope.Room, roomNode.Room.EventSubscriptions);
        var timerDefinitionsNode = roomNode.Children.OfType<ScopedTimerDefinitionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateTimerDefinitionsNode(roomNode, PropertyResolutionScope.Room, roomNode.Room.TimerDefinitions);
        var traversalLegsNode = roomNode.Children.OfType<RoomTraversalLegsNodeViewModel>().FirstOrDefault()
            ?? CreateTraversalLegsNode(roomNode);
        var objectsNode = roomNode.Children.OfType<RoomGameObjectsNodeViewModel>().FirstOrDefault()
            ?? CreateObjectsNode(roomNode);

        roomNode.Children.Clear();
        roomNode.Children.Add(settingsNode);
        roomNode.Children.Add(variablesNode);
        roomNode.Children.Add(actionsNode);
        roomNode.Children.Add(verbsNode);
        roomNode.Children.Add(directionalsNode);
        roomNode.Children.Add(soundEffectsNode);
        roomNode.Children.Add(eventSubscriptionsNode);
        roomNode.Children.Add(timerDefinitionsNode);
        roomNode.Children.Add(traversalLegsNode);
        roomNode.Children.Add(objectsNode);

        ApplyHideEmptyConfiguration(roomNode);
    }

    private void RebuildTemplateRoomBranch(TemplateRoomNodeViewModel templateRoomNode)
    {
        var settingsNode = templateRoomNode.Children.OfType<RoomSettingsNodeViewModel>().FirstOrDefault(node => node.TemplateRoomNode is not null)
            ?? CreateRoomSettingsNode(templateRoomNode);
        var variablesNode = templateRoomNode.Children.OfType<GamePropertiesContainerNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateVariablesNode(templateRoomNode, templateRoomNode.Room.Variables, PropertyResolutionScope.Room);
        var actionsNode = templateRoomNode.Children.OfType<ScopedActionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateActionsNode(templateRoomNode, PropertyResolutionScope.Room, templateRoomNode.Room.AvailableActions);
        var verbsNode = templateRoomNode.Children.OfType<ScopedVerbsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateVerbsNode(templateRoomNode, PropertyResolutionScope.Room, templateRoomNode.Room.AdditionalVerbs);
        var directionalsNode = templateRoomNode.Children.OfType<ScopedDirectionalsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateDirectionalsNode(templateRoomNode, PropertyResolutionScope.Room, templateRoomNode.Room.AdditionalDirectionals);
        var soundEffectsNode = templateRoomNode.Children.OfType<ScopedSoundEffectsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateSoundEffectsNode(templateRoomNode, PropertyResolutionScope.Room, templateRoomNode.Room.SoundEffectLibraryEntries);
        var eventSubscriptionsNode = templateRoomNode.Children.OfType<ScopedEventSubscriptionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateEventSubscriptionsNode(templateRoomNode, PropertyResolutionScope.Room, templateRoomNode.Room.EventSubscriptions);
        var timerDefinitionsNode = templateRoomNode.Children.OfType<ScopedTimerDefinitionsNodeViewModel>()
            .FirstOrDefault(node => node.Scope == PropertyResolutionScope.Room)
            ?? CreateTimerDefinitionsNode(templateRoomNode, PropertyResolutionScope.Room, templateRoomNode.Room.TimerDefinitions);
        var objectsNode = templateRoomNode.Children.OfType<ObjectTemplatesNodeViewModel>().FirstOrDefault(node => node.Parent == templateRoomNode)
            ?? CreateTemplateRoomObjectsNode(templateRoomNode);

        templateRoomNode.Children.Clear();
        templateRoomNode.Children.Add(settingsNode);
        templateRoomNode.Children.Add(variablesNode);
        templateRoomNode.Children.Add(actionsNode);
        templateRoomNode.Children.Add(verbsNode);
        templateRoomNode.Children.Add(directionalsNode);
        templateRoomNode.Children.Add(soundEffectsNode);
        templateRoomNode.Children.Add(eventSubscriptionsNode);
        templateRoomNode.Children.Add(timerDefinitionsNode);
        templateRoomNode.Children.Add(objectsNode);

        ApplyHideEmptyConfiguration(templateRoomNode);
    }

    private static RoomTraversalLegsNodeViewModel CreateTraversalLegsNode(RoomNodeViewModel roomNode)
    {
        var traversalLegsNode = new RoomTraversalLegsNodeViewModel(roomNode);
        var areaRoomLookup = roomNode.Area.Rooms.ToDictionary(room => room.Id, room => room.Name);

        var traversalLegs = roomNode.Area.TraversalConnections
            .Where(connection => connection.RoomAId == roomNode.Room.Id || connection.RoomBId == roomNode.Room.Id)
            .Select(connection =>
            {
                var isFromRoomA = connection.RoomAId == roomNode.Room.Id;
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

        return traversalLegsNode;
    }

    private bool ApplyHideEmptyConfigurationGlobally(bool hideEmptyConfiguration)
    {
        var expandedPaths = EnumerateHierarchyNodes(HierarchyRoots)
            .Where(static node => node.IsExpanded)
            .Select(BuildNodePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedPath = SelectedNode is null ? string.Empty : BuildNodePath(SelectedNode);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            _project.UiState.LastSelectedNodePath = selectedPath;
        }

        ApplyHideEmptyConfigurationToAllSupportedNodes(_project, hideEmptyConfiguration);
        NotifyProjectEdited();
        LoadProjectIntoHierarchy();

        foreach (var node in EnumerateHierarchyNodes(HierarchyRoots))
        {
            if (expandedPaths.Contains(BuildNodePath(node)))
            {
                node.IsExpanded = true;
            }
        }

        ExportStatus = hideEmptyConfiguration
            ? "Global hide-empty configuration applied."
            : "Global show-empty configuration applied.";
        return true;
    }

    private static void ApplyHideEmptyConfigurationToAllSupportedNodes(ProjectModel project, bool hideEmptyConfiguration)
    {
        project.HideEmptyConfiguration = hideEmptyConfiguration;

        foreach (var planet in project.Planets)
        {
            planet.HideEmptyConfiguration = hideEmptyConfiguration;
            ApplyHideEmptyConfigurationToObjects(planet.GameObjects, hideEmptyConfiguration);
            ApplyHideEmptyConfigurationToObjects(planet.BaseObjects, hideEmptyConfiguration);

            foreach (var country in planet.Countries)
            {
                country.HideEmptyConfiguration = hideEmptyConfiguration;
                ApplyHideEmptyConfigurationToObjects(country.GameObjects, hideEmptyConfiguration);
                ApplyHideEmptyConfigurationToObjects(country.BaseObjects, hideEmptyConfiguration);

                foreach (var area in country.Areas)
                {
                    area.HideEmptyConfiguration = hideEmptyConfiguration;
                    ApplyHideEmptyConfigurationToObjects(area.GameObjects, hideEmptyConfiguration);
                    ApplyHideEmptyConfigurationToObjects(area.BaseObjects, hideEmptyConfiguration);

                    foreach (var room in area.Rooms)
                    {
                        room.HideEmptyConfiguration = hideEmptyConfiguration;
                        ApplyHideEmptyConfigurationToObjects(room.GameObjects, hideEmptyConfiguration);
                    }
                }
            }
        }

        foreach (var roomTemplate in project.RoomTemplates)
        {
            roomTemplate.HideEmptyConfiguration = hideEmptyConfiguration;
            ApplyHideEmptyConfigurationToObjects(roomTemplate.GameObjects, hideEmptyConfiguration);
        }

        ApplyHideEmptyConfigurationToObjects(project.GlobalScope.GameObjects, hideEmptyConfiguration);
        ApplyHideEmptyConfigurationToObjects(project.ObjectTemplates, hideEmptyConfiguration);
        ApplyHideEmptyConfigurationToObjects(project.BaseObjects, hideEmptyConfiguration);
    }

    private static void ApplyHideEmptyConfigurationToObjects(IEnumerable<GameObject> objects, bool hideEmptyConfiguration)
    {
        foreach (var gameObject in objects)
        {
            gameObject.HideEmptyConfiguration = hideEmptyConfiguration;
            ApplyHideEmptyConfigurationToObjects(gameObject.ContainedObjects, hideEmptyConfiguration);
        }
    }

    private bool ShouldShowScopedActionsContextAction(HierarchyNodeViewModel node)
    {
        return !TryResolveLockedActionScopeMessage(node, out _);
    }

    private bool ShouldShowScopedTokenContextAction(HierarchyNodeViewModel node, string tokenKind)
    {
        return !TryResolveLockedDefinitionOwnedTokenScopeMessage(node, tokenKind, out _);
    }

    private bool TryResolveLockedDefinitionOwnedTokenScopeMessage(HierarchyNodeViewModel node, string tokenKind, out string message)
    {
        message = string.Empty;

        var gameObject = TryResolveObjectFromNodeOrAncestor(node);
        if (gameObject is null || !IsLinkedRoomInstanceActionScopeLocked(gameObject))
        {
            return false;
        }

        var tokenLabel = string.Equals(tokenKind, "Directionals", StringComparison.Ordinal)
            ? "directionals"
            : string.Equals(tokenKind, "Procedures", StringComparison.Ordinal)
                ? "procedures"
            : string.Equals(tokenKind, "Sound Effects", StringComparison.Ordinal)
                ? "sound effects"
            : string.Equals(tokenKind, "Event Subscriptions", StringComparison.Ordinal)
                ? "event subscriptions"
            : string.Equals(tokenKind, "Timer Definitions", StringComparison.Ordinal)
                ? "timer definitions"
            : "verbs";

        if (TryResolveLinkedBaseObjectScopePath(gameObject, out var basePath))
        {
            message = $"This is a linked item.\n\nBase item path:\n{basePath}\n\nEdit {tokenLabel} on the base item.";
            return true;
        }

        message = $"This is a linked item. Edit {tokenLabel} on the base item.";
        return true;
    }

    private bool ShouldShowGoToBaseObjectContextAction(HierarchyNodeViewModel node)
    {
        var gameObject = TryResolveObjectFromNodeOrAncestor(node);
        return gameObject?.LinkedBaseObjectId.HasValue == true;
    }

    private static GameObject? TryResolveObjectFromNodeOrAncestor(HierarchyNodeViewModel node)
    {
        var direct = node switch
        {
            GameObjectNodeViewModel objectNode => objectNode.GameObject,
            GlobalObjectNodeViewModel playerObjectNode => playerObjectNode.GameObject,
            TemplateGameObjectNodeViewModel templateObjectNode => templateObjectNode.GameObject,
            _ => null
        };

        if (direct is not null)
        {
            return direct;
        }

        return GetAncestry(node)
            .Select(static ancestor => ancestor switch
            {
                GameObjectNodeViewModel objectNode => objectNode.GameObject,
                GlobalObjectNodeViewModel playerObjectNode => playerObjectNode.GameObject,
                TemplateGameObjectNodeViewModel templateObjectNode => templateObjectNode.GameObject,
                _ => null
            })
            .FirstOrDefault(static obj => obj is not null);
    }

    private bool GoToBaseObjectForNode(HierarchyNodeViewModel node)
    {
        var sourceObject = TryResolveObjectFromNodeOrAncestor(node);
        if (sourceObject is null || !sourceObject.LinkedBaseObjectId.HasValue)
        {
            return false;
        }

        var definitionObject = TryResolveDefinitionObject(sourceObject);
        if (definitionObject is null)
        {
            return false;
        }

        var targetNode = EnumerateHierarchyNodes(HierarchyRoots)
            .FirstOrDefault(candidate => candidate switch
            {
                GameObjectNodeViewModel objectNode => objectNode.GameObject.ObjectId,
                GlobalObjectNodeViewModel playerObjectNode => playerObjectNode.GameObject.ObjectId,
                TemplateGameObjectNodeViewModel templateObjectNode => templateObjectNode.GameObject.ObjectId,
                _ => Guid.Empty
            } == definitionObject.ObjectId);

        if (targetNode is null)
        {
            return false;
        }

        ExpandToNode(targetNode);
        SelectedNode = targetNode;
        ExportStatus = $"Navigated to base object '{targetNode.EditableName}'.";
        return true;
    }

    public bool ExecuteTreeContextAction(string actionId, HierarchyNodeViewModel node)
    {
        return actionId switch
        {
            GoToBaseObjectActionId => GoToBaseObjectForNode(node),
            CreateTemplateFromObjectActionId when node is GameObjectNodeViewModel roomObjectNode => CreateTemplateFromObject(roomObjectNode),
            CreateTemplateFromRoomActionId when node is RoomNodeViewModel roomNode => CreateTemplateFromRoom(roomNode),
            PromoteToBaseObjectActionId when node is GameObjectNodeViewModel roomObjectNode => PromoteToBaseObject(roomObjectNode),
            "run-traversal-wizard" when node is RoomNodeViewModel roomNode => RunTraversalWizardForRoom(roomNode),
            ToggleHideEmptyConfigurationActionId => ToggleHideEmptyConfigurationForNode(node),
            HideEmptyConfigurationGlobalActionId => ApplyHideEmptyConfigurationGlobally(true),
            ShowEmptyConfigurationGlobalActionId => ApplyHideEmptyConfigurationGlobally(false),
            "edit-scoped-actions" => EditScopedActionsForNode(node),
            EditScopedActionActionId => EditSingleScopedActionForNode(node),
            "edit-scoped-verbs" => EditScopedVerbsForNode(node),
            "edit-scoped-directionals" => EditScopedDirectionalsForNode(node),
            EditScopedSoundEffectsActionId => EditScopedSoundEffectsForNode(node),
            OpenAllScopedSoundEffectsActionId => EditScopedSoundEffectsForNode(node),
            AddScopedSoundEffectActionId => AddScopedSoundEffectForNode(node),
            EditScopedSoundEffectEntryActionId => EditSingleScopedSoundEffectForNode(node),
            EditScopedEventSubscriptionsActionId => EditScopedEventSubscriptionsForNode(node),
            AddScopedEventSubscriptionActionId => AddScopedEventSubscriptionForNode(node),
            EditScopedEventSubscriptionEntryActionId => EditSingleScopedEventSubscriptionForNode(node),
            EditScopedTimerDefinitionsActionId => EditScopedTimerDefinitionsForNode(node),
            AddScopedTimerDefinitionActionId => AddScopedTimerDefinitionForNode(node),
            EditScopedTimerDefinitionEntryActionId => EditSingleScopedTimerDefinitionForNode(node),
            EditScopedProceduresActionId => EditScopedProceduresForNode(node),
            AddScopedProcedureActionId => AddScopedProcedureForNode(node),
            DesignProceduresActionId => EditProcedureDefinitions(),
            EditIgnoredValidationRulesActionId => EditIgnoredValidationRulesForNode(node),
            EditProjectIgnoredValidationRulesActionId => EditProjectIgnoredValidationRules(),
            EditGlobalIgnoredValidationRulesActionId => EditGlobalIgnoredValidationRules(),
            "open-traversal-map" when node is TraversalLegNodeViewModel legNode => OpenTraversalMapDesigner(legNode),
            "open-traversal-map" when node is RoomTraversalLegsNodeViewModel legsNode => OpenTraversalMapDesigner(legsNode.RoomNode),
            "add-new-game-property" when node is GamePropertiesContainerNodeViewModel variablesNode => AddNewVariable(variablesNode),
            "review-game-properties" when node is GamePropertiesContainerNodeViewModel variablesNode => ReviewVariables(variablesNode),
            "share-game-property" when node is GamePropertyNodeViewModel variableNode => ShareGameProperty(variableNode),
            "review-shared-property-relationships" when node is GamePropertyNodeViewModel variableNode => ReviewSharedPropertyRelationships(variableNode),
            LeaveSharedVariableActionId when node is GamePropertyNodeViewModel variableNode => LeaveSharedVariable(variableNode),
            "remove-game-property" when node is GamePropertyNodeViewModel variableNode => RemoveVariableNode(variableNode),
            "set-starting-room" when node is RoomNodeViewModel roomNode => SetStartingRoom(roomNode),
            "add-new-room" when node is AreaNodeViewModel areaNode => AddNewRoom(areaNode),
            "add-new-room" when node is ChildrenGroupNodeViewModel { OwnerNode: AreaNodeViewModel areaNode } => AddNewRoom(areaNode),
            "delete-room" when node is RoomNodeViewModel roomNode => DeleteRoom(roomNode),
            AddNewPhaseBookActionId when node is PhaseBooksNodeViewModel phaseBooksNode => AddNewPhaseBook(phaseBooksNode),
            AddNewPhaseChapterActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Book } phaseBookNode => AddChildPhaseNode(phaseBookNode, PhaseTier.Chapter),
            AddNewPhasePageActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Chapter } phaseChapterNode => AddChildPhaseNode(phaseChapterNode, PhaseTier.Page),
            MovePhaseNodeUpActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Chapter or PhaseTier.Page } phaseNode => MovePhaseNode(phaseNode, -1),
            MovePhaseNodeDownActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Chapter or PhaseTier.Page } phaseNode => MovePhaseNode(phaseNode, 1),
            ReviewPhaseNarrativeActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Book or PhaseTier.Chapter } phaseNode => ReviewPhaseNarrative(phaseNode),
            EditPhaseNodeActionId when node is PhaseNodeViewModel phaseNode => EditPhaseNodeDetails(phaseNode),
            DeletePhaseNodeActionId when node is PhaseNodeViewModel phaseNode => DeletePhaseNode(phaseNode),
            SetStartingPhasePageActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Page } phasePageNode => SetStartingPhasePage(phasePageNode),
            ClearStartingPhasePageActionId when node is PhaseNodeViewModel { PhaseNode.Tier: PhaseTier.Page } phasePageNode => ClearStartingPhasePage(phasePageNode),
            "add-new-object" when node is PlanetNodeViewModel planetNode => AddNewObject(planetNode),
            "add-new-object" when node is CountryNodeViewModel countryNode => AddNewObject(countryNode),
            "add-new-object" when node is AreaNodeViewModel areaNode => AddNewObject(areaNode),
            "add-new-object" when node is PlanetGameObjectsNodeViewModel objectsNode => AddNewObject(objectsNode.PlanetNode),
            "add-new-object" when node is CountryGameObjectsNodeViewModel objectsNode => AddNewObject(objectsNode.CountryNode),
            "add-new-object" when node is AreaGameObjectsNodeViewModel objectsNode => AddNewObject(objectsNode.AreaNode),
            "add-new-object" when node is RoomNodeViewModel roomNode => AddNewObject(roomNode),
            "add-new-object" when node is RoomGameObjectsNodeViewModel objectsNode => AddNewObject(objectsNode.RoomNode),
            "add-new-object" when node is ObjectTemplatesNodeViewModel { Parent: TemplateRoomNodeViewModel templateRoomNode } => AddNewObject(templateRoomNode),
            "add-new-object" when node is TemplateRoomNodeViewModel templateRoomNode => AddNewObject(templateRoomNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: RoomNodeViewModel roomNode } => AddNewObject(roomNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: TemplateRoomNodeViewModel templateRoomNode } => AddNewObject(templateRoomNode),
            "add-existing-quantifiable-object" when node is RoomNodeViewModel roomNode => AddExistingQuantifiableObject(roomNode),
            "add-existing-quantifiable-object" when node is RoomGameObjectsNodeViewModel objectsNode => AddExistingQuantifiableObject(objectsNode.RoomNode),
            "add-existing-quantifiable-object" when node is ChildrenGroupNodeViewModel { OwnerNode: RoomNodeViewModel roomNode } => AddExistingQuantifiableObject(roomNode),
            "add-existing-quantifiable-object" when node is ObjectTemplatesNodeViewModel { Parent: TemplateRoomNodeViewModel templateRoomNode } => AddExistingQuantifiableObject(templateRoomNode),
            "add-existing-quantifiable-object" when node is ChildrenGroupNodeViewModel { OwnerNode: TemplateRoomNodeViewModel templateRoomNode } => AddExistingQuantifiableObject(templateRoomNode),
            "add-new-object" when node is GlobalObjectsNodeViewModel globalObjectsNode => AddNewGlobalGameObject(globalObjectsNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: GlobalObjectsNodeViewModel globalObjectsNode } => AddNewGlobalGameObject(globalObjectsNode),
            "add-new-object" when node is ObjectTemplatesNodeViewModel templatesNode => AddNewTemplateObject(templatesNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: ObjectTemplatesNodeViewModel templatesNode } => AddNewTemplateObject(templatesNode),
            AddNewRoomTemplateActionId when node is RoomTemplatesNodeViewModel templatesNode => AddNewRoomTemplate(templatesNode),
            DeleteRoomTemplateActionId when node is TemplateRoomNodeViewModel templateRoomNode => DeleteRoomTemplate(templateRoomNode),
            "add-new-object" when node is GameObjectNodeViewModel objectNode => AddNewContainedObject(objectNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: GameObjectNodeViewModel objectNode } => AddNewContainedObject(objectNode),
            "add-new-object" when node is GlobalObjectNodeViewModel objectNode => AddNewContainedGlobalGameObject(objectNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: GlobalObjectNodeViewModel objectNode } => AddNewContainedGlobalGameObject(objectNode),
            "add-new-object" when node is TemplateGameObjectNodeViewModel objectNode => AddNewContainedTemplateObject(objectNode),
            "add-new-object" when node is ChildrenGroupNodeViewModel { OwnerNode: TemplateGameObjectNodeViewModel objectNode } => AddNewContainedTemplateObject(objectNode),
            "remove-object" when node is GameObjectNodeViewModel objectNode => RemoveInteractiveObject(objectNode),
            "remove-object" when node is GlobalObjectNodeViewModel objectNode => RemoveInteractiveObject(objectNode),
            "remove-object" when node is TemplateGameObjectNodeViewModel objectNode => RemoveInteractiveObject(objectNode),
            RunValidationActionId => RunValidationFromContext(node),
            ValidateNodeOnlyActionId => ValidateProjectWithScope(node, ValidationExecutionKind.ScopedNodeOnly),
            ValidateFromHereActionId => ValidateProjectWithScope(node, ValidationExecutionKind.ScopedFromNode),
            ValidateFromHereStopOnFirstBlockingActionId => ValidateProjectWithScope(
                node,
                ValidationExecutionKind.ScopedFromNode,
                completionModeOverride: TreeValidationCompletionMode.StopOnFirstBlocking),
            ValidateWholeProjectActionId => ValidateProjectWithScope(node, ValidationExecutionKind.WholeProject),
            ShowNodeValidationIssuesActionId => ShowValidationIssuesForNode(node),
            _ => false
        };
    }

    private bool AddNewPhaseBook(PhaseBooksNodeViewModel phaseBooksNode)
    {
        var created = CreatePhaseNodeModel(PhaseTier.Book, _project.PhaseBooks.Select(static node => node.PhaseKey));
        _project.PhaseBooks.Add(created);
        return ReloadAndSelectPhaseNode(created.Id, "Added new phase book.");
    }

    private bool AddChildPhaseNode(PhaseNodeViewModel parentNode, PhaseTier tier)
    {
        var created = CreatePhaseNodeModel(tier, EnumerateAllPhaseKeys(_project.PhaseBooks));
        parentNode.PhaseNode.Children.Add(created);
        return ReloadAndSelectPhaseNode(created.Id, $"Added new phase {tier.ToString().ToLowerInvariant()}.");
    }

    private static void AppendPhaseReorderActions(List<TreeContextAction> actions, PhaseNodeViewModel node)
    {
        var siblings = GetPhaseSiblingList(node);
        if (siblings is null)
        {
            return;
        }

        var index = siblings.IndexOf(node.PhaseNode);
        if (index > 0)
        {
            actions.Insert(0, new TreeContextAction(MovePhaseNodeUpActionId, "Move Up"));
        }

        if (index >= 0 && index < siblings.Count - 1)
        {
            actions.Insert(0, new TreeContextAction(MovePhaseNodeDownActionId, "Move Down"));
        }
    }

    private bool MovePhaseNode(PhaseNodeViewModel node, int offset)
    {
        if (offset is not (-1 or 1))
        {
            return false;
        }

        var siblings = GetPhaseSiblingList(node);
        if (siblings is null)
        {
            return false;
        }

        var index = siblings.IndexOf(node.PhaseNode);
        if (index < 0)
        {
            return false;
        }

        var targetIndex = index + offset;
        if (targetIndex < 0 || targetIndex >= siblings.Count)
        {
            return false;
        }

        (siblings[index], siblings[targetIndex]) = (siblings[targetIndex], siblings[index]);

        var directionText = offset < 0 ? "up" : "down";
        return ReloadAndSelectPhaseNode(node.PhaseNode.Id, $"Moved phase {directionText}.");
    }

    private static List<PhaseNode>? GetPhaseSiblingList(PhaseNodeViewModel node)
    {
        return node.Parent switch
        {
            PhaseNodeViewModel parentNode => parentNode.PhaseNode.Children,
            PhaseBooksNodeViewModel => null,
            _ => null
        };
    }

    private IReadOnlyList<string> BuildKnownPhaseAmbientTimerKeySuggestions()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AddRange(HashSet<string> accumulator, IEnumerable<RuntimeTimerDefinitionDto> definitions)
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

        static void AddObjectRanges(HashSet<string> accumulator, IEnumerable<GameObject> objects)
        {
            foreach (var gameObject in EnumerateGameObjectsRecursive(objects))
            {
                AddRange(accumulator, gameObject.TimerDefinitions);
            }
        }

        AddRange(keys, _project.GlobalScope.TimerDefinitions);
        AddObjectRanges(keys, _project.GlobalScope.GameObjects);
        AddObjectRanges(keys, _project.BaseObjects);

        foreach (var templateRoom in _project.RoomTemplates)
        {
            AddRange(keys, templateRoom.TimerDefinitions);
            AddObjectRanges(keys, templateRoom.GameObjects);
        }

        foreach (var planet in _project.Planets)
        {
            AddRange(keys, planet.TimerDefinitions);
            AddObjectRanges(keys, planet.GameObjects);
            AddObjectRanges(keys, planet.BaseObjects);

            foreach (var country in planet.Countries)
            {
                AddRange(keys, country.TimerDefinitions);
                AddObjectRanges(keys, country.GameObjects);
                AddObjectRanges(keys, country.BaseObjects);

                foreach (var area in country.Areas)
                {
                    AddRange(keys, area.TimerDefinitions);
                    AddObjectRanges(keys, area.GameObjects);
                    AddObjectRanges(keys, area.BaseObjects);

                    foreach (var room in area.Rooms)
                    {
                        AddRange(keys, room.TimerDefinitions);
                        AddObjectRanges(keys, room.GameObjects);
                    }
                }
            }
        }

        return keys
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool EditPhaseNodeDetails(PhaseNodeViewModel node)
    {
        var current = new PhaseNodeEditRequest(
            node.PhaseNode.Tier,
            node.PhaseNode.DisplayName,
            node.PhaseNode.PhaseKey,
            node.PhaseNode.Title,
            node.PhaseNode.TitlePresentationCueEffectKey,
            node.PhaseNode.Prologue,
            node.PhaseNode.ProloguePresentationCueEffectKey,
            node.PhaseNode.Narrative,
            node.PhaseNode.NarrativePresentationCueEffectKey,
            node.PhaseNode.PhaseAmbientSoundEffectId,
            node.PhaseNode.PhaseAmbientTimerKey,
            node.PhaseNode.PhaseAmbienceMode);

        if (!_treeContextInteractionService.TryEditPhaseNode(
                current,
                _project.GlobalScope.SoundEffectLibraryEntries,
                _phaseTextPresentationCueCatalogService.GetTextCueOptions(),
                BuildKnownPhaseAmbientTimerKeySuggestions(),
                out var updated))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(updated.PhaseKey))
        {
            _projectUiService.ShowWarning("Phase key is required.", "Phase Settings");
            return false;
        }

        var existingPhaseKeys = EnumerateAllPhaseKeys(_project.PhaseBooks)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        existingPhaseKeys.Remove(node.PhaseNode.PhaseKey);
        if (existingPhaseKeys.Contains(updated.PhaseKey))
        {
            _projectUiService.ShowWarning($"A phase with key '{updated.PhaseKey}' already exists. Choose a unique key.", "Phase Settings");
            return false;
        }

        var displayName = updated.DisplayName.Trim();
        var phaseKey = updated.PhaseKey.Trim();
        var title = string.IsNullOrWhiteSpace(updated.Title) ? null : updated.Title.Trim();
        var titleCueEffectKey = string.IsNullOrWhiteSpace(updated.TitlePresentationCueEffectKey) ? null : updated.TitlePresentationCueEffectKey.Trim();
        var prologue = string.IsNullOrWhiteSpace(updated.Prologue) ? null : updated.Prologue.Trim();
        var prologueCueEffectKey = string.IsNullOrWhiteSpace(updated.ProloguePresentationCueEffectKey) ? null : updated.ProloguePresentationCueEffectKey.Trim();
        var narrative = string.IsNullOrWhiteSpace(updated.Narrative) ? null : updated.Narrative.Trim();
        var narrativeCueEffectKey = string.IsNullOrWhiteSpace(updated.NarrativePresentationCueEffectKey) ? null : updated.NarrativePresentationCueEffectKey.Trim();
        var timerKey = string.IsNullOrWhiteSpace(updated.PhaseAmbientTimerKey) ? null : updated.PhaseAmbientTimerKey.Trim();

        var hasChanges = !string.Equals(node.PhaseNode.DisplayName, displayName, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.PhaseKey, phaseKey, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.Title, title, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.TitlePresentationCueEffectKey, titleCueEffectKey, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.Prologue, prologue, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.ProloguePresentationCueEffectKey, prologueCueEffectKey, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.Narrative, narrative, StringComparison.Ordinal)
            || !string.Equals(node.PhaseNode.NarrativePresentationCueEffectKey, narrativeCueEffectKey, StringComparison.Ordinal)
            || node.PhaseNode.PhaseAmbientSoundEffectId != updated.PhaseAmbientSoundEffectId
            || !string.Equals(node.PhaseNode.PhaseAmbientTimerKey, timerKey, StringComparison.Ordinal)
            || node.PhaseNode.PhaseAmbienceMode != updated.PhaseAmbienceMode;

        if (!hasChanges)
        {
            return false;
        }

        node.PhaseNode.DisplayName = displayName;
        node.PhaseNode.PhaseKey = phaseKey;
        node.PhaseNode.Title = title;
        node.PhaseNode.TitlePresentationCueEffectKey = titleCueEffectKey;
        node.PhaseNode.Prologue = prologue;
        node.PhaseNode.ProloguePresentationCueEffectKey = prologueCueEffectKey;
        node.PhaseNode.Narrative = narrative;
        node.PhaseNode.NarrativePresentationCueEffectKey = narrativeCueEffectKey;
        node.PhaseNode.PhaseAmbientSoundEffectId = updated.PhaseAmbientSoundEffectId;
        node.PhaseNode.PhaseAmbientTimerKey = timerKey;
        node.PhaseNode.PhaseAmbienceMode = updated.PhaseAmbienceMode;

        return ReloadAndSelectPhaseNode(node.PhaseNode.Id, "Updated phase settings.");
    }

    private bool DeletePhaseNode(PhaseNodeViewModel node)
    {
        var removed = false;

        if (node.Parent is PhaseNodeViewModel parentNode)
        {
            removed = parentNode.PhaseNode.Children.Remove(node.PhaseNode);
        }
        else if (node.Parent is PhaseBooksNodeViewModel)
        {
            removed = _project.PhaseBooks.Remove(node.PhaseNode);
        }

        if (!removed)
        {
            return false;
        }

        if (_project.StartingPhasePageId.HasValue && ContainsPhaseId(node.PhaseNode, _project.StartingPhasePageId.Value))
        {
            _project.StartingPhasePageId = null;
        }

        NotifyProjectEdited();
        LoadProjectIntoHierarchy();
        SelectedNode = HierarchyRoots.OfType<ProjectRootNodeViewModel>().FirstOrDefault();
        ExportStatus = "Deleted phase node.";
        return true;
    }

    private bool SetStartingPhasePage(PhaseNodeViewModel node)
    {
        _project.StartingPhasePageId = node.PhaseNode.Id;
        return ReloadAndSelectPhaseNode(node.PhaseNode.Id, "Updated starting phase page.");
    }

    private bool ClearStartingPhasePage(PhaseNodeViewModel node)
    {
        if (_project.StartingPhasePageId != node.PhaseNode.Id)
        {
            return false;
        }

        _project.StartingPhasePageId = null;
        return ReloadAndSelectPhaseNode(node.PhaseNode.Id, "Cleared starting phase page.");
    }

    private bool ReviewPhaseNarrative(PhaseNodeViewModel node)
    {
        if (node.PhaseNode.Tier is not (PhaseTier.Book or PhaseTier.Chapter))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_projectFilePath))
        {
            ExportStatus = "Save the project first before reviewing phase narrative.";
            return false;
        }

        if (_isProjectDirty && !SaveProject())
        {
            return false;
        }

        string reviewPath;
        try
        {
            reviewPath = _jsonExportService.ExportPhaseNarrativeReviewHtml(_projectFilePath, _project);
        }
        catch (Exception ex)
        {
            ExportStatus = $"Phase narrative review export failed: {ex.Message}";
            return false;
        }

        var anchorId = BuildPhaseNarrativeAnchorId(node.PhaseNode.Id);
        var reviewUri = new Uri(reviewPath).AbsoluteUri + "#" + anchorId;
        if (!_projectUiService.TryOpenUriInDefaultBrowser(reviewUri, out var errorMessage))
        {
            ExportStatus = string.IsNullOrWhiteSpace(errorMessage)
                ? "Phase narrative review export succeeded, but opening the browser failed."
                : $"Phase narrative review export succeeded, but opening the browser failed: {errorMessage}";
            return false;
        }

        ExportStatus = $"Opened phase narrative review: {reviewPath}";
        return true;
    }

    private static string BuildPhaseNarrativeAnchorId(Guid phaseNodeId)
    {
        return $"phase-{phaseNodeId:N}";
    }

    private bool ReloadAndSelectPhaseNode(Guid phaseNodeId, string status)
    {
        NotifyProjectEdited();
        LoadProjectIntoHierarchy();

        var selected = EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<PhaseNodeViewModel>()
            .FirstOrDefault(candidate => candidate.PhaseNode.Id == phaseNodeId);

        if (selected is not null)
        {
            ExpandToNode(selected);
            SelectedNode = selected;
        }

        ExportStatus = status;
        return true;
    }

    private static bool ContainsPhaseId(PhaseNode root, Guid id)
    {
        if (root.Id == id)
        {
            return true;
        }

        foreach (var child in root.Children)
        {
            if (ContainsPhaseId(child, id))
            {
                return true;
            }
        }

        return false;
    }

    private static PhaseNode CreatePhaseNodeModel(PhaseTier tier, IEnumerable<string> existingKeys)
    {
        var keyPrefix = tier switch
        {
            PhaseTier.Book => "book",
            PhaseTier.Chapter => "chapter",
            _ => "page"
        };

        var existingKeySet = new HashSet<string>(
            existingKeys
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Select(static key => key.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var index = 1;
        string key;
        do
        {
            key = $"{keyPrefix}-{index}";
            index++;
        }
        while (existingKeySet.Contains(key));

        return new PhaseNode
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            PhaseKey = key,
            DisplayName = $"New {tier}",
            Title = string.Empty,
            Prologue = string.Empty,
            Narrative = string.Empty
        };
    }

    private static IEnumerable<string> EnumerateAllPhaseKeys(IEnumerable<PhaseNode> roots)
    {
        foreach (var root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root.PhaseKey))
            {
                yield return root.PhaseKey;
            }

            foreach (var childKey in EnumerateAllPhaseKeys(root.Children))
            {
                yield return childKey;
            }
        }
    }

    private bool RunValidationFromContext(HierarchyNodeViewModel node)
    {
        var request = new ValidationRunDialogRequest
        {
            ContextNodeLabel = node.EditableName,
            InitialScope = GetLastValidationRunScope(),
            InitialCompletion = GetLastValidationRunCompletion()
        };

        if (!_projectUiService.TryGetValidationRunSelection(request, out var selection))
        {
            return false;
        }

        var executionKind = selection.Scope switch
        {
            ValidationRunScopeOption.NodeOnly => ValidationExecutionKind.ScopedNodeOnly,
            ValidationRunScopeOption.WholeProject => ValidationExecutionKind.WholeProject,
            _ => ValidationExecutionKind.ScopedFromNode
        };

        var completionOverride = selection.Completion == ValidationCompletionMode.StopOnFirstBlocking
            ? TreeValidationCompletionMode.StopOnFirstBlocking
            : TreeValidationCompletionMode.FullReport;

        return ValidateProjectWithScope(node, executionKind, completionOverride);
    }

    private ValidationRunScopeOption GetLastValidationRunScope()
    {
        var lastActionId = _project.UiState.LastTreeValidationActionId?.Trim();
        if (string.Equals(lastActionId, ValidateNodeOnlyActionId, StringComparison.Ordinal))
        {
            return ValidationRunScopeOption.NodeOnly;
        }

        if (string.Equals(lastActionId, ValidateWholeProjectActionId, StringComparison.Ordinal))
        {
            return ValidationRunScopeOption.WholeProject;
        }

        return ValidationRunScopeOption.FromHere;
    }

    private ValidationCompletionMode GetLastValidationRunCompletion()
    {
        var completion = GetLastTreeValidationCompletionMode();
        return completion == TreeValidationCompletionMode.StopOnFirstBlocking
            ? ValidationCompletionMode.StopOnFirstBlocking
            : ValidationCompletionMode.FullReport;
    }

    private bool RunTraversalWizardForRoom(RoomNodeViewModel roomNode)
    {
        SelectedNode = roomNode;

        var request = BuildTraversalWizardRequest(roomNode);
        if (!_treeContextInteractionService.TryReviewTraversalWizard(request, out var result))
        {
            ExportStatus = $"Traversal Wizard canceled for room '{roomNode.Room.Name}'.";
            return true;
        }

        var applyReport = ApplyTraversalWizardSelections(roomNode, result);
        NotifyProjectEdited();

        var sourceRoom = roomNode.Room;
        var sourceRoomName = sourceRoom.Name;
        LoadProjectIntoHierarchy();
        var refreshedRoomNode = FindRoomNode(sourceRoom);
        if (refreshedRoomNode is not null)
        {
            refreshedRoomNode.IsExpanded = true;
            var traversalLegsNode = refreshedRoomNode.Children.OfType<RoomTraversalLegsNodeViewModel>().FirstOrDefault();
            if (traversalLegsNode is not null)
            {
                traversalLegsNode.IsExpanded = true;
            }

            SelectedNode = refreshedRoomNode;
        }

        var summary = $"Traversal Wizard applied for room '{sourceRoomName}': added {applyReport.AddedTraversalCount} traversal(s), created {applyReport.CreatedDoorCount} door object(s), skipped {applyReport.SkippedCount}.";
        ExportStatus = summary;
        AppendOutputConsoleLine($"TraversalWizard.Apply summary: source='{sourceRoomName}', addedTraversals={applyReport.AddedTraversalCount}, createdDoors={applyReport.CreatedDoorCount}, selected={applyReport.SelectedCount}, skipped={applyReport.SkippedCount}.");
        foreach (var skip in applyReport.SkipReasons.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            AppendOutputConsoleLine($"TraversalWizard.Apply skip[{skip.Key}]={skip.Value}");
        }

        _projectUiService.ShowInformation(summary, "Traversal Wizard");
        return true;
    }

    public bool TryRunTraversalWizardForRoom(Room room)
    {
        var roomNode = FindRoomNode(room);
        if (roomNode is null)
        {
            return false;
        }

        return RunTraversalWizardForRoom(roomNode);
    }

    private TraversalWizardApplyReport ApplyTraversalWizardSelections(
        RoomNodeViewModel roomNode,
        TraversalWizardDialogResult result)
    {
        var area = roomNode.Area;
        var sourceRoom = roomNode.Room;
        var choicesByDirection = result.Rows.ToDictionary(choice => choice.Direction);
        var doorTemplateById = _project.ObjectTemplates
            .Where(template => template.ObjectId != Guid.Empty)
            .GroupBy(template => template.ObjectId)
            .ToDictionary(group => group.Key, group => group.First());
        var neighborByDirection = BuildImmediateNeighborMap(sourceRoom, area);

        var pairSet = area.TraversalConnections
            .Select(connection => ToOrderedPair(connection.RoomAId, connection.RoomBId))
            .ToHashSet();

        var addedTraversalCount = 0;
        var createdDoorCount = 0;
        var selectedCount = result.Rows.Count(row => row.IncludeTraversal);
        var skippedCount = 0;
        var skipReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in TraversalWizardDirectionCandidates())
        {
            if (!choicesByDirection.TryGetValue(candidate.Direction, out var choice)
                || !choice.IncludeTraversal)
            {
                continue;
            }

            if (choice.HasExistingTraversal)
            {
                TrackSkip("existing-traversal");
                continue;
            }

            if (!neighborByDirection.TryGetValue(candidate.Direction, out var destinationRoom)
                || destinationRoom is null)
            {
                TrackSkip("no-immediate-neighbor");
                continue;
            }

            var orderedPair = ToOrderedPair(sourceRoom.Id, destinationRoom.Id);
            if (pairSet.Contains(orderedPair))
            {
                TrackSkip("duplicate-pair");
                continue;
            }

            var connection = new TraversalConnection
            {
                RoomAId = orderedPair.A,
                RoomBId = orderedPair.B,
                BaseTraversalDirectionFromA = orderedPair.A == sourceRoom.Id
                    ? ToDirection10(candidate.Direction)
                    : ToDirection10(TraversalWizardApplyUtility.OppositeDirection(candidate.Direction)),
                TraversalAccessMode = TraversalAccessMode.TwoWay,
                OpenStateBindingMode = choice.DoorBehavior == TraversalWizardDoorBehaviorOption.Together
                    ? OpenStateBindingMode.Together
                    : OpenStateBindingMode.Independent
            };

            var sourceLeg = connection.RoomAId == sourceRoom.Id
                ? connection.TraversalStateFromA
                : connection.TraversalStateFromB;
            var destinationLeg = connection.RoomAId == sourceRoom.Id
                ? connection.TraversalStateFromB
                : connection.TraversalStateFromA;

            if (choice.DoorEnabled)
            {
                var passableDefault = choice.DoorState == TraversalWizardDoorDefaultStateOption.Open;

                var sourceDoorResult = CreateTraversalWizardDoorObject(
                    sourceRoom,
                    candidate.Direction,
                    sourceRoom.Name,
                    destinationRoom.Name,
                    choice.DoorState,
                    choice.DoorLockedWhenClosed,
                    ResolveDoorTemplate(choice.DoorTemplateId),
                    ResolveEffectiveRoomCanvasWidth(sourceRoom),
                    ResolveEffectiveRoomCanvasHeight(sourceRoom));
                var destinationDoorResult = CreateTraversalWizardDoorObject(
                    destinationRoom,
                    TraversalWizardApplyUtility.OppositeDirection(candidate.Direction),
                    destinationRoom.Name,
                    sourceRoom.Name,
                    choice.DoorState,
                    choice.DoorLockedWhenClosed,
                    ResolveDoorTemplate(choice.DoorTemplateId),
                    ResolveEffectiveRoomCanvasWidth(destinationRoom),
                    ResolveEffectiveRoomCanvasHeight(destinationRoom));

                var sourceDoor = sourceDoorResult.Door;
                var destinationDoor = destinationDoorResult.Door;

                if (sourceDoorResult.Created)
                {
                    sourceRoom.AddChildScope(sourceDoor);
                    createdDoorCount++;
                }

                if (destinationDoorResult.Created)
                {
                    destinationRoom.AddChildScope(destinationDoor);
                    createdDoorCount++;
                }

                sourceLeg.OpenableObjectId = sourceDoor.ObjectId;
                destinationLeg.OpenableObjectId = destinationDoor.ObjectId;

                var sourcePassableVariable = EnsureTraversalLegVariable(sourceLeg, "isPassable", passableDefault ? "true" : "false");
                var destinationPassableVariable = EnsureTraversalLegVariable(destinationLeg, "isPassable", passableDefault ? "true" : "false");
                sourcePassableVariable.DefaultValue = passableDefault ? "true" : "false";
                destinationPassableVariable.DefaultValue = passableDefault ? "true" : "false";

                var sourceDoorOpenVariable = EnsureObjectBooleanVariable(sourceDoor, "isOpen", passableDefault);
                var destinationDoorOpenVariable = EnsureObjectBooleanVariable(destinationDoor, "isOpen", passableDefault);

                if (choice.DoorBehavior == TraversalWizardDoorBehaviorOption.Together)
                {
                    var shared = CreateTraversalWizardSharedVariable(
                        name: TraversalWizardApplyUtility.BuildTraversalSharedNameTogether(sourceRoom.Name, destinationRoom.Name),
                        defaultValue: passableDefault ? "true" : "false");

                    DetachObjectParticipantIfLinkedElsewhere(sourceDoor, sourceDoorOpenVariable, shared.Id);
                    DetachObjectParticipantIfLinkedElsewhere(destinationDoor, destinationDoorOpenVariable, shared.Id);

                    sourceDoorOpenVariable.SharedVariableId = shared.Id;
                    destinationDoorOpenVariable.SharedVariableId = shared.Id;
                    sourceLeg.SharedVariableId = shared.Id;
                    destinationLeg.SharedVariableId = shared.Id;
                    sourcePassableVariable.SharedVariableId = shared.Id;
                    destinationPassableVariable.SharedVariableId = shared.Id;

                    UpsertSharedParticipant(shared, "object", sourceDoor.ObjectId, "isOpen", null);
                    UpsertSharedParticipant(shared, "object", destinationDoor.ObjectId, "isOpen", null);
                    UpsertSharedParticipant(shared, "traversal-leg", connection.TraversalConnectionId, "isPassable", connection.RoomAId == sourceRoom.Id ? "a2b" : "b2a");
                    UpsertSharedParticipant(shared, "traversal-leg", connection.TraversalConnectionId, "isPassable", connection.RoomAId == sourceRoom.Id ? "b2a" : "a2b");
                }
                else
                {
                    var sourceShared = CreateTraversalWizardSharedVariable(
                        name: TraversalWizardApplyUtility.BuildTraversalSharedNameDirectional(sourceRoom.Name, destinationRoom.Name),
                        defaultValue: passableDefault ? "true" : "false");
                    var destinationShared = CreateTraversalWizardSharedVariable(
                        name: TraversalWizardApplyUtility.BuildTraversalSharedNameDirectional(destinationRoom.Name, sourceRoom.Name),
                        defaultValue: passableDefault ? "true" : "false");

                    DetachObjectParticipantIfLinkedElsewhere(sourceDoor, sourceDoorOpenVariable, sourceShared.Id);
                    DetachObjectParticipantIfLinkedElsewhere(destinationDoor, destinationDoorOpenVariable, destinationShared.Id);

                    sourceDoorOpenVariable.SharedVariableId = sourceShared.Id;
                    sourceLeg.SharedVariableId = sourceShared.Id;
                    sourcePassableVariable.SharedVariableId = sourceShared.Id;
                    destinationDoorOpenVariable.SharedVariableId = destinationShared.Id;
                    destinationLeg.SharedVariableId = destinationShared.Id;
                    destinationPassableVariable.SharedVariableId = destinationShared.Id;

                    UpsertSharedParticipant(sourceShared, "object", sourceDoor.ObjectId, "isOpen", null);
                    UpsertSharedParticipant(sourceShared, "traversal-leg", connection.TraversalConnectionId, "isPassable", connection.RoomAId == sourceRoom.Id ? "a2b" : "b2a");

                    UpsertSharedParticipant(destinationShared, "object", destinationDoor.ObjectId, "isOpen", null);
                    UpsertSharedParticipant(destinationShared, "traversal-leg", connection.TraversalConnectionId, "isPassable", connection.RoomAId == sourceRoom.Id ? "b2a" : "a2b");
                }
            }
            else
            {
                connection.OpenStateBindingMode = OpenStateBindingMode.Independent;
                var sourcePassableVariable = EnsureTraversalLegVariable(sourceLeg, "isPassable", "true");
                var destinationPassableVariable = EnsureTraversalLegVariable(destinationLeg, "isPassable", "true");
                sourceLeg.SharedVariableId = null;
                destinationLeg.SharedVariableId = null;
                sourcePassableVariable.DefaultValue = "true";
                destinationPassableVariable.DefaultValue = "true";
                sourcePassableVariable.SharedVariableId = null;
                destinationPassableVariable.SharedVariableId = null;
            }

            EnsureTraversalLookAction(sourceLeg, destinationRoom.Name);
            EnsureTraversalLookAction(destinationLeg, sourceRoom.Name);

            area.TraversalConnections.Add(connection);
            pairSet.Add(orderedPair);
            addedTraversalCount++;
        }

        return new TraversalWizardApplyReport(
            AddedTraversalCount: addedTraversalCount,
            CreatedDoorCount: createdDoorCount,
            SelectedCount: selectedCount,
            SkippedCount: skippedCount,
            SkipReasons: skipReasons);

        GameObject? ResolveDoorTemplate(Guid? templateId)
        {
            if (!templateId.HasValue || templateId.Value == Guid.Empty)
            {
                return null;
            }

            return doorTemplateById.TryGetValue(templateId.Value, out var template)
                ? template
                : null;
        }

        void TrackSkip(string reason)
        {
            skippedCount++;
            skipReasons[reason] = skipReasons.TryGetValue(reason, out var count) ? count + 1 : 1;
        }
    }

    private Dictionary<Direction, Room> BuildImmediateNeighborMap(Room sourceRoom, Area area)
    {
        var placementByRoomId = area.RoomPlacements
            .Where(placement => placement.RoomId != Guid.Empty)
            .GroupBy(placement => placement.RoomId)
            .ToDictionary(group => group.Key, group => group.First());

        if (!placementByRoomId.TryGetValue(sourceRoom.Id, out var sourcePlacement))
        {
            return new Dictionary<Direction, Room>();
        }

        var sourceCell = ToGridCell(sourcePlacement.X, sourcePlacement.Y);
        var roomById = area.Rooms
            .Where(room => room.Id != Guid.Empty)
            .GroupBy(room => room.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var roomByGridCell = area.RoomPlacements
            .Where(placement => placement.RoomId != Guid.Empty)
            .GroupBy(placement => ToGridCell(placement.X, placement.Y))
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(placement => roomById.TryGetValue(placement.RoomId, out var room) ? room : null)
                    .FirstOrDefault(room => room is not null));

        var map = new Dictionary<Direction, Room>();
        foreach (var candidate in TraversalWizardDirectionCandidates())
        {
            var targetCell = (sourceCell.col + candidate.Dx, sourceCell.row + candidate.Dy);
            if (roomByGridCell.TryGetValue(targetCell, out var room) && room is not null)
            {
                map[candidate.Direction] = room;
            }
        }

        return map;
    }

    private SharedVariableDefinition CreateTraversalWizardSharedVariable(string name, string defaultValue)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "shared-variable" : name.Trim();
        var uniqueName = baseName;
        var suffix = 2;
        while (_project.SharedVariables.Any(existing => string.Equals(existing.Name, uniqueName, StringComparison.OrdinalIgnoreCase)))
        {
            uniqueName = $"{baseName}_{suffix}";
            suffix++;
        }

        var shared = new SharedVariableDefinition
        {
            Id = Guid.NewGuid(),
            Name = uniqueName,
            DefaultValue = defaultValue,
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        _project.SharedVariables.Add(shared);
        return shared;
    }

    private static GamePropertyDefinition EnsureObjectBooleanVariable(GameObject obj, string variableName, bool defaultValue)
    {
        var existing = obj.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.ValueRestriction = GamePropertyValueRestriction.TrueFalse;
            existing.Lifetime = GamePropertyLifetime.Singleton;
            existing.DefaultValue = defaultValue ? "true" : "false";
            return existing;
        }

        var created = new GamePropertyDefinition
        {
            Name = variableName,
            DefaultValue = defaultValue ? "true" : "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Lifetime = GamePropertyLifetime.Singleton
        };
        obj.Variables.Add(created);
        return created;
    }

    private static (GameObject Door, bool Created) CreateTraversalWizardDoorObject(
        Room ownerRoom,
        Direction direction,
        string sourceRoomName,
        string destinationRoomName,
        TraversalWizardDoorDefaultStateOption doorState,
        bool isLockedWhenClosed,
        GameObject? selectedDoorTemplate,
        double roomCanvasWidth,
        double roomCanvasHeight)
    {
        var isOpen = doorState == TraversalWizardDoorDefaultStateOption.Open;
        var baseName = TraversalWizardApplyUtility.BuildDoorBaseName(direction);
        var existingDoor = ownerRoom.GameObjects.FirstOrDefault(obj =>
            string.Equals(obj.Name?.Trim(), baseName, StringComparison.OrdinalIgnoreCase));

        var created = existingDoor is null;
        var door = existingDoor ?? (selectedDoorTemplate is null
            ? new GameObject
            {
                Description = string.Empty,
                Commands = new List<string>(),
                Variables = new List<GamePropertyDefinition>(),
                ContainedObjects = new List<GameObject>()
            }
            : CloneTemplateObject(selectedDoorTemplate));

        if (created)
        {
            ResetHideEmptyConfigurationForNewObject(door);
        }

        if (created)
        {
            door.Name = baseName;

            var rotationOffset = TraversalWizardApplyUtility.ResolveDoorRotationDegrees(direction);
            door.ImageRotationDegrees += rotationOffset;

            var doorFootprint = ResolveTraversalWizardDoorPlacementFootprint(door);
            var doorPlacement = TraversalWizardApplyUtility.ResolveDoorWallCenterPosition(
                direction,
                roomCanvasWidth,
                roomCanvasHeight,
                doorFootprint.Width,
                doorFootprint.Height);
            door.PositionX = doorPlacement.X;
            door.PositionY = doorPlacement.Y;
        }

        door.ProducerNotes = TraversalWizardApplyUtility.BuildDoorProducerNotes(direction, sourceRoomName, destinationRoomName);
        door.IsOpenable = true;
        door.IsOpenDefaultValue = isOpen;
        door.IsLockable = true;
        door.IsLockedDefaultValue = !isOpen && isLockedWhenClosed;
        door.IsInventoriable = false;

        door.ApplyFeatureVariableContract();
        EnsureDoorLookingFromHereVariable(door, direction);
        EnsureDoorDestinationFromHereVariable(door, destinationRoomName);
        return (door, created);
    }

    private static void EnsureDoorLookingFromHereVariable(GameObject door, Direction direction)
    {
        const string variableName = "lookingFromHere";

        var existing = door.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
        var defaultText = ResolveLookingFromHereDefault(direction);

        if (existing is null)
        {
            door.Variables.Add(new GamePropertyDefinition
            {
                Name = variableName,
                DefaultValue = defaultText,
                ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                Lifetime = GamePropertyLifetime.Singleton
            });
            return;
        }

        existing.ValueRestriction = GamePropertyValueRestriction.Unrestricted;
        if (string.IsNullOrWhiteSpace(existing.DefaultValue))
        {
            existing.DefaultValue = defaultText;
        }
    }

    private static string ResolveLookingFromHereDefault(Direction direction)
    {
        var directionText = direction switch
        {
            Direction.North => "north",
            Direction.NorthEast => "northeast",
            Direction.East => "east",
            Direction.SouthEast => "southeast",
            Direction.South => "south",
            Direction.SouthWest => "southwest",
            Direction.West => "west",
            Direction.NorthWest => "northwest",
            _ => "north"
        };

        return $"the {directionText}";
    }

    private static void EnsureDoorDestinationFromHereVariable(GameObject door, string destinationRoomName)
    {
        const string variableName = "destinationFromHere";

        var existing = door.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
        var defaultText = string.IsNullOrWhiteSpace(destinationRoomName)
            ? "Unknown Room"
            : destinationRoomName.Trim();

        if (existing is null)
        {
            door.Variables.Add(new GamePropertyDefinition
            {
                Name = variableName,
                DefaultValue = defaultText,
                ValueRestriction = GamePropertyValueRestriction.Unrestricted,
                Lifetime = GamePropertyLifetime.Singleton
            });
            return;
        }

        existing.ValueRestriction = GamePropertyValueRestriction.Unrestricted;
        if (string.IsNullOrWhiteSpace(existing.DefaultValue))
        {
            existing.DefaultValue = defaultText;
        }
    }

    private static (double Width, double Height) ResolveTraversalWizardDoorPlacementFootprint(GameObject door)
    {
        const double fallbackWidth = 96;
        const double fallbackHeight = 96;

        var resolvedPath = (door.ResolveImagePath(null) ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return (fallbackWidth, fallbackHeight);
        }

        var resolvedScale = door.ResolveImageScale(null);
        if (!double.IsFinite(resolvedScale) || resolvedScale <= 0)
        {
            resolvedScale = 1;
        }

        try
        {
            using var stream = File.OpenRead(resolvedPath);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null || frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            {
                return (fallbackWidth, fallbackHeight);
            }

            var width = frame.PixelWidth * resolvedScale;
            var height = frame.PixelHeight * resolvedScale;
            if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0)
            {
                return (fallbackWidth, fallbackHeight);
            }

            return TraversalWizardApplyUtility.ResolveRotatedFootprint(width, height, door.ImageRotationDegrees);
        }
        catch
        {
            return (fallbackWidth, fallbackHeight);
        }
    }

    private IReadOnlyList<GamePropertyChoiceItem> BuildImageVariantChooserVariableChoices(GameObject ownerObject)
    {
        var choices = GetVariableChoicesForScopeNode(ownerObject, PropertyResolutionScope.Object)
            .Where(static choice => !choice.Value.StartsWith("self.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Keep owner aliases available even if edit workflow entry points bypass current selection synchronization.
        var existingChoiceValues = new HashSet<string>(
            choices.Select(static choice => choice.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var variable in ownerObject.Variables)
        {
            var variableName = variable.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            var ownerToken = $"owner.{variableName}";
            if (!existingChoiceValues.Add(ownerToken))
            {
                continue;
            }

            choices.Add(new GamePropertyChoiceItem
            {
                    Value = ownerToken,
                ScopePath = ownerObject.Name,
                    OwnerVariable = ownerToken,
                Scope = PropertyResolutionScope.Object,
                ValueRestriction = variable.ValueRestriction,
                Priority = 0,
                Relation = GamePropertyChoiceRelation.Self
            });
        }

        if (ownerObject.IsQuantifiable)
        {
            const string ownerQuantityToken = "owner.nearByQuantity";
            if (existingChoiceValues.Add(ownerQuantityToken))
            {
                choices.Add(new GamePropertyChoiceItem
                {
                    Value = ownerQuantityToken,
                    ScopePath = ownerObject.Name,
                    OwnerVariable = ownerQuantityToken,
                    Scope = PropertyResolutionScope.Object,
                    ValueRestriction = GamePropertyValueRestriction.Numeric,
                    Priority = 0,
                    Relation = GamePropertyChoiceRelation.Self
                });
            }
        }

        return choices;
    }

    private static void EnsureTraversalLookAction(TraversalLegState legState, string destinationRoomName)
    {
        var lookEcho = TraversalWizardApplyUtility.BuildTraversalLookEchoMessage(destinationRoomName);
        var existing = legState.AvailableActions.FirstOrDefault(action =>
            action.ActionType == CommandActionType.EchoMessage
            && string.Equals(ActionPayloadAccessors.GetEchoMessage(action), lookEcho, StringComparison.Ordinal));
        if (existing is not null)
        {
            return;
        }

        var lookAction = new CommandAction
        {
            Name = "Look",
            ActionType = CommandActionType.EchoMessage,
            NoVerbLinkage = true
        };

        ActionPayloadAccessors.SetEchoMessage(lookAction, lookEcho);
        legState.AvailableActions.Add(lookAction);
    }

    private readonly record struct TraversalWizardApplyReport(
        int AddedTraversalCount,
        int CreatedDoorCount,
        int SelectedCount,
        int SkippedCount,
        IReadOnlyDictionary<string, int> SkipReasons);

    private TraversalWizardDialogRequest BuildTraversalWizardRequest(RoomNodeViewModel roomNode)
    {
        var area = roomNode.Area;
        var sourceRoom = roomNode.Room;
        var effectiveMode = TraversalModeResolutionUtility.ResolveEffectiveTraversalMode(_project, area, sourceRoom);
        var doorTemplateOptions = _project.ObjectTemplates
            .Where(template => template.ObjectId != Guid.Empty
                               && !string.IsNullOrWhiteSpace(template.Name)
                               && template.Name.Contains("door", StringComparison.OrdinalIgnoreCase))
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .Select(template => new TraversalWizardDoorTemplateOption
            {
                TemplateId = template.ObjectId,
                DisplayName = string.IsNullOrWhiteSpace(template.Name)
                    ? $"Template {template.ObjectId:N}"
                    : template.Name
            })
            .ToList();
        var defaultDoorTemplateId = doorTemplateOptions.FirstOrDefault()?.TemplateId;
        var placementByRoomId = area.RoomPlacements
            .Where(placement => placement.RoomId != Guid.Empty)
            .GroupBy(placement => placement.RoomId)
            .ToDictionary(group => group.Key, group => group.First());

        var roomById = area.Rooms
            .Where(room => room.Id != Guid.Empty)
            .GroupBy(room => room.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var roomByGridCell = area.RoomPlacements
            .Where(placement => placement.RoomId != Guid.Empty)
            .GroupBy(placement => ToGridCell(placement.X, placement.Y))
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(placement => roomById.TryGetValue(placement.RoomId, out var room) ? room : null)
                    .FirstOrDefault(room => room is not null));

        var existingTraversalDirections = BuildExistingTraversalDirectionMap(sourceRoom.Id, area.TraversalConnections, roomById);
        var sourcePlacement = placementByRoomId.TryGetValue(sourceRoom.Id, out var placement)
            ? placement
            : null;

        var rows = new List<TraversalWizardDirectionSeed>();
        foreach (var candidate in TraversalWizardDirectionCandidates())
        {
            var hasExistingTraversal = existingTraversalDirections.TryGetValue(candidate.Direction, out var existingDestinationRoomName);
            var hasImmediateNeighbor = false;
            var destinationRoomName = existingDestinationRoomName ?? string.Empty;

            if (sourcePlacement is not null)
            {
                var sourceCell = ToGridCell(sourcePlacement.X, sourcePlacement.Y);
                var targetCell = (sourceCell.col + candidate.Dx, sourceCell.row + candidate.Dy);
                if (roomByGridCell.TryGetValue(targetCell, out var neighborRoom) && neighborRoom is not null)
                {
                    hasImmediateNeighbor = true;
                    destinationRoomName = neighborRoom.Name;
                }
            }

            var isDiagonal = IsDiagonal(candidate.Direction);
            var canToggleSelection = hasImmediateNeighbor && !hasExistingTraversal;
            var defaultInclude = canToggleSelection;
            string statusText;

            if (hasExistingTraversal)
            {
                statusText = "Traversal already defined - left unchanged.";
                defaultInclude = false;
                canToggleSelection = false;
            }
            else if (!hasImmediateNeighbor)
            {
                statusText = sourcePlacement is null
                    ? "Source room is not placed on the area map."
                    : "No immediate neighboring room in this direction.";
                defaultInclude = false;
                canToggleSelection = false;
            }
            else if (effectiveMode == AreaAdjacencyMode.FourDirectional && isDiagonal)
            {
                statusText = "Diagonal neighbor available. Enable to override 4-way default.";
                defaultInclude = false;
            }
            else
            {
                statusText = "Ready to add traversal.";
            }

            rows.Add(new TraversalWizardDirectionSeed
            {
                Direction = candidate.Direction,
                DirectionLabel = candidate.Label,
                DestinationRoomName = destinationRoomName,
                HasImmediateNeighbor = hasImmediateNeighbor,
                HasExistingTraversal = hasExistingTraversal,
                CanToggleSelection = canToggleSelection,
                DefaultIncludeTraversal = defaultInclude,
                DefaultDoorEnabled = true,
                DefaultDoorBehavior = TraversalWizardDoorBehaviorOption.Together,
                DefaultDoorState = TraversalWizardDoorDefaultStateOption.Closed,
                DefaultDoorLockedWhenClosed = true,
                DefaultDoorTemplateId = defaultDoorTemplateId,
                StatusText = statusText
            });
        }

        return new TraversalWizardDialogRequest
        {
            SourceRoomName = sourceRoom.Name,
            AreaName = area.Name,
            EffectiveTraversalMode = effectiveMode,
            DoorTemplateOptions = doorTemplateOptions,
            Rows = rows
        };
    }

    private static IReadOnlyDictionary<Direction, string> BuildExistingTraversalDirectionMap(
        Guid sourceRoomId,
        IEnumerable<TraversalConnection> traversalConnections,
        IReadOnlyDictionary<Guid, Room> roomById)
    {
        var map = new Dictionary<Direction, string>();
        foreach (var connection in traversalConnections)
        {
            if (connection.RoomAId != sourceRoomId && connection.RoomBId != sourceRoomId)
            {
                continue;
            }

            var sourceIsRoomA = connection.RoomAId == sourceRoomId;
            var direction10 = sourceIsRoomA
                ? connection.BaseTraversalDirectionFromA
                : OppositeDirection(connection.BaseTraversalDirectionFromA);
            if (!TryToEightWayDirection(direction10, out var direction))
            {
                continue;
            }

            if (map.ContainsKey(direction))
            {
                continue;
            }

            var destinationRoomId = sourceIsRoomA
                ? connection.RoomBId
                : connection.RoomAId;
            map[direction] = roomById.TryGetValue(destinationRoomId, out var destinationRoom)
                ? destinationRoom.Name
                : string.Empty;
        }

        return map;
    }

    private static IEnumerable<(Direction Direction, int Dx, int Dy, string Label)> TraversalWizardDirectionCandidates()
    {
        yield return (Direction.North, 0, -1, "N");
        yield return (Direction.NorthEast, 1, -1, "NE");
        yield return (Direction.East, 1, 0, "E");
        yield return (Direction.SouthEast, 1, 1, "SE");
        yield return (Direction.South, 0, 1, "S");
        yield return (Direction.SouthWest, -1, 1, "SW");
        yield return (Direction.West, -1, 0, "W");
        yield return (Direction.NorthWest, -1, -1, "NW");
    }

    private static bool IsDiagonal(Direction direction)
    {
        return direction is Direction.NorthEast or Direction.SouthEast or Direction.SouthWest or Direction.NorthWest;
    }

    private static Direction10 OppositeDirection(Direction10 direction)
    {
        return TraversalWizardApplyUtility.OppositeDirection(direction);
    }

    private static bool TryToEightWayDirection(Direction10 direction, out Direction mappedDirection)
    {
        mappedDirection = direction switch
        {
            Direction10.North => Direction.North,
            Direction10.NorthEast => Direction.NorthEast,
            Direction10.East => Direction.East,
            Direction10.SouthEast => Direction.SouthEast,
            Direction10.South => Direction.South,
            Direction10.SouthWest => Direction.SouthWest,
            Direction10.West => Direction.West,
            Direction10.NorthWest => Direction.NorthWest,
            _ => Direction.North
        };

        return direction is not Direction10.Up and not Direction10.Down;
    }

    private static Direction10 ToDirection10(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction10.North,
            Direction.NorthEast => Direction10.NorthEast,
            Direction.East => Direction10.East,
            Direction.SouthEast => Direction10.SouthEast,
            Direction.South => Direction10.South,
            Direction.SouthWest => Direction10.SouthWest,
            Direction.West => Direction10.West,
            Direction.NorthWest => Direction10.NorthWest,
            _ => Direction10.North
        };
    }

    private static (int col, int row) ToGridCell(double x, double y)
    {
        const double cellWidth = 180;
        const double cellHeight = 140;
        const double gridOriginX = 24;
        const double gridOriginY = 24;

        var col = (int)Math.Round((x - gridOriginX) / cellWidth, MidpointRounding.AwayFromZero);
        var row = (int)Math.Round((y - gridOriginY) / cellHeight, MidpointRounding.AwayFromZero);
        return (Math.Max(0, col), Math.Max(0, row));
    }

    private static (Guid A, Guid B) ToOrderedPair(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }

    private bool OpenTraversalMapDesigner(TraversalLegNodeViewModel legNode)
    {
        OpenAreaEditor(legNode.RoomNode.Area);
        SelectedNode = legNode;
        ExportStatus = $"Opened area map designer for traversal leg '{legNode.Direction}' from room '{legNode.RoomNode.Room.Name}'.";
        return true;
    }

    private bool OpenTraversalMapDesigner(RoomNodeViewModel roomNode)
    {
        OpenAreaEditor(roomNode.Area);
        SelectedNode = roomNode;
        ExportStatus = $"Opened area map designer for room '{roomNode.Room.Name}'.";
        return true;
    }

    private bool EditScopedActionsForNode(HierarchyNodeViewModel node)
    {
        SelectedNode = node;
        EditSelectedNodeScopedActionsExecute();
        return true;
    }

    private bool EditSingleScopedActionForNode(HierarchyNodeViewModel node)
    {
        if (node is not ScopedActionEntryNodeViewModel actionEntryNode)
        {
            return false;
        }

        SelectedNode = actionEntryNode;

        var target = ResolveScopedActionEditTarget(actionEntryNode);
        if (target is null)
        {
            if (TryResolveLockedActionScopeMessage(actionEntryNode, out var lockedMessage))
            {
                ExportStatus = lockedMessage;
                _projectUiService.ShowInformation(lockedMessage, "Linked Actions");
            }

            return false;
        }

        var variableChoices = target.VariableChoices
            ?? (target.ScopeNode is null
                ? Array.Empty<GamePropertyChoiceItem>()
                : GetVariableChoicesForScopeNode(target.ScopeNode, target.VariableScope));
        var echoReferenceTokens = variableChoices
            .Select(choice => choice.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Concat(RuntimeAnchorReferenceTokenCatalog.BuildIntrinsicAnchorTokens())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var containerTargetChoices = GetContainerTargetChoicesForScopeNode(target.ScopeNode);
        var materializeSourceObjectChoices = GetMaterializeSourceObjectChoicesForScopeNode(target.ScopeNode);
        var procedureChoices = GetProcedureChoicesForScopeNode(target.ScopeNode);
        var compositeRecipeChoices = GetCompositeRecipeChoicesForScopeNode(target.ScopeNode);
        var soundEffectChoices = GetSoundEffectChoicesForScopeNode(target.ScopeNode);
        var timerKeySuggestions = GetTimerKeySuggestionsForScopeNode(target.ScopeNode);

        if (!_dialogWorkflowService.EditSingleScopedAction(
                actionEntryNode.Action,
                target.VariableScope,
                variableChoices,
                echoReferenceTokens,
                containerTargetChoices,
                materializeSourceObjectChoices,
                procedureChoices,
                compositeRecipeChoices,
                soundEffectChoices,
                timerKeySuggestions,
                target.Actions.ToList()))
        {
            return false;
        }

        RefreshScopedActionNodesForSelection();
        NotifyProjectEdited();
        return true;
    }

    private bool EditScopedVerbsForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Verbs", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Verbs");
            return false;
        }

        SelectedNode = node;

        if (!TryResolveScopedTokenListTarget(node, tokenKind: "Verbs", out var scopeLabel, out var values))
        {
            return false;
        }

        var inheritedValues = GetInheritedScopedTokenValues(node, "Verbs");

        if (!_treeContextInteractionService.EditScopedTokenList(scopeLabel, "Verbs", values, inheritedValues))
        {
            return false;
        }

        if (string.Equals(scopeLabel, "Global", StringComparison.OrdinalIgnoreCase))
        {
            SyncProjectCommandCatalogsFromModel();
        }

        RefreshScopedVerbNodesForSelection();
        NotifyProjectEdited();
        ExportStatus = "Scoped verbs updated.";
        return true;
    }

    private bool EditScopedDirectionalsForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Directionals", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Directionals");
            return false;
        }

        SelectedNode = node;

        if (!TryResolveScopedTokenListTarget(node, tokenKind: "Directionals", out var scopeLabel, out var values))
        {
            return false;
        }

        if (!TryResolveScopedDirectionalMappingTarget(node, out var mappings))
        {
            return false;
        }

        var inheritedValues = GetInheritedScopedTokenValues(node, "Directionals");

        if (!_treeContextInteractionService.EditScopedTokenList(scopeLabel, "Directionals", values, inheritedValues, mappings))
        {
            return false;
        }

        if (string.Equals(scopeLabel, "Global", StringComparison.OrdinalIgnoreCase))
        {
            SyncProjectCommandCatalogsFromModel();
        }

        RefreshScopedDirectionalNodesForSelection();
        NotifyProjectEdited();
        ExportStatus = "Scoped directionals updated.";
        return true;
    }

    private bool EditScopedSoundEffectsForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Sound Effects", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Sound Effects");
            return false;
        }

        if (!TryResolveScopedSoundEffectLibraryTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var currentEntries = values.ToList();
        var categorySuggestions = GetProjectSoundEffectCategorySuggestions();
        if (!_treeContextInteractionService.TryEditSoundEffectLibrary(currentEntries, categorySuggestions, out var updatedValues))
        {
            return false;
        }

        if (AreSoundEffectLibraryEntriesEquivalent(currentEntries, updatedValues))
        {
            return false;
        }

        values.Clear();
        foreach (var entry in updatedValues)
        {
            values.Add(CloneSoundEffectLibraryEntry(entry));
        }

        RebuildProjectSoundEffectCategorySuggestions();

        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Updated sound effects for {scopeLabel}.";
        return true;
    }

    private bool AddScopedSoundEffectForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Sound Effects", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Sound Effects");
            return false;
        }

        if (!TryResolveScopedSoundEffectLibraryTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var seed = new SoundEffectLibraryEntry
        {
            SoundEffectId = Guid.NewGuid(),
            SoundEffectKey = BuildDefaultSoundEffectKey(values),
            DisplayName = "New Sound",
            Category = "General",
            AssetRef = string.Empty,
            RepeatMode = "None",
            ReplayPolicy = "PlayAgain"
        };

        if (!TryEditSingleSoundEffectEntry(seed, out var created))
        {
            return false;
        }

        values.Add(CloneSoundEffectLibraryEntry(created));
        RebuildProjectSoundEffectCategorySuggestions();
        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Added sound effect to {scopeLabel}.";
        return true;
    }

    private bool EditSingleScopedSoundEffectForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Sound Effects", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Sound Effects");
            return false;
        }

        if (node is not ScopedSoundEffectEntryNodeViewModel entryNode)
        {
            return false;
        }

        if (!TryResolveScopedSoundEffectLibraryTarget(entryNode, out var scopeLabel, out var values))
        {
            return false;
        }

        var selected = entryNode.Entry;
        var selectedIndex = values.IndexOf(selected);
        if (selectedIndex < 0)
        {
            selectedIndex = values
                .Select((value, index) => (value, index))
                .FirstOrDefault(tuple => tuple.value.SoundEffectId == selected.SoundEffectId).index;
        }

        if (selectedIndex < 0 || selectedIndex >= values.Count)
        {
            return false;
        }

        var original = values[selectedIndex];
        if (!TryEditSingleSoundEffectEntry(original, out var updated))
        {
            return false;
        }

        if (AreSoundEffectLibraryEntriesEquivalent(original, updated))
        {
            return false;
        }

        values[selectedIndex] = CloneSoundEffectLibraryEntry(updated);
        RebuildProjectSoundEffectCategorySuggestions();
        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Updated sound effect in {scopeLabel}.";
        return true;
    }

    private bool EditScopedEventSubscriptionsForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Event Subscriptions", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Event Subscriptions");
            return false;
        }

        if (!TryResolveScopedEventSubscriptionTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var currentValues = CloneEventSubscriptions(values.ToList());
        if (!TryEditEventSubscriptions(node, currentValues, out var updatedValues))
        {
            return false;
        }

        if (AreEventSubscriptionsEquivalent(values.ToList(), updatedValues))
        {
            return false;
        }

        values.Clear();
        foreach (var subscription in CloneEventSubscriptions(updatedValues))
        {
            values.Add(subscription);
        }

        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Updated event subscriptions for {scopeLabel}.";
        return true;
    }

    private bool AddScopedEventSubscriptionForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Event Subscriptions", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Event Subscriptions");
            return false;
        }

        if (!TryResolveScopedEventSubscriptionTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var draft = CreateDefaultEventSubscription(values.ToList());
        if (!TryEditSingleEventSubscription(node, draft, out var updatedSubscription))
        {
            return false;
        }

        values.Add(CloneEventSubscription(updatedSubscription));

        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Added event subscription to {scopeLabel}.";
        return true;
    }

    private bool EditSingleScopedEventSubscriptionForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Event Subscriptions", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Event Subscriptions");
            return false;
        }

        if (!TryResolveScopedEventSubscriptionTarget(node, out var scopeLabel, out var values)
            || node is not ScopedEventSubscriptionEntryNodeViewModel entryNode)
        {
            return false;
        }

        var selectedIndex = -1;
        for (var index = 0; index < values.Count; index++)
        {
            if (ReferenceEquals(values[index], entryNode.Entry))
            {
                selectedIndex = index;
                break;
            }
        }

        if (selectedIndex < 0 && entryNode.Entry.Id != Guid.Empty)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index].Id == entryNode.Entry.Id)
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        if (selectedIndex < 0 || selectedIndex >= values.Count)
        {
            return false;
        }

        var original = values[selectedIndex];
        if (!TryEditSingleEventSubscription(node, CloneEventSubscription(original), out var updated))
        {
            return false;
        }

        if (AreEventSubscriptionsEquivalent([original], [updated]))
        {
            return false;
        }

        values[selectedIndex] = CloneEventSubscription(updated);
        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Updated event subscription in {scopeLabel}.";
        return true;
    }

    private bool EditScopedTimerDefinitionsForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Timer Definitions", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Timer Definitions");
            return false;
        }

        if (!TryResolveScopedTimerDefinitionTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var actionRefSuggestions = BuildTimerActionRefSuggestions(node);
        var currentValues = CloneTimerDefinitions(values.ToList());
        var dialog = new Views.TimerDefinitionLibraryDialog(currentValues, actionRefSuggestions)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var updatedValues = dialog.Entries;
        if (AreTimerDefinitionsEquivalent(currentValues, updatedValues))
        {
            return false;
        }

        values.Clear();
        foreach (var timerDefinition in CloneTimerDefinitions(updatedValues))
        {
            values.Add(timerDefinition);
        }

        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Updated timer definitions for {scopeLabel}.";
        return true;
    }

    private bool AddScopedTimerDefinitionForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Timer Definitions", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Timer Definitions");
            return false;
        }

        if (!TryResolveScopedTimerDefinitionTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var actionRefSuggestions = BuildTimerActionRefSuggestions(node);
        var seed = new RuntimeTimerDefinitionDto
        {
            TimerKey = BuildDefaultTimerKey(values),
            ScheduleAfterMs = 1000,
            FireMode = TimerFireMode.OneShot,
            TargetActionRef = "inspect",
            LifetimeOwnerType = TimerOwnerType.Room,
            ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
            Enabled = true
        };

        var dialog = new Views.TimerDefinitionEditorDialog(CloneTimerDefinition(seed), actionRefSuggestions)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        values.Add(CloneTimerDefinition(dialog.Entry));
        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Added timer definition to {scopeLabel}.";
        return true;
    }

    private bool EditSingleScopedTimerDefinitionForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Timer Definitions", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Timer Definitions");
            return false;
        }

        if (!TryResolveScopedTimerDefinitionTarget(node, out var scopeLabel, out var values)
            || node is not ScopedTimerDefinitionEntryNodeViewModel entryNode)
        {
            return false;
        }

        var actionRefSuggestions = BuildTimerActionRefSuggestions(node);
        var selectedIndex = -1;
        for (var index = 0; index < values.Count; index++)
        {
            if (ReferenceEquals(values[index], entryNode.Entry))
            {
                selectedIndex = index;
                break;
            }
        }

        if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(entryNode.Entry.TimerKey))
        {
            selectedIndex = values
                .Select((value, index) => (value, index))
                .FirstOrDefault(tuple => string.Equals(tuple.value.TimerKey, entryNode.Entry.TimerKey, StringComparison.Ordinal)).index;
        }

        if (selectedIndex < 0 || selectedIndex >= values.Count)
        {
            return false;
        }

        var original = values[selectedIndex];
        var dialog = new Views.TimerDefinitionEditorDialog(CloneTimerDefinition(original), actionRefSuggestions)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var updated = dialog.Entry;
        if (AreTimerDefinitionsEquivalent([original], [updated]))
        {
            return false;
        }

        values[selectedIndex] = CloneTimerDefinition(updated);
        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = $"Updated timer definition in {scopeLabel}.";
        return true;
    }

    private IReadOnlyList<string> BuildTimerActionRefSuggestions(HierarchyNodeViewModel contextNode)
    {
        var scopeNode = ResolveScopeNodeForHierarchyNode(contextNode);
        return BuildEventSubscriptionActionNameSuggestions(scopeNode);
    }

    private bool TryEditEventSubscriptions(
        HierarchyNodeViewModel contextNode,
        IReadOnlyList<EventSubscriptionDefinition> source,
        out IReadOnlyList<EventSubscriptionDefinition> updated)
    {
        var scopeNode = ResolveScopeNodeForHierarchyNode(contextNode);
        var eventKeySuggestions = BuildEventSubscriptionEventKeySuggestions(scopeNode);
        var actionNameSuggestions = BuildEventSubscriptionActionNameSuggestions(scopeNode);
        var actionNameToTypes = BuildEventSubscriptionActionTypeLookup(scopeNode);
        var filterVariableSuggestions = BuildEventSubscriptionFilterVariableSuggestions(scopeNode);
        var anchorSuggestions = BuildEventSubscriptionAnchorSuggestions();
        var filterVariableChoices = BuildEventSubscriptionFilterVariableChoices(scopeNode);
        var sourceScopeChoices = BuildEventSubscriptionSourceScopeChoices(scopeNode);
        var soundEffectKeySuggestions = BuildEventSubscriptionSoundEffectKeySuggestions(scopeNode);

        var dialog = new Views.EventSubscriptionLibraryDialog(
            source,
            eventKeySuggestions,
            actionNameSuggestions,
            filterVariableSuggestions,
            anchorSuggestions,
            filterVariableChoices,
            sourceScopeChoices,
            soundEffectKeySuggestions,
            actionNameToTypes)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            updated = source.ToList();
            return false;
        }

        updated = dialog.Entries;
        return true;
    }

    private bool TryEditSingleEventSubscription(
        HierarchyNodeViewModel contextNode,
        EventSubscriptionDefinition source,
        out EventSubscriptionDefinition updated)
    {
        var scopeNode = ResolveScopeNodeForHierarchyNode(contextNode);
        var eventKeySuggestions = BuildEventSubscriptionEventKeySuggestions(scopeNode);
        var actionNameSuggestions = BuildEventSubscriptionActionNameSuggestions(scopeNode);
        var actionNameToTypes = BuildEventSubscriptionActionTypeLookup(scopeNode);
        var filterVariableSuggestions = BuildEventSubscriptionFilterVariableSuggestions(scopeNode);
        var anchorSuggestions = BuildEventSubscriptionAnchorSuggestions();
        var filterVariableChoices = BuildEventSubscriptionFilterVariableChoices(scopeNode);
        var sourceScopeChoices = BuildEventSubscriptionSourceScopeChoices(scopeNode);
        var soundEffectKeySuggestions = BuildEventSubscriptionSoundEffectKeySuggestions(scopeNode);

        var dialog = new Views.EventSubscriptionEditorDialog(
            CloneEventSubscription(source),
            eventKeySuggestions,
            actionNameSuggestions,
            filterVariableSuggestions,
            anchorOptions: anchorSuggestions,
            filterVariableChoices: filterVariableChoices,
            sourceScopeChoices: sourceScopeChoices,
            soundEffectKeySuggestions: soundEffectKeySuggestions,
            actionNameToTypes: actionNameToTypes)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            updated = source;
            return false;
        }

        updated = CloneEventSubscription(dialog.Entry);
        return true;
    }

    private const string EventPayloadManifestRelativePath = "Config\\event-payload.manifest.json";

    private IReadOnlyList<string> BuildEventSubscriptionEventKeySuggestions(ScopeNodeBase? scopeNode)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                keys.Add(normalized);
            }
        }

        void AddRange(IEnumerable<EventSubscriptionDefinition> subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                Add(subscription.EventKey);
            }
        }

        foreach (var baseline in LoadEventKeysFromManifest())
        {
            Add(baseline);
        }

        AddRange(_project.GlobalScope.EventSubscriptions);

        foreach (var planet in _project.Planets)
        {
            AddRange(planet.EventSubscriptions);

            foreach (var country in planet.Countries)
            {
                AddRange(country.EventSubscriptions);

                foreach (var area in country.Areas)
                {
                    AddRange(area.EventSubscriptions);

                    foreach (var room in area.Rooms)
                    {
                        AddRange(room.EventSubscriptions);
                    }
                }
            }
        }

        foreach (var roomTemplate in _project.RoomTemplates)
        {
            AddRange(roomTemplate.EventSubscriptions);
        }

        foreach (var gameObject in EnumerateAllProjectGameObjects())
        {
            AddRange(gameObject.EventSubscriptions);
        }

        if (scopeNode is not null)
        {
            AddRange(scopeNode.EventSubscriptions);
        }

        return keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> LoadEventKeysFromManifest()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, EventPayloadManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("events", out var eventsElement)
                || eventsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var eventElement in eventsElement.EnumerateArray())
            {
                if (!eventElement.TryGetProperty("eventKey", out var keyElement)
                    || keyElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var eventKey = keyElement.GetString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(eventKey))
                {
                    keys.Add(eventKey);
                }
            }

            return keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private IReadOnlyList<string> BuildEventSubscriptionActionNameSuggestions(ScopeNodeBase? scopeNode)
    {
        var effectiveScopeNode = scopeNode ?? _project;
        return _eventSubscriptionActionNameSuggestionDiscoveryService.DiscoverLikelyActionNames(effectiveScopeNode);
    }

    private IReadOnlyDictionary<string, IReadOnlyList<CommandActionType>> BuildEventSubscriptionActionTypeLookup(ScopeNodeBase? scopeNode)
    {
        var ranked = new Dictionary<string, (int Distance, HashSet<CommandActionType> Types)>(StringComparer.OrdinalIgnoreCase);
        var effectiveScopeNode = scopeNode ?? _project;
        var scopeDistance = 0;

        foreach (var node in effectiveScopeNode.EnumerateSelfAndAncestors())
        {
            foreach (var action in EnumerateActionsForScopeNode(node))
            {
                var actionName = action.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                if (!ranked.TryGetValue(actionName, out var entry))
                {
                    ranked[actionName] = (scopeDistance, new HashSet<CommandActionType> { action.ActionType });
                    continue;
                }

                if (scopeDistance < entry.Distance)
                {
                    ranked[actionName] = (scopeDistance, new HashSet<CommandActionType> { action.ActionType });
                    continue;
                }

                if (scopeDistance == entry.Distance)
                {
                    entry.Types.Add(action.ActionType);
                    ranked[actionName] = entry;
                }
            }

            scopeDistance++;
        }

        return ranked
            .OrderBy(static pair => pair.Value.Distance)
            .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<CommandActionType>)pair.Value.Types
                    .OrderBy(static actionType => actionType.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<CommandAction> EnumerateActionsForScopeNode(IScopedAwareNode node)
    {
        return node switch
        {
            ProjectModel project => project.GlobalScope.AvailableActions,
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            Room room => room.AvailableActions,
            GameObject gameObject => gameObject.AvailableActions,
            _ => Array.Empty<CommandAction>()
        };
    }

    private IReadOnlyList<string> BuildEventSubscriptionAnchorSuggestions()
    {
        return LoadAnchorSuggestionsFromManifest();
    }

    private static IReadOnlyList<string> LoadAnchorSuggestionsFromManifest()
    {
        try
        {
            return new RuntimeSessionAnchorManifestReader()
                .Load()
                .GetOrderedAnchorKeys();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private IReadOnlyList<GamePropertyChoiceItem> BuildEventSubscriptionFilterVariableChoices(ScopeNodeBase? scopeNode)
    {
        var effectiveScopeNode = scopeNode ?? _project;
        if (effectiveScopeNode is ProjectModel)
        {
            return BuildProjectWideEventSubscriptionFilterVariableChoices();
        }

        var resolutionScope = effectiveScopeNode switch
        {
            Planet => PropertyResolutionScope.Planet,
            Country => PropertyResolutionScope.Country,
            Area => PropertyResolutionScope.Area,
            Room => PropertyResolutionScope.Room,
            GameObject => PropertyResolutionScope.Object,
            _ => PropertyResolutionScope.Global
        };

        return GetVariableChoicesForScopeNode(effectiveScopeNode, resolutionScope)
            .OrderBy(static choice => choice.Priority)
            .ThenBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<ScopeObjectChoiceItem> BuildEventSubscriptionSourceScopeChoices(ScopeNodeBase? scopeNode)
    {
        var effectiveScopeNode = scopeNode ?? _project;
        var allScopes = EnumerateScopeNodesForEventSourcePicker().ToList();
        var map = new Dictionary<Guid, ScopeObjectChoiceItem>();

        foreach (var candidate in allScopes)
        {
            var scopeNodeId = TryResolveScopeNodeId(candidate);
            if (!scopeNodeId.HasValue || scopeNodeId.Value == Guid.Empty)
            {
                continue;
            }

            var relation = ClassifyScopeRelationship(effectiveScopeNode, candidate);

            var key = scopeNodeId.Value.ToString("D");
            if (map.TryGetValue(scopeNodeId.Value, out var existing))
            {
                map[scopeNodeId.Value] = new ScopeObjectChoiceItem
                {
                    Key = existing.Key,
                    DisplayName = existing.DisplayName,
                    ScopePath = existing.ScopePath,
                    OwnerContext = existing.OwnerContext,
                    Scope = existing.Scope,
                    Relations = existing.Relations
                        .Concat(new[] { relation })
                        .Distinct()
                        .ToList()
                };
                continue;
            }

            map[scopeNodeId.Value] = new ScopeObjectChoiceItem
            {
                Key = key,
                DisplayName = BuildEventSubscriptionScopeChoiceDisplayName(candidate),
                ScopePath = BuildScopePathFromNode(candidate),
                OwnerContext = BuildEventSubscriptionScopeChoiceOwnerContext(effectiveScopeNode, candidate, relation),
                Scope = ToPropertyResolutionScope(candidate),
                Relations = new List<GamePropertyChoiceRelation> { relation }
            };
        }

        return map
            .Values
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<ScopeNodeBase> EnumerateScopeNodesForEventSourcePicker()
    {
        foreach (var planet in _project.Planets)
        {
            yield return planet;

            foreach (var country in planet.Countries)
            {
                yield return country;

                foreach (var area in country.Areas)
                {
                    yield return area;

                    foreach (var room in area.Rooms)
                    {
                        yield return room;

                        foreach (var roomObject in EnumerateGameObjectsRecursive(room.GameObjects))
                        {
                            yield return roomObject;
                        }
                    }

                    foreach (var areaObject in EnumerateGameObjectsRecursive(area.GameObjects))
                    {
                        yield return areaObject;
                    }
                }

                foreach (var countryObject in EnumerateGameObjectsRecursive(country.GameObjects))
                {
                    yield return countryObject;
                }
            }

            foreach (var planetObject in EnumerateGameObjectsRecursive(planet.GameObjects))
            {
                yield return planetObject;
            }
        }

        foreach (var globalObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
        {
            yield return globalObject;
        }
    }

    private static Guid? TryResolveScopeNodeId(ScopeNodeBase node)
    {
        return node switch
        {
            Planet planet when planet.Id != Guid.Empty => planet.Id,
            Country country when country.Id != Guid.Empty => country.Id,
            Area area when area.Id != Guid.Empty => area.Id,
            Room room when room.Id != Guid.Empty => room.Id,
            GameObject gameObject when gameObject.ObjectId != Guid.Empty => gameObject.ObjectId,
            _ => null
        };
    }

    private static string BuildEventSubscriptionScopeChoiceDisplayName(ScopeNodeBase node)
    {
        var scopeLabel = node.ScopeKind.ToString();
        var scopeName = node.ScopeName?.Trim();
        if (string.IsNullOrWhiteSpace(scopeName))
        {
            scopeName = "(unnamed)";
        }

        return $"{scopeLabel}: {scopeName}";
    }

    private static string BuildEventSubscriptionScopeChoiceOwnerContext(
        ScopeNodeBase currentScope,
        ScopeNodeBase candidate,
        GamePropertyChoiceRelation relation)
    {
        return relation switch
        {
            GamePropertyChoiceRelation.Self => "Self",
            GamePropertyChoiceRelation.Parent => "Parent",
            GamePropertyChoiceRelation.Ancestor => "Ancestor",
            GamePropertyChoiceRelation.Child => "Child",
            GamePropertyChoiceRelation.Descendant => "Descendant",
            GamePropertyChoiceRelation.Sibling => "Sibling",
            _ => ReferenceEquals(currentScope, candidate) ? "Self" : "Other"
        };
    }

    private static PropertyResolutionScope ToPropertyResolutionScope(ScopeNodeBase node)
    {
        return node switch
        {
            Planet => PropertyResolutionScope.Planet,
            Country => PropertyResolutionScope.Country,
            Area => PropertyResolutionScope.Area,
            Room => PropertyResolutionScope.Room,
            GameObject => PropertyResolutionScope.Object,
            _ => PropertyResolutionScope.Global
        };
    }

    private static bool IsAncestorNode(IScopedAwareNode possibleAncestor, IScopedAwareNode node)
    {
        for (var current = node.ParentScope; current is not null; current = current.ParentScope)
        {
            if (ReferenceEquals(current, possibleAncestor))
            {
                return true;
            }
        }

        return false;
    }

    private static GamePropertyChoiceRelation ClassifyScopeRelationship(ScopeNodeBase currentScope, ScopeNodeBase candidate)
    {
        if (ReferenceEquals(currentScope, candidate))
        {
            return GamePropertyChoiceRelation.Self;
        }

        if (ReferenceEquals(candidate.ParentScope, currentScope))
        {
            return GamePropertyChoiceRelation.Child;
        }

        if (ReferenceEquals(currentScope.ParentScope, candidate))
        {
            return GamePropertyChoiceRelation.Parent;
        }

        if (IsAncestorNode(candidate, currentScope))
        {
            return GamePropertyChoiceRelation.Ancestor;
        }

        if (IsAncestorNode(currentScope, candidate))
        {
            return GamePropertyChoiceRelation.Descendant;
        }

        if (currentScope.ParentScope is not null
            && candidate.ParentScope is not null
            && ReferenceEquals(currentScope.ParentScope, candidate.ParentScope))
        {
            return GamePropertyChoiceRelation.Sibling;
        }

        return GamePropertyChoiceRelation.Other;
    }

    private IReadOnlyList<GamePropertyChoiceItem> BuildProjectWideEventSubscriptionFilterVariableChoices()
    {
        var choices = new List<GamePropertyChoiceItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddSimpleVariableChoices(
            choices,
            seen,
            _project.GlobalVariables,
            priority: 0,
            scope: PropertyResolutionScope.Global,
            scopePath: "Global",
            ownerName: "global",
            relation: GamePropertyChoiceRelation.Self);

        foreach (var globalObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
        {
            var ownerName = ResolveObjectOwnerName(globalObject);
            AddObjectVariableChoices(
                choices,
                seen,
                globalObject,
                priority: 1,
                scopePath: BuildScopePath("Global", ownerName),
                ownerName: ownerName,
                relation: GamePropertyChoiceRelation.Self,
                includeSelfAlias: false);
        }

        foreach (var planet in _project.Planets)
        {
            AddSimpleVariableChoices(
                choices,
                seen,
                planet.Variables,
                priority: 2,
                scope: PropertyResolutionScope.Planet,
                scopePath: BuildScopePath(planet.ScopeName),
                ownerName: planet.ScopeName,
                relation: GamePropertyChoiceRelation.Descendant);

            foreach (var country in planet.Countries)
            {
                AddSimpleVariableChoices(
                    choices,
                    seen,
                    country.Variables,
                    priority: 3,
                    scope: PropertyResolutionScope.Country,
                    scopePath: BuildScopePath(planet.ScopeName, country.ScopeName),
                    ownerName: country.ScopeName,
                    relation: GamePropertyChoiceRelation.Descendant);

                foreach (var area in country.Areas)
                {
                    AddSimpleVariableChoices(
                        choices,
                        seen,
                        area.Variables,
                        priority: 4,
                        scope: PropertyResolutionScope.Area,
                        scopePath: BuildScopePath(planet.ScopeName, country.ScopeName, area.ScopeName),
                        ownerName: area.ScopeName,
                        relation: GamePropertyChoiceRelation.Descendant);

                    foreach (var room in area.Rooms)
                    {
                        AddSimpleVariableChoices(
                            choices,
                            seen,
                            room.Variables,
                            priority: 5,
                            scope: PropertyResolutionScope.Room,
                            scopePath: BuildScopePath(planet.ScopeName, country.ScopeName, area.ScopeName, room.ScopeName),
                            ownerName: room.ScopeName,
                            relation: GamePropertyChoiceRelation.Descendant);

                        foreach (var roomObject in EnumerateGameObjectsRecursive(room.GameObjects))
                        {
                            var ownerName = ResolveObjectOwnerName(roomObject);
                            AddObjectVariableChoices(
                                choices,
                                seen,
                                roomObject,
                                priority: 6,
                                scopePath: BuildScopePath(planet.ScopeName, country.ScopeName, area.ScopeName, room.ScopeName, ownerName),
                                ownerName: ownerName,
                                relation: GamePropertyChoiceRelation.Descendant,
                                includeSelfAlias: false);
                        }
                    }
                }
            }
        }

        return choices
            .OrderBy(static choice => choice.Priority)
            .ThenBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> BuildEventSubscriptionFilterVariableSuggestions(ScopeNodeBase? scopeNode)
    {
        var suggestions = BuildEventSubscriptionFilterVariableChoices(scopeNode)
            .Select(static choice => choice.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!suggestions.Contains("action.trigger.isEventFired", StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Insert(0, "action.trigger.isEventFired");
        }

        return suggestions;
    }

    private IReadOnlyList<string> BuildEventSubscriptionSoundEffectKeySuggestions(ScopeNodeBase? scopeNode)
    {
        return GetSoundEffectChoicesForScopeNode(scopeNode)
            .Select(static choice => choice.SoundEffectKey?.Trim() ?? string.Empty)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryEditSingleSoundEffectEntry(SoundEffectLibraryEntry source, out SoundEffectLibraryEntry updated)
    {
        var dialog = new Views.SoundEffectEntryEditorDialog(CloneSoundEffectLibraryEntry(source), GetProjectSoundEffectCategorySuggestions());
        if (dialog.ShowDialog() != true)
        {
            updated = source;
            return false;
        }

        updated = CloneSoundEffectLibraryEntry(dialog.Entry);
        return true;
    }

    private static string BuildDefaultSoundEffectKey(IList<SoundEffectLibraryEntry> entries)
    {
        var used = entries
            .Select(entry => (entry.SoundEffectKey ?? string.Empty).Trim())
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sequence = Math.Max(1, entries.Count + 1);
        while (true)
        {
            var candidate = $"sound.effect.{sequence}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }

            sequence++;
        }
    }

    private static EventSubscriptionDefinition CreateDefaultEventSubscription(IReadOnlyList<EventSubscriptionDefinition> subscriptions)
    {
        return new EventSubscriptionDefinition
        {
            Id = Guid.NewGuid(),
            EventKey = string.Empty,
            SubscriptionName = string.Empty,
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition
                    {
                        ActionName = "inspect"
                    }
                }
            ]
        };
    }

    private static List<EventSubscriptionDefinition> CloneEventSubscriptions(IReadOnlyList<EventSubscriptionDefinition> source)
    {
        return source
            .Select(CloneEventSubscription)
            .ToList();
    }

    private static EventSubscriptionDefinition CloneEventSubscription(EventSubscriptionDefinition source)
    {
        var eventKey = source.EventKey?.Trim() ?? string.Empty;
        var subscriptionName = source.SubscriptionName?.Trim() ?? string.Empty;
        return new EventSubscriptionDefinition
        {
            Id = source.Id,
            EventKey = eventKey,
            SubscriptionName = string.IsNullOrWhiteSpace(subscriptionName)
                ? eventKey
                : subscriptionName,
            IsEnabled = source.IsEnabled,
            Lane = source.Lane,
            DispatchDisposition = source.DispatchDisposition,
            SubscriptionVisibleWhenContained = source.SubscriptionVisibleWhenContained,
            SubscriptionSourceMatchMode = source.SubscriptionSourceMatchMode,
            SubscriptionSourceScopeNodeId = source.SubscriptionSourceScopeNodeId,
            SubscriptionSecondarySourceMatchMode = source.SubscriptionSecondarySourceMatchMode,
            SubscriptionSecondarySourceScopeNodeId = source.SubscriptionSecondarySourceScopeNodeId,
            InputArgumentMappings = (source.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
                .Select(static mapping => mapping.CloneNormalized())
                .Where(static mapping =>
                    mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
                        ? !string.IsNullOrWhiteSpace(mapping.OutputActionPayloadArgKey)
                        : !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
                .ToList(),
            ActionBindings = (source.ActionBindings ?? new List<EventActionBindingDefinition>())
                .Select(CloneEventActionBinding)
                .ToList()
        };
    }

    private static EventActionBindingDefinition CloneEventActionBinding(EventActionBindingDefinition source)
    {
        return new EventActionBindingDefinition
        {
            Order = source.Order,
            IsEnabled = source.IsEnabled,
            Condition = new EventBindingConditionDefinition
            {
                QuantityEvaluationMode = source.Condition?.QuantityEvaluationMode
                    ?? Storyboard.Shared.RuntimeContracts.Enums.RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass,
                Filters = (source.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>())
                    .Select(filter => new EventBindingFilterConditionDefinition
                    {
                        VariableName = filter.VariableName,
                        Operator = filter.Operator,
                        ExpectedValue = filter.ExpectedValue
                    })
                    .ToList()
            },
            Target = new EventBindingTargetDefinition
            {
                ActionName = source.Target?.ActionName ?? string.Empty,
                OnMissingAction = source.Target?.OnMissingAction ?? "DiagnosticOnly",
                StopChainOnFailure = source.Target?.StopChainOnFailure ?? true
            },
            InputArgumentMappings = (source.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
                .Select(static mapping => mapping.CloneNormalized())
                .Where(static mapping =>
                    mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
                        ? !string.IsNullOrWhiteSpace(mapping.OutputActionPayloadArgKey)
                        : !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
                .ToList()
        };
    }

    private bool EditScopedProceduresForNode(HierarchyNodeViewModel node)
    {
        if (TryResolveLockedDefinitionOwnedTokenScopeMessage(node, "Procedures", out var lockedMessage))
        {
            _projectUiService.ShowInformation(lockedMessage, "Linked Procedures");
            return false;
        }

        if (!TryResolveScopedProcedureOwnershipTarget(node, out var scopeLabel, out var procedureIds, out var ownerNode))
        {
            return false;
        }

        var scopeNode = ResolveScopeNodeForHierarchyNode(ownerNode);
        var variableScope = ownerNode is ProjectRootNodeViewModel
            ? PropertyResolutionScope.Global
            : PropertyResolutionScope.Object;
        var objectChoices = BuildProjectWideObjectChoices();
        IReadOnlyList<GamePropertyChoiceItem> variableChoices = scopeNode is null
            ? Array.Empty<GamePropertyChoiceItem>()
            : GetVariableChoicesForScopeNode(scopeNode, variableScope);

        if (!_treeContextInteractionService.EditScopedProcedureDefinitions(
                scopeLabel,
                procedureIds,
                _project.Procedures,
                objectChoices,
                variableChoices))
        {
            return false;
        }

        EnsureExclusiveProcedureOwnership(procedureIds);

        PruneProcedureOwnershipToKnownProcedures();

        EnsureScopedProceduresNode(
            ownerNode,
            procedureIds,
            ownerNode is ProjectRootNodeViewModel ? PropertyResolutionScope.Global : PropertyResolutionScope.Object);

        var proceduresNode = ResolveScopedProceduresNode(ownerNode);
        if (proceduresNode is not null)
        {
            RefreshScopedProcedureNodes(proceduresNode, ownerNode);
        }

        if (ownerNode is ProjectRootNodeViewModel projectRootNode)
        {
            RebuildProjectRootBranch(projectRootNode);
        }
        else if (ownerNode is GameObjectNodeViewModel gameObjectNode)
        {
            ApplyHideEmptyConfiguration(gameObjectNode);
        }
        else if (ownerNode is GlobalObjectNodeViewModel globalGameObjectNode)
        {
            ApplyHideEmptyConfiguration(globalGameObjectNode);
        }
        else if (ownerNode is TemplateGameObjectNodeViewModel templateGameObjectNode)
        {
            ApplyHideEmptyConfiguration(templateGameObjectNode);
        }

        NotifyProjectEdited();
        ExportStatus = "Scoped procedures updated.";
        return true;
    }

    private bool AddScopedProcedureForNode(HierarchyNodeViewModel node)
    {
        return EditScopedProceduresForNode(node);
    }

    private bool EditProcedureDefinitions()
    {
        if (!_treeContextInteractionService.EditProcedureDefinitions(_project.Procedures))
        {
            return false;
        }

        PruneProcedureOwnershipToKnownProcedures();
        LoadProjectIntoHierarchy();
        NotifyProjectEdited();
        ExportStatus = "Procedures updated.";
        return true;
    }

    private void PruneProcedureOwnershipToKnownProcedures()
    {
        var knownProcedureIds = (_project.Procedures ?? new List<ProcedureDefinition>())
            .Where(static procedure => procedure.Id != Guid.Empty)
            .Select(static procedure => procedure.Id)
            .ToHashSet();

        _project.ProcedureIds = (_project.ProcedureIds ?? new List<Guid>())
            .Where(knownProcedureIds.Contains)
            .Distinct()
            .ToList();

        foreach (var obj in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects)
                     .Concat(EnumerateGameObjectsRecursive(_project.ObjectTemplates))
                     .Concat(EnumerateGameObjectsRecursive(_project.BaseObjects))
                     .Concat(_project.Planets
                         .SelectMany(static planet => planet.Countries)
                         .SelectMany(static country => country.Areas)
                         .SelectMany(static area => area.Rooms)
                         .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects))))
        {
            obj.ProcedureIds = (obj.ProcedureIds ?? new List<Guid>())
                .Where(knownProcedureIds.Contains)
                .Distinct()
                .ToList();
        }

        EnsureExclusiveProcedureOwnership(_project.ProcedureIds);
    }

    private void EnsureExclusiveProcedureOwnership(IList<Guid> preferredOwnerProcedureIds)
    {
        var preferredIds = (preferredOwnerProcedureIds ?? Array.Empty<Guid>())
            .Where(static id => id != Guid.Empty)
            .ToHashSet();
        if (preferredIds.Count == 0)
        {
            return;
        }

        if (!ReferenceEquals(preferredOwnerProcedureIds, _project.ProcedureIds))
        {
            _project.ProcedureIds = (_project.ProcedureIds ?? new List<Guid>())
                .Where(id => !preferredIds.Contains(id))
                .Distinct()
                .ToList();
        }

        foreach (var obj in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects)
                     .Concat(EnumerateGameObjectsRecursive(_project.ObjectTemplates))
                     .Concat(EnumerateGameObjectsRecursive(_project.BaseObjects))
                     .Concat(_project.Planets
                         .SelectMany(static planet => planet.Countries)
                         .SelectMany(static country => country.Areas)
                         .SelectMany(static area => area.Rooms)
                         .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects))))
        {
            if (ReferenceEquals(preferredOwnerProcedureIds, obj.ProcedureIds))
            {
                continue;
            }

            obj.ProcedureIds = (obj.ProcedureIds ?? new List<Guid>())
                .Where(id => !preferredIds.Contains(id))
                .Distinct()
                .ToList();
        }
    }

    private IReadOnlyList<MaterializeSourceObjectChoiceItem> BuildProjectWideObjectChoices()
    {
        var allObjects = EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects)
            .Concat(EnumerateGameObjectsRecursive(_project.ObjectTemplates))
            .Concat(EnumerateGameObjectsRecursive(_project.BaseObjects))
            .Concat(_project.Planets
                .SelectMany(static planet => planet.Countries)
                .SelectMany(static country => country.Areas)
                .SelectMany(static area => area.Rooms)
                .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects)))
            .Where(static obj => obj.ObjectId != Guid.Empty)
            .GroupBy(static obj => obj.ObjectId)
            .ToDictionary(static group => group.Key, static group => group.First());

        if (allObjects.Count == 0)
        {
            return Array.Empty<MaterializeSourceObjectChoiceItem>();
        }

        return allObjects.Values
            .Select(obj => new MaterializeSourceObjectChoiceItem
            {
                ObjectId = obj.ObjectId,
                SourceObject = obj,
                DisplayName = string.IsNullOrWhiteSpace(obj.Name) ? "Unnamed Object" : obj.Name.Trim(),
                ScopePath = BuildScopePathForScopedNode(obj),
                SourceCategory = "Project Objects"
            })
            .OrderBy(static choice => choice.SourceCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildScopePathForScopedNode(IScopedAwareNode node)
    {
        var segments = new List<string>();
        IScopedAwareNode? current = node;

        while (current is not null)
        {
            var segment = current switch
            {
                GameObject obj => string.IsNullOrWhiteSpace(obj.Name) ? "Unnamed Object" : obj.Name.Trim(),
                Room room => string.IsNullOrWhiteSpace(room.Name) ? "Unnamed Room" : room.Name.Trim(),
                Area area => string.IsNullOrWhiteSpace(area.Name) ? "Unnamed Area" : area.Name.Trim(),
                Country country => string.IsNullOrWhiteSpace(country.Name) ? "Unnamed Country" : country.Name.Trim(),
                Planet planet => string.IsNullOrWhiteSpace(planet.Name) ? "Unnamed Planet" : planet.Name.Trim(),
                ProjectModel => "Global Objects",
                ObjectTemplatesScopeNode => "Object Templates",
                BaseObjectsScopeNode => "Base Objects",
                RoomTemplatesScopeNode => "Room Templates",
                _ => current.GetType().Name
            };

            segments.Add(segment);

            current = current.ParentScope;
        }

        segments.Reverse();
        return segments.Count == 0 ? "Global" : string.Join(" > ", segments);
    }

    private bool TryResolveScopedProcedureOwnershipTarget(
        HierarchyNodeViewModel node,
        out string scopeLabel,
        out IList<Guid> procedureIds,
        out HierarchyNodeViewModel ownerNode)
    {
        scopeLabel = string.Empty;
        procedureIds = Array.Empty<Guid>();
        ownerNode = node;

        switch (node)
        {
            case ProjectRootNodeViewModel projectRootNode:
                scopeLabel = "Global";
                procedureIds = _project.ProcedureIds;
                ownerNode = projectRootNode;
                return true;

            case ScopedProceduresNodeViewModel proceduresNode when proceduresNode.Scope == PropertyResolutionScope.Global:
                scopeLabel = "Global";
                procedureIds = _project.ProcedureIds;
                ownerNode = proceduresNode.Parent ?? node;
                return true;

            case ScopedProceduresNodeViewModel proceduresNode when proceduresNode.Parent is not null:
                return TryResolveScopedProcedureOwnershipTarget(proceduresNode.Parent, out scopeLabel, out procedureIds, out ownerNode);

            case ScopedProcedureEntryNodeViewModel procedureEntryNode:
                return TryResolveScopedProcedureOwnershipTarget(procedureEntryNode.ParentProceduresNode, out scopeLabel, out procedureIds, out ownerNode);

            case GameObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {GetScopeLabel(objectNode.GameObject.Name, "Unnamed Object")}";
                procedureIds = objectNode.GameObject.ProcedureIds;
                ownerNode = objectNode;
                return true;

            case GlobalObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {GetScopeLabel(objectNode.GameObject.Name, "Unnamed Object")}";
                procedureIds = objectNode.GameObject.ProcedureIds;
                ownerNode = objectNode;
                return true;

            case TemplateGameObjectNodeViewModel objectNode:
                scopeLabel = $"Template Object - {GetScopeLabel(objectNode.GameObject.Name, "Unnamed Object")}";
                procedureIds = objectNode.GameObject.ProcedureIds;
                ownerNode = objectNode;
                return true;

            default:
                return false;
        }
    }

    private bool EditProjectIgnoredValidationRules()
    {
        var inherited = _project.GlobalIgnoredValidationRuleIds
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!_treeContextInteractionService.EditValidationIgnoredRuleList(
                $"Project - {_project.Name}",
                _project.IgnoredValidationRuleIds,
                inherited,
                BuildValidationRuleCatalog()))
        {
            return false;
        }

        NotifyProjectEdited();
        ExportStatus = "Project ignored validation rules updated.";
        return true;
    }

    private bool EditGlobalIgnoredValidationRules()
    {
        var inherited = _project.IgnoredValidationRuleIds
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!_treeContextInteractionService.EditValidationIgnoredRuleList(
                "Global",
                _project.GlobalIgnoredValidationRuleIds,
                inherited,
                BuildValidationRuleCatalog()))
        {
            return false;
        }

        NotifyProjectEdited();
        ExportStatus = "Global ignored validation rules updated.";
        return true;
    }

    private bool EditIgnoredValidationRulesForNode(HierarchyNodeViewModel node)
    {
        if (!TryResolveIgnoredValidationRuleTarget(node, out var scopeLabel, out var values))
        {
            return false;
        }

        var inheritedValues = GetInheritedScopedTokenValues(node, "ValidationIgnores");
        if (!_treeContextInteractionService.EditValidationIgnoredRuleList(
                scopeLabel,
                values,
                inheritedValues,
                BuildValidationRuleCatalog()))
        {
            return false;
        }

        NotifyProjectEdited();
        ExportStatus = "Ignored validation rules updated.";
        return true;
    }

    private bool TryResolveIgnoredValidationRuleTarget(
        HierarchyNodeViewModel node,
        out string scopeLabel,
        out IList<string> values)
    {
        scopeLabel = string.Empty;
        values = Array.Empty<string>();

        switch (node)
        {
            case PlanetNodeViewModel planetNode:
                scopeLabel = $"Planet - {planetNode.Planet.Name}";
                values = planetNode.Planet.IgnoredValidationRuleIds;
                return true;
            case CountryNodeViewModel countryNode:
                scopeLabel = $"Country - {countryNode.Country.Name}";
                values = countryNode.Country.IgnoredValidationRuleIds;
                return true;
            case AreaNodeViewModel areaNode:
                scopeLabel = $"Area - {areaNode.Area.Name}";
                values = areaNode.Area.IgnoredValidationRuleIds;
                return true;
            case RoomNodeViewModel roomNode:
                scopeLabel = $"Room - {roomNode.Room.Name}";
                values = roomNode.Room.IgnoredValidationRuleIds;
                return true;
            case GlobalObjectsNodeViewModel:
                scopeLabel = "Global Objects";
                values = _project.GlobalScope.IgnoredValidationRuleIds;
                return true;
            case ObjectTemplatesNodeViewModel:
                scopeLabel = ((ObjectTemplatesNodeViewModel)node).IsBaseCatalog ? "Base Objects" : "Object Templates";
                values = ((ObjectTemplatesNodeViewModel)node).CatalogIgnoredValidationRuleIds;
                return true;
            case RoomTemplatesNodeViewModel templatesNode:
                scopeLabel = "Room Templates";
                values = templatesNode.CatalogIgnoredValidationRuleIds;
                return true;
            case GameObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.IgnoredValidationRuleIds;
                return true;
            case TemplateRoomNodeViewModel templateRoomNode:
                scopeLabel = $"Template Room - {templateRoomNode.Room.Name}";
                values = templateRoomNode.Room.IgnoredValidationRuleIds;
                return true;
            case GlobalObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.IgnoredValidationRuleIds;
                return true;
            case TemplateGameObjectNodeViewModel objectNode:
                scopeLabel = $"Template Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.IgnoredValidationRuleIds;
                return true;
            default:
                return false;
        }
    }

    private static IReadOnlyList<ValidationRuleCatalogItem> BuildValidationRuleCatalog()
    {
        return CreateValidationRuleRegistry()
            .Rules
            .Select(rule => rule.Metadata)
            .Select(metadata => new ValidationRuleCatalogItem(
                metadata.RuleId,
                metadata.Title,
                metadata.Category,
                metadata.DefaultSeverity))
            .OrderBy(static item => item.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryResolveScopedDirectionalMappingTarget(HierarchyNodeViewModel node, out IList<DirectionalTraversalMapping> mappings)
    {
        mappings = Array.Empty<DirectionalTraversalMapping>();

        switch (node)
        {
            case ProjectRootNodeViewModel:
                mappings = _project.DirectionalTraversalMappings;
                return true;
            case PlanetNodeViewModel planetNode:
                mappings = planetNode.Planet.AdditionalDirectionalTraversalMappings;
                return true;
            case CountryNodeViewModel countryNode:
                mappings = countryNode.Country.AdditionalDirectionalTraversalMappings;
                return true;
            case AreaNodeViewModel areaNode:
                mappings = areaNode.Area.AdditionalDirectionalTraversalMappings;
                return true;
            case RoomNodeViewModel roomNode:
                mappings = roomNode.Room.AdditionalDirectionalTraversalMappings;
                return true;
            case TemplateRoomNodeViewModel roomNode:
                mappings = roomNode.Room.AdditionalDirectionalTraversalMappings;
                return true;
            case GameObjectNodeViewModel objectNode:
                mappings = objectNode.GameObject.AdditionalDirectionalTraversalMappings;
                return true;
            case GlobalObjectNodeViewModel objectNode:
                mappings = objectNode.GameObject.AdditionalDirectionalTraversalMappings;
                return true;
            case TemplateGameObjectNodeViewModel objectNode:
                mappings = objectNode.GameObject.AdditionalDirectionalTraversalMappings;
                return true;
            case ScopedDirectionalsNodeViewModel directionalsNode when directionalsNode.Scope == PropertyResolutionScope.Global:
                mappings = _project.DirectionalTraversalMappings;
                return true;
            case ScopedDirectionalsNodeViewModel directionalsNode when directionalsNode.Parent is not null:
                return TryResolveScopedDirectionalMappingTarget(directionalsNode.Parent, out mappings);
            default:
                return false;
        }
    }

    private bool TryResolveScopedSoundEffectLibraryTarget(
        HierarchyNodeViewModel node,
        out string scopeLabel,
        out IList<SoundEffectLibraryEntry> values)
    {
        scopeLabel = string.Empty;
        values = Array.Empty<SoundEffectLibraryEntry>();

        switch (node)
        {
            case ProjectRootNodeViewModel:
                scopeLabel = "Global";
                values = _project.GlobalScope.SoundEffectLibraryEntries;
                return true;
            case PlanetNodeViewModel planetNode:
                scopeLabel = $"Planet - {planetNode.Planet.Name}";
                values = planetNode.Planet.SoundEffectLibraryEntries;
                return true;
            case CountryNodeViewModel countryNode:
                scopeLabel = $"Country - {countryNode.Country.Name}";
                values = countryNode.Country.SoundEffectLibraryEntries;
                return true;
            case AreaNodeViewModel areaNode:
                scopeLabel = $"Area - {areaNode.Area.Name}";
                values = areaNode.Area.SoundEffectLibraryEntries;
                return true;
            case RoomNodeViewModel roomNode:
                scopeLabel = $"Room - {roomNode.Room.Name}";
                values = roomNode.Room.SoundEffectLibraryEntries;
                return true;
            case TemplateRoomNodeViewModel roomNode:
                scopeLabel = $"Template Room - {roomNode.Room.Name}";
                values = roomNode.Room.SoundEffectLibraryEntries;
                return true;
            case GameObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.SoundEffectLibraryEntries;
                return true;
            case GlobalObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.SoundEffectLibraryEntries;
                return true;
            case TemplateGameObjectNodeViewModel objectNode:
                scopeLabel = $"Template Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.SoundEffectLibraryEntries;
                return true;
            case ScopedSoundEffectsNodeViewModel soundEffectsNode when soundEffectsNode.Scope == PropertyResolutionScope.Global:
                scopeLabel = "Global";
                values = _project.GlobalScope.SoundEffectLibraryEntries;
                return true;
            case ScopedSoundEffectsNodeViewModel soundEffectsNode when soundEffectsNode.Parent is not null:
                return TryResolveScopedSoundEffectLibraryTarget(soundEffectsNode.Parent, out scopeLabel, out values);
            case ScopedSoundEffectEntryNodeViewModel soundEffectEntryNode:
                return TryResolveScopedSoundEffectLibraryTarget(soundEffectEntryNode.ParentSoundEffectsNode, out scopeLabel, out values);
            default:
                return false;
        }
    }

    private bool TryResolveScopedEventSubscriptionTarget(
        HierarchyNodeViewModel node,
        out string scopeLabel,
        out IList<EventSubscriptionDefinition> values)
    {
        scopeLabel = string.Empty;
        values = Array.Empty<EventSubscriptionDefinition>();

        switch (node)
        {
            case ProjectRootNodeViewModel:
                scopeLabel = "Global";
                values = _project.GlobalScope.EventSubscriptions;
                return true;
            case PlanetNodeViewModel planetNode:
                scopeLabel = $"Planet - {planetNode.Planet.Name}";
                values = planetNode.Planet.EventSubscriptions;
                return true;
            case CountryNodeViewModel countryNode:
                scopeLabel = $"Country - {countryNode.Country.Name}";
                values = countryNode.Country.EventSubscriptions;
                return true;
            case AreaNodeViewModel areaNode:
                scopeLabel = $"Area - {areaNode.Area.Name}";
                values = areaNode.Area.EventSubscriptions;
                return true;
            case RoomNodeViewModel roomNode:
                scopeLabel = $"Room - {roomNode.Room.Name}";
                values = roomNode.Room.EventSubscriptions;
                return true;
            case TemplateRoomNodeViewModel roomNode:
                scopeLabel = $"Template Room - {roomNode.Room.Name}";
                values = roomNode.Room.EventSubscriptions;
                return true;
            case GameObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.EventSubscriptions;
                return true;
            case GlobalObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.EventSubscriptions;
                return true;
            case TemplateGameObjectNodeViewModel objectNode:
                scopeLabel = $"Template Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.EventSubscriptions;
                return true;
            case ScopedEventSubscriptionsNodeViewModel eventSubscriptionsNode when eventSubscriptionsNode.Scope == PropertyResolutionScope.Global:
                scopeLabel = "Global";
                values = _project.GlobalScope.EventSubscriptions;
                return true;
            case ScopedEventSubscriptionsNodeViewModel eventSubscriptionsNode when eventSubscriptionsNode.Parent is not null:
                return TryResolveScopedEventSubscriptionTarget(eventSubscriptionsNode.Parent, out scopeLabel, out values);
            case ScopedEventSubscriptionEntryNodeViewModel eventEntryNode:
                return TryResolveScopedEventSubscriptionTarget(eventEntryNode.ParentEventSubscriptionsNode, out scopeLabel, out values);
            default:
                return false;
        }
    }

    private bool TryResolveScopedTimerDefinitionTarget(
        HierarchyNodeViewModel node,
        out string scopeLabel,
        out IList<RuntimeTimerDefinitionDto> values)
    {
        scopeLabel = string.Empty;
        values = Array.Empty<RuntimeTimerDefinitionDto>();

        switch (node)
        {
            case ProjectRootNodeViewModel:
                scopeLabel = "Global";
                values = _project.GlobalScope.TimerDefinitions;
                return true;
            case PlanetNodeViewModel planetNode:
                scopeLabel = $"Planet - {planetNode.Planet.Name}";
                values = planetNode.Planet.TimerDefinitions;
                return true;
            case CountryNodeViewModel countryNode:
                scopeLabel = $"Country - {countryNode.Country.Name}";
                values = countryNode.Country.TimerDefinitions;
                return true;
            case AreaNodeViewModel areaNode:
                scopeLabel = $"Area - {areaNode.Area.Name}";
                values = areaNode.Area.TimerDefinitions;
                return true;
            case RoomNodeViewModel roomNode:
                scopeLabel = $"Room - {roomNode.Room.Name}";
                values = roomNode.Room.TimerDefinitions;
                return true;
            case TemplateRoomNodeViewModel roomNode:
                scopeLabel = $"Template Room - {roomNode.Room.Name}";
                values = roomNode.Room.TimerDefinitions;
                return true;
            case GameObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.TimerDefinitions;
                return true;
            case GlobalObjectNodeViewModel objectNode:
                scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.TimerDefinitions;
                return true;
            case TemplateGameObjectNodeViewModel objectNode:
                scopeLabel = $"Template Object - {objectNode.GameObject.Name}";
                values = objectNode.GameObject.TimerDefinitions;
                return true;
            case ScopedTimerDefinitionsNodeViewModel timerDefinitionsNode when timerDefinitionsNode.Scope == PropertyResolutionScope.Global:
                scopeLabel = "Global";
                values = _project.GlobalScope.TimerDefinitions;
                return true;
            case ScopedTimerDefinitionsNodeViewModel timerDefinitionsNode when timerDefinitionsNode.Parent is not null:
                return TryResolveScopedTimerDefinitionTarget(timerDefinitionsNode.Parent, out scopeLabel, out values);
            case ScopedTimerDefinitionEntryNodeViewModel timerEntryNode:
                return TryResolveScopedTimerDefinitionTarget(timerEntryNode.ParentTimerDefinitionsNode, out scopeLabel, out values);
            default:
                return false;
        }
    }

    private bool TryResolveScopedTokenListTarget(
        HierarchyNodeViewModel node,
        string tokenKind,
        out string scopeLabel,
        out IList<string> values)
    {
        scopeLabel = string.Empty;
        values = Array.Empty<string>();

        if (tokenKind == "Verbs")
        {
            switch (node)
            {
                case ProjectRootNodeViewModel:
                    scopeLabel = "Global";
                    values = _project.CommandVerbs;
                    return true;
                case PlanetNodeViewModel planetNode:
                    scopeLabel = $"Planet - {planetNode.Planet.Name}";
                    values = planetNode.Planet.AdditionalVerbs;
                    return true;
                case CountryNodeViewModel countryNode:
                    scopeLabel = $"Country - {countryNode.Country.Name}";
                    values = countryNode.Country.AdditionalVerbs;
                    return true;
                case AreaNodeViewModel areaNode:
                    scopeLabel = $"Area - {areaNode.Area.Name}";
                    values = areaNode.Area.AdditionalVerbs;
                    return true;
                case RoomNodeViewModel roomNode:
                    scopeLabel = $"Room - {roomNode.Room.Name}";
                    values = roomNode.Room.AdditionalVerbs;
                    return true;
                case TemplateRoomNodeViewModel roomNode:
                    scopeLabel = $"Template Room - {roomNode.Room.Name}";
                    values = roomNode.Room.AdditionalVerbs;
                    return true;
                case GameObjectNodeViewModel objectNode:
                    scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                    values = objectNode.GameObject.AdditionalVerbs;
                    return true;
                case GlobalObjectNodeViewModel objectNode:
                    scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                    values = objectNode.GameObject.AdditionalVerbs;
                    return true;
                case TemplateGameObjectNodeViewModel objectNode:
                    scopeLabel = $"Template Object - {objectNode.GameObject.Name}";
                    values = objectNode.GameObject.AdditionalVerbs;
                    return true;
                case ScopedVerbsNodeViewModel verbsNode when verbsNode.Scope == PropertyResolutionScope.Global:
                    scopeLabel = "Global";
                    values = _project.CommandVerbs;
                    return true;
                case ScopedVerbsNodeViewModel verbsNode when verbsNode.Parent is not null:
                    return TryResolveScopedTokenListTarget(verbsNode.Parent, tokenKind, out scopeLabel, out values);
            }
        }
        else if (tokenKind == "Directionals")
        {
            switch (node)
            {
                case ProjectRootNodeViewModel:
                    scopeLabel = "Global";
                    values = _project.Directionals;
                    return true;
                case PlanetNodeViewModel planetNode:
                    scopeLabel = $"Planet - {planetNode.Planet.Name}";
                    values = planetNode.Planet.AdditionalDirectionals;
                    return true;
                case CountryNodeViewModel countryNode:
                    scopeLabel = $"Country - {countryNode.Country.Name}";
                    values = countryNode.Country.AdditionalDirectionals;
                    return true;
                case AreaNodeViewModel areaNode:
                    scopeLabel = $"Area - {areaNode.Area.Name}";
                    values = areaNode.Area.AdditionalDirectionals;
                    return true;
                case RoomNodeViewModel roomNode:
                    scopeLabel = $"Room - {roomNode.Room.Name}";
                    values = roomNode.Room.AdditionalDirectionals;
                    return true;
                case TemplateRoomNodeViewModel roomNode:
                    scopeLabel = $"Template Room - {roomNode.Room.Name}";
                    values = roomNode.Room.AdditionalDirectionals;
                    return true;
                case GameObjectNodeViewModel objectNode:
                    scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                    values = objectNode.GameObject.AdditionalDirectionals;
                    return true;
                case GlobalObjectNodeViewModel objectNode:
                    scopeLabel = $"Game Object - {objectNode.GameObject.Name}";
                    values = objectNode.GameObject.AdditionalDirectionals;
                    return true;
                case TemplateGameObjectNodeViewModel objectNode:
                    scopeLabel = $"Template Object - {objectNode.GameObject.Name}";
                    values = objectNode.GameObject.AdditionalDirectionals;
                    return true;
                case ScopedDirectionalsNodeViewModel directionalsNode when directionalsNode.Scope == PropertyResolutionScope.Global:
                    scopeLabel = "Global";
                    values = _project.Directionals;
                    return true;
                case ScopedDirectionalsNodeViewModel directionalsNode when directionalsNode.Parent is not null:
                    return TryResolveScopedTokenListTarget(directionalsNode.Parent, tokenKind, out scopeLabel, out values);
            }
        }

        return false;
    }

    private IReadOnlyList<string> GetInheritedScopedTokenValues(HierarchyNodeViewModel node, string tokenKind)
    {
        var targetNode = ResolveScopedTokenTargetNode(node);
        if (targetNode is null)
        {
            return Array.Empty<string>();
        }

        var inherited = new List<string>();

        if (string.Equals(tokenKind, "ValidationIgnores", StringComparison.Ordinal)
            && targetNode is not ProjectRootNodeViewModel)
        {
            inherited.AddRange(_project.IgnoredValidationRuleIds);
        }

        var cursor = targetNode.Parent;

        while (cursor is not null)
        {
            inherited.AddRange(GetTokenValuesFromNode(cursor, tokenKind));
            cursor = cursor.Parent;
        }

        return inherited
            .Select(static value => value.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HierarchyNodeViewModel? ResolveScopedTokenTargetNode(HierarchyNodeViewModel node)
    {
        return node switch
        {
            ScopedVerbsNodeViewModel verbsNode => verbsNode.Parent,
            ScopedDirectionalsNodeViewModel directionalsNode => directionalsNode.Parent,
            _ => node
        };
    }

    private IEnumerable<string> GetTokenValuesFromNode(HierarchyNodeViewModel node, string tokenKind)
    {
        if (tokenKind == "Verbs")
        {
            return node switch
            {
                ProjectRootNodeViewModel => _project.CommandVerbs,
                PlanetNodeViewModel planetNode => planetNode.Planet.AdditionalVerbs,
                CountryNodeViewModel countryNode => countryNode.Country.AdditionalVerbs,
                AreaNodeViewModel areaNode => areaNode.Area.AdditionalVerbs,
                RoomNodeViewModel roomNode => roomNode.Room.AdditionalVerbs,
                TemplateRoomNodeViewModel roomNode => roomNode.Room.AdditionalVerbs,
                GameObjectNodeViewModel objectNode => objectNode.GameObject.AdditionalVerbs,
                GlobalObjectNodeViewModel objectNode => objectNode.GameObject.AdditionalVerbs,
                TemplateGameObjectNodeViewModel objectNode => objectNode.GameObject.AdditionalVerbs,
                _ => Array.Empty<string>()
            };
        }

        if (tokenKind == "Directionals")
        {
            return node switch
            {
                ProjectRootNodeViewModel => _project.Directionals,
                PlanetNodeViewModel planetNode => planetNode.Planet.AdditionalDirectionals,
                CountryNodeViewModel countryNode => countryNode.Country.AdditionalDirectionals,
                AreaNodeViewModel areaNode => areaNode.Area.AdditionalDirectionals,
                RoomNodeViewModel roomNode => roomNode.Room.AdditionalDirectionals,
                TemplateRoomNodeViewModel roomNode => roomNode.Room.AdditionalDirectionals,
                GameObjectNodeViewModel objectNode => objectNode.GameObject.AdditionalDirectionals,
                GlobalObjectNodeViewModel objectNode => objectNode.GameObject.AdditionalDirectionals,
                TemplateGameObjectNodeViewModel objectNode => objectNode.GameObject.AdditionalDirectionals,
                _ => Array.Empty<string>()
            };
        }

        if (tokenKind == "ValidationIgnores")
        {
            return node switch
            {
                ProjectRootNodeViewModel => _project.GlobalIgnoredValidationRuleIds,
                PlanetNodeViewModel planetNode => planetNode.Planet.IgnoredValidationRuleIds,
                CountryNodeViewModel countryNode => countryNode.Country.IgnoredValidationRuleIds,
                AreaNodeViewModel areaNode => areaNode.Area.IgnoredValidationRuleIds,
                RoomNodeViewModel roomNode => roomNode.Room.IgnoredValidationRuleIds,
                GlobalObjectsNodeViewModel => _project.GlobalScope.IgnoredValidationRuleIds,
                ObjectTemplatesNodeViewModel templatesNode => templatesNode.CatalogIgnoredValidationRuleIds,
                RoomTemplatesNodeViewModel templatesNode => templatesNode.CatalogIgnoredValidationRuleIds,
                GameObjectNodeViewModel objectNode => objectNode.GameObject.IgnoredValidationRuleIds,
                GlobalObjectNodeViewModel objectNode => objectNode.GameObject.IgnoredValidationRuleIds,
                TemplateGameObjectNodeViewModel objectNode => objectNode.GameObject.IgnoredValidationRuleIds,
                TemplateRoomNodeViewModel roomNode => roomNode.Room.IgnoredValidationRuleIds,
                _ => Array.Empty<string>()
            };
        }

        return Array.Empty<string>();
    }

    public GamePropertyNodeViewModel? AddVariableToContainer(GamePropertiesContainerNodeViewModel variablesNode, string name, GamePropertyLifetime lifetime, string defaultValue, GamePropertyValueRestriction valueRestriction)
    {
        var variableName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        var (collection, scope) = ResolveVariableCollectionAndScope(variablesNode);
        if (collection is null)
        {
            return null;
        }

        var uniqueName = MakeUniqueVariableName(collection, variableName, GetOrCreateVariableNameIndex(collection));
        var variable = new GamePropertyDefinition
        {
            Name = uniqueName,
            DefaultValue = defaultValue,
            ValueRestriction = valueRestriction,
            Lifetime = lifetime
        };

        collection.Add(variable);
        var node = new GamePropertyNodeViewModel(variable, scope, variablesNode);
        variablesNode.Children.Add(node);
        variablesNode.IsExpanded = true;
        RefreshSharedPropertyIndicators();
        RefreshVariableNameIndex(collection);
        NotifyProjectEdited();
        SelectedNode = node;
        ExportStatus = $"Added variable '{uniqueName}'.";
        return node;
    }

    public bool RemoveVariableNode(GamePropertyNodeViewModel variableNode)
    {
        var (collection, _) = ResolveVariableCollectionAndScope(variableNode.VariablesContainer);
        if (collection is null)
        {
            return false;
        }

        var removedModel = collection.Remove(variableNode.Variable);
        var removedNode = variableNode.VariablesContainer.Children.Remove(variableNode);
        if (!removedModel && !removedNode)
        {
            return false;
        }

        if (collection is not null)
        {
            RefreshVariableNameIndex(collection);
        }

        RefreshSharedPropertyIndicators();
        NotifyProjectEdited();
        SelectedNode = variableNode.VariablesContainer;
        ExportStatus = "Removed variable.";
        return true;
    }

    private bool LeaveSharedVariable(GamePropertyNodeViewModel variableNode)
    {
        if (variableNode.Variable.SharedVariableId is not Guid sharedVariableId
            || sharedVariableId == Guid.Empty)
        {
            return false;
        }

        var sharedName = _project.SharedVariables
            .FirstOrDefault(shared => shared.Id == sharedVariableId)?.Name;
        var sharedLabel = string.IsNullOrWhiteSpace(sharedName)
            ? sharedVariableId.ToString("N")
            : $"{sharedName} ({sharedVariableId:N})";

        if (!_projectUiService.Confirm(
                $"Leave shared variable '{sharedLabel}' for '{variableNode.Variable.Name}'?",
                "Leave Share"))
        {
            return false;
        }

        variableNode.Variable.SharedVariableId = null;

        if (variableNode.VariablesContainer.Parent is TraversalLegNodeViewModel traversalLegNode
            && string.Equals(variableNode.Variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase))
        {
            traversalLegNode.LegState.SharedVariableId = null;
        }

        SharedVariableReconciliationService.ReconcileInPlace(_project);
        RefreshSharedPropertyIndicators();
        NotifyProjectEdited();
        ExportStatus = "Left shared variable membership.";
        return true;
    }

    private bool ReviewSharedPropertyRelationships(GamePropertyNodeViewModel variableNode)
    {
        var relationships = GetSharedPropertyRelationships(variableNode);
        var propertyPath = BuildVariablePropertyPath(variableNode);
        var shareStateCallout = BuildShareStateCallout(variableNode);
        var selectedSharedVariableId = variableNode.Variable.SharedVariableId;
        var selectedSharedVariableDisplayName = selectedSharedVariableId is Guid sharedId
            ? _project.SharedVariables.FirstOrDefault(shared => shared.Id == sharedId)?.Name
            : null;
        Action? createSharedRelationshipAction = TryBuildCreateSharedRelationshipAction(variableNode);
        Action? openSharedVariablesManagerAction =
            () => OpenSharedVariablesManager(selectedSharedVariableId);
        Func<SharedPropertyRelationshipReviewItem, bool>? removeSharedRelationshipAction =
            TryResolveVariableOwnerObject(variableNode, out _)
                ? relationship => RemoveSharedPropertyRelationship(variableNode, relationship)
                : null;
        Func<IReadOnlyList<SharedPropertyRelationshipReviewItem>> reloadRelationships =
            () => GetSharedPropertyRelationships(variableNode);
        _treeContextInteractionService.ShowSharedPropertyRelationships(
            propertyPath,
            relationships,
            createSharedRelationshipAction,
            removeSharedRelationshipAction,
            reloadRelationships,
            selectedSharedVariableId,
            selectedSharedVariableDisplayName,
            RenameSharedVariableDisplayName,
            openSharedVariablesManagerAction,
            shareStateCallout);
        return true;
    }

    private void OpenSharedVariablesManager(Guid? selectedSharedVariableId)
    {
        _treeContextInteractionService.ShowSharedVariablesManager(
            BuildSharedVariablesManagerItems(),
            selectedSharedVariableId,
            RenameSharedVariableDisplayName,
            DropEmptySharedVariable,
            BuildSharedVariablesManagerItems);
    }

    private IReadOnlyList<SharedVariableManagerListItem> BuildSharedVariablesManagerItems()
    {
        return (_project.SharedVariables ?? new List<SharedVariableDefinition>())
            .OrderBy(static shared => shared.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static shared => shared.Id)
            .Select(shared => new SharedVariableManagerListItem
            {
                Id = shared.Id,
                DisplayName = string.IsNullOrWhiteSpace(shared.Name)
                    ? shared.Id.ToString("N")
                    : shared.Name.Trim(),
                ParticipantCount = shared.Participants.Count,
                Status = shared.Participants.Count switch
                {
                    0 => "Empty Metadata Shell",
                    1 => "Single Participant",
                    _ => "Shared"
                }
            })
            .ToList();
    }

    private bool RenameSharedVariableDisplayName(Guid sharedVariableId, string displayName)
    {
        var shared = _project.SharedVariables.FirstOrDefault(candidate => candidate.Id == sharedVariableId);
        if (shared is null)
        {
            return false;
        }

        var normalized = (displayName ?? string.Empty).Trim();
        if (string.Equals(shared.Name, normalized, StringComparison.Ordinal))
        {
            return true;
        }

        shared.Name = normalized;
        NotifyProjectEdited();
        ExportStatus = string.IsNullOrWhiteSpace(normalized)
            ? $"Cleared shared variable display name ({sharedVariableId:N})."
            : $"Renamed shared variable to '{normalized}'.";

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var duplicateCount = _project.SharedVariables.Count(candidate =>
                string.Equals(candidate.Name, normalized, StringComparison.OrdinalIgnoreCase));
            if (duplicateCount > 1)
            {
                _projectUiService.ShowWarning(
                    $"Duplicate shared display name '{normalized}' detected ({duplicateCount} entries). IDs remain canonical identity.",
                    "Shared Variables Manager");
            }
        }

        return true;
    }

    private bool DropEmptySharedVariable(Guid sharedVariableId)
    {
        var shared = _project.SharedVariables.FirstOrDefault(candidate => candidate.Id == sharedVariableId);
        if (shared is null)
        {
            return false;
        }

        if (shared.Participants.Count > 0)
        {
            _projectUiService.ShowWarning(
                "Only empty shared metadata shells can be dropped from this action.",
                "Shared Variables Manager");
            return false;
        }

        var sharedLabel = string.IsNullOrWhiteSpace(shared.Name)
            ? shared.Id.ToString("N")
            : $"{shared.Name} ({shared.Id:N})";
        if (!_projectUiService.Confirm($"Drop empty shared metadata shell '{sharedLabel}'?", "Shared Variables Manager"))
        {
            return false;
        }

        _project.SharedVariables.Remove(shared);
        RefreshSharedPropertyIndicators();
        NotifyProjectEdited();
        ExportStatus = "Dropped empty shared metadata shell.";
        return true;
    }

    private string? BuildShareStateCallout(GamePropertyNodeViewModel variableNode)
    {
        if (variableNode.Variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
        {
            return null;
        }

        var shared = _project.SharedVariables.FirstOrDefault(candidate => candidate.Id == sharedId);
        if (shared is null)
        {
            return "Shared metadata is missing for this variable link. Save/reload or reconcile to repair metadata.";
        }

        var participantCount = shared.Participants.Count;
        if (participantCount == 0)
        {
            return "This shared variable is currently empty (metadata shell only). You can keep it or delete it explicitly in manager flows.";
        }

        if (participantCount == 1)
        {
            return "Single Participant Share: this variable is the only current participant.";
        }

        return null;
    }

    public void OpenSharedRelationshipManager(GamePropertyNodeViewModel variableNode)
    {
        if (variableNode is null)
        {
            return;
        }

        ReviewSharedPropertyRelationships(variableNode);
    }

    private bool ShareGameProperty(GamePropertyNodeViewModel variableNode)
    {
        var propertyName = variableNode.Variable.Name?.Trim() ?? string.Empty;
        var sourceRestriction = variableNode.Variable.ValueRestriction;
        if (!TryResolveVariableOwnerObject(variableNode, out var sourceObject) || sourceObject.ObjectId == Guid.Empty)
        {
            _projectUiService.ShowWarning("Only object properties can be shared from this menu.", "Share Property");
            return false;
        }

        if (string.IsNullOrWhiteSpace(propertyName))
        {
            _projectUiService.ShowWarning("Select a valid property before sharing.", "Share Property");
            return false;
        }

        var choices = BuildShareTargetVariableChoices(sourceObject, variableNode.Variable);
        if (choices.Count == 0)
        {
            _projectUiService.ShowWarning("No compatible target variables were found.", "Share Property");
            return false;
        }

        var shareTitle = $"Share {sourceObject.Name}.{propertyName} with...";
        var hasTraversalLegTargets = choices.Any(choice => choice.Relation == GamePropertyChoiceRelation.TraversalLeg);
        if (sourceRestriction == GamePropertyValueRestriction.TrueFalse && hasTraversalLegTargets)
        {
            shareTitle = $"Share {sourceObject.Name}.{propertyName} with... (Traversal leg: choose A->B or B->A)";
        }

        if (!_treeContextInteractionService.TryChooseVariable(
                choices,
                PropertyResolutionScope.Object,
                shareTitle,
                out var selectedValue))
        {
            return false;
        }

        if (!TryParseShareTargetValue(selectedValue, out var targetSelection))
        {
            _projectUiService.ShowWarning("Could not interpret the selected target variable.", "Share Property");
            return false;
        }

        var sharedVariable = GetOrCreateSharedVariableForSource(sourceObject, variableNode.Variable);

        if (targetSelection.Kind == ShareTargetKind.TraversalLeg)
        {
            if (sourceRestriction != GamePropertyValueRestriction.TrueFalse)
            {
                _projectUiService.ShowWarning("Traversal-leg open-state binding requires a True/False source property.", "Share Property");
                return false;
            }

            if (!TryApplyTraversalLegSharedVariableLink(sharedVariable, targetSelection.TargetId, targetSelection.Leg, out var legStatusMessage))
            {
                _projectUiService.ShowWarning(legStatusMessage, "Share Property");
                return false;
            }

            RefreshSharedPropertyIndicators();
            NotifyProjectEdited();
            ExportStatus = legStatusMessage;
            return true;
        }

        if (targetSelection.Kind == ShareTargetKind.Traversal)
        {
            if (!TryApplyTraversalVariableSharedLink(sharedVariable, targetSelection.TargetId, targetSelection.PropertyName, sourceRestriction, variableNode.Variable.DefaultValue, out var traversalStatusMessage))
            {
                _projectUiService.ShowWarning(traversalStatusMessage, "Share Property");
                return false;
            }

            RefreshSharedPropertyIndicators();
            NotifyProjectEdited();
            ExportStatus = traversalStatusMessage;
            return true;
        }

        if (!TryFindObjectById(_project, targetSelection.TargetId, out var targetObject))
        {
            _projectUiService.ShowWarning("Selected target object no longer exists.", "Share Property");
            return false;
        }

        if (ReferenceEquals(sourceObject, targetObject))
        {
            _projectUiService.ShowWarning("Choose a different object as the share target.", "Share Property");
            return false;
        }

        var targetVariable = targetObject.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, targetSelection.PropertyName, StringComparison.OrdinalIgnoreCase));
        if (targetVariable is null)
        {
            _projectUiService.ShowWarning("Selected target variable no longer exists.", "Share Property");
            return false;
        }

        if (targetVariable.ValueRestriction != sourceRestriction)
        {
            _projectUiService.ShowWarning("Source and target variable types must match for sharing.", "Share Property");
            return false;
        }

        AttachObjectVariableToSharedVariable(sharedVariable, targetObject, targetVariable);

        var statusMessage = $"Shared {sourceObject.Name}.{propertyName} with {targetObject.Name}.{targetVariable.Name}.";

        RefreshSharedPropertyIndicators();
        NotifyProjectEdited();
        ExportStatus = statusMessage;
        return true;
    }

    private List<GamePropertyChoiceItem> BuildShareTargetVariableChoices(GameObject sourceObject, GamePropertyDefinition sourceVariable)
    {
        var sourcePropertyName = sourceVariable.Name?.Trim() ?? string.Empty;
        var sourceRestriction = sourceVariable.ValueRestriction;
        var choices = new List<GamePropertyChoiceItem>();

        TryFindRoomAndAreaForObject(_project, sourceObject, out var sourceArea, out var sourceRoom);

        foreach (var planet in _project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        foreach (var candidate in EnumerateGameObjectsRecursive(room.GameObjects))
                        {
                            if (ReferenceEquals(candidate, sourceObject) || candidate.ObjectId == Guid.Empty)
                            {
                                continue;
                            }

                            foreach (var variable in candidate.Variables.Where(v => v.ValueRestriction == sourceRestriction))
                            {
                                choices.Add(new GamePropertyChoiceItem
                                {
                                    Value = BuildObjectShareTargetValue(candidate.ObjectId, variable.Name),
                                    ScopePath = BuildScopePath(planet.Name, country.Name, area.Name, room.Name),
                                    OwnerVariable = BuildOwnerVariable(candidate.Name, variable.Name),
                                    Scope = PropertyResolutionScope.Object,
                                    ValueRestriction = variable.ValueRestriction,
                                    Priority = 0,
                                    Relation = GamePropertyChoiceRelation.Sibling
                                });
                            }
                        }
                    }

                    foreach (var connection in area.TraversalConnections)
                    {
                        var roomAName = area.Rooms.FirstOrDefault(roomCandidate => roomCandidate.Id == connection.RoomAId)?.Name
                                        ?? "(unknown room A)";
                        var roomBName = area.Rooms.FirstOrDefault(roomCandidate => roomCandidate.Id == connection.RoomBId)?.Name
                                        ?? "(unknown room B)";
                        var traversalLabel = BuildTraversalLabel(roomAName, roomBName);

                        foreach (var variable in connection.Variables.Where(v => v.ValueRestriction == sourceRestriction
                                                                              && !string.Equals(v.Name, "isPassable", StringComparison.OrdinalIgnoreCase)))
                        {
                            choices.Add(new GamePropertyChoiceItem
                            {
                                Value = BuildTraversalShareTargetValue(connection.TraversalConnectionId, variable.Name),
                                ScopePath = BuildScopePath(planet.Name, country.Name, area.Name, roomAName, traversalLabel),
                                OwnerVariable = BuildOwnerVariable("traversal", variable.Name),
                                Scope = PropertyResolutionScope.Area,
                                ValueRestriction = variable.ValueRestriction,
                                Priority = 1,
                                Relation = GamePropertyChoiceRelation.Sibling
                            });

                            if (!string.Equals(roomAName, roomBName, StringComparison.OrdinalIgnoreCase))
                            {
                                choices.Add(new GamePropertyChoiceItem
                                {
                                    Value = BuildTraversalShareTargetValue(connection.TraversalConnectionId, variable.Name),
                                    ScopePath = BuildScopePath(planet.Name, country.Name, area.Name, roomBName, traversalLabel),
                                    OwnerVariable = BuildOwnerVariable("traversal", variable.Name),
                                    Scope = PropertyResolutionScope.Area,
                                    ValueRestriction = variable.ValueRestriction,
                                    Priority = 1,
                                    Relation = GamePropertyChoiceRelation.Sibling
                                });
                            }
                        }

                        if (sourceRestriction == GamePropertyValueRestriction.TrueFalse
                            && sourceArea is not null
                            && ReferenceEquals(sourceArea, area))
                        {
                            choices.Add(new GamePropertyChoiceItem
                            {
                                Value = BuildTraversalLegShareTargetValue(connection.TraversalConnectionId, TraversalLegKind.AtoB, "isPassable"),
                                ScopePath = BuildScopePath(planet.Name, country.Name, area.Name, roomAName, traversalLabel),
                                OwnerVariable = BuildOwnerVariable("traversal-leg", $"{roomAName}->{roomBName}.isPassable"),
                                Scope = PropertyResolutionScope.Area,
                                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                                Priority = 1,
                                Relation = GamePropertyChoiceRelation.TraversalLeg
                            });

                            choices.Add(new GamePropertyChoiceItem
                            {
                                Value = BuildTraversalLegShareTargetValue(connection.TraversalConnectionId, TraversalLegKind.BtoA, "isPassable"),
                                ScopePath = BuildScopePath(planet.Name, country.Name, area.Name, roomBName, traversalLabel),
                                OwnerVariable = BuildOwnerVariable("traversal-leg", $"{roomBName}->{roomAName}.isPassable"),
                                Scope = PropertyResolutionScope.Area,
                                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                                Priority = 1,
                                Relation = GamePropertyChoiceRelation.TraversalLeg
                            });
                        }
                    }
                }
            }
        }

        return choices
            .OrderBy(choice => choice.Priority)
            .ThenBy(choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.OwnerVariable, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryFindRoomAndAreaForObject(ProjectModel project, GameObject targetObject, out Area area, out Room room)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var candidateArea in country.Areas)
                {
                    foreach (var candidateRoom in candidateArea.Rooms)
                    {
                        if (ContainsObject(candidateRoom.GameObjects, targetObject))
                        {
                            area = candidateArea;
                            room = candidateRoom;
                            return true;
                        }
                    }
                }
            }
        }

        area = null!;
        room = null!;
        return false;
    }

    private static bool TryFindObjectById(ProjectModel project, Guid objectId, out GameObject gameObject)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        var match = EnumerateGameObjectsRecursiveStatic(room.GameObjects)
                            .FirstOrDefault(candidate => candidate.ObjectId == objectId);
                        if (match is not null)
                        {
                            gameObject = match;
                            return true;
                        }
                    }
                }
            }
        }

        gameObject = null!;
        return false;
    }

    private static IEnumerable<GameObject> EnumerateGameObjectsRecursiveStatic(IEnumerable<GameObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in EnumerateGameObjectsRecursiveStatic(root.ContainedObjects))
            {
                yield return child;
            }
        }
    }

    private static string BuildObjectShareTargetValue(Guid objectId, string propertyName)
    {
        return $"object:{objectId:N}:{propertyName}";
    }

    private static string BuildTraversalShareTargetValue(Guid traversalConnectionId, string propertyName)
    {
        return $"traversal:{traversalConnectionId:N}:{propertyName}";
    }

    private static string BuildTraversalLegShareTargetValue(Guid traversalConnectionId, TraversalLegKind leg, string propertyName)
    {
        var legToken = leg switch
        {
            TraversalLegKind.AtoB => "a2b",
            TraversalLegKind.BtoA => "b2a",
            TraversalLegKind.Both => "both",
            _ => "a2b"
        };
        return $"traversalleg:{traversalConnectionId:N}:{legToken}:{propertyName}";
    }

    private static string BuildTraversalLabel(string roomAName, string roomBName)
    {
        return $"Traversal {roomAName} <-> {roomBName}";
    }

    private static bool TryParseShareTargetValue(string value, out ShareTargetSelection selection)
    {
        selection = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var segments = value.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            return false;
        }

        if (!Guid.TryParseExact(segments[1], "N", out var targetId))
        {
            return false;
        }

        if (string.Equals(segments[0], "traversalleg", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length != 4)
            {
                return false;
            }

            var leg = ParseTraversalLegKind(segments[2]);
            if (!leg.HasValue || string.IsNullOrWhiteSpace(segments[3]))
            {
                return false;
            }

            selection = new ShareTargetSelection(ShareTargetKind.TraversalLeg, targetId, segments[3], leg.Value);
            return true;
        }

        if (segments.Length != 3)
        {
            return false;
        }

        var propertyName = segments[2];
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (string.Equals(segments[0], "object", StringComparison.OrdinalIgnoreCase))
        {
            selection = new ShareTargetSelection(ShareTargetKind.Object, targetId, propertyName, null);
            return true;
        }

        if (string.Equals(segments[0], "traversal", StringComparison.OrdinalIgnoreCase))
        {
            selection = new ShareTargetSelection(ShareTargetKind.Traversal, targetId, propertyName, null);
            return true;
        }

        return false;
    }

    private static TraversalLegKind? ParseTraversalLegKind(string value)
    {
        if (string.Equals(value, "a2b", StringComparison.OrdinalIgnoreCase))
        {
            return TraversalLegKind.AtoB;
        }

        if (string.Equals(value, "b2a", StringComparison.OrdinalIgnoreCase))
        {
            return TraversalLegKind.BtoA;
        }

        if (string.Equals(value, "both", StringComparison.OrdinalIgnoreCase))
        {
            return TraversalLegKind.Both;
        }

        return null;
    }

    private bool TryApplyTraversalLegSharedVariableLink(
        SharedVariableDefinition sharedVariable,
        Guid traversalConnectionId,
        TraversalLegKind? leg,
        out string statusMessage)
    {
        statusMessage = string.Empty;
        if (!leg.HasValue)
        {
            statusMessage = "Traversal leg target is invalid.";
            return false;
        }

        if (!TryFindTraversalConnectionById(_project, traversalConnectionId, out var connection, out var _))
        {
            statusMessage = "Traversal connection was not found.";
            return false;
        }

        // Enforce leg-only passability semantics for traversal sharing.
        RemoveTraversalLevelPassableBindings(connection);

        TraversalLegState state;
        string targetLabel;
        if (leg.Value == TraversalLegKind.AtoB)
        {
            state = connection.TraversalStateFromA;
            targetLabel = "A->B";
        }
        else if (leg.Value == TraversalLegKind.BtoA)
        {
            state = connection.TraversalStateFromB;
            targetLabel = "B->A";
        }

        else
        {
            connection.TraversalStateFromA.SharedVariableId = null;
            connection.TraversalStateFromB.SharedVariableId = null;
            var legVariableA = EnsureTraversalLegVariable(connection.TraversalStateFromA, "isPassable", sharedVariable.DefaultValue);
            var legVariableB = EnsureTraversalLegVariable(connection.TraversalStateFromB, "isPassable", sharedVariable.DefaultValue);
            legVariableA.SharedVariableId = sharedVariable.Id;
            legVariableB.SharedVariableId = sharedVariable.Id;
            UpsertSharedParticipant(sharedVariable, "traversal-leg", connection.TraversalConnectionId, "isPassable", "a2b");
            UpsertSharedParticipant(sharedVariable, "traversal-leg", connection.TraversalConnectionId, "isPassable", "b2a");

            statusMessage = "Linked traversal both directions isPassable to shared variable pool.";
            return true;
        }

        state.SharedVariableId = null;
        var legVariable = EnsureTraversalLegVariable(state, "isPassable", sharedVariable.DefaultValue);
        legVariable.SharedVariableId = sharedVariable.Id;
        var legToken = leg.Value == TraversalLegKind.AtoB ? "a2b" : "b2a";
        UpsertSharedParticipant(sharedVariable, "traversal-leg", connection.TraversalConnectionId, "isPassable", legToken);

        statusMessage = $"Linked traversal leg {targetLabel}.isPassable to shared variable pool.";
        return true;
    }

    private void RemoveTraversalLevelPassableBindings(TraversalConnection connection)
    {
        connection.Variables.RemoveAll(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));

        foreach (var sharedVariable in _project.SharedVariables)
        {
            sharedVariable.Participants.RemoveAll(participant =>
                string.Equals(participant.Kind, "traversal", StringComparison.OrdinalIgnoreCase)
                && participant.OwnerId == connection.TraversalConnectionId
                && string.Equals(participant.VariableName, "isPassable", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static GamePropertyDefinition EnsureTraversalLegVariable(
        TraversalLegState state,
        string variableName,
        string defaultValue)
    {
        var existing = state.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = variableName;
            existing.ValueRestriction = GamePropertyValueRestriction.TrueFalse;
            existing.Lifetime = GamePropertyLifetime.Singleton;
            if (string.IsNullOrWhiteSpace(existing.DefaultValue))
            {
                existing.DefaultValue = defaultValue;
            }

            return existing;
        }

        var created = new GamePropertyDefinition
        {
            Name = variableName,
            DefaultValue = defaultValue,
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Lifetime = GamePropertyLifetime.Singleton
        };
        state.Variables.Add(created);
        return created;
    }

    private SharedVariableDefinition GetOrCreateSharedVariableForSource(GameObject sourceObject, GamePropertyDefinition sourceVariable)
    {
        if (sourceVariable.SharedVariableId.HasValue)
        {
            var existing = _project.SharedVariables.FirstOrDefault(shared => shared.Id == sourceVariable.SharedVariableId.Value);
            if (existing is not null)
            {
                AttachObjectVariableToSharedVariable(existing, sourceObject, sourceVariable);
                return existing;
            }
        }

        var created = new SharedVariableDefinition
        {
            Id = sourceVariable.SharedVariableId ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(sourceVariable.Name) ? "shared-variable" : sourceVariable.Name,
            DefaultValue = sourceVariable.DefaultValue,
            ValueRestriction = sourceVariable.ValueRestriction
        };

        _project.SharedVariables.Add(created);
        AttachObjectVariableToSharedVariable(created, sourceObject, sourceVariable);
        return created;
    }

    private static void AttachObjectVariableToSharedVariable(SharedVariableDefinition sharedVariable, GameObject ownerObject, GamePropertyDefinition variable)
    {
        variable.SharedVariableId = sharedVariable.Id;
        UpsertSharedParticipant(
            sharedVariable,
            kind: "object",
            ownerId: ownerObject.ObjectId,
            variableName: variable.Name,
            leg: null);
    }

    private bool TryApplyTraversalVariableSharedLink(
        SharedVariableDefinition sharedVariable,
        Guid traversalConnectionId,
        string variableName,
        GamePropertyValueRestriction sourceRestriction,
        string sourceDefaultValue,
        out string statusMessage)
    {
        statusMessage = string.Empty;
        if (!TryFindTraversalConnectionById(_project, traversalConnectionId, out var connection, out var _))
        {
            statusMessage = "Traversal connection was not found.";
            return false;
        }

        var traversalVariable = EnsureTraversalVariable(connection, variableName, sourceRestriction, sourceDefaultValue);
        AttachTraversalVariableToSharedVariable(sharedVariable, connection, traversalVariable);
        statusMessage = $"Linked traversal {variableName} to shared variable pool.";
        return true;
    }

    private static void AttachTraversalVariableToSharedVariable(SharedVariableDefinition sharedVariable, TraversalConnection connection, GamePropertyDefinition variable)
    {
        variable.SharedVariableId = sharedVariable.Id;
        UpsertSharedParticipant(
            sharedVariable,
            kind: "traversal",
            ownerId: connection.TraversalConnectionId,
            variableName: variable.Name,
            leg: null);
    }

    private static void UpsertSharedParticipant(
        SharedVariableDefinition sharedVariable,
        string kind,
        Guid ownerId,
        string variableName,
        string? leg)
    {
        if (ownerId == Guid.Empty || string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        var existing = sharedVariable.Participants.FirstOrDefault(participant =>
            string.Equals(participant.Kind, kind, StringComparison.OrdinalIgnoreCase)
            && participant.OwnerId == ownerId
            && string.Equals(participant.VariableName, variableName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(participant.Leg ?? string.Empty, leg ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        sharedVariable.Participants.Add(new SharedVariableParticipant
        {
            Kind = kind,
            OwnerId = ownerId,
            VariableName = variableName,
            Leg = leg
        });
    }

    private static GamePropertyDefinition EnsureTraversalVariable(
        TraversalConnection connection,
        string variableName,
        GamePropertyValueRestriction restriction,
        string defaultValue)
    {
        if (string.Equals(variableName, "isPassable", StringComparison.OrdinalIgnoreCase))
        {
            connection.Variables.RemoveAll(variable =>
                string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase)
                || string.Equals(variable.Name, "isLocked", StringComparison.OrdinalIgnoreCase));
        }

        var existing = connection.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.ValueRestriction = restriction;
            existing.Lifetime = GamePropertyLifetime.Singleton;
            if (string.IsNullOrWhiteSpace(existing.DefaultValue))
            {
                existing.DefaultValue = defaultValue;
            }

            return existing;
        }

        var created = new GamePropertyDefinition
        {
            Name = variableName,
            DefaultValue = defaultValue,
            ValueRestriction = restriction,
            Lifetime = GamePropertyLifetime.Singleton
        };
        connection.Variables.Add(created);
        return created;
    }

    private static bool TryFindTraversalConnectionById(ProjectModel project, Guid traversalConnectionId, out TraversalConnection connection, out Area area)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var candidateArea in country.Areas)
                {
                    var match = candidateArea.TraversalConnections.FirstOrDefault(candidate => candidate.TraversalConnectionId == traversalConnectionId);
                    if (match is null)
                    {
                        continue;
                    }

                    connection = match;
                    area = candidateArea;
                    return true;
                }
            }
        }

        connection = null!;
        area = null!;
        return false;
    }

    private int RemoveTraversalSharedParticipantsByConnectionIds(IEnumerable<Guid> traversalConnectionIds)
    {
        var ids = traversalConnectionIds
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        if (ids.Count == 0)
        {
            return 0;
        }

        var removedParticipantCount = 0;
        foreach (var sharedVariable in _project.SharedVariables)
        {
            var removedTraversalParticipantCount = sharedVariable.Participants.RemoveAll(participant =>
                ids.Contains(participant.OwnerId)
                && (string.Equals(participant.Kind, "traversal", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(participant.Kind, "traversal-leg", StringComparison.OrdinalIgnoreCase)));

            removedParticipantCount += removedTraversalParticipantCount;
            if (removedTraversalParticipantCount <= 0)
            {
                continue;
            }

            // Traversal deletion can leave stale object participants in legacy shared variables.
            var staleParticipants = sharedVariable.Participants
                .Where(participant => !IsParticipantLinkedToSharedVariable(participant, sharedVariable.Id))
                .ToList();
            foreach (var staleParticipant in staleParticipants)
            {
                UnlinkParticipantSharedVariable(staleParticipant, sharedVariable.Id);
            }

            if (staleParticipants.Count > 0)
            {
                removedParticipantCount += sharedVariable.Participants.RemoveAll(staleParticipants.Contains);
            }
        }

        if (removedParticipantCount > 0)
        {
            RemoveEmptySharedVariables();
            RefreshSharedPropertyIndicators();
            ExportStatus = $"Detached {removedParticipantCount} shared participant link(s) from deleted traversal connection(s).";
        }

        return removedParticipantCount;
    }

    private bool IsParticipantLinkedToSharedVariable(SharedVariableParticipant participant, Guid sharedVariableId)
    {
        if (string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindObjectById(_project, participant.OwnerId, out var objectOwner))
            {
                return false;
            }

            return objectOwner.Variables.Any(candidate =>
                string.Equals(candidate.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase)
                && candidate.SharedVariableId == sharedVariableId);
        }

        if (string.Equals(participant.Kind, "traversal", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindTraversalConnectionById(_project, participant.OwnerId, out var connection, out _))
            {
                return false;
            }

            return connection.Variables.Any(candidate =>
                string.Equals(candidate.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase)
                && candidate.SharedVariableId == sharedVariableId);
        }

        if (string.Equals(participant.Kind, "traversal-leg", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindTraversalConnectionById(_project, participant.OwnerId, out var connection, out _))
            {
                return false;
            }

            var includeA = string.Equals(participant.Leg, "a2b", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(participant.Leg, "both", StringComparison.OrdinalIgnoreCase);
            var includeB = string.Equals(participant.Leg, "b2a", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(participant.Leg, "both", StringComparison.OrdinalIgnoreCase);

            var linkedOnA = includeA && connection.TraversalStateFromA.Variables.Any(candidate =>
                string.Equals(candidate.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase)
                && candidate.SharedVariableId == sharedVariableId);
            var linkedOnB = includeB && connection.TraversalStateFromB.Variables.Any(candidate =>
                string.Equals(candidate.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase)
                && candidate.SharedVariableId == sharedVariableId);

            return linkedOnA || linkedOnB;
        }

        return false;
    }

    private readonly record struct ShareTargetSelection(ShareTargetKind Kind, Guid TargetId, string PropertyName, TraversalLegKind? Leg);

    private enum ShareTargetKind
    {
        Object,
        Traversal,
        TraversalLeg
    }

    private enum TraversalLegKind
    {
        AtoB,
        BtoA,
        Both
    }

    private Action? TryBuildCreateSharedRelationshipAction(GamePropertyNodeViewModel variableNode)
    {
        if (!TryResolveVariableOwnerObject(variableNode, out var ownerObject))
        {
            return null;
        }

        return () => ShareGameProperty(variableNode);
    }

    private static bool TryFindAreaContainingObject(ProjectModel project, GameObject targetObject, out Area area)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var candidateArea in country.Areas)
                {
                    foreach (var room in candidateArea.Rooms)
                    {
                        if (ContainsObject(room.GameObjects, targetObject))
                        {
                            area = candidateArea;
                            return true;
                        }
                    }
                }
            }
        }

        area = null!;
        return false;
    }

    private static bool ContainsObject(IEnumerable<GameObject> objects, GameObject targetObject)
    {
        foreach (var obj in objects)
        {
            if (ReferenceEquals(obj, targetObject))
            {
                return true;
            }

            if (ContainsObject(obj.ContainedObjects, targetObject))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveDoor(Room room, Guid? objectId, out GameObject? door)
    {
        door = null;
        if (!objectId.HasValue || objectId.Value == Guid.Empty)
        {
            return true;
        }

        door = EnumerateGameObjectsRecursiveStatic(room.GameObjects)
            .FirstOrDefault(candidate => candidate.ObjectId == objectId.Value);
        if (door is null)
        {
            return false;
        }

        return door.IsOpenable && !door.IsInventoriable;
    }

    private SharedVariableDefinition? ResolveOrCreateDoorTraversalSharedVariable(IReadOnlyList<GameObject> doors, TraversalConnection connection)
    {
        foreach (var door in doors)
        {
            var openVariable = door.Variables.FirstOrDefault(variable =>
                string.Equals(variable.Name, "isOpen", StringComparison.OrdinalIgnoreCase));
            if (openVariable?.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            var existing = _project.SharedVariables.FirstOrDefault(shared => shared.Id == sharedId);
            if (existing is not null)
            {
                return existing;
            }
        }

        var defaultValue = doors.Any(door => door.IsOpenDefaultValue) ? "true" : "false";
        return CreateTraversalWizardSharedVariable($"traversal_{connection.TraversalConnectionId:N}_door_open", defaultValue);
    }

    private void RemoveTraversalLegPassableBindings(TraversalConnection connection)
    {
        connection.TraversalStateFromA.SharedVariableId = null;
        connection.TraversalStateFromB.SharedVariableId = null;

        var legVariableA = connection.TraversalStateFromA.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        if (legVariableA is not null)
        {
            legVariableA.SharedVariableId = null;
        }

        var legVariableB = connection.TraversalStateFromB.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, "isPassable", StringComparison.OrdinalIgnoreCase));
        if (legVariableB is not null)
        {
            legVariableB.SharedVariableId = null;
        }

        foreach (var sharedVariable in _project.SharedVariables)
        {
            sharedVariable.Participants.RemoveAll(participant =>
                string.Equals(participant.Kind, "traversal-leg", StringComparison.OrdinalIgnoreCase)
                && participant.OwnerId == connection.TraversalConnectionId
                && string.Equals(participant.VariableName, "isPassable", StringComparison.OrdinalIgnoreCase));
        }
    }

    private void DetachObjectParticipantIfLinkedElsewhere(GameObject ownerObject, GamePropertyDefinition variable, Guid targetSharedId)
    {
        if (ownerObject.ObjectId == Guid.Empty)
        {
            return;
        }

        var variableName = variable.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        var removedAny = false;

        foreach (var shared in _project.SharedVariables)
        {
            if (shared.Id == targetSharedId)
            {
                continue;
            }

            var removedCount = shared.Participants.RemoveAll(participant =>
                string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase)
                && participant.OwnerId == ownerObject.ObjectId
                && string.Equals(participant.VariableName, variableName, StringComparison.OrdinalIgnoreCase));

            removedAny |= removedCount > 0;
        }

        if (variable.SharedVariableId is Guid currentSharedId
            && currentSharedId != Guid.Empty
            && currentSharedId != targetSharedId)
        {
            variable.SharedVariableId = null;
            removedAny = true;
        }

        if (removedAny)
        {
            RemoveEmptySharedVariables();
        }
    }

    private void RemoveEmptySharedVariables()
    {
        _project.SharedVariables.RemoveAll(shared => shared.Participants.Count == 0);
    }

    private void RefreshTraversalLegNodesForArea(Area area)
    {
        if (area is null)
        {
            return;
        }

        var roomNameById = area.Rooms
            .GroupBy(room => room.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var roomNodes = EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<RoomNodeViewModel>()
            .Where(node => ReferenceEquals(node.Area, area))
            .ToList();

        foreach (var roomNode in roomNodes)
        {
            var traversalLegsNode = roomNode.Children.OfType<RoomTraversalLegsNodeViewModel>().FirstOrDefault();
            if (traversalLegsNode is null)
            {
                continue;
            }

            var wasExpanded = traversalLegsNode.IsExpanded;
            traversalLegsNode.Children.Clear();

            var traversalLegs = area.TraversalConnections
                .Where(connection => connection.RoomAId == roomNode.Room.Id || connection.RoomBId == roomNode.Room.Id)
                .Select(connection =>
                {
                    var isFromRoomA = connection.RoomAId == roomNode.Room.Id;
                    var destinationRoomId = isFromRoomA ? connection.RoomBId : connection.RoomAId;
                    var direction = isFromRoomA
                        ? connection.BaseTraversalDirectionFromA
                        : TraversalWizardApplyUtility.OppositeDirection(connection.BaseTraversalDirectionFromA);

                    var destinationRoomName = roomNameById.TryGetValue(destinationRoomId, out var name)
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

            traversalLegsNode.IsExpanded = wasExpanded;
        }
    }

    private void RefreshSharedPropertyIndicators()
    {
        foreach (var variableNode in HierarchyRoots
                     .SelectMany(root => EnumerateHierarchyNodes(root.Children))
                     .OfType<GamePropertyNodeViewModel>())
        {
            var sharedCount = GetSharedPropertyRelationships(variableNode).Count;
            variableNode.SetSharedState(sharedCount);
        }
    }

    private IReadOnlyList<SharedPropertyRelationshipReviewItem> GetSharedPropertyRelationships(GamePropertyNodeViewModel variableNode)
    {
        if (!TryResolveSharedRelationshipSource(variableNode, out var source))
        {
            return Array.Empty<SharedPropertyRelationshipReviewItem>();
        }

        if (source.SharedVariableId == Guid.Empty)
        {
            return Array.Empty<SharedPropertyRelationshipReviewItem>();
        }

        var sharedVariable = _project.SharedVariables.FirstOrDefault(entry => entry.Id == source.SharedVariableId);
        if (sharedVariable is null)
        {
            return Array.Empty<SharedPropertyRelationshipReviewItem>();
        }

        var objectPathsById = BuildObjectPathIndex();

        var relationships = new List<SharedPropertyRelationshipReviewItem>();
        var priority = 0;

        foreach (var participant in sharedVariable.Participants)
        {
            if (ParticipantMatchesSource(participant, source))
            {
                continue;
            }

            var endpoint = BuildParticipantEndpointDisplay(participant, objectPathsById);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                continue;
            }

            relationships.Add(new SharedPropertyRelationshipReviewItem
            {
                RelationshipId = sharedVariable.Id,
                Priority = priority,
                ParticipantKind = participant.Kind,
                ParticipantOwnerId = participant.OwnerId,
                ParticipantVariableName = participant.VariableName,
                ParticipantLeg = participant.Leg,
                CounterpartEndpoint = endpoint,
                Directionality = "SharedGroup",
                TransformMode = "SharedVariablePool",
                EnabledState = "Enabled",
                SyncMode = "Immediate (Shared Pool)",
                RuntimeDiagnostics = "No runtime diagnostics yet"
            });
            priority++;
        }

        return SharedPropertyRelationshipOrdering.OrderForDisplay(relationships);
    }

    private bool TryResolveSharedRelationshipSource(
        GamePropertyNodeViewModel variableNode,
        out SharedRelationshipSource source)
    {
        source = default;

        var variableName = variableNode.Variable.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        if (TryResolveVariableOwnerObject(variableNode, out var ownerObject)
            && ownerObject.ObjectId != Guid.Empty)
        {
            var sourceVariable = ownerObject.Variables.FirstOrDefault(variable => ReferenceEquals(variable, variableNode.Variable))
                                 ?? ownerObject.Variables.FirstOrDefault(variable =>
                                     string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
            var sharedVariableId = sourceVariable?.SharedVariableId ?? Guid.Empty;
            source = new SharedRelationshipSource("object", ownerObject.ObjectId, variableName, null, sharedVariableId);
            return true;
        }

        if (variableNode.VariablesContainer.Parent is TraversalLegNodeViewModel traversalLegNode)
        {
            var sourceVariable = traversalLegNode.LegState.Variables.FirstOrDefault(variable => ReferenceEquals(variable, variableNode.Variable))
                                 ?? traversalLegNode.LegState.Variables.FirstOrDefault(variable =>
                                     string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase));
            var sharedVariableId = sourceVariable?.SharedVariableId
                                   ?? traversalLegNode.LegState.SharedVariableId
                                   ?? Guid.Empty;
            var legToken = traversalLegNode.IsFromRoomA ? "a2b" : "b2a";
            source = new SharedRelationshipSource(
                "traversal-leg",
                traversalLegNode.Connection.TraversalConnectionId,
                variableName,
                legToken,
                sharedVariableId);
            return true;
        }

        return false;
    }

    private static bool ParticipantMatchesSource(SharedVariableParticipant participant, SharedRelationshipSource source)
    {
        if (!string.Equals(participant.Kind, source.ParticipantKind, StringComparison.OrdinalIgnoreCase)
            || participant.OwnerId != source.OwnerId
            || !string.Equals(participant.VariableName, source.VariableName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(source.ParticipantKind, "traversal-leg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(participant.Leg ?? string.Empty, source.Leg ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private bool RemoveSharedPropertyRelationship(GamePropertyNodeViewModel sourceVariableNode, SharedPropertyRelationshipReviewItem relationship)
    {
        if (!TryResolveVariableOwnerObject(sourceVariableNode, out var sourceObject)
            || sourceObject.ObjectId == Guid.Empty)
        {
            _projectUiService.ShowWarning("Only object properties can remove shared relationships from this dialog.", "Shared Relationships");
            return false;
        }

        var sourceVariable = sourceObject.Variables.FirstOrDefault(variable => ReferenceEquals(variable, sourceVariableNode.Variable))
                             ?? sourceObject.Variables.FirstOrDefault(variable =>
                                 string.Equals(variable.Name, sourceVariableNode.Variable.Name, StringComparison.OrdinalIgnoreCase));
        if (sourceVariable?.SharedVariableId is not Guid sharedVariableId || sharedVariableId == Guid.Empty)
        {
            return false;
        }

        var sharedVariable = _project.SharedVariables.FirstOrDefault(variable => variable.Id == sharedVariableId);
        if (sharedVariable is null)
        {
            return false;
        }

        var participant = sharedVariable.Participants.FirstOrDefault(candidate =>
            string.Equals(candidate.Kind, relationship.ParticipantKind, StringComparison.OrdinalIgnoreCase)
            && candidate.OwnerId == relationship.ParticipantOwnerId
            && string.Equals(candidate.VariableName, relationship.ParticipantVariableName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Leg ?? string.Empty, relationship.ParticipantLeg ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (participant is null)
        {
            return false;
        }

        UnlinkParticipantSharedVariable(participant, sharedVariable.Id);
        sharedVariable.Participants.Remove(participant);

        var hasCounterparts = sharedVariable.Participants.Any(candidate =>
            !(string.Equals(candidate.Kind, "object", StringComparison.OrdinalIgnoreCase)
              && candidate.OwnerId == sourceObject.ObjectId
              && string.Equals(candidate.VariableName, sourceVariable.Name, StringComparison.OrdinalIgnoreCase)));
        if (!hasCounterparts)
        {
            sourceVariable.SharedVariableId = null;
            sharedVariable.Participants.RemoveAll(candidate =>
                string.Equals(candidate.Kind, "object", StringComparison.OrdinalIgnoreCase)
                && candidate.OwnerId == sourceObject.ObjectId
                && string.Equals(candidate.VariableName, sourceVariable.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (sharedVariable.Participants.Count == 0)
        {
            _project.SharedVariables.Remove(sharedVariable);
        }

        RefreshSharedPropertyIndicators();
        NotifyProjectEdited();
        ExportStatus = "Shared relationship removed.";
        return true;
    }

    private void UnlinkParticipantSharedVariable(SharedVariableParticipant participant, Guid sharedVariableId)
    {
        if (string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindObjectById(_project, participant.OwnerId, out var objectOwner))
            {
                return;
            }

            var variable = objectOwner.Variables.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase)
                && candidate.SharedVariableId == sharedVariableId);
            if (variable is not null)
            {
                variable.SharedVariableId = null;
            }

            return;
        }

        if (string.Equals(participant.Kind, "traversal", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindTraversalConnectionById(_project, participant.OwnerId, out var connection, out _))
            {
                return;
            }

            var variable = connection.Variables.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase)
                && candidate.SharedVariableId == sharedVariableId);
            if (variable is not null)
            {
                variable.SharedVariableId = null;
            }

            return;
        }

        if (string.Equals(participant.Kind, "traversal-leg", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindTraversalConnectionById(_project, participant.OwnerId, out var connection, out _))
            {
                return;
            }

            if (string.Equals(participant.Leg, "a2b", StringComparison.OrdinalIgnoreCase)
                || string.Equals(participant.Leg, "both", StringComparison.OrdinalIgnoreCase))
            {
                UnlinkTraversalLegState(connection.TraversalStateFromA, participant.VariableName, sharedVariableId);
            }

            if (string.Equals(participant.Leg, "b2a", StringComparison.OrdinalIgnoreCase)
                || string.Equals(participant.Leg, "both", StringComparison.OrdinalIgnoreCase))
            {
                UnlinkTraversalLegState(connection.TraversalStateFromB, participant.VariableName, sharedVariableId);
            }
        }
    }

    private static void UnlinkTraversalLegState(TraversalLegState state, string variableName, Guid sharedVariableId)
    {
        var variable = state.Variables.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, variableName, StringComparison.OrdinalIgnoreCase)
            && candidate.SharedVariableId == sharedVariableId);
        if (variable is not null)
        {
            variable.SharedVariableId = null;
        }
    }

    private static string BuildParticipantEndpointDisplay(
        SharedVariableParticipant participant,
        IReadOnlyDictionary<Guid, string> objectPathsById)
    {
        if (string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase))
        {
            if (objectPathsById.TryGetValue(participant.OwnerId, out var objectPath))
            {
                return $"{objectPath}.{participant.VariableName}";
            }

            return $"Object:{participant.OwnerId:N}.{participant.VariableName}";
        }

        if (string.Equals(participant.Kind, "traversal", StringComparison.OrdinalIgnoreCase))
        {
            return $"Traversal:{participant.OwnerId:N}.{participant.VariableName}";
        }

        if (string.Equals(participant.Kind, "traversal-leg", StringComparison.OrdinalIgnoreCase))
        {
            var legLabel = string.IsNullOrWhiteSpace(participant.Leg) ? "leg" : participant.Leg;
            return $"Traversal:{participant.OwnerId:N}.{legLabel}.{participant.VariableName}";
        }

        return $"{participant.Kind}:{participant.OwnerId:N}.{participant.VariableName}";
    }

    private Dictionary<Guid, string> BuildObjectPathIndex()
    {
        var index = new Dictionary<Guid, string>();

        foreach (var planet in _project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        AddObjects(index, room.GameObjects, $"{planet.Name}/{country.Name}/{area.Name}/{room.Name}");
                    }
                }
            }
        }

        AddObjects(index, _project.GlobalScope.GameObjects, "Global Objects");
        AddObjects(index, _project.ObjectTemplates, "Object Templates");
        AddObjects(index, _project.BaseObjects, "Base Objects");

        foreach (var planet in _project.Planets)
        {
            AddObjects(index, planet.BaseObjects, $"{planet.Name}/Base Objects");

            foreach (var country in planet.Countries)
            {
                AddObjects(index, country.BaseObjects, $"{planet.Name}/{country.Name}/Base Objects");

                foreach (var area in country.Areas)
                {
                    AddObjects(index, area.BaseObjects, $"{planet.Name}/{country.Name}/{area.Name}/Base Objects");
                }
            }
        }

        return index;
    }

    private static void AddObjects(Dictionary<Guid, string> index, IEnumerable<GameObject> objects, string prefix)
    {
        foreach (var obj in objects)
        {
            if (obj.ObjectId != Guid.Empty && !index.ContainsKey(obj.ObjectId))
            {
                index[obj.ObjectId] = string.IsNullOrWhiteSpace(prefix) ? obj.Name : $"{prefix}/{obj.Name}";
            }

            AddObjects(index, obj.ContainedObjects, string.IsNullOrWhiteSpace(prefix) ? obj.Name : $"{prefix}/{obj.Name}");
        }
    }

    public IReadOnlyList<SharedVariableDefinition> GetSharedVariableDefinitionsSnapshot()
    {
        return _project.SharedVariables
            .Select(sharedVariable => new SharedVariableDefinition
            {
                Id = sharedVariable.Id,
                Name = sharedVariable.Name,
                DefaultValue = sharedVariable.DefaultValue,
                ValueRestriction = sharedVariable.ValueRestriction,
                Participants = sharedVariable.Participants
                    .Select(participant => new SharedVariableParticipant
                    {
                        Kind = participant.Kind,
                        OwnerId = participant.OwnerId,
                        VariableName = participant.VariableName,
                        Leg = participant.Leg
                    })
                    .ToList()
            })
            .ToList();
    }

    public void ApplySharedVariableDefinitions(IReadOnlyList<SharedVariableDefinition> sharedVariables)
    {
        var updated = (sharedVariables ?? Array.Empty<SharedVariableDefinition>())
            .Select(sharedVariable => new SharedVariableDefinition
            {
                Id = sharedVariable.Id == Guid.Empty ? Guid.NewGuid() : sharedVariable.Id,
                Name = sharedVariable.Name?.Trim() ?? string.Empty,
                DefaultValue = sharedVariable.DefaultValue ?? string.Empty,
                ValueRestriction = sharedVariable.ValueRestriction,
                Participants = sharedVariable.Participants
                    .Select(participant => new SharedVariableParticipant
                    {
                        Kind = participant.Kind,
                        OwnerId = participant.OwnerId,
                        VariableName = participant.VariableName,
                        Leg = participant.Leg
                    })
                    .ToList()
            })
            .Where(sharedVariable => !string.IsNullOrWhiteSpace(sharedVariable.Name))
            .GroupBy(sharedVariable => sharedVariable.Id)
            .Select(group => group.Last())
            .ToList();

        var updatedIds = updated.Select(sharedVariable => sharedVariable.Id).ToHashSet();
        var removedIds = _project.SharedVariables
            .Where(sharedVariable => !updatedIds.Contains(sharedVariable.Id))
            .Select(sharedVariable => sharedVariable.Id)
            .ToHashSet();

        if (removedIds.Count > 0)
        {
            foreach (var variable in EnumerateAllGameProperties())
            {
                if (variable.SharedVariableId.HasValue && removedIds.Contains(variable.SharedVariableId.Value))
                {
                    variable.SharedVariableId = null;
                }
            }

            foreach (var legState in EnumerateTraversalLegStates())
            {
                if (legState.SharedVariableId.HasValue && removedIds.Contains(legState.SharedVariableId.Value))
                {
                    legState.SharedVariableId = null;
                }

                foreach (var legVariable in legState.Variables)
                {
                    if (legVariable.SharedVariableId.HasValue && removedIds.Contains(legVariable.SharedVariableId.Value))
                    {
                        legVariable.SharedVariableId = null;
                    }
                }
            }
        }

        _project.SharedVariables = updated;
        RefreshSharedPropertyIndicators();
        NotifyProjectEdited();
        ExportStatus = "Shared variables updated.";
    }

    private IEnumerable<TraversalLegState> EnumerateTraversalLegStates()
    {
        foreach (var planet in _project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var connection in area.TraversalConnections)
                    {
                        yield return connection.TraversalStateFromA;
                        yield return connection.TraversalStateFromB;
                    }
                }
            }
        }
    }

    private IEnumerable<GamePropertyDefinition> EnumerateAllGameProperties()
    {
        foreach (var variable in _project.GlobalVariables)
        {
            yield return variable;
        }

        foreach (var planet in _project.Planets)
        {
            foreach (var variable in planet.Variables)
            {
                yield return variable;
            }

            foreach (var country in planet.Countries)
            {
                foreach (var variable in country.Variables)
                {
                    yield return variable;
                }

                foreach (var area in country.Areas)
                {
                    foreach (var variable in area.Variables)
                    {
                        yield return variable;
                    }

                    foreach (var connection in area.TraversalConnections)
                    {
                        foreach (var variable in connection.Variables)
                        {
                            yield return variable;
                        }

                        foreach (var legVariable in connection.TraversalStateFromA.Variables)
                        {
                            yield return legVariable;
                        }

                        foreach (var legVariable in connection.TraversalStateFromB.Variables)
                        {
                            yield return legVariable;
                        }
                    }

                    foreach (var room in area.Rooms)
                    {
                        foreach (var variable in room.Variables)
                        {
                            yield return variable;
                        }

                        foreach (var variable in EnumerateObjectVariables(room.GameObjects))
                        {
                            yield return variable;
                        }
                    }
                }
            }
        }

        foreach (var variable in EnumerateObjectVariables(_project.GlobalScope.GameObjects))
        {
            yield return variable;
        }

        foreach (var variable in EnumerateObjectVariables(_project.ObjectTemplates))
        {
            yield return variable;
        }

        foreach (var variable in EnumerateObjectVariables(_project.BaseObjects))
        {
            yield return variable;
        }

        foreach (var variable in _project.Planets
                     .SelectMany(planet => EnumerateObjectVariables(planet.BaseObjects)))
        {
            yield return variable;
        }

        foreach (var variable in _project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => EnumerateObjectVariables(country.BaseObjects)))
        {
            yield return variable;
        }

        foreach (var variable in _project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => EnumerateObjectVariables(area.BaseObjects)))
        {
            yield return variable;
        }
    }

    private static IEnumerable<GamePropertyDefinition> EnumerateObjectVariables(IEnumerable<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            foreach (var variable in obj.Variables)
            {
                yield return variable;
            }

            foreach (var nested in EnumerateObjectVariables(obj.ContainedObjects))
            {
                yield return nested;
            }
        }
    }

    private string BuildVariablePropertyPath(GamePropertyNodeViewModel variableNode)
    {
        if (TryResolveVariableOwnerObject(variableNode, out var ownerObject))
        {
            return $"{ownerObject.Name}.{variableNode.Variable.Name}";
        }

        if (variableNode.VariablesContainer.Parent is TraversalLegNodeViewModel traversalLegNode)
        {
            var legLabel = traversalLegNode.IsFromRoomA ? "A->B" : "B->A";
            return $"Traversal:{traversalLegNode.Connection.TraversalConnectionId:N}.{legLabel}.{variableNode.Variable.Name}";
        }

        return $"{variableNode.Scope}.{variableNode.Variable.Name}";
    }

    private readonly record struct SharedRelationshipSource(
        string ParticipantKind,
        Guid OwnerId,
        string VariableName,
        string? Leg,
        Guid SharedVariableId);

    private static bool TryResolveVariableOwnerObject(GamePropertyNodeViewModel variableNode, out GameObject ownerObject)
    {
        ownerObject = null!;
        switch (variableNode.VariablesContainer.Parent)
        {
            case GameObjectNodeViewModel objectNode:
                ownerObject = objectNode.GameObject;
                return true;
            case GlobalObjectNodeViewModel playerObjectNode:
                ownerObject = playerObjectNode.GameObject;
                return true;
            case TemplateGameObjectNodeViewModel templateObjectNode:
                ownerObject = templateObjectNode.GameObject;
                return true;
            default:
                return false;
        }
    }

    public bool AddObjectToCurrentRoom()
    {
        if (SelectedNode is ObjectTemplatesNodeViewModel { Parent: TemplateRoomNodeViewModel parentTemplateRoomNode })
        {
            return AddNewObject(parentTemplateRoomNode);
        }

        if (SelectedNode is ObjectTemplatesNodeViewModel templatesNode)
        {
            return AddNewTemplateObject(templatesNode);
        }

        if (SelectedNode is TemplateGameObjectNodeViewModel selectedTemplateObjectNode)
        {
            return AddNewContainedTemplateObject(selectedTemplateObjectNode);
        }

        if (SelectedNode is GlobalObjectsNodeViewModel globalObjectsNode)
        {
            return AddNewGlobalGameObject(globalObjectsNode);
        }

        if (SelectedNode is PlanetGameObjectsNodeViewModel planetObjectsNode)
        {
            return AddNewObject(planetObjectsNode.PlanetNode);
        }

        if (SelectedNode is CountryGameObjectsNodeViewModel countryObjectsNode)
        {
            return AddNewObject(countryObjectsNode.CountryNode);
        }

        if (SelectedNode is AreaGameObjectsNodeViewModel areaObjectsNode)
        {
            return AddNewObject(areaObjectsNode.AreaNode);
        }

        if (SelectedNode is GlobalObjectNodeViewModel selectedGlobalObjectNode)
        {
            return AddNewGlobalGameObject(selectedGlobalObjectNode.ParentGlobalObjectsNode);
        }

        if (SelectedNode is RoomNodeViewModel roomNode)
        {
            return AddNewObject(roomNode);
        }

        if (SelectedNode is PlanetNodeViewModel planetNode)
        {
            return AddNewObject(planetNode);
        }

        if (SelectedNode is CountryNodeViewModel countryNode)
        {
            return AddNewObject(countryNode);
        }

        if (SelectedNode is AreaNodeViewModel areaNode)
        {
            return AddNewObject(areaNode);
        }

        if (SelectedNode is TemplateRoomNodeViewModel templateRoomNode)
        {
            return AddNewObject(templateRoomNode);
        }

        if (SelectedNode is RoomGameObjectsNodeViewModel objectsNode)
        {
            return AddNewObject(objectsNode.RoomNode);
        }

        if (SelectedNode is GameObjectNodeViewModel objectNode)
        {
            return AddNewContainedObject(objectNode);
        }

        if (SelectedNode is GlobalObjectNodeViewModel globalObjectNode)
        {
            return AddNewContainedGlobalGameObject(globalObjectNode);
        }

        if (SelectedRoom is null)
        {
            return false;
        }

        var selectedRoomNode = FindRoomNode(SelectedRoom);
        if (selectedRoomNode is not null)
        {
            return AddNewObject(selectedRoomNode);
        }

        var selectedTemplateRoomNode = FindTemplateRoomNode(SelectedRoom);
        return selectedTemplateRoomNode is not null && AddNewObject(selectedTemplateRoomNode);
    }

    public bool RemoveSelectedGameObject()
    {
        if (SelectedNode is TemplateGameObjectNodeViewModel templateObjectNode)
        {
            return RemoveInteractiveObject(templateObjectNode);
        }

        if (SelectedNode is GlobalObjectNodeViewModel playerObjectNode)
        {
            return RemoveInteractiveObject(playerObjectNode);
        }

        if (SelectedNode is GameObjectNodeViewModel objectNode)
        {
            return RemoveInteractiveObject(objectNode);
        }

        if (SelectedGameObject is null)
        {
            return false;
        }

        var playerObjectNodeByModel = HierarchyRoots
            .OfType<ProjectRootNodeViewModel>()
            .SelectMany(root => root.Children.OfType<GlobalObjectsNodeViewModel>())
            .SelectMany(player => EnumerateHierarchyNodes(player.Children).OfType<GlobalObjectNodeViewModel>())
            .FirstOrDefault(node => ReferenceEquals(node.GameObject, SelectedGameObject));

        if (playerObjectNodeByModel is not null)
        {
            return RemoveInteractiveObject(playerObjectNodeByModel);
        }

        var templateNodeByModel = EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<TemplateGameObjectNodeViewModel>()
            .FirstOrDefault(node => ReferenceEquals(node.GameObject, SelectedGameObject));

        if (templateNodeByModel is not null)
        {
            return RemoveInteractiveObject(templateNodeByModel);
        }

        var objectNodeByModel = EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<GameObjectNodeViewModel>()
            .FirstOrDefault(node => ReferenceEquals(node.GameObject, SelectedGameObject));

        return objectNodeByModel is not null && RemoveInteractiveObject(objectNodeByModel);
    }

    public bool EditGlobalSettings(ProjectRootNodeViewModel projectNode)
    {
        var current = new GlobalSettingsEditRequest(
            _project.AutoSaveSeconds,
            _project.StartingPlanetName,
            _project.PlayerCharacterObjectName,
            _project.RoomImageCanvasWidth,
            _project.RoomImageCanvasHeight,
            _project.RoomDesignerGridCellSize,
            _project.StackScaleStepDefault,
            _project.MinStackScaleDefault,
            _project.GameDisplayName,
            _project.GameSummary,
            _project.GamePreviewImages.ToList());

        var startingPlanetOptions = _project.Planets
            .Select(planet => planet.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var playerCharacterObjectOptions = _project.GlobalScope.GameObjects
            .Select(obj => obj.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        playerCharacterObjectOptions.Insert(0, string.Empty);

        if (!_treeContextInteractionService.TryEditGlobalSettings(
            current,
            startingPlanetOptions,
            playerCharacterObjectOptions,
            _project.GlobalScope.SoundEffectLibraryEntries,
            GetProjectSoundEffectCategorySuggestions(),
            out var updated,
            out var updatedSoundEffectLibraryEntries))
        {
            return false;
        }

        var validStartingPlanetName = startingPlanetOptions.Contains(updated.StartingPlanetName, StringComparer.OrdinalIgnoreCase)
            ? updated.StartingPlanetName
            : string.Empty;

        var validPlayerCharacterObjectName = playerCharacterObjectOptions.Contains(updated.PlayerCharacterObjectName, StringComparer.OrdinalIgnoreCase)
            ? updated.PlayerCharacterObjectName
            : string.Empty;

        var hasChanges = _project.AutoSaveSeconds != updated.AutoSaveSeconds
                         || !string.Equals(_project.StartingPlanetName, validStartingPlanetName, StringComparison.Ordinal)
                         || !string.Equals(_project.PlayerCharacterObjectName, validPlayerCharacterObjectName, StringComparison.Ordinal)
                         || _project.RoomImageCanvasWidth != updated.RoomImageCanvasWidth
                         || _project.RoomImageCanvasHeight != updated.RoomImageCanvasHeight
                         || _project.RoomDesignerGridCellSize != updated.RoomDesignerGridCellSize
                         || !AreNearlyEqual(_project.StackScaleStepDefault, updated.StackScaleStepDefault)
                         || !AreNearlyEqual(_project.MinStackScaleDefault, updated.MinStackScaleDefault)
                         || !string.Equals(_project.GameDisplayName, updated.GameDisplayName?.Trim() ?? string.Empty, StringComparison.Ordinal)
                         || !string.Equals(_project.GameSummary, updated.GameSummary?.Trim() ?? string.Empty, StringComparison.Ordinal)
                         || !AreStringListsEquivalent(_project.GamePreviewImages, updated.GamePreviewImages)
                         || !AreSoundEffectLibraryEntriesEquivalent(_project.GlobalScope.SoundEffectLibraryEntries, updatedSoundEffectLibraryEntries);

        if (!hasChanges)
        {
            return false;
        }

        _project.AutoSaveSeconds = updated.AutoSaveSeconds;
        _project.StartingPlanetName = validStartingPlanetName;
        _project.PlayerCharacterObjectName = validPlayerCharacterObjectName;
        _project.RoomImageCanvasWidth = updated.RoomImageCanvasWidth;
        _project.RoomImageCanvasHeight = updated.RoomImageCanvasHeight;
        _project.RoomDesignerGridCellSize = updated.RoomDesignerGridCellSize;
        _project.StackScaleStepDefault = updated.StackScaleStepDefault;
        _project.MinStackScaleDefault = updated.MinStackScaleDefault;
        _project.GameDisplayName = updated.GameDisplayName?.Trim() ?? string.Empty;
        _project.GameSummary = updated.GameSummary?.Trim() ?? string.Empty;
        _project.GamePreviewImages = NormalizePreviewImagePaths(updated.GamePreviewImages);
        _project.GlobalScope.SoundEffectLibraryEntries = updatedSoundEffectLibraryEntries
            .Select(CloneSoundEffectLibraryEntry)
            .ToList();
        RebuildProjectSoundEffectCategorySuggestions();
        ApplyPlayerCharacterVariableContract();

        foreach (var editor in OpenRoomEditors)
        {
            editor.DesignerCanvasWidth = ResolveEffectiveRoomCanvasWidth(editor.Room);
            editor.DesignerCanvasHeight = ResolveEffectiveRoomCanvasHeight(editor.Room);
            editor.RoomGridCellSize = ResolveEffectiveRoomGridCellSize();
        }

        NotifyProjectEdited();
        SelectedNode = projectNode;
        ExportStatus = "Updated global settings.";
        return true;
    }

    private static bool AreStringListsEquivalent(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> NormalizePreviewImagePaths(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool AreSoundEffectLibraryEntriesEquivalent(
        IReadOnlyList<SoundEffectLibraryEntry> left,
        IReadOnlyList<SoundEffectLibraryEntry> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!AreSoundEffectLibraryEntriesEquivalent(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEventSubscriptionsEquivalent(
        IReadOnlyList<EventSubscriptionDefinition> left,
        IReadOnlyList<EventSubscriptionDefinition> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!AreEventSubscriptionsEquivalent(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreTimerDefinitionsEquivalent(
        IReadOnlyList<RuntimeTimerDefinitionDto> left,
        IReadOnlyList<RuntimeTimerDefinitionDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!AreTimerDefinitionsEquivalent(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEventSubscriptionsEquivalent(EventSubscriptionDefinition left, EventSubscriptionDefinition right)
    {
        if (left.Id != right.Id
            || !string.Equals(left.EventKey, right.EventKey, StringComparison.Ordinal)
            || !string.Equals(left.SubscriptionName, right.SubscriptionName, StringComparison.Ordinal)
            || left.IsEnabled != right.IsEnabled
            || !string.Equals(left.Lane, right.Lane, StringComparison.Ordinal)
            || left.DispatchDisposition != right.DispatchDisposition
            || left.SubscriptionVisibleWhenContained != right.SubscriptionVisibleWhenContained
            || left.SubscriptionSourceMatchMode != right.SubscriptionSourceMatchMode
            || left.SubscriptionSourceScopeNodeId != right.SubscriptionSourceScopeNodeId
            || left.SubscriptionSecondarySourceMatchMode != right.SubscriptionSecondarySourceMatchMode
            || left.SubscriptionSecondarySourceScopeNodeId != right.SubscriptionSecondarySourceScopeNodeId)
        {
            return false;
        }

        var leftMappings = left.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>();
        var rightMappings = right.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>();
        if (leftMappings.Count != rightMappings.Count)
        {
            return false;
        }

        for (var mappingIndex = 0; mappingIndex < leftMappings.Count; mappingIndex++)
        {
            var leftMapping = leftMappings[mappingIndex];
            var rightMapping = rightMappings[mappingIndex];
            if (!string.Equals(leftMapping.InputEventPaylloadArgKey, rightMapping.InputEventPaylloadArgKey, StringComparison.Ordinal)
                || !string.Equals(leftMapping.OutputActionPayloadArgKey, rightMapping.OutputActionPayloadArgKey, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var leftBindings = left.ActionBindings ?? new List<EventActionBindingDefinition>();
        var rightBindings = right.ActionBindings ?? new List<EventActionBindingDefinition>();
        if (leftBindings.Count != rightBindings.Count)
        {
            return false;
        }

        for (var i = 0; i < leftBindings.Count; i++)
        {
            var leftBinding = leftBindings[i];
            var rightBinding = rightBindings[i];

            if (leftBinding.Order != rightBinding.Order
                || leftBinding.IsEnabled != rightBinding.IsEnabled
                || leftBinding.Condition?.QuantityEvaluationMode != rightBinding.Condition?.QuantityEvaluationMode
                || !string.Equals(leftBinding.Target?.ActionName, rightBinding.Target?.ActionName, StringComparison.Ordinal)
                || !string.Equals(leftBinding.Target?.OnMissingAction, rightBinding.Target?.OnMissingAction, StringComparison.Ordinal)
                || leftBinding.Target?.StopChainOnFailure != rightBinding.Target?.StopChainOnFailure)
            {
                return false;
            }

            var leftFilters = leftBinding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>();
            var rightFilters = rightBinding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>();
            if (leftFilters.Count != rightFilters.Count)
            {
                return false;
            }

            for (var filterIndex = 0; filterIndex < leftFilters.Count; filterIndex++)
            {
                var leftFilter = leftFilters[filterIndex];
                var rightFilter = rightFilters[filterIndex];
                if (!string.Equals(leftFilter.VariableName, rightFilter.VariableName, StringComparison.Ordinal)
                    || leftFilter.Operator != rightFilter.Operator
                    || !string.Equals(leftFilter.ExpectedValue, rightFilter.ExpectedValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            var leftMappingsByBinding = leftBinding.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>();
            var rightMappingsByBinding = rightBinding.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>();
            if (leftMappingsByBinding.Count != rightMappingsByBinding.Count)
            {
                return false;
            }

            for (var mappingIndex = 0; mappingIndex < leftMappingsByBinding.Count; mappingIndex++)
            {
                var leftMapping = leftMappingsByBinding[mappingIndex];
                var rightMapping = rightMappingsByBinding[mappingIndex];
                if (!string.Equals(leftMapping.InputEventPaylloadArgKey, rightMapping.InputEventPaylloadArgKey, StringComparison.Ordinal)
                    || !string.Equals(leftMapping.OutputActionPayloadArgKey, rightMapping.OutputActionPayloadArgKey, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AreSoundEffectLibraryEntriesEquivalent(SoundEffectLibraryEntry left, SoundEffectLibraryEntry right)
    {
        return left.SoundEffectId == right.SoundEffectId
               && string.Equals(left.SoundEffectKey, right.SoundEffectKey, StringComparison.Ordinal)
               && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
               && string.Equals(left.Category, right.Category, StringComparison.Ordinal)
               && string.Equals(left.AssetRef, right.AssetRef, StringComparison.Ordinal)
               && left.SoundEffectLane == right.SoundEffectLane
               && left.BaseVolumeDb == right.BaseVolumeDb
               && left.FadeInMs == right.FadeInMs
               && left.FadeOutMs == right.FadeOutMs
               && string.Equals(left.RepeatMode, right.RepeatMode, StringComparison.Ordinal)
               && string.Equals(left.ReplayPolicy, right.ReplayPolicy, StringComparison.Ordinal)
               && left.RepeatCount == right.RepeatCount
               && left.StartDelayMs == right.StartDelayMs
               && left.RepeatIntervalMs == right.RepeatIntervalMs
               && left.DurationMs == right.DurationMs
               && left.MaxPlayDurationMs == right.MaxPlayDurationMs
               && left.RepeatDurationMs == right.RepeatDurationMs
               && left.RepeatCooldownMs == right.RepeatCooldownMs
               && string.Equals(left.ConcurrencyGroup, right.ConcurrencyGroup, StringComparison.Ordinal)
               && left.ConcurrencyGroupImportance == right.ConcurrencyGroupImportance
               && left.Importance == right.Importance;
    }

    private static bool AreTimerDefinitionsEquivalent(RuntimeTimerDefinitionDto left, RuntimeTimerDefinitionDto right)
    {
        return string.Equals(left.TimerKey, right.TimerKey, StringComparison.Ordinal)
               && left.ScheduleAfterMs == right.ScheduleAfterMs
               && left.FireMode == right.FireMode
               && left.RepeatMode == right.RepeatMode
               && left.RepeatProgressionMode == right.RepeatProgressionMode
               && left.RepeatIntervalMs == right.RepeatIntervalMs
               && left.RepeatIntervalStepMs == right.RepeatIntervalStepMs
               && left.RepeatProgressionRate == right.RepeatProgressionRate
               && left.RepeatIntervalMinMs == right.RepeatIntervalMinMs
               && left.ShrinkingExpiresUnderMs == right.ShrinkingExpiresUnderMs
               && string.Equals(left.TargetActionRef, right.TargetActionRef, StringComparison.Ordinal)
               && string.Equals(left.OnShrinkExpiryActionRef, right.OnShrinkExpiryActionRef, StringComparison.Ordinal)
               && left.LifetimeOwnerType == right.LifetimeOwnerType
               && left.ConflictBehavior == right.ConflictBehavior
               && left.Enabled == right.Enabled;
    }

    private static SoundEffectLibraryEntry CloneSoundEffectLibraryEntry(SoundEffectLibraryEntry source)
    {
        return new SoundEffectLibraryEntry
        {
            SoundEffectId = source.SoundEffectId,
            SoundEffectKey = source.SoundEffectKey,
            DisplayName = source.DisplayName,
            Category = source.Category,
            AssetRef = source.AssetRef,
            SoundEffectLane = source.SoundEffectLane,
            BaseVolumeDb = source.BaseVolumeDb,
            FadeInMs = source.FadeInMs,
            FadeOutMs = source.FadeOutMs,
            RepeatMode = source.RepeatMode,
            ReplayPolicy = source.ReplayPolicy,
            RepeatCount = source.RepeatCount,
            StartDelayMs = source.StartDelayMs,
            RepeatIntervalMs = source.RepeatIntervalMs,
            DurationMs = source.DurationMs,
            MaxPlayDurationMs = source.MaxPlayDurationMs,
            RepeatDurationMs = source.RepeatDurationMs,
            RepeatCooldownMs = source.RepeatCooldownMs,
            ConcurrencyGroup = source.ConcurrencyGroup,
            ConcurrencyGroupImportance = source.ConcurrencyGroupImportance,
            Importance = source.Importance
        };
    }

    private static List<RuntimeTimerDefinitionDto> CloneTimerDefinitions(IReadOnlyList<RuntimeTimerDefinitionDto> source)
    {
        return source.Select(CloneTimerDefinition).ToList();
    }

    private static RuntimeTimerDefinitionDto CloneTimerDefinition(RuntimeTimerDefinitionDto source)
    {
        return new RuntimeTimerDefinitionDto
        {
            TimerKey = source.TimerKey,
            ScheduleAfterMs = source.ScheduleAfterMs,
            FireMode = source.FireMode,
            RepeatMode = source.RepeatMode,
            RepeatProgressionMode = source.RepeatProgressionMode,
            RepeatIntervalMs = source.RepeatIntervalMs,
            RepeatIntervalStepMs = source.RepeatIntervalStepMs,
            RepeatProgressionRate = source.RepeatProgressionRate,
            RepeatIntervalMinMs = source.RepeatIntervalMinMs,
            ShrinkingExpiresUnderMs = source.ShrinkingExpiresUnderMs,
            TargetActionRef = source.TargetActionRef,
            OnShrinkExpiryActionRef = source.OnShrinkExpiryActionRef,
            LifetimeOwnerType = source.LifetimeOwnerType,
            ConflictBehavior = source.ConflictBehavior,
            Enabled = source.Enabled
        };
    }

    private static string BuildDefaultTimerKey(IList<RuntimeTimerDefinitionDto> entries)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var key = (entry.TimerKey ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                used.Add(key);
            }
        }

        var index = 1;
        while (true)
        {
            var candidate = $"timer.{index}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private IReadOnlyList<string> BuildProjectSoundEffectCategorySuggestions()
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCategory(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            categories.Add(normalized);
        }

        void AddEntries(IEnumerable<SoundEffectLibraryEntry> entries)
        {
            foreach (var entry in entries)
            {
                AddCategory(entry.Category);
            }
        }

        AddCategory("General");
        AddEntries(_project.GlobalScope.SoundEffectLibraryEntries);
        AddEntries(_project.Planets.SelectMany(static planet => planet.SoundEffectLibraryEntries));
        AddEntries(_project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.SoundEffectLibraryEntries));
        AddEntries(_project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.SoundEffectLibraryEntries));
        AddEntries(_project.Planets.SelectMany(static planet => planet.Countries).SelectMany(static country => country.Areas).SelectMany(static area => area.Rooms).SelectMany(static room => room.SoundEffectLibraryEntries));
        AddEntries(_project.RoomTemplates.SelectMany(static room => room.SoundEffectLibraryEntries));
        AddEntries(EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects).SelectMany(static obj => obj.SoundEffectLibraryEntries));
        AddEntries(EnumerateGameObjectsRecursive(_project.ObjectTemplates).SelectMany(static obj => obj.SoundEffectLibraryEntries));
        AddEntries(EnumerateGameObjectsRecursive(_project.BaseObjects).SelectMany(static obj => obj.SoundEffectLibraryEntries));
        AddEntries(_project.Planets
            .SelectMany(static planet => planet.Countries)
            .SelectMany(static country => country.Areas)
            .SelectMany(static area => area.Rooms)
            .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects))
            .SelectMany(static obj => obj.SoundEffectLibraryEntries));
        AddEntries(_project.RoomTemplates
            .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects))
            .SelectMany(static obj => obj.SoundEffectLibraryEntries));

        return categories
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => string.Equals(value, "General", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
    }

    private IReadOnlyList<string> GetProjectSoundEffectCategorySuggestions()
    {
        if (_projectSoundEffectCategories.Count == 0)
        {
            RebuildProjectSoundEffectCategorySuggestions();
        }

        return _projectSoundEffectCategories.ToList();
    }

    private void RebuildProjectSoundEffectCategorySuggestions()
    {
        var refreshed = BuildProjectSoundEffectCategorySuggestions();
        _projectSoundEffectCategories.Clear();
        foreach (var category in refreshed)
        {
            _projectSoundEffectCategories.Add(category);
        }
    }

    private void ApplyPlayerCharacterVariableContract()
    {
        var selectedName = _project.PlayerCharacterObjectName?.Trim() ?? string.Empty;

        // isPlayer is a host routing marker tied to global settings selection.
        // It does not change definition/instance ownership policy for object fields.

        foreach (var globalObject in _project.GlobalScope.GameObjects)
        {
            var isSelected = !string.IsNullOrWhiteSpace(selectedName)
                && string.Equals(globalObject.Name, selectedName, StringComparison.OrdinalIgnoreCase);

            var existing = globalObject.Variables.FirstOrDefault(variable =>
                string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase));

            if (!isSelected)
            {
                if (existing is not null)
                {
                    globalObject.Variables.Remove(existing);
                }

                continue;
            }

            if (existing is null)
            {
                globalObject.Variables.Add(new GamePropertyDefinition
                {
                    Name = "isPlayer",
                    DefaultValue = "true",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                    Lifetime = GamePropertyLifetime.Singleton
                });
                continue;
            }

            existing.DefaultValue = "true";
            existing.ValueRestriction = GamePropertyValueRestriction.TrueFalse;
            existing.Lifetime = GamePropertyLifetime.Singleton;
        }
    }

    public bool EditPlanetSettings(PlanetNodeViewModel planetNode)
    {
        var current = new PlanetSettingsEditRequest(
            planetNode.Planet.Name,
            planetNode.Planet.ProducerNotes,
            planetNode.Planet.StartingCountryName);

        var startingCountryOptions = planetNode.Planet.Countries
            .Select(country => country.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!_treeContextInteractionService.TryEditPlanetSettings(current, startingCountryOptions, out var updated))
        {
            return false;
        }

        var uniqueName = MakeUniqueScopedEntityName(_project.Planets, static planet => planet.Name, updated.Name, planetNode.Planet);
        var validStartingCountryName = startingCountryOptions.Contains(updated.StartingCountryName, StringComparer.OrdinalIgnoreCase)
            ? updated.StartingCountryName
            : string.Empty;

        var hasChanges = !string.Equals(planetNode.Planet.Name, uniqueName, StringComparison.Ordinal)
                         || !string.Equals(planetNode.Planet.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || !string.Equals(planetNode.Planet.StartingCountryName, validStartingCountryName, StringComparison.Ordinal);

        if (!hasChanges)
        {
            return false;
        }

        planetNode.EditableName = uniqueName;
        planetNode.Planet.ProducerNotes = updated.ProducerNotes;
        planetNode.Planet.StartingCountryName = validStartingCountryName;
        RefreshScopedNameIndex(_project.Planets, static planet => planet.Name);

        NotifyProjectEdited();
        SelectedNode = planetNode;
        ExportStatus = "Updated planet settings.";
        return true;
    }

    public bool EditCountrySettings(CountryNodeViewModel countryNode)
    {
        var current = new CountrySettingsEditRequest(
            countryNode.Country.Name,
            countryNode.Country.ProducerNotes,
            countryNode.Country.StartingAreaName);

        var startingAreaOptions = countryNode.Country.Areas
            .Select(area => area.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!_treeContextInteractionService.TryEditCountrySettings(current, startingAreaOptions, out var updated))
        {
            return false;
        }

        if (countryNode.Parent is not PlanetNodeViewModel planetNode)
        {
            return false;
        }

        var uniqueName = MakeUniqueScopedEntityName(planetNode.Planet.Countries, static country => country.Name, updated.Name, countryNode.Country);
        var validStartingAreaName = startingAreaOptions.Contains(updated.StartingAreaName, StringComparer.OrdinalIgnoreCase)
            ? updated.StartingAreaName
            : string.Empty;

        var hasChanges = !string.Equals(countryNode.Country.Name, uniqueName, StringComparison.Ordinal)
                         || !string.Equals(countryNode.Country.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || !string.Equals(countryNode.Country.StartingAreaName, validStartingAreaName, StringComparison.Ordinal);

        if (!hasChanges)
        {
            return false;
        }

        countryNode.EditableName = uniqueName;
        countryNode.Country.ProducerNotes = updated.ProducerNotes;
        countryNode.Country.StartingAreaName = validStartingAreaName;
        RefreshScopedNameIndex(planetNode.Planet.Countries, static country => country.Name);

        NotifyProjectEdited();
        SelectedNode = countryNode;
        ExportStatus = "Updated country settings.";
        return true;
    }

    public bool EditRoomSettings(RoomNodeViewModel roomNode)
    {
        var current = new RoomSettingsEditRequest(
            roomNode.Room.Name,
            roomNode.Room.ProducerNotes,
            roomNode.Room.NameInGame,
            ResolveEffectiveRoomCanvasWidth(roomNode.Room),
            ResolveEffectiveRoomCanvasHeight(roomNode.Room),
            _project.RoomDesignerGridCellSize);

        if (!_treeContextInteractionService.TryEditRoomSettings(current, out var updated))
        {
            return false;
        }

        if (roomNode.Parent is not AreaNodeViewModel areaNode)
        {
            return false;
        }

        var uniqueName = MakeUniqueScopedEntityName(areaNode.Area.Rooms, static room => room.Name, updated.Name, roomNode.Room);
        var hasChanges = !string.Equals(roomNode.Room.Name, uniqueName, StringComparison.Ordinal)
                         || !string.Equals(roomNode.Room.NameInGame, updated.NameInGame, StringComparison.Ordinal)
                         || !string.Equals(roomNode.Room.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || roomNode.Room.RoomImageCanvasWidth != updated.RoomCanvasWidth
                         || roomNode.Room.RoomImageCanvasHeight != updated.RoomCanvasHeight;

        if (!hasChanges)
        {
            return false;
        }

        roomNode.EditableName = uniqueName;
        roomNode.Room.NameInGame = updated.NameInGame;
        roomNode.Room.ProducerNotes = updated.ProducerNotes;
        roomNode.Room.RoomImageCanvasWidth = updated.RoomCanvasWidth;
        roomNode.Room.RoomImageCanvasHeight = updated.RoomCanvasHeight;
        RefreshScopedNameIndex(areaNode.Area.Rooms, static room => room.Name);

        foreach (var editor in OpenRoomEditors.Where(tab => ReferenceEquals(tab.Room, roomNode.Room) || tab.Room.Id == roomNode.Room.Id))
        {
            editor.DesignerCanvasWidth = ResolveEffectiveRoomCanvasWidth(roomNode.Room);
            editor.DesignerCanvasHeight = ResolveEffectiveRoomCanvasHeight(roomNode.Room);
            editor.RoomGridCellSize = ResolveEffectiveRoomGridCellSize();
        }

        NotifyProjectEdited();
        SelectedNode = roomNode;
        ExportStatus = "Updated room settings.";
        return true;
    }

    public bool EditRoomSettings(TemplateRoomNodeViewModel templateRoomNode)
    {
        var current = new RoomSettingsEditRequest(
            templateRoomNode.Room.Name,
            templateRoomNode.Room.ProducerNotes,
            templateRoomNode.Room.NameInGame,
            ResolveEffectiveRoomCanvasWidth(templateRoomNode.Room),
            ResolveEffectiveRoomCanvasHeight(templateRoomNode.Room),
            _project.RoomDesignerGridCellSize);

        if (!_treeContextInteractionService.TryEditRoomSettings(current, out var updated))
        {
            return false;
        }

        var uniqueName = MakeUniqueScopedEntityName(_project.RoomTemplates, static room => room.Name, updated.Name, templateRoomNode.Room);
        var hasChanges = !string.Equals(templateRoomNode.Room.Name, uniqueName, StringComparison.Ordinal)
                         || !string.Equals(templateRoomNode.Room.NameInGame, updated.NameInGame, StringComparison.Ordinal)
                         || !string.Equals(templateRoomNode.Room.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || templateRoomNode.Room.RoomImageCanvasWidth != updated.RoomCanvasWidth
                         || templateRoomNode.Room.RoomImageCanvasHeight != updated.RoomCanvasHeight;

        if (!hasChanges)
        {
            return false;
        }

        templateRoomNode.EditableName = uniqueName;
        templateRoomNode.Room.NameInGame = updated.NameInGame;
        templateRoomNode.Room.ProducerNotes = updated.ProducerNotes;
        templateRoomNode.Room.RoomImageCanvasWidth = updated.RoomCanvasWidth;
        templateRoomNode.Room.RoomImageCanvasHeight = updated.RoomCanvasHeight;
        RefreshScopedNameIndex(_project.RoomTemplates, static room => room.Name);

        NotifyProjectEdited();
        SelectedNode = templateRoomNode;
        ExportStatus = "Updated room template settings.";
        return true;
    }

    public bool EditObjectBasicProperties(GameObjectNodeViewModel objectNode)
    {
        var linkedDefinition = TryResolveLinkedDefinitionObject(objectNode.GameObject);
        GameObject? ResolveLinkedDefinition(Guid id) => linkedDefinition is not null && id == linkedDefinition.ObjectId
            ? linkedDefinition
            : null;

        var currentName = EffectiveObjectFieldResolver.GetEffectiveName(objectNode.GameObject, ResolveLinkedDefinition);
        var currentNameInGame = EffectiveObjectFieldResolver.GetEffectiveNameInGame(objectNode.GameObject, ResolveLinkedDefinition);
        var currentDescription = EffectiveObjectFieldResolver.GetEffectiveDescription(objectNode.GameObject, ResolveLinkedDefinition);
        var current = CreateObjectBasicPropertiesEditRequest(
            objectNode.GameObject,
            currentName,
            currentNameInGame,
            currentDescription,
            GetScopedObjectSiblings(objectNode),
            "objects",
            includeImageVariantChooserOptions: true,
            includeLockOperationRequirements: true);

        if (linkedDefinition is not null)
        {
            current = current with
            {
                LinkedEditNotice = BuildLinkedInstanceEditNotice(objectNode.GameObject),
                LinkedBaseObjectName = linkedDefinition.Name
            };
        }

        if (!_treeContextInteractionService.TryEditObjectBasicProperties(current, out var updated))
        {
            return false;
        }

        if (linkedDefinition is not null)
        {
            updated = EnforceLinkedInstanceEditableFieldPolicy(objectNode.GameObject, updated);
        }

        var scopedObjects = GetScopedObjectSiblings(objectNode);
        if (!TryValidateScopedObjectName(scopedObjects, objectNode.GameObject, updated.Name, out var normalizedName))
        {
            return false;
        }

        var updatedNameSynonyms = ParseObjectNameSynonyms(updated.ObjectNameSynonyms);
        var overridePlan = EffectiveObjectFieldResolver.BuildDefinitionOwnedOverridePlan(
            objectNode.GameObject,
            linkedDefinition,
            updated.NameInGame,
            updatedNameSynonyms,
            updated.Description);

        var hasNameInGameOverrideChange = overridePlan.HasNameInGameOverrideChange;
        var hasNameSynonymsOverrideChange = overridePlan.HasNameSynonymsOverrideChange;
        var hasDescriptionOverrideChange = overridePlan.HasDescriptionOverrideChange;

        var hasChanges = !string.Equals(objectNode.GameObject.Name, normalizedName, StringComparison.Ordinal)
                         || !string.Equals(objectNode.GameObject.NameInGame, updated.NameInGame, StringComparison.Ordinal)
                         || hasNameInGameOverrideChange
                         || !objectNode.GameObject.NameSynonyms.SequenceEqual(updatedNameSynonyms, StringComparer.OrdinalIgnoreCase)
                         || hasNameSynonymsOverrideChange
                         || !string.Equals(objectNode.GameObject.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || objectNode.GameObject.IsInventoriable != updated.IsInventoriable
                         || objectNode.GameObject.InventoryPointsDefaultValue != updated.InventoryPointsDefaultValue
                         || objectNode.GameObject.IsContainer != updated.IsContainer
                         || objectNode.GameObject.ContainerPointsDefaultValue != updated.ContainerPointsDefaultValue
                         || objectNode.GameObject.IsCapacityPointShareDividerEnabled != updated.IsCapacityPointShareDividerEnabled
                         || objectNode.GameObject.CapacityPointShareDividerDefaultValue != updated.CapacityPointShareDividerDefaultValue
                         || objectNode.GameObject.IsOpenable != updated.IsOpenable
                         || objectNode.GameObject.IsOpenDefaultValue != updated.IsOpenDefaultValue
                         || objectNode.GameObject.IsLockable != updated.IsLockable
                         || objectNode.GameObject.IsLockedDefaultValue != updated.IsLockedDefaultValue
                         || objectNode.GameObject.IsActivatable != updated.IsActivatable
                         || objectNode.GameObject.IsActiveDefaultValue != updated.IsActiveDefaultValue
                         || objectNode.GameObject.IsHidable != updated.IsHidable
                         || objectNode.GameObject.IsHiddenDefaultValue != updated.IsHiddenDefaultValue
                         || objectNode.GameObject.IsQuantifiable != updated.IsQuantifiable
                         || objectNode.GameObject.Quantity != updated.Quantity
                         || !string.Equals(objectNode.GameObject.QuantifiablePlacementDistributionMode, updated.QuantifiablePlacementDistributionMode, StringComparison.Ordinal)
                         || objectNode.GameObject.IsMovable != updated.IsMovable
                         || objectNode.GameObject.IsMovableDefaultValue != updated.IsMovableDefaultValue
                         || objectNode.GameObject.SpatialType != NormalizeSpatialType(updated.SpatialType)
                         || objectNode.GameObject.StackGroup != updated.StackGroup
                         || objectNode.GameObject.FootprintWidthCells != updated.FootprintWidthCells
                         || objectNode.GameObject.FootprintHeightCells != updated.FootprintHeightCells
                         || !string.Equals(objectNode.GameObject.FootprintOrientation, updated.FootprintOrientation, StringComparison.Ordinal)
                         || !string.Equals(objectNode.GameObject.HeadingDirection, updated.HeadingDirection, StringComparison.Ordinal)
                         || objectNode.GameObject.ObjectHeightUnits != updated.ObjectHeightUnits
                         || objectNode.GameObject.HeightInRoom != updated.HeightInRoom
                         || !Nullable.Equals(objectNode.GameObject.StackScaleStepOverride, updated.StackScaleStepOverride)
                         || !Nullable.Equals(objectNode.GameObject.MinStackScaleOverride, updated.MinStackScaleOverride)
                         || !AreEquivalentMovementRestrictions(objectNode.GameObject.MovementRestrictions, updated.MovementRestrictions)
                         || objectNode.GameObject.IsCompositeTarget != updated.IsCompositeTarget
                         || objectNode.GameObject.IsCompositeReversible != updated.IsCompositeReversible
                         || !string.Equals(objectNode.GameObject.CompositePartRequirementMode, updated.CompositePartRequirementMode, StringComparison.Ordinal)
                         || objectNode.GameObject.CompositeMinimumRequiredPartCount != updated.CompositeMinimumRequiredPartCount
                         || !AreEquivalentCompositePartRequirements(objectNode.GameObject.CompositeRequiredParts, ResolveCompositePartRequirements(updated))
                         || !string.Equals(objectNode.GameObject.Description, updated.Description, StringComparison.Ordinal)
                         || Math.Abs(objectNode.GameObject.ImageRotationDegrees - updated.ImageRotationDegrees) > 0.0001
                         || !AreEquivalentImageVariants(objectNode.GameObject.ImageVariants, updated.ImageVariants)
                         || !string.Equals(objectNode.GameObject.ImageVariantChooserScript, updated.ImageVariantChooserScript, StringComparison.Ordinal)
                         || !AreEquivalentLockOperationRequirements(objectNode.GameObject.LockOperationRequirements, updated.LockOperationRequirements)
                         || hasDescriptionOverrideChange;

        if (!hasChanges)
        {
            return false;
        }

        var currentIsIndividualQuantifiable = objectNode.GameObject.IsQuantifiable
            && string.Equals(objectNode.GameObject.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase);
        var updatedIsIndividualQuantifiable = updated.IsQuantifiable
            && string.Equals(updated.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase);
        var shouldExpandIndividualInstances = updatedIsIndividualQuantifiable
            && updated.Quantity > 1
            && (!currentIsIndividualQuantifiable
                || objectNode.GameObject.Quantity != updated.Quantity);
        objectNode.EditableName = normalizedName;
        if (linkedDefinition is not null)
        {
            EffectiveObjectFieldResolver.ApplyDefinitionOwnedOverrides(objectNode.GameObject, overridePlan);
        }
        objectNode.GameObject.NameInGame = updated.NameInGame;
        objectNode.GameObject.NameSynonyms = linkedDefinition is null
            ? updatedNameSynonyms
            : ParseObjectNameSynonyms(linkedDefinition.NameSynonyms);
        objectNode.GameObject.ProducerNotes = updated.ProducerNotes;
        objectNode.GameObject.IsInventoriable = updated.IsInventoriable;
        objectNode.GameObject.InventoryPointsDefaultValue = updated.InventoryPointsDefaultValue;
        objectNode.GameObject.IsContainer = updated.IsContainer;
        objectNode.GameObject.ContainerPointsDefaultValue = updated.ContainerPointsDefaultValue;
        objectNode.GameObject.IsCapacityPointShareDividerEnabled = updated.IsCapacityPointShareDividerEnabled;
        objectNode.GameObject.CapacityPointShareDividerDefaultValue = updated.CapacityPointShareDividerDefaultValue;
        objectNode.GameObject.IsOpenable = updated.IsOpenable;
        objectNode.GameObject.IsOpenDefaultValue = updated.IsOpenDefaultValue;
        objectNode.GameObject.IsLockable = updated.IsLockable;
        objectNode.GameObject.IsLockedDefaultValue = updated.IsLockedDefaultValue;
        objectNode.GameObject.IsActivatable = updated.IsActivatable;
        objectNode.GameObject.IsActiveDefaultValue = updated.IsActiveDefaultValue;
        objectNode.GameObject.IsHidable = updated.IsHidable;
        objectNode.GameObject.IsHiddenDefaultValue = updated.IsHiddenDefaultValue;
        objectNode.GameObject.IsQuantifiable = updated.IsQuantifiable;
        objectNode.GameObject.Quantity = shouldExpandIndividualInstances ? 1 : updated.Quantity;
        objectNode.GameObject.QuantifiablePlacementDistributionMode = updated.QuantifiablePlacementDistributionMode;
        objectNode.GameObject.IsMovable = updated.IsMovable;
        objectNode.GameObject.IsMovableDefaultValue = updated.IsMovableDefaultValue;
        objectNode.GameObject.SpatialType = NormalizeSpatialType(updated.SpatialType);
        objectNode.GameObject.StackGroup = updated.StackGroup;
        objectNode.GameObject.FootprintWidthCells = updated.FootprintWidthCells;
        objectNode.GameObject.FootprintHeightCells = updated.FootprintHeightCells;
        objectNode.GameObject.FootprintOrientation = updated.FootprintOrientation;
        objectNode.GameObject.HeadingDirection = updated.HeadingDirection;
        objectNode.GameObject.ObjectHeightUnits = updated.ObjectHeightUnits;
        objectNode.GameObject.HeightInRoom = updated.HeightInRoom;
        objectNode.GameObject.StackScaleStepOverride = updated.StackScaleStepOverride;
        objectNode.GameObject.MinStackScaleOverride = updated.MinStackScaleOverride;
        objectNode.GameObject.MovementRestrictions = CloneMovementRestrictions(updated.MovementRestrictions);
        objectNode.GameObject.IsCompositeTarget = updated.IsCompositeTarget;
        objectNode.GameObject.IsCompositeReversible = updated.IsCompositeReversible;
        objectNode.GameObject.CompositePartRequirementMode = updated.CompositePartRequirementMode;
        objectNode.GameObject.CompositeMinimumRequiredPartCount = updated.CompositeMinimumRequiredPartCount;
        objectNode.GameObject.CompositeRequiredParts = ResolveCompositePartRequirements(updated);
        objectNode.GameObject.Description = updated.Description;
        objectNode.GameObject.ImageRotationDegrees = updated.ImageRotationDegrees;
        objectNode.GameObject.ImageVariants = NormalizeObjectImageVariantsForModel(updated.ImageVariants, updated.FullImagePath);
        objectNode.GameObject.ImageVariantChooserScript = updated.ImageVariantChooserScript?.Trim() ?? string.Empty;
        objectNode.GameObject.LockOperationRequirements = CloneLockOperationRequirements(updated.LockOperationRequirements);
        var addedInstanceCount = 0;
        if (shouldExpandIndividualInstances)
        {
            objectNode.GameObject.LinkedBaseObjectId = null;
            objectNode.GameObject.LinkActionsToBaseObject = false;
            var parentObjectNode = objectNode.ParentObjectNode;
            var parentObjectsNode = objectNode.ParentObjectsNode;
            for (var index = 1; index < updated.Quantity; index++)
            {
                var instance = CloneTemplateObject(objectNode.GameObject);
                instance.Quantity = 1;
                ConfigureAsLinkedRoomInstance(instance, objectNode.GameObject.ObjectId);
                instance.Name = normalizedName;

                var addedToModel = parentObjectNode is not null
                    ? parentObjectNode.GameObject.AddChildScope(instance)
                    : parentObjectsNode.ScopeNode.AddChildScope(instance);
                if (!addedToModel)
                {
                    continue;
                }

                var instanceNode = parentObjectNode is not null
                    ? CreateNestedRoomObjectNode(instance, parentObjectNode)
                    : CreateScopedObjectNode(instance, parentObjectsNode);

                if (parentObjectNode is not null)
                {
                    var parentChildrenGroup = GetOrCreateChildrenGroup(parentObjectNode);
                    parentChildrenGroup.Children.Add(instanceNode);
                    parentObjectNode.IsExpanded = true;
                }
                else
                {
                    parentObjectsNode.Children.Add(instanceNode);
                    parentObjectsNode.IsExpanded = true;
                }

                addedInstanceCount++;
            }
        }

        RefreshScopedNameIndex(scopedObjects, static obj => obj.Name);

        NotifyProjectEdited();
        SelectedNode = objectNode;
        ExportStatus = shouldExpandIndividualInstances
            ? $"Updated game object properties and materialized {addedInstanceCount + 1} individual instances."
            : "Updated game object properties.";
        return true;
    }

    private GameObject? TryResolveDefinitionObject(GameObject instance)
    {
        if (!instance.LinkedBaseObjectId.HasValue)
        {
            return null;
        }

        var lookup = BuildObjectScopePathLookup();
        return lookup.TryGetValue(instance.LinkedBaseObjectId.Value, out var entry)
            ? entry.Object
            : null;
    }

    private GameObject? TryResolveLinkedDefinitionObject(GameObject instance)
    {
        var definition = TryResolveDefinitionObject(instance);
        if (definition is null)
        {
            return null;
        }

        // Guardrail for stale metadata: an object must not resolve itself as its own linked definition.
        return ReferenceEquals(definition, instance) || definition.ObjectId == instance.ObjectId
            ? null
            : definition;
    }

    private (double StackScaleStepOverride, double MinStackScaleOverride) ResolveObjectStackScaleEditorDefaults(GameObject gameObject)
    {
        var stackScaleStep = gameObject.StackScaleStepOverride ?? _project.StackScaleStepDefault;
        var minStackScale = gameObject.MinStackScaleOverride ?? _project.MinStackScaleDefault;
        return (stackScaleStep, minStackScale);
    }

    private static string GetEffectiveLinkedStringField(GameObject instance, string fieldKey, string definitionValue)
    {
        if (instance.InstanceOverrides.TryGetValue(fieldKey, out var overrideValue))
        {
            return overrideValue ?? string.Empty;
        }

        return definitionValue ?? string.Empty;
    }

    private static ObjectBasicPropertiesEditRequest EnforceLinkedInstanceEditableFieldPolicy(GameObject instance, ObjectBasicPropertiesEditRequest updated)
    {
        return updated with
        {
            ProducerNotes = instance.ProducerNotes,
            IsInventoriable = instance.IsInventoriable,
            InventoryPointsDefaultValue = instance.InventoryPointsDefaultValue,
            IsContainer = instance.IsContainer,
            ContainerPointsDefaultValue = instance.ContainerPointsDefaultValue,
            IsCapacityPointShareDividerEnabled = instance.IsCapacityPointShareDividerEnabled,
            CapacityPointShareDividerDefaultValue = instance.CapacityPointShareDividerDefaultValue,
            IsOpenable = instance.IsOpenable,
            IsOpenDefaultValue = instance.IsOpenDefaultValue,
            IsLockable = instance.IsLockable,
            IsLockedDefaultValue = instance.IsLockedDefaultValue,
            IsActivatable = instance.IsActivatable,
            IsActiveDefaultValue = instance.IsActiveDefaultValue,
            IsHidable = instance.IsHidable,
            IsHiddenDefaultValue = instance.IsHiddenDefaultValue,
            IsQuantifiable = instance.IsQuantifiable,
            QuantifiablePlacementDistributionMode = instance.QuantifiablePlacementDistributionMode,
            IsMovable = instance.IsMovable,
            IsMovableDefaultValue = instance.IsMovableDefaultValue,
            SpatialType = instance.SpatialType,
            StackGroup = instance.StackGroup,
            FootprintWidthCells = instance.FootprintWidthCells,
            FootprintHeightCells = instance.FootprintHeightCells,
            FootprintOrientation = instance.FootprintOrientation,
            HeadingDirection = instance.HeadingDirection,
            ObjectHeightUnits = instance.ObjectHeightUnits,
            HeightInRoom = instance.HeightInRoom,
            StackScaleStepOverride = instance.StackScaleStepOverride,
            MinStackScaleOverride = instance.MinStackScaleOverride,
            MovementRestrictions = CloneMovementRestrictions(instance.MovementRestrictions),
            IsCompositeTarget = instance.IsCompositeTarget,
            IsCompositeReversible = instance.IsCompositeReversible,
            CompositePartRequirementMode = instance.CompositePartRequirementMode,
            CompositeMinimumRequiredPartCount = instance.CompositeMinimumRequiredPartCount,
            CompositeRequiredPartObjectIds = ExpandCompositePartObjectIds(instance.CompositeRequiredParts),
            CompositeRequiredParts = CloneCompositePartRequirements(instance.CompositeRequiredParts),
            FullImagePath = instance.ResolveDefaultImagePath(),
            ImageRotationDegrees = instance.ImageRotationDegrees,
            ImageVariants = instance.ImageVariants,
            ImageVariantChooserScript = instance.ImageVariantChooserScript,
            LockOperationRequirements = CloneLockOperationRequirements(instance.LockOperationRequirements)
        };
    }

    private ObjectBasicPropertiesEditRequest CreateObjectBasicPropertiesEditRequest(
        GameObject gameObject,
        string currentName,
        string currentNameInGame,
        string currentDescription,
        IEnumerable<GameObject> compositePartSource,
        string scopeTag,
        bool includeImageVariantChooserOptions,
        bool includeLockOperationRequirements)
    {
        var (stackScaleStepOverride, minStackScaleOverride) = ResolveObjectStackScaleEditorDefaults(gameObject);

        var lockOperationRequirements = includeLockOperationRequirements
            ? CloneLockOperationRequirements(gameObject.LockOperationRequirements)
            : null;

        if (includeImageVariantChooserOptions)
        {
            var imageVariantChooserVariableChoices = BuildImageVariantChooserVariableChoices(gameObject);
            var imageVariantChooserReferenceTokens = imageVariantChooserVariableChoices
                .Select(choice => choice.Value)
                .Where(static token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static token => token, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ObjectBasicPropertiesEditRequest(
                currentName,
                currentNameInGame,
                gameObject.ProducerNotes,
                gameObject.IsInventoriable,
                gameObject.InventoryPointsDefaultValue,
                gameObject.IsContainer,
                gameObject.ContainerPointsDefaultValue,
                gameObject.IsCapacityPointShareDividerEnabled,
                gameObject.CapacityPointShareDividerDefaultValue,
                gameObject.IsOpenable,
                gameObject.IsOpenDefaultValue,
                gameObject.IsLockable,
                gameObject.IsLockedDefaultValue,
                gameObject.IsActivatable,
                gameObject.IsActiveDefaultValue,
                gameObject.IsHidable,
                gameObject.IsHiddenDefaultValue,
                gameObject.IsQuantifiable,
                gameObject.Quantity,
                gameObject.QuantifiablePlacementDistributionMode,
                gameObject.IsCompositeTarget,
                gameObject.IsCompositeReversible,
                gameObject.CompositePartRequirementMode,
                gameObject.CompositeMinimumRequiredPartCount,
                ExpandCompositePartObjectIds(gameObject.CompositeRequiredParts),
                BuildAvailableCompositePartOptions(compositePartSource, gameObject),
                currentDescription,
                gameObject.ResolveDefaultImagePath(),
                gameObject.ImageRotationDegrees,
                _projectFilePath ?? string.Empty,
                scopeTag,
                ImageVariantChooserReferenceTokens: imageVariantChooserReferenceTokens,
                ImageVariantChooserVariableChoices: imageVariantChooserVariableChoices,
                ImageVariantChooserVariableScope: PropertyResolutionScope.Object,
                ImageVariants: gameObject.ImageVariants,
                ImageVariantChooserScript: gameObject.ImageVariantChooserScript,
                LockOperationRequirements: lockOperationRequirements,
                AvailableLockKeyOptions: BuildAvailableLockKeyOptions(gameObject),
                CompositeRequiredParts: CloneCompositePartRequirements(gameObject.CompositeRequiredParts),
                ObjectNameSynonyms: SerializeObjectNameSynonyms(gameObject.NameSynonyms),
                IsMovable: gameObject.IsMovable,
                IsMovableDefaultValue: gameObject.IsMovableDefaultValue,
                SpatialType: gameObject.SpatialType,
                StackGroup: gameObject.StackGroup,
                FootprintWidthCells: gameObject.FootprintWidthCells,
                FootprintHeightCells: gameObject.FootprintHeightCells,
                FootprintOrientation: gameObject.FootprintOrientation,
                HeadingDirection: gameObject.HeadingDirection,
                ObjectHeightUnits: gameObject.ObjectHeightUnits,
                HeightInRoom: gameObject.HeightInRoom,
                StackScaleStepOverride: stackScaleStepOverride,
                MinStackScaleOverride: minStackScaleOverride,
                MovementRestrictions: CloneMovementRestrictions(gameObject.MovementRestrictions),
                ProjectRoomGridCellSize: _project.RoomDesignerGridCellSize,
                ProjectRoomCanvasWidth: _project.RoomImageCanvasWidth,
                ProjectRoomCanvasHeight: _project.RoomImageCanvasHeight);
        }

        return new ObjectBasicPropertiesEditRequest(
            currentName,
            currentNameInGame,
            gameObject.ProducerNotes,
            gameObject.IsInventoriable,
            gameObject.InventoryPointsDefaultValue,
            gameObject.IsContainer,
            gameObject.ContainerPointsDefaultValue,
            gameObject.IsCapacityPointShareDividerEnabled,
            gameObject.CapacityPointShareDividerDefaultValue,
            gameObject.IsOpenable,
            gameObject.IsOpenDefaultValue,
            gameObject.IsLockable,
            gameObject.IsLockedDefaultValue,
            gameObject.IsActivatable,
            gameObject.IsActiveDefaultValue,
            gameObject.IsHidable,
            gameObject.IsHiddenDefaultValue,
            gameObject.IsQuantifiable,
            gameObject.Quantity,
            gameObject.QuantifiablePlacementDistributionMode,
            gameObject.IsCompositeTarget,
            gameObject.IsCompositeReversible,
            gameObject.CompositePartRequirementMode,
            gameObject.CompositeMinimumRequiredPartCount,
            ExpandCompositePartObjectIds(gameObject.CompositeRequiredParts),
            BuildAvailableCompositePartOptions(compositePartSource, gameObject),
            currentDescription,
            gameObject.ResolveDefaultImagePath(),
            gameObject.ImageRotationDegrees,
            _projectFilePath ?? string.Empty,
            scopeTag,
            ImageVariants: gameObject.ImageVariants,
            ImageVariantChooserScript: gameObject.ImageVariantChooserScript,
            LockOperationRequirements: lockOperationRequirements,
            AvailableLockKeyOptions: BuildAvailableLockKeyOptions(gameObject),
            CompositeRequiredParts: CloneCompositePartRequirements(gameObject.CompositeRequiredParts),
            ObjectNameSynonyms: SerializeObjectNameSynonyms(gameObject.NameSynonyms),
            IsMovable: gameObject.IsMovable,
            IsMovableDefaultValue: gameObject.IsMovableDefaultValue,
            SpatialType: gameObject.SpatialType,
            StackGroup: gameObject.StackGroup,
            FootprintWidthCells: gameObject.FootprintWidthCells,
            FootprintHeightCells: gameObject.FootprintHeightCells,
            FootprintOrientation: gameObject.FootprintOrientation,
            HeadingDirection: gameObject.HeadingDirection,
            ObjectHeightUnits: gameObject.ObjectHeightUnits,
            HeightInRoom: gameObject.HeightInRoom,
            StackScaleStepOverride: stackScaleStepOverride,
            MinStackScaleOverride: minStackScaleOverride,
            MovementRestrictions: CloneMovementRestrictions(gameObject.MovementRestrictions),
            ProjectRoomGridCellSize: _project.RoomDesignerGridCellSize,
            ProjectRoomCanvasWidth: _project.RoomImageCanvasWidth,
            ProjectRoomCanvasHeight: _project.RoomImageCanvasHeight);
    }

    private static bool AreEquivalentImageVariants(IReadOnlyList<ObjectImageVariant>? left, IReadOnlyList<ObjectImageVariant>? right)
    {
        var normalizedLeft = NormalizeObjectImageVariantsForModel(left, string.Empty);
        var normalizedRight = NormalizeObjectImageVariantsForModel(right, string.Empty);

        if (normalizedLeft.Count != normalizedRight.Count)
        {
            return false;
        }

        for (var index = 0; index < normalizedLeft.Count; index++)
        {
            var lhs = normalizedLeft[index];
            var rhs = normalizedRight[index];
            if (!string.Equals(lhs.VariantName, rhs.VariantName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(lhs.FullImagePath, rhs.FullImagePath, StringComparison.Ordinal)
                || Math.Abs(lhs.ImageLocalAlignmentRotationDegrees - rhs.ImageLocalAlignmentRotationDegrees) > 0.0001
                || Math.Abs(lhs.ImageLocalAlignmentOffsetX - rhs.ImageLocalAlignmentOffsetX) > 0.0001
                || Math.Abs(lhs.ImageLocalAlignmentOffsetY - rhs.ImageLocalAlignmentOffsetY) > 0.0001
                || Math.Abs(lhs.ImageScale - rhs.ImageScale) > 0.0001
                || lhs.IsDefault != rhs.IsDefault)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalentMovementRestrictions(ObjectMovementRestrictions? left, ObjectMovementRestrictions? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

         return Nullable.Equals(left.MultiLegMaxTotalDistanceCells, right.MultiLegMaxTotalDistanceCells)
             && AreEquivalentMovementRestrictionCategory(left.FirstUnstacked, right.FirstUnstacked)
               && AreEquivalentMovementRestrictionCategory(left.FirstStacked, right.FirstStacked)
               && AreEquivalentMovementRestrictionCategory(left.SubsequentUnstacked, right.SubsequentUnstacked)
               && AreEquivalentMovementRestrictionCategory(left.SubsequentStacked, right.SubsequentStacked);
    }

    private static bool AreEquivalentMovementRestrictionCategory(ObjectMovementRestrictionCategory left, ObjectMovementRestrictionCategory right)
    {
        return AreEquivalentMovementRestrictionRule(left.N, right.N)
               && AreEquivalentMovementRestrictionRule(left.NE, right.NE)
               && AreEquivalentMovementRestrictionRule(left.E, right.E)
               && AreEquivalentMovementRestrictionRule(left.SE, right.SE)
               && AreEquivalentMovementRestrictionRule(left.S, right.S)
               && AreEquivalentMovementRestrictionRule(left.SW, right.SW)
               && AreEquivalentMovementRestrictionRule(left.W, right.W)
               && AreEquivalentMovementRestrictionRule(left.NW, right.NW);
    }

    private static bool AreEquivalentMovementRestrictionRule(ObjectMovementRestrictionRule left, ObjectMovementRestrictionRule right)
    {
        return Nullable.Equals(left.MaxDistance, right.MaxDistance)
               && Nullable.Equals(left.AllowJumpOver, right.AllowJumpOver);
    }

    private static ObjectMovementRestrictions? CloneMovementRestrictions(ObjectMovementRestrictions? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ObjectMovementRestrictions
        {
            MultiLegMaxTotalDistanceCells = source.MultiLegMaxTotalDistanceCells,
            FirstUnstacked = CloneMovementRestrictionCategory(source.FirstUnstacked),
            FirstStacked = CloneMovementRestrictionCategory(source.FirstStacked),
            SubsequentUnstacked = CloneMovementRestrictionCategory(source.SubsequentUnstacked),
            SubsequentStacked = CloneMovementRestrictionCategory(source.SubsequentStacked)
        };
    }

    private static ObjectMovementRestrictionCategory CloneMovementRestrictionCategory(ObjectMovementRestrictionCategory source)
    {
        return new ObjectMovementRestrictionCategory
        {
            N = CloneMovementRestrictionRule(source.N),
            NE = CloneMovementRestrictionRule(source.NE),
            E = CloneMovementRestrictionRule(source.E),
            SE = CloneMovementRestrictionRule(source.SE),
            S = CloneMovementRestrictionRule(source.S),
            SW = CloneMovementRestrictionRule(source.SW),
            W = CloneMovementRestrictionRule(source.W),
            NW = CloneMovementRestrictionRule(source.NW)
        };
    }

    private static ObjectMovementRestrictionRule CloneMovementRestrictionRule(ObjectMovementRestrictionRule source)
    {
        return new ObjectMovementRestrictionRule
        {
            MaxDistance = source.MaxDistance,
            AllowJumpOver = source.AllowJumpOver
        };
    }

    private static LockOperationRequirements CloneLockOperationRequirements(LockOperationRequirements? source)
    {
        var requirements = source ?? new LockOperationRequirements();
        return new LockOperationRequirements
        {
            UnlockKeyRequirements = (requirements.UnlockKeyRequirements ?? new List<LockKeyRequirement>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity
                })
                .ToList(),
            RequireKeyForLockOperation = requirements.RequireKeyForLockOperation,
            UseUnlockKeysForLockOperation = requirements.UseUnlockKeysForLockOperation,
            LockKeyRequirements = (requirements.LockKeyRequirements ?? new List<LockKeyRequirement>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity
                })
                .ToList()
        };
    }

    private static bool AreEquivalentLockOperationRequirements(LockOperationRequirements? left, LockOperationRequirements? right)
    {
        var normalizedLeft = CloneLockOperationRequirements(left);
        var normalizedRight = CloneLockOperationRequirements(right);

        if (normalizedLeft.RequireKeyForLockOperation != normalizedRight.RequireKeyForLockOperation
            || normalizedLeft.UseUnlockKeysForLockOperation != normalizedRight.UseUnlockKeysForLockOperation)
        {
            return false;
        }

        return AreEquivalentLockKeyRequirements(normalizedLeft.UnlockKeyRequirements, normalizedRight.UnlockKeyRequirements)
            && AreEquivalentLockKeyRequirements(normalizedLeft.LockKeyRequirements, normalizedRight.LockKeyRequirements);
    }

    private static bool AreEquivalentLockKeyRequirements(IReadOnlyList<LockKeyRequirement>? left, IReadOnlyList<LockKeyRequirement>? right)
    {
        var normalizedLeft = (left ?? Array.Empty<LockKeyRequirement>())
            .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
            .OrderBy(static requirement => requirement.RequiredObjectId)
            .Select(static requirement => new
            {
                requirement.RequiredObjectId,
                RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity
            })
            .ToList();
        var normalizedRight = (right ?? Array.Empty<LockKeyRequirement>())
            .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
            .OrderBy(static requirement => requirement.RequiredObjectId)
            .Select(static requirement => new
            {
                requirement.RequiredObjectId,
                RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity
            })
            .ToList();

        if (normalizedLeft.Count != normalizedRight.Count)
        {
            return false;
        }

        for (var index = 0; index < normalizedLeft.Count; index++)
        {
            if (normalizedLeft[index].RequiredObjectId != normalizedRight[index].RequiredObjectId
                || normalizedLeft[index].RequiredQuantity != normalizedRight[index].RequiredQuantity)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalentNameSynonyms(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        var normalizedLeft = ParseObjectNameSynonyms(left);
        var normalizedRight = ParseObjectNameSynonyms(right);
        return normalizedLeft.SequenceEqual(normalizedRight, StringComparer.OrdinalIgnoreCase);
    }

    private string BuildLinkedInstanceEditNotice(GameObject gameObject)
    {
        if (!TryResolveLinkedBaseObjectScopePath(gameObject, out var basePath))
        {
            return "Linked instance: base-owned fields write through to the base object.";
        }

        return $"Linked instance: base-owned fields write through to the base object. Base item path: {basePath}";
    }

    public bool EditObjectBasicProperties(GlobalObjectNodeViewModel objectNode)
    {
        var linkedDefinition = TryResolveLinkedDefinitionObject(objectNode.GameObject);
        GameObject? ResolveLinkedDefinition(Guid id) => linkedDefinition is not null && id == linkedDefinition.ObjectId
            ? linkedDefinition
            : null;

        var currentName = EffectiveObjectFieldResolver.GetEffectiveName(objectNode.GameObject, ResolveLinkedDefinition);
        var currentNameInGame = EffectiveObjectFieldResolver.GetEffectiveNameInGame(objectNode.GameObject, ResolveLinkedDefinition);
        var currentDescription = EffectiveObjectFieldResolver.GetEffectiveDescription(objectNode.GameObject, ResolveLinkedDefinition);

        var current = CreateObjectBasicPropertiesEditRequest(
            objectNode.GameObject,
            currentName,
            currentNameInGame,
            currentDescription,
            objectNode.ParentGlobalObjectsNode.GameObjects,
            "objects",
            includeImageVariantChooserOptions: false,
            includeLockOperationRequirements: false);

        if (linkedDefinition is not null)
        {
            current = current with
            {
                LinkedEditNotice = BuildLinkedInstanceEditNotice(objectNode.GameObject),
                LinkedBaseObjectName = linkedDefinition.Name
            };
        }

        if (!_treeContextInteractionService.TryEditObjectBasicProperties(current, out var updated))
        {
            return false;
        }

        if (linkedDefinition is not null)
        {
            updated = EnforceLinkedInstanceEditableFieldPolicy(objectNode.GameObject, updated);
        }

        var scopedObjects = GetScopedObjectSiblings(objectNode);
        if (!TryValidateScopedObjectName(scopedObjects, objectNode.GameObject, updated.Name, out var normalizedName))
        {
            return false;
        }

        var updatedNameSynonyms = ParseObjectNameSynonyms(updated.ObjectNameSynonyms);
        var overridePlan = EffectiveObjectFieldResolver.BuildDefinitionOwnedOverridePlan(
            objectNode.GameObject,
            linkedDefinition,
            updated.NameInGame,
            updatedNameSynonyms,
            updated.Description);

        var hasNameInGameOverrideChange = overridePlan.HasNameInGameOverrideChange;
        var hasNameSynonymsOverrideChange = overridePlan.HasNameSynonymsOverrideChange;
        var hasDescriptionOverrideChange = overridePlan.HasDescriptionOverrideChange;

        var hasChanges = !string.Equals(objectNode.GameObject.Name, normalizedName, StringComparison.Ordinal)
                         || !string.Equals(objectNode.GameObject.NameInGame, updated.NameInGame, StringComparison.Ordinal)
                         || hasNameInGameOverrideChange
                         || !objectNode.GameObject.NameSynonyms.SequenceEqual(updatedNameSynonyms, StringComparer.OrdinalIgnoreCase)
                         || hasNameSynonymsOverrideChange
                         || !string.Equals(objectNode.GameObject.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || objectNode.GameObject.IsInventoriable != updated.IsInventoriable
                         || objectNode.GameObject.InventoryPointsDefaultValue != updated.InventoryPointsDefaultValue
                         || objectNode.GameObject.IsContainer != updated.IsContainer
                         || objectNode.GameObject.ContainerPointsDefaultValue != updated.ContainerPointsDefaultValue
                         || objectNode.GameObject.IsCapacityPointShareDividerEnabled != updated.IsCapacityPointShareDividerEnabled
                         || objectNode.GameObject.CapacityPointShareDividerDefaultValue != updated.CapacityPointShareDividerDefaultValue
                         || objectNode.GameObject.IsOpenable != updated.IsOpenable
                         || objectNode.GameObject.IsOpenDefaultValue != updated.IsOpenDefaultValue
                         || objectNode.GameObject.IsLockable != updated.IsLockable
                         || objectNode.GameObject.IsLockedDefaultValue != updated.IsLockedDefaultValue
                         || objectNode.GameObject.IsActivatable != updated.IsActivatable
                         || objectNode.GameObject.IsActiveDefaultValue != updated.IsActiveDefaultValue
                         || objectNode.GameObject.IsHidable != updated.IsHidable
                         || objectNode.GameObject.IsHiddenDefaultValue != updated.IsHiddenDefaultValue
                         || objectNode.GameObject.IsQuantifiable != updated.IsQuantifiable
                         || objectNode.GameObject.Quantity != updated.Quantity
                         || !string.Equals(objectNode.GameObject.QuantifiablePlacementDistributionMode, updated.QuantifiablePlacementDistributionMode, StringComparison.Ordinal)
                         || objectNode.GameObject.IsMovable != updated.IsMovable
                         || objectNode.GameObject.IsMovableDefaultValue != updated.IsMovableDefaultValue
                         || objectNode.GameObject.SpatialType != NormalizeSpatialType(updated.SpatialType)
                         || objectNode.GameObject.StackGroup != updated.StackGroup
                         || objectNode.GameObject.FootprintWidthCells != updated.FootprintWidthCells
                         || objectNode.GameObject.FootprintHeightCells != updated.FootprintHeightCells
                         || !string.Equals(objectNode.GameObject.FootprintOrientation, updated.FootprintOrientation, StringComparison.Ordinal)
                         || !string.Equals(objectNode.GameObject.HeadingDirection, updated.HeadingDirection, StringComparison.Ordinal)
                         || objectNode.GameObject.ObjectHeightUnits != updated.ObjectHeightUnits
                         || objectNode.GameObject.HeightInRoom != updated.HeightInRoom
                         || !Nullable.Equals(objectNode.GameObject.StackScaleStepOverride, updated.StackScaleStepOverride)
                         || !Nullable.Equals(objectNode.GameObject.MinStackScaleOverride, updated.MinStackScaleOverride)
                         || !AreEquivalentMovementRestrictions(objectNode.GameObject.MovementRestrictions, updated.MovementRestrictions)
                         || objectNode.GameObject.IsCompositeTarget != updated.IsCompositeTarget
                         || objectNode.GameObject.IsCompositeReversible != updated.IsCompositeReversible
                         || !string.Equals(objectNode.GameObject.CompositePartRequirementMode, updated.CompositePartRequirementMode, StringComparison.Ordinal)
                         || objectNode.GameObject.CompositeMinimumRequiredPartCount != updated.CompositeMinimumRequiredPartCount
                         || !AreEquivalentCompositePartRequirements(objectNode.GameObject.CompositeRequiredParts, ResolveCompositePartRequirements(updated))
                         || !string.Equals(objectNode.GameObject.Description, updated.Description, StringComparison.Ordinal)
                         || Math.Abs(objectNode.GameObject.ImageRotationDegrees - updated.ImageRotationDegrees) > 0.0001
                         || !AreEquivalentImageVariants(objectNode.GameObject.ImageVariants, updated.ImageVariants)
                         || !string.Equals(objectNode.GameObject.ImageVariantChooserScript, updated.ImageVariantChooserScript, StringComparison.Ordinal)
                         || hasDescriptionOverrideChange;

        if (!hasChanges)
        {
            return false;
        }

        objectNode.EditableName = normalizedName;
        if (linkedDefinition is not null)
        {
            EffectiveObjectFieldResolver.ApplyDefinitionOwnedOverrides(objectNode.GameObject, overridePlan);
        }
        objectNode.GameObject.NameInGame = updated.NameInGame;
        objectNode.GameObject.NameSynonyms = linkedDefinition is null
            ? updatedNameSynonyms
            : ParseObjectNameSynonyms(linkedDefinition.NameSynonyms);
        objectNode.GameObject.ProducerNotes = updated.ProducerNotes;
        objectNode.GameObject.IsInventoriable = updated.IsInventoriable;
        objectNode.GameObject.InventoryPointsDefaultValue = updated.InventoryPointsDefaultValue;
        objectNode.GameObject.IsContainer = updated.IsContainer;
        objectNode.GameObject.ContainerPointsDefaultValue = updated.ContainerPointsDefaultValue;
        objectNode.GameObject.IsCapacityPointShareDividerEnabled = updated.IsCapacityPointShareDividerEnabled;
        objectNode.GameObject.CapacityPointShareDividerDefaultValue = updated.CapacityPointShareDividerDefaultValue;
        objectNode.GameObject.IsOpenable = updated.IsOpenable;
        objectNode.GameObject.IsOpenDefaultValue = updated.IsOpenDefaultValue;
        objectNode.GameObject.IsLockable = updated.IsLockable;
        objectNode.GameObject.IsLockedDefaultValue = updated.IsLockedDefaultValue;
        objectNode.GameObject.IsActivatable = updated.IsActivatable;
        objectNode.GameObject.IsActiveDefaultValue = updated.IsActiveDefaultValue;
        objectNode.GameObject.IsHidable = updated.IsHidable;
        objectNode.GameObject.IsHiddenDefaultValue = updated.IsHiddenDefaultValue;
        objectNode.GameObject.IsQuantifiable = updated.IsQuantifiable;
        objectNode.GameObject.Quantity = updated.Quantity;
        objectNode.GameObject.QuantifiablePlacementDistributionMode = updated.QuantifiablePlacementDistributionMode;
        objectNode.GameObject.IsMovable = updated.IsMovable;
        objectNode.GameObject.IsMovableDefaultValue = updated.IsMovableDefaultValue;
        objectNode.GameObject.SpatialType = NormalizeSpatialType(updated.SpatialType);
        objectNode.GameObject.StackGroup = updated.StackGroup;
        objectNode.GameObject.FootprintWidthCells = updated.FootprintWidthCells;
        objectNode.GameObject.FootprintHeightCells = updated.FootprintHeightCells;
        objectNode.GameObject.FootprintOrientation = updated.FootprintOrientation;
        objectNode.GameObject.HeadingDirection = updated.HeadingDirection;
        objectNode.GameObject.ObjectHeightUnits = updated.ObjectHeightUnits;
        objectNode.GameObject.HeightInRoom = updated.HeightInRoom;
        objectNode.GameObject.StackScaleStepOverride = updated.StackScaleStepOverride;
        objectNode.GameObject.MinStackScaleOverride = updated.MinStackScaleOverride;
        objectNode.GameObject.MovementRestrictions = CloneMovementRestrictions(updated.MovementRestrictions);
        objectNode.GameObject.IsCompositeTarget = updated.IsCompositeTarget;
        objectNode.GameObject.IsCompositeReversible = updated.IsCompositeReversible;
        objectNode.GameObject.CompositePartRequirementMode = updated.CompositePartRequirementMode;
        objectNode.GameObject.CompositeMinimumRequiredPartCount = updated.CompositeMinimumRequiredPartCount;
        objectNode.GameObject.CompositeRequiredParts = ResolveCompositePartRequirements(updated);
        objectNode.GameObject.Description = updated.Description;
        objectNode.GameObject.ImageRotationDegrees = updated.ImageRotationDegrees;
        objectNode.GameObject.ImageVariants = NormalizeObjectImageVariantsForModel(updated.ImageVariants, updated.FullImagePath);
        objectNode.GameObject.ImageVariantChooserScript = updated.ImageVariantChooserScript?.Trim() ?? string.Empty;
        objectNode.GameObject.LockOperationRequirements = CloneLockOperationRequirements(updated.LockOperationRequirements);
        RefreshScopedNameIndex(scopedObjects, static obj => obj.Name);

        NotifyProjectEdited();
        SelectedNode = objectNode;
        ExportStatus = "Updated game object properties.";
        return true;
    }

    public bool EditObjectBasicProperties(TemplateGameObjectNodeViewModel objectNode)
    {
        var linkedDefinition = objectNode.GameObject.LinkedBaseObjectId.HasValue
            ? TryResolveLinkedDefinitionObject(objectNode.GameObject)
            : null;

        GameObject? ResolveLinkedDefinition(Guid id) => linkedDefinition is not null && id == linkedDefinition.ObjectId
            ? linkedDefinition
            : null;

        var currentName = EffectiveObjectFieldResolver.GetEffectiveName(objectNode.GameObject, ResolveLinkedDefinition);
        var currentNameInGame = EffectiveObjectFieldResolver.GetEffectiveNameInGame(objectNode.GameObject, ResolveLinkedDefinition);
        var currentDescription = EffectiveObjectFieldResolver.GetEffectiveDescription(objectNode.GameObject, ResolveLinkedDefinition);
        var imageVariantChooserVariableChoices = BuildImageVariantChooserVariableChoices(objectNode.GameObject);
        var imageVariantChooserReferenceTokens = imageVariantChooserVariableChoices
            .Select(choice => choice.Value)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static token => token, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var (stackScaleStepOverride, minStackScaleOverride) = ResolveObjectStackScaleEditorDefaults(objectNode.GameObject);

        var current = new ObjectBasicPropertiesEditRequest(
            currentName,
            currentNameInGame,
            objectNode.GameObject.ProducerNotes,
            objectNode.GameObject.IsInventoriable,
            objectNode.GameObject.InventoryPointsDefaultValue,
            objectNode.GameObject.IsContainer,
            objectNode.GameObject.ContainerPointsDefaultValue,
            objectNode.GameObject.IsCapacityPointShareDividerEnabled,
            objectNode.GameObject.CapacityPointShareDividerDefaultValue,
            objectNode.GameObject.IsOpenable,
            objectNode.GameObject.IsOpenDefaultValue,
            objectNode.GameObject.IsLockable,
            objectNode.GameObject.IsLockedDefaultValue,
            objectNode.GameObject.IsActivatable,
            objectNode.GameObject.IsActiveDefaultValue,
            objectNode.GameObject.IsHidable,
            objectNode.GameObject.IsHiddenDefaultValue,
            objectNode.GameObject.IsQuantifiable,
            objectNode.GameObject.Quantity,
            objectNode.GameObject.QuantifiablePlacementDistributionMode,
            objectNode.GameObject.IsCompositeTarget,
            objectNode.GameObject.IsCompositeReversible,
            objectNode.GameObject.CompositePartRequirementMode,
            objectNode.GameObject.CompositeMinimumRequiredPartCount,
            ExpandCompositePartObjectIds(objectNode.GameObject.CompositeRequiredParts),
            BuildAvailableCompositePartOptions(objectNode.ParentTemplatesNode.CatalogObjects, objectNode.GameObject),
            currentDescription,
            objectNode.GameObject.ResolveDefaultImagePath(),
            objectNode.GameObject.ImageRotationDegrees,
            _projectFilePath ?? string.Empty,
            "object-templates",
            ImageVariantChooserReferenceTokens: imageVariantChooserReferenceTokens,
            ImageVariantChooserVariableChoices: imageVariantChooserVariableChoices,
            ImageVariantChooserVariableScope: PropertyResolutionScope.Object,
            ImageVariants: objectNode.GameObject.ImageVariants,
            ImageVariantChooserScript: objectNode.GameObject.ImageVariantChooserScript,
            LockOperationRequirements: CloneLockOperationRequirements(objectNode.GameObject.LockOperationRequirements),
            AvailableLockKeyOptions: BuildAvailableLockKeyOptions(objectNode.GameObject),
            CompositeRequiredParts: CloneCompositePartRequirements(objectNode.GameObject.CompositeRequiredParts),
            ObjectNameSynonyms: SerializeObjectNameSynonyms(objectNode.GameObject.NameSynonyms),
            IsMovable: objectNode.GameObject.IsMovable,
            IsMovableDefaultValue: objectNode.GameObject.IsMovableDefaultValue,
            SpatialType: objectNode.GameObject.SpatialType,
            StackGroup: objectNode.GameObject.StackGroup,
            FootprintWidthCells: objectNode.GameObject.FootprintWidthCells,
            FootprintHeightCells: objectNode.GameObject.FootprintHeightCells,
            FootprintOrientation: objectNode.GameObject.FootprintOrientation,
            HeadingDirection: objectNode.GameObject.HeadingDirection,
            ObjectHeightUnits: objectNode.GameObject.ObjectHeightUnits,
            HeightInRoom: objectNode.GameObject.HeightInRoom,
            StackScaleStepOverride: stackScaleStepOverride,
            MinStackScaleOverride: minStackScaleOverride,
            MovementRestrictions: CloneMovementRestrictions(objectNode.GameObject.MovementRestrictions),
            ProjectRoomGridCellSize: _project.RoomDesignerGridCellSize,
            ProjectRoomCanvasWidth: _project.RoomImageCanvasWidth,
            ProjectRoomCanvasHeight: _project.RoomImageCanvasHeight);

        if (linkedDefinition is not null)
        {
            current = current with
            {
                LinkedEditNotice = BuildLinkedInstanceEditNotice(objectNode.GameObject),
                LinkedBaseObjectName = linkedDefinition.Name
            };
        }

        if (!_treeContextInteractionService.TryEditObjectBasicProperties(current, out var updated))
        {
            return false;
        }

        if (linkedDefinition is not null)
        {
            updated = EnforceLinkedInstanceEditableFieldPolicy(objectNode.GameObject, updated);
        }

        var scopedObjects = GetScopedObjectSiblings(objectNode);
        if (!TryValidateScopedObjectName(scopedObjects, objectNode.GameObject, updated.Name, out var normalizedName))
        {
            return false;
        }

        var updatedNameSynonyms = ParseObjectNameSynonyms(updated.ObjectNameSynonyms);
        var overridePlan = EffectiveObjectFieldResolver.BuildDefinitionOwnedOverridePlan(
            objectNode.GameObject,
            linkedDefinition,
            updated.NameInGame,
            updatedNameSynonyms,
            updated.Description);

        var hasNameInGameOverrideChange = overridePlan.HasNameInGameOverrideChange;
        var hasNameSynonymsOverrideChange = overridePlan.HasNameSynonymsOverrideChange;
        var hasDescriptionOverrideChange = overridePlan.HasDescriptionOverrideChange;

        var hasChanges = !string.Equals(objectNode.GameObject.Name, normalizedName, StringComparison.Ordinal)
                         || !string.Equals(objectNode.GameObject.NameInGame, updated.NameInGame, StringComparison.Ordinal)
                         || hasNameInGameOverrideChange
                         || !objectNode.GameObject.NameSynonyms.SequenceEqual(updatedNameSynonyms, StringComparer.OrdinalIgnoreCase)
                         || hasNameSynonymsOverrideChange
                         || !string.Equals(objectNode.GameObject.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || objectNode.GameObject.IsInventoriable != updated.IsInventoriable
                         || objectNode.GameObject.InventoryPointsDefaultValue != updated.InventoryPointsDefaultValue
                         || objectNode.GameObject.IsContainer != updated.IsContainer
                         || objectNode.GameObject.ContainerPointsDefaultValue != updated.ContainerPointsDefaultValue
                         || objectNode.GameObject.IsCapacityPointShareDividerEnabled != updated.IsCapacityPointShareDividerEnabled
                         || objectNode.GameObject.CapacityPointShareDividerDefaultValue != updated.CapacityPointShareDividerDefaultValue
                         || objectNode.GameObject.IsOpenable != updated.IsOpenable
                         || objectNode.GameObject.IsOpenDefaultValue != updated.IsOpenDefaultValue
                         || objectNode.GameObject.IsLockable != updated.IsLockable
                         || objectNode.GameObject.IsLockedDefaultValue != updated.IsLockedDefaultValue
                         || objectNode.GameObject.IsActivatable != updated.IsActivatable
                         || objectNode.GameObject.IsActiveDefaultValue != updated.IsActiveDefaultValue
                         || objectNode.GameObject.IsHidable != updated.IsHidable
                         || objectNode.GameObject.IsHiddenDefaultValue != updated.IsHiddenDefaultValue
                         || objectNode.GameObject.IsQuantifiable != updated.IsQuantifiable
                         || objectNode.GameObject.Quantity != updated.Quantity
                         || !string.Equals(objectNode.GameObject.QuantifiablePlacementDistributionMode, updated.QuantifiablePlacementDistributionMode, StringComparison.Ordinal)
                         || objectNode.GameObject.IsMovable != updated.IsMovable
                         || objectNode.GameObject.IsMovableDefaultValue != updated.IsMovableDefaultValue
                         || objectNode.GameObject.SpatialType != NormalizeSpatialType(updated.SpatialType)
                         || objectNode.GameObject.StackGroup != updated.StackGroup
                         || objectNode.GameObject.FootprintWidthCells != updated.FootprintWidthCells
                         || objectNode.GameObject.FootprintHeightCells != updated.FootprintHeightCells
                         || !string.Equals(objectNode.GameObject.FootprintOrientation, updated.FootprintOrientation, StringComparison.Ordinal)
                         || !string.Equals(objectNode.GameObject.HeadingDirection, updated.HeadingDirection, StringComparison.Ordinal)
                         || objectNode.GameObject.ObjectHeightUnits != updated.ObjectHeightUnits
                         || objectNode.GameObject.HeightInRoom != updated.HeightInRoom
                         || !Nullable.Equals(objectNode.GameObject.StackScaleStepOverride, updated.StackScaleStepOverride)
                         || !Nullable.Equals(objectNode.GameObject.MinStackScaleOverride, updated.MinStackScaleOverride)
                         || !AreEquivalentMovementRestrictions(objectNode.GameObject.MovementRestrictions, updated.MovementRestrictions)
                         || objectNode.GameObject.IsCompositeTarget != updated.IsCompositeTarget
                         || objectNode.GameObject.IsCompositeReversible != updated.IsCompositeReversible
                         || !string.Equals(objectNode.GameObject.CompositePartRequirementMode, updated.CompositePartRequirementMode, StringComparison.Ordinal)
                         || objectNode.GameObject.CompositeMinimumRequiredPartCount != updated.CompositeMinimumRequiredPartCount
                         || !AreEquivalentCompositePartRequirements(objectNode.GameObject.CompositeRequiredParts, ResolveCompositePartRequirements(updated))
                         || !string.Equals(objectNode.GameObject.Description, updated.Description, StringComparison.Ordinal)
                         || Math.Abs(objectNode.GameObject.ImageRotationDegrees - updated.ImageRotationDegrees) > 0.0001
                         || !AreEquivalentImageVariants(objectNode.GameObject.ImageVariants, updated.ImageVariants)
                         || !string.Equals(objectNode.GameObject.ImageVariantChooserScript, updated.ImageVariantChooserScript, StringComparison.Ordinal)
                         || !AreEquivalentLockOperationRequirements(objectNode.GameObject.LockOperationRequirements, updated.LockOperationRequirements)
                         || hasDescriptionOverrideChange;

        if (!hasChanges)
        {
            return false;
        }

        objectNode.EditableName = normalizedName;
        if (linkedDefinition is not null)
        {
            EffectiveObjectFieldResolver.ApplyDefinitionOwnedOverrides(objectNode.GameObject, overridePlan);
        }
        objectNode.GameObject.NameInGame = updated.NameInGame;
        objectNode.GameObject.NameSynonyms = linkedDefinition is null
            ? updatedNameSynonyms
            : ParseObjectNameSynonyms(linkedDefinition.NameSynonyms);
        objectNode.GameObject.ProducerNotes = updated.ProducerNotes;
        objectNode.GameObject.IsInventoriable = updated.IsInventoriable;
        objectNode.GameObject.InventoryPointsDefaultValue = updated.InventoryPointsDefaultValue;
        objectNode.GameObject.IsContainer = updated.IsContainer;
        objectNode.GameObject.ContainerPointsDefaultValue = updated.ContainerPointsDefaultValue;
        objectNode.GameObject.IsCapacityPointShareDividerEnabled = updated.IsCapacityPointShareDividerEnabled;
        objectNode.GameObject.CapacityPointShareDividerDefaultValue = updated.CapacityPointShareDividerDefaultValue;
        objectNode.GameObject.IsOpenable = updated.IsOpenable;
        objectNode.GameObject.IsOpenDefaultValue = updated.IsOpenDefaultValue;
        objectNode.GameObject.IsLockable = updated.IsLockable;
        objectNode.GameObject.IsLockedDefaultValue = updated.IsLockedDefaultValue;
        objectNode.GameObject.IsActivatable = updated.IsActivatable;
        objectNode.GameObject.IsActiveDefaultValue = updated.IsActiveDefaultValue;
        objectNode.GameObject.IsHidable = updated.IsHidable;
        objectNode.GameObject.IsHiddenDefaultValue = updated.IsHiddenDefaultValue;
        objectNode.GameObject.IsQuantifiable = updated.IsQuantifiable;
        objectNode.GameObject.Quantity = updated.Quantity;
        objectNode.GameObject.QuantifiablePlacementDistributionMode = updated.QuantifiablePlacementDistributionMode;
        objectNode.GameObject.IsMovable = updated.IsMovable;
        objectNode.GameObject.IsMovableDefaultValue = updated.IsMovableDefaultValue;
        objectNode.GameObject.SpatialType = NormalizeSpatialType(updated.SpatialType);
        objectNode.GameObject.StackGroup = updated.StackGroup;
        objectNode.GameObject.FootprintWidthCells = updated.FootprintWidthCells;
        objectNode.GameObject.FootprintHeightCells = updated.FootprintHeightCells;
        objectNode.GameObject.FootprintOrientation = updated.FootprintOrientation;
        objectNode.GameObject.HeadingDirection = updated.HeadingDirection;
        objectNode.GameObject.ObjectHeightUnits = updated.ObjectHeightUnits;
        objectNode.GameObject.HeightInRoom = updated.HeightInRoom;
        objectNode.GameObject.StackScaleStepOverride = updated.StackScaleStepOverride;
        objectNode.GameObject.MinStackScaleOverride = updated.MinStackScaleOverride;
        objectNode.GameObject.MovementRestrictions = CloneMovementRestrictions(updated.MovementRestrictions);
        objectNode.GameObject.IsCompositeTarget = updated.IsCompositeTarget;
        objectNode.GameObject.IsCompositeReversible = updated.IsCompositeReversible;
        objectNode.GameObject.CompositePartRequirementMode = updated.CompositePartRequirementMode;
        objectNode.GameObject.CompositeMinimumRequiredPartCount = updated.CompositeMinimumRequiredPartCount;
        objectNode.GameObject.CompositeRequiredParts = ResolveCompositePartRequirements(updated);
        objectNode.GameObject.Description = updated.Description;
        objectNode.GameObject.ImageRotationDegrees = updated.ImageRotationDegrees;
        objectNode.GameObject.ImageVariants = NormalizeObjectImageVariantsForModel(updated.ImageVariants, updated.FullImagePath);
        objectNode.GameObject.ImageVariantChooserScript = updated.ImageVariantChooserScript?.Trim() ?? string.Empty;
        objectNode.GameObject.LockOperationRequirements = CloneLockOperationRequirements(updated.LockOperationRequirements);
        RefreshScopedNameIndex(scopedObjects, static obj => obj.Name);

        NotifyProjectEdited();
        SelectedNode = objectNode;
        ExportStatus = objectNode.ParentTemplatesNode.IsBaseCatalog
            ? "Updated base object properties."
            : "Updated object template properties.";
        return true;
    }

    public bool EditAreaBasicProperties(AreaNodeViewModel areaNode)
    {
        var current = new AreaBasicPropertiesEditRequest(
            areaNode.Area.Name,
            areaNode.Area.ProducerNotes,
            areaNode.Area.AdjacencyMode,
            areaNode.Area.RoomDropBehavior,
            areaNode.Area.StartingRoomId);

        var startingRoomOptions = areaNode.Area.Rooms
            .Select(room => new AreaStartingRoomOption(room.Id, room.Name))
            .ToList();

        if (!_treeContextInteractionService.TryEditAreaBasicProperties(current, startingRoomOptions, out var updated))
        {
            return false;
        }

        if (areaNode.Parent is not CountryNodeViewModel countryNode)
        {
            return false;
        }

        var uniqueName = MakeUniqueScopedEntityName(countryNode.Country.Areas, static area => area.Name, updated.Name, areaNode.Area);
        var validStartingRoomId = updated.StartingRoomId.HasValue && areaNode.Area.Rooms.Any(room => room.Id == updated.StartingRoomId.Value)
            ? updated.StartingRoomId
            : null;

        var hasChanges = !string.Equals(areaNode.Area.Name, uniqueName, StringComparison.Ordinal)
                         || !string.Equals(areaNode.Area.ProducerNotes, updated.ProducerNotes, StringComparison.Ordinal)
                         || areaNode.Area.AdjacencyMode != updated.AdjacencyMode
                         || areaNode.Area.RoomDropBehavior != updated.RoomDropBehavior
                         || areaNode.Area.StartingRoomId != validStartingRoomId;

        if (!hasChanges)
        {
            return false;
        }

        areaNode.EditableName = uniqueName;
        areaNode.Area.ProducerNotes = updated.ProducerNotes;
        areaNode.Area.AdjacencyMode = updated.AdjacencyMode;
        areaNode.Area.RoomDropBehavior = updated.RoomDropBehavior;
        areaNode.Area.StartingRoomId = validStartingRoomId;
        RefreshScopedNameIndex(countryNode.Country.Areas, static area => area.Name);

        NotifyProjectEdited();
        SelectedNode = areaNode;
        ExportStatus = "Updated area settings.";
        return true;
    }

    private IReadOnlyList<GameObjectSelectionOption> BuildAvailableCompositePartOptions(IEnumerable<GameObject> sourceObjects, GameObject currentObject)
    {
        var scopeAnchor = sourceObjects.FirstOrDefault();
        if (scopeAnchor is null)
        {
            return Array.Empty<GameObjectSelectionOption>();
        }

        var options = _gameObjectSelectionOptionDiscoveryService.Discover(new GameObjectSelectionOptionDiscoveryRequest(
            CurrentObject: scopeAnchor,
            ScopeSearchDepth: ScopeSearchDepth.LocalRoom,
            ScopeSearchType: ResolveCompositePartScopeSearchType(scopeAnchor),
            ExcludeCurrentObject: false));

        return options
            .Where(option => option.Id != currentObject.ObjectId)
            .ToList();
    }

    private IReadOnlyList<GameObjectSelectionOption> BuildAvailableLockKeyOptions(GameObject currentObject)
    {
        return _gameObjectSelectionOptionDiscoveryService.Discover(new GameObjectSelectionOptionDiscoveryRequest(
            CurrentObject: currentObject,
            RequiredFeatures: GameObjectFeatureRequirements.Inventoriable,
            ScopeSearchDepth: ScopeSearchDepth.Project,
            ScopeSearchType: GameObjectOptionSourceTarget.RealObjects,
            ExcludeCurrentObject: true));
    }

    private static GameObjectOptionSourceTarget ResolveCompositePartScopeSearchType(GameObject scopeAnchor)
    {
        var current = scopeAnchor as IScopedAwareNode;
        while (current is not null)
        {
            var parent = current.ParentScope;
            if (parent is null)
            {
                return GameObjectOptionSourceTarget.RealObjects;
            }

            if (parent is ObjectTemplatesScopeNode or BaseObjectsScopeNode or RoomTemplatesScopeNode)
            {
                return GameObjectOptionSourceTarget.ObjectTemplates;
            }

            if (parent is Planet planet && ReferenceEquals(current, scopeAnchor) && planet.BaseObjects.Contains(scopeAnchor))
            {
                return GameObjectOptionSourceTarget.ObjectTemplates;
            }

            if (parent is Country country && ReferenceEquals(current, scopeAnchor) && country.BaseObjects.Contains(scopeAnchor))
            {
                return GameObjectOptionSourceTarget.ObjectTemplates;
            }

            if (parent is Area area && ReferenceEquals(current, scopeAnchor) && area.BaseObjects.Contains(scopeAnchor))
            {
                return GameObjectOptionSourceTarget.ObjectTemplates;
            }

            current = parent;
        }

        return GameObjectOptionSourceTarget.RealObjects;
    }

    private bool AddNewRoom(AreaNodeViewModel areaNode)
    {
        if (!TryCreateNewRoomFromTemplate(out var room))
        {
            return false;
        }

        var defaultRoomName = MakeUniqueScopedEntityName(areaNode.Area.Rooms, static templateRoom => templateRoom.Name, "New Room");
        if (!TryConfigureNewRoomBeforeInsert(areaNode, defaultRoomName, room.NameInGame, out var roomSettings))
        {
            return false;
        }

        room.Name = roomSettings.Name;
        room.NameInGame = roomSettings.NameInGame;
        room.ProducerNotes = roomSettings.ProducerNotes;
        room.RoomImageCanvasWidth = roomSettings.RoomCanvasWidth;
        room.RoomImageCanvasHeight = roomSettings.RoomCanvasHeight;

        if (!areaNode.Area.AddChildScope(room))
        {
            return false;
        }
        if (areaNode.Area.StartingRoomId is null)
        {
            areaNode.Area.StartingRoomId = room.Id;
        }

        var roomNode = new RoomNodeViewModel(room, areaNode.Area, areaNode);
        roomNode.Children.Add(CreateRoomSettingsNode(roomNode));
        roomNode.Children.Add(CreateVariablesNode(roomNode, room.Variables, PropertyResolutionScope.Room));
        roomNode.Children.Add(CreateActionsNode(roomNode, PropertyResolutionScope.Room, room.AvailableActions));
        roomNode.Children.Add(CreateVerbsNode(roomNode, PropertyResolutionScope.Room, room.AdditionalVerbs));
        roomNode.Children.Add(CreateDirectionalsNode(roomNode, PropertyResolutionScope.Room, room.AdditionalDirectionals));
        roomNode.Children.Add(CreateSoundEffectsNode(roomNode, PropertyResolutionScope.Room, room.SoundEffectLibraryEntries));
        roomNode.Children.Add(CreateEventSubscriptionsNode(roomNode, PropertyResolutionScope.Room, room.EventSubscriptions));
        roomNode.Children.Add(CreateTimerDefinitionsNode(roomNode, PropertyResolutionScope.Room, room.TimerDefinitions));
        roomNode.Children.Add(new RoomTraversalLegsNodeViewModel(roomNode));
        roomNode.Children.Add(CreateObjectsNode(roomNode));

        var areaChildrenGroup = areaNode.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault();
        if (areaChildrenGroup is null)
        {
            areaChildrenGroup = new ChildrenGroupNodeViewModel(areaNode);
            areaNode.Children.Add(areaChildrenGroup);
        }

        areaChildrenGroup.Children.Add(roomNode);
        areaNode.IsExpanded = true;
        RefreshScopedNameIndex(areaNode.Area.Rooms, static room => room.Name);
        NotifyProjectEdited();
        SelectedNode = roomNode;
        OpenRoomEditor(room);
        ExportStatus = $"Added room '{room.Name}'.";
        return true;
    }

    private bool DeleteRoom(RoomNodeViewModel roomNode)
    {
        if (roomNode.Parent is not AreaNodeViewModel areaNode)
        {
            return false;
        }

        var room = roomNode.Room;
        var roomName = room.Name;
        var deletedConnectionIds = areaNode.Area.TraversalConnections
            .Where(connection => connection.RoomAId == room.Id || connection.RoomBId == room.Id)
            .Select(connection => connection.TraversalConnectionId)
            .Distinct()
            .ToList();

        foreach (var areaEditor in OpenAreaEditors.Where(editor => ReferenceEquals(editor.Area, areaNode.Area)).ToList())
        {
            areaEditor.RemoveRoomFromMap(room);
        }

        // Ensure room map artifacts are removed even when no area editor is open.
        areaNode.Area.RoomPlacements.RemoveAll(placement => placement.RoomId == room.Id);
        areaNode.Area.TraversalConnections.RemoveAll(connection => connection.RoomAId == room.Id || connection.RoomBId == room.Id);
        RemoveTraversalSharedParticipantsByConnectionIds(deletedConnectionIds);

        areaNode.Area.Rooms.RemoveAll(candidate => candidate.Id == room.Id);
        if (areaNode.Area.StartingRoomId == room.Id)
        {
            areaNode.Area.StartingRoomId = areaNode.Area.Rooms.FirstOrDefault()?.Id;
        }

        var areaChildrenGroup = areaNode.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault();
        var roomNodeInTree = areaChildrenGroup?.Children
            .OfType<RoomNodeViewModel>()
            .FirstOrDefault(candidate => candidate.Room.Id == room.Id);
        if (roomNodeInTree is not null)
        {
            areaChildrenGroup!.Children.Remove(roomNodeInTree);
        }

        foreach (var roomEditor in OpenRoomEditors.Where(editor => editor.Room.Id == room.Id).ToList())
        {
            OpenRoomEditors.Remove(roomEditor);
        }

        if (SelectedRoomEditor?.Room.Id == room.Id)
        {
            SelectedRoomEditor = OpenRoomEditors.LastOrDefault();
        }

        if (SelectedRoom?.Id == room.Id)
        {
            SelectedRoom = areaNode.Area.Rooms.FirstOrDefault();
        }

        if (_project.UiState.RoomId == room.Id)
        {
            _project.UiState.RoomId = SelectedRoom?.Id;
        }

        for (HierarchyNodeViewModel? current = SelectedNode; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, roomNode))
            {
                SelectedNode = areaNode;
                break;
            }
        }

        RefreshScopedNameIndex(areaNode.Area.Rooms, static candidate => candidate.Name);
        RefreshTraversalLegNodesForArea(areaNode.Area);
        areaNode.IsExpanded = true;

        NotifyProjectEdited();
        ExportStatus = $"Deleted room '{roomName}'.";
        return true;
    }

    private bool DeleteRoomTemplate(TemplateRoomNodeViewModel templateRoomNode)
    {
        if (templateRoomNode.Parent is not RoomTemplatesNodeViewModel templatesNode)
        {
            return false;
        }

        var templateRoom = templateRoomNode.Room;
        var templateRoomName = templateRoom.Name;

        templatesNode.RoomTemplates.RemoveAll(candidate => candidate.Id == templateRoom.Id);
        templatesNode.Children.Remove(templateRoomNode);

        foreach (var roomEditor in OpenRoomEditors.Where(editor => editor.Room.Id == templateRoom.Id).ToList())
        {
            OpenRoomEditors.Remove(roomEditor);
        }

        if (SelectedRoomEditor?.Room.Id == templateRoom.Id)
        {
            SelectedRoomEditor = OpenRoomEditors.LastOrDefault();
        }

        if (SelectedRoom?.Id == templateRoom.Id)
        {
            SelectedRoom = null;
        }

        if (_project.UiState.RoomId == templateRoom.Id)
        {
            _project.UiState.RoomId = null;
        }

        for (HierarchyNodeViewModel? current = SelectedNode; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, templateRoomNode))
            {
                SelectedNode = templatesNode;
                break;
            }
        }

        templatesNode.IsExpanded = true;
        RefreshScopedNameIndex(templatesNode.RoomTemplates, static room => room.Name);

        NotifyProjectEdited();
        ExportStatus = $"Deleted room template '{templateRoomName}'.";
        return true;
    }

    private bool TryCreateNewRoomFromTemplate(out Room room)
    {
        room = null!;
        Room? selectedTemplate = null;

        if (_project.RoomTemplates.Count > 0)
        {
            if (!_treeContextInteractionService.TrySelectRoomTemplate(_project.RoomTemplates, out selectedTemplate))
            {
                return false;
            }
        }

        if (selectedTemplate is not null)
        {
            room = CloneTemplateRoom(selectedTemplate);
            ResetHideEmptyConfigurationForNewRoom(room);
            var normalizationWarnings = NormalizeSharedVariableLinksToRoomBoundary(selectedTemplate, room, "Add New Room");
            if (normalizationWarnings.Count > 0)
            {
                var message = string.Join(Environment.NewLine, normalizationWarnings);
                _projectUiService.ShowWarning(message, "Add New Room");
                AppendOutputConsoleLine($"RoomTemplateBootstrap.SharedVariableNormalization: {message}");
            }

            return true;
        }

        room = new Room
        {
            Description = string.Empty,
            Commands = new List<string>(),
            GameObjects = new List<GameObject>(),
            Variables = new List<GamePropertyDefinition>(),
            RoomImageCanvasWidth = _project.RoomImageCanvasWidth,
            RoomImageCanvasHeight = _project.RoomImageCanvasHeight,
            Images = CreateDefaultRoomImages()
        };
        return true;
    }

    private bool SetStartingRoom(RoomNodeViewModel roomNode)
    {
        if (roomNode.Parent is not AreaNodeViewModel areaNode
            || areaNode.Parent is not CountryNodeViewModel countryNode
            || countryNode.Parent is not PlanetNodeViewModel planetNode)
        {
            return false;
        }

        areaNode.Area.StartingRoomId = roomNode.Room.Id;
        countryNode.Country.StartingAreaName = areaNode.Area.Name;
        planetNode.Planet.StartingCountryName = countryNode.Country.Name;
        _project.StartingPlanetName = planetNode.Planet.Name;

        NotifyProjectEdited();
        SelectedNode = roomNode;
        ExportStatus = $"Starting room set to '{roomNode.Room.Name}' with hierarchy updated.";
        return true;
    }

    private bool AddNewObject(RoomNodeViewModel roomNode)
    {
        if (!TryAddTopLevelObjectCore(
                roomNode,
                static node => node.Room.GameObjects,
                static node => node.Room,
                static node => node,
                static (instance, node) =>
                {
                    var objectsNode = GetOrCreateRoomObjectsNode(node);
                    var addedNode = CreateScopedObjectNode(instance, objectsNode);
                    objectsNode.Children.Insert(0, addedNode);
                    return addedNode;
                },
                static node => GetOrCreateRoomObjectsNode(node).IsExpanded = true,
                () => RefreshOpenRoomEditorObjectLists(roomNode.Room),
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual quantifiable objects from '{objectName}'."
            : $"Added game object '{objectName}'.";
        return true;
    }

    private bool AddNewObject(PlanetNodeViewModel planetNode)
    {
        if (!TryAddTopLevelObjectCore(
                planetNode,
                static node => node.Planet.GameObjects,
                static node => node.Planet,
                static node => node,
                static (instance, node) =>
                {
                    var objectsNode = GetOrCreatePlanetObjectsNode(node);
                    var addedNode = CreateScopedObjectNode(instance, objectsNode);
                    objectsNode.Children.Add(addedNode);
                    return addedNode;
                },
                static node => GetOrCreatePlanetObjectsNode(node).IsExpanded = true,
                postAddAction: null,
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual quantifiable objects from '{objectName}'."
            : $"Added game object '{objectName}' at planet scope.";
        return true;
    }

    private bool AddNewObject(CountryNodeViewModel countryNode)
    {
        if (!TryAddTopLevelObjectCore(
                countryNode,
                static node => node.Country.GameObjects,
                static node => node.Country,
                static node => node,
                static (instance, node) =>
                {
                    var objectsNode = GetOrCreateCountryObjectsNode(node);
                    var addedNode = CreateScopedObjectNode(instance, objectsNode);
                    objectsNode.Children.Add(addedNode);
                    return addedNode;
                },
                static node => GetOrCreateCountryObjectsNode(node).IsExpanded = true,
                postAddAction: null,
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual quantifiable objects from '{objectName}'."
            : $"Added game object '{objectName}' at country scope.";
        return true;
    }

    private bool AddNewObject(AreaNodeViewModel areaNode)
    {
        if (!TryAddTopLevelObjectCore(
                areaNode,
                static node => node.Area.GameObjects,
                static node => node.Area,
                static node => node,
                static (instance, node) =>
                {
                    var objectsNode = GetOrCreateAreaObjectsNode(node);
                    var addedNode = CreateScopedObjectNode(instance, objectsNode);
                    objectsNode.Children.Add(addedNode);
                    return addedNode;
                },
                static node => GetOrCreateAreaObjectsNode(node).IsExpanded = true,
                postAddAction: null,
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual quantifiable objects from '{objectName}'."
            : $"Added game object '{objectName}' at area scope.";
        return true;
    }

    private bool AddNewObject(TemplateRoomNodeViewModel templateRoomNode)
    {
        if (!TryAddTopLevelObjectCore(
                templateRoomNode,
                static node => node.Room.GameObjects,
                static node => node.Room,
                static node => node,
                static (instance, node) =>
                {
                    var objectsNode = GetOrCreateTemplateRoomObjectsNode(node);
                    var addedNode = CreateTemplateObjectNode(instance, objectsNode);
                    objectsNode.Children.Insert(0, addedNode);
                    return addedNode;
                },
                static node => GetOrCreateTemplateRoomObjectsNode(node).IsExpanded = true,
                postAddAction: () => ApplyDesignerRoomTemplateScope(templateRoomNode.Room),
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual quantifiable objects to template room '{templateRoomNode.Room.Name}'."
            : $"Added game object '{objectName}' to template room '{templateRoomNode.Room.Name}'.";
        return true;
    }

    private TemplateRoomNodeViewModel? FindTemplateRoomNode(Room room)
    {
        return EnumerateHierarchyNodes(HierarchyRoots)
            .OfType<TemplateRoomNodeViewModel>()
            .FirstOrDefault(node => ReferenceEquals(node.Room, room));
    }

    private bool AddExistingQuantifiableObject(RoomNodeViewModel roomNode)
    {
        var candidates = GetQuantifiableBaseObjectCandidates(roomNode)
            .ToList();

        if (candidates.Count == 0)
        {
            ExportStatus = "No quantifiable base object definitions are available yet.";
            return false;
        }

        if (!_treeContextInteractionService.TrySelectQuantifiableObjectPlacement(candidates, out var selection)
            || selection is null)
        {
            return false;
        }

        var mode = string.Equals(selection.SourceObject.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)
            ? "IndividualInstances"
            : "GroupedStack";

        var addedNodes = new List<GameObjectNodeViewModel>();
        if (mode == "IndividualInstances")
        {
            for (var index = 0; index < selection.Quantity; index++)
            {
                if (TryAddQuantifiableClone(roomNode, selection.SourceObject, 1, mode, out var addedNode))
                {
                    addedNodes.Add(addedNode);
                }
            }
        }
        else
        {
            if (TryAddQuantifiableClone(roomNode, selection.SourceObject, selection.Quantity, mode, out var addedNode))
            {
                addedNodes.Add(addedNode);
            }
        }

        if (addedNodes.Count == 0)
        {
            return false;
        }

        roomNode.IsExpanded = true;
        var objectsNode = EnumerateHierarchyNodes(roomNode.Children)
            .OfType<RoomGameObjectsNodeViewModel>()
            .FirstOrDefault();
        if (objectsNode is not null)
        {
            objectsNode.IsExpanded = true;
        }

        RefreshOpenRoomEditorObjectLists(roomNode.Room);

        NotifyProjectEdited();
        SelectedNode = addedNodes[0];
        ExportStatus = mode == "IndividualInstances"
            ? $"Added {addedNodes.Count} individual quantifiable objects from base object '{selection.SourceObject.Name}'."
            : $"Added quantifiable object '{addedNodes[0].GameObject.Name}' with quantity {selection.Quantity}.";
        return true;
    }

    private bool AddExistingQuantifiableObject(TemplateRoomNodeViewModel templateRoomNode)
    {
        var candidates = GetQuantifiableBaseObjectCandidates(templateRoomNode)
            .ToList();

        if (candidates.Count == 0)
        {
            ExportStatus = "No quantifiable base object definitions are available yet.";
            return false;
        }

        if (!_treeContextInteractionService.TrySelectQuantifiableObjectPlacement(candidates, out var selection)
            || selection is null)
        {
            return false;
        }

        var mode = string.Equals(selection.SourceObject.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)
            ? "IndividualInstances"
            : "GroupedStack";

        var addedNodes = new List<TemplateGameObjectNodeViewModel>();
        if (mode == "IndividualInstances")
        {
            for (var index = 0; index < selection.Quantity; index++)
            {
                if (TryAddQuantifiableClone(templateRoomNode, selection.SourceObject, 1, mode, out var addedNode))
                {
                    addedNodes.Add(addedNode);
                }
            }
        }
        else
        {
            if (TryAddQuantifiableClone(templateRoomNode, selection.SourceObject, selection.Quantity, mode, out var addedNode))
            {
                addedNodes.Add(addedNode);
            }
        }

        if (addedNodes.Count == 0)
        {
            return false;
        }

        templateRoomNode.IsExpanded = true;
        var objectsNode = GetOrCreateTemplateRoomObjectsNode(templateRoomNode);
        objectsNode.IsExpanded = true;

        NotifyProjectEdited();
        SelectedNode = addedNodes[0];
        ExportStatus = mode == "IndividualInstances"
            ? $"Added {addedNodes.Count} individual quantifiable objects from base object '{selection.SourceObject.Name}' to template room '{templateRoomNode.Room.Name}'."
            : $"Added quantifiable object '{addedNodes[0].GameObject.Name}' with quantity {selection.Quantity} to template room '{templateRoomNode.Room.Name}'.";
        return true;
    }

    private IReadOnlyList<QuantifiableObjectPlacementCandidate> GetQuantifiableBaseObjectCandidates(RoomNodeViewModel roomNode)
    {
        var candidates = new List<QuantifiableObjectPlacementCandidate>();
        var seen = new HashSet<Guid>();

        void AddCandidates(IEnumerable<GameObject> source, string scopePath, int scopePriority)
        {
            foreach (var obj in EnumerateGameObjectsRecursive(source))
            {
                if (!obj.IsQuantifiable || obj.IsRoomInstance)
                {
                    continue;
                }

                if (obj.ObjectId != Guid.Empty && !seen.Add(obj.ObjectId))
                {
                    continue;
                }

                candidates.Add(new QuantifiableObjectPlacementCandidate(obj, scopePath, scopePriority));
            }
        }

        var hierarchy = ResolveRoomHierarchy(roomNode.Room);
        if (hierarchy is not null)
        {
            AddCandidates(hierarchy.Value.area.BaseObjects, $"Area: {hierarchy.Value.area.Name}", 0);
            AddCandidates(hierarchy.Value.country.BaseObjects, $"Country: {hierarchy.Value.country.Name}", 1);
            AddCandidates(hierarchy.Value.planet.BaseObjects, $"Planet: {hierarchy.Value.planet.Name}", 2);
        }

        AddCandidates(_project.BaseObjects, "Global", 3);

        return candidates
            .OrderBy(candidate => candidate.ScopePriority)
            .ThenBy(candidate => candidate.SourceObject.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<QuantifiableObjectPlacementCandidate> GetQuantifiableBaseObjectCandidates(TemplateRoomNodeViewModel templateRoomNode)
    {
        var candidates = new List<QuantifiableObjectPlacementCandidate>();
        var seen = new HashSet<Guid>();

        foreach (var obj in EnumerateGameObjectsRecursive(_project.BaseObjects))
        {
            if (!obj.IsQuantifiable || obj.IsRoomInstance)
            {
                continue;
            }

            if (obj.ObjectId != Guid.Empty && !seen.Add(obj.ObjectId))
            {
                continue;
            }

            candidates.Add(new QuantifiableObjectPlacementCandidate(obj, "Global", 0));
        }

        return candidates
            .OrderBy(candidate => candidate.SourceObject.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryAddQuantifiableClone(
        RoomNodeViewModel roomNode,
        GameObject sourceObject,
        int quantity,
        string distributionMode,
        out GameObjectNodeViewModel addedNode)
    {
        addedNode = null!;

        var cloned = CloneTemplateObject(sourceObject);
        ResetHideEmptyConfigurationForNewObject(cloned);
        cloned.IsQuantifiable = true;
        cloned.Quantity = quantity;
        cloned.QuantifiablePlacementDistributionMode = distributionMode;
        var linkedBaseObjectId = sourceObject.LinkedBaseObjectId ?? sourceObject.ObjectId;
        ConfigureAsLinkedRoomInstance(cloned, linkedBaseObjectId);
        cloned.Name = MakeUniqueScopedEntityName(roomNode.Room.GameObjects, static obj => obj.Name, sourceObject.Name, cloned);

        if (!roomNode.Room.AddChildScope(cloned))
        {
            return false;
        }

        MoveGameObjectToTop(roomNode.Room.GameObjects, cloned);

        var objectsNode = GetOrCreateRoomObjectsNode(roomNode);

        addedNode = CreateScopedObjectNode(cloned, objectsNode);
        objectsNode.Children.Insert(0, addedNode);
        RefreshScopedNameIndex(roomNode.Room.GameObjects, static obj => obj.Name);
        return true;
    }

    private bool TryAddQuantifiableClone(
        TemplateRoomNodeViewModel templateRoomNode,
        GameObject sourceObject,
        int quantity,
        string distributionMode,
        out TemplateGameObjectNodeViewModel addedNode)
    {
        addedNode = null!;

        var cloned = CloneTemplateObject(sourceObject);
        ResetHideEmptyConfigurationForNewObject(cloned);
        cloned.IsQuantifiable = true;
        cloned.Quantity = quantity;
        cloned.QuantifiablePlacementDistributionMode = distributionMode;
        var linkedBaseObjectId = sourceObject.LinkedBaseObjectId ?? sourceObject.ObjectId;
        ConfigureAsLinkedRoomInstance(cloned, linkedBaseObjectId);
        cloned.Name = MakeUniqueScopedEntityName(templateRoomNode.Room.GameObjects, static obj => obj.Name, sourceObject.Name, cloned);
        ApplyDesignerTemplateScope(cloned);

        if (!templateRoomNode.Room.AddChildScope(cloned))
        {
            return false;
        }

        MoveGameObjectToTop(templateRoomNode.Room.GameObjects, cloned);

        var objectsNode = GetOrCreateTemplateRoomObjectsNode(templateRoomNode);

        addedNode = CreateTemplateObjectNode(cloned, objectsNode);
        objectsNode.Children.Insert(0, addedNode);
        RefreshScopedNameIndex(templateRoomNode.Room.GameObjects, static obj => obj.Name);
        return true;
    }

    private static RoomGameObjectsNodeViewModel GetOrCreateRoomObjectsNode(RoomNodeViewModel roomNode)
    {
        var existing = roomNode.Children.OfType<RoomGameObjectsNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = CreateObjectsNode(roomNode);
        roomNode.Children.Add(created);
        return created;
    }

    private static PlanetGameObjectsNodeViewModel GetOrCreatePlanetObjectsNode(PlanetNodeViewModel planetNode)
    {
        var existing = planetNode.Children.OfType<PlanetGameObjectsNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = CreateObjectsNode(planetNode);
        planetNode.Children.Add(created);
        return created;
    }

    private static CountryGameObjectsNodeViewModel GetOrCreateCountryObjectsNode(CountryNodeViewModel countryNode)
    {
        var existing = countryNode.Children.OfType<CountryGameObjectsNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = CreateObjectsNode(countryNode);
        countryNode.Children.Add(created);
        return created;
    }

    private static AreaGameObjectsNodeViewModel GetOrCreateAreaObjectsNode(AreaNodeViewModel areaNode)
    {
        var existing = areaNode.Children.OfType<AreaGameObjectsNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = CreateObjectsNode(areaNode);
        areaNode.Children.Add(created);
        return created;
    }

    private static ObjectTemplatesNodeViewModel GetOrCreateTemplateRoomObjectsNode(TemplateRoomNodeViewModel templateRoomNode)
    {
        var existing = templateRoomNode.Children
            .OfType<ObjectTemplatesNodeViewModel>()
            .FirstOrDefault(node => node.Parent is TemplateRoomNodeViewModel && ReferenceEquals(node.CatalogObjects, templateRoomNode.Room.GameObjects));
        if (existing is not null)
        {
            return existing;
        }

        var created = CreateTemplateRoomObjectsNode(templateRoomNode);
        templateRoomNode.Children.Add(created);
        return created;
    }

    private static ChildrenGroupNodeViewModel GetOrCreateChildrenGroup(GameObjectNodeViewModel node)
    {
        var existing = node.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new ChildrenGroupNodeViewModel(node);
        node.Children.Add(created);
        return created;
    }

    private static ChildrenGroupNodeViewModel GetOrCreateChildrenGroup(GlobalObjectNodeViewModel node)
    {
        var existing = node.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new ChildrenGroupNodeViewModel(node);
        node.Children.Add(created);
        return created;
    }

    private static ChildrenGroupNodeViewModel GetOrCreateChildrenGroup(TemplateGameObjectNodeViewModel node)
    {
        var existing = node.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new ChildrenGroupNodeViewModel(node);
        node.Children.Add(created);
        return created;
    }

    private bool AddNewContainedObject(GameObjectNodeViewModel parentNode)
    {
        if (!TryAddContainedObjectCore(
                parentNode,
                parentNode.ParentObjectsNode.GameObjects,
                static node => node.GameObject,
                static node => GetOrCreateChildrenGroup(node),
                static (instance, node) => CreateNestedRoomObjectNode(instance, node),
                defaultObjectName: "New Game Object",
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual contained game objects from '{objectName}'."
            : $"Added contained game object '{objectName}'.";
        return true;
    }

    private static bool CanPromoteToBaseObject(GameObjectNodeViewModel node)
    {
        return !node.GameObject.LinkedBaseObjectId.HasValue;
    }

    private sealed record PromoteScopeTarget(
        BaseObjectPromotionScopeKind ScopeKind,
        string ScopeLabel,
        List<GameObject> Catalog,
        ScopeNodeBase? CatalogParentScope);

    private bool PromoteToBaseObject(GameObjectNodeViewModel objectNode)
    {
        if (objectNode.ParentObjectsNode is not RoomGameObjectsNodeViewModel roomObjectsNode)
        {
            return false;
        }

        if (!CanPromoteToBaseObject(objectNode))
        {
            return false;
        }

        var area = roomObjectsNode.RoomNode.Area;
        var scopeTargets = BuildPromoteScopeTargets(area);
        if (scopeTargets.Count == 0)
        {
            return false;
        }

        var scopeOptions = scopeTargets
            .Select(static target => new BaseObjectPromotionScopeOption(target.ScopeKind, target.ScopeLabel))
            .ToList();

        if (!_treeContextInteractionService.TrySelectBaseObjectPromotionScope(scopeOptions, out var selectedScopeKind))
        {
            return false;
        }

        var scopeTarget = scopeTargets.FirstOrDefault(target => target.ScopeKind == selectedScopeKind) ?? scopeTargets[0];
        var baseCatalog = scopeTarget.Catalog;
        var source = objectNode.GameObject;
        var normalizedName = NormalizeScopedName(source.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _projectUiService.ShowWarning("Object name is required before promoting to base object.", "Promote to Base Object");
            return false;
        }

        var duplicateExists = baseCatalog.Any(existing =>
            string.Equals(NormalizeScopedName(existing.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicateExists)
        {
            _projectUiService.ShowWarning($"A base object named '{source.Name}' already exists in {scopeTarget.ScopeLabel}.", "Promote to Base Object");
            return false;
        }

        var definition = CloneTemplateObject(source);
        ResetHideEmptyConfigurationForNewObject(definition);
        definition.Name = source.Name;
        definition.NameInGame = source.NameInGame;
        NormalizePromotedBaseDefinition(definition);

        var previousLinkedBaseObjectId = source.LinkedBaseObjectId;
        var previousLinkActionsToBase = source.LinkActionsToBaseObject;
        var previousActions = CloneActions(source.AvailableActions);

        try
        {
            if (baseCatalog.Contains(definition))
            {
                return false;
            }

            baseCatalog.Add(definition);

            if (!baseCatalog.Contains(definition))
            {
                return false;
            }

            var injectedFailure = PromoteToBaseObjectFailureInjection?.Invoke(definition);
            if (injectedFailure is not null)
            {
                throw injectedFailure;
            }

            var baseCatalogNode = FindBaseCatalogNodeForPromotionTarget(scopeTarget);

            if (baseCatalogNode is not null)
            {
                var baseNode = CreateTemplateObjectNode(definition, baseCatalogNode);
                baseCatalogNode.Children.Add(baseNode);
                baseCatalogNode.IsExpanded = true;
            }

            source.LinkedBaseObjectId = definition.ObjectId;
            source.LinkActionsToBaseObject = true;
            source.AvailableActions.Clear();

            NotifyProjectEdited();
            SelectedNode = objectNode;
            ExportStatus = $"Promoted '{source.Name}' to {scopeTarget.ScopeLabel} base objects and converted the placed object to an instance.";
            return true;
        }
        catch
        {
            baseCatalog.Remove(definition);
            source.LinkedBaseObjectId = previousLinkedBaseObjectId;
            source.LinkActionsToBaseObject = previousLinkActionsToBase;
            source.AvailableActions = previousActions;
            throw;
        }
    }

    private bool CreateTemplateFromObject(GameObjectNodeViewModel objectNode)
    {
        var templateCatalog = _project.ObjectTemplates;
        var source = objectNode.GameObject;
        if (!_treeContextInteractionService.TryGetTemplateNameFromObject(source.Name, out var templateName))
        {
            return false;
        }

        var normalizedName = NormalizeScopedName(templateName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _projectUiService.ShowWarning("Object name is required before creating an object template.", "Create Template From");
            return false;
        }

        var duplicateExists = templateCatalog.Any(existing =>
            string.Equals(NormalizeScopedName(existing.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicateExists)
        {
            _projectUiService.ShowWarning($"An object template named '{templateName}' already exists.", "Create Template From");
            return false;
        }

        var definition = CloneTemplateObject(source);
        ResetHideEmptyConfigurationForNewObject(definition);
        definition.Name = templateName;
        definition.NameInGame = source.NameInGame;
        NormalizePromotedBaseDefinition(definition);

        try
        {
            if (templateCatalog.Contains(definition))
            {
                return false;
            }

            templateCatalog.Add(definition);

            if (!templateCatalog.Contains(definition))
            {
                return false;
            }

            var templateCatalogNode = EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<ObjectTemplatesNodeViewModel>()
                .FirstOrDefault(node => !node.IsBaseCatalog && node.Parent is ProjectRootNodeViewModel);

            if (templateCatalogNode is not null)
            {
                var templateNode = CreateTemplateObjectNode(definition, templateCatalogNode);
                templateCatalogNode.Children.Add(templateNode);
                templateCatalogNode.IsExpanded = true;
            }

            NotifyProjectEdited();
            SelectedNode = objectNode;
            ExportStatus = $"Created object template '{definition.Name}' from placed object.";
            return true;
        }
        catch
        {
            templateCatalog.Remove(definition);
            throw;
        }
    }

    private bool CreateTemplateFromRoom(RoomNodeViewModel roomNode)
    {
        var templateCatalog = _project.RoomTemplates;
        var source = roomNode.Room;
        if (!_treeContextInteractionService.TryGetTemplateNameFromObject(source.Name, out var templateName))
        {
            return false;
        }

        var normalizedName = NormalizeScopedName(templateName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _projectUiService.ShowWarning("Room name is required before creating a room template.", "Create Template From");
            return false;
        }

        var duplicateExists = templateCatalog.Any(existing =>
            string.Equals(NormalizeScopedName(existing.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicateExists)
        {
            _projectUiService.ShowWarning($"A room template named '{templateName}' already exists.", "Create Template From");
            return false;
        }

        var definition = CloneTemplateRoom(source);
        ResetHideEmptyConfigurationForNewRoom(definition);
        definition.Name = templateName;
        var normalizationWarnings = NormalizeSharedVariableLinksToRoomBoundary(source, definition, "Create Template From");

        try
        {
            if (templateCatalog.Contains(definition))
            {
                return false;
            }

            templateCatalog.Add(definition);

            if (!templateCatalog.Contains(definition))
            {
                return false;
            }

            var templateCatalogNode = EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<RoomTemplatesNodeViewModel>()
                .FirstOrDefault(node => node.Parent is ProjectRootNodeViewModel);

            if (templateCatalogNode is not null)
            {
                var templateNode = CreateTemplateRoomNode(definition, templateCatalogNode);
                templateCatalogNode.Children.Add(templateNode);
                templateCatalogNode.IsExpanded = true;
            }

            NotifyProjectEdited();
            SelectedNode = roomNode;
            if (normalizationWarnings.Count > 0)
            {
                var message = string.Join(Environment.NewLine, normalizationWarnings);
                _projectUiService.ShowWarning(message, "Create Template From");
                AppendOutputConsoleLine($"RoomTemplatePromotion.SharedVariableNormalization: {message}");
            }

            ExportStatus = $"Created room template '{definition.Name}' from room '{source.Name}'.";
            return true;
        }
        catch
        {
            templateCatalog.Remove(definition);
            throw;
        }
    }

    private List<string> NormalizeSharedVariableLinksToRoomBoundary(Room sourceRoom, Room clonedRoom, string operationLabel)
    {
        var warnings = new List<string>();
        var ownerIdMap = BuildRoomCloneOwnerIdMap(sourceRoom, clonedRoom);
        var boundaryOwnerIds = ownerIdMap.Keys.ToHashSet();
        var sourceSharedIds = EnumerateRoomVariables(sourceRoom)
            .Where(static entry => entry.Variable.SharedVariableId.HasValue && entry.Variable.SharedVariableId.Value != Guid.Empty)
            .Select(static entry => entry.Variable.SharedVariableId!.Value)
            .Distinct()
            .ToList();

        // Reset clone-side links first, then rehydrate only boundary-safe links.
        foreach (var entry in EnumerateRoomVariables(clonedRoom))
        {
            entry.Variable.SharedVariableId = null;
        }

        foreach (var sharedId in sourceSharedIds)
        {
            var shared = _project.SharedVariables.FirstOrDefault(candidate => candidate.Id == sharedId);
            if (shared is null)
            {
                warnings.Add($"{operationLabel}: dropped missing shared variable '{sharedId:N}' from room template boundary.");
                continue;
            }

            var supportedParticipants = shared.Participants
                .Where(static participant => IsSupportedRoomTemplateParticipantKind(participant.Kind))
                .ToList();
            var boundaryParticipants = supportedParticipants
                .Where(participant => boundaryOwnerIds.Contains(participant.OwnerId))
                .ToList();

            var droppedExternalCount = supportedParticipants.Count - boundaryParticipants.Count;
            var droppedUnsupportedCount = shared.Participants.Count - supportedParticipants.Count;

            if (boundaryParticipants.Count == 0)
            {
                if (droppedExternalCount > 0 || droppedUnsupportedCount > 0)
                {
                    warnings.Add($"{operationLabel}: dropped out-of-boundary shared variable '{shared.Name}' links ({droppedExternalCount + droppedUnsupportedCount} participant(s)).");
                }

                continue;
            }

            var remappedParticipants = new List<SharedVariableParticipant>();
            foreach (var participant in boundaryParticipants)
            {
                if (!ownerIdMap.TryGetValue(participant.OwnerId, out var mappedOwnerId))
                {
                    continue;
                }

                remappedParticipants.Add(new SharedVariableParticipant
                {
                    Kind = participant.Kind,
                    OwnerId = mappedOwnerId,
                    VariableName = participant.VariableName,
                    Leg = participant.Leg
                });
            }

            if (remappedParticipants.Count == 0)
            {
                continue;
            }

            var clonedShared = CreateClonedSharedVariableDefinition(shared, remappedParticipants);
            _project.SharedVariables.Add(clonedShared);

            foreach (var participant in remappedParticipants)
            {
                var variable = ResolveVariableForParticipant(clonedRoom, participant);
                if (variable is not null)
                {
                    variable.SharedVariableId = clonedShared.Id;
                }
            }

            if (droppedExternalCount > 0 || droppedUnsupportedCount > 0)
            {
                warnings.Add($"{operationLabel}: dropped out-of-boundary shared links from '{shared.Name}' ({droppedExternalCount + droppedUnsupportedCount} participant(s)).");
            }
        }

        RemoveEmptySharedVariables();
        return warnings;
    }

    private static bool IsSupportedRoomTemplateParticipantKind(string? kind)
    {
        return string.Equals(kind?.Trim(), "object", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind?.Trim(), "room", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreNearlyEqual(double left, double right)
    {
        return Math.Abs(left - right) <= 0.000001d;
    }

    private static Dictionary<Guid, Guid> BuildRoomCloneOwnerIdMap(Room source, Room clone)
    {
        var map = new Dictionary<Guid, Guid>();
        map[source.Id] = clone.Id;
        MapObjectPairs(source.GameObjects, clone.GameObjects, map);
        return map;
    }

    private static void MapObjectPairs(IReadOnlyList<GameObject> source, IReadOnlyList<GameObject> clone, IDictionary<Guid, Guid> map)
    {
        var count = Math.Min(source.Count, clone.Count);
        for (var index = 0; index < count; index++)
        {
            var sourceObject = source[index];
            var cloneObject = clone[index];
            map[sourceObject.ObjectId] = cloneObject.ObjectId;
            MapObjectPairs(sourceObject.ContainedObjects, cloneObject.ContainedObjects, map);
        }
    }

    private static IEnumerable<(Guid OwnerId, GamePropertyDefinition Variable)> EnumerateRoomVariables(Room room)
    {
        foreach (var variable in room.Variables)
        {
            yield return (room.Id, variable);
        }

        foreach (var obj in EnumerateGameObjectsRecursive(room.GameObjects))
        {
            foreach (var variable in obj.Variables)
            {
                yield return (obj.ObjectId, variable);
            }
        }
    }

    private static SharedVariableDefinition CreateClonedSharedVariableDefinition(
        SharedVariableDefinition source,
        List<SharedVariableParticipant> participants)
    {
        var cloned = new SharedVariableDefinition
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            DefaultValue = source.DefaultValue,
            ValueRestriction = source.ValueRestriction,
            Participants = participants
        };

        return cloned;
    }

    private static GamePropertyDefinition? ResolveVariableForParticipant(Room room, SharedVariableParticipant participant)
    {
        if (string.Equals(participant.Kind, "room", StringComparison.OrdinalIgnoreCase)
            && participant.OwnerId == room.Id)
        {
            return room.Variables.FirstOrDefault(variable =>
                string.Equals(variable.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(participant.Kind, "object", StringComparison.OrdinalIgnoreCase))
        {
            var owner = EnumerateGameObjectsRecursive(room.GameObjects)
                .FirstOrDefault(obj => obj.ObjectId == participant.OwnerId);
            if (owner is null)
            {
                return null;
            }

            return owner.Variables.FirstOrDefault(variable =>
                string.Equals(variable.Name, participant.VariableName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private bool AddNewRoomTemplate(RoomTemplatesNodeViewModel templatesNode)
    {
        var defaultTemplateName = MakeUniqueScopedEntityName(
            templatesNode.RoomTemplates,
            static room => room.Name,
            "New Room Template");

        if (!_treeContextInteractionService.TryGetTemplateNameFromObject(defaultTemplateName, out var templateName))
        {
            return false;
        }

        var normalizedName = NormalizeScopedName(templateName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _projectUiService.ShowWarning("Room template name is required.", "Add New Room Template");
            return false;
        }

        var duplicateExists = templatesNode.RoomTemplates.Any(existing =>
            string.Equals(NormalizeScopedName(existing.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicateExists)
        {
            _projectUiService.ShowWarning($"A room template named '{templateName}' already exists.", "Add New Room Template");
            return false;
        }

        var templateRoom = new Room
        {
            Name = templateName,
            RoomImageCanvasWidth = _project.RoomImageCanvasWidth,
            RoomImageCanvasHeight = _project.RoomImageCanvasHeight
        };
        templateRoom.DesignerPersistenceScopeKind = ScopeNodeKind.RoomTemplates;

        if (!templatesNode.CatalogParentScope.AddChildScope(templateRoom))
        {
            return false;
        }

        var roomNode = CreateTemplateRoomNode(templateRoom, templatesNode);
        templatesNode.Children.Add(roomNode);
        templatesNode.IsExpanded = true;

        NotifyProjectEdited();
        SelectedNode = roomNode;
        ExportStatus = $"Added room template '{templateName}'.";
        return true;
    }

    private List<PromoteScopeTarget> BuildPromoteScopeTargets(Area area)
    {
        var targets = new List<PromoteScopeTarget>
        {
            new(
                BaseObjectPromotionScopeKind.Area,
                $"Area: {area.Name}",
                area.BaseObjects,
                area),
            new(
                BaseObjectPromotionScopeKind.Global,
                "Global",
                _project.BaseObjects,
                null)
        };

        if (!TryResolveAreaAncestors(area, out var country, out var planet))
        {
            return targets;
        }

        targets.Insert(1,
            new PromoteScopeTarget(
                BaseObjectPromotionScopeKind.Country,
                $"Country: {country.Name}",
                country.BaseObjects,
                country));
        targets.Insert(2,
            new PromoteScopeTarget(
                BaseObjectPromotionScopeKind.Planet,
                $"Planet: {planet.Name}",
                planet.BaseObjects,
                planet));

        return targets;
    }

    private bool TryResolveAreaAncestors(Area area, out Country country, out Planet planet)
    {
        foreach (var candidatePlanet in _project.Planets)
        {
            foreach (var candidateCountry in candidatePlanet.Countries)
            {
                if (!candidateCountry.Areas.Contains(area))
                {
                    continue;
                }

                country = candidateCountry;
                planet = candidatePlanet;
                return true;
            }
        }

        country = new Country();
        planet = new Planet();
        return false;
    }

    private ObjectTemplatesNodeViewModel? FindBaseCatalogNodeForPromotionTarget(PromoteScopeTarget target)
    {
        return target.ScopeKind switch
        {
            BaseObjectPromotionScopeKind.Global => EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<ObjectTemplatesNodeViewModel>()
                .FirstOrDefault(node => node.IsBaseCatalog && node.Parent is ProjectRootNodeViewModel),
            _ => EnumerateHierarchyNodes(HierarchyRoots)
                .OfType<ObjectTemplatesNodeViewModel>()
                .FirstOrDefault(node => node.IsBaseCatalog && ReferenceEquals(node.CatalogParentScope, target.CatalogParentScope))
        };
    }

    private static void NormalizePromotedBaseDefinition(GameObject definition)
    {
        definition.LinkedBaseObjectId = null;
        definition.LinkActionsToBaseObject = false;
        definition.InstanceOverrides.Clear();

        foreach (var child in definition.ContainedObjects)
        {
            NormalizePromotedBaseDefinition(child);
        }
    }

    private bool AddNewGlobalGameObject(GlobalObjectsNodeViewModel globalObjectsNode)
    {
        if (!TryAddTopLevelObjectCore(
                globalObjectsNode,
                static node => node.GameObjects,
            static node => node.ScopeNode,
                static node => node,
                static (instance, node) =>
                {
                    var addedNode = CreateGlobalGameObjectNode(instance, node);
                    node.Children.Add(addedNode);
                    return addedNode;
                },
                expandCollectionNodeAction: null,
                postAddAction: null,
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual quantifiable global objects from '{objectName}'."
            : $"Added global object '{objectName}'.";
        return true;
    }

    private bool TryAddTopLevelObjectCore<TCollectionNode, TObjectNode>(
        TCollectionNode collectionNode,
        Func<TCollectionNode, List<GameObject>> gameObjectsAccessor,
        Func<TCollectionNode, IScopedAwareNode> scopeNodeAccessor,
        Func<TCollectionNode, HierarchyNodeViewModel> expandNodeAccessor,
        Func<GameObject, TCollectionNode, TObjectNode> insertNode,
        Action<TCollectionNode>? expandCollectionNodeAction,
        Action? postAddAction,
        out HierarchyNodeViewModel selectedNode,
        out string objectName,
        out int addedCount,
        out bool usedIndividualExpansion)
        where TCollectionNode : class
        where TObjectNode : HierarchyNodeViewModel
    {
        selectedNode = null!;
        objectName = string.Empty;
        addedCount = 0;
        usedIndividualExpansion = false;

        var gameObjects = gameObjectsAccessor(collectionNode);
        var defaultObjectName = MakeUniqueScopedEntityName(gameObjects, static obj => obj.Name, "New Game Object");

        if (!TryCreateAndConfigureNewObject(
                defaultInventoriable: false,
                gameObjects,
                gameObjects,
                defaultObjectName,
                out var interactiveObject,
                out objectName))
        {
            return false;
        }

        usedIndividualExpansion = interactiveObject.IsQuantifiable
            && string.Equals(interactiveObject.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)
            && interactiveObject.Quantity > 1;

        var scopeNode = scopeNodeAccessor(collectionNode);
        var addedNodes = new List<TObjectNode>();

        if (usedIndividualExpansion)
        {
            var instanceCount = interactiveObject.Quantity;
            interactiveObject.LinkedBaseObjectId = null;
            interactiveObject.LinkActionsToBaseObject = false;
            for (var index = 0; index < instanceCount; index++)
            {
                var instance = index == 0
                    ? interactiveObject
                    : CloneTemplateObject(interactiveObject);

                instance.Quantity = 1;
                if (index > 0)
                {
                    ConfigureAsLinkedRoomInstance(instance, interactiveObject.ObjectId);
                }

                instance.Name = MakeUniqueScopedEntityName(gameObjects, static obj => obj.Name, objectName, instance);
                if (!scopeNode.AddChildScope(instance))
                {
                    continue;
                }

                if (scopeNode.ScopeKind == ScopeNodeKind.Room)
                {
                    MoveGameObjectToTop(gameObjects, instance);
                }

                var instanceNode = insertNode(instance, collectionNode);

                addedNodes.Add(instanceNode);
            }
        }
        else
        {
            if (!scopeNode.AddChildScope(interactiveObject))
            {
                return false;
            }

            if (scopeNode.ScopeKind == ScopeNodeKind.Room)
            {
                MoveGameObjectToTop(gameObjects, interactiveObject);
            }

            var objectNode = insertNode(interactiveObject, collectionNode);

            addedNodes.Add(objectNode);
        }

        if (addedNodes.Count == 0)
        {
            return false;
        }

        expandNodeAccessor(collectionNode).IsExpanded = true;
        expandCollectionNodeAction?.Invoke(collectionNode);
        RefreshScopedNameIndex(gameObjects, static obj => obj.Name);
        postAddAction?.Invoke();

        selectedNode = addedNodes[0];
        addedCount = addedNodes.Count;
        return true;
    }

    private static void MoveGameObjectToTop(List<GameObject> gameObjects, GameObject gameObject)
    {
        var currentIndex = gameObjects.IndexOf(gameObject);
        if (currentIndex <= 0)
        {
            return;
        }

        gameObjects.RemoveAt(currentIndex);
        gameObjects.Insert(0, gameObject);
    }

    private bool AddNewContainedGlobalGameObject(GlobalObjectNodeViewModel parentNode)
    {
        if (!TryAddContainedObjectCore(
                parentNode,
                parentNode.ParentGlobalObjectsNode.GameObjects,
                static node => node.GameObject,
                static node => GetOrCreateChildrenGroup(node),
                static (instance, node) => CreateNestedGlobalGameObjectNode(instance, node),
                defaultObjectName: "New Game Object",
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = usedIndividualExpansion
            ? $"Added {addedCount} individual contained game objects from '{objectName}'."
            : $"Added contained game object '{objectName}'.";
        return true;
    }

    private bool TryAddContainedObjectCore<TParentNode, TObjectNode>(
        TParentNode parentNode,
        List<GameObject> compositePartSource,
        Func<TParentNode, GameObject> parentObjectAccessor,
        Func<TParentNode, ChildrenGroupNodeViewModel> childrenGroupAccessor,
        Func<GameObject, TParentNode, TObjectNode> nestedNodeFactory,
        string defaultObjectName,
        out HierarchyNodeViewModel selectedNode,
        out string objectName,
        out int addedCount,
        out bool usedIndividualExpansion)
        where TParentNode : class
        where TObjectNode : HierarchyNodeViewModel
    {
        selectedNode = null!;
        objectName = string.Empty;
        addedCount = 0;
        usedIndividualExpansion = false;

        var parentObject = parentObjectAccessor(parentNode);
        var siblings = parentObject.ContainedObjects;
    defaultObjectName = MakeUniqueScopedEntityName(siblings, static obj => obj.Name, defaultObjectName);

        if (!TryCreateAndConfigureNewObject(
            defaultInventoriable: false,
            siblings,
            compositePartSource,
            defaultObjectName,
            out var interactiveObject,
            out objectName))
        {
            return false;
        }

        usedIndividualExpansion = interactiveObject.IsQuantifiable
            && string.Equals(interactiveObject.QuantifiablePlacementDistributionMode, "IndividualInstances", StringComparison.OrdinalIgnoreCase)
            && interactiveObject.Quantity > 1;

        var addedNodes = new List<TObjectNode>();
        var parentChildrenGroup = childrenGroupAccessor(parentNode);

        if (usedIndividualExpansion)
        {
            var instanceCount = interactiveObject.Quantity;
            interactiveObject.LinkedBaseObjectId = null;
            interactiveObject.LinkActionsToBaseObject = false;
            for (var index = 0; index < instanceCount; index++)
            {
                var instance = index == 0
                    ? interactiveObject
                    : CloneTemplateObject(interactiveObject);

                instance.Quantity = 1;
                if (index > 0)
                {
                    ConfigureAsLinkedRoomInstance(instance, interactiveObject.ObjectId);
                }

                instance.Name = MakeUniqueScopedEntityName(siblings, static obj => obj.Name, objectName, instance);

                if (!parentObject.AddChildScope(instance))
                {
                    continue;
                }

                var instanceNode = nestedNodeFactory(instance, parentNode);
                parentChildrenGroup.Children.Add(instanceNode);
                addedNodes.Add(instanceNode);
            }
        }
        else
        {
            if (!parentObject.AddChildScope(interactiveObject))
            {
                return false;
            }

            var objectNode = nestedNodeFactory(interactiveObject, parentNode);
            parentChildrenGroup.Children.Add(objectNode);
            addedNodes.Add(objectNode);
        }

        if (addedNodes.Count == 0)
        {
            return false;
        }

        if (parentNode is HierarchyNodeViewModel parentHierarchyNode)
        {
            parentHierarchyNode.IsExpanded = true;
        }

        RefreshScopedNameIndex(siblings, static obj => obj.Name);
        selectedNode = addedNodes[0];
        addedCount = addedNodes.Count;
        return true;
    }

    private bool AddNewTemplateObject(ObjectTemplatesNodeViewModel templatesNode)
    {
        var defaultObjectName = MakeUniqueScopedEntityName(
            templatesNode.CatalogObjects,
            static obj => obj.Name,
            templatesNode.IsBaseCatalog ? "New Base Object" : "New Object Template");

        if (!TryCreateAndConfigureNewObject(
                defaultInventoriable: false,
                templatesNode.CatalogObjects,
                templatesNode.CatalogObjects,
                defaultObjectName,
                out var interactiveObject,
                out var objectName))
        {
            return false;
        }

        ApplyDesignerTemplateScope(interactiveObject);

        var templatesScope = templatesNode.CatalogParentScope;
        if (templatesScope is ObjectTemplatesScopeNode or BaseObjectsScopeNode)
        {
            if (!templatesScope.AddChildScope(interactiveObject))
            {
                return false;
            }
        }
        else
        {
            if (!templatesNode.CatalogObjects.Contains(interactiveObject))
            {
                templatesNode.CatalogObjects.Add(interactiveObject);
            }

            interactiveObject.ParentScope = templatesScope;
        }

        var objectNode = CreateTemplateObjectNode(interactiveObject, templatesNode);
        templatesNode.Children.Add(objectNode);
        templatesNode.IsExpanded = true;
        RefreshScopedNameIndex(templatesNode.CatalogObjects, static obj => obj.Name);

        NotifyProjectEdited();
        SelectedNode = objectNode;
        ExportStatus = templatesNode.IsBaseCatalog
            ? $"Added base object '{objectName}'."
            : $"Added object template '{objectName}'.";
        return true;
    }

    private bool AddNewContainedTemplateObject(TemplateGameObjectNodeViewModel parentNode)
    {
        var defaultObjectName = MakeUniqueScopedEntityName(
            parentNode.GameObject.ContainedObjects,
            static obj => obj.Name,
            parentNode.ParentTemplatesNode.IsBaseCatalog ? "New Base Object" : "New Object Template");

        if (!TryAddContainedObjectCore(
                parentNode,
                parentNode.ParentTemplatesNode.CatalogObjects,
                static node => node.GameObject,
                static node => GetOrCreateChildrenGroup(node),
                static (instance, node) => CreateNestedTemplateObjectNode(instance, node),
                defaultObjectName,
                out var selectedNode,
                out var objectName,
                out var addedCount,
                out var usedIndividualExpansion))
        {
            return false;
        }

            ApplyDesignerTemplateScope(parentNode.ParentTemplatesNode.CatalogObjects);

        NotifyProjectEdited();
        SelectedNode = selectedNode;
        ExportStatus = parentNode.ParentTemplatesNode.IsBaseCatalog
            ? (usedIndividualExpansion
                ? $"Added {addedCount} individual contained base objects from '{objectName}'."
                : $"Added contained base object '{objectName}'.")
            : (usedIndividualExpansion
                ? $"Added {addedCount} individual contained object templates from '{objectName}'."
                : $"Added contained object template '{objectName}'.");
        return true;
    }

    private bool TryCreateNewObjectFromTemplate(bool defaultInventoriable, out GameObject gameObject)
    {
        gameObject = null!;
        GameObject? selectedTemplate = null;

        if (_project.ObjectTemplates.Count > 0)
        {
            if (!_treeContextInteractionService.TrySelectObjectTemplate(_project.ObjectTemplates, out selectedTemplate))
            {
                return false;
            }
        }

        if (selectedTemplate is not null)
        {
            gameObject = CloneTemplateObject(selectedTemplate);
            ResetHideEmptyConfigurationForNewObject(gameObject);
            return true;
        }

        gameObject = CreateBlankObject(defaultInventoriable);
        return true;
    }

    private bool TryCreateAndConfigureNewObject(
        bool defaultInventoriable,
        List<GameObject> siblings,
        IEnumerable<GameObject> compositePartSource,
        string defaultObjectName,
        out GameObject interactiveObject,
        out string objectName)
    {
        objectName = string.Empty;
        interactiveObject = null!;

        if (!TryCreateNewObjectFromTemplate(defaultInventoriable, out interactiveObject))
        {
            return false;
        }

        if (!TryConfigureNewObjectBeforeInsert(
                interactiveObject,
                siblings,
                compositePartSource,
                defaultObjectName,
                out objectName))
        {
            return false;
        }

        return true;
    }

    private static GameObject CreateBlankObject(bool isInventoriable)
    {
        return new GameObject
        {
            Name = "New Game Object",
            ProducerNotes = string.Empty,
            IsInventoriable = isInventoriable,
            Description = string.Empty,
            Commands = new List<string>(),
            Variables = new List<GamePropertyDefinition>(),
            ContainedObjects = new List<GameObject>()
        };
    }

    private static GameObject CloneTemplateObject(GameObject template)
    {
        var clone = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = template.Name,
            ObjectType = template.ObjectType,
            NameInGame = template.NameInGame,
            NameSynonyms = template.NameSynonyms.ToList(),
            ProducerNotes = template.ProducerNotes,
            IncludeInPreview = template.IncludeInPreview,
            IsInventoriable = template.IsInventoriable,
            InventoryPointsDefaultValue = template.InventoryPointsDefaultValue,
            IsContainer = template.IsContainer,
            ContainerPointsDefaultValue = template.ContainerPointsDefaultValue,
            IsCapacityPointShareDividerEnabled = template.IsCapacityPointShareDividerEnabled,
            CapacityPointShareDividerDefaultValue = template.CapacityPointShareDividerDefaultValue,
            IsOpenable = template.IsOpenable,
            IsOpenDefaultValue = template.IsOpenDefaultValue,
            IsLockable = template.IsLockable,
            IsLockedDefaultValue = template.IsLockedDefaultValue,
            LockOperationRequirements = CloneLockOperationRequirements(template.LockOperationRequirements),
            IsActivatable = template.IsActivatable,
            IsActiveDefaultValue = template.IsActiveDefaultValue,
            IsHidable = template.IsHidable,
            IsHiddenDefaultValue = template.IsHiddenDefaultValue,
            IsQuantifiable = template.IsQuantifiable,
            Quantity = template.Quantity,
            QuantifiablePlacementDistributionMode = template.QuantifiablePlacementDistributionMode,
            LinkedBaseObjectId = null,
            LinkActionsToBaseObject = false,
            IsCompositeTarget = template.IsCompositeTarget,
            IsCompositeReversible = template.IsCompositeReversible,
            CompositePartRequirementMode = template.CompositePartRequirementMode,
            CompositeMinimumRequiredPartCount = template.CompositeMinimumRequiredPartCount,
            Description = template.Description,
            PositionX = template.PositionX,
            PositionY = template.PositionY,
            RenderZOrder = template.RenderZOrder,
            AuthoredRenderOrder = template.AuthoredRenderOrder,
            ImageRotationDegrees = template.ImageRotationDegrees,
            ImageVariantChooserScript = template.ImageVariantChooserScript,
            IsMovable = template.IsMovable,
            IsMovableDefaultValue = template.IsMovableDefaultValue,
            SpatialType = template.SpatialType,
            StackGroup = template.StackGroup,
            FootprintWidthCells = template.FootprintWidthCells,
            FootprintHeightCells = template.FootprintHeightCells,
            FootprintOrientation = template.FootprintOrientation,
            HeadingDirection = template.HeadingDirection,
            ObjectHeightUnits = template.ObjectHeightUnits,
            HeightInRoom = template.HeightInRoom,
            AuthoredBaseHeightInRoom = template.AuthoredBaseHeightInRoom,
            IsHeightPinned = template.IsHeightPinned,
            OccupiedCellIds = template.OccupiedCellIds.ToList(),
            OccupancyDerivationSourceEcho = template.OccupancyDerivationSourceEcho,
            StackScaleStepOverride = template.StackScaleStepOverride,
            MinStackScaleOverride = template.MinStackScaleOverride,
            MovementRestrictions = CloneMovementRestrictions(template.MovementRestrictions),
            ImageVariants = template.ImageVariants.Select(variant => new ObjectImageVariant
            {
                VariantName = variant.VariantName,
                FullImagePath = variant.FullImagePath,
                ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                ImageScale = variant.ImageScale,
                IsDefault = variant.IsDefault
            }).ToList(),
            AdditionalVerbs = template.AdditionalVerbs.ToList(),
            AdditionalDirectionals = template.AdditionalDirectionals.ToList(),
            EventSubscriptions = CloneEventSubscriptions(template.EventSubscriptions),
            TimerDefinitions = CloneTimerDefinitions(template.TimerDefinitions),
            Commands = template.Commands.ToList(),
            Variables = template.Variables.Select(variable => new GamePropertyDefinition
            {
                Id = Guid.NewGuid(),
                Name = variable.Name,
                DefaultValue = variable.DefaultValue,
                Lifetime = variable.Lifetime,
                ValueRestriction = variable.ValueRestriction
            }).ToList(),
            AvailableActions = CloneActions(template.AvailableActions),
            SoundEffectLibraryEntries = template.SoundEffectLibraryEntries
                .Select(CloneSoundEffectLibraryEntry)
                .ToList(),
            AdditionalDirectionalTraversalMappings = template.AdditionalDirectionalTraversalMappings
                .Select(mapping => new DirectionalTraversalMapping
                {
                    Token = mapping.Token,
                    TraversalDirection = mapping.TraversalDirection
                })
                .ToList(),
            CompositeRecipeId = template.CompositeRecipeId,
            CompositeRequiredParts = template.CompositeRequiredParts.Select(part => new CompositePartRequirement
            {
                PartObjectId = part.PartObjectId,
                PartObjectName = part.PartObjectName,
                RequiredQuantity = part.RequiredQuantity,
                MatchKind = part.MatchKind,
                MatchValue = part.MatchValue,
                SatisfactionMode = part.SatisfactionMode,
                ConsumptionPolicy = part.ConsumptionPolicy,
                OptionalPart = part.OptionalPart,
                VariableRequirements = (part.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                    .Select(static requirement => new LockParticipantVariableRequirement
                    {
                        VariableName = requirement.VariableName.Trim(),
                        Operator = requirement.Operator,
                        ExpectedValue = requirement.ExpectedValue,
                        QuantityEvaluationMode = requirement.QuantityEvaluationMode
                    })
                    .ToList()
            }).ToList(),
            ContainedObjects = template.ContainedObjects.Select(CloneTemplateObject).ToList()
        };

        clone.DesignerPersistenceScopeKind = template.DesignerPersistenceScopeKind;

        clone.InitializeFeatureFlagsFromKnownVariables();
        clone.ApplyFeatureVariableContract();
        return clone;
    }

    private static void ApplyDesignerTemplateScope(IEnumerable<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            ApplyDesignerTemplateScope(obj);
        }
    }

    private static void ApplyDesignerTemplateScope(GameObject obj)
    {
        obj.DesignerPersistenceScopeKind = ScopeNodeKind.Templates;
        foreach (var child in obj.ContainedObjects)
        {
            ApplyDesignerTemplateScope(child);
        }
    }

    private static void ApplyDesignerRoomTemplateScope(Room room)
    {
        room.DesignerPersistenceScopeKind = ScopeNodeKind.RoomTemplates;
        ApplyDesignerTemplateScope(room.GameObjects);
    }

    private static void ResetHideEmptyConfigurationForNewRoom(Room room)
    {
        room.HideEmptyConfiguration = false;
        foreach (var gameObject in room.GameObjects)
        {
            ResetHideEmptyConfigurationForNewObject(gameObject);
        }
    }

    private static void ResetHideEmptyConfigurationForNewObject(GameObject gameObject)
    {
        gameObject.HideEmptyConfiguration = false;
        foreach (var child in gameObject.ContainedObjects)
        {
            ResetHideEmptyConfigurationForNewObject(child);
        }
    }

    private static void ConfigureAsLinkedRoomInstance(GameObject instance, Guid linkedBaseObjectId)
    {
        instance.LinkedBaseObjectId = linkedBaseObjectId;
        instance.LinkActionsToBaseObject = true;
        ClearActionsRecursively(instance);
    }

    private static void ClearActionsRecursively(GameObject obj)
    {
        obj.AvailableActions.Clear();
        foreach (var child in obj.ContainedObjects)
        {
            ClearActionsRecursively(child);
        }
    }

    private bool TryConfigureNewRoomBeforeInsert(
        AreaNodeViewModel areaNode,
        string defaultName,
        string defaultNameInGame,
        out RoomSettingsEditRequest configured)
    {
        var initial = new RoomSettingsEditRequest(
            defaultName,
            string.Empty,
            defaultNameInGame,
            RoomCanvasWidth: _project.RoomImageCanvasWidth,
            RoomCanvasHeight: _project.RoomImageCanvasHeight,
            ProjectRoomGridCellSize: _project.RoomDesignerGridCellSize);
        if (!_treeContextInteractionService.TryEditRoomSettings(initial, out var updated))
        {
            configured = initial;
            return false;
        }

        var uniqueName = MakeUniqueScopedEntityName(areaNode.Area.Rooms, static room => room.Name, updated.Name);
        configured = updated with { Name = uniqueName };
        return true;
    }

    private bool TryConfigureNewObjectBeforeInsert(
        GameObject target,
        List<GameObject> siblings,
        IEnumerable<GameObject> compositePartSource,
        string defaultName,
        out string configuredName)
    {
        configuredName = string.Empty;
        var (stackScaleStepOverride, minStackScaleOverride) = ResolveObjectStackScaleEditorDefaults(target);

        var initial = new ObjectBasicPropertiesEditRequest(
            defaultName,
            target.NameInGame,
            target.ProducerNotes,
            target.IsInventoriable,
            target.InventoryPointsDefaultValue,
            target.IsContainer,
            target.ContainerPointsDefaultValue,
            target.IsCapacityPointShareDividerEnabled,
            target.CapacityPointShareDividerDefaultValue,
            target.IsOpenable,
            target.IsOpenDefaultValue,
            target.IsLockable,
            target.IsLockedDefaultValue,
            target.IsActivatable,
            target.IsActiveDefaultValue,
            target.IsHidable,
            target.IsHiddenDefaultValue,
            target.IsQuantifiable,
            target.Quantity,
            target.QuantifiablePlacementDistributionMode,
            target.IsCompositeTarget,
            target.IsCompositeReversible,
            target.CompositePartRequirementMode,
            target.CompositeMinimumRequiredPartCount,
            ExpandCompositePartObjectIds(target.CompositeRequiredParts),
            BuildAvailableCompositePartOptions(compositePartSource, target),
            target.Description,
            target.ResolveDefaultImagePath(),
            target.ImageRotationDegrees,
            _projectFilePath ?? string.Empty,
            "objects",
            ImageVariants: target.ImageVariants,
            ImageVariantChooserScript: target.ImageVariantChooserScript,
            LockOperationRequirements: CloneLockOperationRequirements(target.LockOperationRequirements),
            AvailableLockKeyOptions: BuildAvailableLockKeyOptions(target),
            CompositeRequiredParts: CloneCompositePartRequirements(target.CompositeRequiredParts),
            ObjectNameSynonyms: SerializeObjectNameSynonyms(target.NameSynonyms),
            IsMovable: target.IsMovable,
            IsMovableDefaultValue: target.IsMovableDefaultValue,
            SpatialType: target.SpatialType,
            StackGroup: target.StackGroup,
            FootprintWidthCells: target.FootprintWidthCells,
            FootprintHeightCells: target.FootprintHeightCells,
            FootprintOrientation: target.FootprintOrientation,
            HeadingDirection: target.HeadingDirection,
            ObjectHeightUnits: target.ObjectHeightUnits,
            HeightInRoom: target.HeightInRoom,
            StackScaleStepOverride: stackScaleStepOverride,
            MinStackScaleOverride: minStackScaleOverride,
            MovementRestrictions: CloneMovementRestrictions(target.MovementRestrictions),
            ProjectRoomGridCellSize: _project.RoomDesignerGridCellSize,
            ProjectRoomCanvasWidth: _project.RoomImageCanvasWidth,
            ProjectRoomCanvasHeight: _project.RoomImageCanvasHeight);

        if (!_treeContextInteractionService.TryEditObjectBasicProperties(initial, out var updated))
        {
            return false;
        }

        if (!TryValidateScopedObjectName(siblings, target, updated.Name, out var normalizedName))
        {
            return false;
        }

        ApplyObjectBasicProperties(target, normalizedName, updated);
        configuredName = normalizedName;
        return true;
    }

    private static void ApplyObjectBasicProperties(
        GameObject target,
        string normalizedName,
        ObjectBasicPropertiesEditRequest updated)
    {
        target.Name = normalizedName;
        target.NameInGame = updated.NameInGame;
        target.NameSynonyms = ParseObjectNameSynonyms(updated.ObjectNameSynonyms);
        target.ProducerNotes = updated.ProducerNotes;
        target.IsInventoriable = updated.IsInventoriable;
        target.InventoryPointsDefaultValue = updated.InventoryPointsDefaultValue;
        target.IsContainer = updated.IsContainer;
        target.ContainerPointsDefaultValue = updated.ContainerPointsDefaultValue;
        target.IsCapacityPointShareDividerEnabled = updated.IsCapacityPointShareDividerEnabled;
        target.CapacityPointShareDividerDefaultValue = updated.CapacityPointShareDividerDefaultValue;
        target.IsOpenable = updated.IsOpenable;
        target.IsOpenDefaultValue = updated.IsOpenDefaultValue;
        target.IsLockable = updated.IsLockable;
        target.IsLockedDefaultValue = updated.IsLockedDefaultValue;
        target.IsActivatable = updated.IsActivatable;
        target.IsActiveDefaultValue = updated.IsActiveDefaultValue;
        target.IsHidable = updated.IsHidable;
        target.IsHiddenDefaultValue = updated.IsHiddenDefaultValue;
        target.IsQuantifiable = updated.IsQuantifiable;
        target.Quantity = updated.Quantity;
        target.QuantifiablePlacementDistributionMode = updated.QuantifiablePlacementDistributionMode;
        target.IsMovable = updated.IsMovable;
        target.IsMovableDefaultValue = updated.IsMovableDefaultValue;
        target.SpatialType = NormalizeSpatialType(updated.SpatialType);
        target.StackGroup = updated.StackGroup;
        target.FootprintWidthCells = updated.FootprintWidthCells;
        target.FootprintHeightCells = updated.FootprintHeightCells;
        target.FootprintOrientation = updated.FootprintOrientation;
        target.HeadingDirection = updated.HeadingDirection;
        target.ObjectHeightUnits = updated.ObjectHeightUnits;
        target.HeightInRoom = updated.HeightInRoom;
        target.StackScaleStepOverride = updated.StackScaleStepOverride;
        target.MinStackScaleOverride = updated.MinStackScaleOverride;
        target.MovementRestrictions = CloneMovementRestrictions(updated.MovementRestrictions);
        target.IsCompositeTarget = updated.IsCompositeTarget;

        if (target.IsCompositeTarget)
        {
            if (target.CompositeRecipeId == Guid.Empty)
            {
                target.CompositeRecipeId = Guid.NewGuid();
            }

            target.IsCompositeReversible = updated.IsCompositeReversible;
            target.CompositePartRequirementMode = updated.CompositePartRequirementMode;
            target.CompositeMinimumRequiredPartCount = updated.CompositeMinimumRequiredPartCount;
            target.CompositeRequiredParts = ResolveCompositePartRequirements(updated);
        }
        else
        {
            target.CompositeRecipeId = Guid.Empty;
            target.IsCompositeReversible = false;
            target.CompositeRequiredParts = new List<CompositePartRequirement>();
        }

        target.Description = updated.Description;
        target.ImageRotationDegrees = updated.ImageRotationDegrees;
        target.ImageVariants = NormalizeObjectImageVariantsForModel(updated.ImageVariants, updated.FullImagePath);
        target.ImageVariantChooserScript = updated.ImageVariantChooserScript?.Trim() ?? string.Empty;
        target.LockOperationRequirements = CloneLockOperationRequirements(updated.LockOperationRequirements);
    }

    private static List<ObjectImageVariant> NormalizeObjectImageVariantsForModel(IReadOnlyList<ObjectImageVariant>? variants, string fallbackFullImagePath, double fallbackImageScale = 1)
    {
        var normalizedScale = fallbackImageScale <= 0 ? 1 : fallbackImageScale;
        var normalized = (variants ?? Array.Empty<ObjectImageVariant>())
            .Where(static variant => variant is not null && !string.IsNullOrWhiteSpace(variant.VariantName))
            .Select(variant => new ObjectImageVariant
            {
                VariantName = variant.VariantName.Trim(),
                FullImagePath = variant.FullImagePath?.Trim() ?? string.Empty,
                ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                ImageScale = variant.ImageScale <= 0 ? normalizedScale : variant.ImageScale,
                IsDefault = variant.IsDefault
            })
            .GroupBy(static variant => variant.VariantName, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(fallbackFullImagePath))
        {
            normalized.Add(new ObjectImageVariant
            {
                VariantName = "default",
                FullImagePath = fallbackFullImagePath.Trim(),
                ImageLocalAlignmentRotationDegrees = 0,
                ImageLocalAlignmentOffsetX = 0,
                ImageLocalAlignmentOffsetY = 0,
                ImageScale = normalizedScale,
                IsDefault = true
            });
            return normalized;
        }

        var defaultIndex = normalized.FindIndex(static variant => variant.IsDefault);
        if (defaultIndex >= 0)
        {
            for (var index = 0; index < normalized.Count; index++)
            {
                normalized[index].IsDefault = index == defaultIndex;
            }
        }
        else if (normalized.Count == 1)
        {
            normalized[0].IsDefault = true;
        }

        return normalized;
    }

    private bool EditRoomChildObjectImageFromRoomDesigner(GameObject target)
    {
        var linkedDefinition = TryResolveDefinitionObject(target);
        var effectiveDefinitionOwnedSource = linkedDefinition ?? target;
        var (stackScaleStepOverride, minStackScaleOverride) = ResolveObjectStackScaleEditorDefaults(target);

        var initial = new ObjectBasicPropertiesEditRequest(
            target.Name,
            target.NameInGame,
            target.ProducerNotes,
            target.IsInventoriable,
            target.InventoryPointsDefaultValue,
            target.IsContainer,
            target.ContainerPointsDefaultValue,
            target.IsCapacityPointShareDividerEnabled,
            target.CapacityPointShareDividerDefaultValue,
            target.IsOpenable,
            target.IsOpenDefaultValue,
            target.IsLockable,
            target.IsLockedDefaultValue,
            target.IsActivatable,
            target.IsActiveDefaultValue,
            target.IsHidable,
            target.IsHiddenDefaultValue,
            target.IsQuantifiable,
            target.Quantity,
            target.QuantifiablePlacementDistributionMode,
            target.IsCompositeTarget,
            target.IsCompositeReversible,
            target.CompositePartRequirementMode,
            target.CompositeMinimumRequiredPartCount,
            ExpandCompositePartObjectIds(target.CompositeRequiredParts),
            BuildAvailableCompositePartOptions(Array.Empty<GameObject>(), target),
            target.Description,
            effectiveDefinitionOwnedSource.ResolveDefaultImagePath(),
            target.ImageRotationDegrees,
            _projectFilePath ?? string.Empty,
            "objects",
            ImageVariants: effectiveDefinitionOwnedSource.ImageVariants,
            ImageVariantChooserScript: effectiveDefinitionOwnedSource.ImageVariantChooserScript,
            LockOperationRequirements: CloneLockOperationRequirements(target.LockOperationRequirements),
            AvailableLockKeyOptions: BuildAvailableLockKeyOptions(target),
            CompositeRequiredParts: CloneCompositePartRequirements(target.CompositeRequiredParts),
            ObjectNameSynonyms: SerializeObjectNameSynonyms(target.NameSynonyms),
            IsMovable: target.IsMovable,
            IsMovableDefaultValue: target.IsMovableDefaultValue,
            SpatialType: target.SpatialType,
            StackGroup: target.StackGroup,
            FootprintWidthCells: target.FootprintWidthCells,
            FootprintHeightCells: target.FootprintHeightCells,
            FootprintOrientation: target.FootprintOrientation,
            HeadingDirection: target.HeadingDirection,
            ObjectHeightUnits: target.ObjectHeightUnits,
            HeightInRoom: target.HeightInRoom,
            StackScaleStepOverride: stackScaleStepOverride,
            MinStackScaleOverride: minStackScaleOverride,
            MovementRestrictions: CloneMovementRestrictions(target.MovementRestrictions),
            ProjectRoomGridCellSize: _project.RoomDesignerGridCellSize,
            ProjectRoomCanvasWidth: _project.RoomImageCanvasWidth,
            ProjectRoomCanvasHeight: _project.RoomImageCanvasHeight);

        if (linkedDefinition is not null)
        {
            initial = initial with
            {
                LinkedEditNotice = BuildLinkedInstanceEditNotice(target)
            };
        }


        if (!_treeContextInteractionService.TryEditObjectBasicProperties(initial, out var updated))
        {
            return false;
        }

        var hasChanges = !string.Equals(target.NameInGame, updated.NameInGame, StringComparison.Ordinal)
                         || !target.NameSynonyms.SequenceEqual(ParseObjectNameSynonyms(updated.ObjectNameSynonyms), StringComparer.OrdinalIgnoreCase)
                         || Math.Abs(target.ImageRotationDegrees - updated.ImageRotationDegrees) > 0.0001
                         || !AreEquivalentImageVariants(target.ImageVariants, updated.ImageVariants)
                         || !string.Equals(target.ImageVariantChooserScript, updated.ImageVariantChooserScript, StringComparison.Ordinal)
                         || target.SpatialType != NormalizeSpatialType(updated.SpatialType)
                         || target.StackGroup != updated.StackGroup
                         || !AreEquivalentMovementRestrictions(target.MovementRestrictions, updated.MovementRestrictions);

        if (!hasChanges)
        {
            return false;
        }

        target.NameInGame = updated.NameInGame;
        target.NameSynonyms = ParseObjectNameSynonyms(updated.ObjectNameSynonyms);
        target.ImageRotationDegrees = updated.ImageRotationDegrees;
        target.ImageVariants = NormalizeObjectImageVariantsForModel(updated.ImageVariants, updated.FullImagePath);
        target.ImageVariantChooserScript = updated.ImageVariantChooserScript?.Trim() ?? string.Empty;
        target.SpatialType = NormalizeSpatialType(updated.SpatialType);
        target.StackGroup = updated.StackGroup;
        target.MovementRestrictions = CloneMovementRestrictions(updated.MovementRestrictions);

        NotifyProjectEdited();
        ExportStatus = "Updated room object image properties.";
        return true;
    }

    private static List<CommandAction> CloneActions(IEnumerable<CommandAction> source)
    {
        var map = new Dictionary<Guid, Guid>();
        var clones = new List<CommandAction>();

        foreach (var action in source)
        {
            var containerPayload = ActionPayloadAccessors.GetContainerTransfer(action);
            var movePayload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(action);
            var rotatePayload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(action);
            var stackPayload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(action);
            var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(action);
            var compositeTargetObjectId = action.CompositeTargetObjectId;
            var compositeRecipeId = action.CompositeRecipeId;
            var compositeRequiredPartObjectIds = action.CompositeRequiredPartObjectIds.ToList();
            var compositeStrictPartCountEnforcement = action.CompositeStrictPartCountEnforcement;
            var compositeMinimumRequiredPartCount = action.CompositeMinimumRequiredPartCount;
            var compositeMatchMode = action.CompositeMatchMode;
            var compositeAmbiguityPolicy = action.CompositeAmbiguityPolicy;
            var compositePartConsumptionMode = action.CompositePartConsumptionMode;
            var compositeResolvedTargetOutputTemplate = action.CompositeResolvedTargetOutputTemplate;

            if (action.ActionType == CommandActionType.BuildCompositeByTarget)
            {
                var payload = ActionPayloadAccessors.GetCompositeByTargetPayload(action);
                compositeTargetObjectId = payload.CompositeTargetObjectId;
                compositeRecipeId = payload.CompositeRecipeId;
                compositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
                compositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
                compositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
                compositePartConsumptionMode = payload.CompositePartConsumptionMode;
            }
            else if (action.ActionType == CommandActionType.BuildCompositeByParts)
            {
                var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(action);
                compositeTargetObjectId = payload.CompositeTargetObjectId;
                compositeRecipeId = payload.CompositeRecipeId;
                compositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
                compositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
                compositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
                compositeMatchMode = payload.CompositeMatchMode;
                compositeAmbiguityPolicy = payload.CompositeAmbiguityPolicy;
                compositePartConsumptionMode = payload.CompositePartConsumptionMode;
                compositeResolvedTargetOutputTemplate = payload.CompositeResolvedTargetOutputTemplate;
            }
            else if (action.ActionType == CommandActionType.BreakCompositeItem)
            {
                var payload = ActionPayloadAccessors.GetBreakCompositePayload(action);
                compositeTargetObjectId = payload.CompositeTargetObjectId;
                compositeRecipeId = payload.CompositeRecipeId;
                compositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
                compositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
                compositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
                compositePartConsumptionMode = payload.CompositePartConsumptionMode;
            }

            var cloned = new CommandAction
            {
                Id = Guid.NewGuid(),
                Name = action.Name,
                ActionType = action.ActionType,
                FlagName = action.ActionType == CommandActionType.SetFlag
                    ? ActionPayloadAccessors.GetSetFlagName(action)
                    : ActionPayloadAccessors.GetCheckPropertyName(action),
                FlagValue = action.ActionType == CommandActionType.SetFlag
                    ? ActionPayloadAccessors.GetSetFlagValue(action)
                    : ActionPayloadAccessors.GetCheckExpectedValue(action),
                GamePropertyName = ActionPayloadAccessors.GetSetPropertyName(action),
                GamePropertyValue = ActionPayloadAccessors.GetSetPropertyValue(action),
                TargetContainerId = containerPayload.TargetContainerId,
                MoveDirectionToken = movePayload.DirectionToken,
                MoveDistanceInCells = movePayload.DistanceInCells,
                MoveAllowPartialMove = movePayload.AllowPartialMove,
                MoveAllowJumpOver = movePayload.AllowJumpOver,
                MoveVisualTransitionHint = movePayload.VisualTransitionHint,
                MoveTravelVisualizationMode = movePayload.TravelVisualizationMode,
                RotateMode = rotatePayload.Mode,
                RotateTurnDegrees = rotatePayload.TurnDegrees,
                RotateFacingDirectionToken = rotatePayload.FacingDirectionToken,
                RotateVisualTransitionHint = rotatePayload.VisualTransitionHint,
                StackVisualTransitionHint = stackPayload.VisualTransitionHint,
                SynonymTargetActionId = synonymTargetActionId,
                CompositeTargetObjectId = compositeTargetObjectId,
                CompositeRecipeId = compositeRecipeId,
                CompositeRequiredPartObjectIds = compositeRequiredPartObjectIds,
                CompositeStrictPartCountEnforcement = compositeStrictPartCountEnforcement,
                CompositeMinimumRequiredPartCount = compositeMinimumRequiredPartCount,
                CompositeMatchMode = compositeMatchMode,
                CompositeAmbiguityPolicy = compositeAmbiguityPolicy,
                CompositePartConsumptionMode = compositePartConsumptionMode,
                CompositeResolvedTargetOutputTemplate = compositeResolvedTargetOutputTemplate,
                NoVerbLinkage = action.NoVerbLinkage,
                Verbs = action.Verbs.ToList(),
                DirectionQualifierText = action.DirectionQualifierText,
                ChildCommandForwardingMode = action.ChildCommandForwardingMode,
                OutcomeMessageMap = action.OutcomeMessageMap,
                OutcomeSoundEffectsMap = CommandAction.CloneOutcomeSoundEffectsMap(action.OutcomeSoundEffectsMap),
                LinkedActions = action.LinkedActions.Select(reference => new LinkedActionReference
                {
                    ActionId = reference.ActionId,
                    Order = reference.Order,
                    RunWhen = reference.RunWhen
                }).ToList()
            };

            ActionPayloadAccessors.SetEchoMessage(cloned, ActionPayloadAccessors.GetEchoMessage(action));

            map[action.Id] = cloned.Id;
            clones.Add(cloned);
        }

        foreach (var cloned in clones)
        {
            if (cloned.SynonymTargetActionId.HasValue && map.TryGetValue(cloned.SynonymTargetActionId.Value, out var synonymMappedId))
            {
                cloned.SynonymTargetActionId = synonymMappedId;
            }

            foreach (var linked in cloned.LinkedActions)
            {
                if (map.TryGetValue(linked.ActionId, out var mappedId))
                {
                    linked.ActionId = mappedId;
                }
            }

            cloned.Payload = cloned.CreatePayloadSnapshot();
        }

        return clones;
    }

    private static string SerializeObjectNameSynonyms(IEnumerable<string>? synonyms)
    {
        return string.Join(", ", ParseObjectNameSynonyms(synonyms));
    }

    private static List<string> ParseObjectNameSynonyms(string? raw)
    {
        return ParseObjectNameSynonyms((raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> ParseObjectNameSynonyms(IEnumerable<string>? synonyms)
    {
        return (synonyms ?? Array.Empty<string>())
            .Select(static synonym => synonym?.Trim() ?? string.Empty)
            .Where(static synonym => !string.IsNullOrWhiteSpace(synonym))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<Guid> ExpandCompositePartObjectIds(IEnumerable<CompositePartRequirement> requirements)
    {
        return (requirements ?? Array.Empty<CompositePartRequirement>())
            .Where(static part => part.PartObjectId != Guid.Empty)
            .SelectMany(part => Enumerable.Repeat(part.PartObjectId, Math.Max(1, part.RequiredQuantity)))
            .ToList();
    }

    private static List<CompositePartRequirement> CloneCompositePartRequirements(IEnumerable<CompositePartRequirement>? requirements)
    {
        return (requirements ?? Array.Empty<CompositePartRequirement>())
            .Where(static part => part.PartObjectId != Guid.Empty)
            .Select(static part => new CompositePartRequirement
            {
                PartObjectId = part.PartObjectId,
                PartObjectName = part.PartObjectName,
                RequiredQuantity = part.RequiredQuantity < 1 ? 1 : part.RequiredQuantity,
                MatchKind = part.MatchKind,
                MatchValue = part.MatchValue,
                SatisfactionMode = part.SatisfactionMode,
                ConsumptionPolicy = part.ConsumptionPolicy,
                OptionalPart = part.OptionalPart,
                VariableRequirements = (part.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static entry => !string.IsNullOrWhiteSpace(entry.VariableName))
                    .Select(static entry => new LockParticipantVariableRequirement
                    {
                        VariableName = entry.VariableName.Trim(),
                        Operator = entry.Operator,
                        ExpectedValue = entry.ExpectedValue,
                        QuantityEvaluationMode = entry.QuantityEvaluationMode
                    })
                    .ToList()
            })
            .ToList();
    }

    private static List<CompositePartRequirement> ResolveCompositePartRequirements(ObjectBasicPropertiesEditRequest request)
    {
        return CloneCompositePartRequirements(request.CompositeRequiredParts);
    }

    private static bool AreEquivalentCompositePartRequirements(
        IReadOnlyList<CompositePartRequirement>? left,
        IReadOnlyList<CompositePartRequirement>? right)
    {
        var normalizedLeft = CloneCompositePartRequirements(left)
            .OrderBy(static part => part.PartObjectId)
            .ThenBy(static part => part.RequiredQuantity)
            .ThenBy(static part => part.MatchKind)
            .ThenBy(static part => part.MatchValue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static part => part.SatisfactionMode)
            .ThenBy(static part => part.ConsumptionPolicy)
            .ThenBy(static part => part.OptionalPart)
            .ToList();
        var normalizedRight = CloneCompositePartRequirements(right)
            .OrderBy(static part => part.PartObjectId)
            .ThenBy(static part => part.RequiredQuantity)
            .ThenBy(static part => part.MatchKind)
            .ThenBy(static part => part.MatchValue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static part => part.SatisfactionMode)
            .ThenBy(static part => part.ConsumptionPolicy)
            .ThenBy(static part => part.OptionalPart)
            .ToList();

        if (normalizedLeft.Count != normalizedRight.Count)
        {
            return false;
        }

        for (var index = 0; index < normalizedLeft.Count; index++)
        {
            var lhs = normalizedLeft[index];
            var rhs = normalizedRight[index];
            if (lhs.PartObjectId != rhs.PartObjectId
                || lhs.RequiredQuantity != rhs.RequiredQuantity
                || lhs.MatchKind != rhs.MatchKind
                || !string.Equals(lhs.MatchValue, rhs.MatchValue, StringComparison.OrdinalIgnoreCase)
                || lhs.SatisfactionMode != rhs.SatisfactionMode
                || lhs.ConsumptionPolicy != rhs.ConsumptionPolicy
                || lhs.OptionalPart != rhs.OptionalPart
                || !AreEquivalentParticipantVariableRequirements(lhs.VariableRequirements, rhs.VariableRequirements))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalentParticipantVariableRequirements(
        IReadOnlyList<LockParticipantVariableRequirement>? left,
        IReadOnlyList<LockParticipantVariableRequirement>? right)
    {
        var normalizedLeft = (left ?? Array.Empty<LockParticipantVariableRequirement>())
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.VariableName))
            .Select(static entry => new
            {
                VariableName = entry.VariableName.Trim(),
                entry.Operator,
                ExpectedValue = entry.ExpectedValue?.Trim() ?? string.Empty,
                entry.QuantityEvaluationMode
            })
            .OrderBy(static entry => entry.VariableName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Operator)
            .ThenBy(static entry => entry.ExpectedValue, StringComparer.Ordinal)
            .ThenBy(static entry => entry.QuantityEvaluationMode)
            .ToList();

        var normalizedRight = (right ?? Array.Empty<LockParticipantVariableRequirement>())
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.VariableName))
            .Select(static entry => new
            {
                VariableName = entry.VariableName.Trim(),
                entry.Operator,
                ExpectedValue = entry.ExpectedValue?.Trim() ?? string.Empty,
                entry.QuantityEvaluationMode
            })
            .OrderBy(static entry => entry.VariableName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Operator)
            .ThenBy(static entry => entry.ExpectedValue, StringComparer.Ordinal)
            .ThenBy(static entry => entry.QuantityEvaluationMode)
            .ToList();

        if (normalizedLeft.Count != normalizedRight.Count)
        {
            return false;
        }

        for (var index = 0; index < normalizedLeft.Count; index++)
        {
            var lhs = normalizedLeft[index];
            var rhs = normalizedRight[index];
            if (!string.Equals(lhs.VariableName, rhs.VariableName, StringComparison.OrdinalIgnoreCase)
                || lhs.Operator != rhs.Operator
                || !string.Equals(lhs.ExpectedValue, rhs.ExpectedValue, StringComparison.Ordinal)
                || lhs.QuantityEvaluationMode != rhs.QuantityEvaluationMode)
            {
                return false;
            }
        }

        return true;
    }

    private static List<CompositePartRequirement> CollapseCompositePartObjectIds(IEnumerable<Guid> partObjectIds)
    {
        return (partObjectIds ?? Array.Empty<Guid>())
            .Where(static id => id != Guid.Empty)
            .GroupBy(id => id)
            .Select(group => new CompositePartRequirement
            {
                PartObjectId = group.Key,
                RequiredQuantity = group.Count(),
                MatchKind = ProcedureParticipantMatchKind.ObjectId,
                MatchValue = group.Key.ToString("D"),
                SatisfactionMode = ProcedureParticipantSatisfactionMode.PossessionRequired,
                ConsumptionPolicy = ProcedureParticipantConsumptionPolicy.None,
                OptionalPart = false
            })
            .ToList();
    }

    private bool RemoveInteractiveObject(GameObjectNodeViewModel objectNode)
    {
        var ownerCollection = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.GameObject.ContainedObjects
            : objectNode.ParentObjectsNode.GameObjects;

        var removedModel = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.GameObject.RemoveChildScope(objectNode.GameObject)
            : objectNode.ParentObjectsNode.ScopeNode.RemoveChildScope(objectNode.GameObject);
        var removedNode = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.Children.Remove(objectNode)
            : objectNode.ParentObjectsNode.Children.Remove(objectNode);
        HierarchyNodeViewModel fallbackNode = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode
            : objectNode.ParentObjectsNode;
        return FinalizeGameObjectRemoval(
            removedModel,
            removedNode,
            ownerCollection,
            fallbackNode,
            objectNode.ParentObjectNode is null && objectNode.ParentObjectsNode is RoomGameObjectsNodeViewModel roomObjectsNode
                ? () => RefreshOpenRoomEditorObjectLists(roomObjectsNode.RoomNode.Room)
                : null);
    }

    private bool RemoveInteractiveObject(GlobalObjectNodeViewModel objectNode)
    {
        var ownerCollection = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.GameObject.ContainedObjects
            : objectNode.ParentGlobalObjectsNode.GameObjects;

        var removedObjectScopeName = objectNode.GameObject.Name?.Trim() ?? string.Empty;

        var removedModel = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.GameObject.RemoveChildScope(objectNode.GameObject)
            : objectNode.ParentGlobalObjectsNode.ScopeNode.RemoveChildScope(objectNode.GameObject);
        var removedNode = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.Children.Remove(objectNode)
            : objectNode.ParentGlobalObjectsNode.Children.Remove(objectNode);
        HierarchyNodeViewModel fallbackNode = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode
            : objectNode.ParentGlobalObjectsNode;
        return FinalizeGameObjectRemoval(
            removedModel,
            removedNode,
            ownerCollection,
            fallbackNode,
            () =>
            {
                if (string.Equals(_project.PlayerCharacterObjectName?.Trim(), removedObjectScopeName, StringComparison.OrdinalIgnoreCase))
                {
                    _project.PlayerCharacterObjectName = string.Empty;
                }

                ApplyPlayerCharacterVariableContract();
            });
    }

    private bool FinalizeGameObjectRemoval(
        bool removedModel,
        bool removedNode,
        List<GameObject> ownerCollection,
        HierarchyNodeViewModel fallbackNode,
        Action? postRemovalAction,
        string successStatus = "Removed game object.")
    {
        if (!removedModel && !removedNode)
        {
            return false;
        }

        RefreshScopedNameIndex(ownerCollection, static obj => obj.Name);
        postRemovalAction?.Invoke();

        NotifyProjectEdited();
        SelectedGameObject = null;
        SelectedNode = fallbackNode;
        ExportStatus = successStatus;
        return true;
    }

    private bool RemoveInteractiveObject(TemplateGameObjectNodeViewModel objectNode)
    {
        var ownerCollection = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.GameObject.ContainedObjects
            : objectNode.ParentTemplatesNode.CatalogObjects;

        var removedModel = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.GameObject.RemoveChildScope(objectNode.GameObject)
            : objectNode.ParentTemplatesNode.CatalogParentScope is ObjectTemplatesScopeNode or BaseObjectsScopeNode
                ? objectNode.ParentTemplatesNode.CatalogParentScope.RemoveChildScope(objectNode.GameObject)
                : objectNode.ParentTemplatesNode.CatalogObjects.Remove(objectNode.GameObject);

        if (removedModel && objectNode.ParentObjectNode is null)
        {
            objectNode.GameObject.ParentScope = null;
        }
        var removedNode = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode.Children.Remove(objectNode)
            : objectNode.ParentTemplatesNode.Children.Remove(objectNode);
        HierarchyNodeViewModel fallbackNode = objectNode.ParentObjectNode is not null
            ? objectNode.ParentObjectNode
            : objectNode.ParentTemplatesNode;
        var status = objectNode.ParentTemplatesNode.IsBaseCatalog
            ? "Removed base object."
            : "Removed object template.";
        return FinalizeGameObjectRemoval(
            removedModel,
            removedNode,
            ownerCollection,
            fallbackNode,
            postRemovalAction: null,
            successStatus: status);
    }

    private static RoomGameObjectsNodeViewModel CreateObjectsNode(RoomNodeViewModel roomNode)
    {
        var objectsNode = new RoomGameObjectsNodeViewModel(roomNode);
        foreach (var interactiveObject in roomNode.Room.GameObjects)
        {
            var objectNode = CreateScopedObjectNode(interactiveObject, objectsNode);
            objectsNode.Children.Add(objectNode);
        }

        return objectsNode;
    }

    private static PlanetGameObjectsNodeViewModel CreateObjectsNode(PlanetNodeViewModel planetNode)
    {
        var objectsNode = new PlanetGameObjectsNodeViewModel(planetNode);
        foreach (var interactiveObject in planetNode.Planet.GameObjects)
        {
            var objectNode = CreateScopedObjectNode(interactiveObject, objectsNode);
            objectsNode.Children.Add(objectNode);
        }

        return objectsNode;
    }

    private static CountryGameObjectsNodeViewModel CreateObjectsNode(CountryNodeViewModel countryNode)
    {
        var objectsNode = new CountryGameObjectsNodeViewModel(countryNode);
        foreach (var interactiveObject in countryNode.Country.GameObjects)
        {
            var objectNode = CreateScopedObjectNode(interactiveObject, objectsNode);
            objectsNode.Children.Add(objectNode);
        }

        return objectsNode;
    }

    private static AreaGameObjectsNodeViewModel CreateObjectsNode(AreaNodeViewModel areaNode)
    {
        var objectsNode = new AreaGameObjectsNodeViewModel(areaNode);
        foreach (var interactiveObject in areaNode.Area.GameObjects)
        {
            var objectNode = CreateScopedObjectNode(interactiveObject, objectsNode);
            objectsNode.Children.Add(objectNode);
        }

        return objectsNode;
    }

    private static List<GlobalObjectNodeViewModel> CreateGlobalGameObjectNodes(GlobalObjectsNodeViewModel globalObjectsNode)
    {
        var objectNodes = new List<GlobalObjectNodeViewModel>();
        foreach (var interactiveObject in globalObjectsNode.GameObjects)
        {
            var objectNode = CreateGlobalGameObjectNode(interactiveObject, globalObjectsNode);
            objectNodes.Add(objectNode);
        }

        return objectNodes;
    }

    private static List<TemplateGameObjectNodeViewModel> CreateTemplateObjectNodes(ObjectTemplatesNodeViewModel templatesNode)
    {
        var objectNodes = new List<TemplateGameObjectNodeViewModel>();
        foreach (var interactiveObject in templatesNode.CatalogObjects)
        {
            var objectNode = CreateTemplateObjectNode(interactiveObject, templatesNode);
            objectNodes.Add(objectNode);
        }

        return objectNodes;
    }

    private static List<TemplateRoomNodeViewModel> CreateTemplateRoomNodes(RoomTemplatesNodeViewModel templatesNode)
    {
        var roomNodes = new List<TemplateRoomNodeViewModel>();
        foreach (var room in templatesNode.RoomTemplates)
        {
            roomNodes.Add(CreateTemplateRoomNode(room, templatesNode));
        }

        return roomNodes;
    }

    private static PhaseBooksNodeViewModel CreatePhaseBooksNode(ProjectRootNodeViewModel projectRootNode)
    {
        var node = new PhaseBooksNodeViewModel(projectRootNode.Project, projectRootNode);
        foreach (var phaseBook in projectRootNode.Project.PhaseBooks)
        {
            node.Children.Add(CreatePhaseNodeViewModel(phaseBook, node, projectRootNode.Project.StartingPhasePageId));
        }

        return node;
    }

    private static PhaseNodeViewModel CreatePhaseNodeViewModel(PhaseNode phaseNode, HierarchyNodeViewModel parent, Guid? startingPhasePageId)
    {
        var node = new PhaseNodeViewModel(phaseNode, parent, phaseNode.Tier == PhaseTier.Page && phaseNode.Id == startingPhasePageId);
        foreach (var child in phaseNode.Children)
        {
            node.Children.Add(CreatePhaseNodeViewModel(child, node, startingPhasePageId));
        }

        return node;
    }

    private static TemplateRoomNodeViewModel CreateTemplateRoomNode(Room room, RoomTemplatesNodeViewModel templatesNode)
    {
        var roomNode = new TemplateRoomNodeViewModel(room, templatesNode);
        roomNode.Children.Add(CreateRoomSettingsNode(roomNode));
        roomNode.Children.Add(CreateVariablesNode(roomNode, room.Variables, PropertyResolutionScope.Room));
        roomNode.Children.Add(CreateActionsNode(roomNode, PropertyResolutionScope.Room, room.AvailableActions));
        roomNode.Children.Add(CreateVerbsNode(roomNode, PropertyResolutionScope.Room, room.AdditionalVerbs));
        roomNode.Children.Add(CreateDirectionalsNode(roomNode, PropertyResolutionScope.Room, room.AdditionalDirectionals));
        roomNode.Children.Add(CreateSoundEffectsNode(roomNode, PropertyResolutionScope.Room, room.SoundEffectLibraryEntries));
        roomNode.Children.Add(CreateEventSubscriptionsNode(roomNode, PropertyResolutionScope.Room, room.EventSubscriptions));
        roomNode.Children.Add(CreateTimerDefinitionsNode(roomNode, PropertyResolutionScope.Room, room.TimerDefinitions));

        roomNode.Children.Add(CreateTemplateRoomObjectsNode(roomNode));
        ApplyHideEmptyConfiguration(roomNode);

        return roomNode;
    }

    private static ObjectTemplatesNodeViewModel CreateTemplateRoomObjectsNode(TemplateRoomNodeViewModel templateRoomNode)
    {
        var objectsNode = new ObjectTemplatesNodeViewModel(
            templateRoomNode.ParentTemplatesNode.Project,
            templateRoomNode,
            "Game Objects",
            templateRoomNode.Room.GameObjects,
            new List<string>(),
            isBaseCatalog: false,
            catalogParentScope: templateRoomNode.Room);

        foreach (var interactiveObject in templateRoomNode.Room.GameObjects)
        {
            var objectNode = CreateTemplateObjectNode(interactiveObject, objectsNode);
            objectsNode.Children.Add(objectNode);
        }

        return objectsNode;
    }

    private static GameObjectNodeViewModel CreateScopedObjectNode(GameObject interactiveObject, GameObjectsNodeViewModel parent)
    {
        var objectNode = new GameObjectNodeViewModel(interactiveObject, parent);
        PopulateObjectNodeCommonChildren(objectNode, interactiveObject);
        var childrenGroup = objectNode.Children.OfType<ChildrenGroupNodeViewModel>().First();
        foreach (var childObject in interactiveObject.ContainedObjects)
        {
            childrenGroup.Children.Add(CreateNestedRoomObjectNode(childObject, objectNode));
        }

        ApplyHideEmptyConfiguration(objectNode);

        return objectNode;
    }

    private static GameObjectNodeViewModel CreateNestedRoomObjectNode(GameObject interactiveObject, GameObjectNodeViewModel parent)
    {
        var objectNode = new GameObjectNodeViewModel(interactiveObject, parent);
        PopulateObjectNodeCommonChildren(objectNode, interactiveObject);
        var childrenGroup = objectNode.Children.OfType<ChildrenGroupNodeViewModel>().First();
        foreach (var childObject in interactiveObject.ContainedObjects)
        {
            childrenGroup.Children.Add(CreateNestedRoomObjectNode(childObject, objectNode));
        }

        ApplyHideEmptyConfiguration(objectNode);

        return objectNode;
    }

    private static GlobalObjectNodeViewModel CreateGlobalGameObjectNode(GameObject interactiveObject, GlobalObjectsNodeViewModel parent)
    {
        var objectNode = new GlobalObjectNodeViewModel(interactiveObject, parent);
        PopulateGlobalObjectNodeCommonChildren(objectNode, interactiveObject);
        var childrenGroup = objectNode.Children.OfType<ChildrenGroupNodeViewModel>().First();
        foreach (var childObject in interactiveObject.ContainedObjects)
        {
            childrenGroup.Children.Add(CreateNestedGlobalGameObjectNode(childObject, objectNode));
        }

        ApplyHideEmptyConfiguration(objectNode);

        return objectNode;
    }

    private static GlobalObjectNodeViewModel CreateNestedGlobalGameObjectNode(GameObject interactiveObject, GlobalObjectNodeViewModel parent)
    {
        var objectNode = new GlobalObjectNodeViewModel(interactiveObject, parent);
        PopulateGlobalObjectNodeCommonChildren(objectNode, interactiveObject);
        var childrenGroup = objectNode.Children.OfType<ChildrenGroupNodeViewModel>().First();
        foreach (var childObject in interactiveObject.ContainedObjects)
        {
            childrenGroup.Children.Add(CreateNestedGlobalGameObjectNode(childObject, objectNode));
        }

        ApplyHideEmptyConfiguration(objectNode);

        return objectNode;
    }

    private static TemplateGameObjectNodeViewModel CreateTemplateObjectNode(GameObject interactiveObject, ObjectTemplatesNodeViewModel parent)
    {
        var objectNode = new TemplateGameObjectNodeViewModel(interactiveObject, parent);
        PopulateTemplateObjectNodeCommonChildren(objectNode, interactiveObject);
        var childrenGroup = objectNode.Children.OfType<ChildrenGroupNodeViewModel>().First();
        foreach (var childObject in interactiveObject.ContainedObjects)
        {
            childrenGroup.Children.Add(CreateNestedTemplateObjectNode(childObject, objectNode));
        }

        ApplyHideEmptyConfiguration(objectNode);

        return objectNode;
    }

    private static TemplateGameObjectNodeViewModel CreateNestedTemplateObjectNode(GameObject interactiveObject, TemplateGameObjectNodeViewModel parent)
    {
        var objectNode = new TemplateGameObjectNodeViewModel(interactiveObject, parent);
        PopulateTemplateObjectNodeCommonChildren(objectNode, interactiveObject);
        var childrenGroup = objectNode.Children.OfType<ChildrenGroupNodeViewModel>().First();
        foreach (var childObject in interactiveObject.ContainedObjects)
        {
            childrenGroup.Children.Add(CreateNestedTemplateObjectNode(childObject, objectNode));
        }

        ApplyHideEmptyConfiguration(objectNode);

        return objectNode;
    }

    private static void PopulateObjectNodeCommonChildren(GameObjectNodeViewModel objectNode, GameObject interactiveObject)
    {
        var childrenGroup = new ChildrenGroupNodeViewModel(objectNode);
        objectNode.Children.Add(CreateObjectSettingsNode(objectNode));
        objectNode.Children.Add(CreateVariablesNode(objectNode, interactiveObject.Variables, PropertyResolutionScope.Object));
        objectNode.Children.Add(CreateActionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AvailableActions));
        objectNode.Children.Add(CreateVerbsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AdditionalVerbs));
        objectNode.Children.Add(CreateDirectionalsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AdditionalDirectionals));
        objectNode.Children.Add(CreateSoundEffectsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.SoundEffectLibraryEntries));
        objectNode.Children.Add(CreateEventSubscriptionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.EventSubscriptions));
        objectNode.Children.Add(CreateTimerDefinitionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.TimerDefinitions));
        objectNode.Children.Add(CreateProceduresNode(objectNode, PropertyResolutionScope.Object, interactiveObject.ProcedureIds));
        objectNode.Children.Add(childrenGroup);
    }

    private static void PopulateGlobalObjectNodeCommonChildren(GlobalObjectNodeViewModel objectNode, GameObject interactiveObject)
    {
        var childrenGroup = new ChildrenGroupNodeViewModel(objectNode);
        objectNode.Children.Add(CreateObjectSettingsNode(objectNode));
        objectNode.Children.Add(CreateVariablesNode(objectNode, interactiveObject.Variables, PropertyResolutionScope.Object));
        objectNode.Children.Add(CreateActionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AvailableActions));
        objectNode.Children.Add(CreateVerbsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AdditionalVerbs));
        objectNode.Children.Add(CreateDirectionalsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AdditionalDirectionals));
        objectNode.Children.Add(CreateSoundEffectsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.SoundEffectLibraryEntries));
        objectNode.Children.Add(CreateEventSubscriptionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.EventSubscriptions));
        objectNode.Children.Add(CreateTimerDefinitionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.TimerDefinitions));
        objectNode.Children.Add(CreateProceduresNode(objectNode, PropertyResolutionScope.Object, interactiveObject.ProcedureIds));
        objectNode.Children.Add(childrenGroup);
    }

    private static void PopulateTemplateObjectNodeCommonChildren(TemplateGameObjectNodeViewModel objectNode, GameObject interactiveObject)
    {
        var childrenGroup = new ChildrenGroupNodeViewModel(objectNode);
        objectNode.Children.Add(CreateObjectSettingsNode(objectNode));
        objectNode.Children.Add(CreateVariablesNode(objectNode, interactiveObject.Variables, PropertyResolutionScope.Object));
        objectNode.Children.Add(CreateActionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AvailableActions));
        objectNode.Children.Add(CreateVerbsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AdditionalVerbs));
        objectNode.Children.Add(CreateDirectionalsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.AdditionalDirectionals));
        objectNode.Children.Add(CreateSoundEffectsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.SoundEffectLibraryEntries));
        objectNode.Children.Add(CreateEventSubscriptionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.EventSubscriptions));
        objectNode.Children.Add(CreateTimerDefinitionsNode(objectNode, PropertyResolutionScope.Object, interactiveObject.TimerDefinitions));
        objectNode.Children.Add(CreateProceduresNode(objectNode, PropertyResolutionScope.Object, interactiveObject.ProcedureIds));
        objectNode.Children.Add(childrenGroup);
    }

    private static void ApplyHideEmptyConfiguration(HierarchyNodeViewModel ownerNode)
    {
        if (!IsHideEmptyConfigurationEnabled(ownerNode))
        {
            return;
        }

        for (var index = ownerNode.Children.Count - 1; index >= 0; index--)
        {
            var childNode = ownerNode.Children[index];
            if (ShouldSuppressEmptyConfigurationChild(ownerNode, childNode))
            {
                ownerNode.Children.RemoveAt(index);
            }
        }
    }

    private static bool IsHideEmptyConfigurationEnabled(HierarchyNodeViewModel ownerNode)
    {
        return ownerNode switch
        {
            ProjectRootNodeViewModel projectNode => projectNode.Project.HideEmptyConfiguration,
            PlanetNodeViewModel planetNode => planetNode.Planet.HideEmptyConfiguration,
            CountryNodeViewModel countryNode => countryNode.Country.HideEmptyConfiguration,
            AreaNodeViewModel areaNode => areaNode.Area.HideEmptyConfiguration,
            RoomNodeViewModel roomNode => roomNode.Room.HideEmptyConfiguration,
            TemplateRoomNodeViewModel templateRoomNode => templateRoomNode.Room.HideEmptyConfiguration,
            GameObjectNodeViewModel objectNode => objectNode.GameObject.HideEmptyConfiguration,
            GlobalObjectNodeViewModel objectNode => objectNode.GameObject.HideEmptyConfiguration,
            TemplateGameObjectNodeViewModel objectNode => objectNode.GameObject.HideEmptyConfiguration,
            _ => false
        };
    }

    private static bool ShouldSuppressEmptyConfigurationChild(HierarchyNodeViewModel ownerNode, HierarchyNodeViewModel childNode)
    {
        if (IsAlwaysVisibleConfigurationChild(ownerNode, childNode))
        {
            return false;
        }

        if (!IsSuppressibleConfigurationGroupingNode(childNode))
        {
            return false;
        }

        return IsConfigurationGroupingNodeEmpty(childNode);
    }

    private static bool IsAlwaysVisibleConfigurationChild(HierarchyNodeViewModel ownerNode, HierarchyNodeViewModel childNode)
    {
        if (childNode is GlobalSettingsNodeViewModel
            or PhaseBooksNodeViewModel
            or PlanetSettingsNodeViewModel
            or CountrySettingsNodeViewModel
            or AreaSettingsNodeViewModel
            or RoomSettingsNodeViewModel
            or GameObjectSettingsNodeViewModel)
        {
            return true;
        }

        if (childNode is ChildrenGroupNodeViewModel)
        {
            return ownerNode is PlanetNodeViewModel
                or CountryNodeViewModel
                or AreaNodeViewModel;
        }

        return false;
    }

    private static bool IsSuppressibleConfigurationGroupingNode(HierarchyNodeViewModel childNode)
    {
        return childNode is GamePropertiesContainerNodeViewModel
            or ScopedActionsNodeViewModel
            or ScopedVerbsNodeViewModel
            or ScopedDirectionalsNodeViewModel
            or ScopedSoundEffectsNodeViewModel
            or ScopedEventSubscriptionsNodeViewModel
            or ScopedTimerDefinitionsNodeViewModel
            or ScopedProceduresNodeViewModel
            or GameObjectsNodeViewModel
            or ObjectTemplatesNodeViewModel
            or RoomTemplatesNodeViewModel
            or RoomTraversalLegsNodeViewModel
            or ChildrenGroupNodeViewModel;
    }

    private static bool IsConfigurationGroupingNodeEmpty(HierarchyNodeViewModel childNode)
    {
        return childNode switch
        {
            GamePropertiesContainerNodeViewModel variablesNode => variablesNode.Children.Count == 0,
            ScopedActionsNodeViewModel actionsNode => actionsNode.Actions.Count == 0,
            ScopedVerbsNodeViewModel verbsNode => verbsNode.Verbs.Count == 0,
            ScopedDirectionalsNodeViewModel directionalsNode => directionalsNode.Directionals.Count == 0,
            ScopedSoundEffectsNodeViewModel soundEffectsNode => soundEffectsNode.SoundEffects.Count == 0,
            ScopedEventSubscriptionsNodeViewModel eventSubscriptionsNode => eventSubscriptionsNode.Subscriptions.Count == 0,
            ScopedTimerDefinitionsNodeViewModel timerDefinitionsNode => timerDefinitionsNode.TimerDefinitions.Count == 0,
            ScopedProceduresNodeViewModel proceduresNode => proceduresNode.ProcedureIds.Count == 0,
            GameObjectsNodeViewModel gameObjectsNode => gameObjectsNode.GameObjects.Count == 0,
            ObjectTemplatesNodeViewModel objectTemplatesNode => objectTemplatesNode.CatalogObjects.Count == 0,
            RoomTemplatesNodeViewModel roomTemplatesNode => roomTemplatesNode.RoomTemplates.Count == 0,
            RoomTraversalLegsNodeViewModel traversalLegsNode => traversalLegsNode.Children.Count == 0,
            ChildrenGroupNodeViewModel childrenGroupNode => childrenGroupNode.Children.Count == 0,
            _ => false
        };
    }

    private static IEnumerable<GameObject> EnumerateGameObjectsRecursive(IEnumerable<GameObject> rootObjects)
    {
        foreach (var gameObject in rootObjects)
        {
            yield return gameObject;

            foreach (var nested in EnumerateGameObjectsRecursive(gameObject.ContainedObjects))
            {
                yield return nested;
            }
        }
    }

    private static GamePropertiesContainerNodeViewModel CreateVariablesNode(HierarchyNodeViewModel parent, List<GamePropertyDefinition> source, PropertyResolutionScope scope)
    {
        var variablesNode = new GamePropertiesContainerNodeViewModel(scope, parent);
        foreach (var variable in source)
        {
            variablesNode.Children.Add(new GamePropertyNodeViewModel(variable, scope, variablesNode));
        }

        return variablesNode;
    }

    private static ScopedActionsNodeViewModel CreateActionsNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<CommandAction> source)
    {
        var actionsNode = new ScopedActionsNodeViewModel(scope, source, parent);
        RefreshScopedActionNodes(actionsNode);
        return actionsNode;
    }

    private static ScopedVerbsNodeViewModel CreateVerbsNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<string> source)
    {
        var verbsNode = new ScopedVerbsNodeViewModel(scope, source, parent);
        RefreshScopedVerbNodes(verbsNode);
        return verbsNode;
    }

    private static ScopedDirectionalsNodeViewModel CreateDirectionalsNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<string> source)
    {
        var directionalsNode = new ScopedDirectionalsNodeViewModel(scope, source, parent);
        RefreshScopedDirectionalNodes(directionalsNode);
        directionalsNode.IsExpanded = false;
        return directionalsNode;
    }

    private static ScopedSoundEffectsNodeViewModel CreateSoundEffectsNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<SoundEffectLibraryEntry> source)
    {
        var soundEffectsNode = new ScopedSoundEffectsNodeViewModel(scope, source, parent);
        RefreshScopedSoundEffectNodes(soundEffectsNode);
        soundEffectsNode.IsExpanded = false;
        return soundEffectsNode;
    }

    private static ScopedEventSubscriptionsNodeViewModel CreateEventSubscriptionsNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<EventSubscriptionDefinition> source)
    {
        var eventSubscriptionsNode = new ScopedEventSubscriptionsNodeViewModel(scope, source, parent);
        RefreshScopedEventSubscriptionNodes(eventSubscriptionsNode);
        eventSubscriptionsNode.IsExpanded = false;
        return eventSubscriptionsNode;
    }

    private static ScopedTimerDefinitionsNodeViewModel CreateTimerDefinitionsNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<RuntimeTimerDefinitionDto> source)
    {
        var timerDefinitionsNode = new ScopedTimerDefinitionsNodeViewModel(scope, source, parent);
        RefreshScopedTimerDefinitionNodes(timerDefinitionsNode);
        timerDefinitionsNode.IsExpanded = false;
        return timerDefinitionsNode;
    }

    private static ScopedProceduresNodeViewModel CreateProceduresNode(HierarchyNodeViewModel parent, PropertyResolutionScope scope, IList<Guid> source)
    {
        var proceduresNode = new ScopedProceduresNodeViewModel(scope, source, parent)
        {
            IsExpanded = false
        };

        return proceduresNode;
    }

    private static void EnsureScopedProceduresNode(HierarchyNodeViewModel ownerNode, IList<Guid> procedureIds, PropertyResolutionScope scope)
    {
        var existingNode = ownerNode.Children.OfType<ScopedProceduresNodeViewModel>()
            .FirstOrDefault(node => node.Scope == scope);
        var shouldShow = !IsHideEmptyConfigurationEnabled(ownerNode) || procedureIds.Count > 0;

        if (!shouldShow)
        {
            if (existingNode is not null)
            {
                ownerNode.Children.Remove(existingNode);
            }

            return;
        }

        if (existingNode is not null)
        {
            return;
        }

        var newNode = CreateProceduresNode(ownerNode, scope, procedureIds);
        var childrenGroup = ownerNode.Children.OfType<ChildrenGroupNodeViewModel>().FirstOrDefault();
        if (childrenGroup is null)
        {
            ownerNode.Children.Add(newNode);
            return;
        }

        var insertionIndex = ownerNode.Children.IndexOf(childrenGroup);
        if (insertionIndex < 0)
        {
            ownerNode.Children.Add(newNode);
            return;
        }

        ownerNode.Children.Insert(insertionIndex, newNode);
    }

    private static GlobalSettingsNodeViewModel CreateGlobalSettingsNode(ProjectRootNodeViewModel projectNode)
    {
        return new GlobalSettingsNodeViewModel(projectNode);
    }

    private static PlanetSettingsNodeViewModel CreatePlanetSettingsNode(PlanetNodeViewModel planetNode)
    {
        return new PlanetSettingsNodeViewModel(planetNode);
    }

    private static CountrySettingsNodeViewModel CreateCountrySettingsNode(CountryNodeViewModel countryNode)
    {
        return new CountrySettingsNodeViewModel(countryNode);
    }

    private static RoomSettingsNodeViewModel CreateRoomSettingsNode(RoomNodeViewModel roomNode)
    {
        return new RoomSettingsNodeViewModel(roomNode);
    }

    private static RoomSettingsNodeViewModel CreateRoomSettingsNode(TemplateRoomNodeViewModel templateRoomNode)
    {
        return new RoomSettingsNodeViewModel(templateRoomNode);
    }

    private static AreaSettingsNodeViewModel CreateAreaSettingsNode(AreaNodeViewModel areaNode)
    {
        return new AreaSettingsNodeViewModel(areaNode);
    }

    private static GameObjectSettingsNodeViewModel CreateObjectSettingsNode(GameObjectNodeViewModel objectNode)
    {
        return new GameObjectSettingsNodeViewModel(objectNode);
    }

    private static GameObjectSettingsNodeViewModel CreateObjectSettingsNode(GlobalObjectNodeViewModel objectNode)
    {
        return new GameObjectSettingsNodeViewModel(objectNode);
    }

    private static GameObjectSettingsNodeViewModel CreateObjectSettingsNode(TemplateGameObjectNodeViewModel objectNode)
    {
        return new GameObjectSettingsNodeViewModel(objectNode);
    }

    private static void RefreshScopedActionNodes(ScopedActionsNodeViewModel container)
    {
        var wasExpanded = container.IsExpanded;
        container.Children.Clear();
        foreach (var action in container.Actions)
        {
            container.Children.Add(new ScopedActionEntryNodeViewModel(action, container));
        }

        container.IsExpanded = wasExpanded;
    }

    private void RefreshScopedActionNodesForSelection()
    {
        var actionsNode = ResolveScopedActionsNode(SelectedNode);
        if (actionsNode is null)
        {
            return;
        }

        RefreshScopedActionNodes(actionsNode);
    }

    private static void RefreshScopedVerbNodes(ScopedVerbsNodeViewModel container)
    {
        container.Children.Clear();
        foreach (var verb in container.Verbs)
        {
            container.Children.Add(new ScopedVerbEntryNodeViewModel(verb, container));
        }
    }

    private void RefreshScopedVerbNodesForSelection()
    {
        var verbsNode = ResolveScopedVerbsNode(SelectedNode);
        if (verbsNode is null)
        {
            return;
        }

        RefreshScopedVerbNodes(verbsNode);
    }

    private static void RefreshScopedDirectionalNodes(ScopedDirectionalsNodeViewModel container)
    {
        var wasExpanded = container.IsExpanded;
        container.Children.Clear();
        foreach (var directional in container.Directionals)
        {
            container.Children.Add(new ScopedDirectionalEntryNodeViewModel(directional, container));
        }

        container.IsExpanded = wasExpanded;
    }

    private static void RefreshScopedSoundEffectNodes(ScopedSoundEffectsNodeViewModel container)
    {
        var wasExpanded = container.IsExpanded;
        container.Children.Clear();
        foreach (var soundEffect in container.SoundEffects)
        {
            container.Children.Add(new ScopedSoundEffectEntryNodeViewModel(soundEffect, container));
        }

        container.IsExpanded = wasExpanded;
    }

    private static void RefreshScopedEventSubscriptionNodes(ScopedEventSubscriptionsNodeViewModel container)
    {
        var wasExpanded = container.IsExpanded;
        container.Children.Clear();
        foreach (var subscription in container.Subscriptions)
        {
            container.Children.Add(new ScopedEventSubscriptionEntryNodeViewModel(subscription, container));
        }

        container.IsExpanded = wasExpanded;
    }

    private static void RefreshScopedTimerDefinitionNodes(ScopedTimerDefinitionsNodeViewModel container)
    {
        var wasExpanded = container.IsExpanded;
        container.Children.Clear();
        foreach (var timerDefinition in container.TimerDefinitions)
        {
            container.Children.Add(new ScopedTimerDefinitionEntryNodeViewModel(timerDefinition, container));
        }

        container.IsExpanded = wasExpanded;
    }

    private void RefreshScopedDirectionalNodesForSelection()
    {
        var directionalsNode = ResolveScopedDirectionalsNode(SelectedNode);
        if (directionalsNode is null)
        {
            return;
        }

        RefreshScopedDirectionalNodes(directionalsNode);
    }

    private static ScopedActionsNodeViewModel? ResolveScopedActionsNode(HierarchyNodeViewModel? node)
    {
        return node switch
        {
            ScopedActionsNodeViewModel scopedNode => scopedNode,
            ScopedActionEntryNodeViewModel actionEntry => actionEntry.ParentActionsNode,
            ProjectRootNodeViewModel projectRootNode => EnumerateHierarchyNodes(projectRootNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            PlanetNodeViewModel planetNode => EnumerateHierarchyNodes(planetNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            CountryNodeViewModel countryNode => EnumerateHierarchyNodes(countryNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            AreaNodeViewModel areaNode => EnumerateHierarchyNodes(areaNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            RoomNodeViewModel roomNode => EnumerateHierarchyNodes(roomNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            PlanetGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.PlanetNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            CountryGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.CountryNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            AreaGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.AreaNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            RoomGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.RoomNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            TraversalLegNodeViewModel legNode => EnumerateHierarchyNodes(legNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            GameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            GlobalObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            TemplateGameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedActionsNodeViewModel>().FirstOrDefault(),
            _ => null
        };
    }

    private static ScopedVerbsNodeViewModel? ResolveScopedVerbsNode(HierarchyNodeViewModel? node)
    {
        return node switch
        {
            ScopedVerbsNodeViewModel scopedNode => scopedNode,
            ScopedVerbEntryNodeViewModel verbEntry => verbEntry.ParentVerbsNode,
            ProjectRootNodeViewModel projectRootNode => EnumerateHierarchyNodes(projectRootNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            PlanetNodeViewModel planetNode => EnumerateHierarchyNodes(planetNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            CountryNodeViewModel countryNode => EnumerateHierarchyNodes(countryNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            AreaNodeViewModel areaNode => EnumerateHierarchyNodes(areaNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            RoomNodeViewModel roomNode => EnumerateHierarchyNodes(roomNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            PlanetGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.PlanetNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            CountryGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.CountryNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            AreaGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.AreaNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            RoomGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.RoomNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            GameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            GlobalObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            TemplateGameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedVerbsNodeViewModel>().FirstOrDefault(),
            _ => null
        };
    }

    private static ScopedDirectionalsNodeViewModel? ResolveScopedDirectionalsNode(HierarchyNodeViewModel? node)
    {
        return node switch
        {
            ScopedDirectionalsNodeViewModel scopedNode => scopedNode,
            ScopedDirectionalEntryNodeViewModel directionalEntry => directionalEntry.ParentDirectionalsNode,
            ProjectRootNodeViewModel projectRootNode => EnumerateHierarchyNodes(projectRootNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            PlanetNodeViewModel planetNode => EnumerateHierarchyNodes(planetNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            CountryNodeViewModel countryNode => EnumerateHierarchyNodes(countryNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            AreaNodeViewModel areaNode => EnumerateHierarchyNodes(areaNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            RoomNodeViewModel roomNode => EnumerateHierarchyNodes(roomNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            PlanetGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.PlanetNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            CountryGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.CountryNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            AreaGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.AreaNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            RoomGameObjectsNodeViewModel objectsNode => EnumerateHierarchyNodes(objectsNode.RoomNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            GameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            GlobalObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            TemplateGameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedDirectionalsNodeViewModel>().FirstOrDefault(),
            _ => null
        };
    }

    private static ScopedProceduresNodeViewModel? ResolveScopedProceduresNode(HierarchyNodeViewModel? node)
    {
        return node switch
        {
            ScopedProceduresNodeViewModel scopedNode => scopedNode,
            ScopedProcedureEntryNodeViewModel procedureEntry => procedureEntry.ParentProceduresNode,
            ProjectRootNodeViewModel projectRootNode => EnumerateHierarchyNodes(projectRootNode.Children).OfType<ScopedProceduresNodeViewModel>().FirstOrDefault(),
            GameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedProceduresNodeViewModel>().FirstOrDefault(),
            GlobalObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedProceduresNodeViewModel>().FirstOrDefault(),
            TemplateGameObjectNodeViewModel objectNode => EnumerateHierarchyNodes(objectNode.Children).OfType<ScopedProceduresNodeViewModel>().FirstOrDefault(),
            _ => null
        };
    }

    private void RefreshScopedProcedureNodesForSelection()
    {
        var proceduresNode = ResolveScopedProceduresNode(SelectedNode);
        if (proceduresNode is null)
        {
            return;
        }

        RefreshScopedProcedureNodes(proceduresNode, proceduresNode.Parent);
    }

    private void RefreshAllScopedProcedureNodes()
    {
        foreach (var proceduresNode in EnumerateHierarchyNodes(HierarchyRoots).OfType<ScopedProceduresNodeViewModel>())
        {
            RefreshScopedProcedureNodes(proceduresNode, proceduresNode.Parent);
        }
    }

    private void RefreshScopedProcedureNodes(ScopedProceduresNodeViewModel container, HierarchyNodeViewModel? ownerNode)
    {
        var wasExpanded = container.IsExpanded;
        container.Children.Clear();

        if (ownerNode is null)
        {
            return;
        }

        var knownById = (_project.Procedures ?? new List<ProcedureDefinition>())
            .Where(static procedure => procedure.Id != Guid.Empty)
            .ToDictionary(static procedure => procedure.Id);

        foreach (var procedureId in container.ProcedureIds)
        {
            if (!knownById.TryGetValue(procedureId, out var procedure))
            {
                continue;
            }

            container.Children.Add(new ScopedProcedureEntryNodeViewModel(procedure, container));
        }

        container.IsExpanded = wasExpanded;
    }

    private (List<GamePropertyDefinition>? collection, PropertyResolutionScope scope) ResolveVariableCollectionAndScope(GamePropertiesContainerNodeViewModel variablesNode)
    {
        return variablesNode.Scope switch
        {
            PropertyResolutionScope.Global => (_project.GlobalVariables, PropertyResolutionScope.Global),
            PropertyResolutionScope.Planet when variablesNode.Parent is PlanetNodeViewModel planetNode => (planetNode.Planet.Variables, PropertyResolutionScope.Planet),
            PropertyResolutionScope.Country when variablesNode.Parent is CountryNodeViewModel countryNode => (countryNode.Country.Variables, PropertyResolutionScope.Country),
            PropertyResolutionScope.Area when variablesNode.Parent is AreaNodeViewModel areaNode => (areaNode.Area.Variables, PropertyResolutionScope.Area),
            PropertyResolutionScope.Room when variablesNode.Parent is RoomNodeViewModel roomNode => (roomNode.Room.Variables, PropertyResolutionScope.Room),
            PropertyResolutionScope.Room when variablesNode.Parent is TemplateRoomNodeViewModel templateRoomNode => (templateRoomNode.Room.Variables, PropertyResolutionScope.Room),
            PropertyResolutionScope.Room when variablesNode.Parent is TraversalLegNodeViewModel legNode => (legNode.LegState.Variables, PropertyResolutionScope.Room),
            PropertyResolutionScope.Object when variablesNode.Parent is GameObjectNodeViewModel objectNode => (objectNode.GameObject.Variables, PropertyResolutionScope.Object),
            PropertyResolutionScope.Object when variablesNode.Parent is GlobalObjectNodeViewModel objectNode => (objectNode.GameObject.Variables, PropertyResolutionScope.Object),
            PropertyResolutionScope.Object when variablesNode.Parent is TemplateGameObjectNodeViewModel objectNode => (objectNode.GameObject.Variables, PropertyResolutionScope.Object),
            _ => (null, variablesNode.Scope)
        };
    }

    private static string MakeUniqueVariableName(IEnumerable<GamePropertyDefinition> variables, string baseName)
    {
        return MakeUniqueVariableName(variables, baseName, null);
    }

    private static string MakeUniqueVariableName(IEnumerable<GamePropertyDefinition> variables, string baseName, HashSet<string>? reservedNames)
    {
        var candidate = baseName;
        var index = 2;
        while (IsVariableNameTaken(variables, reservedNames, candidate))
        {
            candidate = $"{baseName}{index}";
            index++;
        }

        return candidate;
    }

    private bool AddNewVariable(GamePropertiesContainerNodeViewModel variablesNode)
    {
        if (!_treeContextInteractionService.TryGetNewVariableInput(string.Empty, GamePropertyLifetime.Singleton, "false", GamePropertyValueRestriction.TrueFalse, out var variableName, out var lifetime, out var defaultValue, out var valueRestriction))
        {
            return false;
        }

        return AddVariableToContainer(variablesNode, variableName, lifetime, defaultValue, valueRestriction) is not null;
    }

    private bool ReviewVariables(GamePropertiesContainerNodeViewModel variablesNode)
    {
        var (collection, scope) = ResolveVariableCollectionAndScope(variablesNode);
        if (collection is null)
        {
            return false;
        }

        // Keep the source list aligned with tree nodes so the review dialog always reflects what is visible.
        foreach (var node in variablesNode.Children.OfType<GamePropertyNodeViewModel>())
        {
            if (!collection.Contains(node.Variable))
            {
                collection.Add(node.Variable);
            }
        }

        var ownerName = variablesNode.Parent?.EditableName ?? "Scope";
        var scopeLabel = scope switch
        {
            PropertyResolutionScope.Global => "Global",
            PropertyResolutionScope.GlobalObjects => "Global Objects",
            PropertyResolutionScope.Planet => $"Planet - {ownerName}",
            PropertyResolutionScope.Country => $"Country - {ownerName}",
            PropertyResolutionScope.Area => $"Area - {ownerName}",
            PropertyResolutionScope.Room => $"Room - {ownerName}",
            PropertyResolutionScope.Object => $"Game Object - {ownerName}",
            _ => ownerName
        };

        _treeContextInteractionService.ShowVariableScopeReview(scopeLabel, collection);
        SynchronizeVariableNodesWithCollection(variablesNode, collection, scope);
        RefreshSharedPropertyIndicators();
        RefreshVariableNameIndex(collection);
        NotifyProjectEdited();
        ExportStatus = $"Reviewed {collection.Count} variable(s).";
        return true;
    }

    private static void SynchronizeVariableNodesWithCollection(
        GamePropertiesContainerNodeViewModel variablesNode,
        IReadOnlyList<GamePropertyDefinition> collection,
        PropertyResolutionScope scope)
    {
        foreach (var variable in collection)
        {
            var existingNode = variablesNode.Children
                .OfType<GamePropertyNodeViewModel>()
                .FirstOrDefault(node => ReferenceEquals(node.Variable, variable));

            if (existingNode is null)
            {
                variablesNode.Children.Add(new GamePropertyNodeViewModel(variable, scope, variablesNode));
                continue;
            }

            var variableName = variable.Name?.Trim() ?? string.Empty;
            var nodeName = existingNode.EditableName?.Trim() ?? string.Empty;
            if (!string.Equals(nodeName, variableName, StringComparison.Ordinal))
            {
                existingNode.SetEditableNameWithoutModelRename(variableName);
            }
        }

        var staleNodes = variablesNode.Children
            .OfType<GamePropertyNodeViewModel>()
            .Where(node => !collection.Any(variable => ReferenceEquals(variable, node.Variable)))
            .ToList();

        foreach (var staleNode in staleNodes)
        {
            variablesNode.Children.Remove(staleNode);
        }

        if (variablesNode.Children.Count > 0)
        {
            variablesNode.IsExpanded = true;
        }
    }

    public IReadOnlyCollection<string> GetVariableNamesReservedInScope(GamePropertyNodeViewModel variableNode)
    {
        var (collection, _) = ResolveVariableCollectionAndScope(variableNode.VariablesContainer);
        if (collection is null)
        {
            return Array.Empty<string>();
        }

        var currentName = variableNode.Variable.Name?.Trim() ?? string.Empty;
        return GetOrCreateVariableNameIndex(collection)
            .Where(name => !string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void RefreshVariableNameIndex(GamePropertyNodeViewModel variableNode)
    {
        var (collection, _) = ResolveVariableCollectionAndScope(variableNode.VariablesContainer);
        if (collection is not null)
        {
            RefreshVariableNameIndex(collection);
        }
    }

    private HashSet<string> GetOrCreateVariableNameIndex(List<GamePropertyDefinition> collection)
    {
        return GetOrCreateScopedNameIndex(collection, static variable => variable.Name);
    }

    private HashSet<string> RefreshVariableNameIndex(List<GamePropertyDefinition> collection)
    {
        return RefreshScopedNameIndex(collection, static variable => variable.Name);
    }

    public void EnsureUniqueHierarchyNodeName(HierarchyNodeViewModel node)
    {
        switch (node)
        {
            case PlanetNodeViewModel planetNode:
            {
                var uniqueName = MakeUniqueScopedEntityName(_project.Planets, static planet => planet.Name, planetNode.EditableName, planetNode.Planet);
                planetNode.EditableName = uniqueName;
                RefreshScopedNameIndex(_project.Planets, static planet => planet.Name);
                break;
            }

            case CountryNodeViewModel countryNode when countryNode.Parent is PlanetNodeViewModel planetNode:
            {
                var uniqueName = MakeUniqueScopedEntityName(planetNode.Planet.Countries, static country => country.Name, countryNode.EditableName, countryNode.Country);
                countryNode.EditableName = uniqueName;
                RefreshScopedNameIndex(planetNode.Planet.Countries, static country => country.Name);
                break;
            }

            case AreaNodeViewModel areaNode when areaNode.Parent is CountryNodeViewModel countryNode:
            {
                var uniqueName = MakeUniqueScopedEntityName(countryNode.Country.Areas, static area => area.Name, areaNode.EditableName, areaNode.Area);
                areaNode.EditableName = uniqueName;
                RefreshScopedNameIndex(countryNode.Country.Areas, static area => area.Name);
                break;
            }

            case RoomNodeViewModel roomNode when roomNode.Parent is AreaNodeViewModel areaNode:
            {
                var uniqueName = MakeUniqueScopedEntityName(areaNode.Area.Rooms, static room => room.Name, roomNode.EditableName, roomNode.Room);
                roomNode.EditableName = uniqueName;
                RefreshScopedNameIndex(areaNode.Area.Rooms, static room => room.Name);
                break;
            }

            case GameObjectNodeViewModel objectNode:
            {
                var siblings = GetScopedObjectSiblings(objectNode);
                if (!TryValidateScopedObjectName(siblings, objectNode.GameObject, objectNode.EditableName, out var normalizedName))
                {
                    objectNode.EditableName = objectNode.GameObject.Name;
                    break;
                }

                objectNode.EditableName = normalizedName;
                RefreshScopedNameIndex(siblings, static obj => obj.Name);
                break;
            }

            case GlobalObjectNodeViewModel objectNode:
            {
                var siblings = GetScopedObjectSiblings(objectNode);
                if (!TryValidateScopedObjectName(siblings, objectNode.GameObject, objectNode.EditableName, out var normalizedName))
                {
                    objectNode.EditableName = objectNode.GameObject.Name;
                    break;
                }

                objectNode.EditableName = normalizedName;
                RefreshScopedNameIndex(siblings, static obj => obj.Name);
                break;
            }

            case TemplateGameObjectNodeViewModel objectNode:
            {
                var siblings = GetScopedObjectSiblings(objectNode);
                if (!TryValidateScopedObjectName(siblings, objectNode.GameObject, objectNode.EditableName, out var normalizedName))
                {
                    objectNode.EditableName = objectNode.GameObject.Name;
                    break;
                }

                objectNode.EditableName = normalizedName;
                RefreshScopedNameIndex(siblings, static obj => obj.Name);
                break;
            }
        }
    }

    private void PropagateLinkedRoomInstanceNames(GameObject sourceObject, string requestedName)
    {
        var normalizedName = NormalizeScopedName(requestedName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var linkedGroupId = sourceObject.LinkedBaseObjectId ?? sourceObject.ObjectId;
        if (linkedGroupId == Guid.Empty)
        {
            return;
        }

        var refreshedCollections = new HashSet<List<GameObject>>();
        foreach (var candidate in EnumerateAllProjectGameObjects())
        {
            if (ReferenceEquals(candidate, sourceObject))
            {
                continue;
            }

            if (!candidate.IsRoomInstance)
            {
                continue;
            }

            var candidateGroupId = candidate.LinkedBaseObjectId ?? candidate.ObjectId;
            if (candidateGroupId != linkedGroupId)
            {
                continue;
            }

            if (!TryResolveOwningObjectCollection(candidate, out var ownerCollection))
            {
                candidate.Name = normalizedName;
                continue;
            }

            candidate.Name = normalizedName;
            refreshedCollections.Add(ownerCollection);
        }

        foreach (var ownerCollection in refreshedCollections)
        {
            RefreshScopedNameIndex(ownerCollection, static obj => obj.Name);
        }
    }

    private bool TryResolveOwningObjectCollection(GameObject gameObject, out List<GameObject> ownerCollection)
    {
        switch (gameObject.ParentScope)
        {
            case GameObject parentObject:
                ownerCollection = parentObject.ContainedObjects;
                return true;
            case Room room:
                ownerCollection = room.GameObjects;
                return true;
            case ProjectModel:
                ownerCollection = _project.GlobalScope.GameObjects;
                return true;
            case ObjectTemplatesScopeNode:
                ownerCollection = _project.ObjectTemplates;
                return true;
            case BaseObjectsScopeNode:
                ownerCollection = _project.BaseObjects;
                return true;
            default:
                ownerCollection = null!;
                return false;
        }
    }

    private IEnumerable<GameObject> EnumerateAllProjectGameObjects()
    {
        foreach (var template in EnumerateGameObjectsRecursive(_project.ObjectTemplates))
        {
            yield return template;
        }

        foreach (var baseObject in EnumerateGameObjectsRecursive(_project.BaseObjects))
        {
            yield return baseObject;
        }

        foreach (var baseObject in _project.Planets
                     .SelectMany(planet => EnumerateGameObjectsRecursive(planet.BaseObjects)))
        {
            yield return baseObject;
        }

        foreach (var baseObject in _project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => EnumerateGameObjectsRecursive(country.BaseObjects)))
        {
            yield return baseObject;
        }

        foreach (var baseObject in _project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => EnumerateGameObjectsRecursive(area.BaseObjects)))
        {
            yield return baseObject;
        }

        foreach (var playerObject in EnumerateGameObjectsRecursive(_project.GlobalScope.GameObjects))
        {
            yield return playerObject;
        }

        foreach (var roomObject in _project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => EnumerateGameObjectsRecursive(room.GameObjects)))
        {
            yield return roomObject;
        }
    }

    private bool TryValidateScopedObjectName(List<GameObject> siblings, GameObject current, string candidateName, out string normalizedName)
    {
        normalizedName = NormalizeScopedName(candidateName);
        var candidate = normalizedName;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _projectUiService.ShowWarning("Game object name is required.", "Object Name");
            return false;
        }

        var duplicateExists = siblings.Any(obj =>
            !ReferenceEquals(obj, current)
            && string.Equals(NormalizeScopedName(obj.Name), candidate, StringComparison.OrdinalIgnoreCase));

        if (!duplicateExists)
        {
            return true;
        }

        _projectUiService.ShowWarning($"A game object named '{normalizedName}' already exists in this scope. Choose a unique name.", "Object Name");
        return false;
    }

    private static List<GameObject> GetScopedObjectSiblings(GameObjectNodeViewModel node)
    {
        return node.ParentObjectNode is not null
            ? node.ParentObjectNode.GameObject.ContainedObjects
            : node.ParentObjectsNode.GameObjects;
    }

    private static List<GameObject> GetScopedObjectSiblings(GlobalObjectNodeViewModel node)
    {
        return node.ParentObjectNode is not null
            ? node.ParentObjectNode.GameObject.ContainedObjects
            : node.ParentGlobalObjectsNode.GameObjects;
    }

    private static List<GameObject> GetScopedObjectSiblings(TemplateGameObjectNodeViewModel node)
    {
        return node.ParentObjectNode is not null
            ? node.ParentObjectNode.GameObject.ContainedObjects
            : node.ParentTemplatesNode.CatalogObjects;
    }

    private string MakeUniqueScopedEntityName<T>(List<T> collection, Func<T, string> selector, string baseName, T? current = null)
        where T : class
    {
        var normalizedBase = string.IsNullOrWhiteSpace(baseName) ? "New Item" : baseName.Trim();
        var reserved = new HashSet<string>(GetOrCreateScopedNameIndex(collection, selector), StringComparer.OrdinalIgnoreCase);

        if (current is not null)
        {
            var currentName = NormalizeScopedName(selector(current));
            if (!string.IsNullOrWhiteSpace(currentName))
            {
                reserved.Remove(currentName);
            }
        }

        if (!reserved.Contains(normalizedBase))
        {
            return normalizedBase;
        }

        var index = 2;
        var candidate = $"{normalizedBase} {index}";
        while (reserved.Contains(candidate))
        {
            index++;
            candidate = $"{normalizedBase} {index}";
        }

        return candidate;
    }

    private HashSet<string> GetOrCreateScopedNameIndex<T>(List<T> collection, Func<T, string> selector)
    {
        if (_scopedNameIndexes.TryGetValue(collection, out var index) && IsScopedNameIndexInSync(collection, index, selector))
        {
            return index;
        }

        return RefreshScopedNameIndex(collection, selector);
    }

    private HashSet<string> RefreshScopedNameIndex<T>(List<T> collection, Func<T, string> selector)
    {
        var index = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in collection)
        {
            var name = NormalizeScopedName(selector(entry));
            if (!string.IsNullOrWhiteSpace(name))
            {
                index.Add(name);
            }
        }

        _scopedNameIndexes[collection] = index;
        return index;
    }

    private static bool IsScopedNameIndexInSync<T>(List<T> collection, HashSet<string> index, Func<T, string> selector)
    {
        var names = collection
            .Select(entry => NormalizeScopedName(selector(entry)))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return names.Count == index.Count && names.All(index.Contains);
    }

    private static string NormalizeScopedName(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static RuntimeObjectSpatialTypes NormalizeSpatialType(RuntimeObjectSpatialTypes spatialType)
    {
        return spatialType == RuntimeObjectSpatialTypes.PassiveObject
            ? RuntimeObjectSpatialTypes.PassiveObject
            : RuntimeObjectSpatialTypes.SolidObject;
    }

    private void ClearScopedNameIndexes()
    {
        _scopedNameIndexes.Clear();
    }

    private static bool IsVariableNameTaken(IEnumerable<GamePropertyDefinition> variables, HashSet<string>? reservedNames, string candidate)
    {
        if (reservedNames is not null)
        {
            return reservedNames.Contains(candidate.Trim());
        }

        return variables.Any(v => string.Equals(v.Name, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private RoomNodeViewModel? FindRoomNode(Room room)
    {
        foreach (var root in HierarchyRoots)
        {
            var found = FindRoomNodeRecursive(root, room);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static RoomNodeViewModel? FindRoomNodeRecursive(HierarchyNodeViewModel node, Room room)
    {
        if (node is RoomNodeViewModel roomNode && ReferenceEquals(roomNode.Room, room))
        {
            return roomNode;
        }

        foreach (var child in node.Children)
        {
            var found = FindRoomNodeRecursive(child, room);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}





