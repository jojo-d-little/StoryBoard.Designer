using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Views;

public partial class GamePropertyConditionEditorDialog : Window
{
    private readonly Func<ProjectGamePropertySourceReferenceDto?, ProjectGamePropertySourceReferenceDto?> _chooseSource;
    private readonly Func<ProjectGamePropertySourceReferenceDto?, string>? _formatSource;
    private ProjectGamePropertyConditionDto? _root;
    private bool _refreshing;

    public GamePropertyConditionEditorDialog(
        ProjectGamePropertyConditionDto? condition,
        Func<ProjectGamePropertySourceReferenceDto?, ProjectGamePropertySourceReferenceDto?> chooseSource,
        Func<ProjectGamePropertySourceReferenceDto?, string>? formatSource = null)
    {
        InitializeComponent();
        _chooseSource = chooseSource;
        _formatSource = formatSource;
        _root = Clone(condition);
        RefreshTree();
    }

    public ProjectGamePropertyConditionDto? Condition => DialogResult == true ? Clone(_root) : null;

    private void RefreshTree(ProjectGamePropertyConditionDto? select = null)
    {
        ConditionTree.ItemsSource = _root is null ? Array.Empty<ConditionTreeItem>() : new[] { BuildNode(_root, null) };
        PreviewTextBlock.Text = _root is null ? "(empty)" : BuildPreview(_root);
        if (_root is not null && ConditionTree.Items.Count > 0)
        {
            ConditionTree.UpdateLayout();
            if (ConditionTree.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem first)
            {
                first.IsExpanded = true;
                if (select is not null)
                {
                    SelectTreeItem(first, select);
                }
            }
        }

        UpdateSelectionState();
    }

    private bool SelectTreeItem(TreeViewItem item, ProjectGamePropertyConditionDto? selected)
    {
        if (selected is not null && item.DataContext is ConditionTreeItem node && ReferenceEquals(node.Condition, selected))
        {
            item.IsSelected = true;
            return true;
        }

        if (item.Items.Count == 0) return false;
        item.IsExpanded = true;
        item.UpdateLayout();
        for (var index = 0; index < item.Items.Count; index++)
        {
            if (item.ItemContainerGenerator.ContainerFromIndex(index) is TreeViewItem child && SelectTreeItem(child, selected))
                return true;
        }
        return false;
    }

    private ConditionTreeItem BuildNode(ProjectGamePropertyConditionDto condition, ConditionTreeItem? parent)
    {
        var node = new ConditionTreeItem(condition, parent, _formatSource);
        if (condition.Kind is "All" or "Any")
        {
            foreach (var child in condition.Conditions ?? new List<ProjectGamePropertyConditionDto>())
                node.Children.Add(BuildNode(child, node));
        }
        else if (condition.Kind == "Not" && condition.Condition is not null)
        {
            node.Children.Add(BuildNode(condition.Condition, node));
        }
        return node;
    }

    private void ConditionTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not ConditionTreeItem node)
        {
            UpdateSelectionState();
            return;
        }

        _refreshing = true;
        var condition = node.Condition;
        SourceTextBox.Text = FormatSource(condition.GamePropertySource);
        SelectComboValue(OperatorComboBox, condition.Operator);
        var operand = condition.RightOperand;
        RightOperandIsSourceCheckBox.IsChecked = operand?.GamePropertySource is not null;
        RightSourceTextBox.Text = FormatSource(operand?.GamePropertySource);
        SelectComboValue(ValueKindComboBox, operand?.ValueKind);
        ValueTextBox.Text = operand?.Value ?? string.Empty;
        var isCompare = condition.Kind == "Compare";
        var hasRight = isCompare && condition.Operator is not ("IsTrue" or "IsFalse");
        OperatorComboBox.IsEnabled = isCompare;
        SourceTextBox.IsEnabled = condition.Kind is "Compare" or "Exists";
        RightOperandIsSourceCheckBox.IsEnabled = hasRight;
        RightSourcePanel.IsEnabled = hasRight;
        RightConstantPanel.IsEnabled = hasRight;
        RightOperandIsSourceCheckBox.Visibility = isCompare ? Visibility.Visible : Visibility.Collapsed;
        RightSourcePanel.Visibility = hasRight && operand?.GamePropertySource is not null ? Visibility.Visible : Visibility.Collapsed;
        RightConstantPanel.Visibility = hasRight && operand?.GamePropertySource is null ? Visibility.Visible : Visibility.Collapsed;
        _refreshing = false;

        var canEditCondition = condition.Kind is "Compare" or "Exists";
        ConditionEditorPanel.Visibility = canEditCondition ? Visibility.Visible : Visibility.Collapsed;
        ConditionSelectionPromptTextBlock.Visibility = canEditCondition ? Visibility.Collapsed : Visibility.Visible;
        ConditionSelectionPromptTextBlock.Text = condition.Kind switch
        {
            "All" => "AND group selected. Select a comparison or Exists condition to edit its property and value, or use the buttons above to add logic to this group.",
            "Any" => "OR group selected. Select a comparison or Exists condition to edit its property and value, or use the buttons above to add logic to this group.",
            "Not" => "NOT selected. Select a condition to edit its property and value, or use the buttons above to change the logic.",
            _ => "Select a comparison or Exists condition to edit its property and value."
        };
        UpdateSelectionState();
        ConditionEditorPanel.IsEnabled = canEditCondition;
    }

    private void UpdateSelectionState()
    {
        var hasSelection = ConditionTree.SelectedItem is ConditionTreeItem;
        var canStartEmptyTree = _root is null;
        var canAdd = hasSelection || canStartEmptyTree;

        AddAllButton.IsEnabled = canAdd;
        AddAnyButton.IsEnabled = canAdd;
        AddNotButton.IsEnabled = hasSelection;
        AddCompareButton.IsEnabled = canAdd;
        AddExistsButton.IsEnabled = canAdd;
        RemoveButton.IsEnabled = hasSelection;

        if (!hasSelection)
        {
            ConditionEditorPanel.Visibility = Visibility.Collapsed;
            ConditionEditorPanel.IsEnabled = false;
            ConditionSelectionPromptTextBlock.Visibility = Visibility.Visible;
            ConditionSelectionPromptTextBlock.Text = canStartEmptyTree
                ? "The logic tree is empty. Add a group or condition to begin."
                : "Select a group or condition in the tree to see its available actions.";
        }
    }

    private void AddAll_OnClick(object sender, RoutedEventArgs e) => AddNode(new ProjectGamePropertyConditionDto { Kind = "All", Conditions = new() });
    private void AddAny_OnClick(object sender, RoutedEventArgs e) => AddNode(new ProjectGamePropertyConditionDto { Kind = "Any", Conditions = new() });
    private void AddNot_OnClick(object sender, RoutedEventArgs e)
    {
        var wrapper = new ProjectGamePropertyConditionDto { Kind = "Not" };
        if (_root is null)
        {
            wrapper.Condition = new ProjectGamePropertyConditionDto { Kind = "Compare", Operator = "IsTrue" };
            _root = wrapper;
        }
        else if (ConditionTree.SelectedItem is ConditionTreeItem selected)
        {
            wrapper.Condition = selected.Condition;
            if (selected.Parent is null)
            {
                _root = wrapper;
            }
            else if (selected.Parent.Condition.Kind is "All" or "Any")
            {
                var children = selected.Parent.Condition.Conditions;
                var index = children?.IndexOf(selected.Condition) ?? -1;
                if (index >= 0) children![index] = wrapper;
            }
            else if (selected.Parent.Condition.Kind == "Not")
            {
                selected.Parent.Condition.Condition = wrapper;
            }
        }
        else
        {
            wrapper.Condition = _root;
            _root = wrapper;
        }

        RefreshTree(wrapper);
    }
    private void AddCompare_OnClick(object sender, RoutedEventArgs e) => AddNode(new ProjectGamePropertyConditionDto { Kind = "Compare", Operator = "IsTrue" });
    private void AddExists_OnClick(object sender, RoutedEventArgs e) => AddNode(new ProjectGamePropertyConditionDto { Kind = "Exists" });

    private void AddNode(ProjectGamePropertyConditionDto condition)
    {
        if (_root is null)
        {
            _root = condition;
        }
        else if (ConditionTree.SelectedItem is ConditionTreeItem selected
                 && selected.Condition.Kind is "All" or "Any")
        {
            selected.Condition.Conditions ??= new();
            selected.Condition.Conditions.Add(condition);
        }
        else if (ConditionTree.SelectedItem is ConditionTreeItem selectedNode)
        {
            // Add beside the selected expression in its containing AND/OR group.
            // A condition inside NOT is part of the NOT expression, so insert beside
            // the NOT wrapper rather than inside it.
            var insertionAnchor = selectedNode.Parent?.Condition.Kind == "Not"
                ? selectedNode.Parent
                : selectedNode;
            if (insertionAnchor?.Parent?.Condition.Kind is "All" or "Any")
            {
                var siblings = insertionAnchor.Parent.Condition.Conditions;
                siblings ??= new();
                insertionAnchor.Parent.Condition.Conditions = siblings;
                var index = siblings.IndexOf(insertionAnchor.Condition);
                siblings.Insert(index < 0 ? siblings.Count : index + 1, condition);
            }
            else
            {
                _root = new ProjectGamePropertyConditionDto
                {
                    Kind = "All",
                    Conditions = new() { _root, condition }
                };
            }
        }
        else
        {
            _root = new ProjectGamePropertyConditionDto
            {
                Kind = "All",
                Conditions = new() { _root, condition }
            };
        }

        RefreshTree(condition);
    }

    private void Remove_OnClick(object sender, RoutedEventArgs e)
    {
        if (ConditionTree.SelectedItem is not ConditionTreeItem selected) return;
        if (selected.Parent is null)
        {
            _root = null;
        }
        else if (selected.Parent.Condition.Kind is "All" or "Any")
        {
            selected.Parent.Condition.Conditions?.Remove(selected.Condition);
        }
        else if (selected.Parent.Condition.Kind == "Not")
        {
            selected.Parent.Condition.Condition = null!;
        }

        RefreshTree(_root);
    }

    private void ChooseSource_OnClick(object sender, RoutedEventArgs e) => SetSource(right: false);
    private void ChooseRightSource_OnClick(object sender, RoutedEventArgs e) => SetSource(right: true);

    private void SetSource(bool right)
    {
        if (ConditionTree.SelectedItem is not ConditionTreeItem selected) return;
        var priorSource = right ? selected.Condition.RightOperand?.GamePropertySource : selected.Condition.GamePropertySource;
        var source = _chooseSource(priorSource);
        if (source is null) return;
        if (right)
        {
            selected.Condition.RightOperand ??= new ProjectGamePropertyOperandDto();
            selected.Condition.RightOperand.GamePropertySource = source;
            selected.Condition.RightOperand.Value = null;
            selected.Condition.RightOperand.ValueKind = null;
        }
        else
        {
            selected.Condition.GamePropertySource = source;
        }
        RefreshTree(selected.Condition);
    }

    private void OperandMode_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing || ConditionTree.SelectedItem is not ConditionTreeItem selected) return;
        var condition = selected.Condition;
        if (condition.Operator is "IsTrue" or "IsFalse") condition.Operator = "Equals";
        condition.RightOperand ??= new ProjectGamePropertyOperandDto();
        if (RightOperandIsSourceCheckBox.IsChecked == true)
        {
            condition.RightOperand.GamePropertySource ??= new ProjectGamePropertySourceReferenceDto();
            condition.RightOperand.Value = null;
            condition.RightOperand.ValueKind = null;
        }
        else
        {
            condition.RightOperand.GamePropertySource = null!;
            condition.RightOperand.ValueKind = GetComboValue(ValueKindComboBox) ?? "Boolean";
            condition.RightOperand.Value = ValueTextBox.Text;
        }
        ApplyOperandEditorState();
        RightSourceTextBox.Text = FormatSource(condition.RightOperand.GamePropertySource);
        PreviewTextBlock.Text = _root is null ? "(empty)" : BuildPreview(_root);
    }

    private void Operand_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing || ConditionTree.SelectedItem is not ConditionTreeItem selected) return;
        var condition = selected.Condition;
        if (condition.Kind != "Compare") return;
        condition.Operator = GetComboValue(OperatorComboBox) ?? "IsTrue";
        if (condition.Operator is "IsTrue" or "IsFalse")
        {
            condition.RightOperand = null!;
        }
        else if (RightOperandIsSourceCheckBox.IsChecked == true)
        {
            condition.RightOperand ??= new ProjectGamePropertyOperandDto();
            condition.RightOperand.GamePropertySource ??= new ProjectGamePropertySourceReferenceDto();
            condition.RightOperand.Value = null;
            condition.RightOperand.ValueKind = null;
        }
        else
        {
            condition.RightOperand ??= new ProjectGamePropertyOperandDto();
            condition.RightOperand.GamePropertySource = null!;
            condition.RightOperand.ValueKind = GetComboValue(ValueKindComboBox) ?? "Boolean";
            condition.RightOperand.Value = ValueTextBox.Text;
        }
        RefreshTree(condition);
        ApplyOperandEditorState();
    }

    private void ApplyOperandEditorState()
    {
        var isSource = RightOperandIsSourceCheckBox.IsChecked == true;
        RightSourcePanel.Visibility = isSource ? Visibility.Visible : Visibility.Collapsed;
        RightConstantPanel.Visibility = isSource ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (_root is null)
        {
            System.Windows.MessageBox.Show(this, "Add a condition before saving.", "Computed Game Property", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private string BuildPreview(ProjectGamePropertyConditionDto condition)
    {
        return condition.Kind switch
        {
            "All" => "(" + string.Join(" AND ", (condition.Conditions ?? new()).Select(BuildPreview)) + ")",
            "Any" => "(" + string.Join(" OR ", (condition.Conditions ?? new()).Select(BuildPreview)) + ")",
            "Not" => "NOT (" + (condition.Condition is null ? "<missing condition>" : BuildPreview(condition.Condition)) + ")",
            "Exists" => "exists(" + FormatSource(condition.GamePropertySource) + ")",
            "Compare" => FormatSource(condition.GamePropertySource) + " " + (condition.Operator ?? "?") + (condition.RightOperand is null ? string.Empty : " " + FormatOperand(condition.RightOperand)),
            _ => "<unknown condition>"
        };
    }

    private string FormatOperand(ProjectGamePropertyOperandDto operand) => operand.GamePropertySource is not null
        ? FormatSource(operand.GamePropertySource)
        : $"{operand.ValueKind ?? "?"}({operand.Value ?? ""})";

    private string FormatSource(ProjectGamePropertySourceReferenceDto? source)
    {
        if (source is null) return "<choose property>";
        if (_formatSource is not null) return _formatSource(source);
        var prefix = !string.IsNullOrWhiteSpace(source.AnchorKey)
            ? source.AnchorKey + "."
            : source.OwnerId.HasValue ? $"owner[{source.OwnerId.Value:N}]." : "self.";
        return prefix + (source.PropertyName ?? "<missing name>");
    }

    private static void SelectComboValue(System.Windows.Controls.ComboBox combo, string? value)
    {
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetComboValue(System.Windows.Controls.ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();

    private static ProjectGamePropertyConditionDto? Clone(ProjectGamePropertyConditionDto? condition) => condition is null
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<ProjectGamePropertyConditionDto>(System.Text.Json.JsonSerializer.Serialize(condition));

    private sealed class ConditionTreeItem
    {
        private readonly Func<ProjectGamePropertySourceReferenceDto?, string>? _formatSource;

        public ConditionTreeItem(ProjectGamePropertyConditionDto condition, ConditionTreeItem? parent, Func<ProjectGamePropertySourceReferenceDto?, string>? formatSource)
        {
            Condition = condition;
            Parent = parent;
            _formatSource = formatSource;
            Children = new ObservableCollection<ConditionTreeItem>();
        }

        public ProjectGamePropertyConditionDto Condition { get; }
        public ConditionTreeItem? Parent { get; }
        public ObservableCollection<ConditionTreeItem> Children { get; }
        public string Label => Condition.Kind switch
        {
            "All" => "AND group",
            "Any" => "OR group",
            "Not" => "NOT",
            "Exists" => "Exists: " + FormatSource(Condition.GamePropertySource),
            "Compare" => FormatSource(Condition.GamePropertySource) + " " + (Condition.Operator ?? "?"),
            _ => Condition.Kind ?? "Condition"
        };

        private string FormatSource(ProjectGamePropertySourceReferenceDto? source)
        {
            if (source is null) return "<choose property>";
            if (_formatSource is not null) return _formatSource(source);
            var prefix = !string.IsNullOrWhiteSpace(source.AnchorKey)
                ? source.AnchorKey + "."
                : source.OwnerId.HasValue ? $"owner[{source.OwnerId.Value:N}]." : "self.";
            return prefix + (source.PropertyName ?? "<missing name>");
        }

    }
}
