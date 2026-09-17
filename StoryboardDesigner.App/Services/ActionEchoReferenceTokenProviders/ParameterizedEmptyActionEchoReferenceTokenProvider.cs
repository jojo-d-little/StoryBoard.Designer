namespace StoryboardDesigner.App.Services;

public sealed class ParameterizedEmptyActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    public ParameterizedEmptyActionEchoReferenceTokenProvider(CommandActionType actionType)
    {
        ActionType = actionType;
    }

    public CommandActionType ActionType { get; }

    public IReadOnlyList<string> GetTokens()
    {
        return Array.Empty<string>();
    }
}