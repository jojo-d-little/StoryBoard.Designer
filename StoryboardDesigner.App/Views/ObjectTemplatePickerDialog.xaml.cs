using System.Windows;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class ObjectTemplatePickerDialog : Window
{
    private sealed class Option
    {
        public GameObject? Template { get; init; }
        public string Label { get; init; } = string.Empty;
    }

    public ObjectTemplatePickerDialog(IReadOnlyList<GameObject> templates)
    {
        InitializeComponent();

        var options = new List<Option>
        {
            new()
            {
                Template = null,
                Label = "Blank Object"
            }
        };

        options.AddRange(templates
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .Select(template => new Option
            {
                Template = template,
                Label = template.Name
            }));

        TemplatesListBox.ItemsSource = options;
        TemplatesListBox.SelectedIndex = 0;
    }

    public GameObject? SelectedTemplate { get; private set; }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (TemplatesListBox.SelectedItem is not Option option)
        {
            System.Windows.MessageBox.Show(this, "Choose a template option.", "Object Template", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedTemplate = option.Template;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TemplatesListBox_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Select_OnClick(sender, e);
    }
}
