using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Services;

public sealed class SetFlagActionEchoReferenceTokenProvider : EmptyActionEchoReferenceTokenProviderBase
{
    public override CommandActionType ActionType => CommandActionType.SetFlag;
}
