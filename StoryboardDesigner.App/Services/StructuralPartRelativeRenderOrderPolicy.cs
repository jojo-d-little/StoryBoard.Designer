using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

/// <summary>One row in the local front-to-back render order of a composite family.</summary>
public sealed record StructuralPartRenderOrderEntry(
    Guid? ObjectId,
    string DisplayName,
    bool IsParent,
    int? RelativeOrder)
{
    public string RelativeOrderLabel => IsParent ? "0" : RelativeOrder?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Legacy";
}

/// <summary>
/// Converts between the Designer's front-to-back family list and the Contracts field
/// stored on each direct parent-relative structural child.
/// </summary>
public static class StructuralPartRelativeRenderOrderPolicy
{
    public static List<StructuralPartRenderOrderEntry> BuildFrontToBack(
        string parentName,
        IReadOnlyList<StructuralPartPlacementEditItem> structuralChildren)
    {
        var eligible = structuralChildren
            .Where(static child => child.Placement.Mode == StructuralPartPlacementKind.ParentRelative)
            .ToList();
        var parent = new StructuralPartRenderOrderEntry(null, $"{parentName} (parent)", true, 0);
        if (eligible.All(static child => child.StructuralPartRelativeRenderOrder is null))
        {
            // Legacy Engine ordering adds increasing Z offsets in child collection order,
            // so later siblings render in front of earlier siblings and all render above
            // the parent.
            return eligible.AsEnumerable().Reverse()
                .Select(static child => new StructuralPartRenderOrderEntry(
                    child.ObjectId,
                    child.Name,
                    false,
                    null))
                .Append(parent)
                .ToList();
        }

        var explicitlyInFront = eligible
            .Where(static child => child.StructuralPartRelativeRenderOrder > 0)
            .OrderByDescending(static child => child.StructuralPartRelativeRenderOrder)
            .Select(ToEntry);
        var unAuthored = eligible
            .Where(static child => child.StructuralPartRelativeRenderOrder is null or 0)
            .Reverse()
            .Select(ToEntry);
        var explicitlyBehind = eligible
            .Where(static child => child.StructuralPartRelativeRenderOrder < 0)
            .OrderByDescending(static child => child.StructuralPartRelativeRenderOrder)
            .Select(ToEntry);

        return explicitlyInFront
            .Concat(unAuthored)
            .Append(parent)
            .Concat(explicitlyBehind)
            .ToList();
    }

    public static IReadOnlyDictionary<Guid, int> AssignRelativeRanks(
        IReadOnlyList<StructuralPartRenderOrderEntry> frontToBack)
    {
        var parentIndex = -1;
        for (var index = 0; index < frontToBack.Count; index++)
        {
            if (frontToBack[index].IsParent)
            {
                if (parentIndex >= 0)
                {
                    throw new ArgumentException("The family render order must contain exactly one parent placeholder.", nameof(frontToBack));
                }

                parentIndex = index;
            }
        }

        if (parentIndex < 0)
        {
            throw new ArgumentException("The family render order must contain a parent placeholder.", nameof(frontToBack));
        }

        var ranks = new Dictionary<Guid, int>();
        for (var index = 0; index < frontToBack.Count; index++)
        {
            var entry = frontToBack[index];
            if (entry.IsParent)
            {
                continue;
            }

            if (entry.ObjectId is not Guid objectId || objectId == Guid.Empty)
            {
                throw new ArgumentException("Every child row must identify a structural child object.", nameof(frontToBack));
            }

            var rank = parentIndex - index;
            if (rank == 0)
            {
                throw new ArgumentException("A child cannot occupy the parent placeholder rank.", nameof(frontToBack));
            }

            ranks.Add(objectId, rank);
        }

        return ranks;
    }

    /// <summary>Whether the child renders in front of its parent; omission keeps legacy behavior.</summary>
    public static bool RendersInFrontOfParent(int? relativeOrder) => relativeOrder is null or 0 or > 0;

    private static StructuralPartRenderOrderEntry ToEntry(StructuralPartPlacementEditItem child) =>
        new(child.ObjectId, child.Name, false, child.StructuralPartRelativeRenderOrder);
}
