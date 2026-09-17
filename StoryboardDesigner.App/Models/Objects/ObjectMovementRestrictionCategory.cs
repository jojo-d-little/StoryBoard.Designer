namespace StoryboardDesigner.App.Models;

public sealed class ObjectMovementRestrictionCategory
{
    public ObjectMovementRestrictionRule N { get; set; } = new();
    public ObjectMovementRestrictionRule NE { get; set; } = new();
    public ObjectMovementRestrictionRule E { get; set; } = new();
    public ObjectMovementRestrictionRule SE { get; set; } = new();
    public ObjectMovementRestrictionRule S { get; set; } = new();
    public ObjectMovementRestrictionRule SW { get; set; } = new();
    public ObjectMovementRestrictionRule W { get; set; } = new();
    public ObjectMovementRestrictionRule NW { get; set; } = new();

    public bool HasAnyRule()
    {
        return N.HasRule()
               || NE.HasRule()
               || E.HasRule()
               || SE.HasRule()
               || S.HasRule()
               || SW.HasRule()
               || W.HasRule()
               || NW.HasRule();
    }
}
