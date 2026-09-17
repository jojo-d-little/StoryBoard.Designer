using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Models;

/// <summary>
/// Designer payload for ClearActiveRoomObjects action authoring.
/// </summary>
public sealed record ClearActiveRoomObjectsPayload(RuntimeClearActiveRoomObjectsScope ClearScope) : IActionPayload
{
	public CommandActionType ActionType => CommandActionType.ClearActiveRoomObjects;

	public IReadOnlyList<string> GetReferenceTokens()
	{
		return RuntimeActionOutputVariableKeyCatalog.GetDeclaredActionPropertyTokens(ActionType);
	}

	public IReadOnlyList<(string FieldName, string Value)> GetScriptFields()
	{
		return [];
	}
}
