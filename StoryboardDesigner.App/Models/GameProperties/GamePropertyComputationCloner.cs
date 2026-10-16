using System.Text.Json;
using Storyboard.Shared.DesignerContracts;

namespace StoryboardDesigner.App.Models;

public static class GamePropertyComputationCloner
{
    public static ProjectGamePropertyComputationDto? Clone(ProjectGamePropertyComputationDto? computation)
    {
        return computation is null
            ? null
            : JsonSerializer.Deserialize<ProjectGamePropertyComputationDto>(JsonSerializer.Serialize(computation));
    }

    public static void RemapOwnerIds(ProjectGamePropertyComputationDto? computation, IReadOnlyDictionary<Guid, Guid> ownerIdMap)
    {
        if (computation?.Condition is null || ownerIdMap.Count == 0)
        {
            return;
        }

        void RemapSource(ProjectGamePropertySourceReferenceDto? source)
        {
            if (source?.OwnerId is Guid ownerId && ownerIdMap.TryGetValue(ownerId, out var mappedOwnerId))
            {
                source.OwnerId = mappedOwnerId;
            }
        }

        void RemapCondition(ProjectGamePropertyConditionDto condition)
        {
            RemapSource(condition.GamePropertySource);
            RemapSource(condition.RightOperand?.GamePropertySource);
            if (condition.Condition is not null) RemapCondition(condition.Condition);
            foreach (var child in condition.Conditions ?? []) RemapCondition(child);
        }

        RemapCondition(computation.Condition);
    }
}
