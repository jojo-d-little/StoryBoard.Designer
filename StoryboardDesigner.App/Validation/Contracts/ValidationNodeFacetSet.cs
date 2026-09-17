namespace StoryboardDesigner.App.Validation.Contracts;

public readonly record struct ValidationNodeFacetSet(
    bool HasActions,
    bool HasTraversalLegs,
    bool HasScriptContent);
