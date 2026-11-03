using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

/// <summary>Shared reachability rules for Linked Action flow nodes.</summary>
public static class LinkedActionFlowReachability
{
    public sealed record Analysis(
        IReadOnlyList<LinkedActionFlowNode> ReachableNodes,
        IReadOnlyList<LinkedActionFlowNode> OrphanedNodes);

    /// <summary>
    /// Finds all nodes reachable from EntryNodeId by following every branch. Reachable
    /// nodes retain their authored node order, with the entry first because serialized
    /// Linked Action payloads use their first node as the entry.
    /// </summary>
    public static Analysis Analyze(LinkedActionFlowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var nodesById = definition.Nodes
            .Where(static node => node.NodeId > 0)
            .GroupBy(static node => node.NodeId)
            .ToDictionary(static group => group.Key, static group => group.First());
        var reachableIds = new HashSet<int>();
        if (definition.EntryNodeId is int entryId && nodesById.ContainsKey(entryId))
        {
            var pending = new Stack<int>();
            pending.Push(entryId);
            while (pending.Count > 0)
            {
                var nodeId = pending.Pop();
                if (!reachableIds.Add(nodeId) || !nodesById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                // Link list order is intentionally left untouched. Traversal only
                // determines membership; output ordering comes from the definition.
                for (var index = node.Links.Count - 1; index >= 0; index--)
                {
                    pending.Push(node.Links[index].TargetNodeId);
                }
            }
        }

        var entry = definition.EntryNodeId is int validEntryId
            ? nodesById.GetValueOrDefault(validEntryId)
            : null;
        var reachable = new List<LinkedActionFlowNode>(reachableIds.Count);
        if (entry is not null)
        {
            reachable.Add(entry);
        }
        reachable.AddRange(definition.Nodes.Where(node => reachableIds.Contains(node.NodeId)
            && ReferenceEquals(nodesById[node.NodeId], node)
            && (entry is null || node.NodeId != entry.NodeId)));

        var orphaned = definition.Nodes
            .Where(node => node.NodeId <= 0 || !reachableIds.Contains(node.NodeId)
                || !ReferenceEquals(nodesById.GetValueOrDefault(node.NodeId), node))
            .ToList();
        return new Analysis(reachable, orphaned);
    }

    /// <summary>Removes nodes unreachable from the entry and returns them in authored order.</summary>
    public static IReadOnlyList<LinkedActionFlowNode> PruneUnreachableNodes(LinkedActionFlowDefinition definition)
    {
        var analysis = Analyze(definition);
        var orphaned = analysis.OrphanedNodes.ToList();
        var reachableNodes = analysis.ReachableNodes.ToHashSet();
        definition.Nodes.RemoveAll(node => !reachableNodes.Contains(node));
        return orphaned;
    }
}
