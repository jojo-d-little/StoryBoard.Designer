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
    private SequenceTimelineScreenTimeMap? _sequenceTimelineScreenTimeMap;
    private string _lastSequenceRulerLayout = string.Empty;
    private string _lastFlowConnectionLayout = string.Empty;
    private int _unchangedFlowLayoutPassCount;
    private double _unchangedFlowLayoutTotalMs;
    private double _unchangedFlowVisualScanTotalMs;

    private sealed record SequenceRouteTargetEditState(Guid SourceStepId, string RouteKind, Guid? OriginalTargetStepId);

    private sealed class SoundPlaybackTrackVisual
    {
        public required Guid StepId { get; init; }
        public required System.Windows.Shapes.Line Halo { get; init; }
        public required System.Windows.Shapes.Line Span { get; init; }
        public required System.Windows.Controls.Border Marker { get; init; }
        public required System.Windows.Controls.TextBlock Label { get; init; }
        public required double MarkerCenterX { get; init; }
        public required double MarkerCenterY { get; init; }
    }

    private readonly List<SoundPlaybackTrackVisual> _soundPlaybackTrackVisuals = new();

    public SequenceDesignerWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += SequenceDesignerWorkspaceView_OnDataContextChanged;
        Loaded += (_, _) => InvalidateSequenceFlowConnections();
    }

    private void SequenceDesignerWorkspaceView_OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        _lastSequenceRulerLayout = string.Empty;
        _lastFlowConnectionLayout = string.Empty;
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
        if (e.PropertyName == nameof(StoryboardDesigner.App.ViewModels.MainWindowViewModel.SelectedSequenceStep))
        {
            UpdateSoundPlaybackTrackSelection();
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimeSlices)
            || e.PropertyName == nameof(StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceFlowTimeSlices))
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
            || comboBox.Tag is not string routeKind)
        {
            return;
        }

        var currentTargetStepId = routeKind switch
        {
            "Success" => source.OnSuccessStepId,
            "Failure" => source.OnFailureStepId,
            "Merge" => source.MergeTargetStepId,
            _ => null
        };
        _sequenceRouteTargetEdits[comboBox] = new SequenceRouteTargetEditState(source.StepId, routeKind, currentTargetStepId);
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
        if (sender is not System.Windows.Controls.Border border)
        {
            return;
        }

        var lane = border.DataContext as StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineBranchLaneViewModel;
        var timeCellBorder = lane is null ? border : FindParentTimeCellBorder(border);
        if (timeCellBorder?.DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel timeSlice)
        {
            return;
        }

        var currentViewModel = DataContext as StoryboardDesigner.App.ViewModels.MainWindowViewModel;
        var relocationTargetBranchId = currentViewModel?.SequenceTimelineTool == StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Relocate
            ? lane?.BranchId
            : null;
        var targetOffsetMs = DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            && viewModel.SequenceTimelineTool is StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Move
                or StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Ripple
                or StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Relocate
            ? GetInsertionOffsetAtPointer(timeCellBorder, timeSlice, e.GetPosition(SequenceFlowConnectionCanvas))
            : GetTimelineOffsetAtPointer(timeCellBorder, timeSlice, e.GetPosition(timeCellBorder));
        MoveDraggedSequenceStep(e, targetOffsetMs, relocationTargetBranchId);
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

        var targetOffsetMs = GetInsertionOffsetAtPointer(
            timeCellBorder,
            timeSlice,
            System.Windows.Input.Mouse.GetPosition(SequenceFlowConnectionCanvas));
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
            var addDisabledReason = viewModel.GetSequenceStepAddDisabledReason(targetOffsetMs, branchId);
            var laneName = viewModel.GetSequenceBranchDisplayName(branchId);
            var addInline = new System.Windows.Controls.MenuItem
            {
                Header = $"Add inline action to {laneName} at {targetOffsetMs} ms",
                IsEnabled = addDisabledReason is null
            };
            SetDisabledMenuItemToolTip(addInline, addDisabledReason);
            addInline.Click += (_, _) => viewModel.AddInlineActionSequenceStepAtOffset(targetOffsetMs, branchId);
            menu.Items.Add(addInline);

            var addExisting = new System.Windows.Controls.MenuItem
            {
                Header = $"Add existing action to {laneName} at {targetOffsetMs} ms",
                IsEnabled = addDisabledReason is null
            };
            SetDisabledMenuItemToolTip(addExisting, addDisabledReason);
            addExisting.Click += (_, _) => AddExistingActionAtOffset(targetOffsetMs, branchId, viewModel);
            menu.Items.Add(addExisting);

            var addSound = new System.Windows.Controls.MenuItem
            {
                Header = $"Add sound effect to {laneName} at {targetOffsetMs} ms",
                IsEnabled = addDisabledReason is null
            };
            SetDisabledMenuItemToolTip(addSound, addDisabledReason);
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

        AddForkOutcomeMenuItems(menu, targetOffsetMs, viewModel);

        menu.PlacementTarget = border;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        border.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void SequenceTimelineRulerCanvas_OnContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        if (sender is not System.Windows.Controls.Canvas rulerCanvas
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            || _sequenceTimelineScreenTimeMap is null)
        {
            return;
        }

        e.Handled = true;
        var targetOffsetMs = _sequenceTimelineScreenTimeMap.GetOffsetAtX(
            System.Windows.Input.Mouse.GetPosition(SequenceFlowConnectionCanvas).X);
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(CreateBranchChoiceMenu("Add inline action", targetOffsetMs, viewModel,
            branchId => viewModel.AddInlineActionSequenceStepAtOffset(targetOffsetMs, branchId)));
        menu.Items.Add(CreateBranchChoiceMenu("Add existing action", targetOffsetMs, viewModel,
            branchId => AddExistingActionAtOffset(targetOffsetMs, branchId, viewModel)));
        menu.Items.Add(CreateBranchChoiceMenu("Add sound effect", targetOffsetMs, viewModel,
            branchId => viewModel.AddSoundSequenceStepAtOffset(targetOffsetMs, branchId)));
        AddForkOutcomeMenuItems(menu, targetOffsetMs, viewModel);
        menu.PlacementTarget = rulerCanvas;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        rulerCanvas.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void SequenceTimelineRulerCanvas_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (_sequenceTimelineScreenTimeMap is null)
        {
            return;
        }

        MoveDraggedSequenceStep(
            e,
            _sequenceTimelineScreenTimeMap.GetOffsetAtX(e.GetPosition(SequenceFlowConnectionCanvas).X),
            targetBranchId: null);
        _sequenceDropEventCompletedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private void AddForkOutcomeMenuItems(
        System.Windows.Controls.ContextMenu menu,
        int targetOffsetMs,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        if (viewModel.SelectedSequenceStep is not { StepType: Storyboard.Shared.RuntimeContracts.Enums.SequenceStepType.Action } fork
            || (fork.ContinuationMode ?? Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Continue)
                != Storyboard.Shared.RuntimeContracts.Enums.ContinuationMode.Branch)
        {
            return;
        }

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
            var disabledReason = viewModel.GetSequenceStepAddDisabledReason(offsetMs, branchId);
            var branchItem = new System.Windows.Controls.MenuItem
            {
                Header = viewModel.GetSequenceBranchDisplayName(branchId),
                IsEnabled = disabledReason is null
            };
            SetDisabledMenuItemToolTip(branchItem, disabledReason);
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
        var choices = viewModel.GetSequenceActionPickerChoices();
        if (choices.Count == 0)
        {
            System.Windows.MessageBox.Show(System.Windows.Window.GetWindow(this),
                "There are no actions in this scope, its ancestors, or its children to add.",
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
        var disabledReason = viewModel.GetForkOutcomeAddDisabledReason(offsetMs);
        var menuItem = new System.Windows.Controls.MenuItem
        {
            Header = $"Add existing action on {outcome} branch at {offsetMs} ms",
            IsEnabled = disabledReason is null
        };
        SetDisabledMenuItemToolTip(menuItem, disabledReason);
        menuItem.Click += (_, _) =>
        {
            var choices = viewModel.GetSequenceActionPickerChoices();
            if (choices.Count == 0)
            {
                System.Windows.MessageBox.Show(System.Windows.Window.GetWindow(this),
                    "There are no actions in this scope, its ancestors, or its children to add.",
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

    private void ChooseSequenceAction_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            || viewModel.SelectedSequenceStep is not { StepType: Storyboard.Shared.RuntimeContracts.Enums.SequenceStepType.Action })
        {
            return;
        }

        var choices = viewModel.GetSequenceActionPickerChoices();
        if (choices.Count == 0)
        {
            System.Windows.MessageBox.Show(System.Windows.Window.GetWindow(this),
                "There are no actions in this scope, its ancestors, or its children.",
                "Choose Action",
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
            viewModel.SelectedSequenceActionId = picker.SelectedAction.Id;
        }
    }

    private static System.Windows.Controls.MenuItem CreateForkStepMenuItem(
        string outcome,
        int offsetMs,
        bool sound,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
    {
        var disabledReason = viewModel.GetForkOutcomeAddDisabledReason(offsetMs);
        var menuItem = new System.Windows.Controls.MenuItem
        {
            Header = $"Add {(sound ? "sound effect" : "action")} on {outcome} branch at {offsetMs} ms",
            IsEnabled = disabledReason is null
        };
        SetDisabledMenuItemToolTip(menuItem, disabledReason);
        menuItem.Click += (_, _) => viewModel.AddForkOutcomeStepAtOffset(offsetMs, success: outcome == "success", sound: sound);
        return menuItem;
    }

    private static void SetDisabledMenuItemToolTip(
        System.Windows.Controls.MenuItem menuItem,
        string? disabledReason)
    {
        if (menuItem.IsEnabled || string.IsNullOrWhiteSpace(disabledReason))
        {
            return;
        }

        menuItem.ToolTip = disabledReason;
        System.Windows.Controls.ToolTipService.SetShowOnDisabled(menuItem, true);
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

    private int GetInsertionOffsetAtPointer(
        System.Windows.Controls.Border timeCellBorder,
        StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel timeSlice,
        System.Windows.Point position)
    {
        return _sequenceTimelineScreenTimeMap is null
            ? GetTimelineOffsetAtPointer(timeCellBorder, timeSlice, System.Windows.Input.Mouse.GetPosition(timeCellBorder))
            : _sequenceTimelineScreenTimeMap.GetOffsetAtX(position.X);
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

    private void MoveDraggedSequenceStep(System.Windows.DragEventArgs e, int targetOffsetMs, string? targetBranchId = null)
    {
        if (e.Data.GetData(typeof(Storyboard.Shared.DesignerContracts.ProjectSequenceStepDto)) is not Storyboard.Shared.DesignerContracts.ProjectSequenceStepDto step
            || DataContext is not StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            return;
        }

        var moved = viewModel.SequenceTimelineTool == StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Relocate
            ? viewModel.RelocateSequenceStepTo(
                step,
                targetOffsetMs,
                targetBranchId ?? step.BranchId ?? StoryboardDesigner.App.ViewModels.MainWindowViewModel.MainSequenceBranchId)
            : viewModel.MoveSequenceStepToOffset(step, targetOffsetMs);
        if (moved)
        {
            e.Handled = true;
        }
    }

    private void SequenceCanvasContent_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateSequenceTimeMappingPreview(
            e.GetPosition(SequenceFlowConnectionCanvas),
            GetSequenceBranchIdAtPointer(e.OriginalSource));
    }

    private void SequenceCanvasContent_OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            && viewModel.SequenceTimelineTool is StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Move
                or StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Ripple
                or StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Relocate)
        {
            UpdateSequenceTimeMappingPreview(
                e.GetPosition(SequenceFlowConnectionCanvas),
                GetSequenceBranchIdAtPointer(e.OriginalSource));
        }
        else
        {
            HideSequenceTimeMappingPreview();
        }
    }

    private void SequenceCanvasContent_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HideSequenceTimeMappingPreview();
    }

    private void UpdateSequenceTimeMappingPreview(System.Windows.Point pointer, string? targetBranchId = null)
    {
        if (_sequenceTimelineScreenTimeMap is null
            || pointer.X < 0d
            || pointer.X > SequenceTimeMappingPreviewCanvas.ActualWidth)
        {
            HideSequenceTimeMappingPreview();
            return;
        }

        var offsetMs = _sequenceTimelineScreenTimeMap.GetOffsetAtX(pointer.X);
        var snapX = _sequenceTimelineScreenTimeMap.GetXAtOffset(offsetMs);
        SequenceTimeMappingPreviewLine.X1 = snapX;
        SequenceTimeMappingPreviewLine.X2 = snapX;
        SequenceTimeMappingPreviewLine.Y1 = 0d;
        SequenceTimeMappingPreviewLine.Y2 = SequenceTimeMappingPreviewCanvas.ActualHeight;
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel
            && viewModel.SequenceTimelineTool == StoryboardDesigner.App.ViewModels.SequenceTimelineToolMode.Relocate
            && viewModel.SelectedSequenceStep is { } selectedStep)
        {
            var destinationBranchId = targetBranchId
                ?? selectedStep.BranchId
                ?? StoryboardDesigner.App.ViewModels.MainWindowViewModel.MainSequenceBranchId;
            SequenceTimeMappingPreviewLabel.Text = viewModel.GetSequenceRelocationPreviewDescription(
                selectedStep,
                offsetMs,
                destinationBranchId);
        }
        else
        {
            SequenceTimeMappingPreviewLabel.Text = $"50 ms slot · {offsetMs:N0} ms";
        }
        SequenceTimeMappingPreviewReadout.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var left = snapX + 7d;
        if (left + SequenceTimeMappingPreviewReadout.DesiredSize.Width > SequenceTimeMappingPreviewCanvas.ActualWidth)
        {
            left = snapX - SequenceTimeMappingPreviewReadout.DesiredSize.Width - 7d;
        }

        var maxLeft = Math.Max(0d, SequenceBranchScrollViewer.ActualWidth - SequenceTimeMappingPreviewReadout.DesiredSize.Width);
        SequenceTimeMappingPreviewReadout.Margin = new System.Windows.Thickness(Math.Clamp(left, 0d, maxLeft), 3d, 0d, 0d);
        SequenceTimeMappingPreviewLine.Visibility = System.Windows.Visibility.Visible;
        SequenceTimeMappingPreviewReadout.Visibility = System.Windows.Visibility.Visible;
    }

    private string? GetSequenceBranchIdAtPointer(object? source)
    {
        var current = source as System.Windows.DependencyObject;
        while (current is not null && !ReferenceEquals(current, SequenceCanvasContent))
        {
            if (current is System.Windows.Controls.Border
                {
                    DataContext: StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineBranchLaneViewModel lane
                })
            {
                return lane.BranchId;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void HideSequenceTimeMappingPreview()
    {
        SequenceTimeMappingPreviewLine.Visibility = System.Windows.Visibility.Collapsed;
        SequenceTimeMappingPreviewReadout.Visibility = System.Windows.Visibility.Collapsed;
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
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel)
        {
            // The flow surface renders the sparse slices, so the ruler and pointer
            // mapping must use that same screen-space layout to stay aligned with cards.
            _sequenceTimelineScreenTimeMap = CreateSequenceTimelineScreenTimeMap(viewModel.SequenceFlowTimeSlices);
            var rulerLayout = string.Join("|", viewModel.SequenceFlowTimeSlices.Select(static slice =>
                $"{slice.OffsetMs}:{slice.RangeStartOffsetMs}-{slice.RangeEndOffsetMs}:{slice.Width:0.##}:{slice.HasSteps}"));
            if (!string.Equals(rulerLayout, _lastSequenceRulerLayout, StringComparison.Ordinal))
            {
                _lastSequenceRulerLayout = rulerLayout;
                DrawSequenceTimelineRuler(viewModel, _sequenceTimelineScreenTimeMap);
            }
        }
        else
        {
            _sequenceTimelineScreenTimeMap = null;
            SequenceTimelineRulerCanvas.Width = 0d;
            SequenceTimelineRulerCanvas.Children.Clear();
        }

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
        SequenceTimeMappingPreviewCanvas.Width = SequenceFlowConnectionCanvas.ActualWidth;
        SequenceTimeMappingPreviewCanvas.Height = SequenceFlowConnectionCanvas.ActualHeight;
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
        _soundPlaybackTrackVisuals.Clear();
        SequenceSoundEndMarkerCanvas.Width = SequenceFlowConnectionCanvas.ActualWidth;
        SequenceSoundEndMarkerCanvas.Height = SequenceFlowConnectionCanvas.ActualHeight;
        DrawSoundPlaybackSpans(cards, _sequenceTimelineScreenTimeMap);
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
        if (DataContext is StoryboardDesigner.App.ViewModels.MainWindowViewModel performanceViewModel)
        {
            performanceViewModel.CompleteSequenceEditPerformance(flowDetails);
        }
        }
        finally
        {
            _isDrawingFlowConnections = false;
        }
    }

    private static SequenceTimelineScreenTimeMap? CreateSequenceTimelineScreenTimeMap(
        IReadOnlyList<StoryboardDesigner.App.ViewModels.MainWindowViewModel.SequenceTimelineTimeSliceViewModel> timeSlices)
    {
        if (timeSlices.Count == 0)
        {
            return null;
        }

        var x = 0d;
        var cells = new List<SequenceTimelineScreenTimeMap.TimelineCell>(timeSlices.Count);
        foreach (var slice in timeSlices)
        {
            cells.Add(new SequenceTimelineScreenTimeMap.TimelineCell(
                slice.OffsetMs,
                slice.RangeStartOffsetMs,
                slice.RangeEndOffsetMs,
                slice.IsRange,
                slice.HasSteps,
                x,
                slice.Width));
            x += slice.Width;
        }

        return new SequenceTimelineScreenTimeMap(cells);
    }

    private void DrawSequenceTimelineRuler(
        StoryboardDesigner.App.ViewModels.MainWindowViewModel viewModel,
        SequenceTimelineScreenTimeMap? screenTimeMap)
    {
        var timeSlices = viewModel.SequenceFlowTimeSlices;
        var width = timeSlices.Sum(static slice => slice.Width);
        SequenceTimelineRulerCanvas.Width = width;
        SequenceTimelineRulerCanvas.Children.Clear();
        if (screenTimeMap is null || timeSlices.Count == 0)
        {
            return;
        }

        var lastSlice = timeSlices[^1];
        var endOffsetMs = lastSlice.IsRange ? lastSlice.RangeEndOffsetMs : lastSlice.OffsetMs;
        long intervalMs = 1_000L;
        while (endOffsetMs / intervalMs > 160L)
        {
            intervalMs *= 2L;
        }

        SequenceTimelineRulerCanvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 0d,
            X2 = width,
            Y1 = 37d,
            Y2 = 37d,
            Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
            StrokeThickness = 1d,
            IsHitTestVisible = false
        });

        double? lastMajorTickX = null;
        var lastLabelRight = double.NegativeInfinity;
        for (long offsetMs = 0L; offsetMs <= endOffsetMs; offsetMs += intervalMs)
        {
            var x = screenTimeMap.GetXAtOffset((int)offsetMs);
            if (lastMajorTickX.HasValue && x - lastMajorTickX.Value < 34d)
            {
                continue;
            }

            lastMajorTickX = x;
            SequenceTimelineRulerCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = 9d,
                Y2 = 37d,
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
                StrokeThickness = 1d,
                IsHitTestVisible = false
            });

            var label = new System.Windows.Controls.TextBlock
            {
                Text = $"{offsetMs / 1_000d:0.##}s",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85)),
                FontSize = 10d,
                IsHitTestVisible = false
            };
            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var left = x + 4d;
            if (left + label.DesiredSize.Width > width)
            {
                left = x - label.DesiredSize.Width - 4d;
            }

            left = Math.Clamp(left, 0d, Math.Max(0d, width - label.DesiredSize.Width));
            if (left >= lastLabelRight + 8d)
            {
                System.Windows.Controls.Canvas.SetLeft(label, left);
                System.Windows.Controls.Canvas.SetTop(label, 15d);
                SequenceTimelineRulerCanvas.Children.Add(label);
                lastLabelRight = left + label.DesiredSize.Width;
            }
        }

        var majorTickPositions = SequenceTimelineRulerCanvas.Children
            .OfType<System.Windows.Shapes.Line>()
            .Where(static line => line.Y1 == 9d)
            .Select(static line => line.X1)
            .ToList();
        foreach (var occupiedOffset in timeSlices
                     .Where(static slice => slice.HasSteps)
                     .Select(static slice => slice.OffsetMs)
                     .Distinct())
        {
            var x = screenTimeMap.GetXAtOffset(occupiedOffset);
            if (majorTickPositions.Any(majorX => Math.Abs(majorX - x) < 2d))
            {
                continue;
            }

            SequenceTimelineRulerCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = 25d,
                Y2 = 37d,
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
                StrokeThickness = 2d,
                IsHitTestVisible = false
            });
        }
    }

    private void DrawSoundPlaybackSpans(
        IReadOnlyList<FlowCardVisual> cards,
        SequenceTimelineScreenTimeMap? screenTimeMap)
    {
        if (screenTimeMap is null)
        {
            return;
        }

        var markerStacks = new Dictionary<(int X, int Y), int>();
        var occupiedVisualTracksByRow = new Dictionary<int, List<int>>();
        var palette = new[]
        {
            System.Windows.Media.Color.FromRgb(13, 148, 136),  // teal
            System.Windows.Media.Color.FromRgb(37, 99, 235),   // blue
            System.Windows.Media.Color.FromRgb(217, 119, 6),   // amber
            System.Windows.Media.Color.FromRgb(124, 58, 237),  // violet
            System.Windows.Media.Color.FromRgb(220, 38, 38),   // red
            System.Windows.Media.Color.FromRgb(5, 150, 105),   // green
            System.Windows.Media.Color.FromRgb(219, 39, 119),  // pink
            System.Windows.Media.Color.FromRgb(71, 85, 105)    // slate
        };
        var soundCards = cards
            .Where(static card => card.Card.IsSound
                && card.Card.PlaybackStartOffsetMs.HasValue
                && card.Card.PlaybackEndOffsetMs.HasValue)
            .OrderBy(static card => card.Card.Step.OffsetMs)
            .ThenBy(static card => card.Card.Step.Order)
            .ThenBy(static card => card.Card.Step.StepId)
            .ToList();
        var selectedStepId = (DataContext as StoryboardDesigner.App.ViewModels.MainWindowViewModel)?.SelectedSequenceStep?.StepId;
        for (var soundIndex = 0; soundIndex < soundCards.Count; soundIndex++)
        {
            var source = soundCards[soundIndex];
            var startX = screenTimeMap.GetXAtOffset(source.Card.PlaybackStartOffsetMs!.Value);
            var endX = screenTimeMap.GetXAtOffset(source.Card.PlaybackEndOffsetMs!.Value);
            if (endX <= startX)
            {
                continue;
            }

            var baseY = source.Position.Y + source.Button.ActualHeight - 7d;
            var rowKey = (int)Math.Round(baseY);
            if (!occupiedVisualTracksByRow.TryGetValue(rowKey, out var activeTrackEndOffsets))
            {
                activeTrackEndOffsets = new List<int>();
                occupiedVisualTracksByRow[rowKey] = activeTrackEndOffsets;
            }

            var startOffsetMs = source.Card.PlaybackStartOffsetMs!.Value;
            var endOffsetMs = source.Card.PlaybackEndOffsetMs!.Value;
            var visualTrackIndex = activeTrackEndOffsets.FindIndex(endOffset => endOffset <= startOffsetMs);
            if (visualTrackIndex < 0)
            {
                visualTrackIndex = activeTrackEndOffsets.Count;
                activeTrackEndOffsets.Add(endOffsetMs);
            }
            else
            {
                activeTrackEndOffsets[visualTrackIndex] = endOffsetMs;
            }

            // Keep simultaneous spans legible without changing branch or card layout.
            // The 6 px offset is only used while another sound span on this visual row is active.
            var y = baseY + visualTrackIndex * 6d;
            var markerStackKey = ((int)Math.Round(endX), (int)Math.Round(y));
            markerStacks.TryGetValue(markerStackKey, out var markerStackIndex);
            markerStacks[markerStackKey] = markerStackIndex + 1;
            var trackColor = palette[soundIndex % palette.Length];
            var isSelected = source.Card.Step.StepId == selectedStepId;
            var halo = new System.Windows.Shapes.Line
            {
                X1 = startX,
                X2 = endX,
                Y1 = y,
                Y2 = y,
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 255, 255)),
                StrokeThickness = 7d,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                Visibility = isSelected ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                IsHitTestVisible = false
            };
            SequenceSoundEndMarkerCanvas.Children.Add(halo);

            var span = new System.Windows.Shapes.Line
            {
                X1 = startX,
                X2 = endX,
                Y1 = y,
                Y2 = y,
                Stroke = new System.Windows.Media.SolidColorBrush(trackColor),
                StrokeThickness = isSelected ? 5d : 3d,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                Opacity = isSelected ? 1d : 0.82d,
                IsHitTestVisible = false
            };
            SequenceSoundEndMarkerCanvas.Children.Add(span);

            const double markerWidth = 8d;
            const double markerHeight = 12d;
            var markerCenterY = y - markerStackIndex * 18d;
            if (markerStackIndex > 0)
            {
                SequenceSoundEndMarkerCanvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = endX,
                    X2 = endX,
                    Y1 = markerCenterY + markerHeight / 2d,
                    Y2 = y,
                    Stroke = new System.Windows.Media.SolidColorBrush(trackColor),
                    StrokeThickness = 1.5d,
                    IsHitTestVisible = false
                });
            }

            var marker = new System.Windows.Controls.Border
            {
                Width = isSelected ? 11d : markerWidth,
                Height = isSelected ? 16d : markerHeight,
                CornerRadius = new System.Windows.CornerRadius(2d),
                Background = new System.Windows.Media.SolidColorBrush(trackColor),
                BorderBrush = isSelected ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
                BorderThickness = new System.Windows.Thickness(isSelected ? 2d : 1d),
                ToolTip = source.Card.PlaybackEndMarkerToolTip,
                IsHitTestVisible = true
            };
            System.Windows.Controls.ToolTipService.SetInitialShowDelay(marker, 250);
            System.Windows.Controls.Canvas.SetLeft(marker, endX - marker.Width / 2d);
            System.Windows.Controls.Canvas.SetTop(marker, markerCenterY - marker.Height / 2d);
            SequenceSoundEndMarkerCanvas.Children.Add(marker);

            var labelText = new System.Windows.Controls.TextBlock
            {
                Text = $"End · {source.Card.SoundEffectLabel}",
                Foreground = new System.Windows.Media.SolidColorBrush(trackColor),
                FontSize = 10d,
                FontWeight = isSelected ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Medium,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                MaxWidth = 180d,
                ToolTip = source.Card.PlaybackEndMarkerToolTip,
                IsHitTestVisible = false
            };
            labelText.Measure(new System.Windows.Size(180d, double.PositiveInfinity));
            var label = new System.Windows.Controls.Border
            {
                Child = labelText,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(238, 255, 255, 255)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(trackColor),
                BorderThickness = new System.Windows.Thickness(isSelected ? 1.5d : 1d),
                CornerRadius = new System.Windows.CornerRadius(2d),
                Padding = new System.Windows.Thickness(3d, 1d, 3d, 1d),
                IsHitTestVisible = false
            };
            label.Measure(new System.Windows.Size(186d, double.PositiveInfinity));
            var labelWidth = label.DesiredSize.Width;
            var labelHeight = label.DesiredSize.Height;
            var labelLeft = endX + 7d;
            if (labelLeft + labelWidth > SequenceSoundEndMarkerCanvas.Width)
            {
                labelLeft = Math.Max(0d, endX - labelWidth - 7d);
            }

            var labelTop = markerCenterY - labelHeight / 2d;
            System.Windows.Controls.Canvas.SetLeft(label, labelLeft);
            System.Windows.Controls.Canvas.SetTop(label, labelTop);
            SequenceSoundEndMarkerCanvas.Children.Add(label);

            _soundPlaybackTrackVisuals.Add(new SoundPlaybackTrackVisual
            {
                StepId = source.Card.Step.StepId,
                Halo = halo,
                Span = span,
                Marker = marker,
                Label = labelText,
                MarkerCenterX = endX,
                MarkerCenterY = markerCenterY
            });
        }
    }

    private void UpdateSoundPlaybackTrackSelection()
    {
        var selectedStepId = (DataContext as StoryboardDesigner.App.ViewModels.MainWindowViewModel)?.SelectedSequenceStep?.StepId;
        foreach (var track in _soundPlaybackTrackVisuals)
        {
            var isSelected = track.StepId == selectedStepId;
            track.Halo.Visibility = isSelected ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            track.Span.StrokeThickness = isSelected ? 5d : 3d;
            track.Span.Opacity = isSelected ? 1d : 0.82d;
            track.Marker.Width = isSelected ? 11d : 8d;
            track.Marker.Height = isSelected ? 16d : 12d;
            track.Marker.BorderThickness = new System.Windows.Thickness(isSelected ? 2d : 1d);
            System.Windows.Controls.Canvas.SetLeft(track.Marker, track.MarkerCenterX - track.Marker.Width / 2d);
            System.Windows.Controls.Canvas.SetTop(track.Marker, track.MarkerCenterY - track.Marker.Height / 2d);
            track.Label.FontWeight = isSelected ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Medium;
            track.Label.FontSize = isSelected ? 11d : 10d;
            if (track.Label.Parent is System.Windows.Controls.Border labelBorder)
            {
                labelBorder.BorderThickness = new System.Windows.Thickness(isSelected ? 1.5d : 1d);
                System.Windows.Controls.Panel.SetZIndex(labelBorder, isSelected ? 3 : 1);
            }

            System.Windows.Controls.Panel.SetZIndex(track.Halo, isSelected ? 1 : 0);
            System.Windows.Controls.Panel.SetZIndex(track.Span, isSelected ? 2 : 0);
            System.Windows.Controls.Panel.SetZIndex(track.Marker, isSelected ? 4 : 2);
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
