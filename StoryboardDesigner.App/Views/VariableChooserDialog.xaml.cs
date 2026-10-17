using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class VariableChooserDialog : Window
{
    private const string AllFilterValue = "*";
    private readonly List<GamePropertyChoiceItem> _allChoices;
    private readonly ICollectionView _choicesView;
    private readonly Guid? _relativeOwnerId;
    private readonly IReadOnlyList<string> _anchorKeys;
    private readonly bool _allowReferenceModes;
    private readonly bool _allowFixedReferenceMode;
    private bool _allowSelf = true;
    private bool _allowChildren = true;
    private bool _allowDescendants = true;
    private bool _allowParents = true;
    private bool _allowAncestors = true;
    private bool _allowSiblings = true;
    private bool _allowTraversalLegs = true;
    private bool _allowPhases = true;
    private bool _allowGlobal = true;
    private bool _allowUnrelated = true;
    private bool _hasUnresolvedSelectedReference;
    private bool _isConfiguringFilters;

    private VariableChooserDialog(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        PropertyResolutionScope initializationScope,
        string title,
        string? selectedValue = null,
        bool allowReferenceModes = false,
        Guid? relativeOwnerId = null,
        IReadOnlyList<string>? anchorKeys = null,
        ProjectGamePropertySourceReferenceDto? selectedReference = null,
        bool allowFixedReferenceMode = true)
    {
        InitializeComponent();
        Title = title;
        _allowReferenceModes = allowReferenceModes;
        _allowFixedReferenceMode = allowFixedReferenceMode;
        _relativeOwnerId = relativeOwnerId;
        _anchorKeys = (anchorKeys ?? Array.Empty<string>()).Where(static key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _allChoices = BuildDistinctDisplayChoices(choices, selectedValue);
        _choicesView = CollectionViewSource.GetDefaultView(_allChoices);
        _choicesView.Filter = choice => ChoiceMatchesFilter(choice);

        ChoicesDataGrid.ItemsSource = _choicesView;
        ConfigureScopeDefaults();
        if (!string.IsNullOrWhiteSpace(selectedValue)
            && _allChoices.FirstOrDefault(choice => string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase)) is { } selectedChoice)
        {
            EnableRelation(selectedChoice.Relation);
        }
        ConfigureReferenceModes(selectedReference);
        ApplyFilter();

        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            var match = _allChoices.FirstOrDefault(choice => string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ChoicesDataGrid.SelectedItem = match;
                ChoicesDataGrid.ScrollIntoView(match);
            }
        }

        ScopeFilterComboBox.Focus();
    }

    public VariableChooserDialog(
        GamePropertyPickerContext context,
        string title,
        string? selectedValue = null,
        bool allowSelf = true,
        bool allowChildren = true,
        bool allowDescendants = true,
        bool allowParents = true,
        bool allowAncestors = true,
        bool allowSiblings = true,
        bool allowTraversalLegs = true,
        bool allowPhases = true,
        bool allowGlobal = true,
        bool allowUnrelated = true)
        : this(
            context.DiscoverChoices(context.AllowReferenceModes),
            context.InitializationScope,
            title,
            selectedValue,
            context.AllowReferenceModes,
            context.RelativeOwnerId,
            context.AnchorKeys,
            context.SelectedReference,
            context.AllowFixedReferenceMode)
    {
        _allowSelf = allowSelf;
        _allowChildren = allowChildren;
        _allowDescendants = allowDescendants;
        _allowParents = allowParents;
        _allowAncestors = allowAncestors;
        _allowSiblings = allowSiblings;
        _allowTraversalLegs = allowTraversalLegs;
        _allowPhases = allowPhases;
        _allowGlobal = allowGlobal;
        _allowUnrelated = allowUnrelated;
        ConfigureAllowedScopes();
        ApplyFilter();
    }

    public string? SelectedValue { get; private set; }
    public GamePropertyChoiceItem? SelectedChoice { get; private set; }
    public ProjectGamePropertySourceReferenceDto? SelectedReference { get; private set; }

    private void ConfigureReferenceModes(ProjectGamePropertySourceReferenceDto? selectedReference)
    {
        if (!_allowReferenceModes)
        {
            return;
        }

        ReferenceModePanel.Visibility = Visibility.Visible;
        AnchorComboBox.ItemsSource = _anchorKeys;
        AnchorComboBox.SelectedItem = selectedReference?.AnchorKey is string selectedAnchor
            ? _anchorKeys.FirstOrDefault(key => string.Equals(key, selectedAnchor, StringComparison.OrdinalIgnoreCase))
            : _anchorKeys.FirstOrDefault();
        GamePropertyChoiceItem? matchedReferenceChoice = null;
        if (selectedReference is not null)
        {
            var propertyName = selectedReference.PropertyName ?? string.Empty;
            matchedReferenceChoice = _allChoices.FirstOrDefault(choice =>
                string.Equals(choice.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase)
                && (selectedReference.OwnerId.HasValue
                    ? choice.OwnerId == selectedReference.OwnerId
                    : selectedReference.AnchorKey is not null
                        ? choice.EligibleAnchors.Contains(selectedReference.AnchorKey, StringComparer.OrdinalIgnoreCase)
                        : choice.OwnerId == _relativeOwnerId));
            _hasUnresolvedSelectedReference = matchedReferenceChoice is null;
            if (matchedReferenceChoice is not null)
            {
                ChoicesDataGrid.SelectedItem = matchedReferenceChoice;
                ChoicesDataGrid.ScrollIntoView(matchedReferenceChoice);
            }
        }
        if (selectedReference?.AnchorKey is not null)
        {
            ReferenceModeComboBox.SelectedIndex = 2;
        }
        else if (!selectedReference?.OwnerId.HasValue ?? true)
        {
            ReferenceModeComboBox.SelectedIndex = 1;
        }
        else
        {
            ReferenceModeComboBox.SelectedIndex = 0;
        }

        if (ReferenceModeComboBox.SelectedIndex == 0 && matchedReferenceChoice is not null)
        {
            EnableRelation(matchedReferenceChoice.Relation);
        }

        if (ReferenceModeComboBox.Items.Count > 1 && ReferenceModeComboBox.Items[1] is System.Windows.Controls.ComboBoxItem ownerRelativeMode)
        {
            ownerRelativeMode.IsEnabled = _relativeOwnerId.HasValue;
        }
        if (ReferenceModeComboBox.Items.Count > 0 && ReferenceModeComboBox.Items[0] is System.Windows.Controls.ComboBoxItem fixedMode)
        {
            fixedMode.IsEnabled = _allowFixedReferenceMode || selectedReference?.OwnerId.HasValue == true;
        }
        if (ReferenceModeComboBox.Items.Count > 2 && ReferenceModeComboBox.Items[2] is System.Windows.Controls.ComboBoxItem anchorMode)
        {
            anchorMode.IsEnabled = _anchorKeys.Count > 0;
        }

        UpdateReferenceModeState();
    }

    private void ReferenceMode_OnChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateReferenceModeState();
        ApplyFilter();
    }

    private void Anchor_OnChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateReferencePreview();
        ApplyFilter();
    }

    private void ChoicesDataGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_allowReferenceModes)
        {
            return;
        }

        UpdateReferenceModeState();
    }

    private void UpdateReferenceModeState()
    {
        var mode = ReferenceModeComboBox.SelectedIndex;
        AnchorLabel.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        AnchorComboBox.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        UpdateReferencePreview();
    }

    private void UpdateReferencePreview()
    {
        if (!_allowReferenceModes)
        {
            return;
        }

        var choice = ChoicesDataGrid.SelectedItem as GamePropertyChoiceItem;
        var mode = ReferenceModeComboBox.SelectedIndex;
        var propertyName = choice?.PropertyName ?? choice?.OwnerVariable.Split('.').LastOrDefault() ?? "property";
        var hasChoice = choice is not null;
        ReferencePreviewTextBlock.Text = mode switch
        {
            1 => hasChoice ? "Owner relative → " + propertyName : "Choose a property on the current owner",
            2 => hasChoice
                ? "Anchor(" + (AnchorComboBox.SelectedItem as string ?? "choose") + ") → " + propertyName
                : "Choose a property available through " + (AnchorComboBox.SelectedItem as string ?? "an anchor"),
            _ => hasChoice ? "Fixed → " + choice!.OwnerVariable : "Choose a property"
        };
    }

    private void ChoicesDataGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TrySelectCurrent();
    }

    private void ChoicesDataGrid_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            TrySelectCurrent();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        TrySelectCurrent();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ScopeOptions_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_allowReferenceModes
            && ReferenceModeComboBox.SelectedIndex == 1
            && (IncludeChildrenCheckBox.IsChecked == true
                || IncludeDescendantsCheckBox.IsChecked == true
                || IncludeParentCheckBox.IsChecked == true
                || IncludeAncestorsCheckBox.IsChecked == true
                || IncludeSiblingsCheckBox.IsChecked == true
                || IncludeTraversalLegsCheckBox.IsChecked == true
                || IncludePhaseVariablesCheckBox.IsChecked == true
                || IncludeGlobalCheckBox.IsChecked == true
                || IncludeOtherScopesCheckBox.IsChecked == true))
        {
            // Owner-relative references can only target the current owner. When the
            // user expands the scope filters, switch to fixed owner references so
            // those newly visible choices can be selected.
            ReferenceModeComboBox.SelectedIndex = 0;
        }

        ApplyFilter();
    }

    private void ScopeFilterComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isConfiguringFilters)
        {
            return;
        }

        ApplyFilter();
    }

    private void PropertyFilterComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isConfiguringFilters)
        {
            return;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ConfigureFilterOptions();
        var previous = ChoicesDataGrid.SelectedItem as GamePropertyChoiceItem;
        _choicesView.Refresh();
        UpdateEmptyState();

        if (previous is not null && _choicesView.Cast<GamePropertyChoiceItem>().Any(item => item == previous))
        {
            ChoicesDataGrid.SelectedItem = previous;
            return;
        }

        if (!_hasUnresolvedSelectedReference && _choicesView.Cast<GamePropertyChoiceItem>().Any())
        {
            ChoicesDataGrid.SelectedIndex = 0;
        }
    }

    private void UpdateEmptyState()
    {
        if (ChoicesDataGrid.Items.Count > 0)
        {
            ChoiceEmptyTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        ChoiceEmptyTextBlock.Text = _hasUnresolvedSelectedReference && ChoicesDataGrid.SelectedItem is null
            ? "The saved source is missing or no longer matches this property list. It will be preserved unless you select a replacement."
            : _allowReferenceModes && ReferenceModeComboBox.SelectedIndex == 2
                ? $"No property is currently available through '{AnchorComboBox.SelectedItem ?? "the selected anchor"}'. Add a property to an eligible owner or choose another anchor."
                : _allowReferenceModes && ReferenceModeComboBox.SelectedIndex == 1
                    ? "The current owner has no properties matching these filters."
                    : "No properties match these filters.";
        ChoiceEmptyTextBlock.Visibility = Visibility.Visible;
    }

    private bool ChoiceMatchesFilter(
        object obj,
        bool ignoreScopeFilter = false,
        bool ignorePropertyFilter = false)
    {
        if (obj is not GamePropertyChoiceItem choice)
        {
            return false;
        }

        if (!IsRelationAllowed(choice.Relation))
        {
            return false;
        }

        var selectedScope = ScopeFilterComboBox.SelectedValue as string;
        if (!ignoreScopeFilter
            && !string.IsNullOrWhiteSpace(selectedScope)
            && !string.Equals(selectedScope, AllFilterValue, StringComparison.Ordinal)
            && !string.Equals(choice.OwnerLabel, selectedScope, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selectedProperty = PropertyFilterComboBox.SelectedValue as string;
        if (!ignorePropertyFilter
            && !string.IsNullOrWhiteSpace(selectedProperty)
            && !string.Equals(selectedProperty, AllFilterValue, StringComparison.Ordinal)
            && !string.Equals(GetPropertyName(choice), selectedProperty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var explicitAnchorScope = _allowReferenceModes && ReferenceModeComboBox.SelectedIndex == 2;
        if (!explicitAnchorScope && !IsRelationEnabled(choice.Relation))
        {
            return false;
        }

        if (_allowReferenceModes)
        {
            if (ReferenceModeComboBox.SelectedIndex == 1 && _relativeOwnerId.HasValue && choice.OwnerId != _relativeOwnerId)
            {
                return false;
            }

            if (ReferenceModeComboBox.SelectedIndex == 2)
            {
                var selectedAnchor = AnchorComboBox.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(selectedAnchor)
                    || !choice.EligibleAnchors.Contains(selectedAnchor, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ConfigureFilterOptions()
    {
        var selectedScope = ScopeFilterComboBox.SelectedValue as string;
        var scopes = _allChoices
            .Where(choice => ChoiceMatchesFilter(choice, ignoreScopeFilter: true, ignorePropertyFilter: true))
            .Select(choice => choice.OwnerLabel)
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scopeOptions = new List<FilterOption>
        {
            new("All scopes", AllFilterValue)
        };
        scopeOptions.AddRange(scopes.Select(scope => new FilterOption(scope, scope)));

        _isConfiguringFilters = true;
        ScopeFilterComboBox.ItemsSource = scopeOptions;
        ScopeFilterComboBox.SelectedValue = scopes.Any(scope => string.Equals(scope, selectedScope, StringComparison.OrdinalIgnoreCase))
            ? selectedScope
            : AllFilterValue;

        var selectedProperty = PropertyFilterComboBox.SelectedValue as string;
        var properties = _allChoices
            .Where(choice => ChoiceMatchesFilter(choice, ignorePropertyFilter: true))
            .Select(GetPropertyName)
            .Where(static property => !string.IsNullOrWhiteSpace(property))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static property => property, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var propertyOptions = new List<FilterOption>
        {
            new("All properties", AllFilterValue)
        };
        propertyOptions.AddRange(properties.Select(property => new FilterOption(property, property)));

        PropertyFilterComboBox.ItemsSource = propertyOptions;
        PropertyFilterComboBox.SelectedValue = properties.Any(property => string.Equals(property, selectedProperty, StringComparison.OrdinalIgnoreCase))
            ? selectedProperty
            : AllFilterValue;
        _isConfiguringFilters = false;
    }

    private static string GetPropertyName(GamePropertyChoiceItem choice)
    {
        return choice.PropertyName
            ?? choice.OwnerVariable.Split('.').LastOrDefault()
            ?? choice.OwnerVariable;
    }

    private void ConfigureAllowedScopes()
    {
        IncludeSelfCheckBox.IsEnabled = _allowSelf;
        IncludeChildrenCheckBox.IsEnabled = _allowChildren;
        IncludeDescendantsCheckBox.IsEnabled = _allowDescendants;
        IncludeParentCheckBox.IsEnabled = _allowParents;
        IncludeAncestorsCheckBox.IsEnabled = _allowAncestors;
        IncludeSiblingsCheckBox.IsEnabled = _allowSiblings;
        IncludeTraversalLegsCheckBox.IsEnabled = _allowTraversalLegs;
        IncludePhaseVariablesCheckBox.IsEnabled = _allowPhases;
        IncludeGlobalCheckBox.IsEnabled = _allowGlobal;
        IncludeOtherScopesCheckBox.IsEnabled = _allowUnrelated;
    }

    private bool IsRelationAllowed(GamePropertyChoiceRelation relation) => relation switch
    {
        GamePropertyChoiceRelation.Self => _allowSelf,
        GamePropertyChoiceRelation.Child => _allowChildren,
        GamePropertyChoiceRelation.Descendant => _allowDescendants,
        GamePropertyChoiceRelation.Parent => _allowParents,
        GamePropertyChoiceRelation.Ancestor => _allowAncestors,
        GamePropertyChoiceRelation.Sibling => _allowSiblings,
        GamePropertyChoiceRelation.TraversalLeg => _allowTraversalLegs,
        GamePropertyChoiceRelation.Phase => _allowPhases,
        GamePropertyChoiceRelation.Global => _allowGlobal,
        _ => _allowUnrelated
    };

    private sealed record FilterOption(string Label, string Value);

    private void TrySelectCurrent()
    {
        if (ChoicesDataGrid.SelectedItem is not GamePropertyChoiceItem choice)
        {
            return;
        }

        SelectedValue = choice.Value;
        SelectedChoice = choice;
        if (_allowReferenceModes)
        {
            var propertyName = choice.PropertyName ?? choice.OwnerVariable.Split('.').LastOrDefault() ?? choice.Value;
            SelectedReference = ReferenceModeComboBox.SelectedIndex switch
            {
                1 when choice.OwnerId == _relativeOwnerId => new ProjectGamePropertySourceReferenceDto { PropertyName = propertyName },
                2 => new ProjectGamePropertySourceReferenceDto { AnchorKey = AnchorComboBox.SelectedItem as string, PropertyName = propertyName },
                _ => new ProjectGamePropertySourceReferenceDto { OwnerId = choice.OwnerId, PropertyName = propertyName }
            };
        }
        DialogResult = true;
    }

    private void ConfigureScopeDefaults()
    {
        // Keep the same scope filters available wherever the shared property picker is used.
        // Start with the current scope only; users opt into related scopes as needed.
        ScopeOptionsPanel.Visibility = Visibility.Visible;
        IncludeSelfCheckBox.IsChecked = true;
        IncludeChildrenCheckBox.IsChecked = false;
        IncludeDescendantsCheckBox.IsChecked = false;
        IncludeParentCheckBox.IsChecked = false;
        IncludeAncestorsCheckBox.IsChecked = false;
        IncludeSiblingsCheckBox.IsChecked = false;
        IncludeTraversalLegsCheckBox.IsChecked = false;
        IncludePhaseVariablesCheckBox.IsChecked = false;
        IncludeGlobalCheckBox.IsChecked = false;
        IncludeOtherScopesCheckBox.IsChecked = false;
    }

    private bool IsRelationEnabled(GamePropertyChoiceRelation relation)
    {
        return relation switch
        {
            GamePropertyChoiceRelation.Self => IncludeSelfCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Child => IncludeChildrenCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Descendant => IncludeDescendantsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Parent => IncludeParentCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Ancestor => IncludeAncestorsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Sibling => IncludeSiblingsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.TraversalLeg => IncludeTraversalLegsCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Phase => IncludePhaseVariablesCheckBox.IsChecked == true,
            GamePropertyChoiceRelation.Global => IncludeGlobalCheckBox.IsChecked == true,
            _ => IncludeOtherScopesCheckBox.IsChecked == true
        };
    }

    private void EnableRelation(GamePropertyChoiceRelation relation)
    {
        switch (relation)
        {
            case GamePropertyChoiceRelation.Self:
                IncludeSelfCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Child:
                IncludeChildrenCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Descendant:
                IncludeDescendantsCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Parent:
                IncludeParentCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Ancestor:
                IncludeAncestorsCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Sibling:
                IncludeSiblingsCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.TraversalLeg:
                IncludeTraversalLegsCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Phase:
                IncludePhaseVariablesCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Global:
                IncludeGlobalCheckBox.IsChecked = true;
                break;
            case GamePropertyChoiceRelation.Other:
                IncludeOtherScopesCheckBox.IsChecked = true;
                break;
        }
    }

    private static List<GamePropertyChoiceItem> BuildDistinctDisplayChoices(
        IReadOnlyList<GamePropertyChoiceItem> choices,
        string? selectedValue)
    {
        return choices
            .GroupBy(
                static choice => BuildDisplayKey(choice),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => SelectPreferredChoice(group, selectedValue))
            .OrderBy(static choice => choice.Priority)
            .ThenBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDisplayKey(GamePropertyChoiceItem choice)
    {
        return $"{choice.ScopePath}|{choice.OwnerVariable}";
    }

    private static GamePropertyChoiceItem SelectPreferredChoice(
        IEnumerable<GamePropertyChoiceItem> choices,
        string? selectedValue)
    {
        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            var selectedMatch = choices.FirstOrDefault(choice =>
                string.Equals(choice.Value, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (selectedMatch is not null)
            {
                return selectedMatch;
            }
        }

        return choices
            .OrderBy(static choice => choice.Priority)
            .ThenBy(static choice => choice.Value, StringComparer.OrdinalIgnoreCase)
            .First();
    }
}
