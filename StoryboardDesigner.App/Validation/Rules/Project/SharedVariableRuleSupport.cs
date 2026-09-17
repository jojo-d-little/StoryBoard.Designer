using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Rules.Project;

internal static class SharedVariableRuleSupport
{
    internal static Dictionary<Guid, GamePropertyDefinition> BuildVariableLookup(ProjectModel project)
    {
        var map = new Dictionary<Guid, GamePropertyDefinition>();

        static void AddRange(Dictionary<Guid, GamePropertyDefinition> target, IEnumerable<GamePropertyDefinition> variables)
        {
            foreach (var variable in variables.Where(static variable => variable.Id != Guid.Empty))
            {
                target[variable.Id] = variable;
            }
        }

        static void AddObjectRanges(Dictionary<Guid, GamePropertyDefinition> target, IEnumerable<GameObject> objects)
        {
            foreach (var gameObject in objects)
            {
                AddRange(target, gameObject.Variables);
                AddObjectRanges(target, gameObject.ContainedObjects);
            }
        }

        AddRange(map, project.GlobalVariables);
        AddRange(map, project.GlobalScope.GameProperties);
        AddObjectRanges(map, project.GlobalScope.GameObjects);
        AddObjectRanges(map, project.ObjectTemplates);

        foreach (var planet in project.Planets)
        {
            AddRange(map, planet.Variables);
            AddObjectRanges(map, planet.GameObjects);

            foreach (var country in planet.Countries)
            {
                AddRange(map, country.Variables);
                AddObjectRanges(map, country.GameObjects);

                foreach (var area in country.Areas)
                {
                    AddRange(map, area.Variables);
                    AddObjectRanges(map, area.GameObjects);
                    AddRange(map, area.TraversalConnections.SelectMany(static connection => connection.Variables));

                    foreach (var connection in area.TraversalConnections)
                    {
                        AddRange(map, connection.TraversalStateFromA.Variables);
                        AddRange(map, connection.TraversalStateFromB.Variables);
                    }

                    foreach (var room in area.Rooms)
                    {
                        AddRange(map, room.Variables);
                        AddObjectRanges(map, room.GameObjects);
                    }
                }
            }
        }

        return map;
    }

    internal static Dictionary<Guid, List<GamePropertyDefinition>> BuildLinkedVariablesBySharedId(ProjectModel project)
    {
        var map = new Dictionary<Guid, List<GamePropertyDefinition>>();

        foreach (var variable in BuildVariableLookup(project).Values)
        {
            if (variable.SharedVariableId is not Guid sharedId || sharedId == Guid.Empty)
            {
                continue;
            }

            if (!map.TryGetValue(sharedId, out var variables))
            {
                variables = new List<GamePropertyDefinition>();
                map[sharedId] = variables;
            }

            variables.Add(variable);
        }

        return map;
    }
}