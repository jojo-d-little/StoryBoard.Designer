using System.ComponentModel;
using System.Windows;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views.Controls;

/// <summary>
/// Project-specific hierarchy recents and bookmarks.
/// </summary>
public partial class QuickAccessPane : System.Windows.Controls.UserControl
{
    private double _recentSectionHeightAtDragStart;
    private double _bookmarksSectionHeightAtDragStart;

    public QuickAccessPane()
    {
        InitializeComponent();
        Loaded += QuickAccessPane_OnLoaded;
        DataContextChanged += QuickAccessPane_OnDataContextChanged;
    }

    private void QuickAccessPane_OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPersistedSplitRatio();
    }

    private void QuickAccessPane_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        if (e.NewValue is MainWindowViewModel newViewModel)
        {
            newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyPersistedSplitRatio();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.QuickAccessRecentSectionRatio), StringComparison.Ordinal))
        {
            ApplyPersistedSplitRatio();
        }
    }

    private void ApplyPersistedSplitRatio()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var ratio = viewModel.QuickAccessRecentSectionRatio;
        RecentSectionRow.Height = new GridLength(ratio, GridUnitType.Star);
        BookmarksSectionRow.Height = new GridLength(1 - ratio, GridUnitType.Star);
    }

    private void SectionSplitter_OnDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _recentSectionHeightAtDragStart = RecentSectionRow.ActualHeight;
        _bookmarksSectionHeightAtDragStart = BookmarksSectionRow.ActualHeight;
    }

    private void SectionSplitter_OnDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var combinedHeight = _recentSectionHeightAtDragStart + _bookmarksSectionHeightAtDragStart;
        if (combinedHeight <= 0)
        {
            return;
        }

        viewModel.QuickAccessRecentSectionRatio = CalculateCompletedSplitRatio(
            _recentSectionHeightAtDragStart,
            _bookmarksSectionHeightAtDragStart,
            e.VerticalChange,
            RecentSectionRow.MinHeight,
            BookmarksSectionRow.MinHeight);
    }

    internal static double CalculateCompletedSplitRatio(
        double recentHeightAtDragStart,
        double bookmarksHeightAtDragStart,
        double verticalChange,
        double minimumRecentHeight,
        double minimumBookmarksHeight)
    {
        var combinedHeight = recentHeightAtDragStart + bookmarksHeightAtDragStart;
        var maximumRecentHeight = combinedHeight - minimumBookmarksHeight;
        if (combinedHeight <= 0 || maximumRecentHeight < minimumRecentHeight)
        {
            return 0.5;
        }

        var completedRecentHeight = Math.Clamp(
            recentHeightAtDragStart + verticalChange,
            minimumRecentHeight,
            maximumRecentHeight);
        return completedRecentHeight / combinedHeight;
    }
}
