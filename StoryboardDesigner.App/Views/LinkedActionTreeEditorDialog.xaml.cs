using System.Collections.ObjectModel;
using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

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
        public LinkedActionReference? Link { get; init; }
        public LinkedActionRunWhen? RunWhen { get; init; }
        public ObservableCollection<TreeNode> Children { get; } = new();
    }

    private readonly string _scopeLabel;
    private readonly CommandAction _rootAction;
    private readonly IList<CommandAction> _editableActions;
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
    private Dictionary<Guid, CommandAction> _allActionsById = new();
    private readonly HashSet<Guid> _originalActionIds;
    private readonly Dictionary<Guid, List<LinkedActionReference>> _originalLinks;
    private readonly HashSet<Guid> _forcedConditional = new();
    private bool _accepted;

    private ObservableCollection<TreeNode> _treeRoots = new();

    public LinkedActionTreeEditorDialog(
        string scopeLabel,
        CommandAction rootAction,
        IList<CommandAction> editableActions,
        IReadOnlyList<CommandAction>? externalActions = null,
        PropertyResolutionScope variableScope = PropertyResolutionScope.Room,
        IReadOnlyList<GamePropertyChoiceItem>? variableChoices = null,
        IReadOnlyList<string>? echoReferenceTokens = null,
        IReadOnlyList<ContainerTargetChoiceItem>? containerTargetChoices = null,
        IReadOnlyList<MaterializeSourceObjectChoiceItem>? materializeSourceObjectChoices = null,
        IReadOnlyList<ProcedureChoiceItem>? procedureChoices = null,
        IReadOnlyList<CompositeRecipeChoiceItem>? compositeRecipeChoices = null,
        IReadOnlyList<SoundEffectChoiceItem>? soundEffectChoices = null,
        IReadOnlyList<string>? timerKeySuggestions = null)
    {
        InitializeComponent();

        _scopeLabel = scopeLabel;
        _rootAction = rootAction;
        _editableActions = editableActions;
        _externalActions = externalActions ?? Array.Empty<CommandAction>();
        _variableScope = variableScope;
        _variableChoices = variableChoices ?? Array.Empty<GamePropertyChoiceItem>();
        _echoReferenceTokens = echoReferenceTokens ?? Array.Empty<string>();
        _containerTargetChoices = containerTargetChoices ?? Array.Empty<ContainerTargetChoiceItem>();
        _materializeSourceObjectChoices = materializeSourceObjectChoices ?? Array.Empty<MaterializeSourceObjectChoiceItem>();
        _procedureChoices = procedureChoices ?? Array.Empty<ProcedureChoiceItem>();
        _compositeRecipeChoices = compositeRecipeChoices ?? Array.Empty<CompositeRecipeChoiceItem>();
        _soundEffectChoices = soundEffectChoices ?? Array.Empty<SoundEffectChoiceItem>();
        _timerKeySuggestions = timerKeySuggestions ?? Array.Empty<string>();
        RefreshActionLookup();
        _originalActionIds = editableActions.Select(action => action.Id).ToHashSet();
        _originalLinks = editableActions.ToDictionary(
            action => action.Id,
            action => action.LinkedActions.Select(link => new LinkedActionReference
            {
                ActionId = link.ActionId,
                RunWhen = link.RunWhen,
                Order = link.Order
            }).ToList());

        HeaderText.Text = $"Linked Action Flow - {rootAction.Name}";
        RebuildTree();
        UpdateActionButtons();
    }

    private void RebuildTree()
    {
        _treeRoots = new ObservableCollection<TreeNode>();
        _treeRoots.Add(BuildActionNode(_rootAction, new HashSet<Guid>()));
        FlowTree.ItemsSource = _treeRoots;
    }

    private TreeNode BuildActionNode(CommandAction action, HashSet<Guid> path)
    {
        var node = new TreeNode
        {
            Kind = NodeKind.Action,
            Title = $"{action.Name} ({action.ActionType})",
            Action = action
        };

        var nextPath = new HashSet<Guid>(path) { action.Id };
        AppendBranchNodes(action, node, nextPath);
        return node;
    }

    private void AppendBranchNodes(CommandAction action, TreeNode parent, HashSet<Guid> path)
    {
        var isConditional = IsConditionalMode(action);
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
                RunWhen = runWhen
            };

            var links = action.LinkedActions
                .Where(link => link.RunWhen == runWhen)
                .OrderBy(link => link.Order)
                .ThenBy(link => link.ActionId)
                .ToList();

            foreach (var link in links)
            {
                var target = TryGetAction(link.ActionId);
                if (target is null)
                {
                    var missingNode = new TreeNode
                    {
                        Kind = NodeKind.LinkedAction,
                        Title = $"-> missing action ({link.ActionId:N})",
                        ParentAction = action,
                        Link = link
                    };
                    branchNode.Children.Add(missingNode);
                    continue;
                }

                var linkNode = new TreeNode
                {
                    Kind = NodeKind.LinkedAction,
                    Title = $"-> {target.Name} ({target.ActionType})",
                    Action = target,
                    ParentAction = action,
                    Link = link
                };

                if (path.Contains(target.Id))
                {
                    linkNode.Children.Add(new TreeNode
                    {
                        Kind = NodeKind.Action,
                        Title = "[cycle]",
                        Action = target
                    });
                }
                else
                {
                    var nextPath = new HashSet<Guid>(path) { target.Id };
                    AppendBranchNodes(target, linkNode, nextPath);
                }

                branchNode.Children.Add(linkNode);
            }

            parent.Children.Add(branchNode);
        }
    }

    private bool IsConditionalMode(CommandAction action)
    {
        if (_forcedConditional.Contains(action.Id))
        {
            return true;
        }

        return action.LinkedActions.Any(link => link.RunWhen != LinkedActionRunWhen.Always);
    }

    private void FlowTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var node = FlowTree.SelectedItem as TreeNode;

        AddExistingButton.IsEnabled = node?.Kind == NodeKind.Branch;
        AddNewButton.IsEnabled = node?.Kind == NodeKind.Branch;
        EditActionButton.IsEnabled = ResolveSelectedEditableLinkedAction(node) is not null;
        RemoveLinkButton.IsEnabled = node?.Kind == NodeKind.LinkedAction && node.Link is not null;

        var selectedAction = ResolveSelectedAction(node);
        RunConditionalButton.IsEnabled = selectedAction is not null;
        RunAlwaysButton.IsEnabled = selectedAction is not null;

        if (node?.Kind == NodeKind.Branch)
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

    private CommandAction? ResolveSelectedAction(TreeNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return node.Kind switch
        {
            NodeKind.Action => node.Action,
            NodeKind.Branch => node.ParentAction,
            NodeKind.LinkedAction => node.ParentAction ?? node.Action,
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
                _externalActions,
                _variableScope,
                _variableChoices,
                _echoReferenceTokens,
                _containerTargetChoices,
                _materializeSourceObjectChoices,
                _procedureChoices,
                _compositeRecipeChoices,
                _soundEffectChoices,
                _timerKeySuggestions)
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
            _editableActions.ToList())
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

        var candidates = _allActionsById.Values
            .Where(action => action.Id != branchNode.ParentAction.Id)
            .Where(action => action.ActionType != CommandActionType.Synonym)
            .OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(action => action.Id)
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

        AddLink(branchNode.ParentAction, branchNode.RunWhen.Value, picker.SelectedAction.Id);
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
        AddLink(branchNode.ParentAction, branchNode.RunWhen.Value, created.Id);
    }

    private void AddLink(CommandAction parentAction, LinkedActionRunWhen runWhen, Guid actionId)
    {
        if (parentAction.Id == actionId)
        {
            System.Windows.MessageBox.Show(this, "An action cannot link to itself.", "Linked Actions", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nextOrder = parentAction.LinkedActions
            .Where(link => link.RunWhen == runWhen)
            .Select(link => link.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        parentAction.LinkedActions.Add(new LinkedActionReference
        {
            ActionId = actionId,
            RunWhen = runWhen,
            Order = nextOrder
        });

        RebuildTree();
    }

    private void RemoveLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (FlowTree.SelectedItem is not TreeNode selected
            || selected.Kind != NodeKind.LinkedAction
            || selected.ParentAction is null
            || selected.Link is null)
        {
            return;
        }

        selected.ParentAction.LinkedActions.Remove(selected.Link);
        RebuildTree();
    }

    private void RunConditionally_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedAction = ResolveSelectedAction(FlowTree.SelectedItem as TreeNode);
        if (selectedAction is null)
        {
            return;
        }

        var alwaysLinks = selectedAction.LinkedActions.Where(link => link.RunWhen == LinkedActionRunWhen.Always).ToList();
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

        _forcedConditional.Add(selectedAction.Id);
        RebuildTree();
    }

    private void RunAlways_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedAction = ResolveSelectedAction(FlowTree.SelectedItem as TreeNode);
        if (selectedAction is null)
        {
            return;
        }

        foreach (var link in selectedAction.LinkedActions)
        {
            link.RunWhen = LinkedActionRunWhen.Always;
        }

        _forcedConditional.Remove(selectedAction.Id);
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

        foreach (var action in _editableActions)
        {
            if (!_originalLinks.TryGetValue(action.Id, out var snapshotLinks))
            {
                action.LinkedActions = new List<LinkedActionReference>();
                continue;
            }

            action.LinkedActions = snapshotLinks.Select(link => new LinkedActionReference
            {
                ActionId = link.ActionId,
                RunWhen = link.RunWhen,
                Order = link.Order
            }).ToList();
        }

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
