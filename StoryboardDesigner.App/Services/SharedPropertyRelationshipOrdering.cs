namespace StoryboardDesigner.App.Services;

public static class SharedPropertyRelationshipOrdering
{
    public static IReadOnlyList<SharedPropertyRelationshipReviewItem> OrderForDisplay(IEnumerable<SharedPropertyRelationshipReviewItem> relationships)
    {
        return relationships
            .OrderBy(static relationship => relationship.Priority)
            .ThenBy(static relationship => relationship.RelationshipId)
            .ToList();
    }
}
