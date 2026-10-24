using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class GamePropertyComputationEditorDialog : Window
{
    private sealed class BranchRow(ProjectGamePropertyResultBranchDto branch, Func<ProjectGamePropertySourceReferenceDto?, string> formatSource) : INotifyPropertyChanged
    {
        public ProjectGamePropertyResultBranchDto Branch { get; } = branch;
        public string Label => $"IF {GamePropertyComputationFormatter.Format(Branch.Condition)} THEN {FormatOperand(Branch.Result, formatSource)}";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RefreshLabel() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }

    private readonly Func<ProjectGamePropertySourceReferenceDto?, ProjectGamePropertySourceReferenceDto?> _chooseSource;
    private readonly Func<ProjectGamePropertySourceReferenceDto?, string> _formatSource;
    private readonly ObservableCollection<BranchRow> _branchRows = new();
    private ProjectGamePropertyConditionDto? _booleanCondition;
    private ProjectGamePropertyOperandDto? _elseResult;
    private bool _branchUsesProperty;
    private bool _elseUsesProperty;
    private BranchRow? _lastSelectedBranch;
    private bool _refreshing;
    private readonly bool _isReadOnly;

    public GamePropertyComputationEditorDialog(
        ProjectGamePropertyComputationDto? computation,
        Func<ProjectGamePropertySourceReferenceDto?, ProjectGamePropertySourceReferenceDto?> chooseSource,
        Func<ProjectGamePropertySourceReferenceDto?, string> formatSource,
        bool readOnly = false)
    {
        InitializeComponent();
        _isReadOnly = readOnly;
        _chooseSource = chooseSource;
        _formatSource = formatSource;
        BranchesListBox.ItemsSource = _branchRows;
        if (computation is not null)
        {
            _booleanCondition = Clone(computation.Condition);
            foreach (var branch in computation.ResultBranches ?? new List<ProjectGamePropertyResultBranchDto>())
                _branchRows.Add(new BranchRow(Clone(branch)!, _formatSource));
            _elseResult = Clone(computation.ElseResult);
        }

        SelectResultKind(computation?.ResultKind ?? "Boolean");
        RefreshBooleanCondition();
        RefreshBranchEditor();
        RefreshElseEditor();
        if (_isReadOnly)
        {
            Title = "Computed Game Property (Read-only)";
            ResultKindComboBox.IsEnabled = false;
            EditBooleanConditionButton.Content = "Review condition tree...";
            SaveButton.Content = "Close";
            CancelButton.Visibility = Visibility.Collapsed;
            AddBranchButton.IsEnabled = false;
            MoveBranchUpButton.IsEnabled = false;
            MoveBranchDownButton.IsEnabled = false;
            RemoveBranchButton.IsEnabled = false;
            EditBranchConditionButton.Content = "Review...";
            BranchUsesPropertyCheckBox.IsEnabled = false;
            ChooseBranchSourceButton.IsEnabled = false;
            BranchLiteralTextBox.IsReadOnly = true;
            ElseUsesPropertyCheckBox.IsEnabled = false;
            ElseLiteralTextBox.IsReadOnly = true;
            ChooseElseSourceButton.IsEnabled = false;
        }
    }

    public ProjectGamePropertyComputationDto? Computation => DialogResult == true ? BuildComputation() : null;

    private string SelectedResultKind => (ResultKindComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Boolean";
    private bool IsBoolean => string.Equals(SelectedResultKind, "Boolean", StringComparison.OrdinalIgnoreCase);
    private string OutputValueKind => SelectedResultKind == "Numeric" ? "Numeric" : "Text";

    private ProjectGamePropertyComputationDto BuildComputation() => new()
    {
        ResultKind = SelectedResultKind,
        Condition = IsBoolean ? Clone(_booleanCondition) : null,
        ResultBranches = IsBoolean ? null : _branchRows.Select(static row => Clone(row.Branch)!).ToList(),
        ElseResult = IsBoolean ? null : Clone(_elseResult)
    };

    private static T? Clone<T>(T? value) where T : class => value is null
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(value));

    private static string FormatOperand(ProjectGamePropertyOperandDto? operand, Func<ProjectGamePropertySourceReferenceDto?, string> formatSource) =>
        operand is null ? "<missing output>" : operand.GamePropertySource is not null ? formatSource(operand.GamePropertySource) : operand.Value ?? "";

    private void SelectResultKind(string resultKind)
    {
        ResultKindComboBox.SelectedItem = ResultKindComboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), resultKind, StringComparison.OrdinalIgnoreCase))
            ?? ResultKindComboBox.Items.OfType<ComboBoxItem>().First();
        UpdateResultKindPanels();
    }

    private void ResultKindComboBox_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        UpdateResultKindPanels();
        foreach (var row in _branchRows)
        {
            if (row.Branch.Result is not null && row.Branch.Result.GamePropertySource is null)
                row.Branch.Result.ValueKind = OutputValueKind;
        }
        if (_elseResult is not null && _elseResult.GamePropertySource is null)
            _elseResult.ValueKind = OutputValueKind;
        foreach (var row in _branchRows) row.RefreshLabel();
        RefreshBranchEditor();
        RefreshElseEditor();
    }

    private void UpdateResultKindPanels()
    {
        BooleanPanel.Visibility = IsBoolean ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = IsBoolean ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshBooleanCondition() => BooleanConditionTextBlock.Text = GamePropertyComputationFormatter.Format(_booleanCondition);

    private void EditBooleanCondition_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = CreateConditionDialog(_booleanCondition);
        if (dialog.ShowDialog() != true || dialog.Condition is null) return;
        _booleanCondition = dialog.Condition;
        RefreshBooleanCondition();
    }

    private GamePropertyConditionEditorDialog CreateConditionDialog(ProjectGamePropertyConditionDto? condition) => new(
        condition,
        _chooseSource,
        _formatSource,
        _isReadOnly)
    {
        Owner = this
    };

    private void AddBranch_OnClick(object sender, RoutedEventArgs e)
    {
        var branch = new ProjectGamePropertyResultBranchDto
        {
            Condition = new ProjectGamePropertyConditionDto { Kind = "Compare", Operator = "IsTrue" },
            Result = new ProjectGamePropertyOperandDto { ValueKind = OutputValueKind, Value = OutputValueKind == "Numeric" ? "0" : string.Empty }
        };
        var row = new BranchRow(branch, _formatSource);
        _branchRows.Add(row);
        BranchesListBox.SelectedItem = row;
        RefreshBranchEditor();
    }

    private void RemoveBranch_OnClick(object sender, RoutedEventArgs e)
    {
        if (BranchesListBox.SelectedItem is not BranchRow row) return;
        var index = _branchRows.IndexOf(row);
        _branchRows.Remove(row);
        if (_branchRows.Count > 0) BranchesListBox.SelectedIndex = Math.Min(index, _branchRows.Count - 1);
        RefreshBranchEditor();
    }

    private void MoveBranchUp_OnClick(object sender, RoutedEventArgs e) => MoveSelectedBranch(-1);
    private void MoveBranchDown_OnClick(object sender, RoutedEventArgs e) => MoveSelectedBranch(1);

    private void MoveSelectedBranch(int offset)
    {
        if (BranchesListBox.SelectedItem is not BranchRow row) return;
        var index = _branchRows.IndexOf(row);
        var target = index + offset;
        if (target < 0 || target >= _branchRows.Count) return;
        _branchRows.Move(index, target);
        BranchesListBox.SelectedItem = row;
    }

    private void EditBranchCondition_OnClick(object sender, RoutedEventArgs e)
    {
        if (BranchesListBox.SelectedItem is not BranchRow row) return;
        var dialog = CreateConditionDialog(row.Branch.Condition);
        if (dialog.ShowDialog() != true || dialog.Condition is null) return;
        row.Branch.Condition = dialog.Condition;
        row.RefreshLabel();
        RefreshBranchEditor();
    }

    private void BranchesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshBranchEditor();

    private void RefreshBranchEditor()
    {
        _refreshing = true;
        var row = BranchesListBox.SelectedItem as BranchRow;
        BranchConditionTextBox.Text = row is null ? string.Empty : GamePropertyComputationFormatter.Format(row.Branch.Condition);
        var result = row?.Branch.Result;
        if (!ReferenceEquals(row, _lastSelectedBranch))
        {
            _branchUsesProperty = result?.GamePropertySource is not null;
            _lastSelectedBranch = row;
        }
        else if (result?.GamePropertySource is not null)
        {
            _branchUsesProperty = true;
        }
        BranchUsesPropertyCheckBox.IsChecked = _branchUsesProperty;
        EditBranchConditionButton.IsEnabled = row is not null;
        BranchSourcePanel.IsEnabled = row is not null && _branchUsesProperty;
        BranchSourceTextBox.Text = _branchUsesProperty ? _formatSource(result?.GamePropertySource) : string.Empty;
        BranchLiteralTextBox.Text = _branchUsesProperty ? string.Empty : result?.Value ?? string.Empty;
        BranchLiteralTextBox.IsEnabled = row is not null && !_branchUsesProperty;
        BranchUsesPropertyCheckBox.IsEnabled = row is not null && !_isReadOnly;
        _refreshing = false;
    }

    private void BranchOutputMode_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing || BranchesListBox.SelectedItem is not BranchRow row) return;
        _branchUsesProperty = BranchUsesPropertyCheckBox.IsChecked == true;
        var result = row.Branch.Result ??= new ProjectGamePropertyOperandDto { ValueKind = OutputValueKind, Value = string.Empty };
        if (_branchUsesProperty)
        {
            result.Value = null;
            result.ValueKind = null;
        }
        else
        {
            result.GamePropertySource = null!;
            result.ValueKind = OutputValueKind;
            result.Value ??= string.Empty;
        }
        RefreshBranchEditor();
        row.RefreshLabel();
    }

    private void BranchLiteralTextBox_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_refreshing || BranchesListBox.SelectedItem is not BranchRow row || BranchUsesPropertyCheckBox.IsChecked == true) return;
        row.Branch.Result ??= new ProjectGamePropertyOperandDto();
        row.Branch.Result.ValueKind = OutputValueKind;
        row.Branch.Result.GamePropertySource = null!;
        row.Branch.Result.Value = BranchLiteralTextBox.Text;
        row.RefreshLabel();
    }

    private void ChooseBranchSource_OnClick(object sender, RoutedEventArgs e)
    {
        if (BranchesListBox.SelectedItem is not BranchRow row) return;
        var selected = _chooseSource(row.Branch.Result?.GamePropertySource);
        if (selected is null) return;
        row.Branch.Result ??= new ProjectGamePropertyOperandDto();
        row.Branch.Result.GamePropertySource = selected;
        row.Branch.Result.Value = null;
        row.Branch.Result.ValueKind = null;
        _branchUsesProperty = true;
        RefreshBranchEditor();
        row.RefreshLabel();
    }

    private void RefreshElseEditor()
    {
        _refreshing = true;
        _elseUsesProperty |= _elseResult?.GamePropertySource is not null;
        ElseUsesPropertyCheckBox.IsChecked = _elseUsesProperty;
        ElseLiteralTextBox.Text = _elseUsesProperty ? string.Empty : _elseResult?.Value ?? string.Empty;
        ElseLiteralTextBox.Visibility = _elseUsesProperty ? Visibility.Collapsed : Visibility.Visible;
        ElseSourceTextBlock.Text = _elseUsesProperty ? _formatSource(_elseResult?.GamePropertySource) : string.Empty;
        ElseSourceTextBlock.Visibility = _elseUsesProperty ? Visibility.Visible : Visibility.Collapsed;
        ChooseElseSourceButton.Visibility = _elseUsesProperty ? Visibility.Visible : Visibility.Collapsed;
        _refreshing = false;
    }

    private void ElseOutputMode_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        _elseUsesProperty = ElseUsesPropertyCheckBox.IsChecked == true;
        if (_elseUsesProperty)
            _elseResult = new ProjectGamePropertyOperandDto();
        else
            _elseResult = new ProjectGamePropertyOperandDto { ValueKind = OutputValueKind, Value = string.Empty };
        RefreshElseEditor();
    }

    private void ElseLiteralTextBox_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_refreshing || ElseUsesPropertyCheckBox.IsChecked == true) return;
        _elseResult ??= new ProjectGamePropertyOperandDto();
        _elseResult.GamePropertySource = null!;
        _elseResult.ValueKind = OutputValueKind;
        _elseResult.Value = ElseLiteralTextBox.Text;
    }

    private void ChooseElseSource_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = _chooseSource(_elseResult?.GamePropertySource);
        if (selected is null) return;
        _elseResult = new ProjectGamePropertyOperandDto { GamePropertySource = selected };
        _elseUsesProperty = true;
        RefreshElseEditor();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isReadOnly)
        {
            DialogResult = true;
            return;
        }

        if (IsBoolean)
        {
            if (_booleanCondition is null)
            {
                ShowError("Add a condition tree before saving the Boolean computation.");
                return;
            }
        }
        else
        {
            if (_branchRows.Count == 0)
            {
                ShowError("Add at least one ordered result branch.");
                return;
            }
            if (_elseResult is null || (_elseResult.GamePropertySource is null && _elseResult.Value is null))
            {
                ShowError("Choose or enter the required else result.");
                return;
            }
            if (_branchRows.Any(static row => row.Branch.Condition is null || row.Branch.Result is null
                || row.Branch.Result.GamePropertySource is null && row.Branch.Result.Value is null))
            {
                ShowError("Each result branch needs a condition and a typed output.");
                return;
            }
            if (OutputValueKind == "Numeric" && (_elseResult.GamePropertySource is null && !IsFinite(_elseResult.Value)
                || _branchRows.Any(row => row.Branch.Result?.GamePropertySource is null && !IsFinite(row.Branch.Result?.Value))))
            {
                ShowError("Numeric outputs must be finite invariant-culture numbers.");
                return;
            }
        }
        DialogResult = true;
    }

    private static bool IsFinite(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number);

    private void ShowError(string message) => System.Windows.MessageBox.Show(this, message, "Computed Game Property", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
