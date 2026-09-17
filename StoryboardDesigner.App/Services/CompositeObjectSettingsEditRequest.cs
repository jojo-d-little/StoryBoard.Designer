using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record CompositeObjectSettingsEditRequest(
    bool IsCompositeReversible,
    string CompositePartRequirementMode,
    int CompositeMinimumRequiredPartCount,
    List<Guid> CompositeRequiredPartObjectIds,
    IReadOnlyList<GameObjectSelectionOption> AvailablePartOptions,
    List<CompositePartRequirement>? CompositeRequiredParts = null);