using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.RuntimeContracts.Enums;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace StoryboardDesigner.App.Views;

public partial class EventSubscriptionLibraryDialog : Window
{
    private const string DefaultOnMissingAction = "DiagnosticOnly";

    private readonly ObservableCollection<EventSubscriptionSummaryRow> _rows;
    private readonly IReadOnlyList<string> _eventKeySuggestions;
    private readonly IReadOnlyList<string> _actionNameSuggestions;
    private readonly IReadOnlyList<string> _filterVariableSuggestions;
    private readonly IReadOnlyList<string> _anchorSuggestions;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _filterVariableChoices;
    private readonly IReadOnlyList<ScopeObjectChoiceItem> _sourceScopeChoices;
    private readonly IReadOnlyList<string> _soundEffectKeySuggestions;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CommandActionType>> _actionNameToTypes;

    public EventSubscriptionLibraryDialog(
        IReadOnlyList<EventSubscriptionDefinition> initialEntries,
        IReadOnlyList<string>? eventKeySuggestions = null,
        IReadOnlyList<string>? actionNameSuggestions = null,
        IReadOnlyList<string>? filterVariableSuggestions = null,
        IReadOnlyList<string>? anchorSuggestions = null,
        IReadOnlyList<GamePropertyChoiceItem>? filterVariableChoices = null,
        IReadOnlyList<ScopeObjectChoiceItem>? sourceScopeChoices = null,
        IReadOnlyList<string>? soundEffectKeySuggestions = null,
        IReadOnlyDictionary<string, IReadOnlyList<CommandActionType>>? actionNameToTypes = null)
    {
        InitializeComponent();

        _eventKeySuggestions = NormalizeSuggestions(eventKeySuggestions);
        _actionNameSuggestions = NormalizeSuggestions(actionNameSuggestions);
        _filterVariableSuggestions = NormalizeSuggestions(filterVariableSuggestions);
        _anchorSuggestions = NormalizeSuggestions(anchorSuggestions);
        _filterVariableChoices = (filterVariableChoices ?? Array.Empty<GamePropertyChoiceItem>()).ToList();
        _sourceScopeChoices = (sourceScopeChoices ?? Array.Empty<ScopeObjectChoiceItem>()).ToList();
        _soundEffectKeySuggestions = NormalizeSuggestions(soundEffectKeySuggestions);
        _actionNameToTypes = actionNameToTypes ?? new Dictionary<string, IReadOnlyList<CommandActionType>>(StringComparer.OrdinalIgnoreCase);
        _rows = new ObservableCollection<EventSubscriptionSummaryRow>(
            CloneEntries(initialEntries).Select(static entry => new EventSubscriptionSummaryRow(entry)));

        SubscriptionsDataGrid.ItemsSource = _rows;
        if (_rows.Count > 0)
        {
            SubscriptionsDataGrid.SelectedIndex = 0;
        }
    }

    public List<EventSubscriptionDefinition> Entries => _rows
        .Select(static row => CloneEntry(row.Entry))
        .ToList();

    private void AddSubscription_OnClick(object sender, RoutedEventArgs e)
    {
        var entry = CreateDefaultEntry();
        if (!TryEditEntry(entry, out var updatedEntry))
        {
            return;
        }

        var row = new EventSubscriptionSummaryRow(updatedEntry);
        _rows.Add(row);
        SubscriptionsDataGrid.SelectedItem = row;
        SubscriptionsDataGrid.ScrollIntoView(row);
    }

    private void EditSubscription_OnClick(object sender, RoutedEventArgs e)
    {
        EditSelectedSubscription();
    }

    private void SubscriptionsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditSelectedSubscription();
    }

    private void EditSelectedSubscription()
    {
        if (SubscriptionsDataGrid.SelectedItem is not EventSubscriptionSummaryRow selected)
        {
            return;
        }

        if (!TryEditEntry(selected.Entry, out var updatedEntry))
        {
            return;
        }

        var selectedIndex = _rows.IndexOf(selected);
        if (selectedIndex < 0)
        {
            return;
        }

        var updatedRow = new EventSubscriptionSummaryRow(updatedEntry);
        _rows[selectedIndex] = updatedRow;
        SubscriptionsDataGrid.SelectedItem = updatedRow;
        SubscriptionsDataGrid.ScrollIntoView(updatedRow);
    }

    private bool TryEditEntry(EventSubscriptionDefinition sourceEntry, out EventSubscriptionDefinition updatedEntry)
    {
        var authoredBindings = _rows
            .SelectMany(static row => row.Entry.ActionBindings ?? new List<EventActionBindingDefinition>());

        var dialog = new EventSubscriptionEditorDialog(
            CloneEntry(sourceEntry),
            eventKeyOptions: BuildEventKeyOptionSet(sourceEntry),
            _actionNameSuggestions,
            _filterVariableSuggestions,
            onMissingActionOptions: BuildTargetOptionSet(DefaultOnMissingAction, authoredBindings.Select(static binding => binding.Target?.OnMissingAction)),
            anchorOptions: _anchorSuggestions,
            filterVariableChoices: _filterVariableChoices,
            sourceScopeChoices: _sourceScopeChoices,
            soundEffectKeySuggestions: _soundEffectKeySuggestions,
            actionNameToTypes: _actionNameToTypes)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            updatedEntry = sourceEntry;
            return false;
        }

        updatedEntry = CloneEntry(dialog.Entry);
        return true;
    }

    private void RemoveSubscription_OnClick(object sender, RoutedEventArgs e)
    {
        if (SubscriptionsDataGrid.SelectedItem is not EventSubscriptionSummaryRow selected)
        {
            return;
        }

        var key = string.IsNullOrWhiteSpace(selected.EventKey)
            ? selected.Id.ToString("D")
            : selected.SubscriptionName;

        var confirmation = WpfMessageBox.Show(
            this,
            $"Remove event subscription '{key}'?",
            "Event Subscriptions",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);

        if (confirmation != WpfMessageBoxResult.Yes)
        {
            return;
        }

        _rows.Remove(selected);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var errors = ValidateEntries(_rows.Select(static row => row.Entry));
        if (errors.Count > 0)
        {
            WpfMessageBox.Show(
                this,
                string.Join(Environment.NewLine, errors),
                "Event Subscriptions",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private IReadOnlyList<string> BuildEventKeyOptionSet(EventSubscriptionDefinition sourceEntry)
    {
        var values = new List<string>();
        values.AddRange(_eventKeySuggestions);
        values.AddRange(_rows.Select(static row => row.EventKey));
        values.Add(sourceEntry.EventKey ?? string.Empty);

        return values
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private EventSubscriptionDefinition CreateDefaultEntry()
    {
        var used = _rows
            .Select(static row => row.EventKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sequence = Math.Max(1, _rows.Count + 1);
        while (true)
        {
            var candidate = $"event.subscription.{sequence}";
            if (!used.Contains(candidate))
            {
                return new EventSubscriptionDefinition
                {
                    Id = Guid.NewGuid(),
                    EventKey = string.Empty,
                    SubscriptionName = string.Empty,
                    IsEnabled = true,
                    Lane = "foreground",
                    DispatchDisposition = EventSubscriberDispatchDisposition.bubble,
                    SubscriptionVisibleWhenContained = false,
                    SubscriptionSourceMatchMode = SubscriptionSourceMatchMode.AnySource,
                    SubscriptionSourceScopeNodeId = null,
                    SubscriptionSecondarySourceMatchMode = SubscriptionSourceMatchMode.AnySource,
                    SubscriptionSecondarySourceScopeNodeId = null,
                    InputArgumentMappings = new List<EventInputArgumentMappingDefinition>(),
                    ActionBindings =
                    [
                        new EventActionBindingDefinition
                        {
                            Order = 1,
                            Target = new EventBindingTargetDefinition
                            {
                                ActionName = _actionNameSuggestions.FirstOrDefault() ?? "inspect"
                            }
                        }
                    ]
                };
            }

            sequence++;
        }
    }

    private static List<string> ValidateEntries(IEnumerable<EventSubscriptionDefinition> entries)
    {
        var errors = new List<string>();
        var index = 0;

        foreach (var entry in entries)
        {
            index++;
            if (string.IsNullOrWhiteSpace(entry.EventKey))
            {
                errors.Add($"Subscription {index}: Event Key is required.");
            }

            var bindings = entry.ActionBindings ?? new List<EventActionBindingDefinition>();
            var duplicateOrders = bindings
                .GroupBy(static binding => binding.Order)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .OrderBy(static order => order)
                .ToList();

            foreach (var duplicateOrder in duplicateOrders)
            {
                errors.Add($"Subscription {index}: duplicate binding order '{duplicateOrder}'.");
            }

            for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                if (string.IsNullOrWhiteSpace(binding.Target?.ActionName))
                {
                    errors.Add($"Subscription {index}, Binding {bindingIndex + 1}: Action Name is required.");
                }

                var filters = binding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>();
                for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(filters[filterIndex].VariableName))
                    {
                        continue;
                    }

                    errors.Add($"Subscription {index}, Binding {bindingIndex + 1}, Filter {filterIndex + 1}: Variable Name is required.");
                }
            }
        }

        return errors;
    }

    private static List<EventSubscriptionDefinition> CloneEntries(IEnumerable<EventSubscriptionDefinition> source)
    {
        return source.Select(CloneEntry).ToList();
    }

    private static EventSubscriptionDefinition CloneEntry(EventSubscriptionDefinition source)
    {
        var eventKey = source.EventKey?.Trim() ?? string.Empty;
        var subscriptionName = source.SubscriptionName?.Trim() ?? string.Empty;
        return new EventSubscriptionDefinition
        {
            Id = source.Id,
            EventKey = eventKey,
            SubscriptionName = string.IsNullOrWhiteSpace(subscriptionName)
                ? eventKey
                : subscriptionName,
            IsEnabled = source.IsEnabled,
            Lane = source.Lane,
            DispatchDisposition = source.DispatchDisposition,
            SubscriptionVisibleWhenContained = source.SubscriptionVisibleWhenContained,
            SubscriptionSourceMatchMode = source.SubscriptionSourceMatchMode,
            SubscriptionSourceScopeNodeId = source.SubscriptionSourceScopeNodeId,
            SubscriptionSecondarySourceMatchMode = source.SubscriptionSecondarySourceMatchMode,
            SubscriptionSecondarySourceScopeNodeId = source.SubscriptionSecondarySourceScopeNodeId,
            InputArgumentMappings = (source.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
                .Select(static mapping => new EventInputArgumentMappingDefinition
                {
                    InputEventPaylloadArgKey = mapping.InputEventPaylloadArgKey?.Trim() ?? string.Empty,
                    OutputActionPayloadArgKey = mapping.OutputActionPayloadArgKey?.Trim() ?? string.Empty
                })
                .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
                .ToList(),
            ActionBindings = (source.ActionBindings ?? new List<EventActionBindingDefinition>())
                .Select(static binding => new EventActionBindingDefinition
                {
                    Order = binding.Order,
                    IsEnabled = binding.IsEnabled,
                    Target = new EventBindingTargetDefinition
                    {
                        ActionName = binding.Target?.ActionName ?? string.Empty,
                        OnMissingAction = binding.Target?.OnMissingAction ?? DefaultOnMissingAction,
                        StopChainOnFailure = binding.Target?.StopChainOnFailure ?? true
                    },
                    Condition = new EventBindingConditionDefinition
                    {
                        QuantityEvaluationMode = binding.Condition?.QuantityEvaluationMode
                            ?? RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass,
                        Filters = (binding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>())
                            .Select(static filter => new EventBindingFilterConditionDefinition
                            {
                                VariableName = filter.VariableName,
                                Operator = filter.Operator,
                                ExpectedValue = filter.ExpectedValue
                            })
                            .ToList()
                    },
                    InputArgumentMappings = (binding.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
                        .Select(static mapping => mapping.CloneNormalized())
                        .Where(static mapping =>
                            mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
                                ? !string.IsNullOrWhiteSpace(mapping.OutputActionPayloadArgKey)
                                : !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
                        .ToList()
                })
                .ToList()
        };
    }

    private static IReadOnlyList<string> NormalizeSuggestions(IReadOnlyList<string>? source)
    {
        return (source ?? Array.Empty<string>())
            .Select(static item => item?.Trim() ?? string.Empty)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildTargetOptionSet(string defaultValue, IEnumerable<string?> authoredValues)
    {
        var values = new List<string> { defaultValue };
        values.AddRange(authoredValues.Where(static value => !string.IsNullOrWhiteSpace(value))!);

        return values
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => string.Equals(value, defaultValue, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
    }

    private sealed class EventSubscriptionSummaryRow
    {
        public EventSubscriptionSummaryRow(EventSubscriptionDefinition entry)
        {
            Entry = entry;
        }

        public EventSubscriptionDefinition Entry { get; }

        public Guid Id => Entry.Id;

        public string SubscriptionName => ResolveSubscriptionName(Entry);

        public string EventKey => Entry.EventKey ?? string.Empty;

        public bool IsEnabled => Entry.IsEnabled;

        public string Lane => Entry.Lane ?? string.Empty;

        public EventSubscriberDispatchDisposition DispatchDisposition => Entry.DispatchDisposition;

        public int BindingCount => Entry.ActionBindings?.Count ?? 0;

        public string FirstActionName => Entry.ActionBindings?
            .Select(static binding => binding.Target?.ActionName?.Trim() ?? string.Empty)
            .FirstOrDefault(static name => !string.IsNullOrWhiteSpace(name))
            ?? string.Empty;

        public string TopFilterVariable => (Entry.ActionBindings ?? new List<EventActionBindingDefinition>())
            .SelectMany(static binding => binding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>())
            .Select(static filter => filter.VariableName?.Trim() ?? string.Empty)
            .FirstOrDefault(static variableName => !string.IsNullOrWhiteSpace(variableName))
            ?? string.Empty;

        private static string ResolveSubscriptionName(EventSubscriptionDefinition entry)
        {
            var subscriptionName = entry.SubscriptionName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(subscriptionName))
            {
                return subscriptionName;
            }

            var eventKey = entry.EventKey?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(eventKey)
                ? "(Missing Event Key)"
                : eventKey;
        }
    }
}
