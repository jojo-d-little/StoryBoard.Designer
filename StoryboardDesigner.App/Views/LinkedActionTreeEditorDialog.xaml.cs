using System.Collections.ObjectModel;
using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Views;

public partial class LinkedActionTreeEditorDialog : Window
{
    public enum NodeKind
    {
        Action,
        Branch,
        LinkedAction
    }

    public sealed class TreeNode
    {
        public required NodeKind Kind { get; init; }
        public required string Title { get; set; }
        public CommandAction? Action { get; init; }
        public CommandAction? ParentAction { get; init; }
        public LinkedActionFlowLink? Link { get; init; }
        public LinkedActionRunWhen? RunWhen { get; init; }
        public int? FlowNodeId { get; init; }
        public int? ParentFlowNodeId { get; init; }
        public bool IsEntryLink { get; init; }
        public ObservableCollection<TreeNode> Children { get; } = new();
    }

    private readonly string _scopeLabel;
    private readonly CommandAction _rootAction;
    private readonly IList<CommandAction> _editableActions;
    private readonly IReadOnlyList<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)> _externalActionChoices;
    private readonly IReadOnlyList<CommandAction> _externalActions;
    private readonly PropertyResolutionScope _variableScope;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _variableChoices;
    private readonly IReadOnlyList<string> _echoReferenceTokens;
    private readonly IReadOnlyList<ContainerTargetChoiceItem> _containerTargetChoices;
    private readonly IReadOnlyList<MaterializeSourceObjectChoiceItem> _materializeSourceObjectChoices;
    private readonly IReadOnlyList<ProcedureChoiceItem> _procedureChoices;
    private readonly IReadOnlyList<CompositeRecipeChoiceItem> _compositeRecipeChoices;
    private readonly IReadOnlyList<SoundEffectChoiceItem> _soundEffectChoices;
    private readonly IReadOnlyList<string> _timerKeySuggestions;
    private readonly IReadOnlyList<SequenceDefinitionChoiceItem> _sequenceDefinitionChoices;
    private Dictionary<Guid, CommandAction> _allActionsById = new();
    private readonly HashSet<Guid> _originalActionIds;
    private readonly LinkedActionFlowDefinition _originalDefinition;
    private readonly HashSet<int> _forcedConditional = new();
    private bool _accepted;
    private bool _updatingExitResultSelection;

    private ObservableCollection<TreeNode> _treeRoots = new();

    public LinkedActionTreeEditorDialog(
        string scopeLabel,
        CommandAction rootAction,
        IList<CommandAction> editableActions,
        IReadOnlyList<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)>? externalActions = null,
        PropertyResolutionScope variableScope = PropertyResolutionScope.Room,
        IReadOnlyList<GamePropertyChoiceItem>? variableChoices = null,
        IReadOnlyList<string>? echoReferenceTokens = null,
        IReadOnlyList<ContainerTargetChoiceItem>? containerTargetChoices = null,
        IReadOnlyList<MaterializeSourceObjectChoiceItem>? materializeSourceObjectChoices = null,
        IReadOnlyList<ProcedureChoiceItem>? procedureChoices = null,
        IReadOnlyList<CompositeRecipeChoiceItem>? compositeRecipeChoices = null,
        IReadOnlyList<SoundEffectChoiceItem>? soundEffectChoices = null,
        IReadOnlyList<string>? timerKeySuggestions = null,
        IReadOnlyList<SequenceDefinitionChoiceItem>? sequenceDefinitionChoices = null)
    {
        InitializeComponent();

        _scopeLabel = scopeLabel;
        _rootAction = rootAction;
        _editableActions = editableActions;
        _externalActionChoices = externalActions ?? Array.Empty<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)>();
        _externalActions = _externalActionChoices.Select(static choice => choice.Action).ToList();
        _variableScope = variableScope;
        _variableChoices = variableChoices ?? Array.Empty<GamePropertyChoiceItem>();
        _echoReferenceTokens = echoReferenceTokens ?? Array.Empty<string>();
        _containerTargetChoices = containerTargetChoices ?? Array.Empty<ContainerTargetChoiceItem>();
        _materializeSourceObjectChoices = materializeSourceObjectChoices ?? Array.Empty<MaterializeSourceObjectChoiceItem>();
        _procedureChoices = procedureChoices ?? Array.Empty<ProcedureChoiceItem>();
        _compositeRecipeChoices = compositeRecipeChoices ?? Array.Empty<CompositeRecipeChoiceItem>();
        _soundEffectChoices = soundEffectChoices ?? Array.Empty<SoundEffectChoiceItem>();
        _timerKeySuggestions = timerKeySuggestions ?? Array.Empty<string>();
        _sequenceDefinitionChoices = sequenceDefinitionChoices ?? Array.Empty<SequenceDefinitionChoiceItem>();
        RefreshActionLookup();
        _originalActionIds = editableActions.Select(action => action.Id).ToHashSet();
        _originalDefinition = rootAction.LinkedFlowDefinition.Clone();

        HeaderText.Text = $"Linked Action Flow - {rootAction.Name}";
        RebuildTree();
        UpdateActionButtons();
    }

    private void RebuildTree()
    {
        _treeRoots = new ObservableCollection<TreeNode>();
        var rootNode = new TreeNode
        {
            Kind = NodeKind.Action,
            Title = $"{_rootAction.Name} ({_rootAction.ActionType})",
            Action = _rootAction
        };
        var entryBranch = new TreeNode
        {
            Kind = NodeKind.Branch,
            Title = "Always",
            ParentAction = _rootAction,
            RunWhen = LinkedActionRunWhen.Always,
            IsEntryLink = true
        };
        if (_rootAction.LinkedFlowDefinition.EntryNodeId is int entryNodeId)
        {
            entryBranch.Children.Add(BuildFlowNode(entryNodeId, null, LinkedActionRunWhen.Always, new HashSet<int>()));
        }
        rootNode.Children.Add(entryBranch);
        _treeRoots.Add(rootNode);
        FlowTree.ItemsSource = _treeRoots;
    }

    private TreeNode BuildFlowNode(
        int flowNodeId,
        int? parentFlowNodeId,
        LinkedActionRunWhen incomingRunWhen,
        HashSet<int> path)
    {
        var flowNode = _rootAction.LinkedFlowDefinition.Nodes.FirstOrDefault(node => node.NodeId == flowNodeId);
        var action = flowNode is null ? null : TryGetAction(flowNode.ActionId);
        var node = new TreeNode
        {
            Kind = NodeKind.LinkedAction,
            Title = action is null ? $"-> missing action ({flowNode?.ActionId:N})" : $"-> {action.Name} ({action.ActionType})",
            Action = action,
            ParentAction = parentFlowNodeId is null ? _rootAction : TryGetAction(_rootAction.LinkedFlowDefinition.Nodes.FirstOrDefault(candidate => candidate.NodeId == parentFlowNodeId.Value)?.ActionId ?? Guid.Empty),
            Link = parentFlowNodeId is null
                ? null
                : _rootAction.LinkedFlowDefinition.Nodes.FirstOrDefault(candidate => candidate.NodeId == parentFlowNodeId.Value)?.Links
                    .FirstOrDefault(link => link.TargetNodeId == flowNodeId && link.RunWhen == incomingRunWhen),
            RunWhen = incomingRunWhen,
            FlowNodeId = flowNodeId,
            ParentFlowNodeId = parentFlowNodeId,
            IsEntryLink = parentFlowNodeId is null
        };

        if (flowNode is null || action is null || path.Contains(flowNodeId))
        {
            return node;
        }

        var nextPath = new HashSet<int>(path) { flowNodeId };
        AppendBranchNodes(flowNode, action, node, nextPath);
        return node;
    }

    private void AppendBranchNodes(LinkedActionFlowNode flowNode, CommandAction action, TreeNode parent, HashSet<int> path)
    {
        var isConditional = IsConditionalMode(flowNode);
        var branches = isConditional
            ? new[] { LinkedActionRunWhen.OnSuccess, LinkedActionRunWhen.OnFailure }
            : new[] { LinkedActionRunWhen.Always };

        foreach (var runWhen in branches)
        {
            var branchNode = new TreeNode
            {
                Kind = NodeKind.Branch,
                Title = runWhen.ToString(),
                ParentAction = action,
                RunWhen = runWhen,
                FlowNodeId = flowNode.NodeId
            };

            var links = flowNode.Links
                .Where(link => link.RunWhen == runWhen)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.TargetNodeId)
                .ToList();

            foreach (var link in links)
            {
                branchNode.Children.Add(BuildFlowNode(link.TargetNodeId, flowNode.NodeId, runWhen, path));
            }

            parent.Children.Add(branchNode);
        }
    }

    private bool IsConditionalMode(LinkedActionFlowNode flowNode)
    {
        if (_forcedConditional.Contains(flowNode.NodeId))
        {
            return true;
        }

        return flowNode.Links.Any(link => link.RunWhen != LinkedActionRunWhen.Always);
    }

    private void FlowTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var node = FlowTree.SelectedItem as TreeNode;

        var definition = _rootAction.LinkedFlowDefinition;
        AddExistingButton.IsEnabled = node?.Kind == NodeKind.Branch
            && (!node.IsEntryLink || !definition.EntryNodeId.HasValue);
        AddNewButton.IsEnabled = AddExistingButton.IsEnabled;
        EditActionButton.IsEnabled = ResolveSelectedEditableLinkedAction(node) is not null;
        RemoveLinkButton.IsEnabled = node?.Kind == NodeKind.LinkedAction && (node.IsEntryLink || node.Link is not null);

        var selectedAction = ResolveSelectedAction(node);
        RunConditionalButton.IsEnabled = selectedAction is not null && ResolveSelectedFlowNodeId(node).HasValue;
        RunAlwaysButton.IsEnabled = RunConditionalButton.IsEnabled;

        var selectedExitBranch = node?.Kind == NodeKind.Branch
            && node.FlowNodeId.HasValue
            && node.RunWhen.HasValue;
        var selectedFlowNode = selectedExitBranch
            ? definition.Nodes.FirstOrDefault(flowNode => flowNode.NodeId == node!.FlowNodeId!.Value)
            : null;
        var selectedRunWhen = selectedExitBranch ? node!.RunWhen : null;
        var exitResult = selectedFlowNode is null || selectedRunWhen is null
            ? null
            : GetExitResult(selectedFlowNode, selectedRunWhen.Value);
        _updatingExitResultSelection = true;
        ExitResultComboBox.Visibility = selectedFlowNode is null ? Visibility.Collapsed : Visibility.Visible;
        ExitResultComboBox.IsEnabled = selectedFlowNode is not null
            && selectedRunWhen is not null
            && !HasOutcomeTargets(selectedFlowNode, selectedRunWhen.Value);
        ExitResultComboBox.SelectedIndex = exitResult switch
        {
            LinkedActionExitResult.Success => 1,
            LinkedActionExitResult.Failure => 2,
            _ => 0
        };
        _updatingExitResultSelection = false;

        if (selectedExitBranch && selectedFlowNode is not null && selectedRunWhen.HasValue)
        {
            HintText.Text = HasOutcomeTargets(selectedFlowNode, selectedRunWhen.Value)
                ? $"This {selectedRunWhen} outcome continues to linked action(s). Remove its linked next action to set an exit result."
                : $"Set the result reported by the linked action when this {selectedRunWhen} outcome ends here. Unset uses the last action's result.";
        }
        else if (node?.Kind == NodeKind.Branch)
        {
            HintText.Text = "Add existing or new linked actions under the selected branch.";
        }
        else if (node?.Kind == NodeKind.LinkedAction)
        {
            HintText.Text = "Edit this linked action, remove this link, or switch the selected action between always and conditional mode.";
        }
        else if (selectedAction is not null)
        {
            HintText.Text = "Switch mode with Run Conditionally / Run Always, then add links in branch nodes.";
        }
        else
        {
            HintText.Text = "Select an action node to switch mode, or select a branch to add links.";
        }
    }

    private void ExitResultComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updatingExitResultSelection
            || FlowTree.SelectedItem is not TreeNode { Kind: NodeKind.Branch, FlowNodeId: int nodeId, RunWhen: LinkedActionRunWhen runWhen })
        {
            return;
        }

        var node = _rootAction.LinkedFlowDefinition.Nodes.FirstOrDefault(flowNode => flowNode.NodeId == nodeId);
        if (node is null || HasOutcomeTargets(node, runWhen))
        {
            return;
        }

        SetExitResult(node, runWhen, ExitResultComboBox.SelectedIndex switch
        {
            1 => LinkedActionExitResult.Success,
            2 => LinkedActionExitResult.Failure,
            _ => null
        });
    }

    private static LinkedActionExitResult? GetExitResult(LinkedActionFlowNode node, LinkedActionRunWhen runWhen) => runWhen switch
    {
        LinkedActionRunWhen.OnSuccess => node.OnSuccessExitResult,
        LinkedActionRunWhen.OnFailure => node.OnFailureExitResult,
        _ => node.OnAlwaysExitResult
    };

    private static void SetExitResult(LinkedActionFlowNode node, LinkedActionRunWhen runWhen, LinkedActionExitResult? result)
    {
        switch (runWhen)
        {
            case LinkedActionRunWhen.OnSuccess:
                node.OnSuccessExitResult = result;
                break;
            case LinkedActionRunWhen.OnFailure:
                node.OnFailureExitResult = result;
                break;
            default:
                node.OnAlwaysExitResult = result;
                break;
        }
    }

    private static bool HasOutcomeTargets(LinkedActionFlowNode node, LinkedActionRunWhen runWhen) =>
        node.Links.Any(link => link.RunWhen == runWhen);

    private CommandAction? ResolveSelectedAction(TreeNode? node)
    {
        if (node is null)
        {
            return null;
        }

        var flowNodeId = ResolveSelectedFlowNodeId(node);
        if (flowNodeId.HasValue)
        {
            var actionId = _rootAction.LinkedFlowDefinition.Nodes
                .FirstOrDefault(flowNode => flowNode.NodeId == flowNodeId.Value)?.ActionId;
            return actionId.HasValue ? TryGetAction(actionId.Value) : null;
        }

        return node.Kind == NodeKind.LinkedAction && node.IsEntryLink
            ? _rootAction
            : null;
    }

    private static int? ResolveSelectedFlowNodeId(TreeNode? node)
    {
        return node?.Kind switch
        {
            NodeKind.Branch => node.FlowNodeId,
            NodeKind.LinkedAction => node.ParentFlowNodeId,
            _ => null
        };
    }

    private CommandAction? ResolveSelectedEditableLinkedAction(TreeNode? node)
    {
        if (node?.Kind != NodeKind.LinkedAction || node.Action is null)
        {
            return null;
        }

        return _editableActions.Any(action => action.Id == node.Action.Id)
            ? node.Action
            : null;
    }

    private void EditAction_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedAction = ResolveSelectedEditableLinkedAction(FlowTree.SelectedItem as TreeNode);
        if (selectedAction is null)
        {
            return;
        }

        if (selectedAction.ActionType == CommandActionType.LinkedActions)
        {
            var linkedDialog = new LinkedActionTreeEditorDialog(
                _scopeLabel,
                selectedAction,
                _editableActions,
                _externalActionChoices,
                _variableScope,
                _variableChoices,
                _echoReferenceTokens,
                _containerTargetChoices,
                _materializeSourceObjectChoices,
                _procedureChoices,
                _compositeRecipeChoices,
                _soundEffectChoices,
                _timerKeySuggestions,
                _sequenceDefinitionChoices)
            {
                Owner = this
            };

            linkedDialog.ShowDialog();
            RebuildTree();
            return;
        }

        var dialog = new RoomActionEditorDialog(
            selectedAction,
            _variableScope,
            _variableChoices,
            _echoReferenceTokens,
            _containerTargetChoices,
            _materializeSourceObjectChoices,
            _procedureChoices,
            _compositeRecipeChoices,
            _soundEffectChoices,
            _timerKeySuggestions,
            _editableActions.ToList(),
            _sequenceDefinitionChoices)
        {
            Owner = this
        };

        dialog.ShowDialog();
        RebuildTree();
    }

    private void AddExisting_OnClick(object sender, RoutedEventArgs e)
    {
        if (FlowTree.SelectedItem is not TreeNode branchNode
            || branchNode.Kind != NodeKind.Branch
            || branchNode.ParentAction is null
            || branchNode.RunWhen is null)
        {
            return;
        }

        var candidates = _editableActions
            .Select(action => (Action: action, ScopeName: _scopeLabel, Group: LinkedActionTargetGroup.CurrentScope))
            .Concat(_externalActionChoices)
            .Where(candidate => candidate.Action.Id != branchNode.ParentAction.Id)
            .Where(static candidate => candidate.Action.ActionType != CommandActionType.Synonym)
            .GroupBy(static candidate => candidate.Action.Id)
            .Select(static group => group.First())
            .OrderBy(candidate => candidate.Action.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Action.Id)
            .ToList();
        if (candidates.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No other actions available to link.", "Linked Actions", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var picker = new SelectLinkedActionDialog(candidates)
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedAction is null)
        {
            return;
        }

        AddLink(branchNode, picker.SelectedAction.Id, reuseExistingFlowNode: true);
    }

    private void AddNew_OnClick(object sender, RoutedEventArgs e)
    {
        if (FlowTree.SelectedItem is not TreeNode branchNode
            || branchNode.Kind != NodeKind.Branch
            || branchNode.ParentAction is null
            || branchNode.RunWhen is null)
        {
            return;
        }

        var creator = new NewLinkedActionDialog
        {
            Owner = this
        };

        if (creator.ShowDialog() != true)
        {
            return;
        }

        if (creator.ActionType == CommandActionType.Synonym)
        {
            System.Windows.MessageBox.Show(this, "Synonym actions are command-entry actions and cannot be created in linked-action flow.", "Linked Actions", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var created = new CommandAction
        {
            Name = creator.ActionName,
            ActionType = creator.ActionType,
            NoVerbLinkage = false,
            VerbListText = string.Empty,
            DirectionQualifierText = string.Empty,
            TargetContainerId = string.Empty,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase),
            SynonymTargetActionId = null
        };

        ActionPayloadAccessors.SetEchoMessage(created, string.Empty);

        _editableActions.Add(created);
        RefreshActionLookup();
        AddLink(branchNode, created.Id, reuseExistingFlowNode: false);
    }

    private void AddLink(TreeNode branchNode, Guid actionId, bool reuseExistingFlowNode)
    {
        if (branchNode.ParentAction?.Id == actionId)
        {
            System.Windows.MessageBox.Show(this, "An action cannot link to itself.", "Linked Actions", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var definition = _rootAction.LinkedFlowDefinition;
        var existingNode = reuseExistingFlowNode
            ? definition.Nodes.FirstOrDefault(node => node.ActionId == actionId)
            : null;
        var nodeId = existingNode?.NodeId
            ?? definition.Nodes.Select(static node => node.NodeId).DefaultIfEmpty(0).Max() + 1;
        if (existingNode is null)
        {
            definition.Nodes.Add(new LinkedActionFlowNode
            {
                NodeId = nodeId,
                ActionId = actionId
            });
        }

        if (branchNode.IsEntryLink)
        {
            if (definition.EntryNodeId.HasValue)
            {
                if (existingNode is null)
                {
                    definition.Nodes.RemoveAll(node => node.NodeId == nodeId);
                }
                System.Windows.MessageBox.Show(this, "This linked action already has an entry step. Add another step from an outcome on the existing step.", "Linked Actions", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            definition.EntryNodeId = nodeId;
        }
        else if (branchNode.FlowNodeId is int parentNodeId && branchNode.RunWhen is LinkedActionRunWhen runWhen)
        {
            var parentNode = definition.Nodes.FirstOrDefault(node => node.NodeId == parentNodeId);
            if (parentNode is null)
            {
                if (existingNode is null)
                {
                    definition.Nodes.RemoveAll(node => node.NodeId == nodeId);
                }
                return;
            }

            var nextOrder = parentNode.Links
                .Where(link => link.RunWhen == runWhen)
                .Select(static link => link.Order)
                .DefaultIfEmpty(-1)
                .Max() + 1;
            parentNode.Links.Add(new LinkedActionFlowLink
            {
                TargetNodeId = nodeId,
                RunWhen = runWhen,
                Order = nextOrder
            });
        }
        else
        {
            if (existingNode is null)
            {
                definition.Nodes.RemoveAll(node => node.NodeId == nodeId);
            }
            return;
        }

        RebuildTree();
    }

    private void RemoveLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (FlowTree.SelectedItem is not TreeNode selected
            || selected.Kind != NodeKind.LinkedAction
            || (!selected.IsEntryLink && (selected.ParentFlowNodeId is null || selected.Link is null)))
        {
            return;
        }

        var definition = _rootAction.LinkedFlowDefinition;
        if (selected.IsEntryLink && selected.FlowNodeId is int entryNodeId)
        {
            definition.EntryNodeId = null;
            definition.Nodes.RemoveAll(node => node.NodeId == entryNodeId);
        }
        else if (selected.ParentFlowNodeId is int parentNodeId && selected.Link is not null)
        {
            var parentNode = definition.Nodes.FirstOrDefault(node => node.NodeId == parentNodeId);
            parentNode?.Links.Remove(selected.Link);
        }

        PruneUnreachableNodes(definition);
        RebuildTree();
    }

    private static void PruneUnreachableNodes(LinkedActionFlowDefinition definition)
    {
        if (definition.EntryNodeId is not int entryNodeId)
        {
            definition.Nodes.Clear();
            return;
        }

        var nodesById = definition.Nodes.ToDictionary(static node => node.NodeId);
        var reachable = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(entryNodeId);
        while (pending.Count > 0)
        {
            var currentId = pending.Pop();
            if (!reachable.Add(currentId) || !nodesById.TryGetValue(currentId, out var current))
            {
                continue;
            }

            foreach (var link in current.Links)
            {
                pending.Push(link.TargetNodeId);
            }
        }

        definition.Nodes.RemoveAll(node => !reachable.Contains(node.NodeId));
    }

    private void RunConditionally_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedAction = ResolveSelectedAction(FlowTree.SelectedItem as TreeNode);
        if (selectedAction is null)
        {
            return;
        }

        var flowNodeId = ResolveSelectedFlowNodeId(FlowTree.SelectedItem as TreeNode);
        var flowNode = flowNodeId.HasValue
            ? _rootAction.LinkedFlowDefinition.Nodes.FirstOrDefault(node => node.NodeId == flowNodeId.Value)
            : null;
        if (flowNode is null)
        {
            return;
        }

        var alwaysLinks = flowNode.Links.Where(link => link.RunWhen == LinkedActionRunWhen.Always).ToList();
        if (alwaysLinks.Count > 0)
        {
            var choice = System.Windows.MessageBox.Show(
                this,
                "Move existing OnAlways links to OnSuccess?\nChoose No to move them to OnFailure.",
                "Run Conditionally",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel)
            {
                return;
            }

            var targetRunWhen = choice == MessageBoxResult.Yes
                ? LinkedActionRunWhen.OnSuccess
                : LinkedActionRunWhen.OnFailure;

            foreach (var link in alwaysLinks)
            {
                link.RunWhen = targetRunWhen;
            }
        }

        _forcedConditional.Add(flowNode.NodeId);
        RebuildTree();
    }

    private void RunAlways_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedAction = ResolveSelectedAction(FlowTree.SelectedItem as TreeNode);
        if (selectedAction is null)
        {
            return;
        }

        var flowNodeId = ResolveSelectedFlowNodeId(FlowTree.SelectedItem as TreeNode);
        var flowNode = flowNodeId.HasValue
            ? _rootAction.LinkedFlowDefinition.Nodes.FirstOrDefault(node => node.NodeId == flowNodeId.Value)
            : null;
        if (flowNode is null)
        {
            return;
        }

        foreach (var link in flowNode.Links)
        {
            link.RunWhen = LinkedActionRunWhen.Always;
        }

        _forcedConditional.Remove(flowNode.NodeId);
        RebuildTree();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var errors = LinkedActionGraphValidator.Validate(_editableActions.ToList(), _scopeLabel, _externalActions);
        if (errors.Count > 0)
        {
            var preview = string.Join(Environment.NewLine, errors.Take(10));
            var suffix = errors.Count > 10 ? Environment.NewLine + "..." : string.Empty;
            System.Windows.MessageBox.Show(this, $"Unable to save linked flow:\n{preview}{suffix}", "Linked Actions", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _accepted = true;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_accepted)
        {
            return;
        }

        RestoreSnapshot();
    }

    private void RestoreSnapshot()
    {
        var toRemove = _editableActions.Where(action => !_originalActionIds.Contains(action.Id)).ToList();
        foreach (var action in toRemove)
        {
            _editableActions.Remove(action);
        }

        _rootAction.LinkedFlowDefinition = _originalDefinition.Clone();

        RefreshActionLookup();
    }

    private CommandAction? TryGetAction(Guid actionId)
    {
        return _allActionsById.TryGetValue(actionId, out var action)
            ? action
            : null;
    }

    private void RefreshActionLookup()
    {
        var map = new Dictionary<Guid, CommandAction>();

        foreach (var action in _externalActions)
        {
            map[action.Id] = action;
        }

        foreach (var action in _editableActions)
        {
            map[action.Id] = action;
        }

        _allActionsById = map;
    }
}
