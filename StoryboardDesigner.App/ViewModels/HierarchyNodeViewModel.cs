using StoryboardDesigner.App.Models;
using System.Collections.ObjectModel;

namespace StoryboardDesigner.App.ViewModels;

public abstract class HierarchyNodeViewModel : ViewModelBase
{
    private string _editableName;
    private string _committedName;
    private int _validationErrorCountOnNode;
    private int _validationWarningCountOnNode;
    private int _validationErrorCountInDescendants;
    private int _validationWarningCountInDescendants;
    private bool _isValidationStatusStale;
    private string _validationRunSummary = "Not yet validated.";
    private IReadOnlyList<string> _directValidationIssueMessages = Array.Empty<string>();

    protected HierarchyNodeViewModel(string editableName, HierarchyNodeViewModel? parent = null)
    {
        _editableName = editableName;
        _committedName = editableName;
        Parent = parent;
    }

    public string NodeTypeLabel { get; protected init; } = "Node";

    public string EditableName
    {
        get => _editableName;
        set
        {
            var candidate = value ?? string.Empty;
            if (_editableName == candidate)
            {
                return;
            }

            _editableName = candidate;
            var trimmed = candidate.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                _committedName = trimmed;
                RenameModel(trimmed);
            }

            OnPropertyChanged();
        }
    }

    public void SetEditableNameWithoutModelRename(string value)
    {
        var candidate = value ?? string.Empty;
        if (_editableName == candidate)
        {
            return;
        }

        _editableName = candidate;
        var trimmed = candidate.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            _committedName = trimmed;
        }

        OnPropertyChanged(nameof(EditableName));
    }

    public HierarchyNodeViewModel? Parent { get; }
    public ObservableCollection<HierarchyNodeViewModel> Children { get; } = new();

    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    protected virtual void RenameModel(string newName)
    {
    }

    public int ValidationErrorCountOnNode
    {
        get => _validationErrorCountOnNode;
        private set
        {
            if (_validationErrorCountOnNode == value)
            {
                return;
            }

            _validationErrorCountOnNode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationErrorsOnNode));
            OnPropertyChanged(nameof(HasValidationIssues));
            OnPropertyChanged(nameof(HasValidationBadge));
            OnPropertyChanged(nameof(ValidationBadgeText));
            OnPropertyChanged(nameof(ValidationSummaryToolTip));
            OnPropertyChanged(nameof(ShouldShowValidationSummaryToolTip));
        }
    }

    public int ValidationWarningCountOnNode
    {
        get => _validationWarningCountOnNode;
        private set
        {
            if (_validationWarningCountOnNode == value)
            {
                return;
            }

            _validationWarningCountOnNode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationWarningsOnNode));
            OnPropertyChanged(nameof(HasValidationIssues));
            OnPropertyChanged(nameof(HasValidationBadge));
            OnPropertyChanged(nameof(ValidationBadgeText));
            OnPropertyChanged(nameof(ValidationBadgeCompactText));
            OnPropertyChanged(nameof(ValidationBadgeCompactText));
            OnPropertyChanged(nameof(ValidationSummaryToolTip));
            OnPropertyChanged(nameof(ShouldShowValidationSummaryToolTip));
        }
    }

    public int ValidationErrorCountInDescendants
    {
        get => _validationErrorCountInDescendants;
        private set
        {
            if (_validationErrorCountInDescendants == value)
            {
                return;
            }

            _validationErrorCountInDescendants = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationErrorsInDescendants));
            OnPropertyChanged(nameof(HasValidationIssues));
            OnPropertyChanged(nameof(HasValidationBadge));
            OnPropertyChanged(nameof(ValidationBadgeText));
            OnPropertyChanged(nameof(ValidationBadgeCompactText));
            OnPropertyChanged(nameof(ValidationSummaryToolTip));
            OnPropertyChanged(nameof(ShouldShowValidationSummaryToolTip));
        }
    }

    public int ValidationWarningCountInDescendants
    {
        get => _validationWarningCountInDescendants;
        private set
        {
            if (_validationWarningCountInDescendants == value)
            {
                return;
            }

            _validationWarningCountInDescendants = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationWarningsInDescendants));
            OnPropertyChanged(nameof(HasValidationIssues));
            OnPropertyChanged(nameof(HasValidationBadge));
            OnPropertyChanged(nameof(ValidationBadgeText));
            OnPropertyChanged(nameof(ValidationBadgeCompactText));
            OnPropertyChanged(nameof(ValidationSummaryToolTip));
            OnPropertyChanged(nameof(ShouldShowValidationSummaryToolTip));
        }
    }

    public bool IsValidationStatusStale
    {
        get => _isValidationStatusStale;
        set
        {
            if (_isValidationStatusStale == value)
            {
                return;
            }

            _isValidationStatusStale = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationBadge));
            OnPropertyChanged(nameof(ValidationBadgeText));
            OnPropertyChanged(nameof(ValidationBadgeCompactText));
            OnPropertyChanged(nameof(ValidationSummaryToolTip));
            OnPropertyChanged(nameof(ShouldShowValidationSummaryToolTip));
        }
    }

    public string ValidationRunSummary
    {
        get => _validationRunSummary;
        private set
        {
            if (string.Equals(_validationRunSummary, value, StringComparison.Ordinal))
            {
                return;
            }

            _validationRunSummary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ValidationSummaryToolTip));
            OnPropertyChanged(nameof(ShouldShowValidationSummaryToolTip));
        }
    }

    public bool HasValidationErrorsOnNode => ValidationErrorCountOnNode > 0;
    public bool HasValidationWarningsOnNode => ValidationWarningCountOnNode > 0;
    public bool HasValidationErrorsInDescendants => ValidationErrorCountInDescendants > 0;
    public bool HasValidationWarningsInDescendants => ValidationWarningCountInDescendants > 0;
    public bool HasValidationIssues => HasValidationErrorsOnNode || HasValidationWarningsOnNode || HasValidationErrorsInDescendants || HasValidationWarningsInDescendants;
    public bool HasValidationBadge => IsValidationStatusStale || HasValidationErrorsOnNode || HasValidationWarningsOnNode;
    public virtual bool ShouldShowValidationSummaryToolTip => HasValidationErrorsOnNode || HasValidationWarningsOnNode;

    public string ValidationBadgeText
    {
        get
        {
            if (IsValidationStatusStale)
            {
                return "stale";
            }

            if (HasValidationErrorsOnNode || HasValidationWarningsOnNode)
            {
                return $"E{ValidationErrorCountOnNode}/W{ValidationWarningCountOnNode}";
            }

            if (HasValidationErrorsInDescendants || HasValidationWarningsInDescendants)
            {
                return $"Desc E{ValidationErrorCountInDescendants}/W{ValidationWarningCountInDescendants}";
            }

            return string.Empty;
        }
    }

    public string ValidationBadgeCompactText
    {
        get
        {
            if (IsValidationStatusStale)
            {
                return "~";
            }

            if (HasValidationErrorsOnNode || HasValidationWarningsOnNode)
            {
                return "!";
            }

            if (HasValidationErrorsInDescendants || HasValidationWarningsInDescendants)
            {
                return string.Empty;
            }

            return string.Empty;
        }
    }

    public string ValidationSummaryToolTip
    {
        get
        {
            var nodeDetails = BuildNodeDetailsToolTip();
            var validationDetails = BuildValidationDetailsToolTip();

            if (string.IsNullOrWhiteSpace(nodeDetails))
            {
                return validationDetails;
            }

            if (string.IsNullOrWhiteSpace(validationDetails))
            {
                return nodeDetails;
            }

            return $"{nodeDetails}\n\n{validationDetails}";
        }
    }

    protected virtual string BuildNodeDetailsToolTip()
    {
        return string.Empty;
    }

    protected virtual bool IncludeValidationDetailsInToolTip => true;

    private string BuildValidationDetailsToolTip()
    {
        if (!IncludeValidationDetailsInToolTip)
        {
            return string.Empty;
        }

        if (!HasValidationIssues)
        {
            return string.Empty;
        }

        var direct = $"Direct issues: {ValidationErrorCountOnNode} error(s), {ValidationWarningCountOnNode} warning(s).";
        var descendants = $"Descendant issues: {ValidationErrorCountInDescendants} error(s), {ValidationWarningCountInDescendants} warning(s).";
        var directIssuePreview = BuildDirectIssuePreview();
        var stale = IsValidationStatusStale
            ? "Validation status is stale and should be refreshed."
            : "Validation status is current for the most recent overlapping run.";

        return $"{direct}\n{descendants}\n{directIssuePreview}\n{stale}\n{ValidationRunSummary}";
    }

    public void SetValidationCounts(int directErrorCount, int directWarningCount, int descendantErrorCount, int descendantWarningCount)
    {
        ValidationErrorCountOnNode = Math.Max(0, directErrorCount);
        ValidationWarningCountOnNode = Math.Max(0, directWarningCount);
        ValidationErrorCountInDescendants = Math.Max(0, descendantErrorCount);
        ValidationWarningCountInDescendants = Math.Max(0, descendantWarningCount);
    }

    public void SetValidationRunSummary(string summary)
    {
        ValidationRunSummary = string.IsNullOrWhiteSpace(summary)
            ? "Not yet validated."
            : summary.Trim();
    }

    public void SetDirectValidationIssueMessages(IReadOnlyList<string> directIssues)
    {
        _directValidationIssueMessages = (directIssues ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        OnPropertyChanged(nameof(ValidationSummaryToolTip));
    }

    private string BuildDirectIssuePreview()
    {
        if (_directValidationIssueMessages.Count == 0)
        {
            return "Direct issue details: none.";
        }

        var previewLines = _directValidationIssueMessages
            .Take(2)
            .Select(static message => $"- {message}")
            .ToList();
        var preview = string.Join("\n", previewLines);

        var remaining = _directValidationIssueMessages.Count - previewLines.Count;
        if (remaining <= 0)
        {
            return $"Direct issue details:\n{preview}";
        }

        return $"Direct issue details:\n{preview}\n...and {remaining} more direct issue(s). Use context menu 'Show Node Validation Issues'.";
    }

    public void CommitEditableName()
    {
        var trimmed = _editableName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            _editableName = _committedName;
            OnPropertyChanged(nameof(EditableName));
            return;
        }

        if (_editableName != trimmed)
        {
            _editableName = trimmed;
            OnPropertyChanged(nameof(EditableName));
        }

        if (_committedName != trimmed)
        {
            _committedName = trimmed;
            RenameModel(trimmed);
        }
    }
}


