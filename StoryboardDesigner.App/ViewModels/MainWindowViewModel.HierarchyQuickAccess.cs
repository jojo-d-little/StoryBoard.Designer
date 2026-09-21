using System.Windows.Input;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const int MaximumRecentHierarchyNodeCount = 10;

    private RelayCommand _addHierarchyBookmarkCommand = null!;
    private RelayCommandOfT<HierarchyQuickAccessItemViewModel> _navigateToHierarchyQuickAccessItemCommand = null!;
    private RelayCommandOfT<HierarchyQuickAccessItemViewModel> _removeHierarchyBookmarkCommand = null!;
    private RelayCommandOfT<HierarchyQuickAccessItemViewModel> _moveHierarchyBookmarkUpCommand = null!;
    private RelayCommandOfT<HierarchyQuickAccessItemViewModel> _moveHierarchyBookmarkDownCommand = null!;

    public ICommand AddHierarchyBookmarkCommand => _addHierarchyBookmarkCommand;
    public ICommand NavigateToHierarchyQuickAccessItemCommand => _navigateToHierarchyQuickAccessItemCommand;
    public ICommand RemoveHierarchyBookmarkCommand => _removeHierarchyBookmarkCommand;
    public ICommand MoveHierarchyBookmarkUpCommand => _moveHierarchyBookmarkUpCommand;
    public ICommand MoveHierarchyBookmarkDownCommand => _moveHierarchyBookmarkDownCommand;

    private void InitializeHierarchyQuickAccessCommands()
    {
        _addHierarchyBookmarkCommand = new RelayCommand(AddSelectedHierarchyNodeBookmark, CanAddSelectedHierarchyNodeBookmark);
        _navigateToHierarchyQuickAccessItemCommand = new RelayCommandOfT<HierarchyQuickAccessItemViewModel>(
            NavigateToHierarchyQuickAccessItem,
            item => item?.IsAvailable == true);
        _removeHierarchyBookmarkCommand = new RelayCommandOfT<HierarchyQuickAccessItemViewModel>(
            RemoveHierarchyBookmark,
            item => FindBookmarkIndex(item) >= 0);
        _moveHierarchyBookmarkUpCommand = new RelayCommandOfT<HierarchyQuickAccessItemViewModel>(
            item => MoveHierarchyBookmark(item, -1),
            item => FindBookmarkIndex(item) > 0);
        _moveHierarchyBookmarkDownCommand = new RelayCommandOfT<HierarchyQuickAccessItemViewModel>(
            item => MoveHierarchyBookmark(item, 1),
            item =>
            {
                var index = FindBookmarkIndex(item);
                return index >= 0 && index < _project.UiState.HierarchyBookmarks.Count - 1;
            });
    }

    private void TrackRecentHierarchyNode(HierarchyNodeViewModel? node)
    {
        if (_suspendUiStatePersistence || node is null)
        {
            return;
        }

        var entry = CreateQuickAccessEntry(node);
        var recentNodes = _project.UiState.RecentHierarchyNodes ??= [];
        recentNodes.RemoveAll(existing => PathsMatch(existing.NodePath, entry.NodePath));
        recentNodes.Insert(0, entry);
        if (recentNodes.Count > MaximumRecentHierarchyNodeCount)
        {
            recentNodes.RemoveRange(MaximumRecentHierarchyNodeCount, recentNodes.Count - MaximumRecentHierarchyNodeCount);
        }

        RefreshHierarchyQuickAccessItems();
    }

    private bool CanAddSelectedHierarchyNodeBookmark()
    {
        if (SelectedNode is null)
        {
            return false;
        }

        var selectedPath = BuildNodePath(SelectedNode);
        return !_project.UiState.HierarchyBookmarks.Any(entry => PathsMatch(entry.NodePath, selectedPath));
    }

    private void AddSelectedHierarchyNodeBookmark()
    {
        if (SelectedNode is null || !CanAddSelectedHierarchyNodeBookmark())
        {
            return;
        }

        _project.UiState.HierarchyBookmarks.Add(CreateQuickAccessEntry(SelectedNode));
        RefreshHierarchyQuickAccessItems();
        PersistUiStateSidecarIfPossible();
    }

    private void NavigateToHierarchyQuickAccessItem(HierarchyQuickAccessItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var target = FindHierarchyNode(item.NodePath);
        if (target is null)
        {
            RefreshHierarchyQuickAccessItems();
            return;
        }

        ExpandToNode(target);
        if (ReferenceEquals(SelectedNode, target))
        {
            OnPropertyChanged(nameof(SelectedNode));
        }
        else
        {
            SelectedNode = target;
        }
    }

    private void RemoveHierarchyBookmark(HierarchyQuickAccessItemViewModel? item)
    {
        var index = FindBookmarkIndex(item);
        if (index < 0)
        {
            return;
        }

        _project.UiState.HierarchyBookmarks.RemoveAt(index);
        RefreshHierarchyQuickAccessItems();
        PersistUiStateSidecarIfPossible();
    }

    private void MoveHierarchyBookmark(HierarchyQuickAccessItemViewModel? item, int offset)
    {
        var currentIndex = FindBookmarkIndex(item);
        var targetIndex = currentIndex + offset;
        var bookmarks = _project.UiState.HierarchyBookmarks;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= bookmarks.Count)
        {
            return;
        }

        (bookmarks[currentIndex], bookmarks[targetIndex]) = (bookmarks[targetIndex], bookmarks[currentIndex]);
        RefreshHierarchyQuickAccessItems();
        PersistUiStateSidecarIfPossible();
    }

    private int FindBookmarkIndex(HierarchyQuickAccessItemViewModel? item)
    {
        if (item is null)
        {
            return -1;
        }

        return _project.UiState.HierarchyBookmarks.FindIndex(entry => PathsMatch(entry.NodePath, item.NodePath));
    }

    private void RefreshHierarchyQuickAccessItems()
    {
        var recentNodes = _project.UiState.RecentHierarchyNodes ??= [];
        var resolvedRecentEntries = new List<ProjectHierarchyQuickAccessEntry>();
        DisposeQuickAccessItems(RecentHierarchyNodes);
        RecentHierarchyNodes.Clear();
        foreach (var entry in recentNodes.Take(MaximumRecentHierarchyNodeCount))
        {
            var node = FindHierarchyNode(entry.NodePath);
            if (node is null || resolvedRecentEntries.Any(existing => PathsMatch(existing.NodePath, entry.NodePath)))
            {
                continue;
            }

            var refreshedEntry = CreateQuickAccessEntry(node);
            resolvedRecentEntries.Add(refreshedEntry);
            RecentHierarchyNodes.Add(CreateQuickAccessItem(refreshedEntry, node, persistDisplayNameChange: false));
        }

        _project.UiState.RecentHierarchyNodes = resolvedRecentEntries;

        DisposeQuickAccessItems(HierarchyBookmarks);
        HierarchyBookmarks.Clear();
        foreach (var entry in _project.UiState.HierarchyBookmarks ??= [])
        {
            var node = FindHierarchyNode(entry.NodePath);
            if (node is not null)
            {
                entry.DisplayName = node.EditableName;
                entry.NodeTypeLabel = node.NodeTypeLabel;
            }

            HierarchyBookmarks.Add(CreateQuickAccessItem(entry, node, persistDisplayNameChange: true));
        }

        RefreshHierarchyQuickAccessCommandStates();
    }

    private void RefreshHierarchyQuickAccessCommandStates()
    {
        _addHierarchyBookmarkCommand?.RaiseCanExecuteChanged();
        _navigateToHierarchyQuickAccessItemCommand?.RaiseCanExecuteChanged();
        _removeHierarchyBookmarkCommand?.RaiseCanExecuteChanged();
        _moveHierarchyBookmarkUpCommand?.RaiseCanExecuteChanged();
        _moveHierarchyBookmarkDownCommand?.RaiseCanExecuteChanged();
    }

    private HierarchyNodeViewModel? FindHierarchyNode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var candidates = BuildRestorePathCandidates(path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return EnumerateHierarchyNodes(HierarchyRoots).FirstOrDefault(node =>
            candidates.Contains(BuildNodePath(node))
            || candidates.Contains(NormalizeNodePathForGroupingCompatibility(BuildNodePath(node)))
            || candidates.Contains(BuildLegacyNodePath(node)));
    }

    private static bool PathsMatch(string left, string right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectHierarchyQuickAccessEntry CreateQuickAccessEntry(HierarchyNodeViewModel node)
    {
        return new ProjectHierarchyQuickAccessEntry
        {
            NodePath = BuildNodePath(node),
            DisplayName = node.EditableName,
            NodeTypeLabel = node.NodeTypeLabel
        };
    }

    private HierarchyQuickAccessItemViewModel CreateQuickAccessItem(
        ProjectHierarchyQuickAccessEntry entry,
        HierarchyNodeViewModel? node,
        bool persistDisplayNameChange)
    {
        var displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Unnamed node" : entry.DisplayName;
        var nodeType = string.IsNullOrWhiteSpace(entry.NodeTypeLabel) ? "Hierarchy node" : entry.NodeTypeLabel;
        var context = node is null ? "Unavailable" : BuildQuickAccessContext(node);
        if (node is null)
        {
            return new HierarchyQuickAccessItemViewModel(entry.NodePath, displayName, nodeType, context, isAvailable: false);
        }

        return new HierarchyQuickAccessItemViewModel(
            entry.NodePath,
            displayName,
            nodeType,
            context,
            node,
            updatedName =>
            {
                entry.DisplayName = updatedName;
                if (persistDisplayNameChange)
                {
                    PersistUiStateSidecarIfPossible();
                }
            });
    }

    private static void DisposeQuickAccessItems(IEnumerable<HierarchyQuickAccessItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.Dispose();
        }
    }

    private static string BuildQuickAccessContext(HierarchyNodeViewModel node)
    {
        var ancestors = GetAncestry(node)
            .Skip(1)
            .Reverse()
            .Select(ancestor => ancestor.EditableName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .TakeLast(3);
        return string.Join(" > ", ancestors);
    }
}
