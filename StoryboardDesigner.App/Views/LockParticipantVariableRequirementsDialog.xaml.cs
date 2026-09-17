using System.Collections.ObjectModel;
using System.Windows;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class LockParticipantVariableRequirementsDialog : Window
{
    private readonly IReadOnlyList<string> _variableNames;
    private readonly ObservableCollection<LockParticipantVariableRequirement> _requirements;

    public LockParticipantVariableRequirementsDialog(
        IReadOnlyList<string> variableNames,
        IReadOnlyList<LockParticipantVariableRequirement> initialRequirements)
    {
        InitializeComponent();

        _variableNames = (variableNames ?? Array.Empty<string>())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _requirements = new ObservableCollection<LockParticipantVariableRequirement>(
            (initialRequirements ?? Array.Empty<LockParticipantVariableRequirement>())
            .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
            .Select(static requirement => new LockParticipantVariableRequirement
            {
                VariableName = requirement.VariableName.Trim(),
                Operator = requirement.Operator,
                ExpectedValue = requirement.ExpectedValue,
                QuantityEvaluationMode = requirement.QuantityEvaluationMode
            }));

        VariableNameComboBox.ItemsSource = _variableNames;
        if (_variableNames.Count > 0)
        {
            VariableNameComboBox.SelectedIndex = 0;
        }

        VariableNameColumn.ItemsSource = _variableNames;
        OperatorColumn.ItemsSource = Enum.GetValues<RuntimeVariableComparisonOperator>();
        QuantityModeColumn.ItemsSource = Enum.GetValues<RuntimeVariableQuantityEvaluationMode>();

        RequirementsDataGrid.ItemsSource = _requirements;
    }

    public IReadOnlyList<LockParticipantVariableRequirement> Values => _requirements
        .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
        .Select(static requirement => new LockParticipantVariableRequirement
        {
            VariableName = requirement.VariableName.Trim(),
            Operator = requirement.Operator,
            ExpectedValue = requirement.ExpectedValue,
            QuantityEvaluationMode = requirement.QuantityEvaluationMode
        })
        .ToList();

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        var variableName = (VariableNameComboBox.SelectedItem as string)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        _requirements.Add(new LockParticipantVariableRequirement
        {
            VariableName = variableName,
            Operator = RuntimeVariableComparisonOperator.Equals,
            ExpectedValue = string.Empty,
            QuantityEvaluationMode = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass
        });
    }

    private void RemoveSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (RequirementsDataGrid.SelectedItem is not LockParticipantVariableRequirement selected)
        {
            return;
        }

        _requirements.Remove(selected);
    }

    private void ClearAll_OnClick(object sender, RoutedEventArgs e)
    {
        _requirements.Clear();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
