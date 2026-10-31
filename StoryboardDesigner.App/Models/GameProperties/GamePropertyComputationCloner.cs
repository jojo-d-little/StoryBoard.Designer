using System.Text.Json;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public static class GamePropertyComputationCloner
{
    public static ProjectGamePropertyExpressionDto? CloneExpression(ProjectGamePropertyExpressionDto? expression) =>
        expression is null
            ? null
            : JsonSerializer.Deserialize<ProjectGamePropertyExpressionDto>(JsonSerializer.Serialize(expression));

    public static ProjectGamePropertyComputationV2Dto? Clone(ProjectGamePropertyComputationV2Dto? computation) =>
        computation is null
            ? null
            : JsonSerializer.Deserialize<ProjectGamePropertyComputationV2Dto>(JsonSerializer.Serialize(computation));

    public static void RemapOwnerIds(ProjectGamePropertyComputationV2Dto? computation, IReadOnlyDictionary<Guid, Guid> ownerIdMap)
    {
        if (computation?.Expression is null || ownerIdMap.Count == 0) return;
        Remap(computation.Expression);

        void Remap(ProjectGamePropertyExpressionDto expression)
        {
            if (expression.Source?.OwnerId is Guid ownerId && ownerIdMap.TryGetValue(ownerId, out var mappedOwnerId))
                expression.Source.OwnerId = mappedOwnerId;
            if (expression.Left is not null) Remap(expression.Left);
            if (expression.Right is not null) Remap(expression.Right);
            if (expression.Expression is not null) Remap(expression.Expression);
            foreach (var operand in expression.Operands ?? []) Remap(operand);
            foreach (var branch in expression.Branches ?? [])
            {
                if (branch.When is not null) Remap(branch.When);
                if (branch.Value is not null) Remap(branch.Value);
            }
            if (expression.Else is not null) Remap(expression.Else);
        }
    }
}
