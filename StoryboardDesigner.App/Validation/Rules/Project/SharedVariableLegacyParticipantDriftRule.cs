using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class SharedVariableLegacyParticipantDriftRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-007",
        Title: "Shared Variable Legacy Participant Drift",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var derivedBySharedId = BuildDerivedParticipantsBySharedId(context.Project);

        foreach (var shared in context.Project.SharedVariables.Where(static shared => shared.Id != Guid.Empty))
        {
            var persisted = shared.Participants
                .Select(ToParticipantKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (persisted.Count == 0)
            {
                continue;
            }

            var derived = derivedBySharedId.TryGetValue(shared.Id, out var participants)
                ? participants.Select(ToParticipantKey).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (persisted.SetEquals(derived))
            {
                continue;
            }

            var staleCount = persisted.Except(derived, StringComparer.OrdinalIgnoreCase).Count();
            var missingCount = derived.Except(persisted, StringComparer.OrdinalIgnoreCase).Count();
            var sharedLabel = string.IsNullOrWhiteSpace(shared.Name)
                ? shared.Id.ToString("N")
                : $"{shared.Name} ({shared.Id:N})";

            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                "Project",
                $"Shared variable '{sharedLabel}' has legacy participant metadata drift (stale={staleCount}, missing={missingCount}).",
                "Reconcile shared metadata from linked variable sharedVariableId values.");
        }
    }

    private static Dictionary<Guid, List<SharedVariableParticipant>> BuildDerivedParticipantsBySharedId(ProjectModel project)
    {
        var map = new Dictionary<Guid, List<SharedVariableParticipant>>();

        AddGlobalParticipants(project.GlobalVariables, map);
        AddGlobalParticipants(project.GlobalScope.GameProperties, map);
        AddObjectParticipants(project.GlobalScope.GameObjects, map);
        AddObjectParticipants(project.ObjectTemplates, map);
        AddObjectParticipants(project.BaseObjects, map);

        foreach (var roomTemplate in project.RoomTemplates)
        {
            AddGlobalParticipants(roomTemplate.Variables, map);
            AddObjectParticipants(roomTemplate.GameObjects, map);
        }

        foreach (var planet in project.Planets)
        {
            AddGlobalParticipants(planet.Variables, map);
            AddObjectParticipants(planet.BaseObjects, map);
            AddObjectParticipants(planet.GameObjects, map);

            foreach (var country in planet.Countries)
            {
                AddGlobalParticipants(country.Variables, map);
                AddObjectParticipants(country.BaseObjects, map);
                AddObjectParticipants(country.GameObjects, map);

                foreach (var area in country.Areas)
                {
                    AddGlobalParticipants(area.Variables, map);
                    AddObjectParticipants(area.BaseObjects, map);
                    AddObjectParticipants(area.GameObjects, map);

                    foreach (var connection in area.TraversalConnections)
                    {
                        AddTraversalParticipants(connection, map);
                    }

                    foreach (var room in area.Rooms)
                    {
                        AddGlobalParticipants(room.Variables, map);
                        AddObjectParticipants(room.GameObjects, map);
                    }
                }
            }
        }

        return map;
    }

    private static void AddGlobalParticipants(
        IEnumerable<GamePropertyDefinition> variables,
        Dictionary<Guid, List<SharedVariableParticipant>> map)
    {
        foreach (var variable in variables)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            Add(sharedId, new SharedVariableParticipant
            {
                Kind = "global",
                OwnerId = variable.Id,
                VariableName = variable.Name,
                Leg = null
            }, map);
        }
    }

    private static void AddObjectParticipants(
        IEnumerable<GameObject> objects,
        Dictionary<Guid, List<SharedVariableParticipant>> map)
    {
        foreach (var gameObject in objects)
        {
            foreach (var variable in gameObject.Variables)
            {
                if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
                {
                    continue;
                }

                Add(sharedId, new SharedVariableParticipant
                {
                    Kind = "object",
                    OwnerId = gameObject.ObjectId,
                    VariableName = variable.Name,
                    Leg = null
                }, map);
            }

            AddObjectParticipants(gameObject.ContainedObjects, map);
        }
    }

    private static void AddTraversalParticipants(
        TraversalConnection connection,
        Dictionary<Guid, List<SharedVariableParticipant>> map)
    {
        foreach (var variable in connection.Variables)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            Add(sharedId, new SharedVariableParticipant
            {
                Kind = "traversal",
                OwnerId = connection.TraversalConnectionId,
                VariableName = variable.Name,
                Leg = null
            }, map);
        }

        AddTraversalLegParticipants(connection.TraversalStateFromA, connection.TraversalConnectionId, "a2b", map);
        AddTraversalLegParticipants(connection.TraversalStateFromB, connection.TraversalConnectionId, "b2a", map);
    }

    private static void AddTraversalLegParticipants(
        TraversalLegState legState,
        Guid connectionId,
        string leg,
        Dictionary<Guid, List<SharedVariableParticipant>> map)
    {
        foreach (var variable in legState.Variables)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            Add(sharedId, new SharedVariableParticipant
            {
                Kind = "traversal-leg",
                OwnerId = connectionId,
                VariableName = variable.Name,
                Leg = leg
            }, map);
        }
    }

    private static void Add(
        Guid sharedId,
        SharedVariableParticipant participant,
        Dictionary<Guid, List<SharedVariableParticipant>> map)
    {
        if (!map.TryGetValue(sharedId, out var list))
        {
            list = new List<SharedVariableParticipant>();
            map[sharedId] = list;
        }

        list.Add(participant);
    }

    private static string ToParticipantKey(SharedVariableParticipant participant)
    {
        return string.Join("|", new[]
        {
            participant.Kind?.Trim() ?? string.Empty,
            participant.OwnerId.ToString("N"),
            participant.VariableName?.Trim() ?? string.Empty,
            participant.Leg?.Trim() ?? string.Empty
        });
    }
}
