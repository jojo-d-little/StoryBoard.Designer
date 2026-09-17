using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Provides project persistence and export operations for authored storyboard data.
/// </summary>
public interface IJsonExportService
{
    /// <summary>
    /// Creates a new project folder structure and returns the created project file path.
    /// </summary>
    /// <param name="baseFolder">The parent folder where the project should be created.</param>
    /// <param name="projectName">The name of the project.</param>
    /// <returns>The full path to the newly created project file.</returns>
    string CreateProjectSkeleton(string baseFolder, string projectName);

    /// <summary>
    /// Saves the authored project model to disk.
    /// </summary>
    /// <param name="projectFilePath">The full path to the project file.</param>
    /// <param name="project">The project model to save.</param>
    void SaveProjectModel(string projectFilePath, ProjectModel project);

    /// <summary>
    /// Saves project-specific UI state sidecar data.
    /// </summary>
    /// <param name="projectFilePath">The full path to the project file.</param>
    /// <param name="uiState">The UI state to persist.</param>
    void SaveProjectUiState(string projectFilePath, ProjectUiState uiState);

    /// <summary>
    /// Attempts to load an authored project model from disk.
    /// </summary>
    /// <param name="projectFilePath">The full path to the project file.</param>
    /// <returns>The loaded project model when found and valid; otherwise <see langword="null"/>.</returns>
    ProjectModel? TryLoadProjectModel(string projectFilePath);

    /// <summary>
    /// Attempts to load a global node authoring payload for import workflows.
    /// </summary>
    /// <param name="globalNodeFilePath">The full path to the global node file.</param>
    /// <returns>The loaded import data when successful; otherwise <see langword="null"/>.</returns>
    ProjectGlobalNodeImportData? TryLoadGlobalNodeForImport(string globalNodeFilePath);

    /// <summary>
    /// Exports the runtime-clean project contract payloads from authored data.
    /// </summary>
    /// <param name="projectFilePath">The full path to the project file.</param>
    /// <param name="project">The authored project model.</param>
    /// <returns>The full path to the primary runtime export file.</returns>
    string ExportCleanProjectV1(string projectFilePath, ProjectModel project);

    /// <summary>
    /// Exports a full phase narrative review HTML document for producer review workflows.
    /// </summary>
    /// <param name="projectFilePath">The full path to the project file.</param>
    /// <param name="project">The authored project model.</param>
    /// <returns>The full path to the generated phase narrative review HTML file.</returns>
    string ExportPhaseNarrativeReviewHtml(string projectFilePath, ProjectModel project);
}
