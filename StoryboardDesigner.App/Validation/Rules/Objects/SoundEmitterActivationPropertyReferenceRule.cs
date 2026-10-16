using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Objects;

public sealed class SoundEmitterActivationPropertyReferenceRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "OBJ-008",
        Title: "Sound Emitter Activation Property Reference",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Sound");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not GameObject gameObject)
        {
            yield break;
        }

        var ownerPath = string.Join(" / ", gameObject
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));

        for (var index = 0; index < gameObject.SoundEmitters.Count; index++)
        {
            var emitter = gameObject.SoundEmitters[index];
            var reference = emitter.ActivationGamePropertyReference;
            var propertyName = reference?.PropertyName?.Trim();
            if (reference is null || string.IsNullOrWhiteSpace(propertyName))
            {
                propertyName = emitter.ActivationGamePropertyName?.Trim();
                if (string.IsNullOrWhiteSpace(propertyName)) continue;
                reference = new ProjectGamePropertySourceReferenceDto { PropertyName = propertyName };
            }

            var emitterPath = $"{ownerPath} / soundEmitter[{index + 1}]";
            if (reference.OwnerId.HasValue && !string.IsNullOrWhiteSpace(reference.AnchorKey))
            {
                yield return BuildIssue(emitterPath, propertyName,
                    "uses both a fixed owner and an anchor.",
                    "Choose one owner mode for the activation property.");
                continue;
            }

            var owners = ResolveOwners(context.Project, gameObject, reference).ToList();
            if (owners.Count == 0)
            {
                yield return BuildIssue(emitterPath, propertyName,
                    $"does not resolve to an available owner ({reference.AnchorKey ?? reference.OwnerId?.ToString("D") ?? "self"}).",
                    "Choose a property on the emitter's object, an available fixed owner, or an eligible anchor.");
                continue;
            }

            var invalidOwners = owners
                .Select(owner => (Owner: owner, Matches: GetProperties(owner)
                    .Where(property => string.Equals(property.Name?.Trim(), propertyName, StringComparison.OrdinalIgnoreCase))
                    .ToList()))
                .Where(static item => item.Matches.Count > 1
                    || (item.Matches.Count == 1 && !IsBooleanProperty(item.Matches[0])))
                .ToList();
            if (invalidOwners.Count > 0)
            {
                var ownerNames = string.Join(", ", invalidOwners.Select(static item => item.Owner.ScopeName));
                yield return BuildIssue(emitterPath, propertyName,
                    $"is ambiguous or non-boolean on the selected owner(s): {ownerNames}.",
                    "Remove duplicate definitions or make the selected property Boolean.");
            }
        }
    }

    private static ValidationIssue BuildIssue(string path, string propertyName, string detail, string suggestion) => new(
        "OBJ-008",
        ValidationSeverity.Error,
        path,
        $"activation game property '{propertyName}' {detail}",
        suggestion);

    private static IEnumerable<ScopeNodeBase> ResolveOwners(
        ProjectModel project,
        GameObject currentObject,
        ProjectGamePropertySourceReferenceDto reference)
    {
        var allOwners = EnumerateOwners(project).ToList();
        if (reference.OwnerId is Guid ownerId)
        {
            return allOwners.Where(owner => GetOwnerId(owner) == ownerId).Take(1);
        }

        if (string.IsNullOrWhiteSpace(reference.AnchorKey))
        {
            return new ScopeNodeBase[] { currentObject };
        }

        var localAncestors = currentObject.EnumerateSelfAndAncestors().OfType<ScopeNodeBase>();
        return reference.AnchorKey switch
        {
            "currentRoom" => NearestOrAll(localAncestors.OfType<Room>(), allOwners.OfType<Room>()),
            "currentArea" => NearestOrAll(localAncestors.OfType<Area>(), allOwners.OfType<Area>()),
            "currentCountry" => NearestOrAll(localAncestors.OfType<Country>(), allOwners.OfType<Country>()),
            "currentPlanet" => NearestOrAll(localAncestors.OfType<Planet>(), allOwners.OfType<Planet>()),
            "currentPhase" => allOwners.OfType<PhaseNode>(),
            "activePlayer" => ResolveActivePlayer(project, allOwners),
            _ => Array.Empty<ScopeNodeBase>()
        };
    }

    private static IEnumerable<TScope> NearestOrAll<TScope>(IEnumerable<TScope> local, IEnumerable<TScope> all)
        where TScope : ScopeNodeBase
    {
        var nearest = local.FirstOrDefault();
        return nearest is null ? all : new[] { nearest };
    }

    private static IEnumerable<GameObject> ResolveActivePlayer(ProjectModel project, IEnumerable<ScopeNodeBase> owners)
    {
        var playerName = project.PlayerCharacterObjectName?.Trim() ?? string.Empty;
        return owners.OfType<GameObject>().Where(gameObject =>
            (!string.IsNullOrWhiteSpace(playerName)
                && string.Equals(gameObject.Name, playerName, StringComparison.OrdinalIgnoreCase))
            || gameObject.Variables.Any(property => string.Equals(property.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(property.DefaultValue, out var isPlayer) && isPlayer));
    }

    private static IEnumerable<ScopeNodeBase> EnumerateOwners(ProjectModel project)
    {
        var pending = new Stack<IScopedAwareNode>();
        pending.Push(project);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is ScopeNodeBase owner) yield return owner;
            foreach (var child in current.ChildScopes.Reverse()) pending.Push(child);
        }
    }

    private static Guid GetOwnerId(ScopeNodeBase owner) => owner switch
    {
        GameObject gameObject => gameObject.ObjectId,
        Room room => room.Id,
        Planet planet => planet.Id,
        Country country => country.Id,
        Area area => area.Id,
        PhaseNode phase => phase.Id,
        ProjectModel => Guid.Empty,
        _ => Guid.Empty
    };

    private static IEnumerable<GamePropertyDefinition> GetProperties(ScopeNodeBase owner) => owner switch
    {
        GameObject gameObject => gameObject.Variables,
        Room room => room.Variables,
        Planet planet => planet.Variables,
        Country country => country.Variables,
        Area area => area.Variables,
        PhaseNode phase => phase.Variables,
        ProjectModel project => project.GlobalVariables,
        _ => Array.Empty<GamePropertyDefinition>()
    };

    private static bool IsBooleanProperty(GamePropertyDefinition property) =>
        property.ValueRestriction == GamePropertyValueRestriction.TrueFalse
        || bool.TryParse(property.DefaultValue, out _);
}
