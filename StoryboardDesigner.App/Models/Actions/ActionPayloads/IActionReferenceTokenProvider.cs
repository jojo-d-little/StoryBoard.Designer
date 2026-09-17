namespace StoryboardDesigner.App.Models;

public interface IActionReferenceTokenProvider
{
    IReadOnlyList<string> GetReferenceTokens()
    {
        return [];
    }
}
