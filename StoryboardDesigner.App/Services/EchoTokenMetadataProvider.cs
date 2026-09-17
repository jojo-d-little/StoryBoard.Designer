namespace StoryboardDesigner.App.Services;

public static class EchoTokenMetadataProvider
{
    public static EchoTokenMetadata GetMetadata(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new EchoTokenMetadata
            {
                Category = "Variable",
                Description = string.Empty,
                Example = string.Empty
            };
        }

        if (string.Equals(token, "Choose variable...", StringComparison.Ordinal))
        {
            return new EchoTokenMetadata
            {
                Category = "Chooser",
                Description = "Open the full variable chooser for all available scoped variables.",
                Example = "Example: pick room.temperature or country.alertLevel"
            };
        }

        if (token.StartsWith("self.", StringComparison.OrdinalIgnoreCase))
        {
            return new EchoTokenMetadata
            {
                Category = "Self",
                Description = "Self variable: value from the current action-attached object.",
                Example = "Example: {self.name}"
            };
        }

        if (token.StartsWith("currentAction.", StringComparison.OrdinalIgnoreCase))
        {
            return new EchoTokenMetadata
            {
                Category = "Action",
                Description = "Anchored action variable: value produced by the current action execution context.",
                Example = "Example: {currentAction.resultCode}"
            };
        }

        if (RuntimeAnchorReferenceTokenCatalog.IsKnownAnchorRootedToken(token))
        {
            return new EchoTokenMetadata
            {
                Category = "Anchor",
                Description = "Session anchor variable rooted at a manifest-defined anchor key.",
                Example = "Example: {currentPlanet.name}"
            };
        }

        return new EchoTokenMetadata
        {
            Category = "Scoped",
            Description = "Scoped variable reference.",
            Example = "Example: {room.temperature}"
        };
    }
}
