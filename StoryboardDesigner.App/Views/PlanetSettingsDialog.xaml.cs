using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class PlanetSettingsDialog : Window
{
    private List<EnvironmentalEffectDefinition> _environmentalEffects;
    private readonly Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationDto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationDto?>? _environmentalEffectComputationEditor;
    private readonly IReadOnlyList<SharedVariableChoiceOption> _environmentalEffectSharedVariableOptions;
    private readonly GamePropertyPickerContext? _environmentalEffectVariablePickerContext;
    public PlanetSettingsDialog(PlanetSettingsEditRequest initialValues, IReadOnlyList<string> startingCountryOptions)
    {
        InitializeComponent();
        _environmentalEffectComputationEditor = initialValues.EnvironmentalEffectComputationEditor;
        _environmentalEffectSharedVariableOptions = initialValues.EnvironmentalEffectSharedVariableOptions ?? Array.Empty<SharedVariableChoiceOption>();
        _environmentalEffectVariablePickerContext = initialValues.EnvironmentalEffectVariablePickerContext;
        PlanetNameTextBox.Text = initialValues.Name;
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        StartingCountryComboBox.ItemsSource = startingCountryOptions;
        StartingCountryComboBox.SelectedItem = initialValues.StartingCountryName;
        _environmentalEffects = (initialValues.EnvironmentalEffects ?? new List<EnvironmentalEffectDefinition>()).ToList();
    }

    public PlanetSettingsEditRequest Values => new(
        PlanetNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        StartingCountryComboBox.SelectedItem as string ?? string.Empty,
        _environmentalEffects.Select(CloneEnvironmentalEffect).ToList(), _environmentalEffectComputationEditor, _environmentalEffectSharedVariableOptions);

    private void ManageEnvironmentalEffects_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new EnvironmentalEffectsAuthoringDialog(_environmentalEffects, _environmentalEffectVariablePickerContext) { Owner = this };
        if (dialog.ShowDialog() == true) _environmentalEffects = dialog.Effects;
    }

    private static EnvironmentalEffectDefinition CloneEnvironmentalEffect(EnvironmentalEffectDefinition source) =>
        ParticleEffectControlSettingCloner.CloneEnvironmentalEffect(source);

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PlanetNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter a planet name.", "Planet Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
