using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class PlacedRoomObjectChoicePolicy
{
    public static IReadOnlyList<MaterializeSourceObjectChoiceItem> Filter(
        IEnumerable<MaterializeSourceObjectChoiceItem> choices,
        Guid? currentObjectId = null)
    {
        var available = choices
            .Where(static choice => choice.SourceObject is not null && choice.ObjectId != Guid.Empty)
            .GroupBy(static choice => choice.ObjectId)
            .Select(static group => group.First())
            .ToList();

        // An action authored on an object definition should select from that
        // definition's family. Engine resolves these stable definition IDs to
        // the corresponding objects in the placed instance executing the action.
        if (currentObjectId is Guid ownerId && ownerId != Guid.Empty
            && available.FirstOrDefault(choice => choice.ObjectId == ownerId)?.SourceObject is GameObject owner)
        {
            return available
                .Where(choice => choice.SourceObject!.EnumerateSelfAndAncestors().Any(ancestor => ReferenceEquals(ancestor, owner)))
                .ToList();
        }

        // Room-scoped actions without a current object can target placed objects
        // in their room as before.
        return available
            .Where(static choice => choice.SourceObject!.EnumerateSelfAndAncestors().OfType<Room>().Any())
            .ToList();
    }
}
