using System.IO;
using System.Text.Json;
using System.Windows;
using System.Collections.ObjectModel;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace StoryboardDesigner.App.Views;

public partial class EventInputArgumentMappingsDialog : Window
{
    private const string AllActionTypesOptionLabel = "(All Action Types)";
    private const string EventPayloadManifestRelativePath = "Config\\event-payload.manifest.json";
    private const string ActionPayloadManifestRelativePath = "Config\\action-payload.manifest.json";
    private const string PlaySoundOverrideSoundEffectKey = "sound.play.overrideSoundEffectKey";
    private readonly List<EventInputArgumentMappingDefinition> _workingMappings;
    private readonly ActionPayloadSuggestionCatalog _actionPayloadSuggestionCatalog;
    private readonly IReadOnlyList<string> _soundEffectKeySuggestions;

    public EventInputArgumentMappingsDialog(
        string eventKey,
        IReadOnlyList<EventInputArgumentMappingDefinition>? initialMappings,
        IReadOnlyList<string>? soundEffectKeySuggestions = null)
    {
        InitializeComponent();

        var normalizedEventKey = eventKey?.Trim() ?? string.Empty;
        InputPayloadKeySuggestions = LoadEventPayloadKeysForEvent(normalizedEventKey);
        _actionPayloadSuggestionCatalog = LoadActionPayloadSuggestionCatalog();
        IntendedActionTypeOptions = BuildIntendedActionTypeOptions(_actionPayloadSuggestionCatalog);
        OutputPayloadKeySuggestions = new ObservableCollection<string>();
        SelectedIntendedActionType = IntendedActionTypeOptions.FirstOrDefault() ?? AllActionTypesOptionLabel;
        RebuildOutputPayloadKeySuggestions();
        _soundEffectKeySuggestions = NormalizeSuggestions(soundEffectKeySuggestions);
        _workingMappings = (initialMappings ?? Array.Empty<EventInputArgumentMappingDefinition>())
            .Select(static mapping => mapping.CloneNormalized())
            .Select(static mapping =>
            {
                if (mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
                    || !Storyboard.Shared.RuntimeContracts.RuntimeEventInputArgumentMappingConventions
                        .TryDecodeConstantTargetKey(mapping.InputEventPaylloadArgKey, out _))
                {
                    return mapping;
                }

                return EventInputArgumentMappingDefinition.FromPersisted(
                    mapping.InputEventPaylloadArgKey,
                    mapping.OutputActionPayloadArgKey);
            })
            .Where(IsValidMapping)
            .ToList();

        MappingsDataGrid.ItemsSource = _workingMappings;
        if (_workingMappings.Count > 0)
        {
            MappingsDataGrid.SelectedIndex = 0;
        }
    }

    public IReadOnlyList<string> InputPayloadKeySuggestions { get; }

    public IReadOnlyList<string> IntendedActionTypeOptions { get; }

    public IReadOnlyList<EventInputArgumentMappingSourceType> MappingSourceTypeOptions { get; } =
        Enum.GetValues<EventInputArgumentMappingSourceType>();

    public ObservableCollection<string> OutputPayloadKeySuggestions { get; }

    public IReadOnlyList<string> SoundEffectKeySuggestions => _soundEffectKeySuggestions;

    public string SelectedIntendedActionType { get; private set; }

    public IReadOnlyList<EventInputArgumentMappingDefinition> Mappings { get; private set; } = Array.Empty<EventInputArgumentMappingDefinition>();

    private void AddMapping_OnClick(object sender, RoutedEventArgs e)
    {
        var mapping = new EventInputArgumentMappingDefinition
        {
            SourceType = EventInputArgumentMappingSourceType.EventPayloadKey,
            InputEventPaylloadArgKey = InputPayloadKeySuggestions.FirstOrDefault() ?? string.Empty,
            OutputActionPayloadArgKey = string.Empty,
            ConstantValue = string.Empty
        };

        _workingMappings.Add(mapping);
        RebindMappingsGrid();
        MappingsDataGrid.SelectedItem = mapping;
        MappingsDataGrid.ScrollIntoView(mapping);
    }

    private void RemoveMapping_OnClick(object sender, RoutedEventArgs e)
    {
        if (MappingsDataGrid.SelectedItem is not EventInputArgumentMappingDefinition selected)
        {
            return;
        }

        _workingMappings.Remove(selected);
        RebindMappingsGrid();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var normalized = _workingMappings
            .Select(static mapping => mapping.CloneNormalized())
            .Where(IsValidMapping)
            .ToList();

        var duplicates = normalized
            .GroupBy(static mapping => BuildDuplicateGuardKey(mapping), StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => DescribeDuplicateKey(group.First()))
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicates.Count > 0)
        {
            WpfMessageBox.Show(
                this,
                $"Duplicate mapping source keys are not allowed: {string.Join(", ", duplicates)}",
                "Event Input Argument Mappings",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
            return;
        }

        Mappings = normalized;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void IntendedActionTypeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectedIntendedActionType = IntendedActionTypeComboBox.SelectedItem as string ?? AllActionTypesOptionLabel;
        RebuildOutputPayloadKeySuggestions();
    }

    private void RebindMappingsGrid()
    {
        MappingsDataGrid.ItemsSource = null;
        MappingsDataGrid.ItemsSource = _workingMappings;
        if (_workingMappings.Count > 0)
        {
            MappingsDataGrid.SelectedIndex = 0;
        }
    }

    private static IReadOnlyList<string> LoadEventPayloadKeysForEvent(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return Array.Empty<string>();
        }

        var manifestPath = Path.Combine(AppContext.BaseDirectory, EventPayloadManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("events", out var eventsElement)
                || eventsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var keys = new List<string>();
            foreach (var eventElement in eventsElement.EnumerateArray())
            {
                if (!eventElement.TryGetProperty("eventKey", out var eventKeyElement)
                    || eventKeyElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var manifestEventKey = eventKeyElement.GetString()?.Trim() ?? string.Empty;
                if (!string.Equals(manifestEventKey, eventKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!eventElement.TryGetProperty("payloadMappings", out var mappingsElement)
                    || mappingsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var mappingElement in mappingsElement.EnumerateArray())
                {
                    if (!mappingElement.TryGetProperty("payloadKey", out var payloadKeyElement)
                        || payloadKeyElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var payloadKey = payloadKeyElement.GetString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(payloadKey))
                    {
                        keys.Add(payloadKey);
                    }
                }
            }

            return keys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void RebuildOutputPayloadKeySuggestions()
    {
        var selectedActionType = string.Equals(SelectedIntendedActionType, AllActionTypesOptionLabel, StringComparison.Ordinal)
            ? null
            : SelectedIntendedActionType;

        var suggestions = _actionPayloadSuggestionCatalog.GetSuggestions(selectedActionType);
        OutputPayloadKeySuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            OutputPayloadKeySuggestions.Add(suggestion);
        }
    }

    private static IReadOnlyList<string> BuildIntendedActionTypeOptions(ActionPayloadSuggestionCatalog catalog)
    {
        var values = new List<string> { AllActionTypesOptionLabel };
        values.AddRange(catalog.ActionTypes);
        return values;
    }

    private void ChooseConstantValue_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EventInputArgumentMappingDefinition mapping)
        {
            return;
        }

        if (mapping.SourceType != EventInputArgumentMappingSourceType.ConstantValue)
        {
            return;
        }

        if (!string.Equals(
                mapping.OutputActionPayloadArgKey?.Trim(),
                PlaySoundOverrideSoundEffectKey,
                StringComparison.OrdinalIgnoreCase))
        {
            WpfMessageBox.Show(
                this,
                "Sound key picker is available when Output Action Payload Arg Key is sound.play.overrideSoundEffectKey.",
                "Event Input Argument Mappings",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
            return;
        }

        if (_soundEffectKeySuggestions.Count == 0)
        {
            WpfMessageBox.Show(
                this,
                "No scoped sound effect keys are available here.",
                "Event Input Argument Mappings",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
            return;
        }

        var choices = _soundEffectKeySuggestions
            .Select(static key => new ScopeObjectChoiceItem
            {
                Key = key,
                DisplayName = key,
                ScopePath = "Sound Effects",
                OwnerContext = "Sound Effect Key",
                Scope = PropertyResolutionScope.Global
            })
            .ToList();

        var dialog = new ScopeObjectChooserDialog(
            choices,
            "Choose Sound Effect Key",
            expectedScope: null,
            selectedKey: mapping.ConstantValue)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedKey))
        {
            return;
        }

        mapping.ConstantValue = dialog.SelectedKey.Trim();
        MappingsDataGrid.Items.Refresh();
    }

    private static bool IsValidMapping(EventInputArgumentMappingDefinition mapping)
    {
        return mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
            ? !string.IsNullOrWhiteSpace(mapping.OutputActionPayloadArgKey)
            : !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey);
    }

    private static string BuildDuplicateGuardKey(EventInputArgumentMappingDefinition mapping)
    {
        if (mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue)
        {
            var outputKey = mapping.OutputActionPayloadArgKey?.Trim() ?? string.Empty;
            return $"constant:{outputKey}";
        }

        var inputKey = mapping.InputEventPaylloadArgKey?.Trim() ?? string.Empty;
        return $"event:{inputKey}";
    }

    private static string DescribeDuplicateKey(EventInputArgumentMappingDefinition mapping)
    {
        if (mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue)
        {
            return $"Constant -> {mapping.OutputActionPayloadArgKey?.Trim()}";
        }

        return $"Event Payload -> {mapping.InputEventPaylloadArgKey?.Trim()}";
    }

    private static IReadOnlyList<string> NormalizeSuggestions(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ActionPayloadSuggestionCatalog LoadActionPayloadSuggestionCatalog()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, ActionPayloadManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            return ActionPayloadSuggestionCatalog.Empty;
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var globalKeys = new List<string>();
            var keysByActionType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var actionTypes = new List<string>();

            if (document.RootElement.TryGetProperty("global", out var globalElement)
                && globalElement.ValueKind == JsonValueKind.Object)
            {
                AddActionPayloadKeysFromCatalog(globalElement, globalKeys);
            }

            if (document.RootElement.TryGetProperty("perActionTypeCatalog", out var perActionTypeCatalogElement)
                && perActionTypeCatalogElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var actionTypeElement in perActionTypeCatalogElement.EnumerateArray())
                {
                    if (actionTypeElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!actionTypeElement.TryGetProperty("actionType", out var actionTypeNameElement)
                        || actionTypeNameElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var actionTypeName = actionTypeNameElement.GetString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(actionTypeName))
                    {
                        continue;
                    }

                    actionTypes.Add(actionTypeName);
                    if (!keysByActionType.TryGetValue(actionTypeName, out var scopedKeys))
                    {
                        scopedKeys = new List<string>();
                        keysByActionType[actionTypeName] = scopedKeys;
                    }

                    AddActionPayloadKeysFromCatalog(actionTypeElement, scopedKeys);
                }
            }

            return new ActionPayloadSuggestionCatalog(
                globalKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                actionTypes
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                keysByActionType.ToDictionary(
                    static pair => pair.Key,
                    static pair => (IReadOnlyList<string>)pair.Value
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return ActionPayloadSuggestionCatalog.Empty;
        }
    }

    private static void AddActionPayloadKeysFromCatalog(JsonElement catalogOwner, List<string> keys)
    {
        if (!catalogOwner.TryGetProperty("actionPayloadArgumentCatalog", out var catalogElement)
            || catalogElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var entryElement in catalogElement.EnumerateArray())
        {
            if (!entryElement.TryGetProperty("actionPayloadArgKey", out var keyElement)
                || keyElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var key = keyElement.GetString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key);
            }
        }
    }

    private sealed class ActionPayloadSuggestionCatalog(
        IReadOnlyList<string> globalKeys,
        IReadOnlyList<string> actionTypes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> keysByActionType)
    {
        internal static readonly ActionPayloadSuggestionCatalog Empty = new(
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));

        private readonly IReadOnlyList<string> _globalKeys = globalKeys;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _keysByActionType = keysByActionType;

        internal IReadOnlyList<string> ActionTypes { get; } = actionTypes;

        internal IReadOnlyList<string> GetSuggestions(string? actionType)
        {
            var output = new List<string>(_globalKeys);

            if (string.IsNullOrWhiteSpace(actionType))
            {
                foreach (var scopedKeys in _keysByActionType.Values)
                {
                    output.AddRange(scopedKeys);
                }
            }
            else if (_keysByActionType.TryGetValue(actionType, out var scopedKeys))
            {
                output.AddRange(scopedKeys);
            }

            return output
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
