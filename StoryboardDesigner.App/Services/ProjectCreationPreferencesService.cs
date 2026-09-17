using System.IO;
using System.Text.Json;
using Storyboard.Shared.Serialization;

namespace StoryboardDesigner.App.Services;

public sealed class ProjectCreationPreferencesService : IProjectCreationPreferencesService
{
    private readonly JsonSerializerOptions _jsonOptions = StoryboardJsonSerializerOptions.Create();

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StoryboardDesigner",
        "project-creation.preferences.json");

    public ProjectCreationPreferences Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new ProjectCreationPreferences();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<ProjectCreationPreferences>(json, _jsonOptions);
            return loaded ?? new ProjectCreationPreferences();
        }
        catch
        {
            return new ProjectCreationPreferences();
        }
    }

    public void Save(ProjectCreationPreferences preferences)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(preferences, _jsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
