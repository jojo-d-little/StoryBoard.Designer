namespace StoryboardDesigner.App.Services;

using StoryboardDesigner.App.Models;

public sealed record RoomSettingsEditRequest(
	string Name,
	string ProducerNotes,
	string NameInGame = "",
	int RoomCanvasWidth = 800,
	int RoomCanvasHeight = 600,
	int ProjectRoomGridCellSize = 40,
	RoomAmbientLighting? AmbientLighting = null,
	string NameSynonyms = "");
