using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class CompositeObjectSettingsDialog : Window
{
    private readonly IReadOnlyList<GameObjectSelectionOption> _availableParts;
    private readonly IReadOnlyList<MaterializeSourceObjectChoiceItem> _objectChoices;
    private readonly ObservableCollection<CompositePartRequirement> _selectedParts;
    private bool _isRefreshing;

    public CompositeObjectSettingsDialog(CompositeObjectSettingsEditRequest initialValues)
    {
        InitializeComponent();

        _availableParts = (initialValues.AvailablePartOptions ?? Array.Empty<GameObjectSelectionOption>())
            .Where(option => option.Id != Guid.Empty)
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _objectChoices = _availableParts
            .Select(part => new MaterializeSourceObjectChoiceItem
            {
                ObjectId = part.Id,
                DisplayName = part.Name,
                ScopePath = "Project",
                SourceCategory = "Object"
            })
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.ObjectId)
            .ToList();

        var normalizedRequiredParts = NormalizeCompositeRequiredParts(initialValues.CompositeRequiredParts);
        var namesById = _availableParts
            .Where(option => option.Id != Guid.Empty)
            .GroupBy(option => option.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        foreach (var participant in normalizedRequiredParts)
        {
            if (namesById.TryGetValue(participant.PartObjectId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                participant.PartObjectName = name;
            }
        }

        _selectedParts = new ObservableCollection<CompositePartRequirement>(normalizedRequiredParts
            .OrderBy(item => item.PartObjectName, StringComparer.OrdinalIgnoreCase));

        SelectedPartsListBox.ItemsSource = _selectedParts;
        SelectedPartObjectComboBox.ItemsSource = _availableParts;
        SelectedPartMatchKindComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantMatchKind>();
        SelectedPartSatisfactionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantSatisfactionMode>();
        SelectedPartConsumptionComboBox.ItemsSource = Enum.GetValues<ProcedureParticipantConsumptionPolicy>();

        IsReversibleCheckBox.IsChecked = initialValues.IsCompositeReversible;

        if (_selectedParts.Count > 0)
        {
            SelectedPartsListBox.SelectedIndex = 0;
        }

        RefreshSelectedPartEditor();
    }

    public CompositeObjectSettingsEditRequest Values => new(
        IsReversibleCheckBox.IsChecked == true,
        "AllRequired",
        1,
        ExpandSelectedPartIds(),
        _availableParts.Select(part => new GameObjectSelectionOption(part.Id, part.Name)).ToList(),
        BuildCompositeRequiredParts());

    private List<Guid> ExpandSelectedPartIds()
    {
        return _selectedParts
            .SelectMany(item => Enumerable.Repeat(item.PartObjectId, Math.Max(1, item.RequiredQuantity)))
            .ToList();
    }

    private List<CompositePartRequirement> BuildCompositeRequiredParts()
    {
        return _selectedParts
            .Where(static item => item.PartObjectId != Guid.Empty)
            .Select(static item => new CompositePartRequirement
            {
                PartObjectId = item.PartObjectId,
                PartObjectName = item.PartObjectName,
                RequiredQuantity = Math.Max(1, item.RequiredQuantity),
                MatchKind = item.MatchKind,
                MatchValue = item.MatchValue,
                SatisfactionMode = item.SatisfactionMode,
                ConsumptionPolicy = item.ConsumptionPolicy,
                OptionalPart = item.OptionalPart,
                VariableRequirements = (item.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static entry => !string.IsNullOrWhiteSpace(entry.VariableName))
                    .Select(static entry => new LockParticipantVariableRequirement
                    {
                        VariableName = entry.VariableName.Trim(),
                        Operator = entry.Operator,
                        ExpectedValue = entry.ExpectedValue,
                        QuantityEvaluationMode = entry.QuantityEvaluationMode
                    })
                    .ToList()
            })
            .ToList();
    }

    private static List<CompositePartRequirement> NormalizeCompositeRequiredParts(
        IReadOnlyList<CompositePartRequirement>? parts)
    {
        return (parts ?? Array.Empty<CompositePartRequirement>())
            .Where(static part => part.PartObjectId != Guid.Empty)
            .Select(part =>
            {
                var matchValue = string.IsNullOrWhiteSpace(part.MatchValue)
                    ? part.PartObjectId.ToString("D")
                    : part.MatchValue;
                return new CompositePartRequirement
                {
                    PartObjectId = part.PartObjectId,
                    PartObjectName = part.PartObjectName,
                    RequiredQuantity = part.RequiredQuantity < 1 ? 1 : part.RequiredQuantity,
                    MatchKind = ProcedureParticipantMatchKind.ObjectId,
                    MatchValue = matchValue,
                    SatisfactionMode = part.SatisfactionMode,
                    ConsumptionPolicy = part.ConsumptionPolicy,
                    OptionalPart = part.OptionalPart,
                    VariableRequirements = (part.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static entry => !string.IsNullOrWhiteSpace(entry.VariableName))
                        .Select(static entry => new LockParticipantVariableRequirement
                        {
                            VariableName = entry.VariableName.Trim(),
                            Operator = entry.Operator,
                            ExpectedValue = entry.ExpectedValue,
                            QuantityEvaluationMode = entry.QuantityEvaluationMode
                        })
                        .ToList()
                };
            })
            .ToList();
    }

    private void AddRequiredPart_OnClick(object sender, RoutedEventArgs e)
    {
        if (_objectChoices.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No objects are available to choose from.", "Add Part", MessageBoxButton.OK, MessageBoxImage.Information);
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

        var selectedId = chooser.SelectedObjectId.Value;
        var selectedMatch = _availableParts.FirstOrDefault(item => item.Id == selectedId);
        var selectedName = selectedMatch?.Name
            ?? chooser.SelectedChoice?.DisplayName
            ?? selectedId.ToString("D");

        var existing = _selectedParts.FirstOrDefault(item => item.PartObjectId == selectedId);
        if (existing is not null)
        {
            existing.RequiredQuantity++;
            SelectedPartsListBox.Items.Refresh();
            SelectedPartsListBox.SelectedItem = existing;
            return;
        }

        var created = new CompositePartRequirement
        {
            PartObjectId = selectedId,
            PartObjectName = selectedName,
            RequiredQuantity = 1,
            MatchKind = ProcedureParticipantMatchKind.ObjectId,
            MatchValue = selectedId.ToString("D"),
            SatisfactionMode = ProcedureParticipantSatisfactionMode.PossessionRequired,
            ConsumptionPolicy = ProcedureParticipantConsumptionPolicy.None,
            OptionalPart = false
        };
        _selectedParts.Add(created);
        SelectedPartsListBox.SelectedItem = created;
    }

    private void RemoveRequiredPart_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedPartsListBox.SelectedItem is not CompositePartRequirement selected)
        {
            return;
        }

        var index = SelectedPartsListBox.SelectedIndex;
        _selectedParts.Remove(selected);
        if (_selectedParts.Count == 0)
        {
            RefreshSelectedPartEditor();
            return;
        }

        SelectedPartsListBox.SelectedIndex = Math.Min(Math.Max(index, 0), _selectedParts.Count - 1);
    }

    private void ClearRequiredParts_OnClick(object sender, RoutedEventArgs e)
    {
        _selectedParts.Clear();
        RefreshSelectedPartEditor();
    }

    private void SelectedPartsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshSelectedPartEditor();
    }

    private void PartField_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        if (SelectedPartsListBox.SelectedItem is not CompositePartRequirement selected)
        {
            return;
        }

        var selectedObject = SelectedPartObjectComboBox.SelectedItem as GameObjectSelectionOption;
        var nextId = selectedObject?.Id ?? selected.PartObjectId;
        var nextName = selectedObject?.Name ?? selected.PartObjectName;
        var nextQuantity = ParseQuantity(SelectedPartQuantityTextBox.Text);
        var nextSatisfaction = SelectedPartSatisfactionComboBox.SelectedItem is ProcedureParticipantSatisfactionMode satisfactionMode
            ? satisfactionMode
            : ProcedureParticipantSatisfactionMode.PossessionRequired;
        var nextConsumption = SelectedPartConsumptionComboBox.SelectedItem is ProcedureParticipantConsumptionPolicy consumptionPolicy
            ? consumptionPolicy
            : ProcedureParticipantConsumptionPolicy.None;
        var nextOptional = SelectedPartOptionalCheckBox.IsChecked == true;

        var duplicate = _selectedParts.FirstOrDefault(item => item.PartObjectId == nextId && !ReferenceEquals(item, selected));
        if (duplicate is not null)
        {
            duplicate.RequiredQuantity += nextQuantity;
            _selectedParts.Remove(selected);
            SelectedPartsListBox.Items.Refresh();
            SelectedPartsListBox.SelectedItem = duplicate;
            return;
        }

        selected.PartObjectId = nextId;
        selected.PartObjectName = nextName;
        selected.RequiredQuantity = nextQuantity;
        selected.MatchKind = ProcedureParticipantMatchKind.ObjectId;
        selected.MatchValue = nextId == Guid.Empty ? string.Empty : nextId.ToString("D");
        selected.SatisfactionMode = nextSatisfaction;
        selected.ConsumptionPolicy = nextConsumption;
        selected.OptionalPart = nextOptional;

        SelectedPartMatchKindComboBox.SelectedItem = ProcedureParticipantMatchKind.ObjectId;
        SelectedPartMatchValueTextBox.Text = selected.MatchValue;
        SelectedPartsListBox.Items.Refresh();
    }

    private CompositePartRequirement? SelectedParticipant => SelectedPartsListBox.SelectedItem as CompositePartRequirement;

    private void EditParticipantVariables_OnClick(object sender, RoutedEventArgs e)
    {
        var participant = SelectedParticipant;
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
        SelectedPartsListBox.Items.Refresh();
    }

    private IReadOnlyList<string> ResolveVariableNamesForParticipant(CompositePartRequirement participant)
    {
        var option = _availableParts.FirstOrDefault(item => item.Id == participant.PartObjectId);
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

    private static int ParseQuantity(string? text)
    {
        return int.TryParse(text?.Trim(), out var parsed) && parsed > 0
            ? parsed
            : 1;
    }

    private void RefreshSelectedPartEditor()
    {
        _isRefreshing = true;
        try
        {
            var selected = SelectedParticipant;
            var hasSelection = selected is not null;

            SelectedPartObjectComboBox.IsEnabled = hasSelection;
            SelectedPartQuantityTextBox.IsEnabled = hasSelection;
            SelectedPartMatchKindComboBox.IsEnabled = hasSelection;
            SelectedPartMatchValueTextBox.IsEnabled = hasSelection;
            SelectedPartSatisfactionComboBox.IsEnabled = hasSelection;
            SelectedPartConsumptionComboBox.IsEnabled = hasSelection;
            SelectedPartOptionalCheckBox.IsEnabled = hasSelection;
            EditParticipantVariablesButton.IsEnabled = hasSelection;

            if (!hasSelection)
            {
                SelectedPartObjectComboBox.SelectedItem = null;
                SelectedPartQuantityTextBox.Text = string.Empty;
                SelectedPartMatchKindComboBox.SelectedItem = null;
                SelectedPartMatchValueTextBox.Text = string.Empty;
                SelectedPartSatisfactionComboBox.SelectedItem = null;
                SelectedPartConsumptionComboBox.SelectedItem = null;
                SelectedPartOptionalCheckBox.IsChecked = false;
                return;
            }

            var selectedPart = selected!;
            var matched = _availableParts.FirstOrDefault(item => item.Id == selectedPart.PartObjectId);
            SelectedPartObjectComboBox.SelectedItem = matched;
            SelectedPartQuantityTextBox.Text = Math.Max(1, selectedPart.RequiredQuantity).ToString();
            SelectedPartMatchKindComboBox.SelectedItem = selectedPart.MatchKind;
            SelectedPartMatchValueTextBox.Text = selectedPart.MatchValue;
            SelectedPartSatisfactionComboBox.SelectedItem = selectedPart.SatisfactionMode;
            SelectedPartConsumptionComboBox.SelectedItem = selectedPart.ConsumptionPolicy;
            SelectedPartOptionalCheckBox.IsChecked = selectedPart.OptionalPart;
            SelectedPartMatchKindComboBox.SelectedItem = ProcedureParticipantMatchKind.ObjectId;
        }
        finally
        {
            _isRefreshing = false;
        }
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
        var selectedPartCount = _selectedParts.Sum(static item => Math.Max(1, item.RequiredQuantity));
        if (selectedPartCount == 0)
        {
            System.Windows.MessageBox.Show(this, "Select at least one required part.", "Composite Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            SelectedPartsListBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

}

