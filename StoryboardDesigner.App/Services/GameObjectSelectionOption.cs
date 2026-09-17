namespace StoryboardDesigner.App.Services;

public sealed record GameObjectSelectionOption(
	Guid Id,
	string Name,
	bool IsInventoriable = false,
	IReadOnlyList<string>? VariableNames = null);