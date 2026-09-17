using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class ImageVariantChooserScriptEditorDialog : Window
{
    private readonly IReadOnlyList<GamePropertyChoiceItem> _variableChoices;
    private readonly PropertyResolutionScope _variableScope;

    public ImageVariantChooserScriptEditorDialog(
        string scriptText,
        IReadOnlyList<string> referenceTokens,
        IReadOnlyList<GamePropertyChoiceItem>? variableChoices,
        PropertyResolutionScope variableScope,
        IReadOnlyList<string>? availableVariantNames)
    {
        InitializeComponent();

        _variableChoices = variableChoices ?? Array.Empty<GamePropertyChoiceItem>();
        _variableScope = variableScope;

        ScriptEditor.ScriptText = scriptText ?? string.Empty;
        ScriptEditor.ReferenceTokens = referenceTokens ?? Array.Empty<string>();
        ScriptEditor.ChooseVariableToken = OpenVariableChooser;

        VariantNamesListBox.ItemsSource = (availableVariantNames ?? Array.Empty<string>())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (VariantNamesListBox.Items.Count > 0)
        {
            VariantNamesListBox.SelectedIndex = 0;
        }
    }

    public string ScriptText => ScriptEditor.ScriptText ?? string.Empty;

    private string? OpenVariableChooser()
    {
        if (_variableChoices.Count == 0)
        {
            return null;
        }

        var dialog = new VariableChooserDialog(_variableChoices, _variableScope, "Choose Variable")
        {
            Owner = this
        };

        return dialog.ShowDialog() == true ? dialog.SelectedValue : null;
    }

    private void InsertVariantName_OnClick(object sender, RoutedEventArgs e)
    {
        InsertSelectedVariantName();
    }

    private void VariantNamesListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedVariantName();
    }

    private void VariantNamesListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedVariantName();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            ScriptEditor.Focus();
            e.Handled = true;
        }
    }

    private void InsertSelectedVariantName()
    {
        if (VariantNamesListBox.SelectedItem is not string selectedVariantName
            || string.IsNullOrWhiteSpace(selectedVariantName))
        {
            return;
        }

        var escapedName = selectedVariantName.Replace("\"", "\\\"");
        ScriptEditor.InsertTextAtCaret($"\"{escapedName}\"");
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
