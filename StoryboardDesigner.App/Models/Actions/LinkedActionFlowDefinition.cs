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
