using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public sealed class ExpressionTreeItem
{
    public string Title { get; set; } = string.Empty;
    public ProjectGamePropertyExpressionDto? Expression { get; init; }
    public Action<ProjectGamePropertyExpressionDto>? Replace { get; init; }
    public Action? Remove { get; init; }
    public ProjectGamePropertyExpressionDto? ParentExpression { get; init; }
    public ProjectGamePropertySelectBranchDto? Branch { get; init; }
    public ObservableCollection<ExpressionTreeItem> Children { get; } = new();
}

public partial class GamePropertyComputationEditorDialog : Window
{
    private readonly Func<ProjectGamePropertySourceReferenceDto?, ProjectGamePropertySourceReferenceDto?> _chooseSource;
    private readonly Func<ProjectGamePropertySourceReferenceDto?, string> _formatSource;
    private readonly bool _isReadOnly;
    private ProjectGamePropertyExpressionDto? _root;
    private bool _refreshing;

    public GamePropertyComputationEditorDialog(
        ProjectGamePropertyComputationV2Dto? computation,
        Func<ProjectGamePropertySourceReferenceDto?, ProjectGamePropertySourceReferenceDto?> chooseSource,
        Func<ProjectGamePropertySourceReferenceDto?, string> formatSource,
        bool readOnly = false)
    {
        InitializeComponent();
        _isReadOnly = readOnly;
        _chooseSource = chooseSource;
        _formatSource = formatSource;
        ResultKindComboBox.SelectedItem = FindItem(ResultKindComboBox, computation?.OutputType ?? "Boolean");
        _root = GamePropertyComputationCloner.CloneExpression(computation?.Expression) ?? Literal(false);
        RefreshTree(_root);
        if (_isReadOnly)
        {
            Title = "Computed Game Property (Read-only)";
            ResultKindComboBox.IsEnabled = false;
            NewNodeKindComboBox.IsEnabled = false;
            KindComboBox.IsEnabled = false;
            LiteralTypeComboBox.IsEnabled = false;
            LiteralTextBox.IsReadOnly = true;
            ChooseSourceButton.IsEnabled = false;
            OperatorComboBox.IsEnabled = false;
            AddExpressionButton.IsEnabled = false;
            AddBranchButton.IsEnabled = false;
            MoveBranchUpButton.IsEnabled = false;
            MoveBranchDownButton.IsEnabled = false;
            RemoveExpressionButton.IsEnabled = false;
            SaveButton.Content = "Close";
            CancelButton.Visibility = Visibility.Collapsed;
        }
    }

    public ProjectGamePropertyComputationV2Dto? Computation => DialogResult == true
        ? new ProjectGamePropertyComputationV2Dto
        {
            OutputType = SelectedOutputType,
            Expression = GamePropertyComputationCloner.CloneExpression(_root) ?? Literal(false)
        }
        : null;

    private string SelectedOutputType => (ResultKindComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Boolean";
    private static ComboBoxItem? FindItem(System.Windows.Controls.ComboBox combo, string? content) => combo.Items.OfType<ComboBoxItem>()
        .FirstOrDefault(item => string.Equals(item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase)) ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();

    private void RefreshTree(ProjectGamePropertyExpressionDto? select = null)
    {
        ExpressionTree.ItemsSource = _root is null ? Array.Empty<ExpressionTreeItem>() : new[] { BuildNode(_root, replacement => _root = replacement, null) };
        ExpressionTree.UpdateLayout();
        if (ExpressionTree.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem first)
        {
            ExpandAll(first);
            if (select is not null) SelectExpression(first, select);
        }
        if (ExpressionTree.SelectedItem is not ExpressionTreeItem) ClearEditor();
        RefreshPreview();
        ExpressionErrorTextBlock.Text = string.Join(Environment.NewLine, ValidateStructure(_root));
        UpdateActions();
    }

    private ExpressionTreeItem BuildNode(ProjectGamePropertyExpressionDto expression, Action<ProjectGamePropertyExpressionDto> replace,
        ProjectGamePropertyExpressionDto? parent, Action? remove = null, string? role = null)
    {
        var item = new ExpressionTreeItem { Expression = expression, Replace = replace, ParentExpression = parent, Remove = remove,
            Title = role is null ? NodeTitle(expression) : $"{role}: {NodeTitle(expression)}" };
        if (expression.Left is not null)
            item.Children.Add(BuildNode(expression.Left, value => expression.Left = value, expression, () => expression.Left = null!, "left"));
        else if (expression.Kind is "compare" or "add" or "subtract" or "multiply" or "divide")
            item.Children.Add(BuildNode(Literal(string.Empty), value => expression.Left = value, expression, () => expression.Left = null!, "left"));
        if (expression.Right is not null)
            item.Children.Add(BuildNode(expression.Right, value => expression.Right = value, expression, () => expression.Right = null!, "right"));
        else if (expression.Kind is "compare" or "add" or "subtract" or "multiply" or "divide")
            item.Children.Add(BuildNode(Literal(0d), value => expression.Right = value, expression, () => expression.Right = null!, "right"));
        if (expression.Expression is not null)
            item.Children.Add(BuildNode(expression.Expression, value => expression.Expression = value, expression, () => expression.Expression = null!, "child"));
        else if (expression.Kind == "not")
            item.Children.Add(BuildNode(Literal(false), value => expression.Expression = value, expression, () => expression.Expression = null!, "child"));
        if (expression.Operands is not null)
        {
            for (var index = 0; index < expression.Operands.Count; index++)
            {
                var childIndex = index;
                item.Children.Add(BuildNode(expression.Operands[index], value => expression.Operands[childIndex] = value, expression,
                    () => expression.Operands.RemoveAt(childIndex), $"operand {index + 1}"));
            }
        }
        if (expression.Kind is "and" or "or" && (expression.Operands is null || expression.Operands.Count == 0))
            item.Children.Add(BuildNode(Literal(false), value => { expression.Operands ??= new(); expression.Operands.Add(value); }, expression));
        if (expression.Branches is not null)
        {
            for (var index = 0; index < expression.Branches.Count; index++)
            {
                var branchIndex = index;
                var branch = expression.Branches[index];
                var branchItem = new ExpressionTreeItem
                {
                    Title = $"Branch {index + 1} (first match)", Branch = branch, ParentExpression = expression,
                    Remove = () => expression.Branches.RemoveAt(branchIndex)
                };
                branchItem.Children.Add(BuildNode(branch.When, value => branch.When = value, expression, role: "when"));
                branchItem.Children.Add(BuildNode(branch.Value, value => branch.Value = value, expression, role: "value"));
                item.Children.Add(branchItem);
            }
        }
        if (expression.Kind == "select")
        {
            if (expression.Else is not null) item.Children.Add(BuildNode(expression.Else, value => expression.Else = value, expression, role: "else"));
            else item.Children.Add(BuildNode(Literal(string.Empty), value => expression.Else = value, expression, role: "else"));
        }
        return item;
    }

    private static string NodeTitle(ProjectGamePropertyExpressionDto expression)
    {
        var detail = expression.Kind switch
        {
            "literal" => " " + FormatValue(expression.Value),
            "property" or "exists" => " " + GamePropertyComputationFormatter.FormatSourceReference(expression.Source),
            "compare" => " " + expression.Operator,
            _ => string.Empty
        };
        return expression.Kind + detail;
    }

    private static void ExpandAll(TreeViewItem item)
    {
        item.IsExpanded = true;
        item.UpdateLayout();
        for (var i = 0; i < item.Items.Count; i++)
            if (item.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem child) ExpandAll(child);
    }

    private bool SelectExpression(TreeViewItem item, ProjectGamePropertyExpressionDto selected)
    {
        if (item.DataContext is ExpressionTreeItem node && ReferenceEquals(node.Expression, selected)) { item.IsSelected = true; return true; }
        item.IsExpanded = true;
        item.UpdateLayout();
        for (var i = 0; i < item.Items.Count; i++)
            if (item.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem child && SelectExpression(child, selected)) return true;
        return false;
    }

    private void ExpressionTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not ExpressionTreeItem item || item.Expression is null) { ClearEditor(); UpdateActions(); return; }
        var expression = item.Expression;
        _refreshing = true;
        SelectComboValue(KindComboBox, expression.Kind);
        SelectComboValue(OperatorComboBox, expression.Operator);
        SelectLiteralType(expression.Value);
        LiteralTextBox.Text = LiteralText(expression.Value);
        SourceTextBox.Text = _formatSource(expression.Source);
        var kind = expression.Kind;
        LiteralPanel.Visibility = kind == "literal" ? Visibility.Visible : Visibility.Collapsed;
        SourcePanel.Visibility = kind is "property" or "exists" ? Visibility.Visible : Visibility.Collapsed;
        OperatorPanel.Visibility = kind == "compare" ? Visibility.Visible : Visibility.Collapsed;
        var isEditable = !_isReadOnly;
        KindComboBox.IsEnabled = isEditable && kind is not ("add" or "subtract" or "multiply" or "divide");
        LiteralTypeComboBox.IsEnabled = isEditable;
        LiteralTextBox.IsReadOnly = !isEditable;
        ChooseSourceButton.IsEnabled = isEditable;
        OperatorComboBox.IsEnabled = isEditable;
        NodeHelpTextBlock.Text = HelpText(kind);
        _refreshing = false;
        UpdateActions();
    }

    private void ClearEditor()
    {
        LiteralPanel.Visibility = SourcePanel.Visibility = OperatorPanel.Visibility = Visibility.Collapsed;
        NodeHelpTextBlock.Text = "Select a node to edit it. Branch conditions and values are separate expressions.";
    }

    private void ResultKindComboBox_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshing) RefreshPreview();
    }

    private void KindComboBox_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression: not null, Replace: not null } selected) return;
        var kind = (KindComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (kind is null || kind == selected.Expression.Kind) return;
        var replacement = CreateDefault(kind);
        selected.Replace(replacement);
        RefreshTree(replacement);
    }

    private void LiteralTypeComboBox_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression.Kind: "literal" } selected) return;
        LiteralTextBox_OnChanged(sender, e);
    }

    private void LiteralTextBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing || ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression: not null } selected) return;
        var expression = selected.Expression;
        if (expression.Kind != "literal") return;
        var type = (LiteralTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "String";
        switch (type)
        {
            case "Boolean":
                if (!bool.TryParse(LiteralTextBox.Text, out var boolean)) { ExpressionErrorTextBlock.Text = "Enter true or false for a Boolean literal."; return; }
                expression.Value = boolean;
                break;
            case "Numeric":
                if (!double.TryParse(LiteralTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                { ExpressionErrorTextBlock.Text = "Enter a finite invariant-culture number."; return; }
                expression.Value = number;
                break;
            default: expression.Value = LiteralTextBox.Text; break;
        }
        ExpressionErrorTextBlock.Text = string.Empty;
        RefreshTree(expression);
    }

    private void OperatorComboBox_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression: not null } selected) return;
        selected.Expression.Operator = (OperatorComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        RefreshTree(selected.Expression);
    }

    private void ChooseSource_OnClick(object sender, RoutedEventArgs e)
    {
        if (ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression: not null } selected) return;
        var source = _chooseSource(selected.Expression.Source);
        if (source is null) return;
        selected.Expression.Source = source;
        RefreshTree(selected.Expression);
    }

    private void AddExpression_OnClick(object sender, RoutedEventArgs e)
    {
        var kind = (NewNodeKindComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "literal";
        var child = CreateDefault(kind);
        if (ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression: not null } selected) { _root = child; RefreshTree(child); return; }
        var parent = selected.Expression;
        switch (parent.Kind)
        {
            case "and": case "or": parent.Operands ??= new(); parent.Operands.Add(child); break;
            case "not": if (parent.Expression is null) parent.Expression = child; else return; break;
            case "compare": case "add": case "subtract": case "multiply": case "divide":
                if (parent.Left is null) parent.Left = child; else if (parent.Right is null) parent.Right = child; else return; break;
            default: return;
        }
        RefreshTree(child);
    }

    private void AddBranch_OnClick(object sender, RoutedEventArgs e)
    {
        if (ExpressionTree.SelectedItem is not ExpressionTreeItem { Expression.Kind: "select" } selected) return;
        selected.Expression.Branches ??= new();
        var branch = new ProjectGamePropertySelectBranchDto { When = Literal(false), Value = Literal(string.Empty) };
        selected.Expression.Branches.Add(branch);
        RefreshTree(branch.When);
    }

    private void RemoveExpression_OnClick(object sender, RoutedEventArgs e)
    {
        if (ExpressionTree.SelectedItem is not ExpressionTreeItem { Remove: not null } selected) return;
        selected.Remove();
        RefreshTree();
    }

    private void MoveBranchUp_OnClick(object sender, RoutedEventArgs e) => MoveSelectedBranch(-1);
    private void MoveBranchDown_OnClick(object sender, RoutedEventArgs e) => MoveSelectedBranch(1);
    private void MoveSelectedBranch(int delta)
    {
        if (ExpressionTree.SelectedItem is not ExpressionTreeItem { Branch: not null, ParentExpression: not null } selected) return;
        var branches = selected.ParentExpression.Branches;
        var index = branches?.IndexOf(selected.Branch) ?? -1;
        var target = index + delta;
        if (branches is null || index < 0 || target < 0 || target >= branches.Count) return;
        branches.RemoveAt(index);
        branches.Insert(target, selected.Branch);
        RefreshTree();
    }

    private void UpdateActions()
    {
        var selected = ExpressionTree.SelectedItem as ExpressionTreeItem;
        var expression = selected?.Expression;
        var canAddChild = expression?.Kind switch
        {
            "and" or "or" => true,
            "not" => expression.Expression is null,
            "compare" => expression.Left is null || expression.Right is null,
            _ => false
        };
        AddExpressionButton.IsEnabled = !_isReadOnly && canAddChild;
        AddBranchButton.IsEnabled = !_isReadOnly && expression?.Kind == "select";
        RemoveExpressionButton.IsEnabled = !_isReadOnly && selected?.Remove is not null;
        var branchIndex = selected?.Branch is null || selected.ParentExpression?.Branches is null ? -1 : selected.ParentExpression.Branches.IndexOf(selected.Branch);
        MoveBranchUpButton.IsEnabled = !_isReadOnly && branchIndex > 0;
        MoveBranchDownButton.IsEnabled = !_isReadOnly && selected?.ParentExpression?.Branches is { } branches && branchIndex >= 0 && branchIndex < branches.Count - 1;
    }

    private void RefreshPreview()
    {
        var converted = SelectedOutputType switch
        {
            "Boolean" => "Boolean output: strings are trimmed and recognized Boolean tokens convert; numbers use zero/nonzero.",
            "Numeric" => "Numeric output: strings parse as invariant numbers; Booleans convert to 1 or 0.",
            _ => "String output: numbers use invariant formatting; Booleans become lowercase true or false."
        };
        ExpressionPreviewTextBlock.Text = $"{SelectedOutputType} ← {GamePropertyComputationFormatter.Format(_root)}\n{converted}";
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isReadOnly) { DialogResult = true; return; }
        var errors = ValidateStructure(_root).ToList();
        if (errors.Count > 0) { ExpressionErrorTextBlock.Text = string.Join(Environment.NewLine, errors); return; }
        DialogResult = true;
    }

    private static IEnumerable<string> ValidateStructure(ProjectGamePropertyExpressionDto? expression)
    {
        if (expression is null) { yield return "The expression is missing."; yield break; }
        switch (expression.Kind)
        {
            case "literal": if (expression.Value is null) yield return "A literal needs a value."; break;
            case "property": case "exists": if (string.IsNullOrWhiteSpace(expression.Source?.PropertyName)) yield return "Choose a property source."; break;
            case "compare": if (expression.Left is null || expression.Right is null) yield return "A comparison needs both operands."; break;
            case "and": case "or": if (expression.Operands is null || expression.Operands.Count < 2) yield return $"{expression.Kind} needs at least two operands."; break;
            case "not": if (expression.Expression is null) yield return "not needs one child expression."; break;
            case "select": if (expression.Branches is null || expression.Branches.Count == 0 || expression.Else is null) yield return "select needs at least one branch and an else value."; break;
            case "add": case "subtract": case "multiply": case "divide": if (expression.Left is null || expression.Right is null) yield return $"{expression.Kind} needs both operands."; break;
            default: yield return $"Unsupported expression kind '{expression.Kind}'."; break;
        }
        foreach (var child in Children(expression)) foreach (var issue in ValidateStructure(child)) yield return issue;
    }

    private static IEnumerable<ProjectGamePropertyExpressionDto?> Children(ProjectGamePropertyExpressionDto expression)
    {
        yield return expression.Left; yield return expression.Right; yield return expression.Expression;
        foreach (var child in expression.Operands ?? []) yield return child;
        foreach (var branch in expression.Branches ?? []) { yield return branch.When; yield return branch.Value; }
        yield return expression.Else;
    }

    private static string HelpText(string kind) => kind switch
    {
        "and" => "All operands must convert to true. Operands are evaluated in order and short-circuit.",
        "or" => "Any operand may convert to true. Operands are evaluated in order and short-circuit.",
        "select" => "Branches are tested in order; the first true condition supplies the value, otherwise else is used.",
        "exists" => "Checks whether the selected source is present, independently of its value.",
        "compare" => "Ordering comparisons convert both sides to Numeric. Equality uses the agreed mixed-kind conversion rules.",
        "add" or "subtract" or "multiply" or "divide" => "Both operands convert to Numeric. Tree nesting defines grouping.",
        "property" => "Reads the selected property and preserves its native value kind.",
        _ => string.Empty
    };

    private static ProjectGamePropertyExpressionDto CreateDefault(string kind) => kind switch
    {
        "property" or "exists" => new ProjectGamePropertyExpressionDto { Kind = kind },
        "compare" => new ProjectGamePropertyExpressionDto { Kind = kind, Operator = "Equals", Left = Literal(string.Empty), Right = Literal(string.Empty) },
        "and" or "or" => new ProjectGamePropertyExpressionDto { Kind = kind, Operands = new() { Literal(false), Literal(false) } },
        "not" => new ProjectGamePropertyExpressionDto { Kind = kind, Expression = Literal(false) },
        "select" => new ProjectGamePropertyExpressionDto { Kind = kind, Branches = new() { new ProjectGamePropertySelectBranchDto { When = Literal(false), Value = Literal(string.Empty) } }, Else = Literal(string.Empty) },
        "add" or "subtract" or "multiply" or "divide" => new ProjectGamePropertyExpressionDto { Kind = kind, Left = Literal(0d), Right = Literal(0d) },
        _ => Literal(false)
    };

    private static ProjectGamePropertyExpressionDto Literal(object value) => new() { Kind = "literal", Value = value };
    private void SelectLiteralType(object? value) => LiteralTypeComboBox.SelectedItem = FindItem(LiteralTypeComboBox, LiteralType(value));
    private static string LiteralType(object? value) => value switch
    {
        bool => "Boolean", double or float or int or long or decimal => "Numeric",
        JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => "Boolean",
        JsonElement element when element.ValueKind == JsonValueKind.Number => "Numeric", _ => "String"
    };
    private static string LiteralText(object? value) => value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    private static string FormatValue(object? value) => LiteralType(value) == "String" ? "\"" + LiteralText(value) + "\"" : LiteralText(value);
    private void SelectComboValue(System.Windows.Controls.ComboBox combo, string? value) => combo.SelectedItem = FindItem(combo, value);

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
