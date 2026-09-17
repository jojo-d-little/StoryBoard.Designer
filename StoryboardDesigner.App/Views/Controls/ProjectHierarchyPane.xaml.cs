using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Views;

namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Interaction logic for ProjectHierarchyPane.xaml
/// </summary>
public partial class ProjectHierarchyPane : System.Windows.Controls.UserControl
{
    private HierarchyNodeViewModel? _contextNode;
    private System.Windows.Point? _hierarchyDragStart;
    private RoomNodeViewModel? _pendingHierarchyDragRoom;
    private TreeViewItem? _pendingHierarchyDragSource;
    private bool _isHierarchyDragInProgress;

    public ProjectHierarchyPane()
    {
        InitializeComponent();
    }

    public System.Windows.Controls.TreeView HierarchyTreeControl => HierarchyTree;

    private MainWindowViewModel? GetViewModel() => DataContext as MainWindowViewModel;

    private MainWindow? GetHostWindow() => Window.GetWindow(this) as MainWindow;

    private void HierarchyTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        GetHostWindow()?.HierarchyTree_OnSelectedItemChanged(sender, e);
    }

    private void HierarchyTree_OnPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GetViewModel() is not { } viewModel)
        {
            return;
        }

        var tree = sender as System.Windows.Controls.TreeView;
        var treeViewItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) ?? FindTreeViewItemAtPointer(tree, e.GetPosition(tree));
        if (treeViewItem?.DataContext is HierarchyNodeViewModel node)
        {
            _contextNode = node;
            viewModel.SelectedNode = node;
            treeViewItem.IsSelected = true;
        }
        else
        {
            _contextNode = null;
        }
    }

    private void HierarchyTree_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (GetViewModel() is not { } viewModel)
        {
            return;
        }

        if (sender is not System.Windows.Controls.TreeView tree)
        {
            return;
        }

        var pointerPosition = Mouse.GetPosition(tree);
        var pointerItem = FindTreeViewItemAtPointer(tree, pointerPosition);
        if (pointerItem?.DataContext is HierarchyNodeViewModel pointerNode)
        {
            _contextNode = pointerNode;
            viewModel.SelectedNode = pointerNode;
            pointerItem.IsSelected = true;
        }

        var node = _contextNode ?? viewModel.SelectedNode;
        if (node is null)
        {
            tree.ContextMenu = null;
            return;
        }

        var actions = viewModel.GetTreeContextActions(node);
        if (actions.Count == 0)
        {
            tree.ContextMenu = null;
            return;
        }

        var menu = new ContextMenu();
        foreach (var action in actions)
        {
            var item = new MenuItem
            {
                Header = action.Header,
                Command = viewModel.ExecuteTreeContextActionCommand,
                CommandParameter = new TreeContextActionRequest(action.ActionId, node),
                IsEnabled = action.IsEnabled
            };
            menu.Items.Add(item);
        }

        tree.ContextMenu = menu;
    }

    private void HierarchyTree_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GetViewModel() is not { } viewModel)
        {
            return;
        }

        var tree = sender as System.Windows.Controls.TreeView;
        var treeViewItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)
                           ?? FindTreeViewItemAtPointer(tree, e.GetPosition(tree));
        if (treeViewItem?.DataContext is RoomNodeViewModel roomNode)
        {
            viewModel.SelectedNode = roomNode;
            viewModel.OpenRoomEditor(roomNode.Room);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is TemplateRoomNodeViewModel templateRoomNode)
        {
            viewModel.SelectedNode = templateRoomNode;
            viewModel.OpenRoomEditor(templateRoomNode.Room);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is RoomGameObjectsNodeViewModel objectsNode)
        {
            viewModel.SelectedNode = objectsNode;
            viewModel.OpenRoomEditor(objectsNode.RoomNode.Room);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is TraversalLegNodeViewModel traversalLegNode)
        {
            viewModel.SelectedNode = traversalLegNode;
            viewModel.ExecuteTreeContextAction("open-traversal-map", traversalLegNode);
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                "Structural traversal edits are done in the area map designer.\n\nUse the map to add/remove connections.",
                "Traversal Leg Guidance",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is AreaNodeViewModel areaNode)
        {
            viewModel.SelectedNode = areaNode;
            viewModel.OpenAreaEditor(areaNode.Area);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is RoomSettingsNodeViewModel roomSettingsNode)
        {
            viewModel.SelectedNode = roomSettingsNode;
            if (roomSettingsNode.RoomNode is not null)
            {
                viewModel.EditRoomSettings(roomSettingsNode.RoomNode);
            }
            else if (roomSettingsNode.TemplateRoomNode is not null)
            {
                viewModel.EditRoomSettings(roomSettingsNode.TemplateRoomNode);
            }

            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is GameObjectSettingsNodeViewModel objectSettingsNode)
        {
            viewModel.SelectedNode = objectSettingsNode;
            if (objectSettingsNode.ObjectNode is GameObjectNodeViewModel interactiveObjectNode)
            {
                viewModel.EditObjectBasicProperties(interactiveObjectNode);
            }
            else if (objectSettingsNode.ObjectNode is GlobalObjectNodeViewModel playerObjectNode)
            {
                viewModel.EditObjectBasicProperties(playerObjectNode);
            }
            else if (objectSettingsNode.ObjectNode is TemplateGameObjectNodeViewModel templateObjectNode)
            {
                viewModel.EditObjectBasicProperties(templateObjectNode);
            }

            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is GamePropertyNodeViewModel variableNode)
        {
            viewModel.SelectedNode = variableNode;
            ShowVariableEditor(viewModel, variableNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is GamePropertiesContainerNodeViewModel gamePropertiesNode)
        {
            viewModel.SelectedNode = gamePropertiesNode;
            // Keep default tree expand/collapse behavior; edit-all is available on context menu.
            return;
        }

        if (treeViewItem?.DataContext is ScopedActionsNodeViewModel scopedActionsNode)
        {
            viewModel.SelectedNode = scopedActionsNode;
            // Keep default tree expand/collapse behavior; edit-all is available on context menu.
            return;
        }

        if (treeViewItem?.DataContext is ScopedVerbsNodeViewModel verbsNode)
        {
            viewModel.SelectedNode = verbsNode;
            // Keep default tree expand/collapse behavior for the verbs folder.
            return;
        }

        var parentVerbsNode = FindOwningScopedVerbsNode(treeViewItem);
        if (parentVerbsNode is not null)
        {
            viewModel.SelectedNode = parentVerbsNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-verbs", parentVerbsNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedDirectionalsNodeViewModel directionalsNode)
        {
            viewModel.SelectedNode = directionalsNode;
            // Keep default tree expand/collapse behavior for the directionals folder.
            return;
        }

        if (treeViewItem?.DataContext is ScopedSoundEffectsNodeViewModel soundEffectsNode)
        {
            viewModel.SelectedNode = soundEffectsNode;
            viewModel.ExecuteTreeContextAction("open-all-scoped-sound-effects", soundEffectsNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedSoundEffectEntryNodeViewModel soundEffectEntryNode)
        {
            viewModel.SelectedNode = soundEffectEntryNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-sound-effect-entry", soundEffectEntryNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedEventSubscriptionsNodeViewModel eventSubscriptionsNode)
        {
            viewModel.SelectedNode = eventSubscriptionsNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-event-subscriptions", eventSubscriptionsNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedEventSubscriptionEntryNodeViewModel eventSubscriptionEntryNode)
        {
            viewModel.SelectedNode = eventSubscriptionEntryNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-event-subscription-entry", eventSubscriptionEntryNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedTimerDefinitionsNodeViewModel timerDefinitionsNode)
        {
            viewModel.SelectedNode = timerDefinitionsNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-timer-definitions", timerDefinitionsNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedTimerDefinitionEntryNodeViewModel timerDefinitionEntryNode)
        {
            viewModel.SelectedNode = timerDefinitionEntryNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-timer-definition-entry", timerDefinitionEntryNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedProceduresNodeViewModel proceduresNode)
        {
            viewModel.SelectedNode = proceduresNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-procedures", proceduresNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedProcedureEntryNodeViewModel procedureNode)
        {
            viewModel.SelectedNode = procedureNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-procedures", procedureNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is PhaseNodeViewModel phaseNode)
        {
            viewModel.SelectedNode = phaseNode;
            viewModel.ExecuteTreeContextAction("edit-phase-node", phaseNode);
            e.Handled = true;
            return;
        }

        var parentDirectionalsNode = FindOwningScopedDirectionalsNode(treeViewItem);
        if (parentDirectionalsNode is not null)
        {
            viewModel.SelectedNode = parentDirectionalsNode;
            viewModel.ExecuteTreeContextAction("edit-scoped-directionals", parentDirectionalsNode);
            e.Handled = true;
            return;
        }

        if (treeViewItem?.DataContext is ScopedActionEntryNodeViewModel scopedActionEntry)
        {
            viewModel.SelectedNode = scopedActionEntry;
            viewModel.ExecuteTreeContextAction("edit-scoped-action", scopedActionEntry);
            e.Handled = true;
        }
    }

    private void HierarchyTree_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var hostWindow = GetHostWindow();
        hostWindow?.BeginHierarchySelectionGesture();

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _hierarchyDragStart = null;
            _pendingHierarchyDragRoom = null;
            _pendingHierarchyDragSource = null;
            return;
        }

        _hierarchyDragStart = e.GetPosition(this);

        var treeViewItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        _pendingHierarchyDragSource = treeViewItem;
        _pendingHierarchyDragRoom = treeViewItem?.DataContext as RoomNodeViewModel;

        Dispatcher.BeginInvoke(() => hostWindow?.EndHierarchySelectionGesture(), DispatcherPriority.Input);
    }

    private void HierarchyTree_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var hostWindow = GetHostWindow();
        hostWindow?.BeginHierarchySelectionGesture();
        Dispatcher.BeginInvoke(() => hostWindow?.EndHierarchySelectionGesture(), DispatcherPriority.Input);
    }

    private void HierarchyTree_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _hierarchyDragStart = null;
            _pendingHierarchyDragRoom = null;
            _pendingHierarchyDragSource = null;
            return;
        }

        if (_isHierarchyDragInProgress || _pendingHierarchyDragRoom is null || _hierarchyDragStart is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = Math.Abs(current.X - _hierarchyDragStart.Value.X);
        var deltaY = Math.Abs(current.Y - _hierarchyDragStart.Value.Y);
        if (deltaX < SystemParameters.MinimumHorizontalDragDistance && deltaY < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var dragSource = _pendingHierarchyDragSource ?? sender as DependencyObject;
        if (dragSource is null)
        {
            return;
        }

        try
        {
            _isHierarchyDragInProgress = true;
            DragDrop.DoDragDrop(dragSource, new System.Windows.DataObject(typeof(RoomNodeViewModel), _pendingHierarchyDragRoom), System.Windows.DragDropEffects.Copy);
        }
        catch (InvalidOperationException)
        {
            // Swallow transient drag-drop dispatcher state errors and allow the app to continue.
        }
        finally
        {
            _isHierarchyDragInProgress = false;
            _hierarchyDragStart = null;
            _pendingHierarchyDragRoom = null;
            _pendingHierarchyDragSource = null;
        }
    }

    private void SharedIndicator_OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        GetHostWindow()?.SharedIndicator_OnMouseLeftButtonUp(sender, e);
    }

    private static TreeViewItem? FindTreeViewItemAtPointer(System.Windows.Controls.TreeView? tree, System.Windows.Point pointerPosition)
    {
        if (tree is null)
        {
            return null;
        }

        var hit = tree.InputHitTest(pointerPosition) as DependencyObject;
        return FindAncestor<TreeViewItem>(hit);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static ScopedVerbsNodeViewModel? FindOwningScopedVerbsNode(TreeViewItem? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item?.DataContext is HierarchyNodeViewModel hierarchyNode
            && hierarchyNode.Parent is ScopedVerbsNodeViewModel parentNode)
        {
            return parentNode;
        }

        var currentParent = FindAncestor<TreeViewItem>(VisualTreeHelper.GetParent(item));
        while (currentParent is not null)
        {
            if (currentParent.DataContext is ScopedVerbsNodeViewModel verbsNode)
            {
                return verbsNode;
            }

            currentParent = FindAncestor<TreeViewItem>(VisualTreeHelper.GetParent(currentParent));
        }

        return null;
    }

    private static ScopedDirectionalsNodeViewModel? FindOwningScopedDirectionalsNode(TreeViewItem? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item?.DataContext is HierarchyNodeViewModel hierarchyNode
            && hierarchyNode.Parent is ScopedDirectionalsNodeViewModel parentNode)
        {
            return parentNode;
        }

        var currentParent = FindAncestor<TreeViewItem>(VisualTreeHelper.GetParent(item));
        while (currentParent is not null)
        {
            if (currentParent.DataContext is ScopedDirectionalsNodeViewModel directionalsNode)
            {
                return directionalsNode;
            }

            currentParent = FindAncestor<TreeViewItem>(VisualTreeHelper.GetParent(currentParent));
        }

        return null;
    }

    private void ShowVariableEditor(MainWindowViewModel viewModel, GamePropertyNodeViewModel variableNode)
    {
        var siblingNames = viewModel.GetVariableNamesReservedInScope(variableNode);

        var dialog = new VariableEditorDialog(
            variableNode.Variable.Name,
            variableNode.Variable.Lifetime,
            variableNode.Variable.DefaultValue,
            variableNode.Variable.ValueRestriction,
            siblingNames,
            _ => viewModel.OpenSharedRelationshipManager(variableNode))
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        variableNode.Variable.Name = dialog.VariableName;
        variableNode.Variable.Lifetime = dialog.Lifetime;
        variableNode.Variable.DefaultValue = dialog.DefaultValue;
        variableNode.Variable.ValueRestriction = dialog.ValueRestriction;
        variableNode.EditableName = dialog.VariableName;
        viewModel.RefreshVariableNameIndex(variableNode);
        viewModel.NotifyProjectEdited();
    }
}
