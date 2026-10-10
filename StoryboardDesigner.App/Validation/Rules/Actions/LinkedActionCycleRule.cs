using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class LinkedActionCycleRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-008", "Linked Action Cycle", ValidationSeverity.Error, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        if (action.ActionType == CommandActionType.LinkedActions)
        {
            var flowCycle = TryFindFlowNodeCycle(action.LinkedFlowDefinition);
            if (flowCycle is null)
            {
                yield break;
            }

            var flowNames = flowCycle.Select(nodeId =>
            {
                var flowNode = action.LinkedFlowDefinition.Nodes.FirstOrDefault(node => node.NodeId == nodeId);
                return flowNode is not null && context.LinkTargetById.TryGetValue(flowNode.ActionId, out var target)
                    ? $"{target.Name} ({flowNode.ActionId:N})"
                    : $"node {nodeId}";
            });
            yield return new ValidationIssue(
                Metadata.RuleId,
                Metadata.DefaultSeverity,
                ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                $"linked-action cycle detected ({string.Join(" -> ", flowNames)}).");
            yield break;
        }

        var cycle = TryFindCycleStartingAt(action.Id, context.ActionById);
        if (cycle is null)
        {
            yield break;
        }

        yield return new ValidationIssue(
            Metadata.RuleId,
            Metadata.DefaultSeverity,
            ActionRuleSupport.BuildActionIssuePath(context.Path, action),
            $"linked-action cycle detected ({FormatPath(cycle, context.ActionById)}).");
    }

    private static IReadOnlyList<int>? TryFindFlowNodeCycle(LinkedActionFlowDefinition definition)
    {
        var nodesById = definition.Nodes
            .Where(static node => node.NodeId > 0)
            .GroupBy(static node => node.NodeId)
            .ToDictionary(static group => group.Key, static group => group.First());
        var visiting = new Dictionary<int, int>();
        var visited = new HashSet<int>();
        var path = new List<int>();

        foreach (var nodeId in nodesById.Keys)
        {
            var cycle = Visit(nodeId);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return null;

        IReadOnlyList<int>? Visit(int nodeId)
        {
            if (visiting.TryGetValue(nodeId, out var cycleStart))
            {
                return path.Skip(cycleStart).Append(nodeId).ToList();
            }

            if (visited.Contains(nodeId) || !nodesById.TryGetValue(nodeId, out var node))
            {
                return null;
            }

            visiting[nodeId] = path.Count;
            path.Add(nodeId);
            foreach (var link in node.Links)
            {
                var cycle = Visit(link.TargetNodeId);
                if (cycle is not null)
                {
                    return cycle;
                }
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(nodeId);
            visited.Add(nodeId);
            return null;
        }
    }

    private static IReadOnlyList<Guid>? TryFindCycleStartingAt(
        Guid startId,
        IReadOnlyDictionary<Guid, CommandAction> actionById)
    {
        var stack = new Stack<Guid>();
        var stackIndex = new Dictionary<Guid, int>();
        var visited = new HashSet<Guid>();
        var path = new List<Guid>();

        return Visit(startId);

        IReadOnlyList<Guid>? Visit(Guid id)
        {
            if (stackIndex.TryGetValue(id, out var cycleStart))
            {
                var cycle = path.Skip(cycleStart).Concat(new[] { id }).ToList();
                return cycle;
            }

            if (!actionById.TryGetValue(id, out var action) || !visited.Add(id))
            {
                return null;
            }

            stack.Push(id);
            stackIndex[id] = path.Count;
            path.Add(id);

            foreach (var targetId in action.GetLinkedActions().Select(link => link.ActionId).Where(actionById.ContainsKey).Distinct())
            {
                var cycle = Visit(targetId);
                if (cycle is not null)
                {
                    return cycle;
                }
            }

            stack.Pop();
            stackIndex.Remove(id);
            path.RemoveAt(path.Count - 1);
            return null;
        }
    }

    private static string FormatPath(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, CommandAction> actionById)
    {
        var names = ids
            .Select(id => actionById.TryGetValue(id, out var node)
                ? $"{node.Name} ({id:N})"
                : id.ToString("N"))
            .ToList();
        return string.Join(" -> ", names);
    }
}
