using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class CountrySettingsDialog : Window
{
    private List<EnvironmentalEffectDefinition> _environmentalEffects;
    private readonly Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?>? _environmentalEffectComputationEditor;
    private readonly IReadOnlyList<SharedVariableChoiceOption> _environmentalEffectSharedVariableOptions;
    private readonly GamePropertyPickerContext? _environmentalEffectVariablePickerContext;
    public CountrySettingsDialog(CountrySettingsEditRequest initialValues, IReadOnlyList<string> startingAreaOptions)
    {
        InitializeComponent();
        _environmentalEffectComputationEditor = initialValues.EnvironmentalEffectComputationEditor;
        _environmentalEffectSharedVariableOptions = initialValues.EnvironmentalEffectSharedVariableOptions ?? Array.Empty<SharedVariableChoiceOption>();
        _environmentalEffectVariablePickerContext = initialValues.EnvironmentalEffectVariablePickerContext;
        CountryNameTextBox.Text = initialValues.Name;
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        StartingAreaComboBox.ItemsSource = startingAreaOptions;
        StartingAreaComboBox.SelectedItem = initialValues.StartingAreaName;
        _environmentalEffects = (initialValues.EnvironmentalEffects ?? new List<EnvironmentalEffectDefinition>()).ToList();
    }

    public CountrySettingsEditRequest Values => new(
        CountryNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        StartingAreaComboBox.SelectedItem as string ?? string.Empty,
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
        if (string.IsNullOrWhiteSpace(CountryNameTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter a country name.", "Country Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
