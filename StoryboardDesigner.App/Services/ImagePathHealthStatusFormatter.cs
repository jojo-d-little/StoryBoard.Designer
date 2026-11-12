namespace StoryboardDesigner.App.Services;

public static class ImagePathHealthStatusFormatter
{
    public static string BuildStatusLabel(string? configuredPath, string? projectFilePath, string sourceBucket, string? runtimeExportDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return "Not configured.";
        }

        if (!string.IsNullOrWhiteSpace(DesignerImagePathResolver.ResolveSourcePath(configuredPath, projectFilePath)))
        {
            return "Source available.";
        }

        var projectLibraryMatch = DesignerImagePathResolver.ResolveForPreview(configuredPath, projectFilePath, sourceBucket, runtimeExportDirectory);
        var exportMatch = DesignerImagePathResolver.ResolveExportAssetPath(configuredPath, projectFilePath, runtimeExportDirectory);
        var ambiguous = DesignerImagePathResolver.HasAmbiguousSourceLibraryMatches(configuredPath, projectFilePath, sourceBucket);

        if (ambiguous)
        {
            if (!string.IsNullOrWhiteSpace(exportMatch))
            {
                return "Fallback: exported asset used. Multiple project-source-images matches detected; relink explicitly.";
            }

            return "Missing source. Multiple project-source-images matches detected; relink explicitly.";
        }

        if (!string.IsNullOrWhiteSpace(projectLibraryMatch)
            && string.IsNullOrWhiteSpace(exportMatch))
        {
            return "Fallback: project-source-images match used.";
        }

        if (!string.IsNullOrWhiteSpace(exportMatch))
        {
            return "Fallback: exported asset used.";
        }

        return "Missing source and fallback.";
    }
}
