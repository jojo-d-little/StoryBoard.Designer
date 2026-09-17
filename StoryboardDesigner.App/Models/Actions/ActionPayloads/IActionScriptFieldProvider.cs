namespace StoryboardDesigner.App.Models;

public interface IActionScriptFieldProvider
{
    IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
    {
        return [];
    }
}
