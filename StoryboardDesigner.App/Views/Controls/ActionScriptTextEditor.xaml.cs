using System.Collections;
using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views.Controls;

public partial class ActionScriptTextEditor : System.Windows.Controls.UserControl
{
    private sealed class QuickReferenceSuggestionItem
    {
        public required string Token { get; init; }
        public required string DisplayText { get; init; }
        public required string CategoryLabel { get; init; }
        public required string Description { get; init; }
        public required string Example { get; init; }
        public bool IsChooserEntry { get; init; }
    }

    private const string ChooseVariableEntry = "Choose variable...";
    private static readonly IReadOnlyList<string> DefaultBuiltInFunctions =
    [
        "@ALL(condition1, condition2)",
        "@ANY(condition1, condition2)",
        "@NOT(condition)",
        "@hasItem(\"itemName\")"
    ];

    private int? _variableCompletionStart;
    private int? _functionCompletionStart;
    private int? _returnCompletionStart;
    private static readonly IReadOnlyList<string> ReturnStatementSuggestions =
    [
        "RETURN SUCCESS",
        "RETURN SUCCESS BUBBLE",
        "RETURN FAILURE",
        "RETURN FAILURE BUBBLE"
    ];

    public ActionScriptTextEditor()
    {
        InitializeComponent();
        RefreshVariableReferenceSource();
        RefreshBuiltInFunctionSource();
    }

    public static readonly DependencyProperty ScriptTextProperty = DependencyProperty.Register(
        nameof(ScriptText),
        typeof(string),
        typeof(ActionScriptTextEditor),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ReferenceTokensProperty = DependencyProperty.Register(
        nameof(ReferenceTokens),
        typeof(IEnumerable<string>),
        typeof(ActionScriptTextEditor),
        new PropertyMetadata(null, OnReferenceTokensChanged));

    public static readonly DependencyProperty BuiltInFunctionsProperty = DependencyProperty.Register(
        nameof(BuiltInFunctions),
        typeof(IEnumerable<string>),
        typeof(ActionScriptTextEditor),
        new PropertyMetadata(null, OnBuiltInFunctionsChanged));

    public string ScriptText
    {
        get => (string)GetValue(ScriptTextProperty);
        set => SetValue(ScriptTextProperty, value);
    }

    public IEnumerable<string>? ReferenceTokens
    {
        get => (IEnumerable<string>?)GetValue(ReferenceTokensProperty);
        set => SetValue(ReferenceTokensProperty, value);
    }

    public IEnumerable<string>? BuiltInFunctions
    {
        get => (IEnumerable<string>?)GetValue(BuiltInFunctionsProperty);
        set => SetValue(BuiltInFunctionsProperty, value);
    }

    public Func<string?>? ChooseVariableToken { get; set; }

    public void InsertTextAtCaret(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        CancelVariableCompletion();
        CancelFunctionCompletion();
        CancelReturnCompletion();

        var caret = EditorTextBox.CaretIndex;
        var clampedCaret = Math.Clamp(caret, 0, EditorTextBox.Text.Length);
        EditorTextBox.Text = EditorTextBox.Text.Insert(clampedCaret, text);
        EditorTextBox.CaretIndex = clampedCaret + text.Length;
        EditorTextBox.Focus();
    }

    public void FocusEditorAtEnd()
    {
        EditorTextBox.Focus();
        var end = EditorTextBox.Text?.Length ?? 0;
        EditorTextBox.CaretIndex = end;
        EditorTextBox.Select(end, 0);
        Keyboard.Focus(EditorTextBox);
    }

    private static void OnReferenceTokensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ActionScriptTextEditor editor)
        {
            editor.RefreshVariableReferenceSource();
        }
    }

    private static void OnBuiltInFunctionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ActionScriptTextEditor editor)
        {
            editor.RefreshBuiltInFunctionSource();
        }
    }

    private void RefreshVariableReferenceSource()
    {
        VariableReferenceListBox.ItemsSource = BuildQuickReferenceSuggestions(string.Empty);
        RefreshVariableReferenceHint();
    }

    private void RefreshBuiltInFunctionSource()
    {
        FunctionListBox.ItemsSource = GetBuiltInFunctions().ToList();
    }

    private IReadOnlyList<string> GetReferenceTokens()
    {
        var source = ReferenceTokens ?? Array.Empty<string>();
        return source
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static token => token.StartsWith("self.", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static token => token)
            .ToList();
    }

    private IReadOnlyList<string> GetBuiltInFunctions()
    {
        var source = BuiltInFunctions ?? DefaultBuiltInFunctions;
        return source
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static token => token)
            .ToList();
    }

    private void EditorTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == "{")
        {
            e.Handled = StartVariableCompletion();
            return;
        }

        if (e.Text == "@")
        {
            e.Handled = StartFunctionCompletion();
        }
    }

    private void EditorTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_variableCompletionStart is not null)
        {
            if (e.Key == Key.Tab)
            {
                InsertTopVariableReference();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelVariableCompletion();
                e.Handled = true;
                return;
            }
        }

        if (_functionCompletionStart is not null)
        {
            if (e.Key == Key.Tab)
            {
                InsertTopFunction();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelFunctionCompletion();
                e.Handled = true;
            }
        }

        if (_returnCompletionStart is not null)
        {
            if (e.Key is Key.Tab or Key.Enter or Key.Return)
            {
                InsertTopReturnStatement();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelReturnCompletion();
                e.Handled = true;
            }
        }
    }

    private void EditorTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_variableCompletionStart is not null)
        {
            RefreshVariableCompletionFilter();
        }

        if (_functionCompletionStart is not null)
        {
            RefreshFunctionCompletionFilter();
        }

        if (_variableCompletionStart is null && _functionCompletionStart is null)
        {
            RefreshReturnCompletionFilter();
        }
        else
        {
            CancelReturnCompletion();
        }
    }

    private void VariableReferenceListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedVariableReference();
    }

    private void VariableReferenceListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedVariableReference();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelVariableCompletion();
            EditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void FunctionListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedFunction();
    }

    private void FunctionListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedFunction();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelFunctionCompletion();
            EditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private void ReturnListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedReturnStatement();
    }

    private void ReturnListBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            InsertSelectedReturnStatement();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelReturnCompletion();
            EditorTextBox.Focus();
            e.Handled = true;
        }
    }

    private bool StartVariableCompletion()
    {
        CancelFunctionCompletion();
        CancelReturnCompletion();

        var tokens = GetReferenceTokens();
        if (tokens.Count == 0 && ChooseVariableToken is null)
        {
            return false;
        }

        var start = EditorTextBox.CaretIndex;
        EditorTextBox.Text = EditorTextBox.Text.Insert(start, "{");
        EditorTextBox.CaretIndex = start + 1;
        _variableCompletionStart = EditorTextBox.CaretIndex;
        RefreshVariableCompletionFilter();
        return true;
    }

    private bool StartFunctionCompletion()
    {
        CancelVariableCompletion();
        CancelReturnCompletion();

        var functions = GetBuiltInFunctions();
        if (functions.Count == 0)
        {
            return false;
        }

        var start = EditorTextBox.CaretIndex;
        EditorTextBox.Text = EditorTextBox.Text.Insert(start, "@");
        EditorTextBox.CaretIndex = start + 1;
        _functionCompletionStart = start;
        RefreshFunctionCompletionFilter();
        return true;
    }

    private void RefreshVariableCompletionFilter()
    {
        if (_variableCompletionStart is null)
        {
            return;
        }

        var prefix = GetBraceCompletionPrefix(_variableCompletionStart.Value);
        if (prefix is null)
        {
            CancelVariableCompletion();
            return;
        }

        var filtered = BuildQuickReferenceSuggestions(prefix);

        VariableReferenceListBox.ItemsSource = filtered;
        VariableReferenceListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        VariableReferencePopup.IsOpen = filtered.Count > 0;
        RefreshVariableReferenceHint();
    }

    private void VariableReferenceListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshVariableReferenceHint();
    }

    private void RefreshVariableReferenceHint()
    {
        var selected = VariableReferenceListBox.SelectedItem as QuickReferenceSuggestionItem;
        VariableReferenceHintTextBlock.Text = selected is null
            ? string.Empty
            : $"{selected.Description}{Environment.NewLine}{selected.Example}";
    }

    private void RefreshFunctionCompletionFilter()
    {
        if (_functionCompletionStart is null)
        {
            return;
        }

        var prefix = GetFunctionCompletionPrefix(_functionCompletionStart.Value);
        if (prefix is null)
        {
            CancelFunctionCompletion();
            return;
        }

        var filtered = GetBuiltInFunctions()
            .Where(token => token.StartsWith("@" + prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        FunctionListBox.ItemsSource = filtered;
        FunctionListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        FunctionPopup.IsOpen = filtered.Count > 0;
    }

    private void RefreshReturnCompletionFilter()
    {
        var context = GetReturnCompletionContext();
        if (context is null)
        {
            CancelReturnCompletion();
            return;
        }

        _returnCompletionStart = context.Value.CompletionStart;

        var normalizedInput = context.Value.CurrentText.TrimEnd();
        var filtered = ReturnStatementSuggestions
            .Where(suggestion => suggestion.StartsWith(normalizedInput, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ReturnListBox.ItemsSource = filtered;
        ReturnListBox.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        ReturnPopup.IsOpen = filtered.Count > 0;

        if (filtered.Count == 0)
        {
            CancelReturnCompletion();
        }
    }

    private void InsertSelectedVariableReference()
    {
        if (VariableReferenceListBox.SelectedItem is not QuickReferenceSuggestionItem item)
        {
            return;
        }

        if (item.IsChooserEntry)
        {
            OpenVariableChooserAndInsertToken();
            return;
        }

        InsertVariableReferenceToken(item.Token);
    }

    private void InsertTopVariableReference()
    {
        var item = VariableReferenceListBox.Items.OfType<QuickReferenceSuggestionItem>().FirstOrDefault();
        if (item is null)
        {
            return;
        }

        if (item.IsChooserEntry)
        {
            OpenVariableChooserAndInsertToken();
            return;
        }

        InsertVariableReferenceToken(item.Token);
    }

    private void OpenVariableChooserAndInsertToken()
    {
        var selectedToken = ChooseVariableToken?.Invoke();
        if (string.IsNullOrWhiteSpace(selectedToken))
        {
            EditorTextBox.Focus();
            return;
        }

        InsertVariableReferenceToken(selectedToken);
    }

    private IReadOnlyList<QuickReferenceSuggestionItem> BuildQuickReferenceSuggestions(string prefix)
    {
        var quickTokens = EchoQuickReferenceTokenFilter.FilterQuickBraceTokens(GetReferenceTokens());
        var filtered = (string.IsNullOrEmpty(prefix)
            ? quickTokens
            : quickTokens.Where(token => token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList());

        var items = filtered
            .Select(token =>
            {
                var metadata = EchoTokenMetadataProvider.GetMetadata(token);
                return new QuickReferenceSuggestionItem
                {
                    Token = token,
                    DisplayText = token,
                    CategoryLabel = metadata.Category,
                    Description = metadata.Description,
                    Example = metadata.Example,
                    IsChooserEntry = false
                };
            })
            .ToList();

        var chooserMetadata = EchoTokenMetadataProvider.GetMetadata(ChooseVariableEntry);
        items.Insert(0, new QuickReferenceSuggestionItem
        {
            Token = ChooseVariableEntry,
            DisplayText = ChooseVariableEntry,
            CategoryLabel = chooserMetadata.Category,
            Description = chooserMetadata.Description,
            Example = chooserMetadata.Example,
            IsChooserEntry = true
        });

        return items;
    }

    private void InsertVariableReferenceToken(string token)
    {
        if (_variableCompletionStart is null)
        {
            return;
        }

        ReplaceBraceCompletionText(_variableCompletionStart.Value, token);
        CancelVariableCompletion();
        EditorTextBox.Focus();
    }

    private void InsertSelectedFunction()
    {
        if (FunctionListBox.SelectedItem is not string token)
        {
            return;
        }

        InsertFunctionToken(token);
    }

    private void InsertTopFunction()
    {
        var token = FunctionListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        InsertFunctionToken(token);
    }

    private void InsertFunctionToken(string token)
    {
        if (_functionCompletionStart is null)
        {
            return;
        }

        ReplaceFunctionCompletionText(_functionCompletionStart.Value, token);
        CancelFunctionCompletion();
        EditorTextBox.Focus();
    }

    private void InsertSelectedReturnStatement()
    {
        if (ReturnListBox.SelectedItem is not string token)
        {
            return;
        }

        InsertReturnStatementToken(token);
    }

    private void InsertTopReturnStatement()
    {
        var token = ReturnListBox.Items.OfType<string>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        InsertReturnStatementToken(token);
    }

    private void InsertReturnStatementToken(string token)
    {
        if (_returnCompletionStart is null)
        {
            return;
        }

        ReplaceReturnCompletionText(_returnCompletionStart.Value, token);
        CancelReturnCompletion();
        EditorTextBox.Focus();
    }

    private string? GetBraceCompletionPrefix(int completionStart)
    {
        if (completionStart < 0 || completionStart > EditorTextBox.Text.Length)
        {
            return null;
        }

        var caret = EditorTextBox.CaretIndex;
        if (caret < completionStart || caret > EditorTextBox.Text.Length)
        {
            return null;
        }

        var prefix = EditorTextBox.Text.Substring(completionStart, caret - completionStart);
        if (prefix.IndexOf('}') >= 0 || prefix.IndexOf('{') >= 0 || prefix.Any(char.IsWhiteSpace))
        {
            return null;
        }

        return prefix;
    }

    private string? GetFunctionCompletionPrefix(int completionStart)
    {
        if (completionStart < 0 || completionStart >= EditorTextBox.Text.Length)
        {
            return null;
        }

        if (EditorTextBox.Text[completionStart] != '@')
        {
            return null;
        }

        var caret = EditorTextBox.CaretIndex;
        if (caret <= completionStart || caret > EditorTextBox.Text.Length)
        {
            return null;
        }

        var prefix = EditorTextBox.Text.Substring(completionStart + 1, caret - completionStart - 1);
        if (prefix.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
        {
            return null;
        }

        return prefix;
    }

    private (int CompletionStart, string CurrentText)? GetReturnCompletionContext()
    {
        var caret = EditorTextBox.CaretIndex;
        var text = EditorTextBox.Text;
        if (caret < 0 || caret > text.Length)
        {
            return null;
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var linePrefix = text.Substring(lineStart, caret - lineStart);
        var leadingWhitespaceCount = linePrefix.TakeWhile(char.IsWhiteSpace).Count();
        var trimmedPrefix = linePrefix.Substring(leadingWhitespaceCount);
        if (trimmedPrefix.Length == 0)
        {
            return null;
        }

        if (trimmedPrefix.Any(c => !(char.IsLetter(c) || char.IsWhiteSpace(c))))
        {
            return null;
        }

        if (!"RETURN".StartsWith(trimmedPrefix, StringComparison.OrdinalIgnoreCase)
            && !trimmedPrefix.StartsWith("RETURN", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return (lineStart + leadingWhitespaceCount, trimmedPrefix);
    }

    private void ReplaceBraceCompletionText(int completionStart, string token)
    {
        if (completionStart < 0 || completionStart > EditorTextBox.Text.Length)
        {
            return;
        }

        var caret = EditorTextBox.CaretIndex;
        if (caret < completionStart)
        {
            return;
        }

        var length = caret - completionStart;
        var replacement = token + "}";
        EditorTextBox.Text = EditorTextBox.Text.Remove(completionStart, length).Insert(completionStart, replacement);
        EditorTextBox.CaretIndex = completionStart + replacement.Length;
    }

    private void ReplaceFunctionCompletionText(int completionStart, string token)
    {
        if (completionStart < 0 || completionStart >= EditorTextBox.Text.Length)
        {
            return;
        }

        var caret = EditorTextBox.CaretIndex;
        if (caret < completionStart)
        {
            return;
        }

        var length = caret - completionStart;
        EditorTextBox.Text = EditorTextBox.Text.Remove(completionStart, length).Insert(completionStart, token);
        EditorTextBox.CaretIndex = completionStart + token.Length;
    }

    private void ReplaceReturnCompletionText(int completionStart, string token)
    {
        if (completionStart < 0 || completionStart > EditorTextBox.Text.Length)
        {
            return;
        }

        var caret = EditorTextBox.CaretIndex;
        if (caret < completionStart)
        {
            return;
        }

        var length = caret - completionStart;
        EditorTextBox.Text = EditorTextBox.Text.Remove(completionStart, length).Insert(completionStart, token);
        EditorTextBox.CaretIndex = completionStart + token.Length;
    }

    private void CancelVariableCompletion()
    {
        _variableCompletionStart = null;
        VariableReferencePopup.IsOpen = false;
    }

    private void CancelFunctionCompletion()
    {
        _functionCompletionStart = null;
        FunctionPopup.IsOpen = false;
    }

    private void CancelReturnCompletion()
    {
        _returnCompletionStart = null;
        ReturnPopup.IsOpen = false;
    }
}
