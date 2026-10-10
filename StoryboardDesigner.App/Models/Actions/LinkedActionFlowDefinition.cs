using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class LinkedActionFlowDefinition
{
    public int? EntryNodeId { get; set; }
    public List<LinkedActionFlowNode> Nodes { get; set; } = new();

    public LinkedActionFlowDefinition Clone()
    {
        return new LinkedActionFlowDefinition
        {
            EntryNodeId = EntryNodeId,
            Nodes = Nodes.Select(static node => node.Clone()).ToList()
        };
    }

    public IReadOnlyList<LinkedActionReference> GetEntryLinks()
    {
        var entry = EntryNodeId.HasValue
            ? Nodes.FirstOrDefault(node => node.NodeId == EntryNodeId.Value)
            : null;

        return entry is null
            ? Array.Empty<LinkedActionReference>()
            : new[]
            {
                new LinkedActionReference
                {
                    ActionId = entry.ActionId,
                    RunWhen = LinkedActionRunWhen.Always,
                    Order = 0
                }
            };
    }
}

public sealed class LinkedActionFlowNode
{
    public int NodeId { get; set; }
    public Guid ActionId { get; set; }
    public LinkedActionExitResult? OnAlwaysExitResult { get; set; }
    public LinkedActionExitResult? OnSuccessExitResult { get; set; }
    public LinkedActionExitResult? OnFailureExitResult { get; set; }
    public List<LinkedActionFlowLink> Links { get; set; } = new();

    public LinkedActionFlowNode Clone()
    {
        return new LinkedActionFlowNode
        {
            NodeId = NodeId,
            ActionId = ActionId,
            OnAlwaysExitResult = OnAlwaysExitResult,
            OnSuccessExitResult = OnSuccessExitResult,
            OnFailureExitResult = OnFailureExitResult,
            Links = Links.Select(static link => link.Clone()).ToList()
        };
    }
}

public sealed class LinkedActionFlowLink
{
    public int TargetNodeId { get; set; }
    public LinkedActionRunWhen RunWhen { get; set; } = LinkedActionRunWhen.Always;
    public int Order { get; set; }

    public LinkedActionFlowLink Clone()
    {
        return new LinkedActionFlowLink
        {
            TargetNodeId = TargetNodeId,
            RunWhen = RunWhen,
            Order = Order
        };
    }
}
