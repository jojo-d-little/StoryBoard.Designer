using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.Commands;
using Storyboard.Shared.GameServices.RuntimeContext;

namespace StoryboardDesigner.App.Models;

public sealed partial class CommandAction
{
    public string GetOutcomeScript(string token)
    {
        var normalizedToken = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return string.Empty;
        }

        return OutcomeMessageMap.TryGetValue(normalizedToken, out var script)
            ? script ?? string.Empty
            : string.Empty;
    }

    public bool SetOutcomeScript(string token, string value)
    {
        var normalizedToken = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return false;
        }

        var normalized = value ?? string.Empty;
        var map = OutcomeMessageMap;
        if (map.TryGetValue(normalizedToken, out var existing)
            && string.Equals(existing ?? string.Empty, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        map[normalizedToken] = normalized;
        OutcomeMessageMap = map;
        return true;
    }

    public void NormalizeOutcomeMapForActionType(
        CommandActionType actionType,
        IReadOnlyDictionary<string, string>? defaultsByToken = null,
        bool preserveUnsupportedTokens = true)
    {
        var normalizedCurrent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, script) in OutcomeMessageMap)
        {
            var token = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (RuntimeActionResultCodeRegistry.TryGetDescriptor(actionType, token, out var descriptor))
            {
                normalizedCurrent[descriptor.Token] = script ?? string.Empty;
                continue;
            }

            normalizedCurrent[token] = script ?? string.Empty;
        }

        var normalizedDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (defaultsByToken is not null)
        {
            foreach (var (key, script) in defaultsByToken)
            {
                var token = key?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (!RuntimeActionResultCodeRegistry.TryGetDescriptor(actionType, token, out var descriptor))
                {
                    continue;
                }

                normalizedDefaults[descriptor.Token] = script ?? string.Empty;
            }
        }

        var normalizedOutcomeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var supported = RuntimeActionResultCodeRegistry.GetSupportedResultCodes(actionType);
        foreach (var descriptor in supported)
        {
            if (normalizedCurrent.TryGetValue(descriptor.Token, out var existingScript))
            {
                normalizedOutcomeMap[descriptor.Token] = existingScript;
                continue;
            }

            if (normalizedDefaults.TryGetValue(descriptor.Token, out var defaultScript))
            {
                normalizedOutcomeMap[descriptor.Token] = defaultScript;
                continue;
            }

            normalizedOutcomeMap[descriptor.Token] = string.Empty;
        }

        if (preserveUnsupportedTokens)
        {
            foreach (var (token, script) in normalizedCurrent)
            {
                if (RuntimeActionResultCodeRegistry.TryGetDescriptor(actionType, token, out _))
                {
                    continue;
                }

                normalizedOutcomeMap[token] = script;
            }
        }

        OutcomeMessageMap = normalizedOutcomeMap;
    }

    public string EchoMessage
    {
        get => GetOutcomeScript("Success");
        set
        {
            if (!SetOutcomeScript("Success", value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged();
        }
    }

    public string FlagName
    {
        get => CommandActionPayloadCoordinator.GetCheckPropertyName(_payload);
        set
        {
            var normalized = value ?? string.Empty;
            if (!CommandActionPayloadCoordinator.TrySetCheckPropertyName(ref _payload, normalized))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public bool FlagValue
    {
        get => CommandActionPayloadCoordinator.GetCheckExpectedValue(_payload);
        set
        {
            if (!CommandActionPayloadCoordinator.TrySetCheckExpectedValue(ref _payload, value))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string GamePropertyName
    {
        get => CommandActionPayloadCoordinator.GetSetPropertyName(_payload);
        set
        {
            var normalized = value ?? string.Empty;
            if (!CommandActionPayloadCoordinator.TrySetSetPropertyName(ref _payload, normalized))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string GamePropertyValue
    {
        get => CommandActionPayloadCoordinator.GetSetPropertyValue(_payload);
        set
        {
            var normalized = value ?? string.Empty;
            if (!CommandActionPayloadCoordinator.TrySetSetPropertyValue(ref _payload, normalized))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string TargetContainerId
    {
        get => _containerFacet.TargetContainerId;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_containerFacet.TargetContainerId, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _containerFacet = _containerFacet with { TargetContainerId = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string MoveDirectionToken
    {
        get => _moveFacet.DirectionToken;
        set
        {
            var normalized = NormalizeMoveDirectionToken(value);
            if (string.Equals(_moveFacet.DirectionToken, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _moveFacet = _moveFacet with { DirectionToken = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public int MoveDistanceInCells
    {
        get => _moveFacet.DistanceInCells;
        set
        {
            var normalized = value < 1 ? 1 : value;
            if (_moveFacet.DistanceInCells == normalized)
            {
                return;
            }

            _moveFacet = _moveFacet with { DistanceInCells = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public bool MoveAllowPartialMove
    {
        get => _moveFacet.AllowPartialMove;
        set
        {
            if (_moveFacet.AllowPartialMove == value)
            {
                return;
            }

            _moveFacet = _moveFacet with { AllowPartialMove = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public bool MoveAllowJumpOver
    {
        get => _moveFacet.AllowJumpOver;
        set
        {
            if (_moveFacet.AllowJumpOver == value)
            {
                return;
            }

            _moveFacet = _moveFacet with { AllowJumpOver = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public RuntimeMovementVisualTransitionHint MoveVisualTransitionHint
    {
        get => _moveFacet.VisualTransitionHint;
        set
        {
            if (_moveFacet.VisualTransitionHint == value)
            {
                return;
            }

            _moveFacet = _moveFacet with { VisualTransitionHint = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public RuntimeMovementTravelVisualizationMode MoveTravelVisualizationMode
    {
        get => _moveFacet.TravelVisualizationMode;
        set
        {
            if (_moveFacet.TravelVisualizationMode == value)
            {
                return;
            }

            _moveFacet = _moveFacet with { TravelVisualizationMode = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public RuntimeRotateRoomObjectOnGridAttemptMode RotateMode
    {
        get => _rotateFacet.Mode;
        set
        {
            var normalized = Enum.IsDefined(value)
                ? value
                : RuntimeRotateRoomObjectOnGridAttemptMode.Turn;

            if (_rotateFacet.Mode == normalized)
            {
                return;
            }

            _rotateFacet = _rotateFacet with { Mode = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public int? RotateTurnDegrees
    {
        get => _rotateFacet.TurnDegrees;
        set
        {
            if (_rotateFacet.TurnDegrees == value)
            {
                return;
            }

            _rotateFacet = _rotateFacet with { TurnDegrees = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string RotateFacingDirectionToken
    {
        get => _rotateFacet.FacingDirectionToken;
        set
        {
            var normalized = NormalizeRotateFacingDirectionToken(value);
            if (string.Equals(_rotateFacet.FacingDirectionToken, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _rotateFacet = _rotateFacet with { FacingDirectionToken = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public RuntimeMovementVisualTransitionHint RotateVisualTransitionHint
    {
        get => _rotateFacet.VisualTransitionHint;
        set
        {
            if (_rotateFacet.VisualTransitionHint == value)
            {
                return;
            }

            _rotateFacet = _rotateFacet with { VisualTransitionHint = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public RuntimeMovementVisualTransitionHint StackVisualTransitionHint
    {
        get => _stackFacet.VisualTransitionHint;
        set
        {
            if (_stackFacet.VisualTransitionHint == value)
            {
                return;
            }

            _stackFacet = _stackFacet with { VisualTransitionHint = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string SelectionCueEffectKey
    {
        get => _setActiveRoomObjectFacet.SelectionCueEffectKey;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_setActiveRoomObjectFacet.SelectionCueEffectKey, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _setActiveRoomObjectFacet = _setActiveRoomObjectFacet with { SelectionCueEffectKey = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public RuntimeClearActiveRoomObjectsScope ClearActiveRoomObjectsScope
    {
        get => _clearActiveRoomObjectsFacet.ClearScope;
        set
        {
            if (_clearActiveRoomObjectsFacet.ClearScope == value)
            {
                return;
            }

            _clearActiveRoomObjectsFacet = _clearActiveRoomObjectsFacet with { ClearScope = value };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    private static string NormalizeMoveDirectionToken(string? value)
    {
        var token = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        if (GameCommandDirectionFormatting.TryParseToken(token, out var parsed))
        {
            return parsed.ToToken();
        }

        if (Enum.TryParse<GameCommandDirection>(token, true, out var byEnumName))
        {
            return byEnumName.ToToken();
        }

        return token;
    }

    private static string NormalizeRotateFacingDirectionToken(string? value)
    {
        var token = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var compact = new string(token
            .ToUpperInvariant()
            .Where(static character => !char.IsWhiteSpace(character) && character != '-' && character != '_')
            .ToArray());
        var mapped = compact switch
        {
            "N" or "NORTH" => "N",
            "NE" or "NORTHEAST" => "NE",
            "E" or "EAST" => "E",
            "SE" or "SOUTHEAST" => "SE",
            "S" or "SOUTH" => "S",
            "SW" or "SOUTHWEST" => "SW",
            "W" or "WEST" => "W",
            "NW" or "NORTHWEST" => "NW",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        if (GameCommandDirectionFormatting.TryParseToken(token, out var parsed)
            && IsEightWayDirection(parsed))
        {
            return parsed.ToToken();
        }

        if (Enum.TryParse<GameCommandDirection>(token, true, out var byEnumName)
            && IsEightWayDirection(byEnumName))
        {
            return byEnumName.ToToken();
        }

        return token;
    }

    private static bool IsEightWayDirection(GameCommandDirection direction)
    {
        return direction is GameCommandDirection.North
            or GameCommandDirection.NorthEast
            or GameCommandDirection.East
            or GameCommandDirection.SouthEast
            or GameCommandDirection.South
            or GameCommandDirection.SouthWest
            or GameCommandDirection.West
            or GameCommandDirection.NorthWest;
    }

    public Guid? SynonymTargetActionId
    {
        get => CommandActionPayloadCoordinator.GetSynonymTargetActionId(_payload);
        set
        {
            if (!CommandActionPayloadCoordinator.TrySetSynonymTargetActionId(ref _payload, value))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public Guid? MaterializeSourceObjectId
    {
        get => CommandActionPayloadCoordinator.GetMaterializeSourceObjectId(_payload);
        set
        {
            if (!CommandActionPayloadCoordinator.TrySetMaterializeSourceObjectId(ref _payload, value))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public Guid? ProcedureId
    {
        get => CommandActionPayloadCoordinator.GetProcedureId(_payload);
        set
        {
            if (!CommandActionPayloadCoordinator.TrySetProcedureId(ref _payload, value))
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public Guid? CompositeTargetObjectId
    {
        get => ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => _compositeByTargetFacet.CompositeTargetObjectId,
            CommandActionType.BuildCompositeByParts => _compositeByPartsFacet.CompositeTargetObjectId,
            CommandActionType.BreakCompositeItem => _breakCompositeFacet.CompositeTargetObjectId,
            _ => _compositeByPartsFacet.CompositeTargetObjectId
                 ?? _compositeByTargetFacet.CompositeTargetObjectId
                 ?? _breakCompositeFacet.CompositeTargetObjectId
        };
        set
        {
            var changed = false;
            if (_compositeByTargetFacet.CompositeTargetObjectId != value)
            {
                _compositeByTargetFacet = _compositeByTargetFacet with { CompositeTargetObjectId = value };
                changed = true;
            }

            if (_compositeByPartsFacet.CompositeTargetObjectId != value)
            {
                _compositeByPartsFacet = _compositeByPartsFacet with { CompositeTargetObjectId = value };
                changed = true;
            }

            if (_breakCompositeFacet.CompositeTargetObjectId != value)
            {
                _breakCompositeFacet = _breakCompositeFacet with { CompositeTargetObjectId = value };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public Guid? CompositeRecipeId
    {
        get => ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => _compositeByTargetFacet.CompositeRecipeId,
            CommandActionType.BuildCompositeByParts => _compositeByPartsFacet.CompositeRecipeId,
            CommandActionType.BreakCompositeItem => _breakCompositeFacet.CompositeRecipeId,
            _ => _compositeByPartsFacet.CompositeRecipeId
                 ?? _compositeByTargetFacet.CompositeRecipeId
                 ?? _breakCompositeFacet.CompositeRecipeId
        };
        set
        {
            var changed = false;
            if (_compositeByTargetFacet.CompositeRecipeId != value)
            {
                _compositeByTargetFacet = _compositeByTargetFacet with { CompositeRecipeId = value };
                changed = true;
            }

            if (_compositeByPartsFacet.CompositeRecipeId != value)
            {
                _compositeByPartsFacet = _compositeByPartsFacet with { CompositeRecipeId = value };
                changed = true;
            }

            if (_breakCompositeFacet.CompositeRecipeId != value)
            {
                _breakCompositeFacet = _breakCompositeFacet with { CompositeRecipeId = value };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public List<Guid> CompositeRequiredPartObjectIds
    {
        get => ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => _compositeByTargetFacet.CompositeRequiredPartObjectIds.ToList(),
            CommandActionType.BuildCompositeByParts => _compositeByPartsFacet.CompositeRequiredPartObjectIds.ToList(),
            CommandActionType.BreakCompositeItem => _breakCompositeFacet.CompositeRequiredPartObjectIds.ToList(),
            _ => _compositeByPartsFacet.CompositeRequiredPartObjectIds.Count > 0
                ? _compositeByPartsFacet.CompositeRequiredPartObjectIds.ToList()
                : _compositeByTargetFacet.CompositeRequiredPartObjectIds.Count > 0
                    ? _compositeByTargetFacet.CompositeRequiredPartObjectIds.ToList()
                    : _breakCompositeFacet.CompositeRequiredPartObjectIds.ToList()
        };
        set
        {
            var normalized = value ?? new List<Guid>();
            var changed = false;
            if (!_compositeByTargetFacet.CompositeRequiredPartObjectIds.SequenceEqual(normalized))
            {
                _compositeByTargetFacet = _compositeByTargetFacet with { CompositeRequiredPartObjectIds = normalized.ToList() };
                changed = true;
            }

            if (!_compositeByPartsFacet.CompositeRequiredPartObjectIds.SequenceEqual(normalized))
            {
                _compositeByPartsFacet = _compositeByPartsFacet with { CompositeRequiredPartObjectIds = normalized.ToList() };
                changed = true;
            }

            if (!_breakCompositeFacet.CompositeRequiredPartObjectIds.SequenceEqual(normalized))
            {
                _breakCompositeFacet = _breakCompositeFacet with { CompositeRequiredPartObjectIds = normalized.ToList() };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public bool? CompositeStrictPartCountEnforcement
    {
        get => ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => _compositeByTargetFacet.CompositeStrictPartCountEnforcement,
            CommandActionType.BuildCompositeByParts => _compositeByPartsFacet.CompositeStrictPartCountEnforcement,
            CommandActionType.BreakCompositeItem => _breakCompositeFacet.CompositeStrictPartCountEnforcement,
            _ => _compositeByPartsFacet.CompositeStrictPartCountEnforcement
                 ?? _compositeByTargetFacet.CompositeStrictPartCountEnforcement
                 ?? _breakCompositeFacet.CompositeStrictPartCountEnforcement
        };
        set
        {
            var changed = false;
            if (_compositeByTargetFacet.CompositeStrictPartCountEnforcement != value)
            {
                _compositeByTargetFacet = _compositeByTargetFacet with { CompositeStrictPartCountEnforcement = value };
                changed = true;
            }

            if (_compositeByPartsFacet.CompositeStrictPartCountEnforcement != value)
            {
                _compositeByPartsFacet = _compositeByPartsFacet with { CompositeStrictPartCountEnforcement = value };
                changed = true;
            }

            if (_breakCompositeFacet.CompositeStrictPartCountEnforcement != value)
            {
                _breakCompositeFacet = _breakCompositeFacet with { CompositeStrictPartCountEnforcement = value };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public int? CompositeMinimumRequiredPartCount
    {
        get => ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => _compositeByTargetFacet.CompositeMinimumRequiredPartCount,
            CommandActionType.BuildCompositeByParts => _compositeByPartsFacet.CompositeMinimumRequiredPartCount,
            CommandActionType.BreakCompositeItem => _breakCompositeFacet.CompositeMinimumRequiredPartCount,
            _ => _compositeByPartsFacet.CompositeMinimumRequiredPartCount
                 ?? _compositeByTargetFacet.CompositeMinimumRequiredPartCount
                 ?? _breakCompositeFacet.CompositeMinimumRequiredPartCount
        };
        set
        {
            var normalized = value.HasValue && value.Value < 1 ? 1 : value;
            var changed = false;
            if (_compositeByTargetFacet.CompositeMinimumRequiredPartCount != normalized)
            {
                _compositeByTargetFacet = _compositeByTargetFacet with { CompositeMinimumRequiredPartCount = normalized };
                changed = true;
            }

            if (_compositeByPartsFacet.CompositeMinimumRequiredPartCount != normalized)
            {
                _compositeByPartsFacet = _compositeByPartsFacet with { CompositeMinimumRequiredPartCount = normalized };
                changed = true;
            }

            if (_breakCompositeFacet.CompositeMinimumRequiredPartCount != normalized)
            {
                _breakCompositeFacet = _breakCompositeFacet with { CompositeMinimumRequiredPartCount = normalized };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string CompositeMatchMode
    {
        get => _compositeByPartsFacet.CompositeMatchMode;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_compositeByPartsFacet.CompositeMatchMode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _compositeByPartsFacet = _compositeByPartsFacet with { CompositeMatchMode = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string CompositeAmbiguityPolicy
    {
        get => _compositeByPartsFacet.CompositeAmbiguityPolicy;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_compositeByPartsFacet.CompositeAmbiguityPolicy, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _compositeByPartsFacet = _compositeByPartsFacet with { CompositeAmbiguityPolicy = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string CompositePartConsumptionMode
    {
        get => ActionType switch
        {
            CommandActionType.BuildCompositeByTarget => _compositeByTargetFacet.CompositePartConsumptionMode,
            CommandActionType.BuildCompositeByParts => _compositeByPartsFacet.CompositePartConsumptionMode,
            CommandActionType.BreakCompositeItem => _breakCompositeFacet.CompositePartConsumptionMode,
            _ => string.IsNullOrWhiteSpace(_compositeByPartsFacet.CompositePartConsumptionMode)
                ? string.IsNullOrWhiteSpace(_compositeByTargetFacet.CompositePartConsumptionMode)
                    ? _breakCompositeFacet.CompositePartConsumptionMode
                    : _compositeByTargetFacet.CompositePartConsumptionMode
                : _compositeByPartsFacet.CompositePartConsumptionMode
        };
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "ContainedInComposite" : value.Trim();
            var changed = false;
            if (!string.Equals(_compositeByTargetFacet.CompositePartConsumptionMode, normalized, StringComparison.Ordinal))
            {
                _compositeByTargetFacet = _compositeByTargetFacet with { CompositePartConsumptionMode = normalized };
                changed = true;
            }

            if (!string.Equals(_compositeByPartsFacet.CompositePartConsumptionMode, normalized, StringComparison.Ordinal))
            {
                _compositeByPartsFacet = _compositeByPartsFacet with { CompositePartConsumptionMode = normalized };
                changed = true;
            }

            if (!string.Equals(_breakCompositeFacet.CompositePartConsumptionMode, normalized, StringComparison.Ordinal))
            {
                _breakCompositeFacet = _breakCompositeFacet with { CompositePartConsumptionMode = normalized };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

    public string CompositeResolvedTargetOutputTemplate
    {
        get => _compositeByPartsFacet.CompositeResolvedTargetOutputTemplate;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_compositeByPartsFacet.CompositeResolvedTargetOutputTemplate, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _compositeByPartsFacet = _compositeByPartsFacet with { CompositeResolvedTargetOutputTemplate = normalized };
            OnPropertyChanged();
            RaiseSummaryChanged();
        }
    }

}
