namespace StoryboardDesigner.App.Services;

/// <summary>Provides action echo tokens from the canonical action manifest for an action type.</summary>
public sealed class ManifestActionEchoReferenceTokenProvider : IActionEchoReferenceTokenProvider
{
    /// <summary>Initializes a provider for the specified action type.</summary>
    /// <param name="actionType">Action whose declared output variables are exposed as echo tokens.</param>
    public ManifestActionEchoReferenceTokenProvider(CommandActionType actionType)
    {
        ActionType = actionType;
    }

    /// <inheritdoc />
    public CommandActionType ActionType { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> GetTokens() => ActionManifestCatalog.GetOutputVariableTokens(ActionType);
}
