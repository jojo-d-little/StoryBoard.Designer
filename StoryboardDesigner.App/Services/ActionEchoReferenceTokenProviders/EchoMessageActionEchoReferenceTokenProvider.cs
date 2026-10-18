namespace StoryboardDesigner.App.Services;

public sealed class EchoMessageActionEchoReferenceTokenProvider : EmptyActionEchoReferenceTokenProviderBase
{
    public override CommandActionType ActionType => CommandActionType.EchoMessage;
}
