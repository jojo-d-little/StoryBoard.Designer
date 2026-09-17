using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class PlanetSettingsDialog : Window
{
    public PlanetSettingsDialog(PlanetSettingsEditRequest initialValues, IReadOnlyList<string> startingCountryOptions)
    {
        InitializeComponent();
        PlanetNameTextBox.Text = initialValues.Name;
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        StartingCountryComboBox.ItemsSource = startingCountryOptions;
        StartingCountryComboBox.SelectedItem = initialValues.StartingCountryName;
    }

    public PlanetSettingsEditRequest Values => new(
        PlanetNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        StartingCountryComboBox.SelectedItem as string ?? string.Empty);

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
