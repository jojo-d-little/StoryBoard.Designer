
namespace StoryboardDesigner.App.Services;

public sealed class SynonymActionEchoReferenceTokenProvider : EmptyActionEchoReferenceTokenProviderBase
{
    public override CommandActionType ActionType => CommandActionType.Synonym;
}
