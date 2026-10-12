using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

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
