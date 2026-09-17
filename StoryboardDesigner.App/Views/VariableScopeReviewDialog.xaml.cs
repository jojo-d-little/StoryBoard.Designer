using System.Windows;
using System.Windows.Controls;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class VariableScopeReviewDialog : Window
{
    private readonly Dictionary<Guid, string> _originalNames = new();
    private readonly IList<GamePropertyDefinition> _variables;

    public VariableScopeReviewDialog(string scopeLabel, IList<GamePropertyDefinition> variables)
    {
        InitializeComponent();
        _variables = variables;
        ScopeLabelTextBlock.Text = $"Scope: {scopeLabel}";
        RestrictionColumn.ItemsSource = GamePropertyValueRestrictionOptions.All;
        VariablesDataGrid.ItemsSource = _variables;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void AddGameProperty_OnClick(object sender, RoutedEventArgs e)
    {
        var disallowedNames = _variables
            .Select(static variable => variable.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var dialog = new VariableEditorDialog(string.Empty, GamePropertyLifetime.Singleton, "false", GamePropertyValueRestriction.TrueFalse, disallowedNames)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var variable = new GamePropertyDefinition
        {
            Name = dialog.VariableName,
            Lifetime = dialog.Lifetime,
            DefaultValue = dialog.DefaultValue,
            ValueRestriction = dialog.ValueRestriction
        };

        _variables.Add(variable);
        RefreshVariablesGrid(variable);
    }

    private void RemoveSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (VariablesDataGrid.SelectedItem is not GamePropertyDefinition variable)
        {
            return;
        }

        _variables.Remove(variable);
        RefreshVariablesGrid();
    }

    private void VariablesDataGrid_OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (!ReferenceEquals(e.Column, VariableNameColumn) || e.Row.Item is not GamePropertyDefinition variable)
        {
            return;
        }

        _originalNames[variable.Id] = variable.Name;
    }

    private void VariablesDataGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not GamePropertyDefinition variable)
        {
            return;
        }

        if (ReferenceEquals(e.Column, VariableNameColumn))
        {
            if (e.EditingElement is not System.Windows.Controls.TextBox textBox)
            {
                return;
            }

            var candidateName = textBox.Text.Trim();
            var originalName = _originalNames.TryGetValue(variable.Id, out var saved) ? saved : variable.Name;

            if (string.IsNullOrWhiteSpace(candidateName))
            {
                System.Windows.MessageBox.Show(this, "Game property name cannot be blank.", "Review Game Properties", MessageBoxButton.OK, MessageBoxImage.Information);
                variable.Name = originalName;
                e.Cancel = true;
                return;
            }

            var duplicates = VariablesDataGrid.Items
                .OfType<GamePropertyDefinition>()
                .Any(other => !ReferenceEquals(other, variable)
                              && string.Equals(other.Name, candidateName, StringComparison.OrdinalIgnoreCase));

            if (duplicates)
            {
                System.Windows.MessageBox.Show(this, "Game property names must be unique within this scope.", "Review Game Properties", MessageBoxButton.OK, MessageBoxImage.Information);
                variable.Name = originalName;
                e.Cancel = true;
            }

            return;
        }

        if (ReferenceEquals(e.Column, DefaultValueColumn))
        {
            if (e.EditingElement is not System.Windows.Controls.TextBox textBox)
            {
                return;
            }

            var candidateValue = textBox.Text.Trim();
            if (!VariableValueRestrictionValidator.IsAllowed(variable.ValueRestriction, candidateValue))
            {
                System.Windows.MessageBox.Show(
                    this,
                    VariableValueRestrictionValidator.BuildInvalidValueMessage(variable.ValueRestriction, "Default value"),
                    "Review Game Properties",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                e.Cancel = true;
            }

            return;
        }

        if (ReferenceEquals(e.Column, RestrictionColumn))
        {
            if (e.EditingElement is not System.Windows.Controls.ComboBox comboBox || comboBox.SelectedValue is not GamePropertyValueRestriction selectedRestriction)
            {
                return;
            }

            if (!VariableValueRestrictionValidator.IsAllowed(selectedRestriction, variable.DefaultValue))
            {
                System.Windows.MessageBox.Show(
                    this,
                    VariableValueRestrictionValidator.BuildInvalidValueMessage(selectedRestriction, "Default value") + " Update the default value first.",
                    "Review Game Properties",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                e.Cancel = true;
            }
        }
    }

    private void RefreshVariablesGrid(GamePropertyDefinition? selected = null)
    {
        VariablesDataGrid.ItemsSource = null;
        VariablesDataGrid.ItemsSource = _variables;

        if (selected is not null)
        {
            VariablesDataGrid.SelectedItem = selected;
            VariablesDataGrid.ScrollIntoView(selected);
        }
    }
}
