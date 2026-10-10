using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class LinkedActionGraphValidator
{
    public static List<string> Validate(
        IReadOnlyCollection<CommandAction> actions,
        string scopeLabel,
        IReadOnlyCollection<CommandAction>? additionalLinkTargetActions = null)
    {
        var errors = new List<string>();
        var actionById = actions
            .Where(static action => action.Id != Guid.Empty)
            .GroupBy(static action => action.Id)
            .ToDictionary(static group => group.Key, static group => group.First());
        var linkTargetById = new Dictionary<Guid, CommandAction>(actionById);
        foreach (var externalAction in additionalLinkTargetActions ?? Array.Empty<CommandAction>())
        {
            if (externalAction.Id != Guid.Empty)
            {
                linkTargetById.TryAdd(externalAction.Id, externalAction);
            }
        }

        var linkedTargets = new HashSet<Guid>();
        foreach (var action in actions)
        {
            if (action.ActionType == CommandActionType.Synonym)
            {
                var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(action);
                if (!synonymTargetActionId.HasValue)
                {
                    errors.Add($"- {scopeLabel}: synonym action '{action.Name}' requires a target action.");
                }
                else if (synonymTargetActionId.Value == action.Id)
                {
                    errors.Add($"- {scopeLabel}: synonym action '{action.Name}' cannot target itself.");
                }
                else if (!linkTargetById.ContainsKey(synonymTargetActionId.Value))
                {
                    errors.Add($"- {scopeLabel}: synonym action '{action.Name}' targets missing action id '{synonymTargetActionId.Value}'.");
                }
            }

            if (action.ActionType != CommandActionType.LinkedActions)
            {
                continue;
            }

            var definition = action.LinkedFlowDefinition;
            var nodesById = definition.Nodes
                .Where(static node => node.NodeId > 0)
                .GroupBy(static node => node.NodeId)
                .ToDictionary(static group => group.Key, static group => group.First());

            if (definition.Nodes.Count > nodesById.Count)
            {
                errors.Add($"- {scopeLabel}: linked action '{action.Name}' has duplicate or invalid flow node IDs.");
            }

            if (definition.Nodes.Count > 0 && !definition.EntryNodeId.HasValue)
            {
                errors.Add($"- {scopeLabel}: linked action '{action.Name}' has flow nodes but no entry step.");
            }

            if (definition.EntryNodeId.HasValue && !nodesById.ContainsKey(definition.EntryNodeId.Value))
            {
                errors.Add($"- {scopeLabel}: linked action '{action.Name}' has missing entry node {definition.EntryNodeId.Value}.");
            }

            foreach (var node in definition.Nodes)
            {
                linkedTargets.Add(node.ActionId);
                if (node.ActionId == Guid.Empty || !linkTargetById.TryGetValue(node.ActionId, out var target))
                {
                    errors.Add($"- {scopeLabel}: linked action '{action.Name}' references missing action id '{node.ActionId}'.");
                }
                else
                {
                    if (node.ActionId == action.Id)
                    {
                        errors.Add($"- {scopeLabel}: linked action '{action.Name}' cannot include itself.");
                    }

                    if (target.ActionType == CommandActionType.Synonym)
                    {
                        errors.Add($"- {scopeLabel}: linked flow cannot target synonym action '{target.Name}'.");
                    }
                }

                foreach (var link in node.Links)
                {
                    if (!nodesById.ContainsKey(link.TargetNodeId))
                    {
                        errors.Add($"- {scopeLabel}: linked action '{action.Name}' node {node.NodeId} targets missing flow node {link.TargetNodeId}.");
                    }
                }
            }

            ValidateCycles(nodesById, linkTargetById, scopeLabel, errors);
        }

        foreach (var synonymAction in actions.Where(static action => action.ActionType == CommandActionType.Synonym))
        {
            if (linkedTargets.Contains(synonymAction.Id))
            {
                errors.Add($"- {scopeLabel}: synonym action '{synonymAction.Name}' cannot be used in linked-action flow.");
            }
        }

        return errors;
    }

    private static void ValidateCycles(
        IReadOnlyDictionary<int, LinkedActionFlowNode> nodesById,
        IReadOnlyDictionary<Guid, CommandAction> actionLookup,
        string scopeLabel,
        List<string> errors)
    {
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();
        var path = new List<int>();

        foreach (var nodeId in nodesById.Keys)
        {
            Visit(nodeId);
        }

        void Visit(int nodeId)
        {
            if (visiting.Contains(nodeId))
            {
                var cycle = path.SkipWhile(id => id != nodeId).Append(nodeId).ToList();
                errors.Add($"- {scopeLabel}: linked-action cycle detected ({FormatPath(cycle)}).");
                return;
            }

            if (visited.Contains(nodeId) || !nodesById.TryGetValue(nodeId, out var node))
            {
                return;
            }

            visiting.Add(nodeId);
            path.Add(nodeId);
            foreach (var link in node.Links)
            {
                Visit(link.TargetNodeId);
            }
            path.RemoveAt(path.Count - 1);
            visiting.Remove(nodeId);
            visited.Add(nodeId);
        }

        string FormatPath(IEnumerable<int> nodeIds)
        {
            return string.Join(" -> ", nodeIds.Select(nodeId =>
            {
                if (!nodesById.TryGetValue(nodeId, out var node))
                {
                    return $"node {nodeId}";
                }

                return actionLookup.TryGetValue(node.ActionId, out var action)
                    ? $"{action.Name} ({node.ActionId:N})"
                    : $"{node.ActionId:N}";
            }));
        }
    }
}
