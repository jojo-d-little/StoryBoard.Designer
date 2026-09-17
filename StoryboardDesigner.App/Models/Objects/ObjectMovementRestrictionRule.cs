namespace StoryboardDesigner.App.Models;

public sealed class ObjectMovementRestrictionRule
{
    public int? MaxDistance { get; set; }
    public bool? AllowJumpOver { get; set; }

    public bool HasRule()
    {
        return MaxDistance.HasValue || AllowJumpOver.HasValue;
    }
}
