using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ImagePathAvailabilityRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Room, ScopeNodeKind.GameObject };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "IMG-001",
        Title: "Image Path Availability",
        DefaultSeverity: ValidationSeverity.Warning,
        Category: "Images");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var projectFilePath = context.Request.ProjectFilePath;

        if (context.CandidateNode is Room room)
        {
            var roomPath = BuildRoomPath(context.Project, room);
            foreach (var image in room.Images)
            {
                foreach (var issue in EvaluateImageReference(
                             projectFilePath,
                             roomPath,
                             image.Image.FullImagePath,
                             $"room image slot '{image.Slot}' (full)",
                             preferredSourceBucket: "rooms"))
                {
                    yield return issue;
                }

                foreach (var issue in EvaluateImageReference(
                             projectFilePath,
                             roomPath,
                             image.Image.GrayMapImagePath,
                             $"room image slot '{image.Slot}' (gray)",
                             preferredSourceBucket: "rooms"))
                {
                    yield return issue;
                }

                foreach (var issue in EvaluateImageReference(
                             projectFilePath,
                             roomPath,
                             image.Image.NormalMapImagePath,
                             $"room image slot '{image.Slot}' (normal)",
                             preferredSourceBucket: "rooms"))
                {
                    yield return issue;
                }
            }

            yield break;
        }

        if (context.CandidateNode is GameObject gameObject)
        {
            var objectPath = BuildObjectPath(context.Project, gameObject);
            var objectLookup = context.SharedState.GetOrAdd(
                "IMG:ProjectObjectsById",
                () => BuildObjectLookup(context.Project));
            var effectiveDefinitionOwnedSource = ResolveEffectiveDefinitionOwnedSource(gameObject, objectLookup);
            var requireExportFallback = !IsObjectTemplateGameObject(context.Project, gameObject);
            foreach (var variant in effectiveDefinitionOwnedSource.ImageVariants.Where(static variant => !string.IsNullOrWhiteSpace(variant.VariantName)))
            {
                var variantName = variant.VariantName.Trim();
                foreach (var issue in EvaluateImageReference(
                             projectFilePath,
                             objectPath,
                             variant.FullImagePath,
                             $"object image variant '{variantName}' (full)",
                             preferredSourceBucket: "objects",
                             requireExportFallback: requireExportFallback))
                {
                    yield return issue;
                }
            }
        }
    }

    private IEnumerable<ValidationIssue> EvaluateImageReference(
        string? projectFilePath,
        string ownerPath,
        string configuredPath,
        string referenceLabel,
        string preferredSourceBucket,
        bool requireExportFallback = true)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            yield break;
        }

        var sourceExists = !string.IsNullOrWhiteSpace(DesignerImagePathResolver.ResolveSourcePath(configuredPath, projectFilePath));
        var exportExists = !string.IsNullOrWhiteSpace(DesignerImagePathResolver.ResolveExportAssetPath(configuredPath, projectFilePath));
        var sourceLibraryAmbiguous = DesignerImagePathResolver.HasAmbiguousSourceLibraryMatches(configuredPath, projectFilePath, preferredSourceBucket);

        if (!sourceExists && exportExists)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                ValidationSeverity.Warning,
                ownerPath,
                sourceLibraryAmbiguous
                    ? $"{referenceLabel}: source image is missing, exported fallback exists, and project-source-images contains ambiguous filename matches."
                    : $"{referenceLabel}: source image is missing, but an exported fallback image exists.",
                sourceLibraryAmbiguous
                    ? "Open Tools > Source Image Management to review preview output and choose an explicit relink target."
                    : "Relink the source path to a valid image in the current node editor.");
            yield break;
        }

        if (!sourceExists && !exportExists)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                ValidationSeverity.Error,
                ownerPath,
                sourceLibraryAmbiguous
                    ? $"{referenceLabel}: source image is missing, no exported fallback image was found, and project-source-images has ambiguous filename matches."
                    : $"{referenceLabel}: source image is missing and no exported fallback image was found.",
                sourceLibraryAmbiguous
                    ? "Open Tools > Source Image Management and resolve ambiguous matches before relinking."
                    : "Relink the source path to a valid image or export the project to regenerate assets.");
            yield break;
        }

        if (sourceExists && !exportExists && requireExportFallback)
        {
            yield return new ValidationIssue(
                Metadata.RuleId,
                ValidationSeverity.Warning,
                ownerPath,
                $"{referenceLabel}: source image exists but exported fallback image is missing.",
                "Run clean export to materialize current image assets.");
        }
    }

    private static bool IsObjectTemplateGameObject(ProjectModel project, GameObject target)
    {
        foreach (var root in project.ObjectTemplates)
        {
            if (ContainsGameObjectRecursive(root, target))
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject ResolveEffectiveDefinitionOwnedSource(
        GameObject gameObject,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        if (!gameObject.LinkedBaseObjectId.HasValue)
        {
            return gameObject;
        }

        return objectLookup.TryGetValue(gameObject.LinkedBaseObjectId.Value, out var definition)
            && !ReferenceEquals(definition, gameObject)
            ? definition
            : gameObject;
    }

    private static bool ContainsGameObjectRecursive(GameObject current, GameObject target)
    {
        if (ReferenceEquals(current, target) || current.ObjectId == target.ObjectId)
        {
            return true;
        }

        foreach (var child in current.ContainedObjects)
        {
            if (ContainsGameObjectRecursive(child, target))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildRoomPath(ProjectModel project, Room room)
    {
        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    if (area.Rooms.Any(candidate => ReferenceEquals(candidate, room) || candidate.Id == room.Id))
                    {
                        return $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name}";
                    }
                }
            }
        }

        return $"Global / {room.Name}";
    }

    private static string BuildObjectPath(ProjectModel project, GameObject target)
    {
        foreach (var root in project.GlobalScope.GameObjects)
        {
            if (TryBuildObjectPathFromRoot(root, target, "Global", out var path))
            {
                return path;
            }
        }

        foreach (var root in project.ObjectTemplates)
        {
            if (TryBuildObjectPathFromRoot(root, target, "Global / Object Templates", out var path))
            {
                return path;
            }
        }

        foreach (var root in project.BaseObjects)
        {
            if (TryBuildObjectPathFromRoot(root, target, "Global / Base Objects", out var path))
            {
                return path;
            }
        }

        foreach (var planet in project.Planets)
        {
            foreach (var country in planet.Countries)
            {
                foreach (var area in country.Areas)
                {
                    foreach (var room in area.Rooms)
                    {
                        foreach (var root in room.GameObjects)
                        {
                            if (TryBuildObjectPathFromRoot(root, target, $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name}", out var path))
                            {
                                return path;
                            }
                        }
                    }
                }
            }
        }

        return $"Global / {target.Name}";
    }

    private static bool TryBuildObjectPathFromRoot(GameObject current, GameObject target, string prefix, out string path)
    {
        var currentPath = $"{prefix} / {current.Name}";
        if (ReferenceEquals(current, target) || current.ObjectId == target.ObjectId)
        {
            path = currentPath;
            return true;
        }

        foreach (var child in current.ContainedObjects)
        {
            if (TryBuildObjectPathFromRoot(child, target, currentPath, out path))
            {
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static Dictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        static void AddObject(GameObject obj, IDictionary<Guid, GameObject> target)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                target[obj.ObjectId] = obj;
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child, target);
            }
        }

        foreach (var template in project.ObjectTemplates)
        {
            AddObject(template, lookup);
        }

        foreach (var roomTemplateObject in project.RoomTemplates.SelectMany(room => room.GameObjects))
        {
            AddObject(roomTemplateObject, lookup);
        }

        foreach (var baseObject in project.BaseObjects)
        {
            AddObject(baseObject, lookup);
        }

        foreach (var playerObject in project.GlobalScope.GameObjects)
        {
            AddObject(playerObject, lookup);
        }

        foreach (var scopedObject in project.Planets
                     .SelectMany(planet => planet.GameObjects)
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.GameObjects))
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.Areas)
                         .SelectMany(area => area.GameObjects)))
        {
            AddObject(scopedObject, lookup);
        }

        foreach (var roomObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => room.GameObjects))
        {
            AddObject(roomObject, lookup);
        }

        foreach (var scopedBaseObject in project.Planets
                     .SelectMany(planet => planet.BaseObjects)
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.BaseObjects))
                     .Concat(project.Planets
                         .SelectMany(planet => planet.Countries)
                         .SelectMany(country => country.Areas)
                         .SelectMany(area => area.BaseObjects)))
        {
            AddObject(scopedBaseObject, lookup);
        }

        return lookup;
    }
}