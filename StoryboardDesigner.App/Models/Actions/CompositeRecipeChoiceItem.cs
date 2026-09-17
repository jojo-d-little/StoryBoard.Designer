namespace StoryboardDesigner.App.Models;

public sealed class CompositeRecipeChoiceItem
{
    public Guid RecipeId { get; init; }
    public Guid TargetObjectId { get; init; }
    public string TargetName { get; init; } = string.Empty;
    public List<Guid> RequiredPartObjectIds { get; init; } = new();
    public string RequiredPartsDisplay { get; init; } = string.Empty;
    public string PartRequirementMode { get; init; } = "AllRequired";
    public int? MinimumRequiredPartCount { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(RequiredPartsDisplay)
        ? TargetName
        : $"{TargetName} <- {RequiredPartsDisplay}";
}
