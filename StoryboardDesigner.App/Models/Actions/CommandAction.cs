using System.ComponentModel;
using System.Runtime.CompilerServices;
using Storyboard.Shared.GameServices;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Models;

public sealed partial class CommandAction : INotifyPropertyChanged, 
                                                IActionScriptFieldProvider, 
                                                IActionLinkedActionsProvider, 
                                                IActionReferenceTokenProvider
{
    private string _name = "New Action";
    private CommandActionType _actionType = CommandActionType.EchoMessage;
    private IActionPayload _payload = new EchoPayload();
    private Dictionary<string, string> _outcomeMessageMap = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<OutcomeSoundEffectCue>> _outcomeSoundEffectsMap = new(StringComparer.OrdinalIgnoreCase);
    private List<LinkedActionReference> _linkedActions = new();
    private bool _noVerbLinkage;
    private List<string> _verbs = new();
    private string _directionQualifierText = string.Empty;
    private ChildCommandForwardingMode _childCommandForwardingMode;
    private SimilarChildDispatchMode _similarChildDispatchMode = SimilarChildDispatchMode.SingleMatchingChild;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public CommandActionType ActionType
    {
        get => _actionType;
        set
        {
            if (_actionType == value)
            {
                return;
            }

            _actionType = value;
            EnsurePayloadForCurrentActionType();
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public Dictionary<string, string> OutcomeMessageMap
    {
        get => new(_outcomeMessageMap, StringComparer.OrdinalIgnoreCase);
        set
        {
            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, script) in value ?? new Dictionary<string, string>())
            {
                var token = key?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                normalized[token] = script ?? string.Empty;
            }

            _outcomeMessageMap = normalized;
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public bool NoVerbLinkage
    {
        get => _noVerbLinkage;
        set
        {
            if (_noVerbLinkage == value)
            {
                return;
            }

            _noVerbLinkage = value;
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public List<LinkedActionReference> LinkedActions
    {
        get => _linkedActions;
        set => _linkedActions = value ?? new List<LinkedActionReference>();
    }

    public Dictionary<string, List<OutcomeSoundEffectCue>> OutcomeSoundEffectsMap
    {
        get => CloneOutcomeSoundEffectsMap(_outcomeSoundEffectsMap);
        set
        {
            _outcomeSoundEffectsMap = NormalizeOutcomeSoundEffectsMap(value);
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public List<string> Verbs
    {
        get => _verbs;
        set => SetVerbs(NormalizeVerbValues(value ?? new List<string>()));
    }

    public string VerbListText
    {
        get => string.Join(", ", _verbs);
        set
        {
            SetVerbs(NormalizeVerbValues(ParseVerbList(value)));
        }
    }

    public string PrimaryVerb
    {
        get => _verbs.FirstOrDefault() ?? string.Empty;
        set
        {
            var normalizedPrimary = value?.Trim() ?? string.Empty;
            var next = _verbs.Skip(1).ToList();
            if (!string.IsNullOrWhiteSpace(normalizedPrimary))
            {
                next.Insert(0, normalizedPrimary);
            }

            SetVerbs(NormalizeVerbValues(next));
        }
    }

    public string AdditionalVerbsText
    {
        get => string.Join(", ", _verbs.Skip(1));
        set
        {
            var next = ParseVerbList(value);
            var primary = _verbs.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(primary))
            {
                next.Insert(0, primary);
            }

            SetVerbs(NormalizeVerbValues(next));
        }
    }

    public GameCommandDirection Direction
    {
        get
        {
            return GameCommandDirectionFormatting.TryParseToken(_directionQualifierText, out var parsed)
                ? parsed
                : GameCommandDirection.Unknown;
        }
        set
        {
            DirectionQualifierText = value.ToToken();
        }
    }

    public string DirectionQualifierText
    {
        get => _directionQualifierText;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_directionQualifierText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _directionQualifierText = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Direction));
            RaiseSummaryChanged();
        }
    }

    public ChildCommandForwardingMode ChildCommandForwardingMode
    {
        get => _childCommandForwardingMode;
        set
        {
            if (_childCommandForwardingMode == value)
            {
                return;
            }

            _childCommandForwardingMode = value;
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public SimilarChildDispatchMode SimilarChildDispatchMode
    {
        get => _similarChildDispatchMode;
        set
        {
            if (_similarChildDispatchMode == value)
            {
                return;
            }

            _similarChildDispatchMode = value;
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }


    public string Summary => CommandActionSummaryPresenter.BuildSummary(this);

    public string SummaryPreview => CommandActionSummaryPresenter.BuildSummaryPreview(this);

    public string SummaryTooltip => CommandActionSummaryPresenter.BuildSummaryTooltip(this);

    public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        var payload = Payload;
        if (payload is not null)
        {
            foreach (var scriptField in payload.GetScriptFields())
            {
                fields[scriptField.FieldName] = scriptField.Value ?? string.Empty;
            }
        }

        foreach (var (token, script) in _outcomeMessageMap)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            fields[$"OutcomeMessageMap[{token}]"] = script ?? string.Empty;
        }

        return fields.Select(static kvp => (kvp.Key, kvp.Value)).ToList();
    }

    public IReadOnlyList<LinkedActionReference> GetLinkedActions()
    {
        var payloadLinks = Payload?.GetLinkedActions() ?? [];
        if (payloadLinks.Count > 0 || ActionType == CommandActionType.Synonym || ActionType == CommandActionType.LinkedActions)
        {
            return payloadLinks;
        }

        return LinkedActions;
    }

    public IReadOnlyList<LinkedActionReference> GetActionLinkedActions() => GetLinkedActions();

    public IReadOnlyList<string> GetReferenceTokens()
    {
        return (Payload?.GetReferenceTokens() ?? [])
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Select(static token => token.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Guid? GetSynonymTargetActionId()
    {
        if (ActionType != CommandActionType.Synonym)
        {
            return null;
        }

        return GetLinkedActions()
            .Select(static link => (Guid?)link.ActionId)
            .FirstOrDefault();
    }

    private void RaiseSummaryChanged()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(SummaryPreview));
        OnPropertyChanged(nameof(SummaryTooltip));
    }


    private void SetVerbs(List<string> verbs)
    {
        if (_verbs.SequenceEqual(verbs, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _verbs = verbs;
        OnPropertyChanged(nameof(Verbs));
        OnPropertyChanged(nameof(VerbListText));
        OnPropertyChanged(nameof(PrimaryVerb));
        OnPropertyChanged(nameof(AdditionalVerbsText));
        RaiseSummaryChanged();
    }

    private static List<string> ParseVerbList(string? value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    private static List<string> NormalizeVerbValues(IEnumerable<string> values)
    {
        var verbs = new List<string>();
        foreach (var value in values)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (verbs.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            verbs.Add(normalized);
        }

        return verbs;
    }

    private static Dictionary<string, List<OutcomeSoundEffectCue>> NormalizeOutcomeSoundEffectsMap(
        IReadOnlyDictionary<string, List<OutcomeSoundEffectCue>>? value)
    {
        var normalized = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase);
        if (value is null)
        {
            return normalized;
        }

        foreach (var (key, cues) in value)
        {
            var token = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var normalizedCues = (cues ?? new List<OutcomeSoundEffectCue>())
                .Where(static cue => cue is not null && cue.SoundEffectId != Guid.Empty)
                .Select(CloneOutcomeSoundEffectCue)
                .ToList();

            normalized[token] = normalizedCues;
        }

        return normalized;
    }

    public static Dictionary<string, List<OutcomeSoundEffectCue>> CloneOutcomeSoundEffectsMap(
        IReadOnlyDictionary<string, List<OutcomeSoundEffectCue>>? source)
    {
        var clone = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return clone;
        }

        foreach (var (token, cues) in source)
        {
            var normalizedToken = token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                continue;
            }

            clone[normalizedToken] = (cues ?? new List<OutcomeSoundEffectCue>())
                .Where(static cue => cue is not null && cue.SoundEffectId != Guid.Empty)
                .Select(CloneOutcomeSoundEffectCue)
                .ToList();
        }

        return clone;
    }

    public static OutcomeSoundEffectCue CloneOutcomeSoundEffectCue(OutcomeSoundEffectCue source)
    {
        return new OutcomeSoundEffectCue
        {
            SoundEffectId = source.SoundEffectId,
            SoundEffectKeyHint = source.SoundEffectKeyHint,
            Enabled = source.Enabled,
            LateDeliveryPolicy = source.LateDeliveryPolicy,
            MaxLateMs = source.MaxLateMs
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
