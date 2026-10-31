namespace StoryboardDesigner.App.Services;

using StoryboardDesigner.App.Models;

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
	List<string> GamePreviewImages,
	PointLightDefaults? PointLightDefaults,
	RoomAmbientLighting? DefaultRoomAmbientLighting = null,
	RoomSoundLossDefaults? RoomSoundLossDefaults = null,
	List<EnvironmentalEffectDefinition>? EnvironmentalEffects = null,
	List<ParticleEffectImageChoice>? ParticleEffectImageChoices = null,
	Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationV2Dto?>? EnvironmentalEffectComputationEditor = null,
	IReadOnlyList<SharedVariableChoiceOption>? EnvironmentalEffectSharedVariableOptions = null,
	GamePropertyPickerContext? EnvironmentalEffectVariablePickerContext = null);
