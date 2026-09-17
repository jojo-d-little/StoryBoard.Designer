using System.IO;
using System.Text.Json;
using Storyboard.Shared.Serialization;

namespace StoryboardDesigner.App.Services;

public sealed class RecentProjectsService : IRecentProjectsService
{
    private readonly JsonSerializerOptions _jsonOptions = StoryboardJsonSerializerOptions.Create();

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StoryboardDesigner",
        "recent-projects.json");

    public List<string> Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new List<string>();
        }

        var json = File.ReadAllText(_settingsPath);
        var items = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
        return items?.Where(File.Exists).ToList() ?? new List<string>();
    }

    public void Save(IEnumerable<string> projectPaths)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(projectPaths.ToList(), _jsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
