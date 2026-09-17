namespace StoryboardDesigner.App.Services;
internal sealed partial class ProjectObjectMovementRestrictionCategoryDto
{
    public bool HasAnyRule()
    {
        return (N?.HasRule() ?? false)
               || (NE?.HasRule() ?? false)
               || (E?.HasRule() ?? false)
               || (SE?.HasRule() ?? false)
               || (S?.HasRule() ?? false)
               || (SW?.HasRule() ?? false)
               || (W?.HasRule() ?? false)
               || (NW?.HasRule() ?? false);
    }
}

