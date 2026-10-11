using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

internal static class PresentationCueTargetChoices
{
    internal static IReadOnlyList<PresentationCueTargetChoice> Build(
        IReadOnlyList<MaterializeSourceObjectChoiceItem> availableObjects,
        Guid? currentObjectId,
        Guid? storedTargetId)
    {
        var result = new List<PresentationCueTargetChoice>
        {
            new(null, "(Command primary target)")
        };
        var seen = new HashSet<Guid>();
        var currentObject = currentObjectId.HasValue
            ? availableObjects
                .Where(choice => choice.ObjectId == currentObjectId.Value && choice.SourceObject is not null)
                .OrderByDescending(choice => choice.SourceCategory.EndsWith("Base Objects", StringComparison.OrdinalIgnoreCase))
                .Select(choice => choice.SourceObject)
                .FirstOrDefault()
            : null;

        if (currentObject is not null)
        {
            Add(currentObject.ObjectId, $"This object (local): {currentObject.Name}");
            foreach (var child in EnumerateDescendants(currentObject))
            {
                Add(child.ObjectId, $"Child object (local): {child.Name}");
            }
        }

        foreach (var choice in availableObjects.Where(choice =>
                     string.Equals(choice.SourceCategory, "Current Room", StringComparison.OrdinalIgnoreCase)))
        {
            Add(choice.ObjectId, $"Room object (absolute): {choice.DisplayName} [{choice.ObjectId:D}]");
        }

        if (storedTargetId is { } storedId && !seen.Contains(storedId))
        {
            var known = availableObjects.FirstOrDefault(choice => choice.ObjectId == storedId);
            var name = known is null ? "outside current picker scope" : known.DisplayName;
            Add(storedId, $"Saved target (absolute): {name} [{storedId:D}]");
        }

        return result;

        void Add(Guid id, string label)
        {
            if (id != Guid.Empty && seen.Add(id))
            {
                result.Add(new PresentationCueTargetChoice(id, label));
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateDescendants(GameObject parent)
    {
        foreach (var child in parent.ContainedObjects)
        {
            yield return child;
            foreach (var descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
