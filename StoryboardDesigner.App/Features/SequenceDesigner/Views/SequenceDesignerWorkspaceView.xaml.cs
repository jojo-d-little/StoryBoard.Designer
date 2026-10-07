namespace StoryboardDesigner.App.Features.SequenceDesigner.Views;

public partial class SequenceDesignerWorkspaceView : System.Windows.Controls.UserControl
{
    private readonly Dictionary<System.Windows.Controls.Button, System.Windows.Point> _sequenceCardDragStarts = new();
    private readonly Dictionary<System.Windows.Controls.ComboBox, SequenceRouteTargetEditState> _sequenceRouteTargetEdits = new();
    private bool _flowConnectionUpdatePending;
    private bool _isDrawingFlowConnections;
    private System.Diagnostics.Stopwatch? _flowConnectionInvalidationElapsed;
    private double? _flowInvalidationToNormalPriorityMs;
    private double? _flowInvalidationToDataBindPriorityMs;
    private long? _sequenceDropEventCompletedTimestamp;
    private double? _sequenceDropToDragReturnMs;
    private string _lastFlowConnectionLayout = string.Empty;
    private int _unchangedFlowLayoutPassCount;
    private double _unchangedFlowLayoutTotalMs;
    private double _unchangedFlowVisualScanTotalMs;

    private sealed record SequenceRouteTargetEditState(Guid SourceStepId, string RouteKind, Guid? OriginalTargetStepId);

    public SequenceDesignerWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += SequenceDesignerWorkspaceView_OnDataContextChanged;
        Loaded += (_, _) => InvalidateSequenceFlowConnections();
    }

    private void SequenceDesignerWorkspaceView_OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is StoryboardDesigner.App.ViewModels.MainWindowViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= SequenceViewModel_OnPropertyChanged;
        }

        if (e.NewValue is StoryboardDesigner.App.ViewModels.MainWindowViewModel newViewModel)
        {
            newViewModel.PropertyChanged += SequenceViewModel_OnPropertyChanged;
        }

        InvalidateSequenceFlowConnections();
    }

    private void SequenceViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimeSlices))
        {
            InvalidateSequenceFlowConnections();
        }
    }

    private void ZoomIn_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            viewModel.SetSequenceZoom(viewModel.SequenceZoom * 1.2d);
        }
    }

    private void ZoomOut_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            viewModel.SetSequenceZoom(viewModel.SequenceZoom / 1.2d);
        }
    }

    private void FitSequence_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            return;
        }

        SequenceCanvasScrollViewer.UpdateLayout();
        var currentZoom = Math.Max(0.2d, viewModel.SequenceZoom);
        var contentWidth = SequenceCanvasContent.DesiredSize.Width / currentZoom;
        var contentHeight = (SequenceTimelineRuler.DesiredSize.Height + SequenceBranchContent.DesiredSize.Height) / currentZoom;
        if (contentWidth <= 0 || contentHeight <= 0 || SequenceCanvasScrollViewer.ViewportWidth <= 0 || SequenceCanvasScrollViewer.ViewportHeight <= 0)
        {
            viewModel.SetSequenceZoom(1d);
            return;
        }

        var fitScale = Math.Min(
            (SequenceCanvasScrollViewer.ViewportWidth - 24d) / contentWidth,
            (SequenceCanvasScrollViewer.ViewportHeight - 24d) / contentHeight);
        viewModel.SetSequenceZoom(Math.Min(1d, fitScale));
        SequenceCanvasScrollViewer.ScrollToHorizontalOffset(0d);
        SequenceBranchScrollViewer.ScrollToVerticalOffset(0d);
    }

    private void SequenceCard_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        if (e.ClickCount == 2
            && button.DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineCardViewModel card
            && card.Step.StepType == Storyboard.Shared.RuntimeContracts.Enums.SequenceStepType.Action
            && DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            _sequenceCardDragStarts.Remove(button);
            viewModel.SelectSequenceStepCommand.Execute(card.Step);
            e.Handled = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (viewModel.EditSequenceActionCommand.CanExecute(null))
                {
                    viewModel.EditSequenceActionCommand.Execute(null);
                }
            }));
            return;
        }

        _sequenceCardDragStarts[button] = e.GetPosition(button);
    }

    private void SequenceStepTiming_OnLostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            viewModel.CommitSelectedSequenceStepTiming();
        }
    }

    private void SequenceTargetComboBox_OnDropDownOpened(object sender, System.EventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox comboBox
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            || viewModel.SelectedSequenceStep is not { } source
            || comboBox.Tag is not string routeKind
            || comboBox.SelectedItem is not StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceStepTargetChoice choice)
        {
            return;
        }

        _sequenceRouteTargetEdits[comboBox] = new SequenceRouteTargetEditState(source.StepId, routeKind, choice.StepId);
    }

    private void SequenceTargetComboBox_OnDropDownClosed(object sender, System.EventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox comboBox
            || !_sequenceRouteTargetEdits.Remove(comboBox, out var edit)
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            || viewModel.SelectedSequenceStep?.StepId != edit.SourceStepId
            || comboBox.SelectedItem is not StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceStepTargetChoice choice
            || choice.StepId == edit.OriginalTargetStepId)
        {
            return;
        }

        viewModel.SetSequenceStepRouteTarget(edit.SourceStepId, edit.RouteKind, choice.StepId);
    }

    private void SequenceCard_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            || viewModel.SequenceTimelineTool == StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Select
            || button.DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineCardViewModel card
            || !_sequenceCardDragStarts.TryGetValue(button, out var dragStart))
        {
            return;
        }

        var current = e.GetPosition(button);
        if (Math.Abs(current.X - dragStart.X) < System.Windows.SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - dragStart.Y) < System.Windows.SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _sequenceCardDragStarts.Remove(button);
        viewModel.SelectSequenceStepCommand.Execute(card.Step);
        _sequenceDropEventCompletedTimestamp = null;
        _sequenceDropToDragReturnMs = null;
        System.Windows.DragDrop.DoDragDrop(button, card.Step, System.Windows.DragDropEffects.Move);
        if (_sequenceDropEventCompletedTimestamp is long dropCompletedAt)
        {
            _sequenceDropToDragReturnMs = System.Diagnostics.Stopwatch.GetElapsedTime(dropCompletedAt).TotalMilliseconds;
        }
    }

    private void TimelineTimeSlice_OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        SetTimelineDropEffect(e);
    }

    private void TimelineTimeSlice_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border { DataContext: StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel timeSlice })
        {
            return;
        }

        var border = (System.Windows.Controls.Border)sender;
        MoveDraggedSequenceStep(e, GetTimelineOffsetAtPointer(border, timeSlice, e.GetPosition(border)));
        _sequenceDropEventCompletedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private void TimelineTimeSlice_OnContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border border
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            return;
        }

        var lane = border.DataContext as StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineBranchLaneViewModel;
        var timeCellBorder = lane is null ? border : FindParentTimeCellBorder(border);
        if (timeCellBorder?.DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel timeSlice)
        {
            return;
        }

        // The menu is built on demand. If the element has no ContextMenu when WPF
        // starts ContextMenuOpening, assigning one here does not open it until the
        // next right-click. Open the newly built menu explicitly for this click.
        e.Handled = true;

        var targetOffsetMs = GetTimelineOffsetAtPointer(timeCellBorder, timeSlice, System.Windows.Input.Mouse.GetPosition(timeCellBorder));
        var menu = new System.Windows.Controls.ContextMenu();
        if (lane is null)
        {
            menu.Items.Add(CreateBranchChoiceMenu("Add inline action", targetOffsetMs, viewModel,
                branchId => viewModel.AddInlineActionSequenceStepAtOffset(targetOffsetMs, branchId)));
            menu.Items.Add(CreateBranchChoiceMenu("Add existing action", targetOffsetMs, viewModel,
                branchId => AddExistingActionAtOffset(targetOffsetMs, branchId, viewModel)));
            menu.Items.Add(CreateBranchChoiceMenu("Add sound effect", targetOffsetMs, viewModel,
                branchId => viewModel.AddSoundSequenceStepAtOffset(targetOffsetMs, branchId)));
        }
        else
        {
            var branchId = lane.BranchId;
            var canAddToLane = viewModel.CanAddSequenceStepAtOffset(targetOffsetMs, branchId);
            var laneName = viewModel.GetSequenceBranchDisplayName(branchId);
            var addInline = new System.Windows.Controls.MenuItem
            {
                Header = $"Add inline action to {laneName} at {targetOffsetMs} ms",
                IsEnabled = canAddToLane
            };
            addInline.Click += (_, _) => viewModel.AddInlineActionSequenceStepAtOffset(targetOffsetMs, branchId);
            menu.Items.Add(addInline);

            var addExisting = new System.Windows.Controls.MenuItem
            {
                Header = $"Add existing action to {laneName} at {targetOffsetMs} ms",
                IsEnabled = canAddToLane
            };
            addExisting.Click += (_, _) => AddExistingActionAtOffset(targetOffsetMs, branchId, viewModel);
            menu.Items.Add(addExisting);

            var addSound = new System.Windows.Controls.MenuItem
            {
                Header = $"Add sound effect to {laneName} at {targetOffsetMs} ms",
                IsEnabled = canAddToLane
            };
            addSound.Click += (_, _) => viewModel.AddSoundSequenceStepAtOffset(targetOffsetMs, branchId);
            menu.Items.Add(addSound);

            menu.Items.Add(new System.Windows.Controls.Separator());
            var renameBranch = new System.Windows.Controls.MenuItem { Header = "Rename branch…" };
            renameBranch.Click += (_, _) => RenameSequenceBranch(branchId, viewModel);
            menu.Items.Add(renameBranch);

            var changeBranchColor = new System.Windows.Controls.MenuItem { Header = "Lane color…" };
            changeBranchColor.Click += (_, _) => ChooseSequenceBranchColor(branchId, viewModel);
            menu.Items.Add(changeBranchColor);

            if (!lane.IsMainBranch)
            {
                menu.Items.Add(new System.Windows.Controls.Separator());
                var canRemoveBranch = viewModel.CanRemoveEmptySequenceBranch(branchId);
                var removeBranch = new System.Windows.Controls.MenuItem
                {
                    Header = "Remove empty branch",
                    IsEnabled = canRemoveBranch,
                    ToolTip = canRemoveBranch
                        ? "Remove this empty branch lane from the sequence."
                        : "A branch can be removed only after all its steps are deleted."
                };
                removeBranch.Click += (_, _) => viewModel.RemoveEmptySequenceBranch(branchId);
                menu.Items.Add(removeBranch);
            }
        }

        if (viewModel.SelectedSequenceStep is { StepType: Storyboard.Shared.RuntimeContracts.Enums.SequenceStepType.Action } fork
            && (fork.ContinuationMode ?? Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Continue) == Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Branch)
        {
            if (!fork.OnSuccessStepId.HasValue || !fork.OnFailureStepId.HasValue)
            {
                menu.Items.Add(new System.Windows.Controls.Separator());
            }

            if (!fork.OnSuccessStepId.HasValue)
            {
                menu.Items.Add(CreateForkStepMenuItem("success", targetOffsetMs, sound: false, viewModel));
                menu.Items.Add(CreateForkExistingActionMenuItem("success", targetOffsetMs, viewModel));
                menu.Items.Add(CreateForkStepMenuItem("success", targetOffsetMs, sound: true, viewModel));
            }

            if (!fork.OnFailureStepId.HasValue)
            {
                menu.Items.Add(CreateForkStepMenuItem("failure", targetOffsetMs, sound: false, viewModel));
                menu.Items.Add(CreateForkExistingActionMenuItem("failure", targetOffsetMs, viewModel));
                menu.Items.Add(CreateForkStepMenuItem("failure", targetOffsetMs, sound: true, viewModel));
            }
        }

        menu.PlacementTarget = border;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        border.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void RenameSequenceBranch(string branchId, StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        var dialog = new RenameSequenceBranchDialog(viewModel.GetSequenceBranchDisplayName(branchId))
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!viewModel.TryRenameSequenceBranch(branchId, dialog.BranchDisplayName, out var errorMessage))
        {
            System.Windows.MessageBox.Show(
                System.Windows.Window.GetWindow(this),
                errorMessage,
                "Rename branch",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void ChooseSequenceBranchColor(string branchId, StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        var branch = viewModel.SelectedSequenceDefinition?.Branches
            .FirstOrDefault(candidate => string.Equals(candidate.BranchId, branchId, StringComparison.Ordinal));
        if (branch is null)
        {
            return;
        }

        var dialog = new StoryboardDesigner.App.Views.ColorPickerDialog(branch.DisplayColor)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true && dialog.SelectedColor is { } selectedColor)
        {
            viewModel.SetSequenceBranchDisplayColor(branchId, selectedColor);
        }
    }

    private static System.Windows.Controls.Border? FindParentTimeCellBorder(System.Windows.DependencyObject element)
    {
        for (var current = System.Windows.Media.VisualTreeHelper.GetParent(element); current is not null;
             current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Border border
                && Equals(border.Tag, "SequenceFlowCell")
                && border.DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel)
            {
                return border;
            }
        }

        return null;
    }

    private System.Windows.Controls.MenuItem CreateBranchChoiceMenu(
        string header,
        int offsetMs,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel,
        System.Action<string> addStep)
    {
        var menu = new System.Windows.Controls.MenuItem { Header = $"{header} at {offsetMs} ms" };
        foreach (var branchId in viewModel.SequenceBranchChoices)
        {
            var branchItem = new System.Windows.Controls.MenuItem
            {
                Header = viewModel.GetSequenceBranchDisplayName(branchId),
                IsEnabled = viewModel.CanAddSequenceStepAtOffset(offsetMs, branchId)
            };
            branchItem.Click += (_, _) => addStep(branchId);
            menu.Items.Add(branchItem);
        }
        return menu;
    }

    private void AddExistingActionAtOffset(
        int offsetMs,
        string branchId,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        var choices = viewModel.GetSequenceActionChoices();
        if (choices.Count == 0)
        {
            System.Windows.MessageBox.Show(System.Windows.Window.GetWindow(this),
                "There are no actions in this scope or its parent scopes to add.",
                "Add Existing Action",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var picker = new StoryboardDesigner.App.Views.SelectLinkedActionDialog(choices)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        if (picker.ShowDialog() == true && picker.SelectedAction is not null)
        {
            viewModel.AddExistingActionSequenceStepAtOffset(offsetMs, branchId, picker.SelectedAction);
        }
    }

    private System.Windows.Controls.MenuItem CreateForkExistingActionMenuItem(
        string outcome,
        int offsetMs,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        var menuItem = new System.Windows.Controls.MenuItem
        {
            Header = $"Add existing action on {outcome} branch at {offsetMs} ms",
            IsEnabled = viewModel.SelectedSequenceStep is { } source
                && offsetMs >= source.OffsetMs
        };
        menuItem.Click += (_, _) =>
        {
            var choices = viewModel.GetSequenceActionChoices();
            if (choices.Count == 0)
            {
                System.Windows.MessageBox.Show(System.Windows.Window.GetWindow(this),
                    "There are no actions in this scope or its parent scopes to add.",
                    "Add Existing Action",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var picker = new StoryboardDesigner.App.Views.SelectLinkedActionDialog(choices)
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            if (picker.ShowDialog() == true && picker.SelectedAction is not null)
            {
                viewModel.AddForkOutcomeExistingActionAtOffset(offsetMs, outcome == "success", picker.SelectedAction);
            }
        };
        return menuItem;
    }

    private static System.Windows.Controls.MenuItem CreateForkStepMenuItem(
        string outcome,
        int offsetMs,
        bool sound,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        var menuItem = new System.Windows.Controls.MenuItem
        {
            Header = $"Add {(sound ? "sound effect" : "action")} on {outcome} branch at {offsetMs} ms",
            IsEnabled = viewModel.SelectedSequenceStep is { } source
                && offsetMs >= source.OffsetMs
        };
        menuItem.Click += (_, _) => viewModel.AddForkOutcomeStepAtOffset(offsetMs, success: outcome == "success", sound: sound);
        return menuItem;
    }

    private static int GetTimelineOffsetAtPointer(
        System.Windows.Controls.Border border,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel timeSlice,
        System.Windows.Point position)
    {
        if (!timeSlice.IsRange || border.ActualWidth <= 0d)
        {
            return timeSlice.OffsetMs;
        }

        var fraction = Math.Clamp(position.X / border.ActualWidth, 0d, 1d);
        var rawOffset = timeSlice.RangeStartOffsetMs
                        + (timeSlice.RangeEndOffsetMs - (double)timeSlice.RangeStartOffsetMs) * fraction;
        var roundedOffset = (int)Math.Round(rawOffset / 50d, MidpointRounding.AwayFromZero) * 50;
        return Math.Clamp(roundedOffset, timeSlice.RangeStartOffsetMs, timeSlice.RangeEndOffsetMs);
    }

    private void SetTimelineDropEffect(System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Storyboard.Shared.DesignerContracts.ProjectSequenceStepDto))
            && DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            && viewModel.SequenceTimelineTool != StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Select)
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void MoveDraggedSequenceStep(System.Windows.DragEventArgs e, int targetOffsetMs)
    {
        if (e.Data.GetData(typeof(Storyboard.Shared.DesignerContracts.ProjectSequenceStepDto)) is not Storyboard.Shared.DesignerContracts.ProjectSequenceStepDto step
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.MoveSequenceStepToOffset(step, targetOffsetMs))
        {
            e.Handled = true;
        }
    }

    private void SequenceCanvasContent_OnSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (!_isDrawingFlowConnections)
        {
            InvalidateSequenceFlowConnections();
        }
    }

    private void InvalidateSequenceFlowConnections()
    {
        if (!IsLoaded || _isDrawingFlowConnections || _flowConnectionUpdatePending)
        {
            return;
        }

        var invalidationElapsed = System.Diagnostics.Stopwatch.StartNew();
        _flowConnectionInvalidationElapsed = invalidationElapsed;
        _flowInvalidationToNormalPriorityMs = null;
        _flowInvalidationToDataBindPriorityMs = null;
        _flowConnectionUpdatePending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
        {
            if (ReferenceEquals(_flowConnectionInvalidationElapsed, invalidationElapsed))
            {
                _flowInvalidationToNormalPriorityMs = invalidationElapsed.Elapsed.TotalMilliseconds;
            }
        }));
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, new Action(() =>
        {
            if (ReferenceEquals(_flowConnectionInvalidationElapsed, invalidationElapsed))
            {
                _flowInvalidationToDataBindPriorityMs = invalidationElapsed.Elapsed.TotalMilliseconds;
            }
        }));
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, new Action(() =>
        {
            _flowConnectionUpdatePending = false;
            DrawSequenceFlowConnections();
        }));
    }

    private void DrawSequenceFlowConnections()
    {
        if (!IsLoaded)
        {
            return;
        }

        _isDrawingFlowConnections = true;
        try
        {
        var total = System.Diagnostics.Stopwatch.StartNew();
        var invalidationToDrawMs = _flowConnectionInvalidationElapsed?.Elapsed.TotalMilliseconds;
        var invalidationToNormalPriorityMs = _flowInvalidationToNormalPriorityMs;
        var invalidationToDataBindPriorityMs = _flowInvalidationToDataBindPriorityMs;
        _flowConnectionInvalidationElapsed = null;
        _flowInvalidationToNormalPriorityMs = null;
        _flowInvalidationToDataBindPriorityMs = null;
        var layout = System.Diagnostics.Stopwatch.StartNew();
        SequenceCanvasContent.UpdateLayout();
        var updateLayoutMs = layout.Elapsed.TotalMilliseconds;

        var scan = System.Diagnostics.Stopwatch.StartNew();
        var cards = FindVisualChildren<System.Windows.Controls.Button>(SequenceCanvasContent)
            .Where(static button => button.DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineCardViewModel)
            .Select(button =>
            {
                var card = (StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineCardViewModel)button.DataContext;
                var topLeft = button.TranslatePoint(new System.Windows.Point(0d, 0d), SequenceFlowConnectionCanvas);
                return new FlowCardVisual(button, card, topLeft);
            })
            .ToList();
        var timeCells = FindVisualChildren<System.Windows.Controls.Border>(SequenceCanvasContent)
            .Where(static border => Equals(border.Tag, "SequenceFlowCell")
                && border.DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel)
            .Select(border => new FlowTimeCellVisual(
                (StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel)border.DataContext,
                border.TranslatePoint(new System.Windows.Point(0d, 0d), SequenceFlowConnectionCanvas),
                border.ActualWidth))
            .ToList();
        var byStepId = cards
            .Where(static card => card.Card.Step.StepId != Guid.Empty)
            .GroupBy(static card => card.Card.Step.StepId)
            .ToDictionary(static group => group.Key, static group => group.First());

        var layoutKey = string.Join("|", cards.Select(card =>
            $"{card.Card.Step.StepId:N}:{card.Position.X:0.0},{card.Position.Y:0.0},{card.Button.ActualWidth:0.0},{card.Button.ActualHeight:0.0}:{card.Card.Step.BranchId}:{card.Card.Step.ContinuationMode}:{card.Card.Step.OnSuccessStepId}:{card.Card.Step.OnFailureStepId}:{card.Card.Step.MergeTargetStepId}:{card.Card.PlaybackStartOffsetMs}:{card.Card.PlaybackEndOffsetMs}"))
            + string.Join("|", timeCells.Select(cell => $"{cell.Slice.RangeStartOffsetMs}-{cell.Slice.RangeEndOffsetMs}:{cell.Position.X:0.0}:{cell.Width:0.0}"));
        var scanMs = scan.Elapsed.TotalMilliseconds;
        if (string.Equals(layoutKey, _lastFlowConnectionLayout, StringComparison.Ordinal))
        {
            _unchangedFlowLayoutPassCount++;
            _unchangedFlowLayoutTotalMs += total.Elapsed.TotalMilliseconds;
            _unchangedFlowVisualScanTotalMs += scanMs;
            return;
        }

        _lastFlowConnectionLayout = layoutKey;
        var draw = System.Diagnostics.Stopwatch.StartNew();
        SequenceFlowConnectionCanvas.Children.Clear();
        SequenceSoundEndMarkerCanvas.Children.Clear();
        SequenceSoundEndMarkerCanvas.Width = SequenceFlowConnectionCanvas.ActualWidth;
        SequenceSoundEndMarkerCanvas.Height = SequenceFlowConnectionCanvas.ActualHeight;
        DrawSoundPlaybackSpans(cards, timeCells);
        foreach (var card in cards)
        {
            var step = card.Card.Step;
            var mode = step.ContinuationMode ?? Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Continue;
            if (mode == Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Continue)
            {
                var nextOnBranch = cards
                    .Where(candidate => !ReferenceEquals(candidate, card)
                        && string.Equals(candidate.Card.Step.BranchId, step.BranchId, StringComparison.Ordinal)
                        && (candidate.Card.Step.OffsetMs > step.OffsetMs
                            || candidate.Card.Step.OffsetMs == step.OffsetMs && candidate.Card.Step.Order > step.Order))
                    .OrderBy(static candidate => candidate.Card.Step.OffsetMs)
                    .ThenBy(static candidate => candidate.Card.Step.Order)
                    .FirstOrDefault();
                if (nextOnBranch is not null)
                {
                    AddConnection(card, nextOnBranch.Card.Step.StepId, "Continue", System.Windows.Media.Color.FromRgb(71, 85, 105), 0.50d, byStepId);
                }
            }
            else if (mode == Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Branch
                     || mode == Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Merge && step.StepType == Storyboard.Shared.RuntimeContracts.Enums.SequenceStepType.Action)
            {
                var routePrefix = mode == Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Merge ? "Merge " : string.Empty;
                AddConnection(card, step.OnSuccessStepId, $"{routePrefix}Success", System.Windows.Media.Color.FromRgb(21, 128, 61), 0.30d, byStepId);
                AddConnection(card, step.OnFailureStepId, $"{routePrefix}Failure", System.Windows.Media.Color.FromRgb(185, 28, 28), 0.70d, byStepId);
            }
            else if (mode == Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Merge)
            {
                AddConnection(card, step.MergeTargetStepId, "Merge", System.Windows.Media.Color.FromRgb(71, 85, 105), 0.50d, byStepId);
            }
        }

        var unchangedPassSummary = _unchangedFlowLayoutPassCount == 0
            ? string.Empty
            : $"; unchanged-passes={_unchangedFlowLayoutPassCount}; "
                + $"unchanged-total={_unchangedFlowLayoutTotalMs:F1}ms; unchanged-scan={_unchangedFlowVisualScanTotalMs:F1}ms";
        _unchangedFlowLayoutPassCount = 0;
        _unchangedFlowLayoutTotalMs = 0d;
        _unchangedFlowVisualScanTotalMs = 0d;
        var flowDetails =
            $"flow-layout total={total.Elapsed.TotalMilliseconds:F1}ms; update-layout={updateLayoutMs:F1}ms; "
            + $"visual-scan={scanMs:F1}ms; redraw={draw.Elapsed.TotalMilliseconds:F1}ms; "
            + $"cards={cards.Count}; time-cells={timeCells.Count}; "
            + $"invalidate-to-draw={(invalidationToDrawMs.HasValue ? $"{invalidationToDrawMs.Value:F1}ms" : "n/a")}; "
            + $"invalidate-to-normal={(invalidationToNormalPriorityMs.HasValue ? $"{invalidationToNormalPriorityMs.Value:F1}ms" : "n/a")}; "
            + $"invalidate-to-databind={(invalidationToDataBindPriorityMs.HasValue ? $"{invalidationToDataBindPriorityMs.Value:F1}ms" : "n/a")}; "
            + $"drop-to-drag-return={(_sequenceDropToDragReturnMs.HasValue ? $"{_sequenceDropToDragReturnMs.Value:F1}ms" : "n/a")}{unchangedPassSummary}";
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            viewModel.CompleteSequenceEditPerformance(flowDetails);
        }
        }
        finally
        {
            _isDrawingFlowConnections = false;
        }
    }

    private void DrawSoundPlaybackSpans(
        IReadOnlyList<FlowCardVisual> cards,
        IReadOnlyList<FlowTimeCellVisual> timeCells)
    {
        if (timeCells.Count == 0)
        {
            return;
        }

        double? GetX(int offsetMs)
        {
            foreach (var cell in timeCells)
            {
                var slice = cell.Slice;
                if (slice.IsRange && offsetMs >= slice.RangeStartOffsetMs && offsetMs <= slice.RangeEndOffsetMs)
                {
                    var fraction = (offsetMs - slice.RangeStartOffsetMs)
                        / (double)(slice.RangeEndOffsetMs - slice.RangeStartOffsetMs);
                    return cell.Position.X + cell.Width * fraction;
                }

                if (!slice.IsRange && slice.OffsetMs == offsetMs)
                {
                    return cell.Position.X;
                }
            }

            var ordered = timeCells.Where(static cell => !cell.Slice.IsRange)
                .OrderBy(static cell => cell.Slice.OffsetMs)
                .ToList();
            var before = ordered.LastOrDefault(cell => cell.Slice.OffsetMs < offsetMs);
            var after = ordered.FirstOrDefault(cell => cell.Slice.OffsetMs > offsetMs);
            if (before is null || after is null)
            {
                return null;
            }

            var fractionBetween = (offsetMs - before.Slice.OffsetMs)
                / (double)(after.Slice.OffsetMs - before.Slice.OffsetMs);
            return before.Position.X + (after.Position.X - before.Position.X) * fractionBetween;
        }

        var markerStacks = new Dictionary<(int X, int Y), int>();
        foreach (var source in cards.Where(static card => card.Card.IsSound
            && card.Card.PlaybackStartOffsetMs.HasValue
            && card.Card.PlaybackEndOffsetMs.HasValue))
        {
            var startX = GetX(source.Card.PlaybackStartOffsetMs!.Value);
            var endX = GetX(source.Card.PlaybackEndOffsetMs!.Value);
            if (!startX.HasValue || !endX.HasValue || endX.Value <= startX.Value)
            {
                continue;
            }

            var y = source.Position.Y + source.Button.ActualHeight - 7d;
            var markerStackKey = ((int)Math.Round(endX.Value), (int)Math.Round(y));
            markerStacks.TryGetValue(markerStackKey, out var markerStackIndex);
            markerStacks[markerStackKey] = markerStackIndex + 1;
            var spanColor = System.Windows.Media.Color.FromArgb(110, 13, 148, 136);
            SequenceSoundEndMarkerCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = startX.Value,
                X2 = endX.Value,
                Y1 = y,
                Y2 = y,
                Stroke = new System.Windows.Media.SolidColorBrush(spanColor),
                StrokeThickness = 3d,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                IsHitTestVisible = false
            });

            var marker = new System.Windows.Controls.Border
            {
                Width = 8d,
                Height = 12d,
                CornerRadius = new System.Windows.CornerRadius(2d),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 148, 136)),
                BorderBrush = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(1d),
                ToolTip = source.Card.PlaybackEndMarkerToolTip,
                IsHitTestVisible = true
            };
            System.Windows.Controls.ToolTipService.SetInitialShowDelay(marker, 250);
            System.Windows.Controls.Canvas.SetLeft(marker, endX.Value - marker.Width / 2d);
            System.Windows.Controls.Canvas.SetTop(marker, y - marker.Height / 2d - markerStackIndex * 9d);
            SequenceSoundEndMarkerCanvas.Children.Add(marker);
        }
    }

    private void AddConnection(
        FlowCardVisual source,
        Guid? targetId,
        string label,
        System.Windows.Media.Color color,
        double sourceHeightFactor,
        IReadOnlyDictionary<Guid, FlowCardVisual> cardsByStepId)
    {
        if (!targetId.HasValue || !cardsByStepId.TryGetValue(targetId.Value, out var target))
        {
            return;
        }

        var direction = target.Position.X >= source.Position.X ? 1d : -1d;
        var start = new System.Windows.Point(
            source.Position.X + (direction > 0 ? source.Button.ActualWidth : 0d),
            source.Position.Y + source.Button.ActualHeight * sourceHeightFactor);
        var end = new System.Windows.Point(
            target.Position.X + (direction > 0 ? 0d : target.Button.ActualWidth),
            target.Position.Y + target.Button.ActualHeight * 0.5d);
        var bend = Math.Max(30d, Math.Abs(end.X - start.X) * 0.45d);

        var geometry = new System.Windows.Media.PathGeometry();
        var figure = new System.Windows.Media.PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new System.Windows.Media.BezierSegment(
            new System.Windows.Point(start.X + direction * bend, start.Y),
            new System.Windows.Point(end.X - direction * bend, end.Y),
            end,
            true));
        geometry.Figures.Add(figure);
        SequenceFlowConnectionCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = new System.Windows.Media.SolidColorBrush(color),
            StrokeThickness = 2d,
            StrokeLineJoin = System.Windows.Media.PenLineJoin.Round
        });

        var arrowTip = end;
        var arrowBaseX = end.X - direction * 9d;
        var arrow = new System.Windows.Shapes.Polygon
        {
            Fill = new System.Windows.Media.SolidColorBrush(color),
            Points = new System.Windows.Media.PointCollection
            {
                arrowTip,
                new(arrowBaseX, end.Y - 5d),
                new(arrowBaseX, end.Y + 5d)
            }
        };
        SequenceFlowConnectionCanvas.Children.Add(arrow);

        var routeLabel = new System.Windows.Controls.TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = new System.Windows.Media.SolidColorBrush(color),
            Background = System.Windows.Media.Brushes.White,
            Padding = new System.Windows.Thickness(2d, 0d, 2d, 0d)
        };
        routeLabel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        System.Windows.Controls.Canvas.SetLeft(routeLabel, (start.X + end.X - routeLabel.DesiredSize.Width) / 2d);
        System.Windows.Controls.Canvas.SetTop(routeLabel, (start.Y + end.Y - routeLabel.DesiredSize.Height) / 2d - 7d);
        SequenceFlowConnectionCanvas.Children.Add(routeLabel);
    }

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent)
        where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record FlowCardVisual(
        System.Windows.Controls.Button Button,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineCardViewModel Card,
        System.Windows.Point Position);

    private sealed record FlowTimeCellVisual(
        StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel Slice,
        System.Windows.Point Position,
        double Width);
}
