using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record SourceImageManagementDialogRequest(
    ProjectModel Project,
    string ProjectFilePath);
