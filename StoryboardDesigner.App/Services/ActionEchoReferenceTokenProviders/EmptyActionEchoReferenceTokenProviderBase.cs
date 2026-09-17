using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public abstract class EmptyActionEchoReferenceTokenProviderBase : IActionEchoReferenceTokenProvider
{
    public abstract CommandActionType ActionType { get; }

    public IReadOnlyList<string> GetTokens()
    {
        return Array.Empty<string>();
    }
}
