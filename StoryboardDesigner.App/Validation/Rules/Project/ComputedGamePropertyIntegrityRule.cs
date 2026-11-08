using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ComputedGamePropertyIntegrityRule : IValidationRule
{
    private static readonly HashSet<string> EligibleAnchors = new(StringComparer.OrdinalIgnoreCase) { "currentRoom", "currentArea", "currentCountry", "currentPlanet", "currentPhase", "activePlayer" };
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };
    public ValidationRuleMetadata Metadata { get; } = new("CGP-001", "Computed Game Property Integrity", ValidationSeverity.Error, "Computed Game Properties");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not ProjectModel project) yield break;
        var owners = EnumerateOwners(project).ToList();
        var definitions = owners.SelectMany(owner => owner.Properties.Select(property => (Owner: owner, Property: property))).ToList();
        foreach (var (owner, property) in definitions.Where(static item => item.Property.Computation is not null))
        {
            var path = $"{owner.Path}.Game Properties.{property.Name}";
            var computation = property.Computation!;
            var outputType = computation.OutputType ?? string.Empty;
            var restriction = ComputedRestriction(outputType);
            if (restriction is null) { yield return Issue(path, $"Unsupported computed output type '{outputType}'."); continue; }
            if (property.ValueRestriction != restriction) yield return Issue(path, $"Computed {outputType} properties must use the {restriction} value restriction.");
            var expectedDefault = outputType switch { "Boolean" => "false", "Numeric" => "0", _ => string.Empty };
            if (!string.Equals(property.DefaultValue, expectedDefault, StringComparison.OrdinalIgnoreCase))
                yield return Issue(path, $"A computed {outputType} property must use '{expectedDefault}' as its compatibility default.");
            foreach (var message in ValidateExpression(computation.Expression, owner.Id, owner.Scope, definitions)) yield return Issue(path, message);
            foreach (var warning in GamePropertyArithmeticAnalysis.DivisionByZeroWarnings(computation.Expression))
                yield return Issue(path, warning, ValidationSeverity.Warning);
        }
        var sharedParticipants = owners.SelectMany(owner =>
            (owner.SharedGroupProperties ?? owner.Properties).Select(property => (Owner: owner, Property: property)));
        foreach (var group in sharedParticipants.Where(static item => item.Property.SharedVariableId.HasValue).GroupBy(static item => item.Property.SharedVariableId!.Value))
        {
            var computed = group.Where(static item => item.Property.Computation is not null).ToList();
            if (computed.Count > 1) yield return Issue("Shared Variables", $"Shared property group '{group.Key:N}' has more than one computed writer.");
            if (computed.Count == 0) continue;
            var expected = ComputedRestriction(computed[0].Property.Computation?.OutputType);
            if (computed.Any(item => ComputedRestriction(item.Property.Computation?.OutputType) != expected))
                yield return Issue("Shared Variables", $"Shared property group '{group.Key:N}' has computed members with different output types.");
            if (expected is not null && group.Any(item => item.Property.ValueRestriction != expected))
                yield return Issue("Shared Variables", $"Shared property group '{group.Key:N}' does not use a consistent value restriction for its computed output type.");
        }
        foreach (var owner in owners)
        foreach (var action in owner.Actions.Where(static action => action.ActionType == CommandActionType.SetGameProperty))
        {
            var propertyName = ActionPayloadAccessors.GetSetPropertyName(action);
            var reference = ActionPayloadAccessors.GetSetGamePropertyReference(action);
            var targetOwners = reference?.OwnerId is Guid targetOwnerId ? owners.Where(candidate => candidate.Id == targetOwnerId)
                : !string.IsNullOrWhiteSpace(reference?.AnchorKey) ? owners.Where(candidate => IsEligibleAnchorOwner(candidate, reference.AnchorKey!)) : new[] { owner };
            var targets = targetOwners.SelectMany(targetOwner => targetOwner.Properties)
                .Where(property => string.Equals(property.Name, reference?.PropertyName ?? propertyName, StringComparison.OrdinalIgnoreCase)).ToList();
            var computedIds = definitions.Where(static item => item.Property.Computation is not null && item.Property.SharedVariableId.HasValue)
                .Select(static item => item.Property.SharedVariableId!.Value).ToHashSet();
            if (targets.Any(property => property.Computation is not null || property.SharedVariableId is Guid id && computedIds.Contains(id)))
                yield return Issue($"{owner.Path}.Actions.{action.Name}", $"Action '{action.Name}' writes computed game property '{propertyName}'.");
        }
    }

    private static GamePropertyValueRestriction? ComputedRestriction(string? type) => type switch
    {
        "Boolean" => GamePropertyValueRestriction.TrueFalse, "Numeric" => GamePropertyValueRestriction.Numeric,
        "String" => GamePropertyValueRestriction.Unrestricted, _ => null
    };

    private static IEnumerable<string> ValidateExpression(ProjectGamePropertyExpressionDto? expression, Guid ownerId, PropertyResolutionScope scope,
        IReadOnlyList<(OwnerInfo Owner, GamePropertyDefinition Property)> definitions)
    {
        if (expression is null) { yield return "The computation has no expression tree."; yield break; }
        switch (expression.Kind)
        {
            case "literal":
                if (expression.Value is null) yield return "A literal expression is missing its value.";
                else if (expression.Value is double number && !double.IsFinite(number)) yield return "A numeric literal must be finite.";
                else if (expression.Value is JsonElement element && element.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.String or JsonValueKind.Number))
                    yield return "A literal must be a Boolean, finite number, or String value.";
                else if (expression.Value is JsonElement numericElement && numericElement.ValueKind == JsonValueKind.Number
                    && (!numericElement.TryGetDouble(out var parsedNumber) || !double.IsFinite(parsedNumber)))
                    yield return "A numeric literal must be finite.";
                break;
            case "property": case "exists":
                foreach (var message in ValidateSource(expression.Source, ownerId, scope, definitions, out _)) yield return message;
                break;
            case "compare":
                if (expression.Operator is not ("Equals" or "NotEquals" or "LessThan" or "LessThanOrEqual" or "GreaterThan" or "GreaterThanOrEqual")) yield return $"Unsupported comparison operator '{expression.Operator}'.";
                foreach (var message in ValidateExpression(expression.Left, ownerId, scope, definitions)) yield return message;
                foreach (var message in ValidateExpression(expression.Right, ownerId, scope, definitions)) yield return message;
                break;
            case "and": case "or":
                if (expression.Operands is null || expression.Operands.Count < 2) yield return $"An {expression.Kind.ToUpperInvariant()} expression must contain at least two operands.";
                foreach (var child in expression.Operands ?? []) foreach (var message in ValidateExpression(child, ownerId, scope, definitions)) yield return message;
                break;
            case "not":
                foreach (var message in ValidateExpression(expression.Expression, ownerId, scope, definitions)) yield return message;
                break;
            case "select":
                if (expression.Branches is null || expression.Branches.Count == 0) yield return "A select expression requires at least one ordered branch.";
                foreach (var branch in expression.Branches ?? [])
                {
                    foreach (var message in ValidateExpression(branch.When, ownerId, scope, definitions)) yield return message;
                    foreach (var message in ValidateExpression(branch.Value, ownerId, scope, definitions)) yield return message;
                }
                foreach (var message in ValidateExpression(expression.Else, ownerId, scope, definitions)) yield return message;
                break;
            case "add": case "subtract": case "multiply": case "divide":
                if (expression.Left is null || expression.Right is null)
                    yield return $"An arithmetic '{expression.Kind}' expression requires both operands.";
                else if (GamePropertyArithmeticAnalysis.HasNonFiniteConstantArithmeticResult(expression))
                    yield return "A constant arithmetic result must be finite; it recovers to Numeric 0 at runtime otherwise.";
                foreach (var message in GamePropertyArithmeticAnalysis.ValidateNumericConversion(expression.Left)) yield return message;
                foreach (var message in GamePropertyArithmeticAnalysis.ValidateNumericConversion(expression.Right)) yield return message;
                if (expression.Left is not null)
                    foreach (var message in ValidateExpression(expression.Left, ownerId, scope, definitions)) yield return message;
                if (expression.Right is not null)
                    foreach (var message in ValidateExpression(expression.Right, ownerId, scope, definitions)) yield return message;
                break;
            default: yield return $"Unsupported expression kind '{expression.Kind}'."; break;
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
            var effectiveRows = EffectiveGamePropertyResolver.Resolve(obj, id => EffectiveGamePropertyResolver.ResolveBaseObject(project, id));
            var effectiveProperties = effectiveRows.Select(static property => property.Definition).ToList();
            var baseDefinition = EffectiveGamePropertyResolver.ResolveBaseObject(project, obj.LinkedBaseObjectId ?? Guid.Empty);
            var sharedGroupProperties = effectiveRows.Where(effective =>
            {
                if (!effective.IsBaseOwned)
                {
                    return true;
                }
                if (!effective.HasValueOverride || effective.BaseDefinitionId is not Guid baseDefinitionId)
                {
                    return false;
                }

                var baseProperty = baseDefinition?.Variables.FirstOrDefault(candidate => candidate.Id == baseDefinitionId);
                return baseProperty is not null && effective.Definition.SharedVariableId != baseProperty.SharedVariableId;
            }).Select(static property => property.Definition).ToList();
            yield return new OwnerInfo(obj.ObjectId, $"{path}.Object.{obj.Name}", PropertyResolutionScope.Object, effectiveProperties, obj.AvailableActions, isPlayer, sharedGroupProperties);
            foreach (var child in obj.ContainedObjects) foreach (var nested in EnumerateObject(child, path + "." + obj.Name)) yield return nested;
        }

        IEnumerable<OwnerInfo> EnumeratePhase(PhaseNode phase)
        {
            var scope = phase.Tier switch { PhaseTier.Book => PropertyResolutionScope.PhaseBook, PhaseTier.Chapter => PropertyResolutionScope.PhaseChapter, _ => PropertyResolutionScope.PhasePage };
            yield return new OwnerInfo(phase.Id, $"Phase.{phase.PhaseKey}", scope, phase.Variables, phase.AvailableActions);
            foreach (var child in phase.Children) foreach (var nested in EnumeratePhase(child)) yield return nested;
        }
    }

    private static ValidationIssue Issue(string path, string description, ValidationSeverity severity = ValidationSeverity.Error) =>
        new("CGP-001", severity, path, description, "Review the computation or source reference, then explicitly override validation if you intend to save or export it.");

    private sealed record OwnerInfo(Guid Id, string Path, PropertyResolutionScope Scope, IReadOnlyList<GamePropertyDefinition> Properties, IReadOnlyList<CommandAction> Actions, bool IsPlayer = false, IReadOnlyList<GamePropertyDefinition>? SharedGroupProperties = null);
}
