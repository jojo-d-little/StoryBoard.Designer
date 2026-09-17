namespace StoryboardDesigner.App.Validation.Contracts;

public enum ValidationSkipReason
{
    UnsupportedScopeType,
    NotInProfile,
    ExcludedByRequest,
    DescendantsRequired,
    FullProjectTraversalRequired,
    MissingRequiredFacet
}
