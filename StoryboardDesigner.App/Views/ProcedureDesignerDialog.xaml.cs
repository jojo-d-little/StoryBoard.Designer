using System.Collections.ObjectModel;
using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class ProcedureDesignerDialog : Window
{
    private sealed class VariablePickerItem
    {
        public required string VariableName { get; init; }
        public required string DisplayText { get; init; }
    }

    private readonly IList<ProcedureDefinition> _targetProcedures;
    private readonly IList<Guid>? _targetProcedureIds;
    private readonly ObservableCollection<ProcedureDefinition> _workingProcedures;
    private readonly IReadOnlyList<MaterializeSourceObjectChoiceItem> _objectChoices;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<VariablePickerItem>> _variablePickerItemsByObjectId;
    private bool _isRefreshing;

    public ProcedureDesignerDialog(IList<ProcedureDefinition> procedures)
        : this(
            "Procedures",
            null,
            procedures,
            Array.Empty<MaterializeSourceObjectChoiceItem>(),
            Array.Empty<GamePropertyChoiceItem>())
    {
    }

    public ProcedureDesignerDialog(
        string scopeLabel,
        IList<Guid>? procedureIds,
        IList<ProcedureDefinition> procedures,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> objectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices)
    {
        InitializeComponent();

        _targetProcedures = procedures;
        _targetProcedureIds = procedureIds;
        var ownedLookup = (procedureIds ?? new List<Guid>())
            .Where(static id => id != Guid.Empty)
            .ToHashSet();
        var sourceProcedures = procedureIds is null
            ? (procedures ?? Array.Empty<ProcedureDefinition>())
            : (procedures ?? Array.Empty<ProcedureDefinition>())
                .Where(procedure => procedure.Id != Guid.Empty && ownedLookup.Contains(procedure.Id));

        _workingProcedures = new ObservableCollection<ProcedureDefinition>(sourceProcedures
            .Select(CloneProcedure)
            .OrderBy(static procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static procedure => procedure.Id));

        _objectChoices = (objectChoices ?? Array.Empty<MaterializeSourceObjectChoiceItem>())
            .Where(static choice => choice.ObjectId != Guid.Empty)
            .GroupBy(static choice => choice.ObjectId)
            .Select(static group => group
                .OrderByDescending(choice => !string.IsNullOrWhiteSpace(choice.DisplayName) && !IsPlaceholderDisplayName(choice.DisplayName))
                .ThenByDescending(choice => choice.SourceObject is not null)
                .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _variablePickerItemsByObjectId = _objectChoices
            .Where(static choice => choice.ObjectId != Guid.Empty)
            .ToDictionary(
                static choice => choice.ObjectId,
                static choice => (IReadOnlyList<VariablePickerItem>)(choice.SourceObject?.Variables ?? new List<GamePropertyDefinition>())
                    .Where(static variable => !string.IsNullOrWhiteSpace(variable.Name))
                    .GroupBy(static variable => variable.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(static group => group.First())
                    .OrderBy(static variable => variable.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(variable => new VariablePickerItem
                    {
                        VariableName = variable.Name.Trim(),
                        DisplayText = variable.Name.Trim()
                    })
                    .ToList());

        NormalizeParticipantDisplayNames();
        NormalizeMutationParticipantDisplayNames();

        ProceduresListBox.ItemsSource = _workingProcedures;
        MutationVariableComboBox.DisplayMemberPath = nameof(VariablePickerItem.DisplayText);

        Title = string.IsNullOrWhiteSpace(scopeLabel)
            ? "Procedure Designer"
            : $"Procedure Designer - {scopeLabel}";

        ParticipantMatchKindComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantMatchKind>();
        ParticipantSatisfactionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantSatisfactionMode>();
        ParticipantConsumptionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantConsumptionPolicy>();
        MutationOperationComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantMutationOperation>();

        if (_workingProcedures.Count > 0)
        {
            ProceduresListBox.SelectedIndex = 0;
        }

        RefreshProcedureEditor();
    }

    private void NormalizeParticipantDisplayNames()
    {
        foreach (var participant in _workingProcedures
                     .SelectMany(static procedure => procedure.Participants))
        {
            if (participant.ObjectId == Guid.Empty
                || !TryResolveParticipantObjectId(participant, out var objectId)
                || string.IsNullOrWhiteSpace(ResolveObjectDisplayName(objectId)))
            {
                continue;
            }

            var objectName = ResolveObjectDisplayName(objectId);

            if (string.IsNullOrWhiteSpace(participant.DisplayName)
                || IsPlaceholderDisplayName(participant.DisplayName)
                || string.Equals(participant.DisplayName.Trim(), participant.MatchValue?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                participant.DisplayName = objectName;
            }
        }
    }

    private static ProcedureDefinition CloneProcedure(ProcedureDefinition procedure)
    {
        return new ProcedureDefinition
        {
            Id = procedure.Id == Guid.Empty ? Guid.NewGuid() : procedure.Id,
            Name = procedure.Name,
            ProcedureSummary = procedure.ProcedureSummary,
            ProcedureDescription = procedure.ProcedureDescription,
            Participants = (procedure.Participants ?? new List<ProcedureParticipantRequirement>())
                .Select(CloneParticipant)
                .ToList(),
            ParticipantMutations = (procedure.ParticipantMutations ?? new List<ProcedureParticipantMutation>())
                .Select(CloneMutation)
                .ToList()
        };
    }

    private static ProcedureParticipantRequirement CloneParticipant(ProcedureParticipantRequirement participant)
    {
        return new ProcedureParticipantRequirement
        {
            ObjectId = participant.ObjectId,
            DisplayName = participant.DisplayName,
            MatchKind = participant.MatchKind,
            MatchValue = participant.MatchValue,
            SatisfactionMode = participant.SatisfactionMode,
            Quantity = participant.Quantity,
            ConsumptionPolicy = participant.ConsumptionPolicy,
            OptionalPart = participant.OptionalPart,
            VariableRequirements = (participant.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                .Select(static requirement => new LockParticipantVariableRequirement
                {
                    VariableName = requirement.VariableName.Trim(),
                    Operator = requirement.Operator,
                    ExpectedValue = requirement.ExpectedValue,
                    QuantityEvaluationMode = requirement.QuantityEvaluationMode
                })
                .ToList()
        };
    }

    private static ProcedureParticipantMutation CloneMutation(ProcedureParticipantMutation mutation)
    {
        return new ProcedureParticipantMutation
        {
            ObjectId = mutation.ObjectId,
            ParticipantDisplayName = mutation.ParticipantDisplayName,
            Operation = mutation.Operation,
            VariableName = mutation.VariableName,
            Value = mutation.Value,
            Delta = mutation.Delta
        };
    }

    private ProcedureDefinition? SelectedProcedure => ProceduresListBox.SelectedItem as ProcedureDefinition;

    private ProcedureParticipantRequirement? SelectedParticipant => ParticipantsListBox.SelectedItem as ProcedureParticipantRequirement;

    private ProcedureParticipantMutation? SelectedMutation => MutationsListBox.SelectedItem as ProcedureParticipantMutation;

    private void AddProcedure_OnClick(object sender, RoutedEventArgs e)
    {
        var procedure = new ProcedureDefinition
        {
            Id = Guid.NewGuid(),
            Name = GenerateProcedureName("New Procedure")
        };

        _workingProcedures.Add(procedure);
        ProceduresListBox.SelectedItem = procedure;
    }

    private void RemoveProcedure_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedProcedure;
        if (selected is null)
        {
            return;
        }

        var index = ProceduresListBox.SelectedIndex;
        _workingProcedures.Remove(selected);

        if (_workingProcedures.Count == 0)
        {
            RefreshProcedureEditor();
            return;
        }

        var nextIndex = Math.Min(Math.Max(index, 0), _workingProcedures.Count - 1);
        ProceduresListBox.SelectedIndex = nextIndex;
    }

    private void ProceduresListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshProcedureEditor();
    }

    private void RefreshProcedureEditor()
    {
        _isRefreshing = true;
        try
        {
            var procedure = SelectedProcedure;
            var hasSelection = procedure is not null;

            ProcedureNameTextBox.IsEnabled = hasSelection;
            ProcedureSummaryTextBox.IsEnabled = hasSelection;
            ProcedureDescriptionTextBox.IsEnabled = hasSelection;

            ProcedureNameTextBox.Text = procedure?.Name ?? string.Empty;
            ProcedureSummaryTextBox.Text = procedure?.ProcedureSummary ?? string.Empty;
            ProcedureDescriptionTextBox.Text = procedure?.ProcedureDescription ?? string.Empty;

            ParticipantsListBox.ItemsSource = hasSelection
                ? procedure!.Participants
                : null;
            MutationsListBox.ItemsSource = hasSelection
                ? procedure!.ParticipantMutations
                : null;

            if (hasSelection && procedure!.Participants.Count > 0)
            {
                ParticipantsListBox.SelectedIndex = 0;
            }
            else
            {
                RefreshParticipantEditor();
            }

            if (hasSelection && procedure!.ParticipantMutations.Count > 0)
            {
                MutationsListBox.SelectedIndex = 0;
            }
            else
            {
                RefreshMutationEditor();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ProcedureNameTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isRefreshing || SelectedProcedure is not { } procedure)
        {
            return;
        }

        procedure.Name = ProcedureNameTextBox.Text;
        ProceduresListBox.Items.Refresh();
    }

    private void ProcedureSummaryTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isRefreshing || SelectedProcedure is not { } procedure)
        {
            return;
        }

        procedure.ProcedureSummary = ProcedureSummaryTextBox.Text;
    }

    private void ProcedureDescriptionTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isRefreshing || SelectedProcedure is not { } procedure)
        {
            return;
        }

        procedure.ProcedureDescription = ProcedureDescriptionTextBox.Text;
    }

    private void AddParticipant_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProcedure is not { } procedure)
        {
            System.Windows.MessageBox.Show(this, "Select or add a procedure first.", "Add Participant", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_objectChoices.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No objects are available to choose from.", "Add Participant", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var chooser = new SelectGameObjectDialog(
            _objectChoices,
            options: new SelectGameObjectDialog.Options(
                DefaultScopeSearchType: GameObjectOptionSourceTarget.RealObjects,
                DefaultScopeSearchDepth: ScopeSearchDepth.Project,
                DefaultRequiredFeatures: GameObjectFeatureRequirements.None,
                DefaultExcludeCurrentObject: true))
        {
            Owner = this
        };

        if (chooser.ShowDialog() != true || !chooser.SelectedObjectId.HasValue)
        {
            return;
        }

        var selectedObjectId = chooser.SelectedObjectId.Value;
        var selectedChoice = chooser.SelectedChoice
            ?? _objectChoices.FirstOrDefault(choice => choice.ObjectId == selectedObjectId);
        var resolvedName = ResolveObjectDisplayName(selectedObjectId, selectedChoice?.DisplayName);

        var participant = new ProcedureParticipantRequirement
        {
            ObjectId = selectedObjectId,
            DisplayName = resolvedName,
            MatchKind = ProcedureParticipantMatchKind.ObjectId,
            MatchValue = selectedObjectId.ToString("N")
        };

        procedure.Participants.Add(participant);
        ParticipantsListBox.Items.Refresh();
        ParticipantsListBox.SelectedItem = participant;
    }

    private void RemoveParticipant_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProcedure is not { } procedure || SelectedParticipant is not { } participant)
        {
            return;
        }

        var index = ParticipantsListBox.SelectedIndex;
        procedure.Participants.Remove(participant);
        ParticipantsListBox.Items.Refresh();

        if (procedure.Participants.Count == 0)
        {
            RefreshParticipantEditor();
            return;
        }

        ParticipantsListBox.SelectedIndex = Math.Min(Math.Max(index, 0), procedure.Participants.Count - 1);
    }

    private void ParticipantsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshParticipantEditor();
    }

    private void RefreshParticipantEditor()
    {
        _isRefreshing = true;
        try
        {
            var participant = SelectedParticipant;
            var hasSelection = participant is not null;

            ParticipantDisplayNameTextBox.IsEnabled = hasSelection;
            ParticipantMatchKindComboBox.IsEnabled = hasSelection;
            ParticipantSatisfactionComboBox.IsEnabled = hasSelection;
            ParticipantQuantityTextBox.IsEnabled = hasSelection;
            ParticipantConsumptionComboBox.IsEnabled = hasSelection;
            ParticipantOptionalCheckBox.IsEnabled = hasSelection;
            EditParticipantVariablesButton.IsEnabled = hasSelection;

            ParticipantDisplayNameTextBox.Text = participant?.DisplayName ?? string.Empty;
            ParticipantMatchKindComboBox.SelectedItem = participant?.MatchKind;
            ParticipantSatisfactionComboBox.SelectedItem = participant?.SatisfactionMode;
            ParticipantQuantityTextBox.Text = participant?.Quantity.ToString() ?? string.Empty;
            ParticipantConsumptionComboBox.SelectedItem = participant?.ConsumptionPolicy;
            ParticipantOptionalCheckBox.IsChecked = participant?.OptionalPart == true;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ParticipantField_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing || SelectedParticipant is not { } participant)
        {
            return;
        }

        var previousMatchKind = participant.MatchKind;
        var previousMatchValue = participant.MatchValue;

        participant.DisplayName = ParticipantDisplayNameTextBox.Text;
        participant.MatchKind = ParticipantMatchKindComboBox.SelectedItem is ProcedureParticipantMatchKind matchKind
            ? matchKind
            : ProcedureParticipantMatchKind.ObjectId;

        if (ReferenceEquals(sender, ParticipantMatchKindComboBox))
        {
            participant.MatchValue = ConvertMatchValueForKind(participant.MatchKind, previousMatchKind, previousMatchValue, participant.MatchValue, participant.ObjectId);
        }

        if (participant.MatchKind == ProcedureParticipantMatchKind.ObjectId
            && Guid.TryParse(participant.MatchValue, out var objectId)
            && objectId != Guid.Empty)
        {
            participant.ObjectId = objectId;
        }

        participant.SatisfactionMode = ParticipantSatisfactionComboBox.SelectedItem is ProcedureParticipantSatisfactionMode satisfactionMode
            ? satisfactionMode
            : ProcedureParticipantSatisfactionMode.ExplicitMentionRequired;
        participant.ConsumptionPolicy = ParticipantConsumptionComboBox.SelectedItem is ProcedureParticipantConsumptionPolicy consumptionPolicy
            ? consumptionPolicy
            : ProcedureParticipantConsumptionPolicy.None;
        participant.Quantity = int.TryParse(ParticipantQuantityTextBox.Text, out var quantity) && quantity > 0
            ? quantity
            : 1;
        participant.OptionalPart = ParticipantOptionalCheckBox.IsChecked == true;

        ParticipantsListBox.Items.Refresh();
    }

    private void EditParticipantVariables_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedParticipant is not { } participant)
        {
            return;
        }

        var variableNames = GetVariablePickerItemsForObject(participant.ObjectId)
            .Select(static item => item.VariableName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dialog = new LockParticipantVariableRequirementsDialog(variableNames, participant.VariableRequirements)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        participant.VariableRequirements = dialog.Values.ToList();
        ParticipantsListBox.Items.Refresh();
    }

    private string ResolveObjectDisplayName(Guid objectId, string? preferred = null)
    {
        var trimmedPreferred = preferred?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedPreferred) && !IsPlaceholderDisplayName(trimmedPreferred))
        {
            return trimmedPreferred;
        }

        var matchingChoices = _objectChoices.Where(choice => choice.ObjectId == objectId);
        foreach (var choice in matchingChoices)
        {
            var displayName = choice.DisplayName?.Trim();
            if (!string.IsNullOrWhiteSpace(displayName) && !IsPlaceholderDisplayName(displayName))
            {
                return displayName;
            }

            var scopeName = choice.SourceObject?.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(scopeName) && !IsPlaceholderDisplayName(scopeName))
            {
                return scopeName;
            }
        }

        return string.Empty;
    }

    private void NormalizeMutationParticipantDisplayNames()
    {
        foreach (var mutation in _workingProcedures
                     .SelectMany(static procedure => procedure.ParticipantMutations))
        {
            if (mutation.ObjectId == Guid.Empty)
            {
                mutation.ParticipantDisplayName = string.Empty;
                continue;
            }

            mutation.ParticipantDisplayName = ResolveObjectDisplayName(mutation.ObjectId, mutation.ParticipantDisplayName);
        }
    }

    private static bool IsPlaceholderDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("Participant", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Participany", StringComparison.OrdinalIgnoreCase);
    }

    private string ConvertMatchValueForKind(
        ProcedureParticipantMatchKind newKind,
        ProcedureParticipantMatchKind previousKind,
        string? previousValue,
        string? currentValue,
        Guid participantObjectId)
    {
        var previous = previousValue?.Trim() ?? string.Empty;
        var current = currentValue?.Trim() ?? string.Empty;

        if (newKind == ProcedureParticipantMatchKind.ObjectId)
        {
            if (Guid.TryParse(current, out var currentObjectId) && currentObjectId != Guid.Empty)
            {
                return currentObjectId.ToString("N");
            }

            if (previousKind == ProcedureParticipantMatchKind.ObjectType)
            {
                var matchByType = _objectChoices.FirstOrDefault(choice =>
                    string.Equals(choice.SourceObject?.Name, previous, StringComparison.OrdinalIgnoreCase));
                if (matchByType is not null)
                {
                    return matchByType.ObjectId.ToString("N");
                }
            }

            if (participantObjectId != Guid.Empty)
            {
                return participantObjectId.ToString("N");
            }

            return current;
        }

        if (newKind == ProcedureParticipantMatchKind.ObjectType)
        {
            if (previousKind == ProcedureParticipantMatchKind.ObjectId
                && Guid.TryParse(previous, out var previousObjectId)
                && previousObjectId != Guid.Empty)
            {
                var byId = _objectChoices.FirstOrDefault(choice => choice.ObjectId == previousObjectId);
                var resolvedType = byId?.SourceObject?.Name?.Trim();
                if (!string.IsNullOrWhiteSpace(resolvedType))
                {
                    return resolvedType;
                }
            }

            if (participantObjectId != Guid.Empty)
            {
                var byPersistedObjectId = _objectChoices.FirstOrDefault(choice => choice.ObjectId == participantObjectId);
                var resolvedType = byPersistedObjectId?.SourceObject?.Name?.Trim();
                if (!string.IsNullOrWhiteSpace(resolvedType))
                {
                    return resolvedType;
                }
            }

            return current;
        }

        return current;
    }

    private void AddMutation_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProcedure is not { } procedure)
        {
            return;
        }

        var mutation = new ProcedureParticipantMutation
        {
            ObjectId = procedure.Participants.FirstOrDefault()?.ObjectId ?? Guid.Empty,
            ParticipantDisplayName = ResolveObjectDisplayName(procedure.Participants.FirstOrDefault()?.ObjectId ?? Guid.Empty),
            Operation = ProcedureParticipantMutationOperation.SetVariable,
            VariableName = "variableName",
            Value = "value"
        };

        procedure.ParticipantMutations.Add(mutation);
        MutationsListBox.Items.Refresh();
        MutationsListBox.SelectedItem = mutation;
    }

    private void RemoveMutation_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProcedure is not { } procedure || SelectedMutation is not { } mutation)
        {
            return;
        }

        var index = MutationsListBox.SelectedIndex;
        procedure.ParticipantMutations.Remove(mutation);
        MutationsListBox.Items.Refresh();

        if (procedure.ParticipantMutations.Count == 0)
        {
            RefreshMutationEditor();
            return;
        }

        MutationsListBox.SelectedIndex = Math.Min(Math.Max(index, 0), procedure.ParticipantMutations.Count - 1);
    }

    private void MutationsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshMutationEditor();
    }

    private void RefreshMutationEditor()
    {
        _isRefreshing = true;
        try
        {
            var mutation = SelectedMutation;
            var hasSelection = mutation is not null;

            MutationParticipantKeyTextBox.IsEnabled = hasSelection;
            MutationOperationComboBox.IsEnabled = hasSelection;
            MutationVariableComboBox.IsEnabled = hasSelection;
            MutationValueTextBox.IsEnabled = hasSelection;
            MutationDeltaTextBox.IsEnabled = hasSelection;

            MutationParticipantKeyTextBox.Text = mutation is null || mutation.ObjectId == Guid.Empty
                ? string.Empty
                : ResolveObjectDisplayName(mutation.ObjectId, mutation.ParticipantDisplayName);

            var allowedVariables = mutation is null
                ? Array.Empty<VariablePickerItem>()
                : GetVariablePickerItemsForObject(mutation.ObjectId);
            MutationVariableComboBox.ItemsSource = allowedVariables;

            MutationOperationComboBox.SelectedItem = mutation?.Operation;
            MutationValueTextBox.Text = mutation?.Value ?? string.Empty;
            MutationDeltaTextBox.Text = mutation?.Delta?.ToString() ?? string.Empty;

            if (mutation is not null)
            {
                MutationVariableComboBox.SelectedItem = allowedVariables
                    .FirstOrDefault(item => string.Equals(item.VariableName, mutation.VariableName, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void MutationField_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing || SelectedMutation is not { } mutation)
        {
            return;
        }

        mutation.ParticipantDisplayName = ResolveObjectDisplayName(mutation.ObjectId, mutation.ParticipantDisplayName);

        mutation.Operation = MutationOperationComboBox.SelectedItem is ProcedureParticipantMutationOperation operation
            ? operation
            : ProcedureParticipantMutationOperation.SetVariable;
        mutation.VariableName = MutationVariableComboBox.SelectedItem is VariablePickerItem selectedVariable
            ? selectedVariable.VariableName
            : string.Empty;
        mutation.Value = MutationValueTextBox.Text;
        mutation.Delta = double.TryParse(MutationDeltaTextBox.Text, out var delta)
            ? delta
            : null;

        MutationsListBox.Items.Refresh();
    }

    private IReadOnlyList<VariablePickerItem> GetVariablePickerItemsForObject(Guid objectId)
    {
        if (objectId == Guid.Empty)
        {
            return Array.Empty<VariablePickerItem>();
        }

        return _variablePickerItemsByObjectId.TryGetValue(objectId, out var items)
            ? items
            : Array.Empty<VariablePickerItem>();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var procedure in _workingProcedures)
        {
            procedure.Id = procedure.Id == Guid.Empty ? Guid.NewGuid() : procedure.Id;
            procedure.Name = string.IsNullOrWhiteSpace(procedure.Name) ? "Procedure" : procedure.Name.Trim();
            procedure.ProcedureSummary = procedure.ProcedureSummary?.Trim() ?? string.Empty;
            procedure.ProcedureDescription = procedure.ProcedureDescription?.Trim() ?? string.Empty;
            procedure.Participants = procedure.Participants
                .Where(static participant => !string.IsNullOrWhiteSpace(participant.MatchValue))
                .Select(CloneParticipant)
                .ToList();

            procedure.ParticipantMutations = procedure.ParticipantMutations
                .Where(static mutation => mutation.ObjectId != Guid.Empty
                    && !string.IsNullOrWhiteSpace(mutation.VariableName))
                .Select(CloneMutation)
                .ToList();
        }

        var normalizedProcedures = _workingProcedures
                     .GroupBy(static procedure => procedure.Id)
                     .Select(static group => group.First())
                     .OrderBy(static procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static procedure => procedure.Id)
            .Select(CloneProcedure)
            .ToList();

        if (_targetProcedureIds is null)
        {
            _targetProcedures.Clear();
            foreach (var procedure in normalizedProcedures)
            {
                _targetProcedures.Add(procedure);
            }
        }
        else
        {
            var removedIds = _targetProcedureIds.ToHashSet();
            for (var i = _targetProcedures.Count - 1; i >= 0; i--)
            {
                if (removedIds.Contains(_targetProcedures[i].Id))
                {
                    _targetProcedures.RemoveAt(i);
                }
            }

            foreach (var procedure in normalizedProcedures)
            {
                _targetProcedures.Add(procedure);
            }

            _targetProcedureIds.Clear();
            foreach (var id in normalizedProcedures.Select(static procedure => procedure.Id))
            {
                _targetProcedureIds.Add(id);
            }
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private string GenerateProcedureName(string baseName)
    {
        var candidate = baseName;
        var index = 2;
        while (_workingProcedures.Any(procedure => string.Equals(procedure.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {index}";
            index++;
        }

        return candidate;
    }

    private static bool TryResolveParticipantObjectId(ProcedureParticipantRequirement participant, out Guid objectId)
    {
        objectId = Guid.Empty;
        if (participant.ObjectId == Guid.Empty)
        {
            return false;
        }

        objectId = participant.ObjectId;
        return true;
    }

}


