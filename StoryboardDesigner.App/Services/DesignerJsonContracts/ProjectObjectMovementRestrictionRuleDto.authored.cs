namespace StoryboardDesigner.App.Services;
internal sealed partial class ProjectObjectMovementRestrictionRuleDto
{
    public bool HasRule()
    {
        return MaxDistance.HasValue || AllowJumpOver.HasValue;
    }
}

