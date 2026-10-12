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
        var currentObjectChoice = currentObjectId.HasValue
            ? availableObjects
                .Where(choice => choice.ObjectId == currentObjectId.Value && choice.SourceObject is not null)
                .OrderByDescending(choice => choice.SourceCategory.EndsWith("Base Objects", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault()
            : null;
        var currentObject = currentObjectChoice?.SourceObject;

        if (currentObject is not null)
        {
            if (currentObjectChoice!.SourceCategory.EndsWith("Base Objects", StringComparison.OrdinalIgnoreCase))
            {
                var baseObjectChoices = availableObjects
                    .Where(choice => choice.SourceObject is not null
                        && choice.SourceCategory.EndsWith("Base Objects", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var baseRoots = baseObjectChoices.Where(candidate => !baseObjectChoices.Any(other =>
                    !ReferenceEquals(other, candidate)
                    && EnumerateDescendants(other.SourceObject!).Any(descendant => descendant.ObjectId == candidate.ObjectId)));
                var baseRoot = baseRoots.FirstOrDefault(candidate => candidate.ObjectId == currentObjectId
                    || EnumerateDescendants(candidate.SourceObject!).Any(descendant => descendant.ObjectId == currentObjectId))
                    ?.SourceObject ?? currentObject;
                Add(baseRoot.ObjectId, $"This base object (local): {baseRoot.Name}");
                foreach (var child in EnumerateDescendants(baseRoot))
                {
                    Add(child.ObjectId, $"Base child (local): {child.Name}");
                }
            }
            else
            {
                Add(currentObject.ObjectId, $"This object (local): {currentObject.Name}");
                foreach (var child in EnumerateDescendants(currentObject))
                {
                    Add(child.ObjectId, $"Child object (local): {child.Name}");
                }
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
