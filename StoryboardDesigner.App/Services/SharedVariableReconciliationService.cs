using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

internal static class SharedVariableReconciliationService
{
    public static void ReconcileInPlace(ProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var metadataById = project.SharedVariables
            .Where(static shared => shared.Id != Guid.Empty)
            .GroupBy(shared => shared.Id)
            .ToDictionary(group => group.Key, group => CloneMetadata(group.First()));

        var participantsBySharedId = new Dictionary<Guid, List<SharedVariableParticipant>>();
        var seedVariableBySharedId = new Dictionary<Guid, GamePropertyDefinition>();

        AddLinkedGlobalParticipants(project.GlobalVariables, participantsBySharedId, seedVariableBySharedId);
        AddLinkedGlobalParticipants(project.GlobalScope.GameProperties, participantsBySharedId, seedVariableBySharedId);
        AddLinkedObjectParticipants(project.GlobalScope.GameObjects, participantsBySharedId, seedVariableBySharedId);
        AddLinkedObjectParticipants(project.ObjectTemplates, participantsBySharedId, seedVariableBySharedId);
        AddLinkedObjectParticipants(project.BaseObjects, participantsBySharedId, seedVariableBySharedId);

        foreach (var roomTemplate in project.RoomTemplates)
        {
            AddLinkedGlobalParticipants(roomTemplate.Variables, participantsBySharedId, seedVariableBySharedId);
            AddLinkedObjectParticipants(roomTemplate.GameObjects, participantsBySharedId, seedVariableBySharedId);
        }

        foreach (var planet in project.Planets)
        {
            AddLinkedGlobalParticipants(planet.Variables, participantsBySharedId, seedVariableBySharedId);
            AddLinkedObjectParticipants(planet.BaseObjects, participantsBySharedId, seedVariableBySharedId);

            foreach (var country in planet.Countries)
            {
                AddLinkedGlobalParticipants(country.Variables, participantsBySharedId, seedVariableBySharedId);
                AddLinkedObjectParticipants(country.BaseObjects, participantsBySharedId, seedVariableBySharedId);

                foreach (var area in country.Areas)
                {
                    AddLinkedGlobalParticipants(area.Variables, participantsBySharedId, seedVariableBySharedId);
                    AddLinkedObjectParticipants(area.BaseObjects, participantsBySharedId, seedVariableBySharedId);

                    foreach (var connection in area.TraversalConnections)
                    {
                        AddLinkedTraversalParticipants(connection, participantsBySharedId, seedVariableBySharedId);
                    }

                    foreach (var room in area.Rooms)
                    {
                        AddLinkedGlobalParticipants(room.Variables, participantsBySharedId, seedVariableBySharedId);
                        AddLinkedObjectParticipants(room.GameObjects, participantsBySharedId, seedVariableBySharedId);
                    }
                }
            }
        }

        foreach (var sharedId in participantsBySharedId.Keys)
        {
            if (metadataById.ContainsKey(sharedId))
            {
                continue;
            }

            seedVariableBySharedId.TryGetValue(sharedId, out var seedVariable);
            metadataById[sharedId] = new SharedVariableDefinition
            {
                Id = sharedId,
                Name = BuildFallbackName(sharedId),
                DefaultValue = seedVariable?.DefaultValue ?? string.Empty,
                ValueRestriction = seedVariable?.ValueRestriction ?? GamePropertyValueRestriction.Unrestricted
            };
        }

        var reconciled = new List<SharedVariableDefinition>();
        foreach (var shared in metadataById.Values.OrderBy(static shared => shared.Id))
        {
            participantsBySharedId.TryGetValue(shared.Id, out var participants);
            shared.Participants = participants is null
                ? new List<SharedVariableParticipant>()
                : participants
                    .Distinct(SharedVariableParticipantComparer.Instance)
                    .OrderBy(static participant => participant.Kind, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static participant => participant.OwnerId)
                    .ThenBy(static participant => participant.VariableName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static participant => participant.Leg ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            reconciled.Add(shared);
        }

        project.SharedVariables = reconciled;
    }

    private static void AddLinkedGlobalParticipants(
        IEnumerable<GamePropertyDefinition> variables,
        Dictionary<Guid, List<SharedVariableParticipant>> participantsBySharedId,
        Dictionary<Guid, GamePropertyDefinition> seedVariableBySharedId)
    {
        foreach (var variable in variables)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            AddParticipant(
                sharedId,
                variable,
                new SharedVariableParticipant
                {
                    Kind = "global",
                    OwnerId = variable.Id,
                    VariableName = variable.Name,
                    Leg = null
                },
                participantsBySharedId,
                seedVariableBySharedId);
        }
    }

    private static void AddLinkedObjectParticipants(
        IEnumerable<GameObject> objects,
        Dictionary<Guid, List<SharedVariableParticipant>> participantsBySharedId,
        Dictionary<Guid, GamePropertyDefinition> seedVariableBySharedId)
    {
        foreach (var gameObject in objects)
        {
            foreach (var variable in gameObject.Variables)
            {
                if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
                {
                    continue;
                }

                AddParticipant(
                    sharedId,
                    variable,
                    new SharedVariableParticipant
                    {
                        Kind = "object",
                        OwnerId = gameObject.ObjectId,
                        VariableName = variable.Name,
                        Leg = null
                    },
                    participantsBySharedId,
                    seedVariableBySharedId);
            }

            AddLinkedObjectParticipants(gameObject.ContainedObjects, participantsBySharedId, seedVariableBySharedId);
        }
    }

    private static void AddLinkedTraversalParticipants(
        TraversalConnection connection,
        Dictionary<Guid, List<SharedVariableParticipant>> participantsBySharedId,
        Dictionary<Guid, GamePropertyDefinition> seedVariableBySharedId)
    {
        foreach (var variable in connection.Variables)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            AddParticipant(
                sharedId,
                variable,
                new SharedVariableParticipant
                {
                    Kind = "traversal",
                    OwnerId = connection.TraversalConnectionId,
                    VariableName = variable.Name,
                    Leg = null
                },
                participantsBySharedId,
                seedVariableBySharedId);
        }

        AddLinkedTraversalLegParticipants(connection.TraversalStateFromA, connection.TraversalConnectionId, "a2b", participantsBySharedId, seedVariableBySharedId);
        AddLinkedTraversalLegParticipants(connection.TraversalStateFromB, connection.TraversalConnectionId, "b2a", participantsBySharedId, seedVariableBySharedId);
    }

    private static void AddLinkedTraversalLegParticipants(
        TraversalLegState legState,
        Guid connectionId,
        string legToken,
        Dictionary<Guid, List<SharedVariableParticipant>> participantsBySharedId,
        Dictionary<Guid, GamePropertyDefinition> seedVariableBySharedId)
    {
        foreach (var variable in legState.Variables)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            AddParticipant(
                sharedId,
                variable,
                new SharedVariableParticipant
                {
                    Kind = "traversal-leg",
                    OwnerId = connectionId,
                    VariableName = variable.Name,
                    Leg = legToken
                },
                participantsBySharedId,
                seedVariableBySharedId);
        }
    }

    private static void AddParticipant(
        Guid sharedId,
        GamePropertyDefinition variable,
        SharedVariableParticipant participant,
        Dictionary<Guid, List<SharedVariableParticipant>> participantsBySharedId,
        Dictionary<Guid, GamePropertyDefinition> seedVariableBySharedId)
    {
        if (!participantsBySharedId.TryGetValue(sharedId, out var participants))
        {
            participants = new List<SharedVariableParticipant>();
            participantsBySharedId[sharedId] = participants;
        }

        participants.Add(participant);
        if (!seedVariableBySharedId.ContainsKey(sharedId))
        {
            seedVariableBySharedId[sharedId] = variable;
        }
    }

    private static SharedVariableDefinition CloneMetadata(SharedVariableDefinition shared)
    {
        return new SharedVariableDefinition
        {
            Id = shared.Id,
            Name = shared.Name,
            DefaultValue = shared.DefaultValue,
            ValueRestriction = shared.ValueRestriction,
            Participants = new List<SharedVariableParticipant>()
        };
    }

    private static string BuildFallbackName(Guid sharedId)
    {
        return $"Shared-{sharedId.ToString("N")[..8]}";
    }
}
