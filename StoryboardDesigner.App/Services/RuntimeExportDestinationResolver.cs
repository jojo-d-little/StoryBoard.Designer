using System.IO;
using System.Text.Json;
using Storyboard.Foundation.Paths;

namespace StoryboardDesigner.App.Services;

internal static class RuntimeExportDestinationResolver
{
    internal static string Resolve(string projectFilePath, string? expression)
    {
        var projectDirectory = Path.GetFullPath(Path.GetDirectoryName(projectFilePath)
            ?? throw new InvalidOperationException("Project file path must include a directory."));
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Path.Combine(projectDirectory, "GameRuntimeJson");
        }

        var result = PathExpressionResolver.Resolve(expression.Trim(), new PathExpressionOptions
        {
            AllowAbsolutePaths = true,
            AllowRelativePaths = true,
            AllowFileUris = false,
            BaseDirectory = projectDirectory
        });
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.PhysicalPath))
        {
            throw new InvalidOperationException(result.DiagnosticMessage ?? "Runtime export directory expression is invalid.");
        }

        var destination = ResolvePhysicalDirectory(result.PhysicalPath);
        var source = ResolvePhysicalDirectory(projectDirectory);
        var filesystemRoot = Path.GetPathRoot(destination)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), filesystemRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The filesystem root cannot be used as a runtime export directory.");
        }
        if (IsSameOrNested(destination, source) || IsSameOrNested(source, destination))
        {
            throw new InvalidOperationException("The configured runtime export directory must be separate from the authoring project folder.");
        }

        return destination;
    }

    internal static void EnsureNoConflictingProjectOutput(string projectFilePath, string? expression, string destination)
    {
        var projectDirectory = Path.GetFullPath(Path.GetDirectoryName(projectFilePath)!);
        var parent = Directory.GetParent(projectDirectory)?.FullName;
        if (parent is null || !Directory.Exists(parent)) return;

        var candidates = new List<string>();
        try
        {
            candidates.AddRange(Directory.EnumerateFiles(parent, "*.sbe.json", SearchOption.TopDirectoryOnly));
            foreach (var child in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                try { candidates.AddRange(Directory.EnumerateFiles(child, "*.sbe.json", SearchOption.TopDirectoryOnly)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(projectFilePath), StringComparison.OrdinalIgnoreCase)) continue;
            var candidateDirectory = Path.GetDirectoryName(candidate)!;
            string? otherExpression = null;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                if (document.RootElement.TryGetProperty("runtimeExportDirectory", out var value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    otherExpression = value.GetString();
                }
            }
            catch (Exception) { continue; }

            string otherDestination;
            try
            {
                otherDestination = string.IsNullOrWhiteSpace(otherExpression)
                    ? Path.Combine(candidateDirectory, "GameRuntimeJson")
                    : Resolve(candidate, otherExpression);
            }
            catch (Exception) { continue; }

            if (string.Equals(Path.GetFullPath(otherDestination).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Runtime export directory is already configured for another project: '{candidate}'. Choose a dedicated folder.");
            }
            if (IsSameOrNested(ResolvePhysicalDirectory(destination), ResolvePhysicalDirectory(candidateDirectory))
                || IsSameOrNested(ResolvePhysicalDirectory(candidateDirectory), ResolvePhysicalDirectory(destination)))
            {
                throw new InvalidOperationException($"Runtime export directory overlaps another project's authoring folder: '{candidate}'. Choose a dedicated folder.");
            }
        }
    }

    private static string ResolvePhysicalDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var current = root;
        foreach (var segment in fullPath[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = new DirectoryInfo(current);
            if (!info.Exists) continue;
            try
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null) current = Path.GetFullPath(target.FullName);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrNested(string candidate, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
