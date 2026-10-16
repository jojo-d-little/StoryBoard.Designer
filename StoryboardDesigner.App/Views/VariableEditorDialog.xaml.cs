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
        Func<ProjectGamePropertyComputationDto?, ProjectGamePropertyComputationDto?>? editComputation = null)
    {
        InitializeComponent();
        _manageSharedVariablesAction = manageSharedVariablesAction;
        _computation = computation;
        _editComputation = editComputation;
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
    }

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
        ComputationPreviewTextBlock.Text = _computation?.Condition is null
            ? "Computation: not defined"
            : "Computation: " + BuildPreview(_computation.Condition);
    }

    private static string BuildPreview(ProjectGamePropertyConditionDto condition) => condition.Kind switch
    {
        "All" => "(" + string.Join(" AND ", (condition.Conditions ?? new()).Select(BuildPreview)) + ")",
        "Any" => "(" + string.Join(" OR ", (condition.Conditions ?? new()).Select(BuildPreview)) + ")",
        "Not" => "NOT (" + (condition.Condition is null ? "<missing>" : BuildPreview(condition.Condition)) + ")",
        "Exists" => "exists(" + FormatSource(condition.GamePropertySource) + ")",
        "Compare" => FormatSource(condition.GamePropertySource) + " " + (condition.Operator ?? "?") + (condition.RightOperand is null ? string.Empty : " " + (condition.RightOperand.GamePropertySource?.PropertyName ?? condition.RightOperand.Value ?? string.Empty)),
        _ => "<unknown condition>"
    };

    private static string FormatSource(ProjectGamePropertySourceReferenceDto? source) => source is null
        ? "<choose property>"
        : (source.AnchorKey is not null ? source.AnchorKey + "." : source.OwnerId.HasValue ? $"owner[{source.OwnerId.Value:N}]." : "self.") + (source.PropertyName ?? "<missing>");

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
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
