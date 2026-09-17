using System.Collections.ObjectModel;
using System.Windows;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class EventBindingVariableAssistDialog : Window
{
    public sealed record ActionOutputVariableDefinition(
        CommandActionType ActionType,
        string ActionOutputVariableKey,
        string Description,
        string ValueType,
        string Notes);

    public sealed record AnchorSubPropertyDefinition(
        string AnchorKey,
        string PropertyKey,
        string SourcePath,
        string ExpectedScopeType)
    {
        public string DisplayText => $"{PropertyKey} ({ExpectedScopeType})";
    }

    private readonly List<KnownPathItem> _allKnownPaths;
    private readonly List<AnchorSubPropertyDefinition> _allAnchorSubProperties;
    private readonly List<ActionOutputVariableItem> _allActionOutputVariables;
    private readonly List<ScopeObjectItem> _allScopeObjects;
    private readonly List<VariableChoiceRow> _allVariableRows;

    public EventBindingVariableAssistDialog(
        IReadOnlyList<string>? anchorOptions,
        IReadOnlyList<KeyValuePair<string, string>>? knownPayloadMappings,
        IReadOnlyList<AnchorSubPropertyDefinition>? anchorSubProperties,
        IReadOnlyList<ActionOutputVariableDefinition>? actionOutputVariables,
        IReadOnlyList<GamePropertyChoiceItem>? variableChoices,
        string? selectedValue = null)
    {
        InitializeComponent();

        AnchorOptions = NormalizeValues(anchorOptions);
        _allKnownPaths = NormalizeKnownPayloadMappings(knownPayloadMappings)
            .Select(static mapping => new KnownPathItem(mapping.Key, mapping.Value))
            .ToList();
        _allAnchorSubProperties = NormalizeAnchorSubProperties(anchorSubProperties).ToList();
        _allActionOutputVariables = NormalizeActionOutputVariables(actionOutputVariables)
            .Select(static item => new ActionOutputVariableItem(
                item.ActionType,
                item.ActionOutputVariableKey,
                BuildCurrentActionOutputPath(item.ActionOutputVariableKey),
                item.Description,
                item.ValueType,
                item.Notes))
            .ToList();

        _allVariableRows = BuildVariableRows(variableChoices);
        _allScopeObjects = _allVariableRows
            .GroupBy(static row => row.ScopeObjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new ScopeObjectItem(
                group.First().ScopeObjectKey,
                group.First().ScopeObjectDisplayName,
                group.First().Scope,
                group.First().ScopePath,
                group.First().ScopeObjectOwnerContext,
                group.Select(static row => row.Relation).Distinct().ToList()))
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TemplateAnchorComboBox.ItemsSource = AnchorOptions;
        TemplateAnchorComboBox.SelectedItem = null;

        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            SelectedPathTextBox.Text = selectedValue.Trim();
        }

        RefreshKnownPaths();
        RefreshActionOutputVariables();
        RefreshTemplateSubProperties();
        RefreshScopeObjects();
        RefreshVariableRows();
    }

    public IReadOnlyList<string> AnchorOptions { get; }

    public string? SelectedVariablePath { get; private set; }

    private void TemplateAnchorComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshTemplateSubProperties();
        RefreshScopeObjects();
        RefreshVariableRows();
        RefreshSelectedPathFromVariableSelection();
    }

    private void TemplateSubPropertyComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshScopeObjects();
        RefreshVariableRows();
        RefreshSelectedPathFromVariableSelection();
    }

    private void KnownPathFilterTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshKnownPaths();
    }

    private void KnownPathsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (KnownPathsListBox.SelectedItem is not KnownPathItem selected)
        {
            return;
        }

        SelectedPathTextBox.Text = selected.PayloadKey;
    }

    private void KnownPathsListBox_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        UseCurrentTextOrSelection();
    }

    private void ActionOutputFilterTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshActionOutputVariables();
    }

    private void ActionOutputListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ActionOutputListBox.SelectedItem is not ActionOutputVariableItem selected)
        {
            return;
        }

        SelectedPathTextBox.Text = selected.VariablePath;
    }

    private void ActionOutputListBox_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        UseCurrentTextOrSelection();
    }

    private void ScopeObjectComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshVariableRows();
        RefreshSelectedPathFromVariableSelection();
    }

    private void ChooseScopeObjectButton_OnClick(object sender, RoutedEventArgs e)
    {
        var expectedScope = GetExpectedScopeFromSelectedSubProperty();
        var dialogChoices = _allScopeObjects
            .Select(static item => new ScopeObjectChoiceItem
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                ScopePath = item.ScopePath,
                OwnerContext = item.OwnerContext,
                Scope = item.Scope,
                Relations = item.Relations
            })
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedKey = (ScopeObjectComboBox.SelectedItem as ScopeObjectItem)?.Key;
        var dialog = new ScopeObjectChooserDialog(dialogChoices, "Choose Scope Object", expectedScope, selectedKey)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedKey))
        {
            return;
        }

        var selectedFromDialog = _allScopeObjects.FirstOrDefault(item =>
            string.Equals(item.Key, dialog.SelectedKey, StringComparison.OrdinalIgnoreCase));
        if (selectedFromDialog is null)
        {
            return;
        }

        var currentItems = (ScopeObjectComboBox.ItemsSource as IEnumerable<ScopeObjectItem>)
            ?.ToList() ?? new List<ScopeObjectItem>();

        var selectedItem = currentItems.FirstOrDefault(item =>
            string.Equals(item.Key, selectedFromDialog.Key, StringComparison.OrdinalIgnoreCase));

        if (selectedItem is null)
        {
            currentItems.Insert(0, selectedFromDialog);
            ScopeObjectComboBox.ItemsSource = currentItems;
            selectedItem = selectedFromDialog;
        }

        ScopeObjectComboBox.SelectedItem = selectedItem;
        ScopeObjectComboBox.Focus();
    }

    private void VariableFilterTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshVariableRows();
    }

    private void VariablesDataGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshSelectedPathFromVariableSelection();
    }

    private void VariablesDataGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        UseCurrentTextOrSelection();
    }

    private void SelectedPathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        KnownPathsListBox.SelectedItem = null;
    }

    private void UseValue_OnClick(object sender, RoutedEventArgs e)
    {
        UseCurrentTextOrSelection();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UseCurrentTextOrSelection()
    {
        var value = SelectedPathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (KnownPathsListBox.SelectedItem is KnownPathItem selectedKnown)
            {
                value = selectedKnown.PayloadKey;
            }
            else if (VariablesDataGrid.SelectedItem is VariableChoiceRow selectedVariable)
            {
                value = BuildTemplateVariablePath(selectedVariable.VariableName);
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SelectedVariablePath = value;
        DialogResult = true;
    }

    private void RefreshKnownPaths()
    {
        var filter = KnownPathFilterTextBox.Text?.Trim() ?? string.Empty;

        var filtered = _allKnownPaths
            .Where(item => string.IsNullOrWhiteSpace(filter)
                || item.PayloadKey.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.SourcePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.PayloadKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        KnownPathsListBox.ItemsSource = filtered;
        if (filtered.Count > 0)
        {
            KnownPathsListBox.SelectedIndex = 0;
        }
    }

    private void RefreshActionOutputVariables()
    {
        var filter = ActionOutputFilterTextBox.Text?.Trim() ?? string.Empty;
        var filtered = _allActionOutputVariables
            .Where(item => string.IsNullOrWhiteSpace(filter)
                || item.ActionOutputVariableKey.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.ValueType.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.ActionType.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.ActionType.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.ActionOutputVariableKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ActionOutputListBox.ItemsSource = filtered;
        if (filtered.Count > 0)
        {
            ActionOutputListBox.SelectedIndex = 0;
        }
    }

    private void RefreshVariableRows()
    {
        var selectedSubProperty = GetSelectedTemplateSubProperty();
        var selectedScope = ScopeObjectComboBox.SelectedItem as ScopeObjectItem;

        if (selectedSubProperty is null || selectedScope is null)
        {
            VariableFilterTextBox.IsEnabled = false;
            VariablesDataGrid.IsEnabled = false;
            VariablesDataGrid.ItemsSource = Array.Empty<VariableChoiceRow>();
            VariablesDataGrid.SelectedItem = null;
            return;
        }

        VariableFilterTextBox.IsEnabled = true;
        VariablesDataGrid.IsEnabled = true;

        var variableFilter = VariableFilterTextBox.Text?.Trim() ?? string.Empty;
        var expectedScope = GetExpectedScopeFromSelectedSubProperty();

        var filtered = _allVariableRows
            .Where(row => expectedScope is null || row.Scope == expectedScope.Value)
            .Where(row => string.Equals(row.ScopeObjectKey, selectedScope.Key, StringComparison.OrdinalIgnoreCase))
            .Where(row => string.IsNullOrWhiteSpace(variableFilter)
                || row.VariableName.Contains(variableFilter, StringComparison.OrdinalIgnoreCase)
                || row.OwnerVariable.Contains(variableFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static row => GetIntrinsicLeafPriority(row.VariableName))
            .ThenBy(static row => row.VariableName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        VariablesDataGrid.ItemsSource = filtered;
        if (filtered.Count > 0)
        {
            VariablesDataGrid.SelectedIndex = 0;
        }
    }

    private void RefreshSelectedPathFromVariableSelection()
    {
        if (VariablesDataGrid.SelectedItem is not VariableChoiceRow selected)
        {
            return;
        }

        SelectedPathTextBox.Text = BuildTemplateVariablePath(selected.VariableName);
    }

    private string GetSelectedTemplateAnchor()
    {
        return (TemplateAnchorComboBox.SelectedItem as string)?.Trim() ?? string.Empty;
    }

    private AnchorSubPropertyDefinition? GetSelectedTemplateSubProperty()
    {
        return TemplateSubPropertyComboBox.SelectedItem as AnchorSubPropertyDefinition;
    }

    private string BuildTemplateVariablePath(string variableName)
    {
        var selectedSubProperty = GetSelectedTemplateSubProperty();
        if (selectedSubProperty is not null)
        {
            return BuildPathForAnchor(selectedSubProperty.AnchorKey, selectedSubProperty.PropertyKey, variableName);
        }

        return BuildPathForAnchor(GetSelectedTemplateAnchor(), string.Empty, variableName);
    }

    private static string BuildPathForAnchor(string anchor, string subProperty, string variableName)
    {
        var safeSubProperty = subProperty.Trim();
        var safeVariable = variableName.Trim();

        if (string.IsNullOrWhiteSpace(anchor))
        {
            return string.IsNullOrWhiteSpace(safeSubProperty)
                ? safeVariable
                : string.IsNullOrWhiteSpace(safeVariable)
                    ? safeSubProperty
                    : $"{safeSubProperty}.{safeVariable}";
        }

        return VariableReferenceSyntax.BuildAnchoredReference(anchor, safeSubProperty, safeVariable);
    }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<KeyValuePair<string, string>> NormalizeKnownPayloadMappings(
        IReadOnlyList<KeyValuePair<string, string>>? mappings)
    {
        return (mappings ?? Array.Empty<KeyValuePair<string, string>>())
            .Select(static mapping => new KeyValuePair<string, string>(
                mapping.Key?.Trim() ?? string.Empty,
                mapping.Value?.Trim() ?? string.Empty))
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.Key)
                && !string.IsNullOrWhiteSpace(mapping.Value))
            .Distinct(KnownPayloadMappingComparer.Instance)
            .OrderBy(static mapping => mapping.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static mapping => mapping.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AnchorSubPropertyDefinition> NormalizeAnchorSubProperties(
        IReadOnlyList<AnchorSubPropertyDefinition>? subProperties)
    {
        return (subProperties ?? Array.Empty<AnchorSubPropertyDefinition>())
            .Select(static item => new AnchorSubPropertyDefinition(
                item.AnchorKey?.Trim() ?? string.Empty,
                item.PropertyKey?.Trim() ?? string.Empty,
                item.SourcePath?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(item.ExpectedScopeType) ? "Unknown" : item.ExpectedScopeType.Trim()))
            .Where(static item => !string.IsNullOrWhiteSpace(item.AnchorKey)
                && !string.IsNullOrWhiteSpace(item.PropertyKey))
            .Distinct(AnchorSubPropertyComparer.Instance)
            .OrderBy(static item => item.AnchorKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.PropertyKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<VariableChoiceRow> BuildVariableRows(IReadOnlyList<GamePropertyChoiceItem>? choices)
    {
        return (choices ?? Array.Empty<GamePropertyChoiceItem>())
            .Select(static choice => new VariableChoiceRow(
                ScopePath: choice.ScopePath,
                OwnerVariable: choice.OwnerVariable,
                VariableName: ExtractVariableName(choice.Value),
                Scope: choice.Scope,
                Relation: choice.Relation,
                ScopeObjectKey: BuildScopeObjectKey(choice.ScopePath, choice.OwnerVariable),
                ScopeObjectDisplayName: BuildScopeObjectDisplayName(choice.ScopePath, choice.OwnerVariable),
                ScopeObjectOwnerContext: BuildScopeObjectOwnerContext(choice.OwnerVariable)))
            .Where(static row => !string.IsNullOrWhiteSpace(row.VariableName))
            .Distinct(VariableChoiceRowComparer.Instance)
            .ToList();
    }

    private void RefreshTemplateSubProperties()
    {
        var selectedAnchor = GetSelectedTemplateAnchor();
        var items = _allAnchorSubProperties
            .Where(item => string.Equals(item.AnchorKey, selectedAnchor, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.PropertyKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TemplateSubPropertyComboBox.IsEnabled = !string.IsNullOrWhiteSpace(selectedAnchor);
        TemplateSubPropertyComboBox.ItemsSource = items;
        TemplateSubPropertyComboBox.SelectedItem = items.FirstOrDefault(item =>
            string.Equals(item.PropertyKey, "scope", StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshScopeObjects()
    {
        var selectedSubProperty = GetSelectedTemplateSubProperty();
        if (selectedSubProperty is null)
        {
            ScopeObjectComboBox.ItemsSource = Array.Empty<ScopeObjectItem>();
            ScopeObjectComboBox.SelectedItem = null;
            ScopeObjectComboBox.IsEnabled = false;
            ChooseScopeObjectButton.IsEnabled = false;
            return;
        }

        ScopeObjectComboBox.IsEnabled = true;
        ChooseScopeObjectButton.IsEnabled = true;

        var previous = ScopeObjectComboBox.SelectedItem as ScopeObjectItem;
        var expectedScope = GetExpectedScopeFromSelectedSubProperty();

        var scopeObjects = _allScopeObjects
            .Where(item => item.Relations.Any(IsLikelyQuickPickRelation))
            .Where(item => expectedScope is null || item.Scope == expectedScope.Value)
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ScopeObjectComboBox.ItemsSource = scopeObjects;
        if (scopeObjects.Count == 0)
        {
            ScopeObjectComboBox.SelectedItem = null;
            return;
        }

        if (previous is not null)
        {
            var restored = scopeObjects.FirstOrDefault(item =>
                string.Equals(item.Key, previous.Key, StringComparison.OrdinalIgnoreCase));
            if (restored is not null)
            {
                ScopeObjectComboBox.SelectedItem = restored;
                return;
            }
        }

        ScopeObjectComboBox.SelectedIndex = 0;
    }

    private static int GetIntrinsicLeafPriority(string variableName)
    {
        return variableName.Trim().ToLowerInvariant() switch
        {
            "nameingame" => 0,
            "name" => 1,
            _ => 2
        };
    }

    private static bool IsLikelyQuickPickRelation(GamePropertyChoiceRelation relation)
    {
        return relation switch
        {
            GamePropertyChoiceRelation.Self => true,
            GamePropertyChoiceRelation.Parent => true,
            GamePropertyChoiceRelation.Ancestor => true,
            _ => false
        };
    }

    private PropertyResolutionScope? GetExpectedScopeFromSelectedSubProperty()
    {
        var selectedSubProperty = GetSelectedTemplateSubProperty();
        if (selectedSubProperty is null)
        {
            return null;
        }

        var expectedType = selectedSubProperty.ExpectedScopeType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedType)
            || string.Equals(expectedType, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return expectedType.ToUpperInvariant() switch
        {
            "GLOBAL" => PropertyResolutionScope.Global,
            "PLANET" => PropertyResolutionScope.Planet,
            "COUNTRY" => PropertyResolutionScope.Country,
            "AREA" => PropertyResolutionScope.Area,
            "ROOM" => PropertyResolutionScope.Room,
            "OBJECT" => PropertyResolutionScope.Object,
            "GAMEOBJECT" => PropertyResolutionScope.Object,
            _ => null
        };
    }

    private static string ExtractVariableName(string value)
    {
        var safe = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safe))
        {
            return string.Empty;
        }

        var lastDot = safe.LastIndexOf('.');
        if (lastDot < 0 || lastDot + 1 >= safe.Length)
        {
            return safe;
        }

        return safe[(lastDot + 1)..];
    }

    private static string BuildCurrentActionOutputPath(string actionOutputVariableKey)
    {
        var normalized = actionOutputVariableKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (VariableReferenceSyntax.TryParseAnchoredReference(normalized, out _, out _))
        {
            return normalized;
        }

        if (normalized.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["currentAction.".Length..];
        }

        return VariableReferenceSyntax.BuildAnchoredReference("currentAction", normalized);
    }

    private static string BuildScopeObjectKey(string scopePath, string ownerVariable)
    {
        var owner = ownerVariable?.Trim() ?? string.Empty;
        var ownerPrefix = owner;
        var lastDot = owner.LastIndexOf('.');
        if (lastDot > 0)
        {
            ownerPrefix = owner[..lastDot];
        }

        return $"{scopePath}|{ownerPrefix}";
    }

    private static string BuildScopeObjectDisplayName(string scopePath, string ownerVariable)
    {
        var owner = ownerVariable?.Trim() ?? string.Empty;
        var ownerPrefix = owner;
        var lastDot = owner.LastIndexOf('.');
        if (lastDot > 0)
        {
            ownerPrefix = owner[..lastDot];
        }

        return string.IsNullOrWhiteSpace(ownerPrefix)
            ? scopePath
            : $"{ownerPrefix} ({scopePath})";
    }

    private static string BuildScopeObjectOwnerContext(string ownerVariable)
    {
        var owner = ownerVariable?.Trim() ?? string.Empty;
        var lastDot = owner.LastIndexOf('.');
        if (lastDot > 0)
        {
            return owner[..lastDot];
        }

        return owner;
    }

    private sealed record ScopeObjectItem(
        string Key,
        string DisplayName,
        PropertyResolutionScope Scope,
        string ScopePath,
        string OwnerContext,
        IReadOnlyList<GamePropertyChoiceRelation> Relations);

    private sealed record ActionOutputVariableItem(
        CommandActionType ActionType,
        string ActionOutputVariableKey,
        string VariablePath,
        string Description,
        string ValueType,
        string Notes)
    {
        public string DisplayText =>
            $"{ActionType}: {VariablePath} ({ValueType}) - {(string.IsNullOrWhiteSpace(Description) ? "No description" : Description)}";
    }

    private sealed record KnownPathItem(string PayloadKey, string SourcePath)
    {
        public string DisplayText => $"{PayloadKey}  <-  {SourcePath}";
    }

    private sealed record VariableChoiceRow(
        string ScopePath,
        string OwnerVariable,
        string VariableName,
        PropertyResolutionScope Scope,
        GamePropertyChoiceRelation Relation,
        string ScopeObjectKey,
        string ScopeObjectDisplayName,
        string ScopeObjectOwnerContext);

    private sealed class VariableChoiceRowComparer : IEqualityComparer<VariableChoiceRow>
    {
        public static VariableChoiceRowComparer Instance { get; } = new();

        public bool Equals(VariableChoiceRow? x, VariableChoiceRow? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.ScopePath, y.ScopePath, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.OwnerVariable, y.OwnerVariable, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(x.VariableName, y.VariableName, StringComparison.OrdinalIgnoreCase)
                     && x.Scope == y.Scope;
        }

        public int GetHashCode(VariableChoiceRow obj)
        {
            return HashCode.Combine(
                obj.ScopePath?.ToUpperInvariant(),
                obj.OwnerVariable?.ToUpperInvariant(),
                obj.VariableName?.ToUpperInvariant(),
                obj.Scope);
        }
    }

    private sealed class AnchorSubPropertyComparer : IEqualityComparer<AnchorSubPropertyDefinition>
    {
        public static AnchorSubPropertyComparer Instance { get; } = new();

        public bool Equals(AnchorSubPropertyDefinition? x, AnchorSubPropertyDefinition? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.AnchorKey, y.AnchorKey, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.PropertyKey, y.PropertyKey, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.SourcePath, y.SourcePath, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.ExpectedScopeType, y.ExpectedScopeType, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(AnchorSubPropertyDefinition obj)
        {
            return HashCode.Combine(
                obj.AnchorKey?.ToUpperInvariant(),
                obj.PropertyKey?.ToUpperInvariant(),
                obj.SourcePath?.ToUpperInvariant(),
                obj.ExpectedScopeType?.ToUpperInvariant());
        }
    }

    private sealed class KnownPayloadMappingComparer : IEqualityComparer<KeyValuePair<string, string>>
    {
        public static KnownPayloadMappingComparer Instance { get; } = new();

        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(KeyValuePair<string, string> obj)
        {
            return HashCode.Combine(obj.Key?.ToUpperInvariant(), obj.Value?.ToUpperInvariant());
        }
    }

    private static IReadOnlyList<ActionOutputVariableDefinition> NormalizeActionOutputVariables(
        IReadOnlyList<ActionOutputVariableDefinition>? variables)
    {
        return (variables ?? Array.Empty<ActionOutputVariableDefinition>())
            .Select(static item => new ActionOutputVariableDefinition(
                item.ActionType,
                item.ActionOutputVariableKey?.Trim() ?? string.Empty,
                item.Description?.Trim() ?? string.Empty,
                item.ValueType?.Trim() ?? string.Empty,
                item.Notes?.Trim() ?? string.Empty))
            .Where(static item => !string.IsNullOrWhiteSpace(item.ActionOutputVariableKey))
            .GroupBy(static item => $"{item.ActionType}|{item.ActionOutputVariableKey}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static item => item.ActionType.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.ActionOutputVariableKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
