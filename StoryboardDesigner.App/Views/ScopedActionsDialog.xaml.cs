using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows;
using Storyboard.Shared.GameServices;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Views;

public partial class ScopedActionsDialog : Window
{
    private const string NoVerbDisplayValue = "(no verb)";

    private readonly string _scopeLabel;
    private readonly IList<CommandAction> _targetActions;
    private readonly IReadOnlyList<GamePropertyChoiceItem> _variableChoices;
    private readonly IReadOnlyList<string> _echoReferenceTokens;
    private readonly IReadOnlyList<CommandAction> _additionalLinkTargetActions;
    private readonly IReadOnlyList<ContainerTargetChoiceItem> _containerTargetChoices;
    private readonly IReadOnlyList<MaterializeSourceObjectChoiceItem> _materializeSourceObjectChoices;
    private readonly IReadOnlyList<ProcedureChoiceItem> _procedureChoices;
    private readonly IReadOnlyList<CompositeRecipeChoiceItem> _compositeRecipeChoices;
    private readonly IReadOnlyList<SoundEffectChoiceItem> _soundEffectChoices;
    private readonly IReadOnlyList<string> _timerKeySuggestions;
    private readonly PropertyResolutionScope _variableScope;
    private readonly string _scopeEntityName;
    private readonly ObservableCollection<CommandAction> _workingActions;
    private readonly IReadOnlyList<string> _verbEntrySuggestions;
    private readonly Func<IReadOnlyList<CommandAction>, IReadOnlyList<ProjectValidationIssue>>? _validateWithPipeline;
    public IReadOnlyList<string> DirectionalSuggestions { get; }

    public ScopedActionsDialog(
        string scopeLabel,
        PropertyResolutionScope variableScope,
        string scopeEntityName,
        IList<CommandAction> actions,
        IReadOnlyList<CommandAction> additionalLinkTargetActions,
        IReadOnlyList<ContainerTargetChoiceItem> containerTargetChoices,
        IReadOnlyList<MaterializeSourceObjectChoiceItem> materializeSourceObjectChoices,
        IReadOnlyList<ProcedureChoiceItem> procedureChoices,
        IReadOnlyList<CompositeRecipeChoiceItem> compositeRecipeChoices,
        IReadOnlyList<SoundEffectChoiceItem> soundEffectChoices,
        IReadOnlyList<GamePropertyChoiceItem> variableChoices,
        IReadOnlyList<string> echoReferenceTokens,
        IReadOnlyList<string> timerKeySuggestions,
        IReadOnlyList<string> verbSuggestions,
        IReadOnlyList<string> directionalSuggestions,
        Func<IReadOnlyList<CommandAction>, IReadOnlyList<ProjectValidationIssue>>? validateWithPipeline = null)
    {
        InitializeComponent();

        _scopeLabel = scopeLabel;
        _targetActions = actions;
        _additionalLinkTargetActions = additionalLinkTargetActions;
        _containerTargetChoices = containerTargetChoices;
        _materializeSourceObjectChoices = materializeSourceObjectChoices;
        _procedureChoices = procedureChoices;
        _compositeRecipeChoices = compositeRecipeChoices;
        _soundEffectChoices = soundEffectChoices;
        _timerKeySuggestions = timerKeySuggestions;
        _variableChoices = variableChoices;
        _echoReferenceTokens = echoReferenceTokens;
        _variableScope = variableScope;
        _scopeEntityName = scopeEntityName;
        _workingActions = new ObservableCollection<CommandAction>(actions.Select(CloneAction));
        _verbEntrySuggestions = BuildVerbEntrySuggestions(verbSuggestions);
        DirectionalSuggestions = directionalSuggestions;
        _validateWithPipeline = validateWithPipeline;

        ScopeLabelText.Text = scopeLabel;
        ActionsListBox.ItemsSource = _workingActions;
    }

    private void AddAction_OnClick(object sender, RoutedEventArgs e)
    {
        var action = new CommandAction
        {
                Name = "New Action",
            ActionType = CommandActionType.EchoMessage,
            TargetContainerId = string.Empty,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase),
            NoVerbLinkage = false,
            Verbs = new List<string>(),
            ChildCommandForwardingMode = ChildCommandForwardingMode.None
        };

        ActionPayloadAccessors.SetEchoMessage(action, string.Empty);

        _workingActions.Add(action);
        ActionsListBox.SelectedItem = action;
    }

    private void EditAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not CommandAction action)
        {
            return;
        }

        if (action.ActionType == CommandActionType.LinkedActions)
        {
            EditLinkedActionFlow(action);
            return;
        }

        var dialog = new RoomActionEditorDialog(action, _variableScope, _variableChoices, _echoReferenceTokens, _containerTargetChoices, _materializeSourceObjectChoices, _procedureChoices, _compositeRecipeChoices, _soundEffectChoices, _timerKeySuggestions, _workingActions)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void RemoveAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not CommandAction action)
        {
            return;
        }

        _workingActions.Remove(action);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateActions(showSuccessMessage: false))
        {
            return;
        }

        _targetActions.Clear();
        var usedAutoGeneratedNames = new HashSet<string>(
            _workingActions
                .Select(static action => action.Name?.Trim() ?? string.Empty)
                .Where(static name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);
        foreach (var action in _workingActions)
        {
            NormalizeAction(action, _scopeEntityName, _variableScope, usedAutoGeneratedNames);
            action.Payload = action.CreatePayloadSnapshot();
            _targetActions.Add(action);
        }

        DialogResult = true;
    }

    private void ValidateActions_OnClick(object sender, RoutedEventArgs e)
    {
        ValidateActions(showSuccessMessage: true);
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static CommandAction CloneAction(CommandAction source)
    {
        var sourceVerbs = source.Verbs.ToList();
        var isNoVerb = source.NoVerbLinkage || sourceVerbs.Count == 0;
        var sourceContainerTransfer = ActionPayloadAccessors.GetContainerTransfer(source);
        var sourceSynonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(source);
        var sourceMaterializeSourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(source);
        var sourceProcedureId = ActionPayloadAccessors.GetProcedureId(source);
        var sourceCompositeTargetObjectId = source.CompositeTargetObjectId;
        var sourceCompositeRecipeId = source.CompositeRecipeId;
        var sourceCompositeRequiredPartObjectIds = source.CompositeRequiredPartObjectIds.ToList();
        var sourceCompositeStrictPartCountEnforcement = source.CompositeStrictPartCountEnforcement;
        var sourceCompositeMinimumRequiredPartCount = source.CompositeMinimumRequiredPartCount;
        var sourceCompositeMatchMode = source.CompositeMatchMode;
        var sourceCompositeAmbiguityPolicy = source.CompositeAmbiguityPolicy;
        var sourceCompositePartConsumptionMode = source.CompositePartConsumptionMode;
        var sourceCompositeResolvedTargetOutputTemplate = source.CompositeResolvedTargetOutputTemplate;

        if (source.ActionType == CommandActionType.BuildCompositeByTarget)
        {
            var payload = ActionPayloadAccessors.GetCompositeByTargetPayload(source);
            sourceCompositeTargetObjectId = payload.CompositeTargetObjectId;
            sourceCompositeRecipeId = payload.CompositeRecipeId;
            sourceCompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            sourceCompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            sourceCompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            sourceCompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }
        else if (source.ActionType == CommandActionType.BuildCompositeByParts)
        {
            var payload = ActionPayloadAccessors.GetCompositeByPartsPayload(source);
            sourceCompositeTargetObjectId = payload.CompositeTargetObjectId;
            sourceCompositeRecipeId = payload.CompositeRecipeId;
            sourceCompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            sourceCompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            sourceCompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            sourceCompositeMatchMode = payload.CompositeMatchMode;
            sourceCompositeAmbiguityPolicy = payload.CompositeAmbiguityPolicy;
            sourceCompositePartConsumptionMode = payload.CompositePartConsumptionMode;
            sourceCompositeResolvedTargetOutputTemplate = payload.CompositeResolvedTargetOutputTemplate;
        }
        else if (source.ActionType == CommandActionType.BreakCompositeItem)
        {
            var payload = ActionPayloadAccessors.GetBreakCompositePayload(source);
            sourceCompositeTargetObjectId = payload.CompositeTargetObjectId;
            sourceCompositeRecipeId = payload.CompositeRecipeId;
            sourceCompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds.ToList();
            sourceCompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement;
            sourceCompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount;
            sourceCompositePartConsumptionMode = payload.CompositePartConsumptionMode;
        }

        return new CommandAction
        {
            Id = source.Id,
            Name = source.Name,
            ActionType = source.ActionType,
            NoVerbLinkage = isNoVerb,
            FlagName = ActionPayloadAccessors.GetCheckPropertyName(source),
            FlagValue = ActionPayloadAccessors.GetCheckExpectedValue(source),
            GamePropertyName = ActionPayloadAccessors.GetSetPropertyName(source),
            GamePropertyValue = ActionPayloadAccessors.GetSetPropertyValue(source),
            TargetContainerId = sourceContainerTransfer.TargetContainerId,
            SynonymTargetActionId = sourceSynonymTargetActionId,
            MaterializeSourceObjectId = sourceMaterializeSourceObjectId,
            ProcedureId = sourceProcedureId,
            CompositeTargetObjectId = sourceCompositeTargetObjectId,
            CompositeRecipeId = sourceCompositeRecipeId,
            CompositeRequiredPartObjectIds = sourceCompositeRequiredPartObjectIds,
            CompositeStrictPartCountEnforcement = sourceCompositeStrictPartCountEnforcement,
            CompositeMinimumRequiredPartCount = sourceCompositeMinimumRequiredPartCount,
            CompositeMatchMode = sourceCompositeMatchMode,
            CompositeAmbiguityPolicy = sourceCompositeAmbiguityPolicy,
            CompositePartConsumptionMode = sourceCompositePartConsumptionMode,
            CompositeResolvedTargetOutputTemplate = sourceCompositeResolvedTargetOutputTemplate,
            Verbs = isNoVerb ? new List<string>() : sourceVerbs,
            DirectionQualifierText = source.DirectionQualifierText,
            ChildCommandForwardingMode = source.ChildCommandForwardingMode,
            OutcomeMessageMap = source.OutcomeMessageMap,
            OutcomeSoundEffectsMap = CommandAction.CloneOutcomeSoundEffectsMap(source.OutcomeSoundEffectsMap),
            LinkedActions = source.LinkedActions.Select(static link => new LinkedActionReference
            {
                ActionId = link.ActionId,
                RunWhen = link.RunWhen,
                Order = link.Order
            }).ToList(),
            Payload = source.CreatePayloadSnapshot()
        };
    }

    private static void NormalizeAction(
        CommandAction action,
        string scopeEntityName,
        PropertyResolutionScope variableScope,
        ISet<string> usedAutoGeneratedNames)
    {
        var verbs = ParseVerbEntry(action.VerbListText);

        action.NoVerbLinkage = verbs.Count == 0;
        action.Verbs = verbs;
        action.DirectionQualifierText = action.DirectionQualifierText?.Trim() ?? string.Empty;

        if (action.NoVerbLinkage)
        {
            action.DirectionQualifierText = string.Empty;
        }

        var originalName = action.Name?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(originalName))
        {
            action.Name = originalName;
            return;
        }

        if (action.NoVerbLinkage)
        {
            // No-verb actions are orchestration/helper nodes and require explicit producer naming.
            action.Name = string.Empty;
            return;
        }

        var verbToken = NormalizeNameToken(verbs.FirstOrDefault(), "verb");
        var directionalToken = NormalizeNameToken(action.DirectionQualifierText, string.Empty);
        var scopeFallback = variableScope.ToString();
        var scopeToken = NormalizeNameToken(scopeEntityName, scopeFallback);

        var segments = new List<string> { verbToken };
        if (!string.IsNullOrWhiteSpace(directionalToken))
        {
            segments.Add(directionalToken);
        }

        segments.Add(scopeToken);
        var generatedBaseName = string.Join("_", segments);
        action.Name = BuildUniqueGeneratedName(generatedBaseName, usedAutoGeneratedNames);
    }

    private static string NormalizeNameToken(string? token, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(token) ? fallback : token;
        var normalized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());

        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? fallback.ToLowerInvariant() : normalized;
    }

    private static string BuildUniqueGeneratedName(string baseName, ISet<string> usedNames)
    {
        if (!usedNames.Contains(baseName))
        {
            usedNames.Add(baseName);
            return baseName;
        }

        var index = 2;
        while (true)
        {
            var candidate = $"{baseName}_{index}";
            if (!usedNames.Contains(candidate))
            {
                usedNames.Add(candidate);
                return candidate;
            }

            index++;
        }
    }

    private bool ValidateActions(bool showSuccessMessage)
    {
        if (_validateWithPipeline is not null)
        {
            return ValidateActionsWithPipeline(showSuccessMessage);
        }

        var errors = new List<string>();

        foreach (var action in _workingActions)
        {
            if (action.NoVerbLinkage && string.IsNullOrWhiteSpace(action.Name))
            {
                errors.Add("Action with 'No verb' enabled requires an explicit action name.");
            }

            if (action.ActionType == CommandActionType.EchoMessage)
            {
                ValidateScriptField(action, ActionPayloadAccessors.GetEchoMessage(action), "Echo message", _echoReferenceTokens, errors);
            }

            var successOutcomeScript = GetOutcomeMessage(action, "Success");
            if (!string.IsNullOrWhiteSpace(successOutcomeScript))
            {
                ValidateScriptField(action, successOutcomeScript, "Success echo message", _echoReferenceTokens, errors);
            }

            var failureOutcomeScript = GetOutcomeMessage(action, "Failure");
            if (!string.IsNullOrWhiteSpace(failureOutcomeScript))
            {
                ValidateScriptField(action, failureOutcomeScript, "Failure echo message", _echoReferenceTokens, errors);
            }

            if (action.ActionType == CommandActionType.SetGameProperty)
            {
                var variableTokens = _variableChoices
                    .Select(choice => choice.Value)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                ValidateScriptField(action, ActionPayloadAccessors.GetSetPropertyValue(action), "Game property value", variableTokens, errors);
            }
        }

        errors.AddRange(ValidateDuplicateVerbUsage(_workingActions));

        errors.AddRange(LinkedActionGraphValidator.Validate(_workingActions, _scopeLabel, _additionalLinkTargetActions));

        if (errors.Count > 0)
        {
            var displayed = string.Join(Environment.NewLine, errors.Take(12));
            var suffix = errors.Count > 12 ? Environment.NewLine + "..." : string.Empty;
            System.Windows.MessageBox.Show(
                this,
                $"Action validation found issues:{Environment.NewLine}{displayed}{suffix}",
                "Validate Game Actions",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (showSuccessMessage)
        {
            System.Windows.MessageBox.Show(
                this,
                "All game actions validated successfully.",
                "Validate Game Actions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return true;
    }

    private bool ValidateActionsWithPipeline(bool showSuccessMessage)
    {
        var issues = _validateWithPipeline?.Invoke(_workingActions.ToList())
                     ?? Array.Empty<ProjectValidationIssue>();

        if (issues.Count > 0)
        {
            var lines = issues
                .Take(12)
                .Select(issue =>
                {
                    var hint = string.IsNullOrWhiteSpace(issue.Hint) ? string.Empty : $" Fix: {issue.Hint}";
                    return $"[{issue.RuleId}] {issue.Path}: {issue.Description}{hint}";
                });

            var displayed = string.Join(Environment.NewLine, lines);
            var suffix = issues.Count > 12 ? Environment.NewLine + "..." : string.Empty;

            System.Windows.MessageBox.Show(
                this,
                $"Action validation found issues:{Environment.NewLine}{displayed}{suffix}",
                "Validate Game Actions",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        if (showSuccessMessage)
        {
            System.Windows.MessageBox.Show(
                this,
                "All game actions validated successfully.",
                "Validate Game Actions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return true;
    }

    private static IEnumerable<string> ValidateDuplicateVerbUsage(IEnumerable<CommandAction> actions)
    {
        var triggerMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actions)
        {
            if (action.NoVerbLinkage || action.Verbs.Count == 0)
            {
                continue;
            }

            var actionLabel = string.IsNullOrWhiteSpace(action.Name)
                ? $"(unnamed {action.Id:N})"
                : action.Name.Trim();

            foreach (var verb in action.Verbs)
            {
                var normalizedVerb = verb?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedVerb))
                {
                    continue;
                }

                var normalizedDirectional = action.DirectionQualifierText?.Trim() ?? string.Empty;
                var triggerKey = string.IsNullOrWhiteSpace(normalizedDirectional)
                    ? normalizedVerb
                    : $"{normalizedVerb}|{normalizedDirectional}";

                if (!triggerMap.TryGetValue(triggerKey, out var labels))
                {
                    labels = new List<string>();
                    triggerMap[triggerKey] = labels;
                }

                if (!labels.Contains(actionLabel, StringComparer.OrdinalIgnoreCase))
                {
                    labels.Add(actionLabel);
                }
            }
        }

        foreach (var pair in triggerMap.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            var joined = string.Join(", ", pair.Value.OrderBy(static label => label, StringComparer.OrdinalIgnoreCase));
            var split = pair.Key.Split('|', 2, StringSplitOptions.TrimEntries);
            var verb = split[0];
            var directional = split.Length > 1 ? split[1] : string.Empty;
            var triggerText = string.IsNullOrWhiteSpace(directional)
                ? verb
                : $"{verb} {directional}";
            yield return $"Trigger '{triggerText}' is assigned to multiple actions in this scope: {joined}. Keep each verb/directional trigger on a single action.";
        }
    }

    private static void ValidateScriptField(
        CommandAction action,
        string? script,
        string fieldLabel,
        IReadOnlyList<string> referenceTokens,
        List<string> errors)
    {
        var diagnostics = ActionScriptEditorDiagnosticsAnalyzer.Analyze(script ?? string.Empty, referenceTokens);
        if (!diagnostics.HasBlockingErrors)
        {
            return;
        }

        foreach (var error in diagnostics.Errors)
        {
            errors.Add($"Action '{action.Name}': {fieldLabel}: {error}");
        }
    }

    private static string GetOutcomeMessage(CommandAction action, string token)
    {
        return action.OutcomeMessageMap.TryGetValue(token, out var script)
            ? script ?? string.Empty
            : string.Empty;
    }

    private void EditLinkedActionFlow(CommandAction action)
    {
        var dialog = new LinkedActionTreeEditorDialog(
            _scopeLabel,
            action,
            _workingActions,
            _additionalLinkTargetActions,
            _variableScope,
            _variableChoices,
            _echoReferenceTokens,
            _containerTargetChoices,
            _materializeSourceObjectChoices,
            _procedureChoices,
                _compositeRecipeChoices,
                _soundEffectChoices,
                _timerKeySuggestions)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private static IReadOnlyList<string> BuildVerbEntrySuggestions(IReadOnlyList<string> verbSuggestions)
    {
        var values = new List<string> { NoVerbDisplayValue };
        foreach (var suggestion in verbSuggestions)
        {
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                continue;
            }

            if (string.Equals(suggestion.Trim(), NoVerbDisplayValue, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values.Add(suggestion);
        }

        return values;
    }

    private void VerbEditor_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || textBox.DataContext is not CommandAction action)
        {
            return;
        }

        e.Handled = true;

        var unavailableVerbs = BuildUnavailableVerbMap(action, action.DirectionQualifierText);

        var dialog = new ActionVerbPickerDialog(_verbEntrySuggestions, action.Verbs, unavailableVerbs, action.DirectionQualifierText)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        action.Verbs = dialog.SelectedVerbs.ToList();
        action.NoVerbLinkage = action.Verbs.Count == 0;
        if (action.NoVerbLinkage)
        {
            action.DirectionQualifierText = string.Empty;
        }
    }

    private Dictionary<string, string> BuildUnavailableVerbMap(CommandAction currentAction, string? directionalQualifier)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var targetDirectional = directionalQualifier?.Trim() ?? string.Empty;

        foreach (var action in _workingActions)
        {
            if (ReferenceEquals(action, currentAction) || action.NoVerbLinkage || action.Verbs.Count == 0)
            {
                continue;
            }

            var actionDirectional = action.DirectionQualifierText?.Trim() ?? string.Empty;
            if (!string.Equals(actionDirectional, targetDirectional, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var actionLabel = string.IsNullOrWhiteSpace(action.Name)
                ? $"(unnamed {action.Id:N})"
                : action.Name.Trim();

            foreach (var verb in action.Verbs)
            {
                var normalized = verb?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!map.TryGetValue(normalized, out var labels))
                {
                    labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[normalized] = labels;
                }

                labels.Add(actionLabel);
            }
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => string.Join(", ", pair.Value.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ParseVerbEntry(string? value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => !string.Equals(token.Trim(), NoVerbDisplayValue, StringComparison.OrdinalIgnoreCase))
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
