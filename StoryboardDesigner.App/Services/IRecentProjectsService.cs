namespace StoryboardDesigner.App.Services;

public interface IRecentProjectsService
{
    List<string> Load();

    void Save(IEnumerable<string> projectPaths);
}
