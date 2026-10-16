using System.Globalization;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ComputedGamePropertyIntegrityRule : IValidationRule
{
    private static readonly HashSet<string> EligibleAnchors = new(StringComparer.OrdinalIgnoreCase)
    {
        "currentRoom", "currentArea", "currentCountry", "currentPlanet", "currentPhase", "activePlayer"
    };

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };
    public ValidationRuleMetadata Metadata { get; } = new("CGP-001", "Computed Game Property Integrity", ValidationSeverity.Error, "Computed Game Properties");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not ProjectModel project) yield break;
        var owners = EnumerateOwners(project).ToList();
        var allDefinitions = owners.SelectMany(owner => owner.Properties.Select(property => (Owner: owner, Property: property))).ToList();

        foreach (var (owner, property) in allDefinitions.Where(static entry => entry.Property.Computation is not null))
        {
            var path = $"{owner.Path}.Game Properties.{property.Name}";
            if (!string.Equals(property.Computation?.ResultKind, "Boolean", StringComparison.OrdinalIgnoreCase))
                yield return Issue(path, "Only Boolean computed results are supported.");
            if (property.ValueRestriction != GamePropertyValueRestriction.TrueFalse)
                yield return Issue(path, "Computed Boolean properties must use the TrueFalse value restriction.");
            if (!string.Equals(property.DefaultValue, "false", StringComparison.OrdinalIgnoreCase))
                yield return Issue(path, "A computed Boolean property must serialize false as its compatibility default.");
            foreach (var message in ValidateCondition(property.Computation?.Condition, owner.Id, owner.Scope, allDefinitions))
                yield return Issue(path, message);
        }

        foreach (var group in allDefinitions.Where(static entry => entry.Property.SharedVariableId.HasValue)
                     .GroupBy(static entry => entry.Property.SharedVariableId!.Value))
        {
            var computed = group.Where(static entry => entry.Property.Computation is not null).ToList();
            if (computed.Count > 1)
                yield return Issue("Shared Variables", $"Shared property group '{group.Key:N}' has more than one computed writer.");
            if (computed.Count > 0 && group.Any(static entry => entry.Property.ValueRestriction != GamePropertyValueRestriction.TrueFalse))
                yield return Issue("Shared Variables", $"Shared property group '{group.Key:N}' has a computed member but not every member is TrueFalse.");
        }

        foreach (var owner in owners)
        {
            foreach (var action in owner.Actions.Where(static action => action.ActionType == CommandActionType.SetGameProperty))
            {
                var propertyName = ActionPayloadAccessors.GetSetPropertyName(action);
                var reference = ActionPayloadAccessors.GetSetGamePropertyReference(action);
                var targetOwners = reference?.OwnerId is Guid targetOwnerId
                    ? owners.Where(candidate => candidate.Id == targetOwnerId)
                    : !string.IsNullOrWhiteSpace(reference?.AnchorKey)
                        ? owners.Where(candidate => IsEligibleAnchorOwner(candidate, reference.AnchorKey!))
                        : new[] { owner };
                var targets = targetOwners
                    .SelectMany(targetOwner => targetOwner.Properties)
                    .Where(property => string.Equals(
                        property.Name,
                        reference?.PropertyName ?? propertyName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var computedSharedIds = allDefinitions
                    .Where(static entry => entry.Property.Computation is not null && entry.Property.SharedVariableId.HasValue)
                    .Select(static entry => entry.Property.SharedVariableId!.Value)
                    .ToHashSet();
                if (targets.Any(property => property.Computation is not null
                    || property.SharedVariableId is Guid sharedId && computedSharedIds.Contains(sharedId)))
                    yield return Issue($"{owner.Path}.Actions.{action.Name}", $"Action '{action.Name}' writes computed game property '{propertyName}'.");
            }
        }
    }

    private static bool IsEligibleAnchorOwner(OwnerInfo owner, string anchorKey) => anchorKey.ToLowerInvariant() switch
    {
        "currentroom" => owner.Scope == PropertyResolutionScope.Room,
        "currentarea" => owner.Scope == PropertyResolutionScope.Area,
        "currentcountry" => owner.Scope == PropertyResolutionScope.Country,
        "currentplanet" => owner.Scope == PropertyResolutionScope.Planet,
        "currentphase" => owner.Scope is PropertyResolutionScope.PhaseBook or PropertyResolutionScope.PhaseChapter or PropertyResolutionScope.PhasePage,
        "activeplayer" => owner.IsPlayer,
        _ => false
    };

    private static IEnumerable<string> ValidateCondition(
        ProjectGamePropertyConditionDto? condition,
        Guid computedOwnerId,
        PropertyResolutionScope computedScope,
        IReadOnlyList<(OwnerInfo Owner, GamePropertyDefinition Property)> definitions)
    {
        if (condition is null)
        {
            yield return "The computation has no condition tree.";
            yield break;
        }

        switch (condition.Kind)
        {
            case "All":
            case "Any":
                if (condition.Conditions is null || condition.Conditions.Count < 2)
                    yield return $"A {condition.Kind} group must contain at least two conditions.";
                foreach (var child in condition.Conditions ?? new())
                    foreach (var message in ValidateCondition(child, computedOwnerId, computedScope, definitions)) yield return message;
                break;
            case "Not":
                if (condition.Condition is null) yield return "A NOT condition must contain one child condition.";
                else foreach (var message in ValidateCondition(condition.Condition, computedOwnerId, computedScope, definitions)) yield return message;
                break;
            case "Exists":
                foreach (var message in ValidateSource(condition.GamePropertySource, computedOwnerId, computedScope, definitions, out _)) yield return message;
                break;
            case "Compare":
            {
                if (condition.Operator is not ("Equals" or "NotEquals" or "LessThan" or "LessThanOrEqual" or "GreaterThan" or "GreaterThanOrEqual" or "IsTrue" or "IsFalse"))
                    yield return $"Unsupported comparison operator '{condition.Operator}'.";
                var leftMessages = ValidateSource(condition.GamePropertySource, computedOwnerId, computedScope, definitions, out var left);
                foreach (var message in leftMessages) yield return message;
                if (condition.Operator is "IsTrue" or "IsFalse")
                {
                    if (condition.RightOperand is not null)
                        yield return "Direct Boolean tests must not have a right operand.";
                    if (left is not null && left.ValueRestriction != GamePropertyValueRestriction.TrueFalse)
                        yield return "Direct Boolean tests require a TrueFalse source property.";
                    yield break;
                }
                if (condition.RightOperand is null)
                {
                    yield return "A comparison is missing its right operand.";
                    yield break;
                }
                if (condition.Operator is "LessThan" or "LessThanOrEqual" or "GreaterThan" or "GreaterThanOrEqual")
                {
                    if (left is not null && left.ValueRestriction != GamePropertyValueRestriction.Numeric)
                        yield return "Ordered comparisons require Numeric source properties.";
                    if (condition.RightOperand.GamePropertySource is null && !string.Equals(condition.RightOperand.ValueKind, "Numeric", StringComparison.OrdinalIgnoreCase))
                        yield return "Ordered comparisons require a Numeric constant or source.";
                }
                if (condition.RightOperand.GamePropertySource is not null)
                {
                    GamePropertyDefinition? right = null;
                    foreach (var message in ValidateSource(condition.RightOperand.GamePropertySource, computedOwnerId, computedScope, definitions, out right)) yield return message;
                    if (left is not null && right is not null && left.ValueRestriction != right.ValueRestriction)
                        yield return "A source-to-source comparison cannot compare different value restrictions.";
                    if (condition.Operator is "LessThan" or "LessThanOrEqual" or "GreaterThan" or "GreaterThanOrEqual"
                        && right is not null && right.ValueRestriction != GamePropertyValueRestriction.Numeric)
                        yield return "Ordered comparisons require Numeric source properties.";
                }
                else
                {
                    var kind = condition.RightOperand.ValueKind;
                    var normalizedKind = kind?.ToLowerInvariant();
                    if (normalizedKind is not ("boolean" or "numeric" or "text"))
                    {
                        yield return "The comparison constant has an unsupported type.";
                        break;
                    }
                    var expected = kind?.ToLowerInvariant() switch
                    {
                        "boolean" => GamePropertyValueRestriction.TrueFalse,
                        "numeric" => GamePropertyValueRestriction.Numeric,
                        "text" => GamePropertyValueRestriction.Unrestricted,
                        _ => GamePropertyValueRestriction.Unrestricted
                    };
                    if (left is not null && left.ValueRestriction != expected)
                        yield return "The comparison constant type does not match the source property restriction.";
                    if (expected == GamePropertyValueRestriction.Numeric
                        && (!double.TryParse(condition.RightOperand.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) || !double.IsFinite(numeric)))
                        yield return "The Numeric comparison constant must be a finite invariant-culture number.";
                    if (expected == GamePropertyValueRestriction.TrueFalse && !bool.TryParse(condition.RightOperand.Value, out _))
                        yield return "The Boolean comparison constant must be true or false.";
                }
                break;
            }
            default:
                yield return $"Unsupported condition kind '{condition.Kind}'.";
                break;
        }
    }

    private static IReadOnlyList<string> ValidateSource(
        ProjectGamePropertySourceReferenceDto? source,
        Guid computedOwnerId,
        PropertyResolutionScope computedScope,
        IReadOnlyList<(OwnerInfo Owner, GamePropertyDefinition Property)> definitions,
        out GamePropertyDefinition? resolved)
    {
        var issues = new List<string>();
        resolved = null;
        if (source is null || string.IsNullOrWhiteSpace(source.PropertyName))
        {
            issues.Add("A source reference is missing its property name.");
            return issues;
        }
        if (source.OwnerId.HasValue && !string.IsNullOrWhiteSpace(source.AnchorKey))
        {
            issues.Add("A source reference cannot contain both an owner ID and an anchor key.");
            return issues;
        }

        IEnumerable<(OwnerInfo Owner, GamePropertyDefinition Property)> candidates;
        var isAnchorReference = !string.IsNullOrWhiteSpace(source.AnchorKey);
        if (!string.IsNullOrWhiteSpace(source.AnchorKey))
        {
            if (!EligibleAnchors.Contains(source.AnchorKey))
            {
                issues.Add($"Anchor '{source.AnchorKey}' is not eligible for persistent computed properties.");
                return issues;
            }
            var targetScope = source.AnchorKey.ToLowerInvariant() switch
            {
                "currentroom" => PropertyResolutionScope.Room,
                "currentarea" => PropertyResolutionScope.Area,
                "currentcountry" => PropertyResolutionScope.Country,
                "currentplanet" => PropertyResolutionScope.Planet,
                "currentphase" => PropertyResolutionScope.PhasePage,
                "activeplayer" => PropertyResolutionScope.Object,
                _ => computedScope
            };
            candidates = definitions.Where(entry => (entry.Owner.Scope == targetScope
                    || (string.Equals(source.AnchorKey, "currentPhase", StringComparison.OrdinalIgnoreCase)
                        && entry.Owner.Scope is PropertyResolutionScope.PhaseBook or PropertyResolutionScope.PhaseChapter))
                && (!string.Equals(source.AnchorKey, "activePlayer", StringComparison.OrdinalIgnoreCase) || entry.Owner.IsPlayer)
                && string.Equals(entry.Property.Name, source.PropertyName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var ownerId = source.OwnerId ?? computedOwnerId;
            candidates = definitions.Where(entry => entry.Owner.Id == ownerId
                && string.Equals(entry.Property.Name, source.PropertyName, StringComparison.OrdinalIgnoreCase));
        }

        var matches = candidates.ToList();
        if (matches.Count == 0)
            issues.Add($"Source '{source.PropertyName}' does not resolve on the selected owner or anchor.");
        else if (isAnchorReference)
        {
            var duplicateOwners = matches
                .GroupBy(static match => match.Owner.Id)
                .Where(static group => group.Count() > 1)
                .ToList();
            if (duplicateOwners.Count > 0)
            {
                var locations = string.Join(
                    "; ",
                    duplicateOwners.Select(group =>
                        $"{group.First().Owner.Path} ({group.Count()} matching definitions)"));
                issues.Add($"Source '{source.PropertyName}' is duplicated on eligible anchor owner(s): {locations}.");
            }
        }
        if (issues.Count == 0 && isAnchorReference && matches.Select(static match => match.Property.ValueRestriction).Distinct().Count() > 1)
            issues.Add($"Source '{source.PropertyName}' has inconsistent value restrictions across eligible anchor targets.");
        else if (issues.Count == 0 && matches.Count > 1)
        {
            if (!isAnchorReference)
                issues.Add($"Source '{source.PropertyName}' is ambiguous on the selected owner or anchor.");
            else
                resolved = matches[0].Property;
        }
        else if (issues.Count == 0)
            resolved = matches[0].Property;
        return issues;
    }

    private static IEnumerable<OwnerInfo> EnumerateOwners(ProjectModel project)
    {
        yield return new OwnerInfo(Guid.Empty, "Global", PropertyResolutionScope.Global, project.GlobalVariables, project.GlobalScope.AvailableActions);
        foreach (var obj in project.GlobalScope.GameObjects) foreach (var owner in EnumerateObject(obj, "Global")) yield return owner;
        foreach (var obj in project.BaseObjects) foreach (var owner in EnumerateObject(obj, "Base Objects")) yield return owner;
        foreach (var obj in project.ObjectTemplates) foreach (var owner in EnumerateObject(obj, "Object Templates")) yield return owner;
        foreach (var room in project.RoomTemplates)
        {
            yield return new OwnerInfo(room.Id, $"Room Template.{room.Name}", PropertyResolutionScope.Room, room.Variables, room.AvailableActions);
            foreach (var obj in room.GameObjects) foreach (var owner in EnumerateObject(obj, $"Room Template.{room.Name}")) yield return owner;
        }
        foreach (var planet in project.Planets)
        {
            yield return new OwnerInfo(planet.Id, $"Planet.{planet.Name}", PropertyResolutionScope.Planet, planet.Variables, planet.AvailableActions);
            foreach (var obj in planet.BaseObjects) foreach (var owner in EnumerateObject(obj, $"Planet.{planet.Name}.Base Objects")) yield return owner;
            foreach (var obj in planet.ObjectTemplates) foreach (var owner in EnumerateObject(obj, $"Planet.{planet.Name}.Object Templates")) yield return owner;
            foreach (var obj in planet.GameObjects) foreach (var owner in EnumerateObject(obj, $"Planet.{planet.Name}")) yield return owner;
            foreach (var country in planet.Countries)
            {
                yield return new OwnerInfo(country.Id, $"Planet.{planet.Name}.Country.{country.Name}", PropertyResolutionScope.Country, country.Variables, country.AvailableActions);
                foreach (var obj in country.BaseObjects) foreach (var owner in EnumerateObject(obj, $"Country.{country.Name}.Base Objects")) yield return owner;
                foreach (var obj in country.ObjectTemplates) foreach (var owner in EnumerateObject(obj, $"Country.{country.Name}.Object Templates")) yield return owner;
                foreach (var obj in country.GameObjects) foreach (var owner in EnumerateObject(obj, $"Planet.{planet.Name}.Country.{country.Name}")) yield return owner;
                foreach (var area in country.Areas)
                {
                    yield return new OwnerInfo(area.Id, $"Planet.{planet.Name}.Country.{country.Name}.Area.{area.Name}", PropertyResolutionScope.Area, area.Variables, area.AvailableActions);
                    foreach (var obj in area.BaseObjects) foreach (var owner in EnumerateObject(obj, $"Area.{area.Name}.Base Objects")) yield return owner;
                    foreach (var obj in area.ObjectTemplates) foreach (var owner in EnumerateObject(obj, $"Area.{area.Name}.Object Templates")) yield return owner;
                    foreach (var obj in area.GameObjects) foreach (var owner in EnumerateObject(obj, $"Area.{area.Name}")) yield return owner;
                    foreach (var room in area.Rooms)
                    {
                        yield return new OwnerInfo(room.Id, $"Area.{area.Name}.Room.{room.Name}", PropertyResolutionScope.Room, room.Variables, room.AvailableActions);
                        foreach (var obj in room.GameObjects) foreach (var owner in EnumerateObject(obj, $"Room.{room.Name}")) yield return owner;
                    }
                }
            }
        }
        foreach (var phase in project.PhaseBooks) foreach (var owner in EnumeratePhase(phase)) yield return owner;

        IEnumerable<OwnerInfo> EnumerateObject(GameObject obj, string path)
        {
            var isPlayer = (!string.IsNullOrWhiteSpace(project.PlayerCharacterObjectName)
                    && string.Equals(obj.Name, project.PlayerCharacterObjectName, StringComparison.OrdinalIgnoreCase))
                || obj.Variables.Any(property => string.Equals(property.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(property.DefaultValue, out var markedPlayer) && markedPlayer);
            yield return new OwnerInfo(obj.ObjectId, $"{path}.Object.{obj.Name}", PropertyResolutionScope.Object, obj.Variables, obj.AvailableActions, isPlayer);
            foreach (var child in obj.ContainedObjects) foreach (var nested in EnumerateObject(child, path + "." + obj.Name)) yield return nested;
        }

        IEnumerable<OwnerInfo> EnumeratePhase(PhaseNode phase)
        {
            var scope = phase.Tier switch { PhaseTier.Book => PropertyResolutionScope.PhaseBook, PhaseTier.Chapter => PropertyResolutionScope.PhaseChapter, _ => PropertyResolutionScope.PhasePage };
            yield return new OwnerInfo(phase.Id, $"Phase.{phase.PhaseKey}", scope, phase.Variables, phase.AvailableActions);
            foreach (var child in phase.Children) foreach (var nested in EnumeratePhase(child)) yield return nested;
        }
    }

    private static ValidationIssue Issue(string path, string description) =>
        new("CGP-001", ValidationSeverity.Error, path, description, "Review the computation or source reference, then explicitly override validation if you intend to save or export it.");

    private sealed record OwnerInfo(Guid Id, string Path, PropertyResolutionScope Scope, IReadOnlyList<GamePropertyDefinition> Properties, IReadOnlyList<CommandAction> Actions, bool IsPlayer = false);
}
