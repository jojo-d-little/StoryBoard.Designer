using System.Security.Cryptography;
using System.IO;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class SourceImageManagementService
{
    public static PreviewResult BuildPreview(
        ProjectModel project,
        string projectFilePath,
        SourceImageManagementMode mode,
        bool repointSourcePaths)
    {
        var result = new PreviewResult();
        var projectRoot = Path.GetDirectoryName(projectFilePath) ?? Environment.CurrentDirectory;
        var sourceRoot = Path.Combine(projectRoot, "project-source-images");

        foreach (var imageRef in EnumerateImageRefs(project))
        {
            var current = imageRef.GetPath();
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            var sourceResolved = DesignerImagePathResolver.ResolveSourcePath(current, projectFilePath);
            var exportResolved = DesignerImagePathResolver.ResolveExportAssetPath(current, projectFilePath);

            if (mode == SourceImageManagementMode.RepairMissingSources)
            {
                if (!string.IsNullOrWhiteSpace(sourceResolved))
                {
                    result.Items.Add(new PreviewItem
                    {
                        ScopePath = imageRef.ScopePath,
                        Channel = imageRef.Channel,
                        CurrentPath = current,
                        Status = "Source available"
                    });
                    continue;
                }

                var localMatch = ResolveUniqueLocalMatch(sourceRoot, imageRef.Bucket, Path.GetFileName(current));
                if (!string.IsNullOrWhiteSpace(localMatch))
                {
                    result.Items.Add(new PreviewItem
                    {
                        ScopePath = imageRef.ScopePath,
                        Channel = imageRef.Channel,
                        CurrentPath = current,
                        ProposedPath = localMatch,
                        Status = repointSourcePaths ? "Will repoint to project-source-images" : "Local match found (repoint disabled)",
                        ApplyAction = repointSourcePaths
                            ? () => imageRef.SetPath(localMatch)
                            : null
                    });
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(exportResolved))
                {
                    result.Items.Add(new PreviewItem
                    {
                        ScopePath = imageRef.ScopePath,
                        Channel = imageRef.Channel,
                        CurrentPath = current,
                        Status = "Export fallback available"
                    });
                    continue;
                }

                result.Items.Add(new PreviewItem
                {
                    ScopePath = imageRef.ScopePath,
                    Channel = imageRef.Channel,
                    CurrentPath = current,
                    Status = "Missing source and fallback"
                });

                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceResolved))
            {
                result.Items.Add(new PreviewItem
                {
                    ScopePath = imageRef.ScopePath,
                    Channel = imageRef.Channel,
                    CurrentPath = current,
                    Status = "Skipped (source missing)"
                });
                continue;
            }

            var ownerFolder = Path.Combine(sourceRoot, imageRef.Bucket, imageRef.OwnerFolderName);
            var targetFileName = GetConflictSafeTargetFileName(ownerFolder, Path.GetFileName(sourceResolved), sourceResolved);
            var targetPath = Path.Combine(ownerFolder, targetFileName);

            result.Items.Add(new PreviewItem
            {
                ScopePath = imageRef.ScopePath,
                Channel = imageRef.Channel,
                CurrentPath = current,
                ProposedPath = targetPath,
                Status = repointSourcePaths ? "Will copy and repoint" : "Will copy",
                ApplyAction = () =>
                {
                    Directory.CreateDirectory(ownerFolder);
                    if (!File.Exists(targetPath))
                    {
                        File.Copy(sourceResolved, targetPath, overwrite: false);
                    }

                    if (repointSourcePaths)
                    {
                        imageRef.SetPath(targetPath);
                    }
                }
            });
        }

        return result;
    }

    public static SourceImageManagementDialogResult Apply(
        PreviewResult preview,
        string projectFilePath,
        SourceImageManagementMode mode)
    {
        var applied = 0;
        var diagnosticsLines = new List<string>
        {
            $"Apply run at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Mode: {mode}",
            string.Empty
        };

        foreach (var item in preview.Items)
        {
            if (item.ApplyAction is null)
            {
                diagnosticsLines.Add($"SKIP | {item.ScopePath} | {item.Channel} | {item.Status}");
                continue;
            }

            item.ApplyAction();
            applied++;
            diagnosticsLines.Add($"APPLY | {item.ScopePath} | {item.Channel} | {item.CurrentPath} -> {item.ProposedPath}");
        }

        WriteDiagnostics(projectFilePath, "apply", diagnosticsLines, mode, preview.Items.Count, applied);
        return new SourceImageManagementDialogResult(applied, $"Applied {applied} of {preview.Items.Count} evaluated image references.");
    }

    public static string BuildPreviewText(
        PreviewResult preview,
        string projectFilePath,
        SourceImageManagementMode mode)
    {
        var lines = new List<string>
        {
            $"Preview run at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            preview.BuildSummary(mode),
            string.Empty
        };

        foreach (var item in preview.Items)
        {
            var plan = string.IsNullOrWhiteSpace(item.ProposedPath) ? item.Status : $"{item.Status} -> {item.ProposedPath}";
            lines.Add($"{item.ScopePath} | {item.Channel} | {item.CurrentPath} | {plan}");
        }

        WriteDiagnostics(projectFilePath, "preview", lines, mode, preview.Items.Count, preview.ActionableCount);
        return string.Join(Environment.NewLine, lines);
    }

    private static void WriteDiagnostics(
        string projectFilePath,
        string runType,
        IReadOnlyList<string> lines,
        SourceImageManagementMode mode,
        int total,
        int actionableOrApplied)
    {
        var projectRoot = Path.GetDirectoryName(projectFilePath) ?? Environment.CurrentDirectory;
        var diagnosticsRoot = Path.Combine(projectRoot, "source-image-management");
        Directory.CreateDirectory(diagnosticsRoot);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var runFile = Path.Combine(diagnosticsRoot, $"{runType}-{stamp}.txt");
        File.WriteAllLines(runFile, lines);

        var appendLog = Path.Combine(diagnosticsRoot, "activity.log");
        File.AppendAllLines(appendLog,
        [
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] type={runType} mode={mode} total={total} count={actionableOrApplied} file={Path.GetFileName(runFile)}"
        ]);
    }

    private static string? ResolveUniqueLocalMatch(string sourceRoot, string bucket, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(sourceRoot))
        {
            return null;
        }

        var bucketPath = Path.Combine(sourceRoot, bucket);
        var bucketMatches = Directory.Exists(bucketPath)
            ? Directory.EnumerateFiles(bucketPath, fileName, SearchOption.AllDirectories).Take(2).ToList()
            : [];

        if (bucketMatches.Count == 1)
        {
            return bucketMatches[0];
        }

        if (bucketMatches.Count > 1)
        {
            return null;
        }

        var globalMatches = Directory.EnumerateFiles(sourceRoot, fileName, SearchOption.AllDirectories).Take(2).ToList();
        return globalMatches.Count == 1 ? globalMatches[0] : null;
    }

    private static string GetConflictSafeTargetFileName(string ownerFolder, string fileName, string sourcePath)
    {
        if (!Directory.Exists(ownerFolder))
        {
            return fileName;
        }

        var candidate = Path.Combine(ownerFolder, fileName);
        if (!File.Exists(candidate))
        {
            return fileName;
        }

        var existingBytes = File.ReadAllBytes(candidate);
        var sourceBytes = File.ReadAllBytes(sourcePath);
        if (existingBytes.SequenceEqual(sourceBytes))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var hash8 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant()[..8];
        return $"{stem}__{hash8}{ext}";
    }

    private static IEnumerable<ImageRef> EnumerateImageRefs(ProjectModel project)
    {
        foreach (var room in project.RoomTemplates)
        {
            foreach (var imageRef in EnumerateRoomImageRefs(room, "room-templates", $"{Sanitize(room.Name)}--{room.Id:N}", $"Global / Room Templates / {room.Name}"))
            {
                yield return imageRef;
            }
        }

        foreach (var obj in project.GlobalScope.GameObjects)
        {
            foreach (var imageRef in EnumerateObjectImageRefs(obj, "objects", $"{Sanitize(obj.Name)}--{obj.ObjectId:N}", $"Global / {obj.Name}"))
            {
                yield return imageRef;
            }
        }

        foreach (var obj in project.ObjectTemplates)
        {
            foreach (var imageRef in EnumerateObjectImageRefs(obj, "object-templates", $"{Sanitize(obj.Name)}--{obj.ObjectId:N}", $"Global / Object Templates / {obj.Name}"))
            {
                yield return imageRef;
            }
        }

        foreach (var obj in project.BaseObjects)
        {
            foreach (var imageRef in EnumerateObjectImageRefs(obj, "base-objects", $"{Sanitize(obj.Name)}--{obj.ObjectId:N}", $"Global / Base Objects / {obj.Name}"))
            {
                yield return imageRef;
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
                        var roomPath = $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name}";
                        foreach (var imageRef in EnumerateRoomImageRefs(room, "rooms", $"{Sanitize(room.Name)}--{room.Id:N}", roomPath))
                        {
                            yield return imageRef;
                        }

                        foreach (var obj in room.GameObjects)
                        {
                            foreach (var imageRef in EnumerateObjectImageRefs(obj, "objects", $"{Sanitize(obj.Name)}--{obj.ObjectId:N}", roomPath + " / " + obj.Name))
                            {
                                yield return imageRef;
                            }
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<ImageRef> EnumerateRoomImageRefs(Room room, string bucket, string ownerFolderName, string scopePath)
    {
        foreach (var image in room.Images)
        {
            yield return new ImageRef(scopePath, bucket, ownerFolderName, $"room:{image.Slot}:full", () => image.Image.FullImagePath, value => image.Image.FullImagePath = value);
            yield return new ImageRef(scopePath, bucket, ownerFolderName, $"room:{image.Slot}:gray", () => image.Image.GrayMapImagePath, value => image.Image.GrayMapImagePath = value);
            yield return new ImageRef(scopePath, bucket, ownerFolderName, $"room:{image.Slot}:normal", () => image.Image.NormalMapImagePath, value => image.Image.NormalMapImagePath = value);
        }
    }

    private static IEnumerable<ImageRef> EnumerateObjectImageRefs(GameObject obj, string bucket, string ownerFolderName, string scopePath)
    {
        var variants = obj.ImageVariants
            .Where(static variant => !string.IsNullOrWhiteSpace(variant.VariantName))
            .ToList();

        if (variants.Count == 0)
        {
            var defaultVariant = new ObjectImageVariant
            {
                VariantName = "default",
                FullImagePath = string.Empty,
                ImageScale = 1,
                IsDefault = true
            };
            obj.ImageVariants.Add(defaultVariant);
            variants.Add(defaultVariant);
        }

        foreach (var variant in variants)
        {
            var key = string.IsNullOrWhiteSpace(variant.VariantName) ? "default" : variant.VariantName.Trim();
            yield return new ImageRef(scopePath, bucket, ownerFolderName, $"object:variant:{key}:full", () => variant.FullImagePath, value => variant.FullImagePath = value);
        }

        foreach (var child in obj.ContainedObjects)
        {
            var childFolder = $"{Sanitize(child.Name)}--{child.ObjectId:N}";
            foreach (var nested in EnumerateObjectImageRefs(child, bucket, childFolder, scopePath + " / " + child.Name))
            {
                yield return nested;
            }
        }
    }

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = (value ?? string.Empty).Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }
}
