using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

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
