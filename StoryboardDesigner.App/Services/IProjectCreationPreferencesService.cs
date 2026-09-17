namespace StoryboardDesigner.App.Services;

public interface IProjectCreationPreferencesService
{
    ProjectCreationPreferences Load();

    void Save(ProjectCreationPreferences preferences);
}
