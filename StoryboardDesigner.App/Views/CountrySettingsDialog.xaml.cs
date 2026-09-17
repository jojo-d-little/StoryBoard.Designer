using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class CountrySettingsDialog : Window
{
    public CountrySettingsDialog(CountrySettingsEditRequest initialValues, IReadOnlyList<string> startingAreaOptions)
    {
        InitializeComponent();
        CountryNameTextBox.Text = initialValues.Name;
        ProducerNotesTextBox.Text = initialValues.ProducerNotes;
        StartingAreaComboBox.ItemsSource = startingAreaOptions;
        StartingAreaComboBox.SelectedItem = initialValues.StartingAreaName;
    }

    public CountrySettingsEditRequest Values => new(
        CountryNameTextBox.Text.Trim(),
        ProducerNotesTextBox.Text,
        StartingAreaComboBox.SelectedItem as string ?? string.Empty);

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
