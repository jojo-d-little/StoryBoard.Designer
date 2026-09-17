namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationSharedPropertyRelationship(
    Guid SourceVariableId,
    Guid TargetVariableId,
    string RelationshipKind,
    string SourceScopePath,
    string TargetScopePath);
