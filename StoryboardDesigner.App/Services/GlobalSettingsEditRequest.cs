namespace StoryboardDesigner.App.Services;

using StoryboardDesigner.App.Models;

/// <summary>Contains the editable global settings and project context for the Global Settings dialog.</summary>
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
	Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationDto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationDto?>? EnvironmentalEffectComputationEditor = null,
	IReadOnlyList<SharedVariableChoiceOption>? EnvironmentalEffectSharedVariableOptions = null,
	GamePropertyPickerContext? EnvironmentalEffectVariablePickerContext = null,
	string? RuntimeExportDirectory = null,
	string? RuntimeExportProjectDirectory = null)
{
	/// <summary>Gets or sets the interval between automatic project saves, in seconds.</summary>
	public int AutoSaveSeconds { get; init; } = AutoSaveSeconds;

	/// <summary>Gets or sets the name of the starting planet.</summary>
	public string StartingPlanetName { get; init; } = StartingPlanetName;

	/// <summary>Gets or sets the name of the player character object.</summary>
	public string PlayerCharacterObjectName { get; init; } = PlayerCharacterObjectName;

	/// <summary>Gets or sets the room image canvas width, in pixels.</summary>
	public int RoomImageCanvasWidth { get; init; } = RoomImageCanvasWidth;

	/// <summary>Gets or sets the room image canvas height, in pixels.</summary>
	public int RoomImageCanvasHeight { get; init; } = RoomImageCanvasHeight;

	/// <summary>Gets or sets the room designer grid cell size, in pixels.</summary>
	public int RoomDesignerGridCellSize { get; init; } = RoomDesignerGridCellSize;

	/// <summary>Gets or sets the default step used when scaling object stacks.</summary>
	public double StackScaleStepDefault { get; init; } = StackScaleStepDefault;

	/// <summary>Gets or sets the default minimum scale used for object stacks.</summary>
	public double MinStackScaleDefault { get; init; } = MinStackScaleDefault;

	/// <summary>Gets or sets the display name shown to players.</summary>
	public string GameDisplayName { get; init; } = GameDisplayName;

	/// <summary>Gets or sets the summary shown to players.</summary>
	public string GameSummary { get; init; } = GameSummary;

	/// <summary>Gets or sets the image paths used to preview the game.</summary>
	public List<string> GamePreviewImages { get; init; } = GamePreviewImages;

	/// <summary>Gets or sets the default point-light settings.</summary>
	public PointLightDefaults? PointLightDefaults { get; init; } = PointLightDefaults;

	/// <summary>Gets or sets the default room ambient-lighting settings.</summary>
	public RoomAmbientLighting? DefaultRoomAmbientLighting { get; init; } = DefaultRoomAmbientLighting;

	/// <summary>Gets or sets the default room sound-loss settings.</summary>
	public RoomSoundLossDefaults? RoomSoundLossDefaults { get; init; } = RoomSoundLossDefaults;

	/// <summary>Gets or sets the environmental effects available to the project.</summary>
	public List<EnvironmentalEffectDefinition>? EnvironmentalEffects { get; init; } = EnvironmentalEffects;

	/// <summary>Gets or sets the editor used to edit environmental-effect computations.</summary>
	public Func<Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationDto?, Storyboard.Shared.DesignerContracts.ProjectGamePropertyComputationDto?>? EnvironmentalEffectComputationEditor { get; init; } = EnvironmentalEffectComputationEditor;

	/// <summary>Gets or sets the image choices available to particle effects.</summary>
	public List<ParticleEffectImageChoice>? ParticleEffectImageChoices { get; init; } = ParticleEffectImageChoices;

	/// <summary>Gets or sets shared-variable choices for environmental effects.</summary>
	public IReadOnlyList<SharedVariableChoiceOption>? EnvironmentalEffectSharedVariableOptions { get; init; } = EnvironmentalEffectSharedVariableOptions;

	/// <summary>Gets or sets the property-picker context for environmental-effect variables.</summary>
	public GamePropertyPickerContext? EnvironmentalEffectVariablePickerContext { get; init; } = EnvironmentalEffectVariablePickerContext;

	/// <summary>Gets the optional portable expression for the runtime export directory.</summary>
	public string? RuntimeExportDirectory { get; init; } = RuntimeExportDirectory;

	/// <summary>Gets the project directory used to resolve and choose portable export paths.</summary>
	public string? RuntimeExportProjectDirectory { get; init; } = RuntimeExportProjectDirectory;
}
