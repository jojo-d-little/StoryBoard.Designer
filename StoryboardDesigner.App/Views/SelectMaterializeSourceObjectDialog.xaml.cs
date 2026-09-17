using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class SelectGameObjectDialog : Window
{
    public sealed record Options(
        GameObjectOptionSourceTarget DefaultScopeSearchType = GameObjectOptionSourceTarget.RealObjects,
        ScopeSearchDepth DefaultScopeSearchDepth = ScopeSearchDepth.Project,
        GameObjectFeatureRequirements DefaultRequiredFeatures = GameObjectFeatureRequirements.None,
        Guid? DefaultCurrentObjectId = null,
        bool DefaultExcludeCurrentObject = true,
        bool IsScopeSearchTypeEditable = true,
        bool IsScopeSearchDepthEditable = true,
        bool IsRequiredFeaturesEditable = true,
        bool IsCurrentObjectEditable = true,
        bool IsExcludeCurrentObjectEditable = true);

    private sealed class CurrentObjectChoice
    {
        public required Guid ObjectId { get; init; }
        public GameObject? SourceObject { get; init; }
        public required string DisplayName { get; init; }
    }

    private readonly List<MaterializeSourceObjectChoiceItem> _choices;
    private readonly HashSet<Guid> _knownChoiceIds;
    private readonly IGameObjectSelectionOptionDiscoveryService _discoveryService;
    private ICollectionView? _choicesView;
    private readonly Options _options;
    private readonly HashSet<Guid> _discoveredObjectIds = new();
    private bool _isInitializing;

    public SelectGameObjectDialog(
        IReadOnlyList<MaterializeSourceObjectChoiceItem> choices,
        Guid? selectedObjectId = null,
        Options? options = null)
    {
        InitializeComponent();
        _options = options ?? new Options();
        _discoveryService = new GameObjectSelectionOptionDiscoveryService();
        _choices = choices
            .OrderBy(choice => choice.SourceCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _knownChoiceIds = _choices
            .Where(static choice => choice.ObjectId != Guid.Empty)
            .Select(static choice => choice.ObjectId)
            .ToHashSet();
        _isInitializing = true;
        _choicesView = CollectionViewSource.GetDefaultView(_choices);
        _choicesView.Filter = ChoiceMatchesFilter;
        ChoicesDataGrid.ItemsSource = _choicesView;

        SourceFilterComboBox.ItemsSource = Enum.GetValues<GameObjectOptionSourceTarget>();
        ScopeFilterComboBox.ItemsSource = Enum.GetValues<ScopeSearchDepth>();
        RequiredFeaturesComboBox.ItemsSource = new[]
        {
            GameObjectFeatureRequirements.None,
            GameObjectFeatureRequirements.Inventoriable,
            GameObjectFeatureRequirements.Openable,
            GameObjectFeatureRequirements.Lockable,
            GameObjectFeatureRequirements.Container,
            GameObjectFeatureRequirements.Activatable,
            GameObjectFeatureRequirements.Hidable,
            GameObjectFeatureRequirements.Quantifiable,
            GameObjectFeatureRequirements.CompositeTarget
        };
        CurrentObjectComboBox.ItemsSource = _choices
            .GroupBy(static choice => choice.ObjectId)
            .Select(static group => group.First())
            .OrderBy(static choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(choice => new CurrentObjectChoice
            {
                ObjectId = choice.ObjectId,
                SourceObject = choice.SourceObject,
                DisplayName = $"{choice.DisplayName} - {choice.ScopePath}"
            })
            .ToList();

        SourceFilterComboBox.SelectedItem = _options.DefaultScopeSearchType;
        ScopeFilterComboBox.SelectedItem = _options.DefaultScopeSearchDepth;
        RequiredFeaturesComboBox.SelectedItem = _options.DefaultRequiredFeatures;
        ExcludeCurrentObjectCheckBox.IsChecked = _options.DefaultExcludeCurrentObject;

        SourceFilterComboBox.IsEnabled = _options.IsScopeSearchTypeEditable;
        ScopeFilterComboBox.IsEnabled = _options.IsScopeSearchDepthEditable;
        RequiredFeaturesComboBox.IsEnabled = _options.IsRequiredFeaturesEditable;
        CurrentObjectComboBox.IsEnabled = _options.IsCurrentObjectEditable;
        ExcludeCurrentObjectCheckBox.IsEnabled = _options.IsExcludeCurrentObjectEditable;

        if (selectedObjectId.HasValue)
        {
            var selectedChoice = _choices.FirstOrDefault(choice => choice.ObjectId == selectedObjectId.Value);
            if (selectedChoice is not null)
            {
                ChoicesDataGrid.SelectedItem = selectedChoice;
                ChoicesDataGrid.ScrollIntoView(selectedChoice);
            }

            var currentObjectChoice = CurrentObjectComboBox.Items
                .OfType<CurrentObjectChoice>()
                .FirstOrDefault(item => item.ObjectId == selectedObjectId.Value);
            if (currentObjectChoice is not null)
            {
                CurrentObjectComboBox.SelectedItem = currentObjectChoice;
            }
        }
        else if (_options.DefaultCurrentObjectId.HasValue)
        {
            var currentObjectChoice = CurrentObjectComboBox.Items
                .OfType<CurrentObjectChoice>()
                .FirstOrDefault(item => item.ObjectId == _options.DefaultCurrentObjectId.Value);
            if (currentObjectChoice is not null)
            {
                CurrentObjectComboBox.SelectedItem = currentObjectChoice;
            }
        }

        if (CurrentObjectComboBox.SelectedItem is null && CurrentObjectComboBox.Items.Count > 0)
        {
            CurrentObjectComboBox.SelectedIndex = 0;
        }

        if (ChoicesDataGrid.SelectedItem is null && _choices.Count > 0)
        {
            ChoicesDataGrid.SelectedIndex = 0;
        }

        _isInitializing = false;
        RefreshDiscoveredChoices();

        SourceFilterComboBox.Focus();
    }

    public Guid? SelectedObjectId { get; private set; }
    public MaterializeSourceObjectChoiceItem? SelectedChoice { get; private set; }

    public GameObjectOptionSourceTarget SelectedScopeSearchType =>
        SourceFilterComboBox.SelectedItem is GameObjectOptionSourceTarget target
            ? target
            : GameObjectOptionSourceTarget.RealObjects;

    public ScopeSearchDepth SelectedScopeSearchDepth =>
        ScopeFilterComboBox.SelectedItem is ScopeSearchDepth target
            ? target
            : ScopeSearchDepth.Project;

    public GameObjectFeatureRequirements SelectedRequiredFeatures =>
        RequiredFeaturesComboBox.SelectedItem is GameObjectFeatureRequirements features
            ? features
            : GameObjectFeatureRequirements.None;

    public Guid? SelectedCurrentObjectId =>
        CurrentObjectComboBox.SelectedItem is CurrentObjectChoice current
            ? current.ObjectId
            : null;

    public bool ExcludeCurrentObject => ExcludeCurrentObjectCheckBox.IsChecked != false;

    public GameObjectSelectionOptionDiscoveryRequest? SelectedDiscoveryRequest { get; private set; }

    private void Filters_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        RefreshDiscoveredChoices();
    }

    private void RefreshDiscoveredChoices()
    {
        if (_choicesView is null)
        {
            return;
        }

        _discoveredObjectIds.Clear();
        var currentChoice = CurrentObjectComboBox.SelectedItem as CurrentObjectChoice;

        if (SelectedScopeSearchDepth == ScopeSearchDepth.Project)
        {
            if (currentChoice?.SourceObject is GameObject currentObject)
            {
                EnsureProjectChoices(currentObject);

                SelectedDiscoveryRequest = new GameObjectSelectionOptionDiscoveryRequest(
                    CurrentObject: currentObject,
                    RequiredFeatures: SelectedRequiredFeatures,
                    ScopeSearchDepth: SelectedScopeSearchDepth,
                    ScopeSearchType: SelectedScopeSearchType,
                    ExcludeCurrentObject: ExcludeCurrentObject);
            }
            else
            {
                SelectedDiscoveryRequest = null;
            }

            foreach (var choice in _choices)
            {
                if (!MatchesScopeSearchType(choice, SelectedScopeSearchType))
                {
                    continue;
                }

                if (!MatchesRequiredFeatures(choice.SourceObject, SelectedRequiredFeatures))
                {
                    continue;
                }

                if (ExcludeCurrentObject && currentChoice is not null && currentChoice.ObjectId == choice.ObjectId)
                {
                    continue;
                }

                _discoveredObjectIds.Add(choice.ObjectId);
            }
        }
        else if (currentChoice?.SourceObject is GameObject current)
        {
            var request = new GameObjectSelectionOptionDiscoveryRequest(
                CurrentObject: current,
                RequiredFeatures: SelectedRequiredFeatures,
                ScopeSearchDepth: SelectedScopeSearchDepth,
                ScopeSearchType: SelectedScopeSearchType,
                ExcludeCurrentObject: ExcludeCurrentObject);
            SelectedDiscoveryRequest = request;

            foreach (var option in _discoveryService.Discover(request))
            {
                _discoveredObjectIds.Add(option.Id);
            }
        }
        else
        {
            SelectedDiscoveryRequest = null;
            foreach (var choice in _choices)
            {
                _discoveredObjectIds.Add(choice.ObjectId);
            }
        }

        _choicesView.Refresh();

        if (ChoicesDataGrid.SelectedItem is MaterializeSourceObjectChoiceItem selected
            && !_discoveredObjectIds.Contains(selected.ObjectId))
        {
            ChoicesDataGrid.SelectedItem = null;
        }

        if (ChoicesDataGrid.SelectedItem is null && _choicesView.Cast<MaterializeSourceObjectChoiceItem>().Any())
        {
            ChoicesDataGrid.SelectedIndex = 0;
        }
    }

    private bool ChoiceMatchesFilter(object obj)
    {
        if (obj is not MaterializeSourceObjectChoiceItem choice)
        {
            return false;
        }

        return _discoveredObjectIds.Count == 0 || _discoveredObjectIds.Contains(choice.ObjectId);
    }

    private static bool MatchesScopeSearchType(MaterializeSourceObjectChoiceItem choice, GameObjectOptionSourceTarget scopeSearchType)
    {
        var isTemplateLike = choice.SourceCategory.Contains("Template", StringComparison.OrdinalIgnoreCase)
                             || choice.SourceCategory.Contains("Base", StringComparison.OrdinalIgnoreCase);

        return scopeSearchType switch
        {
            GameObjectOptionSourceTarget.RealObjects => !isTemplateLike,
            GameObjectOptionSourceTarget.ObjectTemplates => isTemplateLike,
            _ => true
        };
    }

    private static bool MatchesRequiredFeatures(GameObject? sourceObject, GameObjectFeatureRequirements requiredFeatures)
    {
        if (requiredFeatures == GameObjectFeatureRequirements.None)
        {
            return true;
        }

        if (sourceObject is null)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Inventoriable) && !sourceObject.IsInventoriable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Openable) && !sourceObject.IsOpenable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Lockable) && !sourceObject.IsLockable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Container) && !sourceObject.IsContainer)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Activatable) && !sourceObject.IsActivatable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Hidable) && !sourceObject.IsHidable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.Quantifiable) && !sourceObject.IsQuantifiable)
        {
            return false;
        }

        if (requiredFeatures.HasFlag(GameObjectFeatureRequirements.CompositeTarget) && !sourceObject.IsCompositeTarget)
        {
            return false;
        }

        return true;
    }

    private void EnsureProjectChoices(GameObject currentObject)
    {
        var root = GetRootScopeNode(currentObject);
        foreach (var obj in EnumerateScopeTreeGameObjects(root))
        {
            if (obj.ObjectId == Guid.Empty || !_knownChoiceIds.Add(obj.ObjectId))
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(obj.ScopeName)
                ? "(unnamed object)"
                : obj.ScopeName.Trim();

            var scopePath = BuildScopePath(obj.EnumerateSelfAndAncestors().Reverse().Select(static node => node.ScopeName).ToArray());

            _choices.Add(new MaterializeSourceObjectChoiceItem
            {
                ObjectId = obj.ObjectId,
                SourceObject = obj,
                DisplayName = displayName,
                ScopePath = scopePath,
                SourceCategory = IsTemplateLikeObject(obj) ? "Project Templates" : "Project Objects"
            });
        }
    }

    private static IScopedAwareNode GetRootScopeNode(IScopedAwareNode node)
    {
        var current = node;
        while (current.ParentScope is not null)
        {
            current = current.ParentScope;
        }

        return current;
    }

    private static IEnumerable<GameObject> EnumerateScopeTreeGameObjects(IScopedAwareNode root)
    {
        if (root is GameObject obj)
        {
            yield return obj;
        }

        foreach (var child in root.ChildScopes)
        {
            foreach (var nested in EnumerateScopeTreeGameObjects(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsTemplateLikeObject(GameObject obj)
    {
        var current = obj as IScopedAwareNode;
        while (current is not null)
        {
            if (current.ParentScope is ObjectTemplatesScopeNode
                or BaseObjectsScopeNode
                or RoomTemplatesScopeNode)
            {
                return true;
            }

            current = current.ParentScope;
        }

        return false;
    }

    private static string BuildScopePath(params string[] parts)
    {
        return string.Join(
            " > ",
            parts
                .Where(static part => !string.IsNullOrWhiteSpace(part))
                .Select(static part => part.Trim()));
    }

    private void ChoicesDataGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TrySelect();
    }

    private void ChoicesDataGrid_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            TrySelect();
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
        TrySelect();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TrySelect()
    {
        if (ChoicesDataGrid.SelectedItem is not MaterializeSourceObjectChoiceItem selected)
        {
            System.Windows.MessageBox.Show(this, "Choose a game object.", "Game Object", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedChoice = selected;
        SelectedObjectId = selected.ObjectId;
        DialogResult = true;
    }
}



