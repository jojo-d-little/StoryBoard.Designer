using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelSourceImageManagementCommandTests
{
    [Fact]
    public void ManageSourceImages_WhenProjectNotSaved_ShowsWarning()
    {
        var project = new ProjectModel { Name = "Unsaved" };
        var projectUi = new ProjectUiServiceStub();
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        viewModel.ManageSourceImagesCommand.Execute(null);

        Assert.Contains(projectUi.WarningMessages, message => message.Contains("Save the project", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManageSourceImages_WhenAccepted_UpdatesStatusAndInvokesUiService()
    {
        var project = new ProjectModel { Name = "Saved" };
        var projectUi = new ProjectUiServiceStub
        {
            SourceImageManagementAccepted = true,
            SourceImageManagementResult = new SourceImageManagementDialogResult(2, "Applied 2 image changes.")
        };

        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(
            project,
            new TreeContextInteractionServiceStub(),
            projectUi);

        SetProjectFilePath(viewModel, Path.Combine(Path.GetTempPath(), "saved-project.sbe.json"));

        viewModel.ManageSourceImagesCommand.Execute(null);

        Assert.NotNull(projectUi.LastSourceImageManagementRequest);
        Assert.Equal(project, projectUi.LastSourceImageManagementRequest!.Project);
        Assert.Equal("Applied 2 image changes.", viewModel.ExportStatus);
        Assert.Contains(projectUi.InformationMessages, message => message.Contains("Applied 2 image changes.", StringComparison.Ordinal));
    }

    private static void SetProjectFilePath(object target, string value)
    {
        var field = target.GetType().GetField("_projectFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}
