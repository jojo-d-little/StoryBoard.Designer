namespace StoryboardDesigner.App.Services;

public interface IActionEchoReferenceTokenProvider
{
    CommandActionType ActionType { get; }

    IReadOnlyList<string> GetTokens();
}
