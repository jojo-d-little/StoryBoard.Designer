using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record LockOperationRequirementsEditRequest(
    List<LockKeyRequirement> UnlockKeyRequirements,
    bool RequireKeyForLockOperation,
    bool UseUnlockKeysForLockOperation,
    List<LockKeyRequirement> LockKeyRequirements,
    IReadOnlyList<GameObjectSelectionOption> AvailableKeyOptions);
