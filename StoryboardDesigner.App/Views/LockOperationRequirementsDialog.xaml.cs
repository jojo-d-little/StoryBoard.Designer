using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class LockOperationRequirementsDialog : Window
{
    private readonly ObservableCollection<LockKeyRequirement> _unlockParticipants;
    private readonly ObservableCollection<LockKeyRequirement> _lockParticipants;
    private readonly IReadOnlyList<MaterializeSourceObjectChoiceItem> _objectChoices;
    private bool _isRefreshing;

    public LockOperationRequirementsDialog(LockOperationRequirementsEditRequest initialValues)
    {
        InitializeComponent();

        AvailableKeyOptions = (initialValues.AvailableKeyOptions ?? Array.Empty<GameObjectSelectionOption>())
            .Where(option => option.Id != Guid.Empty)
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _objectChoices = AvailableKeyOptions
            .Select(option => new MaterializeSourceObjectChoiceItem
            {
                ObjectId = option.Id,
                DisplayName = option.Name,
                ScopePath = "Project",
                SourceCategory = "Object"
            })
            .OrderBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static choice => choice.ObjectId)
            .ToList();

        _unlockParticipants = new ObservableCollection<LockKeyRequirement>(
            (initialValues.UnlockKeyRequirements ?? new List<LockKeyRequirement>())
            .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
            .Select(CloneRequirement));

        _lockParticipants = new ObservableCollection<LockKeyRequirement>(
            (initialValues.LockKeyRequirements ?? new List<LockKeyRequirement>())
            .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
            .Select(CloneRequirement));

        HydrateParticipantDisplayNames(_unlockParticipants);
        HydrateParticipantDisplayNames(_lockParticipants);

        UnlockParticipantsListBox.ItemsSource = _unlockParticipants;
        LockParticipantsListBox.ItemsSource = _lockParticipants;

        UnlockParticipantObjectComboBox.ItemsSource = AvailableKeyOptions;
        LockParticipantObjectComboBox.ItemsSource = AvailableKeyOptions;

        UnlockParticipantMatchKindComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantMatchKind>();
        UnlockParticipantSatisfactionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantSatisfactionMode>();
        UnlockParticipantConsumptionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantConsumptionPolicy>();

        LockParticipantMatchKindComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantMatchKind>();
        LockParticipantSatisfactionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantSatisfactionMode>();
        LockParticipantConsumptionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantConsumptionPolicy>();

        RequireKeyForLockOperationCheckBox.IsChecked = initialValues.RequireKeyForLockOperation;
        UseUnlockKeysForLockOperationCheckBox.IsChecked = initialValues.UseUnlockKeysForLockOperation
            || ShouldDefaultToUsingUnlockKeys(initialValues);

        if (_unlockParticipants.Count > 0)
        {
            UnlockParticipantsListBox.SelectedIndex = 0;
        }

        if (_lockParticipants.Count > 0)
        {
            LockParticipantsListBox.SelectedIndex = 0;
        }

        RefreshUnlockParticipantEditor();
        RefreshLockParticipantEditor();
        RefreshLockSectionState();
    }

    public IReadOnlyList<GameObjectSelectionOption> AvailableKeyOptions { get; }

    public LockOperationRequirements Values
    {
        get
        {
            var unlockParticipants = _unlockParticipants.Select(CloneRequirement).ToList();
            var lockParticipants = ResolveLockParticipantsForSave();

            return new LockOperationRequirements
            {
                UnlockKeyRequirements = unlockParticipants,
                RequireKeyForLockOperation = RequireKeyForLockOperationCheckBox.IsChecked == true,
                UseUnlockKeysForLockOperation = UseUnlockKeysForLockOperationCheckBox.IsChecked == true,
                LockKeyRequirements = UseUnlockKeysForLockOperationCheckBox.IsChecked == true
                    ? new List<LockKeyRequirement>()
                    : lockParticipants
            };
        }
    }

    private List<LockKeyRequirement> ResolveLockParticipantsForSave()
    {
        if (UseUnlockKeysForLockOperationCheckBox.IsChecked == true)
        {
            return _unlockParticipants.Select(CloneRequirement).ToList();
        }

        return _lockParticipants.Select(CloneRequirement).ToList();
    }

    private static LockKeyRequirement CloneRequirement(LockKeyRequirement requirement)
    {
        return new LockKeyRequirement
        {
            RequiredObjectId = requirement.RequiredObjectId,
            RequiredObjectName = requirement.RequiredObjectName ?? string.Empty,
            RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
            MatchKind = requirement.MatchKind,
            MatchValue = requirement.MatchValue ?? string.Empty,
            SatisfactionMode = requirement.SatisfactionMode,
            ConsumptionPolicy = requirement.ConsumptionPolicy,
            IsOptional = requirement.IsOptional,
            VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                .Select(static item => new LockParticipantVariableRequirement
                {
                    VariableName = item.VariableName.Trim(),
                    Operator = item.Operator,
                    ExpectedValue = item.ExpectedValue,
                    QuantityEvaluationMode = item.QuantityEvaluationMode
                })
                .ToList()
        };
    }

    private LockKeyRequirement? SelectedUnlockParticipant => UnlockParticipantsListBox.SelectedItem as LockKeyRequirement;
    private LockKeyRequirement? SelectedLockParticipant => LockParticipantsListBox.SelectedItem as LockKeyRequirement;

    private void AddUnlockParticipant_OnClick(object sender, RoutedEventArgs e)
    {
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
        var selectedOption = AvailableKeyOptions.FirstOrDefault(option => option.Id == selectedObjectId);
        var resolvedName = selectedChoice?.DisplayName
            ?? selectedOption?.Name
            ?? selectedObjectId.ToString("D");

        var participant = new LockKeyRequirement
        {
            RequiredObjectId = selectedObjectId,
            RequiredObjectName = resolvedName,
            RequiredQuantity = 1,
            MatchKind = ProcedureParticipantMatchKind.ObjectId,
            MatchValue = selectedObjectId.ToString("D"),
            SatisfactionMode = ProcedureParticipantSatisfactionMode.PossessionRequired,
            ConsumptionPolicy = ProcedureParticipantConsumptionPolicy.None
        };

        _unlockParticipants.Add(participant);
        UnlockParticipantsListBox.SelectedItem = participant;
    }

    private void RemoveUnlockParticipant_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUnlockParticipant;
        if (selected is null)
        {
            return;
        }

        var index = UnlockParticipantsListBox.SelectedIndex;
        _unlockParticipants.Remove(selected);

        if (_unlockParticipants.Count == 0)
        {
            RefreshUnlockParticipantEditor();
            return;
        }

        UnlockParticipantsListBox.SelectedIndex = Math.Min(Math.Max(index, 0), _unlockParticipants.Count - 1);
    }

    private void ClearUnlockParticipants_OnClick(object sender, RoutedEventArgs e)
    {
        _unlockParticipants.Clear();
        RefreshUnlockParticipantEditor();
    }

    private void UnlockParticipantsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshUnlockParticipantEditor();
    }

    private void UnlockParticipantField_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        var participant = SelectedUnlockParticipant;
        if (participant is null)
        {
            return;
        }

        participant.RequiredObjectId = ResolveSelectedObjectId(UnlockParticipantObjectComboBox, participant.RequiredObjectId);
        participant.RequiredObjectName = ResolveSelectedObjectName(UnlockParticipantObjectComboBox, participant.RequiredObjectName);
        participant.RequiredQuantity = ParseMinimumCount(UnlockParticipantQuantityTextBox.Text);
        participant.MatchKind = ProcedureParticipantMatchKind.ObjectId;
        participant.MatchValue = participant.RequiredObjectId == Guid.Empty
            ? string.Empty
            : participant.RequiredObjectId.ToString("D");
        participant.SatisfactionMode = UnlockParticipantSatisfactionComboBox.SelectedItem is ProcedureParticipantSatisfactionMode satisfactionMode
            ? satisfactionMode
            : ProcedureParticipantSatisfactionMode.PossessionRequired;
        participant.ConsumptionPolicy = UnlockParticipantConsumptionComboBox.SelectedItem is ProcedureParticipantConsumptionPolicy consumptionPolicy
            ? consumptionPolicy
            : ProcedureParticipantConsumptionPolicy.None;
        participant.IsOptional = UnlockParticipantOptionalCheckBox.IsChecked == true;

        UnlockParticipantMatchKindComboBox.SelectedItem = ProcedureParticipantMatchKind.ObjectId;
        UnlockParticipantMatchValueTextBox.Text = participant.MatchValue;

        UnlockParticipantsListBox.Items.Refresh();
    }

    private void EditUnlockParticipantVariables_OnClick(object sender, RoutedEventArgs e)
    {
        var participant = SelectedUnlockParticipant;
        if (participant is null)
        {
            return;
        }

        var variableNames = ResolveVariableNamesForParticipant(participant);
        var dialog = new LockParticipantVariableRequirementsDialog(variableNames, participant.VariableRequirements)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        participant.VariableRequirements = dialog.Values.ToList();
        UnlockParticipantsListBox.Items.Refresh();
    }

    private void RefreshUnlockParticipantEditor()
    {
        _isRefreshing = true;
        try
        {
            var participant = SelectedUnlockParticipant;
            var hasSelection = participant is not null;

            UnlockParticipantObjectComboBox.IsEnabled = hasSelection;
            UnlockParticipantQuantityTextBox.IsEnabled = hasSelection;
            UnlockParticipantMatchKindComboBox.IsEnabled = hasSelection;
            UnlockParticipantMatchValueTextBox.IsEnabled = hasSelection;
            UnlockParticipantSatisfactionComboBox.IsEnabled = hasSelection;
            UnlockParticipantConsumptionComboBox.IsEnabled = hasSelection;
            UnlockParticipantOptionalCheckBox.IsEnabled = hasSelection;

            if (!hasSelection)
            {
                UnlockParticipantObjectComboBox.SelectedItem = null;
                UnlockParticipantQuantityTextBox.Text = string.Empty;
                UnlockParticipantMatchKindComboBox.SelectedItem = null;
                UnlockParticipantMatchValueTextBox.Text = string.Empty;
                UnlockParticipantSatisfactionComboBox.SelectedItem = null;
                UnlockParticipantConsumptionComboBox.SelectedItem = null;
                UnlockParticipantOptionalCheckBox.IsChecked = false;
                UnlockParticipantMatchValueTextBox.IsReadOnly = false;
                return;
            }

            var option = AvailableKeyOptions.FirstOrDefault(item => item.Id == participant!.RequiredObjectId);
            UnlockParticipantObjectComboBox.SelectedItem = option;
            UnlockParticipantQuantityTextBox.Text = Math.Max(1, participant!.RequiredQuantity).ToString();
            UnlockParticipantMatchKindComboBox.SelectedItem = participant.MatchKind;
            UnlockParticipantMatchValueTextBox.Text = participant.MatchValue;
            UnlockParticipantSatisfactionComboBox.SelectedItem = participant.SatisfactionMode;
            UnlockParticipantConsumptionComboBox.SelectedItem = participant.ConsumptionPolicy;
            UnlockParticipantOptionalCheckBox.IsChecked = participant.IsOptional;
            UnlockParticipantMatchKindComboBox.SelectedItem = ProcedureParticipantMatchKind.ObjectId;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void AddLockParticipant_OnClick(object sender, RoutedEventArgs e)
    {
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
        var selectedOption = AvailableKeyOptions.FirstOrDefault(option => option.Id == selectedObjectId);
        var resolvedName = selectedChoice?.DisplayName
            ?? selectedOption?.Name
            ?? selectedObjectId.ToString("D");

        var participant = new LockKeyRequirement
        {
            RequiredObjectId = selectedObjectId,
            RequiredObjectName = resolvedName,
            RequiredQuantity = 1,
            MatchKind = ProcedureParticipantMatchKind.ObjectId,
            MatchValue = selectedObjectId.ToString("D"),
            SatisfactionMode = ProcedureParticipantSatisfactionMode.PossessionRequired,
            ConsumptionPolicy = ProcedureParticipantConsumptionPolicy.None
        };

        _lockParticipants.Add(participant);
        LockParticipantsListBox.SelectedItem = participant;
    }

    private void RemoveLockParticipant_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedLockParticipant;
        if (selected is null)
        {
            return;
        }

        var index = LockParticipantsListBox.SelectedIndex;
        _lockParticipants.Remove(selected);

        if (_lockParticipants.Count == 0)
        {
            RefreshLockParticipantEditor();
            return;
        }

        LockParticipantsListBox.SelectedIndex = Math.Min(Math.Max(index, 0), _lockParticipants.Count - 1);
    }

    private void ClearLockParticipants_OnClick(object sender, RoutedEventArgs e)
    {
        _lockParticipants.Clear();
        RefreshLockParticipantEditor();
    }

    private void LockParticipantsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshLockParticipantEditor();
    }

    private void LockParticipantField_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        var participant = SelectedLockParticipant;
        if (participant is null)
        {
            return;
        }

        participant.RequiredObjectId = ResolveSelectedObjectId(LockParticipantObjectComboBox, participant.RequiredObjectId);
        participant.RequiredObjectName = ResolveSelectedObjectName(LockParticipantObjectComboBox, participant.RequiredObjectName);
        participant.RequiredQuantity = ParseMinimumCount(LockParticipantQuantityTextBox.Text);
        participant.MatchKind = ProcedureParticipantMatchKind.ObjectId;
        participant.MatchValue = participant.RequiredObjectId == Guid.Empty
            ? string.Empty
            : participant.RequiredObjectId.ToString("D");
        participant.SatisfactionMode = LockParticipantSatisfactionComboBox.SelectedItem is ProcedureParticipantSatisfactionMode satisfactionMode
            ? satisfactionMode
            : ProcedureParticipantSatisfactionMode.PossessionRequired;
        participant.ConsumptionPolicy = LockParticipantConsumptionComboBox.SelectedItem is ProcedureParticipantConsumptionPolicy consumptionPolicy
            ? consumptionPolicy
            : ProcedureParticipantConsumptionPolicy.None;
        participant.IsOptional = LockParticipantOptionalCheckBox.IsChecked == true;

        LockParticipantMatchKindComboBox.SelectedItem = ProcedureParticipantMatchKind.ObjectId;
        LockParticipantMatchValueTextBox.Text = participant.MatchValue;

        LockParticipantsListBox.Items.Refresh();
    }

    private void EditLockParticipantVariables_OnClick(object sender, RoutedEventArgs e)
    {
        var participant = SelectedLockParticipant;
        if (participant is null)
        {
            return;
        }

        var variableNames = ResolveVariableNamesForParticipant(participant);
        var dialog = new LockParticipantVariableRequirementsDialog(variableNames, participant.VariableRequirements)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        participant.VariableRequirements = dialog.Values.ToList();
        LockParticipantsListBox.Items.Refresh();
    }

    private void RefreshLockParticipantEditor()
    {
        _isRefreshing = true;
        try
        {
            var participant = SelectedLockParticipant;
            var hasSelection = participant is not null;

            LockParticipantObjectComboBox.IsEnabled = hasSelection;
            LockParticipantQuantityTextBox.IsEnabled = hasSelection;
            LockParticipantMatchKindComboBox.IsEnabled = hasSelection;
            LockParticipantMatchValueTextBox.IsEnabled = hasSelection;
            LockParticipantSatisfactionComboBox.IsEnabled = hasSelection;
            LockParticipantConsumptionComboBox.IsEnabled = hasSelection;
            LockParticipantOptionalCheckBox.IsEnabled = hasSelection;
            EditLockParticipantVariablesButton.IsEnabled = hasSelection;

            if (!hasSelection)
            {
                LockParticipantObjectComboBox.SelectedItem = null;
                LockParticipantQuantityTextBox.Text = string.Empty;
                LockParticipantMatchKindComboBox.SelectedItem = null;
                LockParticipantMatchValueTextBox.Text = string.Empty;
                LockParticipantSatisfactionComboBox.SelectedItem = null;
                LockParticipantConsumptionComboBox.SelectedItem = null;
                LockParticipantOptionalCheckBox.IsChecked = false;
                LockParticipantMatchValueTextBox.IsReadOnly = false;
                return;
            }

            var option = AvailableKeyOptions.FirstOrDefault(item => item.Id == participant!.RequiredObjectId);
            LockParticipantObjectComboBox.SelectedItem = option;
            LockParticipantQuantityTextBox.Text = Math.Max(1, participant!.RequiredQuantity).ToString();
            LockParticipantMatchKindComboBox.SelectedItem = participant.MatchKind;
            LockParticipantMatchValueTextBox.Text = participant.MatchValue;
            LockParticipantSatisfactionComboBox.SelectedItem = participant.SatisfactionMode;
            LockParticipantConsumptionComboBox.SelectedItem = participant.ConsumptionPolicy;
            LockParticipantOptionalCheckBox.IsChecked = participant.IsOptional;
            LockParticipantMatchKindComboBox.SelectedItem = ProcedureParticipantMatchKind.ObjectId;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static Guid ResolveSelectedObjectId(System.Windows.Controls.ComboBox comboBox, Guid fallback)
    {
        return comboBox.SelectedItem is GameObjectSelectionOption selected
            ? selected.Id
            : fallback;
    }

    private static string ResolveSelectedObjectName(System.Windows.Controls.ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is GameObjectSelectionOption selected
            ? selected.Name
            : fallback;
    }

    private static int ParseMinimumCount(string? text)
    {
        return int.TryParse(text?.Trim(), out var parsed) && parsed > 0
            ? parsed
            : 1;
    }

    private void HydrateParticipantDisplayNames(IEnumerable<LockKeyRequirement> participants)
    {
        var namesById = AvailableKeyOptions
            .Where(option => option.Id != Guid.Empty)
            .GroupBy(option => option.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        foreach (var participant in participants)
        {
            if (namesById.TryGetValue(participant.RequiredObjectId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                participant.RequiredObjectName = name;
            }
        }
    }

    private IReadOnlyList<string> ResolveVariableNamesForParticipant(LockKeyRequirement participant)
    {
        var option = AvailableKeyOptions.FirstOrDefault(item => item.Id == participant.RequiredObjectId);
        if (option is null)
        {
            return Array.Empty<string>();
        }

        return (option.VariableNames ?? Array.Empty<string>())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RequireKeyForLockOperationCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        RefreshLockSectionState();
    }

    private void UseUnlockKeysForLockOperationCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        RefreshLockSectionState();
    }

    private void RefreshLockSectionState()
    {
        var requireKey = RequireKeyForLockOperationCheckBox.IsChecked == true;
        var useUnlock = UseUnlockKeysForLockOperationCheckBox.IsChecked == true;
        var allowCustom = requireKey && !useUnlock;

        LockParticipantsListBox.IsEnabled = allowCustom;
        AddLockParticipantButton.IsEnabled = allowCustom;
        RemoveLockParticipantButton.IsEnabled = allowCustom;
        ClearLockParticipantsButton.IsEnabled = allowCustom;
        LockParticipantObjectComboBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        LockParticipantQuantityTextBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        LockParticipantMatchKindComboBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        LockParticipantMatchValueTextBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        LockParticipantSatisfactionComboBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        LockParticipantConsumptionComboBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        LockParticipantOptionalCheckBox.IsEnabled = allowCustom && SelectedLockParticipant is not null;
        EditLockParticipantVariablesButton.IsEnabled = allowCustom && SelectedLockParticipant is not null;
    }

    private static bool ShouldDefaultToUsingUnlockKeys(LockOperationRequirementsEditRequest initialValues)
    {
        var unlockRequirements = (initialValues.UnlockKeyRequirements ?? new List<LockKeyRequirement>())
            .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
            .ToList();
        var lockRequirements = (initialValues.LockKeyRequirements ?? new List<LockKeyRequirement>())
            .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
            .ToList();

        if (lockRequirements.Count == 0)
        {
            return true;
        }

        if (unlockRequirements.Count != lockRequirements.Count)
        {
            return false;
        }

        var left = unlockRequirements
            .Select(static requirement => requirement.ParticipantSummary)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();
        var right = lockRequirements
            .Select(static requirement => requirement.ParticipantSummary)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();

        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private bool ValidateParticipants(IReadOnlyList<LockKeyRequirement> participants, string sectionName)
    {
        if (participants.Count == 0)
        {
            return true;
        }

        if (participants.Any(participant => participant.RequiredObjectId == Guid.Empty || participant.RequiredQuantity < 1))
        {
            System.Windows.MessageBox.Show(this, $"{sectionName} participants must specify an object and quantity >= 1.", "Lock Requirements", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private void NumberTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void NumberTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (string.IsNullOrWhiteSpace(pastedText) || !pastedText.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateParticipants(_unlockParticipants, "Unlock"))
        {
            return;
        }

        var lockParticipants = ResolveLockParticipantsForSave();
        if (RequireKeyForLockOperationCheckBox.IsChecked == true && !ValidateParticipants(lockParticipants, "Lock"))
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

