namespace StoryboardDesigner.App.Services;

public sealed class CheckGamePropertyActionEchoReferenceTokenProvider : EmptyActionEchoReferenceTokenProviderBase
{
    public override CommandActionType ActionType => CommandActionType.CheckGameProperty;
}
