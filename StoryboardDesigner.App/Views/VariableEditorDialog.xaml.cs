using System.Windows;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class VariableEditorDialog : Window
{
    private readonly HashSet<string> _disallowedNames;
    private readonly Action<Window>? _manageSharedVariablesAction;
    private readonly Func<ProjectGamePropertyComputationDto?, ProjectGamePropertyComputationDto?>? _editComputation;
    private ProjectGamePropertyComputationDto? _computation;

    public VariableEditorDialog(
        string name,
        GamePropertyLifetime lifetime,
        string defaultValue,
        GamePropertyValueRestriction valueRestriction,
        IEnumerable<string>? disallowedNames = null,
        Action<Window>? manageSharedVariablesAction = null,
        ProjectGamePropertyComputationDto? computation = null,
        Func<ProjectGamePropertyComputationDto?, ProjectGamePropertyComputationDto?>? editComputation = null,
        bool readOnly = false)
    {
        InitializeComponent();
        _manageSharedVariablesAction = manageSharedVariablesAction;
        _computation = computation;
        _editComputation = editComputation;
        IsReadOnly = readOnly;
        _disallowedNames = disallowedNames is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(disallowedNames.Where(static n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
        VariableNameTextBox.Text = name;
        LifetimeComboBox.SelectedItem = lifetime;
        DefaultValueTextBox.Text = defaultValue;
        ValueRestrictionComboBox.SelectedValue = valueRestriction;
        ComputedCheckBox.IsChecked = computation is not null;
        EditComputationButton.Visibility = ComputedCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateComputationPreview();
        ManageSharedVariablesButton.Visibility = _manageSharedVariablesAction is null ? Visibility.Collapsed : Visibility.Visible;
        if (IsReadOnly)
        {
            Title = "Game Property (Read-only)";
            VariableNameTextBox.IsEnabled = false;
            ComputedCheckBox.IsEnabled = false;
            EditComputationButton.IsEnabled = false;
            LifetimeComboBox.IsEnabled = false;
            DefaultValueTextBox.IsEnabled = false;
            ValueRestrictionComboBox.IsEnabled = false;
            SaveButton.Content = "Close";
        }
    }

    public bool IsReadOnly { get; }
    public string VariableName => VariableNameTextBox.Text.Trim();
    public string DefaultValue => DefaultValueTextBox.Text;
    public GamePropertyValueRestriction ValueRestriction => ValueRestrictionComboBox.SelectedValue is GamePropertyValueRestriction restriction ? restriction : GamePropertyValueRestriction.Unrestricted;
    public GamePropertyLifetime Lifetime => LifetimeComboBox.SelectedItem is GamePropertyLifetime lifetime ? lifetime : GamePropertyLifetime.Singleton;
    public bool IsComputed => ComputedCheckBox.IsChecked == true;
    public ProjectGamePropertyComputationDto? Computation => IsComputed ? _computation : null;

    private void Computed_OnChanged(object sender, RoutedEventArgs e)
    {
        var enabled = ComputedCheckBox.IsChecked == true;
        EditComputationButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ComputationPreviewTextBlock.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (enabled)
        {
            DefaultValueTextBox.Text = "false";
            ValueRestrictionComboBox.SelectedValue = GamePropertyValueRestriction.TrueFalse;
        }
    }

    private void EditComputation_OnClick(object sender, RoutedEventArgs e)
    {
        if (_editComputation is null)
        {
            System.Windows.MessageBox.Show(this, "A condition editor is not available for this game property.", "Computed Game Property", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var updated = _editComputation(_computation);
        if (updated is not null)
        {
            _computation = updated;
            UpdateComputationPreview();
        }
    }

    private void UpdateComputationPreview()
    {
        ComputationPreviewTextBlock.Visibility = IsComputed ? Visibility.Visible : Visibility.Collapsed;
        ComputationPreviewTextBlock.Text = "Computation: " + GamePropertyComputationFormatter.Format(_computation?.Condition);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly)
        {
            DialogResult = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(VariableName))
        {
            System.Windows.MessageBox.Show(this, "Please enter a game property name.", "Game Property", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_disallowedNames.Contains(VariableName))
        {
            System.Windows.MessageBox.Show(this, "Game property names must be unique within this scope.", "Game Property", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsComputed && _computation is null)
        {
            System.Windows.MessageBox.Show(this, "Edit the condition before saving a computed game property.", "Computed Game Property", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsComputed)
        {
            DefaultValueTextBox.Text = "false";
            ValueRestrictionComboBox.SelectedValue = GamePropertyValueRestriction.TrueFalse;
        }

        if (!VariableValueRestrictionValidator.IsAllowed(ValueRestriction, DefaultValue))
        {
            System.Windows.MessageBox.Show(this, VariableValueRestrictionValidator.BuildInvalidValueMessage(ValueRestriction, "Default value"), "Game Property", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ManageSharedVariables_OnClick(object sender, RoutedEventArgs e)
    {
        _manageSharedVariablesAction?.Invoke(this);
    }
}
