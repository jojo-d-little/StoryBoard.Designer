using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Enums;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Rules.Project;
using Storyboard.Shared.GameServices.References;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.ViewModels;

public enum SequenceTimelineToolMode
{
    Select,
    Move,
    Ripple,
    Relocate
}

public sealed partial class MainWindowViewModel
{
    public sealed class SequenceTimelineTimeSliceViewModel
    {
        public int OffsetMs { get; init; }
        public int RangeStartOffsetMs { get; init; }
        public int RangeEndOffsetMs { get; init; }
        public bool IsRange => RangeEndOffsetMs > RangeStartOffsetMs;
        public string RulerLabel { get; init; } = string.Empty;
        public bool ShowRulerLabel { get; init; }
        public bool HasSteps { get; init; }
        public bool IsDisplayGap { get; init; }
        public double Width { get; init; }
        public ObservableCollection<SequenceTimelineBranchLaneViewModel> Lanes { get; } = new();
    }

    public sealed class SequenceTimelineBranchLaneViewModel
    {
        public string BranchId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool IsMainBranch => string.Equals(BranchId, MainSequenceBranchId, StringComparison.Ordinal);
        public bool IsSuccessBranch => BranchId.EndsWith("-success", StringComparison.Ordinal)
            || BranchId.Contains("-success-", StringComparison.Ordinal);
        public bool IsFailureBranch => BranchId.EndsWith("-failure", StringComparison.Ordinal)
            || BranchId.Contains("-failure-", StringComparison.Ordinal);
        public double Height { get; init; }
        public System.Windows.Media.Brush LaneBackground { get; init; } = System.Windows.Media.Brushes.White;
        public System.Windows.Media.Brush LaneBorder { get; init; } = System.Windows.Media.Brushes.LightGray;
        public ObservableCollection<SequenceTimelineColumnViewModel> Columns { get; } = new();
    }

    public sealed class SequenceTimelineColumnViewModel
    {
        public ObservableCollection<SequenceTimelineCardViewModel> Cards { get; } = new();
    }

    public sealed class SequenceTimelineCardViewModel
    {
        public ProjectSequenceStepDto Step { get; init; } = null!;
        public string StepLabel { get; init; } = string.Empty;
        public bool IsAction => Step.StepType == SequenceStepType.Action;
        public bool IsSound => Step.StepType == SequenceStepType.Sound;
        public bool HasActionRouteTargets => IsAction && (Step.ContinuationMode ?? ContinuationMode.Continue) is ContinuationMode.Branch or ContinuationMode.Merge;
        public bool HasSoundMergeTarget => IsSound && (Step.ContinuationMode ?? ContinuationMode.Continue) == ContinuationMode.Merge;
        public string BranchDisplayName { get; init; } = string.Empty;
        public string RoutingLabel => $"{BranchDisplayName} · {Step.ContinuationMode ?? ContinuationMode.Continue}";
        public string DisplayTitle => Step.StepType == SequenceStepType.Sound ? "Sound effect" : "Action";
        public string ActionDisplayName { get; init; } = string.Empty;
        public string ActionSourceLabel { get; init; } = string.Empty;
        public string SoundEffectLabel { get; init; } = string.Empty;
        public string SoundDurationLabel { get; init; } = string.Empty;
        public int? PlaybackStartOffsetMs { get; init; }
        public int? PlaybackEndOffsetMs { get; init; }
        public string PlaybackEndMarkerToolTip { get; init; } = string.Empty;
        public string SuccessTargetLabel { get; init; } = string.Empty;
        public string FailureTargetLabel { get; init; } = string.Empty;
        public string MergeTargetLabel { get; init; } = string.Empty;
        public bool HasValidationIssues { get; init; }
        public string ValidationMessage { get; init; } = string.Empty;
    }

    public sealed class SequenceStepTargetChoice : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;

        public Guid? StepId { get; init; }
        public bool IsPreservedCurrentTarget { get; init; }
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (string.Equals(_displayName, value, StringComparison.Ordinal)) return;
                _displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public const string MainSequenceBranchId = "main";

    private sealed record SequenceSoundPlaybackTiming(
        SoundEffectChoiceItem Choice,
        int StartOffsetMs,
        int EndOffsetMs,
        int DurationMs);

    private ScopeNodeBase? _selectedSequenceScope;
    private ProjectSequenceDefinitionDto? _selectedSequenceDefinition;
    private ProjectSequenceStepDto? _selectedSequenceStep;
    private readonly ObservableCollection<ProjectSequenceDefinitionDto> _sequenceDefinitions = new();
    private readonly ObservableCollection<ProjectSequenceStepDto> _sequenceSteps = new();
    private readonly ObservableCollection<SequenceTimelineTimeSliceViewModel> _sequenceTimeSlices = new();
    private readonly ObservableCollection<SequenceTimelineTimeSliceViewModel> _sequenceFlowTimeSlices = new();
    private readonly ObservableCollection<SequenceStepTargetChoice> _sequenceStepTargetChoices = new();
    private readonly Dictionary<Guid, IReadOnlyList<SequenceDefinitionIssue>> _sequenceValidationResults = new();
    private IReadOnlyList<SoundEffectChoiceItem> _sequenceSoundEffectChoices = Array.Empty<SoundEffectChoiceItem>();
    private double _sequenceZoom = 1d;
    private SequenceTimelineToolMode _sequenceTimelineToolMode = SequenceTimelineToolMode.Select;
    private string _sequenceContinuationEditMessage = string.Empty;
    private double _lastSequenceTimelineModelMs;
    private double _lastSequenceRefreshStepsMs;
    private double _lastSequenceRouteLabelsMs;
    private SequenceProjectEditPerformance? _lastSequenceProjectEditPerformance;
    private readonly Queue<SequenceEditPerformanceSample> _pendingSequenceEditPerformanceSamples = new();

    private sealed record SequenceProjectEditPerformance(
        double TotalMs,
        double AttachParentsMs,
        double NormalizeSelectionsMs,
        double ValidationProjectionMs,
        double MarkDirtyMs,
        double StartMarkersMs,
        double SharedPropertiesMs);

    private sealed record SequenceEditPerformanceSample(
        string EditKind,
        Stopwatch TotalElapsed,
        double SynchronousMs,
        double RefreshStepsMs,
        double RouteLabelsMs,
        double TimelineModelMs,
        SequenceProjectEditPerformance? ProjectEdit,
        int StepCount,
        int BranchCount,
        int TimeSliceCount);

    private void ResetSequencePerformanceMeasurements()
    {
        _lastSequenceTimelineModelMs = 0d;
        _lastSequenceRefreshStepsMs = 0d;
        _lastSequenceRouteLabelsMs = 0d;
        _lastSequenceProjectEditPerformance = null;
    }

    private void QueueSequenceEditPerformance(string editKind, Stopwatch totalElapsed)
    {
        _pendingSequenceEditPerformanceSamples.Enqueue(new SequenceEditPerformanceSample(
            editKind,
            totalElapsed,
            totalElapsed.Elapsed.TotalMilliseconds,
            _lastSequenceRefreshStepsMs,
            _lastSequenceRouteLabelsMs,
            _lastSequenceTimelineModelMs,
            _lastSequenceProjectEditPerformance,
            _sequenceSteps.Count,
            SelectedSequenceDefinition?.Branches?.Count ?? 0,
            _sequenceTimeSlices.Count));
    }

    internal void CompleteSequenceEditPerformance(string flowDetails)
    {
        if (_pendingSequenceEditPerformanceSamples.Count == 0)
        {
            AppendDiagnosticConsoleLine($"[SEQUENCE-PERF] {flowDetails}");
            return;
        }

        while (_pendingSequenceEditPerformanceSamples.TryDequeue(out var sample))
        {
            var projectEdit = sample.ProjectEdit;
            var projectSummary = projectEdit is null
                ? "project-edit=n/a"
                : $"project-edit={projectEdit.TotalMs:F1}ms [validation={projectEdit.ValidationProjectionMs:F1}ms, "
                    + $"shared-properties={projectEdit.SharedPropertiesMs:F1}ms, start-markers={projectEdit.StartMarkersMs:F1}ms]";
            AppendDiagnosticConsoleLine(
                $"[SEQUENCE-PERF] {sample.EditKind} wall-to-flow-redraw={sample.TotalElapsed.Elapsed.TotalMilliseconds:F1}ms; "
                + $"synchronous={sample.SynchronousMs:F1}ms; refresh-steps={sample.RefreshStepsMs:F1}ms; "
                + $"route-labels={sample.RouteLabelsMs:F1}ms; timeline-model={sample.TimelineModelMs:F1}ms; "
                + $"{projectSummary}; steps={sample.StepCount}; branches={sample.BranchCount}; slices={sample.TimeSliceCount}; "
                + $"{flowDetails}");
        }
    }

    public ScopeNodeBase? SelectedSequenceScope
    {
        get => _selectedSequenceScope;
        private set
        {
            if (ReferenceEquals(_selectedSequenceScope, value)) return;
            _selectedSequenceScope = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSequenceScopeName));
            OnPropertyChanged(nameof(HasSelectedSequenceScope));
            RefreshSequenceDefinitions();
            RefreshSequenceSoundEffectChoices();
        }
    }

    public string SelectedSequenceScopeName => _selectedSequenceScope?.ScopeName ?? string.Empty;
    public bool HasSelectedSequenceScope => _selectedSequenceScope is not null;
    public bool HasSelectedSequenceSteps => _selectedSequenceDefinition?.Steps?.Count > 0;
    public Guid? SelectedSequenceSoundEffectId
    {
        get => SelectedSequenceStep?.SoundEffectId;
        set
        {
            if (SelectedSequenceStep is not { StepType: SequenceStepType.Sound } step
                || step.SoundEffectId == value)
            {
                return;
            }

            step.SoundEffectId = value;
            OnPropertyChanged();
            RefreshSequenceTimeline();
            NotifySequenceProjectEdited();
        }
    }
    public bool IsSelectedSequenceSoundStep => SelectedSequenceStep?.StepType == SequenceStepType.Sound;
    public bool IsSelectedSequenceActionStep => SelectedSequenceStep?.StepType == SequenceStepType.Action;
    public bool IsSelectedSequenceInlineAction => IsSelectedSequenceActionStep && SelectedSequenceStep?.InlineAction is not null;
    public Guid? SelectedSequenceActionId
    {
        get => SelectedSequenceStep is { StepType: SequenceStepType.Action, InlineAction: null } step ? step.ActionId : null;
        set
        {
            if (SelectedSequenceStep is not { StepType: SequenceStepType.Action } step
                || value is not Guid actionId
                || step.InlineAction is null && step.ActionId == actionId
                || !GetSequenceActionPickerChoices().Any(choice => choice.Action.Id == actionId))
            {
                return;
            }

            // Replace only the action reference. Keep this step's identity, timing,
            // branch, and outcome targets intact.
            step.ActionId = actionId;
            step.InlineAction = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSelectedSequenceInlineAction));
            OnPropertyChanged(nameof(SelectedSequenceActionDisplayName));
            OnPropertyChanged(nameof(SelectedSequenceActionSourceDescription));
            OnPropertyChanged(nameof(SequenceActionPickerButtonText));
            _editSequenceActionCommand?.RaiseCanExecuteChanged();
            RefreshSequenceTimeline();
            NotifySequenceProjectEdited();
        }
    }
    public string SequenceActionPickerButtonText => IsSelectedSequenceInlineAction
        ? "Replace with shared action…"
        : "Choose shared action…";
    public string SelectedSequenceActionDisplayName => SelectedSequenceStep is null
        ? string.Empty
        : GetSequenceActionDisplayName(SelectedSequenceStep);
    public string SelectedSequenceActionSourceDescription => SelectedSequenceStep is not { } step
        ? string.Empty
        : step.InlineAction is not null
            ? "Inline action"
            : step.ActionId is Guid actionId
                ? ResolveSequenceAction(actionId) is { } action
                    ? $"Shared action · {action.ScopeName}"
                    : "Shared action · missing reference"
                : "Action not configured";
    public bool IsSelectedSequenceActionRouteMode => IsSelectedSequenceActionStep
        && SelectedSequenceContinuationMode is ContinuationMode.Branch or ContinuationMode.Merge;
    public bool IsSelectedSequenceSoundMergeMode => IsSelectedSequenceSoundStep
        && SelectedSequenceContinuationMode == ContinuationMode.Merge;
    public string SelectedSequenceContinuationDescription => !string.IsNullOrWhiteSpace(_sequenceContinuationEditMessage)
        ? _sequenceContinuationEditMessage
        : SelectedSequenceContinuationMode switch
        {
            ContinuationMode.Continue => "Continue to the next step on this branch, regardless of action result.",
            ContinuationMode.Branch => "Success and failure each transfer to the first step on a new branch. An unset outcome ends the run.",
            ContinuationMode.Merge => IsSelectedSequenceActionStep
                ? "Choose a same-time or future destination on another branch for each action outcome. An unset outcome ends the run."
                : "Transfer to a same-time or future step on another branch.",
            ContinuationMode.End => "End the sequence after this step, regardless of action result.",
            _ => string.Empty
        };
    public string SequenceValidationSummary => string.Join(" ", GetSelectedSequenceValidationIssues()
        .Where(static issue => issue.StepId is null)
        .Select(static issue => issue.Description)
        .Distinct(StringComparer.OrdinalIgnoreCase));
    public ObservableCollection<ProjectSequenceDefinitionDto> SequenceDefinitions => _sequenceDefinitions;
    public ObservableCollection<ProjectSequenceStepDto> SequenceSteps => _sequenceSteps;
    public ObservableCollection<SequenceTimelineTimeSliceViewModel> SequenceTimeSlices => _sequenceTimeSlices;
    public ObservableCollection<SequenceTimelineTimeSliceViewModel> SequenceFlowTimeSlices => _sequenceFlowTimeSlices;
    public ObservableCollection<SequenceStepTargetChoice> SequenceStepTargetChoices => _sequenceStepTargetChoices;
    public IReadOnlyList<ProjectSequenceBranchDto> SequenceBranchDefinitions => SelectedSequenceDefinition is { } definition
        ? definition.Branches
        : Array.Empty<ProjectSequenceBranchDto>();
    public IReadOnlyList<SequenceStepTargetChoice> SequenceMergeTargetChoices => _sequenceStepTargetChoices
        .Where(static choice => choice.StepId.HasValue)
        .ToList();
    public IReadOnlyList<SoundEffectChoiceItem> SequenceSoundEffectChoices => _sequenceSoundEffectChoices;
    public IReadOnlyList<ContinuationMode> SelectedSequenceContinuationModeChoices
    {
        get
        {
            var onMainBranch = string.Equals(SelectedSequenceStep?.BranchId, MainSequenceBranchId, StringComparison.Ordinal);
            if (IsSelectedSequenceSoundStep)
            {
                return onMainBranch
                    ? new[] { ContinuationMode.Continue, ContinuationMode.End }
                    : new[] { ContinuationMode.Continue, ContinuationMode.Merge, ContinuationMode.End };
            }

            return onMainBranch
                ? new[] { ContinuationMode.Continue, ContinuationMode.Branch, ContinuationMode.End }
                : Enum.GetValues<ContinuationMode>();
        }
    }
    public IReadOnlyList<string> SequenceBranchChoices => (SelectedSequenceDefinition?.Branches ?? new List<ProjectSequenceBranchDto>())
        .Select(static branch => branch.BranchId)
        .Where(static id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static id => id == MainSequenceBranchId ? 0 : 1)
        .ToList();

    public string GetSequenceBranchDisplayName(string branchId) => SelectedSequenceDefinition?.Branches?
        .FirstOrDefault(branch => string.Equals(branch.BranchId, branchId, StringComparison.Ordinal))?.DisplayName
        ?? (branchId == MainSequenceBranchId
            ? "Main branch"
            : string.Join(" ", branchId.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => part.Length == 1 ? part.ToUpperInvariant() : char.ToUpperInvariant(part[0]) + part[1..])));

    public bool TryRenameSequenceBranch(string branchId, string displayName, out string errorMessage)
    {
        errorMessage = string.Empty;
        var branches = SelectedSequenceDefinition?.Branches;
        var branch = branches?.FirstOrDefault(candidate => string.Equals(candidate.BranchId, branchId, StringComparison.Ordinal));
        var normalizedName = (displayName ?? string.Empty).Trim();
        if (branch is null || string.IsNullOrWhiteSpace(normalizedName))
        {
            errorMessage = branch is null ? "This branch is no longer in the open sequence." : "Enter a branch name.";
            return false;
        }

        if (branches!.Any(candidate => !ReferenceEquals(candidate, branch)
                                       && string.Equals(candidate.DisplayName?.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = "A branch with that display name already exists in this sequence.";
            return false;
        }

        if (string.Equals(branch.DisplayName, normalizedName, StringComparison.Ordinal))
        {
            return true;
        }

        branch.DisplayName = normalizedName;
        OnPropertyChanged(nameof(SequenceBranchDefinitions));
        OnPropertyChanged(nameof(SequenceBranchChoices));
        RefreshSequenceSteps();
        NotifySequenceProjectEdited();
        return true;
    }

    public bool SetSequenceBranchDisplayColor(string branchId, string displayColor)
    {
        var branch = SelectedSequenceDefinition?.Branches?
            .FirstOrDefault(candidate => string.Equals(candidate.BranchId, branchId, StringComparison.Ordinal));
        if (branch is null || !IsValidBranchDisplayColor(displayColor)
            || string.Equals(branch.DisplayColor, displayColor, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        branch.DisplayColor = displayColor.ToUpperInvariant();
        OnPropertyChanged(nameof(SequenceBranchDefinitions));
        RefreshSequenceTimeline();
        NotifySequenceProjectEdited();
        return true;
    }

    public bool CanRemoveEmptySequenceBranch(string branchId)
    {
        var definition = SelectedSequenceDefinition;
        return definition is not null
            && !string.Equals(branchId, MainSequenceBranchId, StringComparison.Ordinal)
            && definition.Branches?.Any(branch => string.Equals(branch.BranchId, branchId, StringComparison.Ordinal)) == true
            && !(definition.Steps ?? new List<ProjectSequenceStepDto>())
                .Any(step => string.Equals(step.BranchId, branchId, StringComparison.Ordinal));
    }

    public bool RemoveEmptySequenceBranch(string branchId)
    {
        if (!CanRemoveEmptySequenceBranch(branchId)
            || SelectedSequenceDefinition?.Branches is not { } branches)
        {
            return false;
        }

        var branch = branches.First(candidate => string.Equals(candidate.BranchId, branchId, StringComparison.Ordinal));
        branches.Remove(branch);
        OnPropertyChanged(nameof(SequenceBranchDefinitions));
        OnPropertyChanged(nameof(SequenceBranchChoices));
        RefreshSequenceTimeline();
        NotifySequenceProjectEdited();
        return true;
    }

    private static bool IsValidBranchDisplayColor(string? color) => color is { Length: 7 }
        && color[0] == '#'
        && color.AsSpan(1).ToArray().All(Uri.IsHexDigit);

    private static ProjectSequenceBranchDto CreateSequenceBranch(string branchId, string displayName, string displayColor) => new()
    {
        BranchId = branchId,
        DisplayName = displayName,
        DisplayColor = displayColor
    };

    private static string DefaultBranchDisplayColor(string branchId, int fallbackIndex = 0)
    {
        if (branchId == MainSequenceBranchId) return "#64748B";
        if (branchId.EndsWith("-success", StringComparison.Ordinal) || branchId.Contains("-success-", StringComparison.Ordinal)) return "#16A34A";
        if (branchId.EndsWith("-failure", StringComparison.Ordinal) || branchId.Contains("-failure-", StringComparison.Ordinal)) return "#DC2626";

        var palette = new[] { "#2563EB", "#9333EA", "#D97706", "#0D9488", "#DB2777", "#4F46E5" };
        return palette[Math.Abs(fallbackIndex) % palette.Length];
    }

    private static string HumanizeBranchId(string branchId) => branchId == MainSequenceBranchId
        ? "Main branch"
        : string.Join(" ", branchId.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Length == 1 ? part.ToUpperInvariant() : char.ToUpperInvariant(part[0]) + part[1..]));

    private static bool EnsureSequenceBranchDefinitions(ProjectSequenceDefinitionDto definition)
    {
        definition.Branches ??= new List<ProjectSequenceBranchDto>();
        var changed = false;
        if (!definition.Branches.Any(static branch => string.Equals(branch.BranchId, MainSequenceBranchId, StringComparison.Ordinal)))
        {
            definition.Branches.Add(CreateSequenceBranch(MainSequenceBranchId, "Main branch", DefaultBranchDisplayColor(MainSequenceBranchId)));
            changed = true;
        }

        foreach (var branch in definition.Branches)
        {
            if (string.IsNullOrWhiteSpace(branch.DisplayName))
            {
                branch.DisplayName = HumanizeBranchId(branch.BranchId);
                changed = true;
            }

            if (!IsValidBranchDisplayColor(branch.DisplayColor))
            {
                branch.DisplayColor = DefaultBranchDisplayColor(branch.BranchId);
                changed = true;
            }
        }

        var branchIds = (definition.Steps ?? new List<ProjectSequenceStepDto>())
            .Select(static step => step.BranchId)
            .Where(static branchId => !string.IsNullOrWhiteSpace(branchId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static branchId => branchId, StringComparer.Ordinal)
            .ToList();
        for (var index = 0; index < branchIds.Count; index++)
        {
            var branchId = branchIds[index];
            if (definition.Branches.Any(branch => string.Equals(branch.BranchId, branchId, StringComparison.Ordinal)))
            {
                continue;
            }

            definition.Branches.Add(CreateSequenceBranch(
                branchId,
                HumanizeBranchId(branchId),
                DefaultBranchDisplayColor(branchId, index)));
            changed = true;
        }

        return changed;
    }

    private SequenceTimelineBranchLaneViewModel CreateSequenceBranchLane(string branchId, double height)
    {
        var branch = SelectedSequenceDefinition?.Branches?
            .FirstOrDefault(candidate => string.Equals(candidate.BranchId, branchId, StringComparison.Ordinal));
        var colorText = branch?.DisplayColor ?? DefaultBranchDisplayColor(branchId);
        var color = IsValidBranchDisplayColor(colorText)
            ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorText)!
            : System.Windows.Media.Colors.SlateGray;
        var background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(34, color.R, color.G, color.B));
        var border = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(150, color.R, color.G, color.B));
        background.Freeze();
        border.Freeze();

        return new SequenceTimelineBranchLaneViewModel
        {
            BranchId = branchId,
            DisplayName = branch?.DisplayName ?? HumanizeBranchId(branchId),
            Height = height,
            LaneBackground = background,
            LaneBorder = border
        };
    }

    public bool CanAddSequenceStepAtOffset(int offset, string branchId)
        => GetSequenceStepAddDisabledReason(offset, branchId) is null;

    public string? GetSequenceStepAddDisabledReason(int offset, string branchId)
    {
        if (SelectedSequenceDefinition?.Steps is not { } steps)
        {
            return "Open a sequence before adding a step.";
        }

        if (offset < 0 || offset % 50 != 0)
        {
            return "Choose a timeline position aligned to a 50 ms time slot.";
        }

        if (!SequenceBranchChoices.Contains(branchId, StringComparer.Ordinal))
        {
            return "This branch is no longer available in the open sequence.";
        }

        // A branch lane is an authoring surface, not a closed time range. Allow
        // temporary insertions before its current first step or after its current
        // terminal step so authors can add and then rewire/refactor the flow.
        return null;
    }

    public string? GetForkOutcomeAddDisabledReason(int offset)
    {
        if (SelectedSequenceStep is not { StepType: SequenceStepType.Action } source
            || (source.ContinuationMode ?? ContinuationMode.Continue) != ContinuationMode.Branch)
        {
            return "Select an action step in Branch mode before adding an outcome step.";
        }

        if (offset < source.OffsetMs)
        {
            return $"An outcome branch cannot start before its source step ({source.OffsetMs:N0} ms). Choose that time or later.";
        }

        return null;
    }
    public string SelectedSequenceBranchId
    {
        get => string.IsNullOrWhiteSpace(SelectedSequenceStep?.BranchId) ? MainSequenceBranchId : SelectedSequenceStep.BranchId;
        set
        {
            if (SelectedSequenceStep is null || string.IsNullOrWhiteSpace(value)
                || string.Equals(SelectedSequenceStep.BranchId, value, StringComparison.Ordinal))
            {
                return;
            }

            SelectedSequenceStep.BranchId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SequenceBranchChoices));
            OnPropertyChanged(nameof(SelectedSequenceContinuationModeChoices));
            RefreshSequenceSteps();
            NotifySequenceProjectEdited();
        }
    }
    public ContinuationMode SelectedSequenceContinuationMode
    {
        get => SelectedSequenceStep?.ContinuationMode ?? ContinuationMode.Continue;
        set => SetSelectedSequenceContinuationMode(value);
    }
    public Guid? SelectedSequenceSuccessTargetStepId
    {
        get => SelectedSequenceStep?.OnSuccessStepId;
        set => SetSelectedStepTarget(value, static step => step.OnSuccessStepId, static (step, target) => step.OnSuccessStepId = target, nameof(SelectedSequenceSuccessTargetStepId));
    }
    public SequenceStepTargetChoice? SelectedSequenceSuccessTargetChoice
    {
        get => _sequenceStepTargetChoices.FirstOrDefault(choice => choice.StepId == SelectedSequenceSuccessTargetStepId);
        set
        {
            // WPF can report a transient null SelectedItem while a bound ItemsSource
            // is rebuilt. Only the explicit "End this outcome" choice may clear a route.
            if (value is not null) SelectedSequenceSuccessTargetStepId = value.StepId;
        }
    }
    public Guid? SelectedSequenceFailureTargetStepId
    {
        get => SelectedSequenceStep?.OnFailureStepId;
        set => SetSelectedStepTarget(value, static step => step.OnFailureStepId, static (step, target) => step.OnFailureStepId = target, nameof(SelectedSequenceFailureTargetStepId));
    }
    public SequenceStepTargetChoice? SelectedSequenceFailureTargetChoice
    {
        get => _sequenceStepTargetChoices.FirstOrDefault(choice => choice.StepId == SelectedSequenceFailureTargetStepId);
        set
        {
            if (value is not null) SelectedSequenceFailureTargetStepId = value.StepId;
        }
    }
    public Guid? SelectedSequenceMergeTargetStepId
    {
        get => SelectedSequenceStep?.MergeTargetStepId;
        set => SetSelectedStepTarget(value, static step => step.MergeTargetStepId, static (step, target) => step.MergeTargetStepId = target, nameof(SelectedSequenceMergeTargetStepId));
    }
    public SequenceStepTargetChoice? SelectedSequenceMergeTargetChoice
    {
        get => _sequenceStepTargetChoices.FirstOrDefault(choice => choice.StepId == SelectedSequenceMergeTargetStepId);
        set
        {
            if (value is not null) SelectedSequenceMergeTargetStepId = value.StepId;
        }
    }
    public double SequenceZoom
    {
        get => _sequenceZoom;
        private set
        {
            var bounded = Math.Clamp(value, 0.2d, 2d);
            if (Math.Abs(_sequenceZoom - bounded) < 0.001d) return;
            _sequenceZoom = bounded;
            OnPropertyChanged();
        }
    }

    public bool IsSequenceSelectToolActive
    {
        get => _sequenceTimelineToolMode == SequenceTimelineToolMode.Select;
        set { if (value) SetSequenceTimelineTool(SequenceTimelineToolMode.Select); }
    }
    public bool IsSequenceMoveToolActive
    {
        get => _sequenceTimelineToolMode == SequenceTimelineToolMode.Move;
        set { if (value) SetSequenceTimelineTool(SequenceTimelineToolMode.Move); }
    }
    public bool IsSequenceRippleToolActive
    {
        get => _sequenceTimelineToolMode == SequenceTimelineToolMode.Ripple;
        set { if (value) SetSequenceTimelineTool(SequenceTimelineToolMode.Ripple); }
    }
    public bool IsSequenceRelocateToolActive
    {
        get => _sequenceTimelineToolMode == SequenceTimelineToolMode.Relocate;
        set { if (value) SetSequenceTimelineTool(SequenceTimelineToolMode.Relocate); }
    }
    public SequenceTimelineToolMode SequenceTimelineTool => _sequenceTimelineToolMode;

    public void SetSequenceZoom(double value) => SequenceZoom = value;

    private void SetSequenceTimelineTool(SequenceTimelineToolMode mode)
    {
        if (_sequenceTimelineToolMode == mode)
        {
            return;
        }

        _sequenceTimelineToolMode = mode;
        OnPropertyChanged(nameof(SequenceTimelineTool));
        OnPropertyChanged(nameof(IsSequenceSelectToolActive));
        OnPropertyChanged(nameof(IsSequenceMoveToolActive));
        OnPropertyChanged(nameof(IsSequenceRippleToolActive));
        OnPropertyChanged(nameof(IsSequenceRelocateToolActive));
    }

    public string GetSequenceRelocationPreviewDescription(
        ProjectSequenceStepDto step,
        int targetOffsetMs,
        string targetBranchId)
    {
        var stepNumber = _sequenceSteps.IndexOf(step) + 1;
        var sourceBranchName = GetSequenceBranchDisplayName(step.BranchId ?? MainSequenceBranchId);
        var targetBranchName = GetSequenceBranchDisplayName(targetBranchId);
        var insertionNote = SelectedSequenceDefinition?.Steps?.Any(candidate =>
                !ReferenceEquals(candidate, step)
                && candidate.OffsetMs == targetOffsetMs
                && string.Equals(candidate.BranchId, targetBranchId, StringComparison.Ordinal)) == true
            ? "at the end of that branch's steps in this time slot"
            : "at this time on the destination branch";

        return $"Relocate Step {stepNumber} from {sourceBranchName} to {targetBranchName} at {targetOffsetMs:N0} ms, {insertionNote}. Explicit outcome links stay attached; Continue flow follows the new branch order.";
    }

    public bool RelocateSequenceStepTo(
        ProjectSequenceStepDto step,
        int targetOffsetMs,
        string targetBranchId)
    {
        if (step is null
            || SelectedSequenceDefinition?.Steps is not { } steps
            || !steps.Contains(step)
            || targetOffsetMs < 0
            || targetOffsetMs % 50 != 0
            || !SequenceBranchChoices.Contains(targetBranchId, StringComparer.Ordinal)
            || _sequenceTimelineToolMode != SequenceTimelineToolMode.Relocate
            || (step.OffsetMs == targetOffsetMs
                && string.Equals(step.BranchId, targetBranchId, StringComparison.Ordinal)))
        {
            return false;
        }

        ResetSequencePerformanceMeasurements();
        var elapsed = Stopwatch.StartNew();
        var sourceOffsetMs = step.OffsetMs;
        var sourceBranchId = step.BranchId ?? MainSequenceBranchId;
        var destinationTimeGroup = steps
            .Where(candidate => !ReferenceEquals(candidate, step) && candidate.OffsetMs == targetOffsetMs)
            .OrderBy(static candidate => candidate.Order)
            .ThenBy(static candidate => candidate.StepId)
            .ToList();
        var destinationBranchSteps = destinationTimeGroup
            .Where(candidate => string.Equals(candidate.BranchId ?? MainSequenceBranchId, targetBranchId, StringComparison.Ordinal))
            .ToList();
        var terminalDestinationStep = destinationBranchSteps.FirstOrDefault(candidate =>
            (candidate.ContinuationMode ?? ContinuationMode.Continue) != ContinuationMode.Continue);
        var insertionIndex = terminalDestinationStep is not null
            ? destinationTimeGroup.IndexOf(terminalDestinationStep)
            : destinationBranchSteps.Count > 0
                ? destinationTimeGroup.IndexOf(destinationBranchSteps[^1]) + 1
                : destinationTimeGroup.Count;

        step.OffsetMs = targetOffsetMs;
        step.BranchId = targetBranchId;
        destinationTimeGroup.Insert(Math.Clamp(insertionIndex, 0, destinationTimeGroup.Count), step);
        for (var index = 0; index < destinationTimeGroup.Count; index++)
        {
            destinationTimeGroup[index].Order = index;
        }

        foreach (var timeGroup in steps.GroupBy(static candidate => candidate.OffsetMs))
        {
            var orderedGroup = timeGroup.OrderBy(static candidate => candidate.Order).ThenBy(static candidate => candidate.StepId).ToList();
            for (var index = 0; index < orderedGroup.Count; index++)
            {
                orderedGroup[index].Order = index;
            }
        }

        SelectedSequenceStep = step;
        RefreshSequenceSteps();
        OnPropertyChanged(nameof(SelectedSequenceStep));
        OnPropertyChanged(nameof(SelectedSequenceBranchId));
        NotifySequenceProjectEdited();
        QueueSequenceEditPerformance(
            $"relocate branch={GetSequenceBranchDisplayName(sourceBranchId)}->{GetSequenceBranchDisplayName(targetBranchId)}, "
            + $"from={sourceOffsetMs}ms to={targetOffsetMs}ms",
            elapsed);
        return true;
    }

    public bool MoveSequenceStepToOffset(ProjectSequenceStepDto step, int targetOffsetMs)
    {
        if (step is null
            || SelectedSequenceDefinition?.Steps is not { } steps
            || !steps.Contains(step)
            || targetOffsetMs < 0
            || _sequenceTimelineToolMode == SequenceTimelineToolMode.Select)
        {
            return false;
        }

        var sourceOffsetMs = step.OffsetMs;
        if (sourceOffsetMs == targetOffsetMs)
        {
            return false;
        }

        ResetSequencePerformanceMeasurements();
        var elapsed = Stopwatch.StartNew();
        var orderedBranch = steps
            .Where(candidate => !ReferenceEquals(candidate, step)
                && string.Equals(candidate.BranchId, step.BranchId, StringComparison.Ordinal))
            .OrderBy(static candidate => candidate.OffsetMs)
            .ThenBy(static candidate => candidate.Order)
            .ToList();
        var previousInBranch = orderedBranch.LastOrDefault(candidate =>
            candidate.OffsetMs < sourceOffsetMs || candidate.OffsetMs == sourceOffsetMs && candidate.Order < step.Order);
        var nextInBranch = orderedBranch.FirstOrDefault(candidate =>
            candidate.OffsetMs > sourceOffsetMs || candidate.OffsetMs == sourceOffsetMs && candidate.Order > step.Order);
        if (_sequenceTimelineToolMode == SequenceTimelineToolMode.Move
            && ((previousInBranch is not null && targetOffsetMs < previousInBranch.OffsetMs)
                || (nextInBranch is not null && targetOffsetMs > nextInBranch.OffsetMs)))
        {
            return false;
        }

        var offsetDelta = targetOffsetMs - sourceOffsetMs;
        List<ProjectSequenceStepDto>? rippleGroup = null;
        if (_sequenceTimelineToolMode == SequenceTimelineToolMode.Ripple)
        {
            // Ripple the whole time slot on this branch as one unit, along with every
            // later step on that branch. Other branches remain at their current times.
            rippleGroup = steps
                .Where(candidate => string.Equals(candidate.BranchId, step.BranchId, StringComparison.Ordinal)
                    && candidate.OffsetMs == sourceOffsetMs)
                .OrderBy(static candidate => candidate.Order)
                .ThenBy(static candidate => candidate.StepId)
                .ToList();

            foreach (var futureStep in steps.Where(candidate => string.Equals(candidate.BranchId, step.BranchId, StringComparison.Ordinal)
                         && candidate.OffsetMs >= sourceOffsetMs))
            {
                futureStep.OffsetMs += offsetDelta;
            }
        }

        step.OffsetMs = targetOffsetMs;
        List<ProjectSequenceStepDto> targetTimeGroup;
        if (rippleGroup is not null)
        {
            var rippleGroupSet = rippleGroup.ToHashSet();
            targetTimeGroup = steps
                .Where(candidate => candidate.OffsetMs == targetOffsetMs && !rippleGroupSet.Contains(candidate))
                .OrderBy(static candidate => candidate.Order)
                .ThenBy(static candidate => candidate.StepId)
                .ToList();
            targetTimeGroup.AddRange(rippleGroup);
        }
        else
        {
            targetTimeGroup = steps
                .Where(candidate => !ReferenceEquals(candidate, step) && candidate.OffsetMs == targetOffsetMs)
                .OrderBy(static candidate => candidate.Order)
                .ThenBy(static candidate => candidate.StepId)
                .ToList();
            var insertionIndex = targetTimeGroup.Count;
            if (nextInBranch?.OffsetMs == targetOffsetMs)
            {
                insertionIndex = targetTimeGroup.IndexOf(nextInBranch);
            }
            else if (previousInBranch?.OffsetMs == targetOffsetMs)
            {
                insertionIndex = targetTimeGroup.IndexOf(previousInBranch) + 1;
            }

            targetTimeGroup.Insert(Math.Clamp(insertionIndex, 0, targetTimeGroup.Count), step);
        }

        for (var index = 0; index < targetTimeGroup.Count; index++)
        {
            targetTimeGroup[index].Order = index;
        }

        foreach (var timeGroup in steps.GroupBy(static candidate => candidate.OffsetMs))
        {
            var orderedGroup = timeGroup.OrderBy(static candidate => candidate.Order).ThenBy(static candidate => candidate.StepId).ToList();
            for (var index = 0; index < orderedGroup.Count; index++)
            {
                orderedGroup[index].Order = index;
            }
        }

        SelectedSequenceStep = step;
        RefreshSequenceSteps();
        // Sequence-step DTOs do not notify for nested property changes. Rebind the selected-step
        // editor so fields such as OffsetMs reflect the completed drag immediately.
        OnPropertyChanged(nameof(SelectedSequenceStep));
        NotifySequenceProjectEdited();
        QueueSequenceEditPerformance(
            $"move mode={_sequenceTimelineToolMode}, branch={GetSequenceBranchDisplayName(step.BranchId)}, "
            + $"from={sourceOffsetMs}ms, to={targetOffsetMs}ms",
            elapsed);
        return true;
    }

    public void CommitSelectedSequenceStepTiming()
    {
        if (SelectedSequenceStep is null || SelectedSequenceDefinition?.Steps is not { } steps)
        {
            return;
        }

        foreach (var timeGroup in steps.GroupBy(static step => step.OffsetMs))
        {
            var ordered = timeGroup.OrderBy(static step => step.Order).ThenBy(static step => step.StepId).ToList();
            for (var index = 0; index < ordered.Count; index++) ordered[index].Order = index;
        }

        RefreshSequenceSteps();
        NotifySequenceProjectEdited();
    }

    private void RefreshSequenceSoundEffectChoices()
    {
        _sequenceSoundEffectChoices = GetSoundEffectChoicesForScopeNode(SelectedSequenceScope);
        OnPropertyChanged(nameof(SequenceSoundEffectChoices));
    }

    private void SetSelectedStepTarget(
        Guid? value,
        Func<ProjectSequenceStepDto, Guid?> getTarget,
        Action<ProjectSequenceStepDto, Guid?> setTarget,
        string propertyName)
    {
        if (SelectedSequenceStep is { } selectedStep)
        {
            SetSequenceStepTarget(selectedStep, value, getTarget, setTarget, propertyName);
        }
    }

    public void SetSequenceStepRouteTarget(Guid sourceStepId, string routeKind, Guid? targetStepId)
    {
        if (SelectedSequenceDefinition?.Steps is not { } steps
            || steps.FirstOrDefault(step => step.StepId == sourceStepId) is not { } source
            || (targetStepId.HasValue && !steps.Any(step => step.StepId == targetStepId.Value)))
        {
            return;
        }

        switch (routeKind)
        {
            case "Success":
                SetSequenceStepTarget(source, targetStepId, static step => step.OnSuccessStepId,
                    static (step, target) => step.OnSuccessStepId = target, nameof(SelectedSequenceSuccessTargetStepId));
                break;
            case "Failure":
                SetSequenceStepTarget(source, targetStepId, static step => step.OnFailureStepId,
                    static (step, target) => step.OnFailureStepId = target, nameof(SelectedSequenceFailureTargetStepId));
                break;
            case "Merge":
                SetSequenceStepTarget(source, targetStepId, static step => step.MergeTargetStepId,
                    static (step, target) => step.MergeTargetStepId = target, nameof(SelectedSequenceMergeTargetStepId));
                break;
        }
    }

    private void SetSequenceStepTarget(
        ProjectSequenceStepDto source,
        Guid? value,
        Func<ProjectSequenceStepDto, Guid?> getTarget,
        Action<ProjectSequenceStepDto, Guid?> setTarget,
        string propertyName)
    {
        if (getTarget(source) == value)
        {
            return;
        }

        ResetSequencePerformanceMeasurements();
        var elapsed = Stopwatch.StartNew();
        setTarget(source, value);
        _sequenceContinuationEditMessage = string.Empty;

        if (SelectedSequenceStep?.StepId == source.StepId)
        {
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(SelectedSequenceSuccessTargetChoice));
            OnPropertyChanged(nameof(SelectedSequenceFailureTargetChoice));
            OnPropertyChanged(nameof(SelectedSequenceMergeTargetChoice));
            OnPropertyChanged(nameof(SelectedSequenceContinuationDescription));
        }
        OnPropertyChanged(nameof(SequenceBranchChoices));
        RefreshSequenceTargetChoiceLabels();
        RefreshSequenceTimeline();
        NotifySequenceProjectEdited();
        QueueSequenceEditPerformance(
            $"route-edit route={propertyName}, linked={value.HasValue}",
            elapsed);
    }

    private void RefreshSequenceTargetChoiceLabels()
    {
        var elapsed = Stopwatch.StartNew();
        if (SelectedSequenceDefinition?.Steps is not { } steps)
        {
            _lastSequenceRouteLabelsMs = elapsed.Elapsed.TotalMilliseconds;
            return;
        }

        var orderedSteps = steps
            .OrderBy(static step => step.OffsetMs)
            .ThenBy(static step => step.Order)
            .ToList();
        var labelsByStepId = orderedSteps
            .Select((step, index) => (step.StepId, Label: $"Step {index + 1}"))
            .ToDictionary(static item => item.StepId, static item => item.Label);

        foreach (var choice in _sequenceStepTargetChoices.Where(static choice => choice.StepId.HasValue))
        {
            var target = orderedSteps.FirstOrDefault(step => step.StepId == choice.StepId);
            if (target is null) continue;
            var currentLinkNote = choice.IsPreservedCurrentTarget ? " · current link outside eligible range" : string.Empty;
            choice.DisplayName = $"{labelsByStepId[target.StepId]} · {GetSequenceActionDisplayName(target)} · {target.OffsetMs} ms · {GetSequenceBranchDisplayName(target.BranchId)}{currentLinkNote}";
        }

        _lastSequenceRouteLabelsMs = elapsed.Elapsed.TotalMilliseconds;
    }

    private void SetSelectedSequenceContinuationMode(ContinuationMode mode)
    {
        if (SelectedSequenceStep is not { } step
            || (step.StepType == SequenceStepType.Sound && mode == ContinuationMode.Branch)
            || (step.ContinuationMode ?? ContinuationMode.Continue) == mode)
        {
            return;
        }

        if (mode is ContinuationMode.Continue or ContinuationMode.End
            && (step.OnSuccessStepId.HasValue || step.OnFailureStepId.HasValue || step.MergeTargetStepId.HasValue))
        {
            _sequenceContinuationEditMessage = "Clear the configured outcome targets before changing this step to Continue or End.";
            OnPropertyChanged(nameof(SelectedSequenceContinuationMode));
            OnPropertyChanged(nameof(SelectedSequenceContinuationDescription));
            return;
        }

        _sequenceContinuationEditMessage = string.Empty;
        step.ContinuationMode = mode;
        switch (mode)
        {
            case ContinuationMode.Continue:
            case ContinuationMode.End:
                break;
            case ContinuationMode.Branch:
                step.MergeTargetStepId = null;
                break;
            case ContinuationMode.Merge when step.StepType == SequenceStepType.Action:
                step.MergeTargetStepId = null;
                break;
            case ContinuationMode.Merge:
                step.OnSuccessStepId = null;
                step.OnFailureStepId = null;
                break;
        }

        OnPropertyChanged(nameof(SelectedSequenceContinuationMode));
        OnPropertyChanged(nameof(IsSelectedSequenceActionRouteMode));
        OnPropertyChanged(nameof(IsSelectedSequenceSoundMergeMode));
        OnPropertyChanged(nameof(SelectedSequenceContinuationDescription));
        OnPropertyChanged(nameof(SelectedSequenceContinuationModeChoices));
        RefreshSequenceSteps();
        NotifySequenceProjectEdited();
    }

    private ProjectSequenceBranchDto CreateForkBranch(ProjectSequenceStepDto source, string outcome)
    {
        var outcomeName = string.Equals(outcome, "success", StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure";
        var sourceName = GetSequenceActionDisplayName(source);
        var displayName = string.IsNullOrWhiteSpace(sourceName) || sourceName == "Action not configured"
            ? $"{outcomeName} branch"
            : $"{sourceName} {outcomeName}";
        var existingNames = (SelectedSequenceDefinition?.Branches ?? new List<ProjectSequenceBranchDto>())
            .Select(static branch => branch.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = displayName;
        for (var suffix = 2; existingNames.Contains(displayName); suffix++)
        {
            displayName = $"{baseName} {suffix}";
        }
        var color = string.Equals(outcome, "success", StringComparison.OrdinalIgnoreCase) ? "#16A34A" : "#DC2626";
        return CreateSequenceBranch(Guid.NewGuid().ToString("N"), displayName, color);
    }

    public IReadOnlyList<TimerOwnerType> SequenceLifetimeOwnerTypeOptions { get; } = Enum.GetValues<TimerOwnerType>();
    public IReadOnlyList<SequenceStartConflictBehavior> SequenceStartConflictBehaviorOptions { get; } = Enum.GetValues<SequenceStartConflictBehavior>();

    public ProjectSequenceDefinitionDto? SelectedSequenceDefinition
    {
        get => _selectedSequenceDefinition;
        set
        {
            if (ReferenceEquals(_selectedSequenceDefinition, value)) return;
            _selectedSequenceDefinition = value;
            if (value is not null)
            {
                EnsureSequenceBranchDefinitions(value);
            }
            OnPropertyChanged();
            RefreshSequenceSteps();
            _addActionSequenceStepAtOffsetCommand?.RaiseCanExecuteChanged();
            _addSoundSequenceStepAtOffsetCommand?.RaiseCanExecuteChanged();
            _editSequenceActionCommand?.RaiseCanExecuteChanged();
            _validateSequenceCommand?.RaiseCanExecuteChanged();
        }
    }

    public ProjectSequenceStepDto? SelectedSequenceStep
    {
        get => _selectedSequenceStep;
        set
        {
            if (ReferenceEquals(_selectedSequenceStep, value)) return;
            _selectedSequenceStep = value;
            _sequenceContinuationEditMessage = string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSelectedSequenceSoundStep));
            OnPropertyChanged(nameof(SelectedSequenceSoundEffectId));
            OnPropertyChanged(nameof(IsSelectedSequenceActionStep));
            OnPropertyChanged(nameof(IsSelectedSequenceInlineAction));
            OnPropertyChanged(nameof(SelectedSequenceActionDisplayName));
            OnPropertyChanged(nameof(SelectedSequenceActionSourceDescription));
            OnPropertyChanged(nameof(SelectedSequenceActionId));
            OnPropertyChanged(nameof(SequenceActionPickerButtonText));
            OnPropertyChanged(nameof(IsSelectedSequenceActionRouteMode));
            OnPropertyChanged(nameof(IsSelectedSequenceSoundMergeMode));
            OnPropertyChanged(nameof(SelectedSequenceContinuationDescription));
            OnPropertyChanged(nameof(SelectedSequenceContinuationMode));
            OnPropertyChanged(nameof(SelectedSequenceContinuationModeChoices));
            OnPropertyChanged(nameof(SelectedSequenceBranchId));
            OnPropertyChanged(nameof(SelectedSequenceSuccessTargetStepId));
            OnPropertyChanged(nameof(SelectedSequenceFailureTargetStepId));
            OnPropertyChanged(nameof(SelectedSequenceMergeTargetStepId));
            _editSequenceActionCommand?.RaiseCanExecuteChanged();
            _deleteSequenceStepCommand?.RaiseCanExecuteChanged();
            RefreshSequenceStepTargetChoices();
        }
    }

    private RelayCommandOfT<int> _addActionSequenceStepAtOffsetCommand = null!;
    private RelayCommand _editSequenceActionCommand = null!;
    private RelayCommand _deleteSequenceStepCommand = null!;
    private RelayCommand _validateSequenceCommand = null!;
    private RelayCommandOfT<int> _addSoundSequenceStepAtOffsetCommand = null!;
    private RelayCommandOfT<ProjectSequenceStepDto> _selectSequenceStepCommand = null!;

    public ICommand AddActionSequenceStepAtOffsetCommand => _addActionSequenceStepAtOffsetCommand;
    public ICommand EditSequenceActionCommand => _editSequenceActionCommand;
    public ICommand DeleteSequenceStepCommand => _deleteSequenceStepCommand;
    public ICommand ValidateSequenceCommand => _validateSequenceCommand;
    public ICommand AddSoundSequenceStepAtOffsetCommand => _addSoundSequenceStepAtOffsetCommand;
    public ICommand SelectSequenceStepCommand => _selectSequenceStepCommand;

    private void InitializeSequenceDesignerCommands()
    {
        _addActionSequenceStepAtOffsetCommand = new RelayCommandOfT<int>(AddActionSequenceStep, offset => SelectedSequenceDefinition is not null && offset >= 0 && offset % 50 == 0);
        _editSequenceActionCommand = new RelayCommand(EditSelectedSequenceAction, () => SelectedSequenceStep?.StepType == SequenceStepType.Action && SelectedSequenceStep.InlineAction is not null);
        _deleteSequenceStepCommand = new RelayCommand(DeleteSelectedSequenceStep, () => SelectedSequenceStep is not null && SelectedSequenceDefinition?.Steps?.Contains(SelectedSequenceStep) == true);
        _validateSequenceCommand = new RelayCommand(ValidateSelectedSequence, () => SelectedSequenceDefinition is not null);
        _addSoundSequenceStepAtOffsetCommand = new RelayCommandOfT<int>(AddSoundSequenceStep, offset => SelectedSequenceDefinition is not null && offset >= 0 && offset % 50 == 0);
        _selectSequenceStepCommand = new RelayCommandOfT<ProjectSequenceStepDto>(step => SelectedSequenceStep = step, step => step is not null);
    }

    private void RefreshSequenceDesignerScope()
    {
        var selectedNode = SelectedNode;
        if (selectedNode is null)
        {
            SelectedSequenceScope = null;
            return;
        }

        var ancestry = GetAncestry(selectedNode);
        var scope = selectedNode is SequenceDefinitionNodeViewModel sequenceNode
            ? sequenceNode.Owner
            : ancestry.Select(ResolveSequenceScopeOwner).FirstOrDefault(static candidate => candidate is not null);
        SelectedSequenceScope = scope;
        if (selectedNode is SequenceDefinitionNodeViewModel selectedSequenceNode)
        {
            SelectedSequenceDefinition = selectedSequenceNode.Definition;
            SelectedWorkspaceTabIndex = 2;
        }
        else if (selectedNode is SequencesNodeViewModel)
        {
            SelectedSequenceDefinition = null;
            SelectedWorkspaceTabIndex = 2;
        }
    }

    private static ScopeNodeBase? ResolveSequenceScopeOwner(HierarchyNodeViewModel node) => node switch
    {
        PhaseNodeViewModel phase => phase.PhaseNode,
        GameObjectNodeViewModel gameObject when !gameObject.GameObject.IsRoomInstance => gameObject.GameObject,
        GlobalObjectNodeViewModel globalObject => globalObject.GameObject,
        TemplateGameObjectNodeViewModel templateObject => templateObject.GameObject,
        RoomNodeViewModel room => room.Room,
        TemplateRoomNodeViewModel templateRoom => templateRoom.Room,
        AreaNodeViewModel area => area.Area,
        CountryNodeViewModel country => country.Country,
        PlanetNodeViewModel planet => planet.Planet,
        ProjectRootNodeViewModel root => root.Project,
        _ => null
    };

    private static IEnumerable<HierarchyNodeViewModel> EnumerateHierarchyNodes(HierarchyNodeViewModel root)
    {
        var pending = new Stack<HierarchyNodeViewModel>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;
            for (var index = current.Children.Count - 1; index >= 0; index--)
            {
                pending.Push(current.Children[index]);
            }
        }
    }

    private void RefreshSequenceDefinitions()
    {
        var definitions = _selectedSequenceScope?.SequenceDefinitions ?? new List<ProjectSequenceDefinitionDto>();
        _sequenceDefinitions.Clear();
        foreach (var definition in definitions.OrderBy(static definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            EnsureSequenceBranchDefinitions(definition);
            _sequenceDefinitions.Add(definition);
        }

        if (SelectedSequenceDefinition is null || !definitions.Contains(SelectedSequenceDefinition))
        {
            SelectedSequenceDefinition = _sequenceDefinitions.FirstOrDefault();
        }
        else
        {
            RefreshSequenceSteps();
        }
    }

    private void RefreshSequenceSteps()
    {
        var elapsed = Stopwatch.StartNew();
        _sequenceSteps.Clear();
        foreach (var step in SelectedSequenceDefinition?.Steps?.OrderBy(static step => step.OffsetMs).ThenBy(static step => step.Order)
                     ?? Enumerable.Empty<ProjectSequenceStepDto>())
        {
            _sequenceSteps.Add(step);
        }

        if (SelectedSequenceStep is null || !_sequenceSteps.Contains(SelectedSequenceStep))
        {
            SelectedSequenceStep = _sequenceSteps.FirstOrDefault();
        }
        RefreshSequenceStepTargetChoices();
        OnPropertyChanged(nameof(SequenceSteps));
        OnPropertyChanged(nameof(SequenceBranchDefinitions));
        OnPropertyChanged(nameof(SequenceBranchChoices));
        OnPropertyChanged(nameof(HasSelectedSequenceSteps));
        OnPropertyChanged(nameof(SequenceValidationSummary));
        RefreshSequenceTimeline();
        _lastSequenceRefreshStepsMs = elapsed.Elapsed.TotalMilliseconds;
    }

    private void RefreshSequenceStepTargetChoices()
    {
        _sequenceStepTargetChoices.Clear();
        _sequenceStepTargetChoices.Add(new SequenceStepTargetChoice
        {
            StepId = null,
                DisplayName = "End this outcome"
        });
        var steps = SelectedSequenceDefinition?.Steps
            .OrderBy(static step => step.OffsetMs)
            .ThenBy(static step => step.Order)
            .ToList() ?? new List<ProjectSequenceStepDto>();
        var selected = SelectedSequenceStep;
        var isBranch = SelectedSequenceContinuationMode == ContinuationMode.Branch;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var isMerge = SelectedSequenceContinuationMode == ContinuationMode.Merge;
            if (selected is not null
                && (step.OffsetMs < selected.OffsetMs
                    || step.OffsetMs == selected.OffsetMs && step.Order <= selected.Order && !isMerge && !isBranch
                    || isMerge
                        && string.Equals(step.BranchId, selected.BranchId, StringComparison.Ordinal)))
            {
                continue;
            }

            _sequenceStepTargetChoices.Add(new SequenceStepTargetChoice
            {
                StepId = step.StepId,
                DisplayName = FormatTargetChoice(step, index, isPreservedCurrentTarget: false)
            });
        }

        // Keep already-authored links selectable even when they violate the normal
        // forward-time filter. The flow canvas draws the stored IDs, so hiding these
        // choices made arrows appear while the corresponding property looked empty.
        var includedTargetIds = _sequenceStepTargetChoices
            .Where(static choice => choice.StepId.HasValue)
            .Select(static choice => choice.StepId!.Value)
            .ToHashSet();
        var existingTargetIds = new[]
            {
                selected?.OnSuccessStepId,
                selected?.OnFailureStepId,
                selected?.MergeTargetStepId
            }
            .Where(static targetId => targetId.HasValue)
            .Select(static targetId => targetId!.Value)
            .Distinct();
        foreach (var targetId in existingTargetIds)
        {
            if (!includedTargetIds.Add(targetId))
            {
                continue;
            }

            var targetIndex = steps.FindIndex(step => step.StepId == targetId);
            if (targetIndex < 0)
            {
                _sequenceStepTargetChoices.Add(new SequenceStepTargetChoice
                {
                    StepId = targetId,
                    IsPreservedCurrentTarget = true,
                    DisplayName = $"Missing step · {targetId:D} · current link outside eligible range"
                });
                continue;
            }

            _sequenceStepTargetChoices.Add(new SequenceStepTargetChoice
            {
                StepId = targetId,
                IsPreservedCurrentTarget = true,
                DisplayName = FormatTargetChoice(steps[targetIndex], targetIndex, isPreservedCurrentTarget: true)
            });
        }

        string FormatTargetChoice(ProjectSequenceStepDto step, int index, bool isPreservedCurrentTarget)
        {
            var currentLinkNote = isPreservedCurrentTarget ? " · current link outside eligible range" : string.Empty;
            return $"Step {index + 1} · {GetSequenceActionDisplayName(step)} · {step.OffsetMs} ms · {GetSequenceBranchDisplayName(step.BranchId)}{currentLinkNote}";
        }

        OnPropertyChanged(nameof(SequenceStepTargetChoices));
        OnPropertyChanged(nameof(SequenceMergeTargetChoices));
        OnPropertyChanged(nameof(SelectedSequenceSuccessTargetChoice));
        OnPropertyChanged(nameof(SelectedSequenceFailureTargetChoice));
        OnPropertyChanged(nameof(SelectedSequenceMergeTargetChoice));
    }

    private IReadOnlyList<SequenceDefinitionIssue> GetSelectedSequenceValidationIssues()
    {
        return SelectedSequenceDefinition is not null
               && _sequenceValidationResults.TryGetValue(SelectedSequenceDefinition.SequenceId, out var issues)
            ? issues
            : Array.Empty<SequenceDefinitionIssue>();
    }

    private void ValidateSelectedSequence()
    {
        if (SelectedSequenceDefinition is null)
        {
            return;
        }

        var issues = SequenceDefinitionIntegrityEvaluator.Evaluate(SelectedSequenceDefinition);
        _sequenceValidationResults[SelectedSequenceDefinition.SequenceId] = issues;
        OnPropertyChanged(nameof(SequenceValidationSummary));
        RefreshSequenceTimeline();
        ExportStatus = issues.Count == 0
            ? $"Sequence '{SelectedSequenceDefinition.DisplayName}' passed validation."
            : $"Sequence '{SelectedSequenceDefinition.DisplayName}' has {issues.Count} validation issue(s).";
    }

    private void RefreshSequenceTimeline()
    {
        var elapsed = Stopwatch.StartNew();
        _sequenceTimeSlices.Clear();
        _sequenceFlowTimeSlices.Clear();
        var steps = _sequenceSteps.ToList();
        var soundEffectNamesById = new Dictionary<Guid, string>();
        foreach (var choice in _sequenceSoundEffectChoices)
        {
            var name = !string.IsNullOrWhiteSpace(choice.DisplayName)
                ? choice.DisplayName.Trim()
                : choice.SoundEffectKey.Trim();
            if (choice.SoundEffectId != Guid.Empty && !string.IsNullOrWhiteSpace(name))
            {
                soundEffectNamesById.TryAdd(choice.SoundEffectId, name);
            }
        }

        // Existing steps can retain a sound reference even if the library entry is
        // no longer in the sequence owner's currently selectable scopes. Resolve
        // those labels from the project so the card still shows the authored name.
        var unresolvedSoundEffectIds = steps
            .Where(static step => step.StepType == SequenceStepType.Sound)
            .Select(static step => step.SoundEffectId)
            .Where(static soundEffectId => soundEffectId.HasValue && soundEffectId.Value != Guid.Empty)
            .Select(static soundEffectId => soundEffectId!.Value)
            .Where(soundEffectId => !soundEffectNamesById.ContainsKey(soundEffectId))
            .ToHashSet();
        foreach (var entry in unresolvedSoundEffectIds.Count == 0
                     ? Enumerable.Empty<SoundEffectLibraryEntry>()
                     : EnumerateProjectSoundEffectLibraryEntries())
        {
            if (!unresolvedSoundEffectIds.Contains(entry.SoundEffectId))
            {
                continue;
            }

            var name = !string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DisplayName.Trim()
                : (entry.SoundEffectKey ?? string.Empty).Trim();
            if (entry.SoundEffectId != Guid.Empty && !string.IsNullOrWhiteSpace(name))
            {
                soundEffectNamesById.TryAdd(entry.SoundEffectId, name);
                unresolvedSoundEffectIds.Remove(entry.SoundEffectId);
                if (unresolvedSoundEffectIds.Count == 0)
                {
                    break;
                }
            }
        }

        var stepNumbers = steps.Select((step, index) => (step.StepId, Label: $"Step {index + 1}"))
            .Where(static pair => pair.StepId != Guid.Empty)
            .ToDictionary(static pair => pair.StepId, static pair => pair.Label);
        var sameTimeDepths = new Dictionary<ProjectSequenceStepDto, int>();
        var calculating = new HashSet<ProjectSequenceStepDto>();

        bool RoutesTo(ProjectSequenceStepDto source, ProjectSequenceStepDto target)
        {
            var mode = source.ContinuationMode ?? ContinuationMode.Continue;
            if (mode == ContinuationMode.Continue)
            {
                var nextOnBranch = steps
                    .Where(candidate => string.Equals(candidate.BranchId, source.BranchId, StringComparison.Ordinal)
                        && (candidate.OffsetMs > source.OffsetMs
                            || candidate.OffsetMs == source.OffsetMs && candidate.Order > source.Order))
                    .OrderBy(static candidate => candidate.OffsetMs)
                    .ThenBy(static candidate => candidate.Order)
                    .FirstOrDefault();
                return ReferenceEquals(nextOnBranch, target);
            }

            if (source.StepType == SequenceStepType.Action
                && mode is ContinuationMode.Branch or ContinuationMode.Merge)
            {
                return source.OnSuccessStepId == target.StepId || source.OnFailureStepId == target.StepId;
            }

            return source.StepType == SequenceStepType.Sound
                && mode == ContinuationMode.Merge
                && source.MergeTargetStepId == target.StepId;
        }

        int ResolveDepth(ProjectSequenceStepDto step)
        {
            if (sameTimeDepths.TryGetValue(step, out var knownDepth)) return knownDepth;
            if (!calculating.Add(step)) return 0;

            var parents = steps.Where(parent => parent.OffsetMs == step.OffsetMs && RoutesTo(parent, step));
            var depth = parents.Any() ? parents.Max(parent => ResolveDepth(parent) + 1) : 0;
            calculating.Remove(step);
            sameTimeDepths[step] = depth;
            return depth;
        }

        var stepsByOffset = steps.GroupBy(static step => step.OffsetMs)
            .ToDictionary(static group => group.Key, static group => group.OrderBy(step => step.Order).ThenBy(step => step.StepId).ToList());
        var actualOffsets = stepsByOffset.Keys.ToHashSet();
        var branchIds = (SelectedSequenceDefinition?.Branches ?? new List<ProjectSequenceBranchDto>())
            .Select(static branch => branch.BranchId)
            .Concat(new[] { MainSequenceBranchId })
            .Concat(steps.Select(static step => step.BranchId).Where(static id => !string.IsNullOrWhiteSpace(id)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id == MainSequenceBranchId ? 0 : 1)
            .ToList();
        var maxCardsPerBranchColumn = branchIds.ToDictionary(static branchId => branchId, static _ => 1, StringComparer.Ordinal);
        foreach (var groupSteps in stepsByOffset.Values)
        {
            var depths = groupSteps.ToDictionary(static step => step, ResolveDepth);
            foreach (var branchGroup in groupSteps.GroupBy(step => (BranchId: step.BranchId ?? MainSequenceBranchId, Depth: depths[step])))
            {
                maxCardsPerBranchColumn[branchGroup.Key.BranchId] = Math.Max(
                    maxCardsPerBranchColumn.GetValueOrDefault(branchGroup.Key.BranchId, 1),
                    branchGroup.Count());
            }
        }
        var branchLaneHeights = maxCardsPerBranchColumn.ToDictionary(
            static pair => pair.Key,
            static pair => Math.Max(184d, pair.Value * 158d + 26d),
            StringComparer.Ordinal);
        var soundTimings = steps
            .Where(static step => step.StepType == SequenceStepType.Sound && step.SoundEffectId.HasValue)
            .Select(step => (Step: step, Choice: _sequenceSoundEffectChoices.FirstOrDefault(choice => choice.SoundEffectId == step.SoundEffectId)))
            .Where(static item => item.Choice is not null && item.Choice.DurationMs is > 0)
            .ToDictionary(
                static item => item.Step,
                item =>
                {
                    var choice = item.Choice!;
                    var duration = choice.DurationMs!.Value;
                    if (choice.MaxPlayDurationMs is > 0)
                    {
                        duration = Math.Min(duration, choice.MaxPlayDurationMs.Value);
                    }

                    var start = (int)Math.Clamp((long)item.Step.OffsetMs + Math.Max(0, choice.StartDelayMs ?? 0), int.MinValue, int.MaxValue);
                    var end = (int)Math.Clamp((long)start + duration, int.MinValue, int.MaxValue);
                    return new SequenceSoundPlaybackTiming(choice, start, end, duration);
                });
        var timeOffsets = new SortedSet<int>();
        var maxOffset = Math.Max(0, Math.Max(
            steps.Select(static step => step.OffsetMs).DefaultIfEmpty(0).Max(),
            soundTimings.Values.Select(static timing => timing.EndOffsetMs).DefaultIfEmpty(0).Max()));
        var regularTimelineEnd = (int)Math.Min((long)maxOffset + 500L, 20_000L);
        for (var offset = 0; offset <= regularTimelineEnd; offset += 250)
        {
            timeOffsets.Add(offset);
        }
        foreach (var actualOffset in actualOffsets)
        {
            timeOffsets.Add(actualOffset);
        }

        void AddRangeTimeSlice(int rangeStart, int rangeEnd)
        {
            var normalizedStart = (int)Math.Max(0L, ((long)rangeStart + 49L) / 50L * 50L);
            var normalizedEnd = (int)((long)rangeEnd / 50L * 50L);
            if (normalizedEnd < normalizedStart)
            {
                return;
            }

            var rangeSlice = new SequenceTimelineTimeSliceViewModel
            {
                OffsetMs = normalizedStart,
                RangeStartOffsetMs = normalizedStart,
                RangeEndOffsetMs = normalizedEnd,
                RulerLabel = $"{normalizedStart}–{normalizedEnd} ms",
                ShowRulerLabel = true,
                HasSteps = false,
                Width = Math.Min(
                    230d,
                    Math.Max(44d, ((normalizedEnd - normalizedStart) / 50d + 1d) * 44d))
            };
            foreach (var branchId in branchIds)
            {
                var lane = CreateSequenceBranchLane(branchId, branchLaneHeights.GetValueOrDefault(branchId, 184d));
                lane.Columns.Add(new SequenceTimelineColumnViewModel());
                rangeSlice.Lanes.Add(lane);
            }
            _sequenceTimeSlices.Add(rangeSlice);
        }

        int? previousOffset = null;
        foreach (var offset in timeOffsets)
        {
            if (previousOffset.HasValue && offset - previousOffset.Value > 50)
            {
                AddRangeTimeSlice(previousOffset.Value + 50, offset - 50);
            }

            var groupSteps = stepsByOffset.TryGetValue(offset, out var offsetSteps)
                ? offsetSteps
                : new List<ProjectSequenceStepDto>();
            var depths = groupSteps.ToDictionary(static step => step, ResolveDepth);
            var maxBranchColumnCount = groupSteps
                .GroupBy(step => step.BranchId ?? MainSequenceBranchId, StringComparer.Ordinal)
                .Select(branchGroup => branchGroup.Select(step => depths[step]).Distinct().Count())
                .DefaultIfEmpty(1)
                .Max();
            var slice = new SequenceTimelineTimeSliceViewModel
            {
                OffsetMs = offset,
                RangeStartOffsetMs = offset,
                RangeEndOffsetMs = offset,
                RulerLabel = $"{offset} ms",
                ShowRulerLabel = offset % 250 == 0 || actualOffsets.Contains(offset),
                HasSteps = groupSteps.Count > 0,
                Width = groupSteps.Count == 0 ? 44d : Math.Max(196d, maxBranchColumnCount * 188d + 16d)
            };

            foreach (var branchId in branchIds)
            {
                var lane = CreateSequenceBranchLane(branchId, branchLaneHeights.GetValueOrDefault(branchId, 184d));
                var branchSteps = groupSteps.Where(step => string.Equals(step.BranchId ?? MainSequenceBranchId, branchId, StringComparison.Ordinal)).ToList();
                var depthGroups = branchSteps
                    .GroupBy(step => depths[step])
                    .OrderBy(static group => group.Key)
                    .ToList();
                if (depthGroups.Count == 0)
                {
                    lane.Columns.Add(new SequenceTimelineColumnViewModel());
                }

                foreach (var depthGroup in depthGroups)
                {
                    var column = new SequenceTimelineColumnViewModel();
                    foreach (var step in depthGroup.OrderBy(static step => step.Order).ThenBy(static step => step.StepId))
                    {
                        string Target(Guid? id) => id.HasValue && stepNumbers.TryGetValue(id.Value, out var label) ? label : "End";
                        var stepValidationIssues = GetSelectedSequenceValidationIssues()
                            .Where(issue => issue.StepId == step.StepId)
                            .Select(static issue => issue.Description)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var playback = soundTimings.GetValueOrDefault(step);
                        var routeMode = step.ContinuationMode ?? ContinuationMode.Continue;
                        var soundName = step.SoundEffectId is Guid selectedSoundId
                            ? soundEffectNamesById.GetValueOrDefault(selectedSoundId) ?? selectedSoundId.ToString("D")
                            : "Sound effect";
                        var stepLabel = stepNumbers.TryGetValue(step.StepId, out var label) ? label : "Step";
                        var repeatNote = playback is not null && !string.Equals(playback.Choice.RepeatMode, "None", StringComparison.OrdinalIgnoreCase)
                            ? $" Repeats may continue after this single-play marker ({playback.Choice.RepeatMode})."
                            : string.Empty;
                        column.Cards.Add(new SequenceTimelineCardViewModel
                        {
                            Step = step,
                            StepLabel = $"{stepLabel} - {step.OffsetMs:N0} ms",
                            BranchDisplayName = GetSequenceBranchDisplayName(step.BranchId ?? MainSequenceBranchId),
                            ActionDisplayName = GetSequenceActionDisplayName(step),
                            ActionSourceLabel = step.InlineAction is null ? "Shared" : "Inline",
                            SoundEffectLabel = step.SoundEffectId is not Guid soundEffectId || soundEffectId == Guid.Empty
                                ? "No sound effect selected"
                                : soundEffectNamesById.GetValueOrDefault(soundEffectId) ?? soundEffectId.ToString("D"),
                            SoundDurationLabel = playback is null ? string.Empty : $"One play · {FormatSoundDuration(playback.DurationMs)}",
                            PlaybackStartOffsetMs = playback?.StartOffsetMs,
                            PlaybackEndOffsetMs = playback?.EndOffsetMs,
                            PlaybackEndMarkerToolTip = playback is null
                                ? string.Empty
                                : $"{soundName} ends one play at {playback.EndOffsetMs} ms.{repeatNote}",
                            SuccessTargetLabel = step.StepType == SequenceStepType.Action
                                && routeMode is ContinuationMode.Branch or ContinuationMode.Merge
                                ? $"{(routeMode == ContinuationMode.Merge ? "Merge success" : "Success")} → {Target(step.OnSuccessStepId)}"
                                : string.Empty,
                            FailureTargetLabel = step.StepType == SequenceStepType.Action
                                && routeMode is ContinuationMode.Branch or ContinuationMode.Merge
                                ? $"{(routeMode == ContinuationMode.Merge ? "Merge failure" : "Failure")} → {Target(step.OnFailureStepId)}"
                                : string.Empty,
                            MergeTargetLabel = step.StepType == SequenceStepType.Sound && routeMode == ContinuationMode.Merge
                                ? $"Merge → {Target(step.MergeTargetStepId)}"
                                : string.Empty,
                            HasValidationIssues = stepValidationIssues.Count > 0,
                            ValidationMessage = string.Join(" ", stepValidationIssues)
                        });
                    }
                    lane.Columns.Add(column);
                }

                slice.Lanes.Add(lane);
            }

            _sequenceTimeSlices.Add(slice);
            previousOffset = offset;
        }

        if (previousOffset.HasValue)
        {
            var rangeStart = (long)previousOffset.Value + 50L;
            var rangeEnd = Math.Min((long)int.MaxValue / 50L * 50L, (long)maxOffset + 60_000L);
            if (rangeStart <= int.MaxValue && rangeEnd >= rangeStart)
            {
                AddRangeTimeSlice((int)rangeStart, (int)rangeEnd);
            }
        }

        BuildSparseSequenceFlowSlices();
        OnPropertyChanged(nameof(SequenceTimeSlices));
        OnPropertyChanged(nameof(SequenceFlowTimeSlices));
        _lastSequenceTimelineModelMs = elapsed.Elapsed.TotalMilliseconds;
    }

    private IEnumerable<SoundEffectLibraryEntry> EnumerateProjectSoundEffectLibraryEntries()
    {
        foreach (var entry in _project.GlobalScope.SoundEffectLibraryEntries)
        {
            yield return entry;
        }

        var pendingScopes = new Stack<IScopedAwareNode>(_project.ChildScopes.Reverse());
        while (pendingScopes.Count > 0)
        {
            var scope = pendingScopes.Pop();
            var entries = scope switch
            {
                GameObject gameObject => gameObject.SoundEffectLibraryEntries,
                Room room => room.SoundEffectLibraryEntries,
                Area area => area.SoundEffectLibraryEntries,
                Country country => country.SoundEffectLibraryEntries,
                Planet planet => planet.SoundEffectLibraryEntries,
                PhaseNode phase => phase.SoundEffectLibraryEntries,
                _ => null
            };

            if (entries is not null)
            {
                foreach (var entry in entries)
                {
                    yield return entry;
                }
            }

            foreach (var childScope in scope.ChildScopes.Reverse())
            {
                pendingScopes.Push(childScope);
            }
        }
    }

    private void BuildSparseSequenceFlowSlices()
    {
        var index = 0;
        while (index < _sequenceTimeSlices.Count)
        {
            var current = _sequenceTimeSlices[index];
            if (current.HasSteps)
            {
                _sequenceFlowTimeSlices.Add(current);
                index++;
                continue;
            }

            var gapStartIndex = index;
            var gapWidth = 0d;
            var gapStartOffset = current.IsRange ? current.RangeStartOffsetMs : current.OffsetMs;
            var gapEndOffset = current.IsRange ? current.RangeEndOffsetMs : current.OffsetMs;
            var gapLanes = current.Lanes;
            while (index < _sequenceTimeSlices.Count)
            {
                var slice = _sequenceTimeSlices[index];
                if (slice.HasSteps)
                {
                    break;
                }

                // Keep the compressed long tail as its own segment so its time scale
                // does not get blended with the regularly spaced ruler intervals.
                if (index > gapStartIndex && slice.IsRange
                    && slice.RangeEndOffsetMs - slice.RangeStartOffsetMs > 1_000)
                {
                    break;
                }

                gapWidth += slice.Width;
                gapEndOffset = slice.IsRange ? slice.RangeEndOffsetMs : slice.OffsetMs;
                index++;

                if (slice.IsRange && slice.RangeEndOffsetMs - slice.RangeStartOffsetMs > 1_000)
                {
                    break;
                }
            }

            var gap = new SequenceTimelineTimeSliceViewModel
            {
                OffsetMs = gapStartOffset,
                RangeStartOffsetMs = gapStartOffset,
                RangeEndOffsetMs = gapEndOffset,
                RulerLabel = gapEndOffset > gapStartOffset ? $"{gapStartOffset}–{gapEndOffset} ms" : $"{gapStartOffset} ms",
                ShowRulerLabel = false,
                HasSteps = false,
                IsDisplayGap = true,
                Width = gapWidth
            };
            foreach (var sourceLane in gapLanes)
            {
                var lane = new SequenceTimelineBranchLaneViewModel
                {
                    BranchId = sourceLane.BranchId,
                    DisplayName = sourceLane.DisplayName,
                    Height = sourceLane.Height,
                    LaneBackground = sourceLane.LaneBackground,
                    LaneBorder = sourceLane.LaneBorder
                };
                lane.Columns.Add(new SequenceTimelineColumnViewModel());
                gap.Lanes.Add(lane);
            }

            _sequenceFlowTimeSlices.Add(gap);
        }
    }

    private static string FormatSoundDuration(int durationMs) => durationMs >= 1000
        ? $"{durationMs / 1000d:0.##} s"
        : $"{durationMs} ms";

    private void DeleteSelectedSequenceStep()
    {
        if (SelectedSequenceDefinition?.Steps is not { } steps || SelectedSequenceStep is not { } removedStep)
        {
            return;
        }

        var removedIndex = steps.IndexOf(removedStep);
        var survivingRoutes = steps
            .Where(step => !ReferenceEquals(step, removedStep))
            .Select(step => (Step: step, Success: step.OnSuccessStepId, Failure: step.OnFailureStepId, Merge: step.MergeTargetStepId))
            .ToList();
        steps.Remove(removedStep);
        foreach (var timeGroup in steps.GroupBy(static step => step.OffsetMs))
        {
            var ordered = timeGroup.OrderBy(static step => step.Order).ThenBy(static step => step.StepId).ToList();
            for (var index = 0; index < ordered.Count; index++) ordered[index].Order = index;
        }

        RefreshSequenceSteps();
        if (_sequenceSteps.Count > 0)
        {
            SelectedSequenceStep = _sequenceSteps[Math.Clamp(removedIndex, 0, _sequenceSteps.Count - 1)];
        }

        // Refreshing selection and target choices must not alter routes on other steps.
        // Only links that pointed at the deleted step are intentionally removed.
        foreach (var route in survivingRoutes)
        {
            route.Step.OnSuccessStepId = route.Success == removedStep.StepId ? null : route.Success;
            route.Step.OnFailureStepId = route.Failure == removedStep.StepId ? null : route.Failure;
            route.Step.MergeTargetStepId = route.Merge == removedStep.StepId ? null : route.Merge;
        }
        OnPropertyChanged(nameof(SelectedSequenceSuccessTargetStepId));
        OnPropertyChanged(nameof(SelectedSequenceFailureTargetStepId));
        OnPropertyChanged(nameof(SelectedSequenceMergeTargetStepId));
        OnPropertyChanged(nameof(SelectedSequenceSuccessTargetChoice));
        OnPropertyChanged(nameof(SelectedSequenceFailureTargetChoice));
        OnPropertyChanged(nameof(SelectedSequenceMergeTargetChoice));
        RefreshSequenceTimeline();
        NotifySequenceProjectEdited();
    }

    private void AddSequenceDefinition()
    {
        if (SelectedSequenceScope is null) return;
        var definition = new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = "New Sequence",
            LifetimeOwnerType = ResolveDefaultSequenceLifetimeOwnerType(),
            StartConflictBehavior = SequenceStartConflictBehavior.Ignore,
            Steps = new List<ProjectSequenceStepDto>(),
            Branches = new List<ProjectSequenceBranchDto>
            {
                CreateSequenceBranch(MainSequenceBranchId, "Main branch", DefaultBranchDisplayColor(MainSequenceBranchId))
            }
        };
        SelectedSequenceScope.SequenceDefinitions.Add(definition);
        _sequenceDefinitions.Add(definition);
        SelectedSequenceDefinition = definition;
        var ownerNode = GetAncestry(SelectedNode!).FirstOrDefault(node => ReferenceEquals(ResolveSequenceScopeOwner(node), SelectedSequenceScope));
        if (ownerNode is not null)
        {
            var sequencesNode = ownerNode.Children.OfType<SequencesNodeViewModel>().FirstOrDefault();
            if (sequencesNode is null)
            {
                sequencesNode = new SequencesNodeViewModel(SelectedSequenceScope, ownerNode);
                ownerNode.Children.Add(sequencesNode);
            }
            var sequenceNode = new SequenceDefinitionNodeViewModel(SelectedSequenceScope, definition, sequencesNode);
            sequencesNode.Children.Add(sequenceNode);
            SelectedNode = sequenceNode;
        }
        NotifySequenceProjectEdited();
    }

    private TimerOwnerType ResolveDefaultSequenceLifetimeOwnerType()
    {
        IEnumerable<HierarchyNodeViewModel> ancestry = SelectedNode is null
            ? Array.Empty<HierarchyNodeViewModel>()
            : GetAncestry(SelectedNode);
        if (ancestry.Any(static node => node is RoomNodeViewModel or TemplateRoomNodeViewModel)) return TimerOwnerType.Room;
        if (ancestry.Any(static node => node is AreaNodeViewModel)) return TimerOwnerType.Area;
        if (ancestry.Any(static node => node is CountryNodeViewModel)) return TimerOwnerType.Country;
        if (ancestry.Any(static node => node is PlanetNodeViewModel)) return TimerOwnerType.Planet;
        if (ancestry.Any(static node => node is GlobalObjectNodeViewModel)) return TimerOwnerType.Player;
        return TimerOwnerType.Session;
    }

    private void RemoveSequenceDefinition()
    {
        if (SelectedSequenceScope is null || SelectedSequenceDefinition is null) return;
        var removed = SelectedSequenceDefinition;
        SelectedSequenceScope.SequenceDefinitions.Remove(removed);
        _sequenceDefinitions.Remove(removed);
        var hierarchyNode = SelectedNode is SequenceDefinitionNodeViewModel selectedSequenceNode
            && ReferenceEquals(selectedSequenceNode.Definition, removed)
            ? selectedSequenceNode
            : HierarchyRoots.SelectMany(EnumerateHierarchyNodes)
                .OfType<SequenceDefinitionNodeViewModel>()
                .FirstOrDefault(node => ReferenceEquals(node.Definition, removed));
        if (hierarchyNode?.Parent is { } parent)
        {
            parent.Children.Remove(hierarchyNode);
            SelectedNode = parent;
        }
        SelectedSequenceDefinition = _sequenceDefinitions.FirstOrDefault();
        NotifySequenceProjectEdited();
    }

    private bool RemoveSequenceDefinitionFromNode(SequenceDefinitionNodeViewModel sequenceNode)
    {
        SelectedSequenceScope = sequenceNode.Owner;
        SelectedSequenceDefinition = sequenceNode.Definition;
        RemoveSequenceDefinition();
        return true;
    }

    private void AddActionSequenceStep(int offset)
    {
        AddInlineActionSequenceStepAtOffset(offset, SelectedSequenceStep?.BranchId ?? MainSequenceBranchId);
    }

    public bool AddInlineActionSequenceStepAtOffset(int offset, string branchId)
    {
        if (SelectedSequenceDefinition is null || string.IsNullOrWhiteSpace(branchId)
            || !CanAddSequenceStepAtOffset(offset, branchId))
        {
            return false;
        }

        var step = CreateNewActionSequenceStep(offset);
        step.BranchId = branchId;
        step.Order = NextSequenceOrderAtOffset(SelectedSequenceDefinition.Steps, offset);
        if (!EditSequenceStepAction(step)) return false;
        SelectedSequenceDefinition.Steps.Add(step);
        RefreshSequenceSteps();
        SelectedSequenceStep = step;
        NotifySequenceProjectEdited();
        return true;
    }

    public IReadOnlyList<(CommandAction Action, string ScopeName)> GetSequenceActionChoices()
    {
        return GetSequenceActionPickerChoices()
            .Select(static choice => (choice.Action, choice.ScopeName))
            .ToList();
    }

    public IReadOnlyList<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)> GetSequenceActionPickerChoices()
    {
        if (SelectedSequenceScope is null)
        {
            return Array.Empty<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)>();
        }

        var choices = new List<(CommandAction Action, string ScopeName, LinkedActionTargetGroup Group)>();
        var seenActionIds = new HashSet<Guid>();

        void AddScopeActions(IScopedAwareNode scope, LinkedActionTargetGroup group)
        {
            foreach (var action in GetAvailableActionsForScope(scope)
                         .Where(static action => action.Id != Guid.Empty)
                         .OrderBy(static action => action.Name, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(static action => action.ActionType))
            {
                if (seenActionIds.Add(action.Id))
                {
                    choices.Add((action, GetScopeDisplayPath(scope), group));
                }
            }
        }

        AddScopeActions(SelectedSequenceScope, LinkedActionTargetGroup.CurrentScope);
        for (var scope = SelectedSequenceScope.ParentScope; scope is not null; scope = scope.ParentScope)
        {
            AddScopeActions(scope, LinkedActionTargetGroup.Ancestors);
        }

        var pendingChildren = new Stack<IScopedAwareNode>(SelectedSequenceScope.ChildScopes.Reverse());
        while (pendingChildren.Count > 0)
        {
            var child = pendingChildren.Pop();
            AddScopeActions(child, LinkedActionTargetGroup.Children);
            foreach (var nestedChild in child.ChildScopes.Reverse())
            {
                pendingChildren.Push(nestedChild);
            }
        }

        return choices;
    }

    public bool AddExistingActionSequenceStepAtOffset(int offset, CommandAction action)
    {
        return AddExistingActionSequenceStepAtOffset(offset, SelectedSequenceStep?.BranchId ?? MainSequenceBranchId, action);
    }

    public bool AddExistingActionSequenceStepAtOffset(int offset, string branchId, CommandAction action)
    {
        if (SelectedSequenceDefinition?.Steps is not { } steps
            || action.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(branchId)
            || !CanAddSequenceStepAtOffset(offset, branchId))
        {
            return false;
        }

        var step = new ProjectSequenceStepDto
        {
            StepId = Guid.NewGuid(),
            StepType = SequenceStepType.Action,
            BranchId = branchId,
            ContinuationMode = ContinuationMode.Continue,
            OffsetMs = offset,
            Order = NextSequenceOrderAtOffset(steps, offset),
            ActionId = action.Id
        };
        steps.Add(step);
        RefreshSequenceSteps();
        SelectedSequenceStep = step;
        NotifySequenceProjectEdited();
        return true;
    }

    public bool AddForkOutcomeExistingActionAtOffset(int offset, bool success, CommandAction action)
    {
        if (SelectedSequenceDefinition?.Steps is not { } steps
            || SelectedSequenceStep is not { StepType: SequenceStepType.Action } source
            || (source.ContinuationMode ?? ContinuationMode.Continue) != ContinuationMode.Branch
            || offset < source.OffsetMs
            || action.Id == Guid.Empty)
        {
            return false;
        }

        if ((success ? source.OnSuccessStepId : source.OnFailureStepId).HasValue)
        {
            return false;
        }

        var order = NextSequenceOrderAtOffset(steps, offset);
        if (offset == source.OffsetMs && order <= source.Order)
        {
            return false;
        }

        var branch = CreateForkBranch(source, success ? "success" : "failure");
        var branchStep = new ProjectSequenceStepDto
        {
            StepId = Guid.NewGuid(),
            BranchId = branch.BranchId,
            ContinuationMode = ContinuationMode.Continue,
            StepType = SequenceStepType.Action,
            OffsetMs = offset,
            Order = order,
            ActionId = action.Id
        };
        steps.Add(branchStep);
        SelectedSequenceDefinition.Branches.Add(branch);
        OnPropertyChanged(nameof(SequenceBranchDefinitions));
        if (success)
        {
            source.OnSuccessStepId = branchStep.StepId;
            OnPropertyChanged(nameof(SelectedSequenceSuccessTargetStepId));
        }
        else
        {
            source.OnFailureStepId = branchStep.StepId;
            OnPropertyChanged(nameof(SelectedSequenceFailureTargetStepId));
        }

        RefreshSequenceSteps();
        SelectedSequenceStep = branchStep;
        NotifySequenceProjectEdited();
        return true;
    }

    private string GetSequenceActionDisplayName(ProjectSequenceStepDto step)
    {
        if (step.InlineAction is not null)
        {
            return step.InlineAction.Name;
        }

        if (step.ActionId is Guid actionId)
        {
            var match = ResolveSequenceAction(actionId);
            return match is null
                ? "Missing action"
                : match.Value.Action.Name;
        }

        return "Action not configured";
    }

    private (CommandAction Action, string ScopeName)? ResolveSequenceAction(Guid actionId)
    {
        return GetSequenceActionPickerChoices()
                   .Select(static choice => (choice.Action, choice.ScopeName))
                   .FirstOrDefault(choice => choice.Action.Id == actionId) is var choice
               && choice.Action is not null
            ? choice
            : null;
    }

    private static IEnumerable<CommandAction> GetAvailableActionsForScope(IScopedAwareNode scope)
    {
        return scope switch
        {
            ProjectModel project => project.GlobalScopeAvailableActions,
            PhaseNode phase => phase.AvailableActions,
            GameObject gameObject => gameObject.AvailableActions,
            Room room => room.AvailableActions,
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            _ => Array.Empty<CommandAction>()
        };
    }

    private static string GetScopeDisplayPath(IScopedAwareNode scope)
    {
        var names = new Stack<string>();
        for (IScopedAwareNode? current = scope; current is not null; current = current.ParentScope)
        {
            names.Push(string.IsNullOrWhiteSpace(current.ScopeName) ? current.ScopeKind.ToString() : current.ScopeName);
        }

        return string.Join(" / ", names);
    }

    private void EditSelectedSequenceAction()
    {
        if (SelectedSequenceStep is { StepType: SequenceStepType.Action, InlineAction: not null } step)
        {
            if (EditSequenceStepAction(step))
            {
                RefreshSequenceSteps();
                NotifySequenceProjectEdited();
            }
        }
    }

    private bool EditSequenceStepAction(ProjectSequenceStepDto step)
    {
        if (step.InlineAction is null || SelectedSequenceScope is null)
        {
            return false;
        }

        var ownerHierarchyNode = SelectedNode is null
            ? null
            : GetAncestry(SelectedNode).FirstOrDefault(node => ReferenceEquals(ResolveSequenceScopeOwner(node), SelectedSequenceScope));
        var target = ownerHierarchyNode is null ? null : ResolveScopedActionEditTarget(ownerHierarchyNode);
        var variableScope = target?.VariableScope ?? PropertyResolutionScope.Global;
        IScopedAwareNode? scopeNode = target?.ScopeNode ?? SelectedSequenceScope;
        var variableChoices = GetVariableChoicesForScopeNode(scopeNode, variableScope);
        var echoReferenceTokens = variableChoices
            .Select(static choice => choice.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Concat(RuntimeAnchorReferenceTokenCatalog.BuildIntrinsicAnchorTokens())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var availableActions = target?.Actions.ToList() ?? new List<CommandAction>();
        var action = JsonExportService.ToCommandActionModel(step.InlineAction);

        if (!_dialogWorkflowService.EditSingleScopedAction(
                action,
                variableScope,
                variableChoices,
                echoReferenceTokens,
                GetContainerTargetChoicesForScopeNode(scopeNode),
                GetMaterializeSourceObjectChoicesForScopeNode(scopeNode),
                GetProcedureChoicesForScopeNode(scopeNode),
                GetCompositeRecipeChoicesForScopeNode(scopeNode),
                GetSoundEffectChoicesForScopeNode(scopeNode),
                GetTimerKeySuggestionsForScopeNode(scopeNode),
                GetSequenceDefinitionChoices(),
                availableActions,
                scopeNode is GameObject ownerObject ? ownerObject.ObjectId : null))
        {
            return false;
        }

        step.InlineAction = JsonExportService.ToCommandActionDtoForSequence(action, availableActions);
        return true;
    }

    private void AddSoundSequenceStep(int offset)
    {
        AddSoundSequenceStepAtOffset(offset, SelectedSequenceStep?.BranchId ?? MainSequenceBranchId);
    }

    public bool AddSoundSequenceStepAtOffset(int offset, string branchId)
    {
        if (SelectedSequenceDefinition is null || string.IsNullOrWhiteSpace(branchId)
            || !CanAddSequenceStepAtOffset(offset, branchId))
        {
            return false;
        }

        var step = new ProjectSequenceStepDto
        {
            StepId = Guid.NewGuid(),
            StepType = SequenceStepType.Sound,
            BranchId = branchId,
            ContinuationMode = ContinuationMode.Continue,
            OffsetMs = offset,
            Order = NextSequenceOrderAtOffset(SelectedSequenceDefinition.Steps, offset),
            SoundEffectId = Guid.Empty
        };
        SelectedSequenceDefinition.Steps.Add(step);
        RefreshSequenceSteps();
        SelectedSequenceStep = step;
        NotifySequenceProjectEdited();
        return true;
    }

    public bool AddForkOutcomeStepAtOffset(int offset, bool success, bool sound)
    {
        if (SelectedSequenceDefinition?.Steps is not { } steps
            || SelectedSequenceStep is not { StepType: SequenceStepType.Action } source
            || (source.ContinuationMode ?? ContinuationMode.Continue) != ContinuationMode.Branch
            || offset < source.OffsetMs)
        {
            return false;
        }

        var currentTargetId = success ? source.OnSuccessStepId : source.OnFailureStepId;
        if (currentTargetId.HasValue)
        {
            return false;
        }

        var branch = CreateForkBranch(source, success ? "success" : "failure");
        var order = NextSequenceOrderAtOffset(steps, offset);
        if (offset == source.OffsetMs && order <= source.Order)
        {
            return false;
        }
        ProjectSequenceStepDto branchStep;
        if (sound)
        {
            branchStep = new ProjectSequenceStepDto
            {
                StepId = Guid.NewGuid(),
                BranchId = branch.BranchId,
                ContinuationMode = ContinuationMode.Continue,
                StepType = SequenceStepType.Sound,
                OffsetMs = offset,
                Order = order,
                SoundEffectId = Guid.Empty
            };
        }
        else
        {
            branchStep = CreateNewActionSequenceStep(offset);
            branchStep.BranchId = branch.BranchId;
            branchStep.Order = order;
            if (!EditSequenceStepAction(branchStep))
            {
                return false;
            }
        }

        steps.Add(branchStep);
        SelectedSequenceDefinition.Branches.Add(branch);
        OnPropertyChanged(nameof(SequenceBranchDefinitions));
        if (success)
        {
            source.OnSuccessStepId = branchStep.StepId;
            OnPropertyChanged(nameof(SelectedSequenceSuccessTargetStepId));
        }
        else
        {
            source.OnFailureStepId = branchStep.StepId;
            OnPropertyChanged(nameof(SelectedSequenceFailureTargetStepId));
        }

        RefreshSequenceSteps();
        SelectedSequenceStep = branchStep;
        NotifySequenceProjectEdited();
        return true;
    }

    private static ProjectSequenceStepDto CreateNewActionSequenceStep(int offset) => new()
    {
        StepId = Guid.NewGuid(),
        StepType = SequenceStepType.Action,
        BranchId = MainSequenceBranchId,
        ContinuationMode = ContinuationMode.Continue,
        OffsetMs = offset,
        Order = 0,
        InlineAction = new CommandActionDto
        {
            Id = Guid.NewGuid(),
            Name = "New Action",
            ActionType = CommandActionType.EchoMessage,
            NoVerbLinkage = true,
            ChildCommandForwardingMode = ChildCommandForwardingMode.None
        }
    };

    private static int NextSequenceOrderAtOffset(IEnumerable<ProjectSequenceStepDto> steps, int offset) =>
        steps.Where(step => step.OffsetMs == offset)
            .Select(static step => step.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;
}
