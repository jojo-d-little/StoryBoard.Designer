using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class VariableEditorDialog : Window
{
    private readonly HashSet<string> _disallowedNames;
    private readonly Action<Window>? _manageSharedVariablesAction;

    public VariableEditorDialog(
        string name,
        GamePropertyLifetime lifetime,
        string defaultValue,
        GamePropertyValueRestriction valueRestriction,
        IEnumerable<string>? disallowedNames = null,
        Action<Window>? manageSharedVariablesAction = null)
    {
        InitializeComponent();
        _manageSharedVariablesAction = manageSharedVariablesAction;
        _disallowedNames = disallowedNames is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(disallowedNames.Where(static n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
        VariableNameTextBox.Text = name;
        LifetimeComboBox.SelectedItem = lifetime;
        DefaultValueTextBox.Text = defaultValue;
        ValueRestrictionComboBox.SelectedValue = valueRestriction;
        ManageSharedVariablesButton.Visibility = _manageSharedVariablesAction is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public string VariableName => VariableNameTextBox.Text.Trim();
    public string DefaultValue => DefaultValueTextBox.Text;
    public GamePropertyValueRestriction ValueRestriction => ValueRestrictionComboBox.SelectedValue is GamePropertyValueRestriction restriction ? restriction : GamePropertyValueRestriction.Unrestricted;
    public GamePropertyLifetime Lifetime => LifetimeComboBox.SelectedItem is GamePropertyLifetime lifetime ? lifetime : GamePropertyLifetime.Singleton;

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
