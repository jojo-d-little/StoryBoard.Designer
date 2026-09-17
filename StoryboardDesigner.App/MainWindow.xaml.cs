using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Win32;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Views;
using StoryboardDesigner.App.ViewModels;
using WinForms = System.Windows.Forms;

namespace StoryboardDesigner.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly object RecentProjectMenuItemTag = new();
    private static readonly object RecentProjectSeparatorTag = new();

    private readonly MainWindowViewModel _viewModel;
    private readonly IWindowPlacementService _windowPlacementService;
    private const int MaxHierarchySelectionSyncAttempts = 8;
    private bool _isUserHierarchySelectionGestureActive;

    private System.Windows.Controls.TreeView? HierarchyTreeControl => ProjectHierarchyPaneControl?.HierarchyTreeControl;

    public MainWindow(MainWindowViewModel viewModel, IWindowPlacementService windowPlacementService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowPlacementService = windowPlacementService;
        DataContext = _viewModel;

        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
        PreviewKeyDown += MainWindow_OnPreviewKeyDown;
        _viewModel.OutputConsoleLines.CollectionChanged += OutputConsoleLines_OnCollectionChanged;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;

        AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyEditableTextChanged), true);
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler(OnAnyToggleChanged), true);
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent, new RoutedEventHandler(OnAnyToggleChanged), true);
        AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectorChanged), true);
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowPlacementService.TryApply(this, out var leftPaneWidth, out var rightPaneWidth);
        ApplyPaneWidths(leftPaneWidth, rightPaneWidth);
        ScrollOutputConsoleToLatest();
        SyncHierarchyTreeSelectionFromViewModel();
    }

    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (HierarchyTreeControl?.SelectedItem is HierarchyNodeViewModel selectedHierarchyNode)
        {
            _viewModel.SelectedNode = selectedHierarchyNode;
        }

        _viewModel.PersistUiStateSnapshot();
        _viewModel.OutputConsoleLines.CollectionChanged -= OutputConsoleLines_OnCollectionChanged;
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _windowPlacementService.Save(this, HierarchyPaneColumn.ActualWidth, InspectorPaneColumn.ActualWidth);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded
            || !string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedNode), StringComparison.Ordinal))
        {
            return;
        }

        SyncHierarchyTreeSelectionFromViewModel();
    }

    private void SyncHierarchyTreeSelectionFromViewModel()
    {
        if (_viewModel.SelectedNode is not HierarchyNodeViewModel selectedNode)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => TrySyncHierarchyTreeSelection(selectedNode, MaxHierarchySelectionSyncAttempts), DispatcherPriority.Background);
    }

    private void TrySyncHierarchyTreeSelection(HierarchyNodeViewModel selectedNode, int remainingAttempts)
    {
        if (!ReferenceEquals(_viewModel.SelectedNode, selectedNode))
        {
            return;
        }

        var hierarchyTree = HierarchyTreeControl;
        if (hierarchyTree is null)
        {
            return;
        }

        var treeItem = FindTreeViewItemForNode(hierarchyTree, selectedNode);
        if (treeItem is not null)
        {
            if (!treeItem.IsSelected)
            {
                treeItem.IsSelected = true;
            }

            treeItem.BringIntoView();
            return;
        }

        if (remainingAttempts <= 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () => TrySyncHierarchyTreeSelection(selectedNode, remainingAttempts - 1),
            DispatcherPriority.ContextIdle);
    }

    private static TreeViewItem? FindTreeViewItemForNode(ItemsControl parent, HierarchyNodeViewModel target)
    {
        if (parent.ItemContainerGenerator.ContainerFromItem(target) is TreeViewItem directMatch)
        {
            return directMatch;
        }

        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem childContainer)
            {
                continue;
            }

            if (item is HierarchyNodeViewModel childNode
                && ContainsHierarchyNode(childNode, target)
                && !childContainer.IsExpanded)
            {
                childContainer.IsExpanded = true;
            }

            childContainer.UpdateLayout();

            var match = FindTreeViewItemForNode(childContainer, target);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool ContainsHierarchyNode(HierarchyNodeViewModel root, HierarchyNodeViewModel target)
    {
        if (ReferenceEquals(root, target))
        {
            return true;
        }

        foreach (var child in root.Children)
        {
            if (ContainsHierarchyNode(child, target))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyPaneWidths(double? leftPaneWidth, double? rightPaneWidth)
    {
        if (leftPaneWidth.HasValue)
        {
            var clampedLeft = Math.Max(HierarchyPaneColumn.MinWidth, leftPaneWidth.Value);
            HierarchyPaneColumn.Width = new GridLength(clampedLeft, GridUnitType.Pixel);
        }

        if (rightPaneWidth.HasValue)
        {
            var clampedRight = Math.Max(InspectorPaneColumn.MinWidth, rightPaneWidth.Value);
            InspectorPaneColumn.Width = new GridLength(clampedRight, GridUnitType.Pixel);
        }
    }

    internal void SharedIndicator_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GamePropertyNodeViewModel variableNode })
        {
            return;
        }

        _viewModel.SelectedNode = variableNode;
        _viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode);
        e.Handled = true;
    }

    internal void BeginHierarchySelectionGesture()
    {
        _isUserHierarchySelectionGestureActive = true;
    }

    internal void EndHierarchySelectionGesture()
    {
        _isUserHierarchySelectionGestureActive = false;
    }

    private void OutputConsoleLines_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        ScrollOutputConsoleToLatest();
    }

    private void ScrollOutputConsoleToLatest()
    {
        var outputListBox = OutputConsolePaneControl?.OutputListBoxControl;
        if (outputListBox is null || outputListBox.Items.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (outputListBox.Items.Count == 0)
            {
                return;
            }

            var lastItem = outputListBox.Items[outputListBox.Items.Count - 1];
            outputListBox.ScrollIntoView(lastItem);
        }, DispatcherPriority.Background);
    }

    private void OnAnyEditableTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || e.OriginalSource is not System.Windows.Controls.TextBox textBox || !textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        _viewModel.NotifyProjectEdited();
    }

    private void OnAnyToggleChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || e.OriginalSource is not System.Windows.Controls.Primitives.ToggleButton toggle || !toggle.IsKeyboardFocusWithin)
        {
            return;
        }

        _viewModel.NotifyProjectEdited();
    }

    private void OnAnySelectorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.OriginalSource is not System.Windows.Controls.Primitives.Selector selector || !selector.IsKeyboardFocusWithin)
        {
            return;
        }

        _viewModel.NotifyProjectEdited();
    }

    internal void HierarchyTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is HierarchyNodeViewModel candidateNode
            && !_isUserHierarchySelectionGestureActive
            && _viewModel.SelectedNode is not null
            && !ReferenceEquals(_viewModel.SelectedNode, candidateNode))
        {
            // During initial tree materialization WPF may raise non-user selection events.
            // Keep the restored selection unless the change is user initiated.
            SyncHierarchyTreeSelectionFromViewModel();
            return;
        }

        if (e.NewValue is HierarchyNodeViewModel node)
        {
            _viewModel.SelectedNode = node;
            var isUserInitiatedSelection = _isUserHierarchySelectionGestureActive;

            if (isUserInitiatedSelection && node is GlobalSettingsNodeViewModel globalSettingsNode)
            {
                _viewModel.EditGlobalSettings(globalSettingsNode.ProjectNode);
            }

            if (isUserInitiatedSelection && node is PlanetSettingsNodeViewModel planetSettingsNode)
            {
                _viewModel.EditPlanetSettings(planetSettingsNode.PlanetNode);
            }

            if (isUserInitiatedSelection && node is CountrySettingsNodeViewModel countrySettingsNode)
            {
                _viewModel.EditCountrySettings(countrySettingsNode.CountryNode);
            }

            if (isUserInitiatedSelection && node is AreaSettingsNodeViewModel areaSettingsNode)
            {
                _viewModel.EditAreaBasicProperties(areaSettingsNode.AreaNode);
            }

            SyncPropertiesPaneFocus(node);
        }
    }

    private void SyncPropertiesPaneFocus(HierarchyNodeViewModel node)
    {
        _ = node;
    }

    private void FileMenu_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem fileMenu)
        {
            return;
        }

        for (var index = fileMenu.Items.Count - 1; index >= 0; index--)
        {
            if (fileMenu.Items[index] is MenuItem menuItem
                && ReferenceEquals(menuItem.Tag, RecentProjectMenuItemTag))
            {
                fileMenu.Items.RemoveAt(index);
                continue;
            }

            if (fileMenu.Items[index] is Separator separator
                && ReferenceEquals(separator.Tag, RecentProjectSeparatorTag))
            {
                fileMenu.Items.RemoveAt(index);
            }
        }

        if (_viewModel.RecentProjects.Count == 0)
        {
            return;
        }

        fileMenu.Items.Add(new Separator
        {
            Tag = RecentProjectSeparatorTag
        });

        foreach (var path in _viewModel.RecentProjects.Take(5))
        {
            var item = new MenuItem { Header = path, Tag = RecentProjectMenuItemTag };
            item.Command = _viewModel.OpenRecentProjectCommand;
            item.CommandParameter = path;
            fileMenu.Items.Add(item);
        }
    }


    private void TraversalLegMapJump_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TraversalLegNodeViewModel legNode)
        {
            return;
        }

        _viewModel.SelectedNode = legNode;
        _viewModel.ExecuteTreeContextAction("open-traversal-map", legNode);
        e.Handled = true;
    }

    internal void AreaMapFit_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject source)
        {
            return;
        }

        var host = FindAncestor<DockPanel>(source);
        if (host is null)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(host);
        if (scrollViewer is null)
        {
            return;
        }

        var viewportWidth = Math.Max(0, scrollViewer.ViewportWidth - 16);
        var viewportHeight = Math.Max(0, scrollViewer.ViewportHeight - 16);
        _viewModel.FitSelectedAreaMap(viewportWidth, viewportHeight);
    }

    private void MainWindow_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
            && e.Key == Key.R
            && Keyboard.FocusedElement is DependencyObject focusedElement
            && HasAncestorDataContext<HierarchyNodeViewModel>(focusedElement)
            && _viewModel.SelectedNode is GamePropertyNodeViewModel variableNode)
        {
            _viewModel.ExecuteTreeContextAction("review-shared-property-relationships", variableNode);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        if (AreaMapDesignerWorkspaceControl?.ClearPendingDestinationMode() == true)
        {
            e.Handled = true;
            return;
        }

        if (_viewModel.TryHandleEscapeAction())
        {
            e.Handled = true;
        }
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

    private static bool HasAncestorDataContext<T>(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is FrameworkElement element && element.DataContext is T)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static T? FindDescendant<T>(DependencyObject? current) where T : DependencyObject
    {
        if (current is null)
        {
            return null;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(current);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(current, index);
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    public void RestoreHierarchyFocusToSelection()
    {
        var hierarchyTree = HierarchyTreeControl;
        if (hierarchyTree is null)
        {
            return;
        }

        if (_viewModel.SelectedNode is null)
        {
            hierarchyTree.Focus();
            return;
        }

        hierarchyTree.UpdateLayout();
        var selectedItem = FindTreeViewItemByDataContext(hierarchyTree, _viewModel.SelectedNode);
        if (selectedItem is null)
        {
            hierarchyTree.Focus();
            return;
        }

        selectedItem.IsSelected = true;
        selectedItem.Focus();
    }

    private static TreeViewItem? FindTreeViewItemByDataContext(ItemsControl parent, object dataContext)
    {
        foreach (var item in parent.Items)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container is null)
            {
                continue;
            }

            if (ReferenceEquals(container.DataContext, dataContext))
            {
                return container;
            }

            if (container.Items.Count > 0)
            {
                var wasExpanded = container.IsExpanded;
                if (!wasExpanded)
                {
                    container.IsExpanded = true;
                    container.UpdateLayout();
                }

                var nested = FindTreeViewItemByDataContext(container, dataContext);
                if (nested is not null)
                {
                    return nested;
                }

                if (!wasExpanded)
                {
                    container.IsExpanded = false;
                }
            }
        }

        return null;
    }
}
