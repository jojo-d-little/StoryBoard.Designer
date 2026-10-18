namespace StoryboardDesigner.App.Services;

public sealed class LinkedActionsActionEchoReferenceTokenProvider : EmptyActionEchoReferenceTokenProviderBase
{
    public override CommandActionType ActionType => CommandActionType.LinkedActions;
}
