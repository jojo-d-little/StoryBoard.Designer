using System.Windows;
using Storyboard.Shared.GameServices;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class TextOutputScriptEditorDialog : Window
{
    private sealed class AvailableVariableItem
    {
        public required string Token { get; init; }
        public required string Description { get; init; }
        public required string Example { get; init; }
    }

    private sealed class AvailableVariableGroup
    {
        public required string Category { get; init; }
        public required IReadOnlyList<AvailableVariableItem> Items { get; init; }
    }

    private bool _saveRequested;
    private readonly IReadOnlyList<string> _referenceTokens;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _variableChoices;
    private readonly PropertyResolutionScope _variableScope;

    public TextOutputScriptEditorDialog(
        string scriptText,
        IReadOnlyList<string> referenceTokens,
        IReadOnlyList<GamePropertyChoiceItem>? variableChoices = null,
        PropertyResolutionScope variableScope = PropertyResolutionScope.Room)
    {
        InitializeComponent();
        _referenceTokens = referenceTokens;
        _variableChoices = variableChoices ?? Array.Empty<GamePropertyChoiceItem>();
        _variableScope = variableScope;
        ScriptEditor.ScriptText = scriptText;
        ScriptEditor.ReferenceTokens = referenceTokens;
        ScriptEditor.ChooseVariableToken = OpenVariableChooser;
        AvailableVariableGroupsItemsControl.ItemsSource = BuildAvailableVariableGroups(referenceTokens);
        Loaded += TextOutputScriptEditorDialog_OnLoaded;
        Closing += TextOutputScriptEditorDialog_OnClosing;
    }

    public string ScriptText => ScriptEditor.ScriptText;

    private string? OpenVariableChooser()
    {
        if (_variableChoices.Count == 0)
        {
            System.Windows.MessageBox.Show(
                this,
                "No scoped variables are available for this action context. You can still type runtime tokens manually, such as {currentRoom.Name}.",
                "Choose Variable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        var dialog = new VariableChooserDialog(_variableChoices, _variableScope, "Choose Variable")
        {
            Owner = this
        };

        return dialog.ShowDialog() == true ? dialog.SelectedValue : null;
    }

    private static IReadOnlyList<AvailableVariableGroup> BuildAvailableVariableGroups(IEnumerable<string> referenceTokens)
    {
        static int GetCategoryOrder(string category)
        {
            return category switch
            {
                "Self" => 0,
                "Action" => 1,
                "Scoped" => 2,
                _ => 3
            };
        }

        return (referenceTokens ?? Array.Empty<string>())
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Select(static token => token.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(token =>
            {
                var metadata = EchoTokenMetadataProvider.GetMetadata(token);
                return new
                {
                    Token = token,
                    metadata.Category,
                    metadata.Description,
                    metadata.Example
                };
            })
            .GroupBy(item => item.Category)
            .OrderBy(group => GetCategoryOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AvailableVariableGroup
            {
                Category = group.Key,
                Items = group
                    .OrderBy(item => item.Token, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new AvailableVariableItem
                    {
                        Token = item.Token,
                        Description = item.Description,
                        Example = item.Example
                    })
                    .ToList()
            })
            .ToList();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateScriptOrShowErrors(showSuccessMessage: false))
        {
            return;
        }

        _saveRequested = true;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Validate_OnClick(object sender, RoutedEventArgs e)
    {
        ValidateScriptOrShowErrors(showSuccessMessage: true);
    }

    private void TextOutputScriptEditorDialog_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_saveRequested)
        {
            return;
        }

        var diagnostics = ActionScriptEditorDiagnosticsAnalyzer.Analyze(ScriptText ?? string.Empty, _referenceTokens);
        if (!diagnostics.HasBlockingErrors)
        {
            return;
        }

        var message = BuildValidationMessage(diagnostics.Errors, "Script has syntax issues:");
        var result = System.Windows.MessageBox.Show(
            this,
            message + Environment.NewLine + Environment.NewLine + "Close anyway?",
            "Script Syntax",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
        }
    }

    private bool ValidateScriptOrShowErrors(bool showSuccessMessage)
    {
        var diagnostics = ActionScriptEditorDiagnosticsAnalyzer.Analyze(ScriptText ?? string.Empty, _referenceTokens);
        if (!diagnostics.HasBlockingErrors)
        {
            if (diagnostics.Warnings.Count > 0)
            {
                var warningBody = BuildValidationMessage(diagnostics.Warnings, "Script warnings (non-blocking):");
                System.Windows.MessageBox.Show(
                    this,
                    warningBody,
                    "Script Diagnostics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            if (showSuccessMessage)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Script validation passed.",
                    "Script Syntax",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return true;
        }

        System.Windows.MessageBox.Show(
            this,
            BuildValidationMessage(diagnostics.Errors, "Script has syntax issues:"),
            "Script Syntax",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    private void TextOutputScriptEditorDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        ScriptEditor.FocusEditorAtEnd();
    }

    private static string BuildValidationMessage(IReadOnlyList<string> messages, string prefix)
    {
        var errorText = string.Join(Environment.NewLine, messages.Take(8));
        var suffix = messages.Count > 8 ? Environment.NewLine + "..." : string.Empty;
        return prefix + Environment.NewLine + errorText + suffix;
    }
}
