namespace StoryboardDesigner.App.Validation.Contracts;

public interface IValidationLookupService
{
    bool TryGetSharedPropertyRelationship(Guid sourceVariableId, out ValidationSharedPropertyRelationship relationship);
    bool TryGetPropertyByVariableId(Guid variableId, out ValidationGamePropertyDescriptor property);
    IReadOnlyList<ValidationSharedPropertyRelationship> GetSharedPropertyRelationshipsForVariable(Guid variableId);
}
