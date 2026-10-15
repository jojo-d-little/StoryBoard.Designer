using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class RoomSpawnPointTraversalIntegrityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = new HashSet<ScopeNodeKind> { ScopeNodeKind.Area };
    public ValidationRuleMetadata Metadata { get; } = new(
        "TRV-SPAWN-001", "Traversal Spawn Point Integrity", ValidationSeverity.Error, "Traversal");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        if (context.CandidateNode is not Area area) yield break;
        var areaPath = BuildAreaPath(context.Project, area);
        var connections = area.TraversalConnections.GroupBy(static connection => connection.TraversalConnectionId)
            .ToDictionary(static group => group.Key, static group => group.First());

        foreach (var room in area.Rooms)
        {
            var roomPath = $"{areaPath} / Room/{room.Name}[{room.Id:N}]";
            var defaults = room.SpawnPoints.Where(static point => point.IsDefault).ToList();
            if (defaults.Count > 1)
                yield return Issue(roomPath, "room has more than one default spawn point.", "Keep at most one room spawn point marked as the default.");

            var referenced = new HashSet<Guid>();
            foreach (var point in room.SpawnPoints.Where(static point => point.TraversalConnectionId.HasValue))
            {
                var id = point.TraversalConnectionId!.Value;
                var pointPath = $"{roomPath} / SpawnPoint[TraversalConnectionId={id:N}]";
                if (!connections.TryGetValue(id, out var connection))
                {
                    yield return Issue(pointPath, $"spawn point references unknown traversal '{id:D}'.", "Select a traversal connection in this Area or remove the traversal association.");
                    continue;
                }

                if (!referenced.Add(id))
                    yield return Issue(pointPath, $"room has duplicate spawn-point references to traversal '{id:D}'.", "Keep one spawn point for this incoming traversal.");

                if (connection.RoomAId != room.Id && connection.RoomBId != room.Id)
                    yield return Issue(pointPath, $"traversal '{id:D}' connects different rooms and is not connected to '{room.Name}'.", "Associate the point with a traversal that has this room as its destination.");
                else if (!RoomSpawnPointAuthoringService.PermitsEntry(connection, room.Id))
                    yield return Issue(pointPath, $"traversal '{id:D}' does not permit entry into room '{room.Name}' in its configured one-way direction.", "Choose an incoming traversal or change the traversal access direction.");
            }

            foreach (var connection in area.TraversalConnections.Where(connection => RoomSpawnPointAuthoringService.PermitsEntry(connection, room.Id)))
            {
                if (!referenced.Contains(connection.TraversalConnectionId))
                {
                    var pointPath = $"{roomPath} / TraversalConnection[{connection.TraversalConnectionId:N}]";
                    yield return Issue(pointPath, $"room is missing a spawn point for incoming traversal '{connection.TraversalConnectionId:D}'.", "Add a spawn point associated with this traversal connection.");
                }
            }
        }
    }

    private ValidationIssue Issue(string path, string message, string resolution) =>
        new(Metadata.RuleId, Metadata.DefaultSeverity, path, message, resolution);

    private static string BuildAreaPath(ProjectModel project, Area area)
    {
        foreach (var planet in project.Planets)
        foreach (var country in planet.Countries)
            if (country.Areas.Any(candidate => candidate.Id == area.Id))
                return $"Global / {planet.Name} / {country.Name} / {area.Name}";
        return $"Global / Area/{area.Name}[{area.Id:N}]";
    }
}
