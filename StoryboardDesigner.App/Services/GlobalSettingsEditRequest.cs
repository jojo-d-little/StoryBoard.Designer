namespace StoryboardDesigner.App.Services;

public sealed record GlobalSettingsEditRequest(
	int AutoSaveSeconds,
	string StartingPlanetName,
	string PlayerCharacterObjectName,
	int RoomImageCanvasWidth,
	int RoomImageCanvasHeight,
	int RoomDesignerGridCellSize,
	double StackScaleStepDefault,
	double MinStackScaleDefault,
	string GameDisplayName,
	string GameSummary,
	List<string> GamePreviewImages);
