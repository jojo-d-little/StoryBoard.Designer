namespace StoryboardDesigner.App.Services;
internal sealed partial class ProjectObjectMovementRestrictionsDto
{
    public bool HasAnyRule()
    {
        return MultiLegMaxTotalDistanceCells.HasValue
               || (FirstUnstacked?.HasAnyRule() ?? false)
               || (FirstStacked?.HasAnyRule() ?? false)
               || (SubsequentUnstacked?.HasAnyRule() ?? false)
               || (SubsequentStacked?.HasAnyRule() ?? false);
    }
}

