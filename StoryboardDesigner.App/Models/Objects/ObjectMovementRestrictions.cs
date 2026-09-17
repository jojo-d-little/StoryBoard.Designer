namespace StoryboardDesigner.App.Models;

public sealed class ObjectMovementRestrictions
{
    public int? MultiLegMaxTotalDistanceCells { get; set; }

    public ObjectMovementRestrictionCategory FirstUnstacked { get; set; } = new();
    public ObjectMovementRestrictionCategory FirstStacked { get; set; } = new();
    public ObjectMovementRestrictionCategory SubsequentUnstacked { get; set; } = new();
    public ObjectMovementRestrictionCategory SubsequentStacked { get; set; } = new();

    public bool HasAnyRule()
    {
        return MultiLegMaxTotalDistanceCells.HasValue
               || FirstUnstacked.HasAnyRule()
               || FirstStacked.HasAnyRule()
               || SubsequentUnstacked.HasAnyRule()
               || SubsequentStacked.HasAnyRule();
    }
}
