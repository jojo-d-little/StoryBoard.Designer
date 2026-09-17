namespace StoryboardDesigner.App.Models;

public sealed class LockOperationRequirements
{
    public List<LockKeyRequirement> UnlockKeyRequirements { get; set; } = new();
    public bool RequireKeyForLockOperation { get; set; }
    public bool UseUnlockKeysForLockOperation { get; set; }
    public List<LockKeyRequirement> LockKeyRequirements { get; set; } = new();
}
