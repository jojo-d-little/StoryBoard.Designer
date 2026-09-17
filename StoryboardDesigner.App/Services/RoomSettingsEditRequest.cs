namespace StoryboardDesigner.App.Services;

public sealed record RoomSettingsEditRequest(
	string Name,
	string ProducerNotes,
	string NameInGame = "",
	int RoomCanvasWidth = 800,
	int RoomCanvasHeight = 600,
	int ProjectRoomGridCellSize = 40);
