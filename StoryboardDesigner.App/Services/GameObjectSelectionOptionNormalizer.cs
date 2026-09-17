using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

[Flags]
public enum GameObjectFeatureRequirements
{
    None = 0,
    Inventoriable = 1 << 0,
    Openable = 1 << 1,
    Lockable = 1 << 2,
    Container = 1 << 3,
    Activatable = 1 << 4,
    Hidable = 1 << 5,
    Quantifiable = 1 << 6,
    CompositeTarget = 1 << 7
}

public enum GameObjectOptionSourceTarget
{
    RealObjects,
    ObjectTemplates
}

public sealed record GameObjectOptionListRequest(
    IEnumerable<GameObject>? Candidates,
    GameObject? CurrentObject,
    bool ExcludeCurrentObject = true);

public static class GameObjectSelectionOptionNormalizer
{
    public static IReadOnlyList<GameObjectSelectionOption> Build(GameObjectOptionListRequest request)
    {
        var source = request.Candidates ?? Array.Empty<GameObject>();

        return source
            .Where(static obj => obj is not null)
            .Where(obj => !request.ExcludeCurrentObject || !ReferenceEquals(obj, request.CurrentObject))
            .Where(obj => obj.ObjectId != Guid.Empty)
            .GroupBy(static obj => obj.ObjectId)
            .Select(static group => group.First())
            .OrderBy(static obj => obj.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static obj => new GameObjectSelectionOption(
                obj.ObjectId,
                obj.Name,
                obj.IsInventoriable,
                obj.Variables
                    .Where(static variable => !string.IsNullOrWhiteSpace(variable.Name))
                    .Select(static variable => variable.Name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static variableName => variableName, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();
    }
}
