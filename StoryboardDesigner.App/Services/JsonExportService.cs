using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using System.Globalization;
using Storyboard.Shared.GameServices;
using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.GameServices.RuntimeContext;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using Storyboard.Shared.Serialization;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Services;

public sealed class JsonExportService : IJsonExportService
{
    private static readonly HashSet<string> LinkedDefinitionOwnedJsonProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "nameSynonyms",
        "description",
        "isInventoriable",
        "inventoryPointsDefaultValue",
        "isContainer",
        "containerPointsDefaultValue",
        "isOpenable",
        "isOpenDefaultValue",
        "isLockable",
        "isLockedDefaultValue",
        "isActivatable",
        "isActiveDefaultValue",
        "isHidable",
        "isHiddenDefaultValue",
        "isQuantifiable",
        "compositeRecipe",
        "lockOperationRequirements",
        "imageVariants",
        "imageVariantChooserScript",
        "appearance",
        "additionalVerbs",
        "additionalDirectionals",
        "additionalDirectionalTraversalMappings",
        "availableGameActions",
        "commands",
        "validationErrors",
        "ignoredValidationRuleIds",
        "procedureIds"
    };

    private const string CleanPreviewImagesFolderName = "previewImages";

    private string SerializeWithLinkedInstancePruning<T>(T dto)
    {
        var json = JsonSerializer.Serialize(dto, _jsonOptions);
        var root = JsonNode.Parse(json);
        if (root is null)
        {
            return json;
        }

        PruneLinkedDefinitionOwnedJsonProperties(root);
        return root.ToJsonString(_jsonOptions);
    }

    private static void PruneLinkedDefinitionOwnedJsonProperties(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (IsLinkedInstanceObject(obj))
                {
                    foreach (var propertyName in LinkedDefinitionOwnedJsonProperties)
                    {
                        obj.Remove(propertyName);
                    }
                }

                foreach (var child in obj.ToList())
                {
                    if (child.Value is not null)
                    {
                        PruneLinkedDefinitionOwnedJsonProperties(child.Value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (child is not null)
                    {
                        PruneLinkedDefinitionOwnedJsonProperties(child);
                    }
                }

                break;
        }
    }

    private static bool IsLinkedInstanceObject(JsonObject obj)
    {
        if (HasNonEmptyGuid(obj, "linkedBaseObjectId"))
        {
            return true;
        }

        // Without a concrete linked base id, pruning definition-owned fields can lose authored data.
        return false;
    }

    private static bool HasNonEmptyGuid(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return false;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<Guid>(out var guidValue))
            {
                return guidValue != Guid.Empty;
            }

            if (value.TryGetValue<string>(out var text)
                && Guid.TryParse(text, out var parsedGuid))
            {
                return parsedGuid != Guid.Empty;
            }
        }

        return false;
    }

    private static string NormalizeProjectRootDefaultTraversalModeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(json);
        }
        catch
        {
            return json;
        }

        if (rootNode is not JsonObject rootObject)
        {
            return json;
        }

        string? traversalPropertyName = null;
        JsonNode? traversalNode = null;
        foreach (var property in rootObject)
        {
            if (!string.Equals(property.Key, "defaultTraversalMode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            traversalPropertyName = property.Key;
            traversalNode = property.Value;
            break;
        }

        var hasValidTraversalMode = traversalNode is JsonValue traversalValue
            && traversalValue.TryGetValue<string>(out var traversalToken)
            && !string.IsNullOrWhiteSpace(traversalToken)
            && Enum.TryParse<AreaAdjacencyMode>(traversalToken, ignoreCase: true, out _);

        if (!hasValidTraversalMode)
        {
            rootObject[traversalPropertyName ?? "defaultTraversalMode"] = AreaAdjacencyMode.FourDirectional.ToString();
        }

        return rootNode.ToJsonString();
    }

    private static bool TryGetBoolean(JsonObject obj, string propertyName, out bool value)
    {
        value = false;
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue jsonValue)
        {
            return false;
        }

        return jsonValue.TryGetValue(out value);
    }

    private static IEnumerable<GameObject> EnumerateGameObjectsRecursive(IEnumerable<GameObject>? source)
    {
        if (source is null)
        {
            yield break;
        }

        var stack = new Stack<GameObject>(source.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.ContainedObjects is null || current.ContainedObjects.Count == 0)
            {
                continue;
            }

            for (var i = current.ContainedObjects.Count - 1; i >= 0; i--)
            {
                stack.Push(current.ContainedObjects[i]);
            }
        }
    }
    private const string TraversalPassableVariableName = "isPassable";

    public const string ProjectFileExtension = ".sbe.json";
    private const int RoomObjectRenderZBase = 1000;
    private const string RoomFolderName = "Room";
    private const string LegacyRoomsFolderSuffix = ".rooms";
    private const string RoomFileSuffix = ".room.json";
    private const string ProjectStateFileSuffix = ".state.json";
    private const string ProjectGlobalsFileSuffix = ".globals.json";
    private const string PlanetFolderName = "Planet";
    private const string LegacyPlanetsFolderSuffix = ".planets";
    private const string PlanetFileSuffix = ".planet.json";
    private const string CountryFolderName = "Country";
    private const string LegacyCountriesFolderSuffix = ".countries";
    private const string CountryFileSuffix = ".country.json";
    private const string AreaFolderName = "Area";
    private const string LegacyAreasFolderSuffix = ".areas";
    private const string AreaFileSuffix = ".area.json";
    private const string ProcedureFolderName = "Procedure";
    private const string LegacyProceduresFolderSuffix = ".procedures";
    private const string ProcedureFileSuffix = ".procedure.json";
    private const string PhaseFolderName = "Book";
    private const string LegacyPhaseFolderName = "Phase";
    private const string PhaseFileSuffix = ".phase.json";
    private const string AuthoringGameObjectFolderName = "GameObject";
    private const string AuthoringTemplateFolderName = "Templates";
    private const string AuthoringGameObjectFileSuffix = ".object.json";
    private const string RoomTemplateFolderName = "RoomTemplate";
    private const string RoomTemplateFileSuffix = ".roomtemplate.json";
    private const string AuthoringIndexFileName = "authoring-index.html";
    private const string PhaseNarrativeReviewFileName = "phase-narrative-review.html";
    private const string CleanProjectFileSuffix = ".sbr.runtime.json";
    private const string CleanNavigationFileSuffix = ".sbr.runtime.navigation.json";
    private const string CleanRoomsFolderSuffix = ".sbr.runtime.rooms";
    private const string CleanRoomFileSuffix = ".sbr.runtime.room.json";
    private const string CleanExportFolderName = "GameRuntimeJson";
    private const string CleanAssetsFolderName = "assets";
    private const string CleanImagesFolderName = "images";
    private const string CleanSharedImagesFolderName = "_shared";
    private const string CleanSoundsFolderName = "sounds";
    private const string CleanSharedSoundsFolderName = "_shared";
    private const string CleanPresentationCuesFolderName = "PresentationCues";
    private const string PresentationCueCatalogFileName = "presentation-effects.catalog.json";
    private const string CleanProcedureFolderName = "Procedure";
    private const string CleanProcedureFileSuffix = ".procedure.json";
    private const string CleanAssetsManifestFileName = "assets-manifest.json";
    private const string CleanRuntimeIndexFileName = "runtime-index.html";
    private const string ScopeKindPlanetFolderName = "Planet";
    private const string ScopeKindCountryFolderName = "Country";
    private const string ScopeKindAreaFolderName = "Area";
    private const string ScopeKindRoomFolderName = "Room";
    private const string ScopeKindBookFolderName = "Book";
    private const string ScopeKindGameObjectFolderName = "GameObject";
    private const string ScopeNodeFileSuffix = ".runtime.json";

    private readonly JsonSerializerOptions _jsonOptions = StoryboardJsonSerializerOptions.Create();
    private readonly JsonSerializerOptions _jsonReadOptions = StoryboardJsonSerializerOptions.Create(propertyNameCaseInsensitive: true);

    public string CreateProjectSkeleton(string baseFolder, string projectName)
    {
        var projectFolder = Path.Combine(baseFolder, Sanitize(projectName));
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(Path.Combine(projectFolder, "assets"));

        var metadata = new ProjectMetadataDto
        {
            Name = projectName,
            CreatedUtc = DateTime.UtcNow
        };

        var projectFilePath = BuildProjectFilePath(projectFolder, projectName);
        File.WriteAllText(projectFilePath, JsonSerializer.Serialize(metadata, _jsonOptions));
        return projectFilePath;
    }

    public void SaveProjectModel(string projectFilePath, ProjectModel project)
    {
        ValidateTraversalOrThrow(project, "save");
        SharedVariableReconciliationService.ReconcileInPlace(project);
        RefreshRoomDerivedOccupancyCaches(project);

        var projectFolder = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            throw new InvalidOperationException("Project file path must include a directory.");
        }

        Directory.CreateDirectory(projectFolder);
        var pendingWrites = new List<(string FilePath, string Content)>();
        var pendingFolderClears = new List<(string FolderPath, string SearchPattern)>();

        var persistedFormatVersion = "1.0";
        var persistedSourceProjectName = project.Name;
        var persistedUpdatedUtc = DateTime.UtcNow;
        if (File.Exists(projectFilePath))
        {
            try
            {
                var existingRootJson = File.ReadAllText(projectFilePath);
                var normalizedExistingRootJson = NormalizeProjectRootDefaultTraversalModeJson(existingRootJson);
                var existingRoot = DeserializeWithFileContext<ProjectAuthoringRootDto>(projectFilePath, normalizedExistingRootJson, _jsonReadOptions);
                if (existingRoot is not null)
                {
                    if (!string.IsNullOrWhiteSpace(existingRoot.FormatVersion))
                    {
                        persistedFormatVersion = existingRoot.FormatVersion;
                    }

                    if (!string.IsNullOrWhiteSpace(existingRoot.SourceProjectName))
                    {
                        persistedSourceProjectName = existingRoot.SourceProjectName;
                    }

                    if (existingRoot.UpdatedUtc != default)
                    {
                        persistedUpdatedUtc = existingRoot.UpdatedUtc;
                    }
                }
            }
            catch
            {
                // Fall back to default metadata values if legacy/invalid root metadata cannot be parsed.
            }
        }

        var dto = new ProjectAuthoringRootDto
        {
            Name = project.Name,
            FormatVersion = persistedFormatVersion,
            SourceProjectName = persistedSourceProjectName,
            UpdatedUtc = persistedUpdatedUtc,
            HideEmptyConfiguration = project.HideEmptyConfiguration,
            AutoSaveSeconds = project.AutoSaveSeconds,
            RoomImageCanvasWidth = project.RoomImageCanvasWidth,
            RoomImageCanvasHeight = project.RoomImageCanvasHeight,
            RoomDesignerGridCellSize = project.RoomDesignerGridCellSize,
            StackScaleStepDefault = project.StackScaleStepDefault,
            MinStackScaleDefault = project.MinStackScaleDefault,
            SimulatorReplayFilePath = project.SimulatorReplayFilePath,
            SimulatorReplaySpeed = project.SimulatorReplaySpeed,
            DefaultTraversalMode = project.DefaultTraversalMode,
            ProjectIgnoredValidationRuleIds = project.IgnoredValidationRuleIds.ToList(),
            GlobalIgnoredValidationRuleIds = project.GlobalIgnoredValidationRuleIds.ToList(),
            ObjectTemplatesIgnoredValidationRuleIds = project.ObjectTemplatesIgnoredValidationRuleIds.ToList(),
            RoomTemplatesIgnoredValidationRuleIds = project.RoomTemplatesIgnoredValidationRuleIds.ToList(),
            BaseObjectsIgnoredValidationRuleIds = project.BaseObjectsIgnoredValidationRuleIds.ToList(),
            StartingPlanetName = project.StartingPlanetName,
            PlayerCharacterObjectName = project.PlayerCharacterObjectName,
            PlayerIgnoredValidationRuleIds = project.GlobalScope.IgnoredValidationRuleIds.ToList(),
            GameDisplayName = project.GameDisplayName,
            GameSummary = project.GameSummary,
            GamePreviewImages = project.GamePreviewImages.ToList()
        };
        pendingWrites.Add((projectFilePath, JsonSerializer.Serialize(dto, _jsonOptions)));

        var mappedGlobalObjects = project.GlobalScope.GameObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
        var mappedObjectTemplates = project.ObjectTemplates.Select(obj => ToProjectGameObjectDto(obj, null, scopeKindOverride: ScopeNodeKind.Templates)).ToList();
        var mappedBaseObjects = project.BaseObjects.Select(obj => ToProjectGameObjectDto(obj, null, scopeKindOverride: ScopeNodeKind.Templates)).ToList();
        var mappedRoomTemplates = project.RoomTemplates.Select(ToRoomTemplateDto).ToList();
        var mappedPhaseBooks = project.PhaseBooks.Select(ToPhaseNodeDto).ToList();
        var mappedPhases = FlattenPhaseDtos(mappedPhaseBooks)
            .Select(ToPhaseSidecarDto)
            .ToList();

        var mappedRoomTemplateObjects = mappedRoomTemplates
            .SelectMany(static room => room.GameObjects)
            .ToList();

        foreach (var templateRoom in mappedRoomTemplates)
        {
            templateRoom.GameObjectIds = templateRoom.GameObjects
                .Select(static obj => obj.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList();
            templateRoom.GameObjects = new List<ProjectGameObjectDto>();
        }

        var globalNode = new ProjectGlobalNodeDto
        {
            ScopeKind = ScopeNodeKind.Global,
            AdditionalVerbs = project.CommandVerbs.ToList(),
            AdditionalDirectionals = project.Directionals.ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(project.DirectionalTraversalMappings),
            AvailableGameActions = ToCommandActionDtos(project.GlobalScope.AvailableActions),
            EventSubscriptions = project.GlobalScope.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
            TimerDefinitions = project.GlobalScope.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
            GameProperties = project.GlobalVariables.Select(ToVariableDto).ToList(),
            SoundEffectLibraryEntries = project.GlobalScope.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
            ProcedureIds = (project.ProcedureIds ?? new List<Guid>())
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            SharedVariables = project.SharedVariables.Select(ToSharedVariableDto).ToList(),
            PlanetIds = project.Planets.Select(planet => EnsureScopeId(planet.Id, $"planet '{planet.Name}'")).ToList(),
            PhaseBookIds = mappedPhaseBooks
                .Select(static phase => phase.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            StartingPhasePageId = project.StartingPhasePageId,
            GameObjectIds = mappedGlobalObjects
                .Select(static obj => obj.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            GameObjects = new List<ProjectGameObjectDto>(),
            PhaseBooks = new List<PhaseNodeDto>(),
            ObjectTemplateIds = mappedObjectTemplates
                .Select(static obj => obj.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            ObjectTemplates = new List<ProjectGameObjectDto>(),
            RoomTemplateIds = mappedRoomTemplates
                .Select(static room => room.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            RoomTemplates = new List<RoomDto>(),
            BaseObjectIds = mappedBaseObjects
                .Select(static obj => obj.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            BaseObjects = new List<ProjectGameObjectDto>()
        };

        pendingWrites.Add((BuildProjectGlobalNodeFilePath(projectFilePath), SerializeWithLinkedInstancePruning(globalNode)));

        var authoringObjectDtos = new Dictionary<Guid, ProjectGameObjectDto>();
        RegisterAuthoringObjectDtos(authoringObjectDtos, mappedGlobalObjects);
        RegisterAuthoringObjectDtos(authoringObjectDtos, mappedObjectTemplates);
        RegisterAuthoringObjectDtos(authoringObjectDtos, mappedBaseObjects);
        RegisterAuthoringObjectDtos(authoringObjectDtos, mappedRoomTemplateObjects);

        var gameObjectFolderPath = BuildAuthoringGameObjectFolderPath(projectFilePath);
        pendingFolderClears.Add((gameObjectFolderPath, $"*{AuthoringGameObjectFileSuffix}"));

        var templatesFolderPath = BuildAuthoringTemplatesFolderPath(projectFilePath);
        pendingFolderClears.Add((templatesFolderPath, $"*{AuthoringGameObjectFileSuffix}"));

        var roomTemplatesFolderPath = BuildRoomTemplatesFolderPath(projectFilePath);
        pendingFolderClears.Add((roomTemplatesFolderPath, $"*{RoomTemplateFileSuffix}"));

        var phaseFolderPath = BuildPhasesFolderPath(projectFilePath);
        pendingFolderClears.Add((phaseFolderPath, $"*{PhaseFileSuffix}"));

        foreach (var roomTemplateDto in mappedRoomTemplates
                     .Where(static room => room.Id != Guid.Empty)
                     .OrderBy(static room => room.Id))
        {
            pendingWrites.Add((
                BuildRoomTemplateFilePath(projectFilePath, roomTemplateDto.Id),
                SerializeWithLinkedInstancePruning(roomTemplateDto)));
        }

            foreach (var phaseDto in mappedPhases
                     .Where(static phase => phase.Id != Guid.Empty)
                     .OrderBy(static phase => phase.Id))
            {
                pendingWrites.Add((
                BuildPhaseFilePath(projectFilePath, phaseDto.Id),
                SerializeWithLinkedInstancePruning(phaseDto)));
            }

        var projectState = new ProjectStateDto
        {
            UiState = ToProjectUiStateDto(project.UiState)
        };

        pendingWrites.Add((BuildProjectStateFilePath(projectFilePath), JsonSerializer.Serialize(projectState, _jsonOptions)));

        var proceduresFolderPath = BuildProceduresFolderPath(projectFilePath);
        pendingFolderClears.Add((proceduresFolderPath, $"*{ProcedureFileSuffix}"));

        var uniqueProcedures = (project.Procedures ?? new List<ProcedureDefinition>())
            .Where(static procedure => procedure.Id != Guid.Empty)
            .GroupBy(static procedure => procedure.Id)
            .Select(static group => group.First())
            .OrderBy(static procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static procedure => procedure.Id)
            .ToList();

        foreach (var procedure in uniqueProcedures)
        {
            var procedureDto = ToProcedureDefinitionDto(procedure);
            pendingWrites.Add((BuildProcedureFilePath(projectFilePath, procedureDto.Id), JsonSerializer.Serialize(procedureDto, _jsonOptions)));
        }

        var allRooms = project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .SelectMany(area => area.Rooms)
            .GroupBy(room => room.Id)
            .Select(group => group.First())
            .ToList();

        var roomsFolderPath = BuildRoomsFolderPath(projectFilePath);
        pendingFolderClears.Add((roomsFolderPath, $"*{RoomFileSuffix}"));

        foreach (var room in allRooms)
        {
            var mappedRoomObjects = room.GameObjects
                .Select((obj, index) => ToProjectGameObjectDto(
                    obj,
                    null,
                    ComputeRoomObjectRenderZOrder(index, room.GameObjects.Count)))
                .ToList();

            RegisterAuthoringObjectDtos(authoringObjectDtos, mappedRoomObjects);

            var roomDto = new RoomDto
            {
                Id = room.Id,
                ScopeKind = ScopeNodeKind.Room,
                Name = room.Name,
                NameInGame = room.NameInGame,
                HideEmptyConfiguration = room.HideEmptyConfiguration,
                Description = room.Description,
                ProducerNotes = room.ProducerNotes,
                RoomImageCanvasWidth = ResolveEffectiveRoomCanvasWidth(project, room),
                RoomImageCanvasHeight = ResolveEffectiveRoomCanvasHeight(project, room),
                RoomDisplayMode = room.RoomDisplayMode,
                ValidationErrors = room.ValidationErrors.ToList(),
                TraversalModeOverride = room.TraversalModeOverride?.ToString(),
                AvailableGameActions = ToCommandActionDtos(room.AvailableActions),
                GameObjectIds = mappedRoomObjects
                    .Select(static obj => obj.Id)
                    .Where(static id => id != Guid.Empty)
                    .Distinct()
                    .ToList(),
                GameObjects = new List<ProjectGameObjectDto>(),
                Images = room.Images.Select(i => new RoomImageDto
                {
                    Slot = i.Slot,
                    OverlayRenderOrder = i.OverlayRenderOrder,
                    OverlayOffsetX = i.OverlayOffsetX,
                    OverlayOffsetY = i.OverlayOffsetY,
                    OverlayRotationDegrees = i.OverlayRotationDegrees,
                    FullImagePath = i.Image.FullImagePath,
                    GrayMapImagePath = i.Image.GrayMapImagePath,
                    NormalMapImagePath = i.Image.NormalMapImagePath
                }).ToList(),
                GameProperties = room.Variables.Select(ToVariableDto).ToList(),
                AdditionalVerbs = room.AdditionalVerbs.ToList(),
                AdditionalDirectionals = room.AdditionalDirectionals.ToList(),
                IgnoredValidationRuleIds = room.IgnoredValidationRuleIds.ToList(),
                SoundEffectLibraryEntries = room.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
                AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(room.AdditionalDirectionalTraversalMappings),
                EventSubscriptions = room.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
                TimerDefinitions = room.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList()
            };

            pendingWrites.Add((BuildRoomFilePath(projectFilePath, room.Id), SerializeWithLinkedInstancePruning(roomDto)));
        }

        var planetsFolderPath = BuildPlanetsFolderPath(projectFilePath);
        pendingFolderClears.Add((planetsFolderPath, $"*{PlanetFileSuffix}"));

        var countriesFolderPath = BuildCountriesFolderPath(projectFilePath);
        pendingFolderClears.Add((countriesFolderPath, $"*{CountryFileSuffix}"));

        var areasFolderPath = BuildAreasFolderPath(projectFilePath);
        pendingFolderClears.Add((areasFolderPath, $"*{AreaFileSuffix}"));

        foreach (var planet in project.Planets)
        {
            var mappedPlanetBaseObjects = planet.BaseObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
            var mappedPlanetObjects = planet.GameObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
            RegisterAuthoringObjectDtos(authoringObjectDtos, mappedPlanetBaseObjects);
            RegisterAuthoringObjectDtos(authoringObjectDtos, mappedPlanetObjects);

            var planetDto = new PlanetDto
            {
                Id = EnsureScopeId(planet.Id, $"planet '{planet.Name}'"),
                ScopeKind = ScopeNodeKind.Planet,
                Name = planet.Name,
                HideEmptyConfiguration = planet.HideEmptyConfiguration,
                StartingCountryName = planet.StartingCountryName,
                ValidationErrors = planet.ValidationErrors.ToList(),
                GameProperties = planet.Variables.Select(ToVariableDto).ToList(),
                AdditionalVerbs = planet.AdditionalVerbs.ToList(),
                AdditionalDirectionals = planet.AdditionalDirectionals.ToList(),
                IgnoredValidationRuleIds = planet.IgnoredValidationRuleIds.ToList(),
                SoundEffectLibraryEntries = planet.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
                BaseObjectsIgnoredValidationRuleIds = planet.BaseObjectsIgnoredValidationRuleIds.ToList(),
                AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(planet.AdditionalDirectionalTraversalMappings),
                AvailableGameActions = ToCommandActionDtos(planet.AvailableActions),
                EventSubscriptions = planet.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
                TimerDefinitions = planet.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
                BaseObjectIds = mappedPlanetBaseObjects
                    .Select(static obj => obj.Id)
                    .Where(static id => id != Guid.Empty)
                    .Distinct()
                    .ToList(),
                BaseObjects = new List<ProjectGameObjectDto>(),
                GameObjectIds = mappedPlanetObjects
                    .Select(static obj => obj.Id)
                    .Where(static id => id != Guid.Empty)
                    .Distinct()
                    .ToList(),
                GameObjects = new List<ProjectGameObjectDto>(),
                CountryIds = planet.Countries.Select(country => EnsureScopeId(country.Id, $"country '{country.Name}'")).ToList()
            };

            pendingWrites.Add((BuildPlanetFilePath(projectFilePath, planetDto.Id), SerializeWithLinkedInstancePruning(planetDto)));

            foreach (var country in planet.Countries)
            {
                var mappedCountryBaseObjects = country.BaseObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
                var mappedCountryObjects = country.GameObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
                RegisterAuthoringObjectDtos(authoringObjectDtos, mappedCountryBaseObjects);
                RegisterAuthoringObjectDtos(authoringObjectDtos, mappedCountryObjects);

                var countryDto = new CountryDto
                {
                    Id = EnsureScopeId(country.Id, $"country '{country.Name}'"),
                    ScopeKind = ScopeNodeKind.Country,
                    ParentPlanetId = planetDto.Id,
                    Name = country.Name,
                    HideEmptyConfiguration = country.HideEmptyConfiguration,
                    StartingAreaName = country.StartingAreaName,
                    ValidationErrors = country.ValidationErrors.ToList(),
                    GameProperties = country.Variables.Select(ToVariableDto).ToList(),
                    AdditionalVerbs = country.AdditionalVerbs.ToList(),
                    AdditionalDirectionals = country.AdditionalDirectionals.ToList(),
                    IgnoredValidationRuleIds = country.IgnoredValidationRuleIds.ToList(),
                    SoundEffectLibraryEntries = country.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
                    BaseObjectsIgnoredValidationRuleIds = country.BaseObjectsIgnoredValidationRuleIds.ToList(),
                    AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(country.AdditionalDirectionalTraversalMappings),
                    AvailableGameActions = ToCommandActionDtos(country.AvailableActions),
                    EventSubscriptions = country.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
                    TimerDefinitions = country.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
                    BaseObjectIds = mappedCountryBaseObjects
                        .Select(static obj => obj.Id)
                        .Where(static id => id != Guid.Empty)
                        .Distinct()
                        .ToList(),
                    BaseObjects = new List<ProjectGameObjectDto>(),
                    GameObjectIds = mappedCountryObjects
                        .Select(static obj => obj.Id)
                        .Where(static id => id != Guid.Empty)
                        .Distinct()
                        .ToList(),
                    GameObjects = new List<ProjectGameObjectDto>(),
                    AreaIds = country.Areas.Select(area => EnsureScopeId(area.Id, $"area '{area.Name}'")).ToList()
                };

                pendingWrites.Add((BuildCountryFilePath(projectFilePath, countryDto.Id), SerializeWithLinkedInstancePruning(countryDto)));

                foreach (var area in country.Areas)
                {
                    var mappedAreaBaseObjects = area.BaseObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
                    var mappedAreaObjects = area.GameObjects.Select(obj => ToProjectGameObjectDto(obj, null)).ToList();
                    RegisterAuthoringObjectDtos(authoringObjectDtos, mappedAreaBaseObjects);
                    RegisterAuthoringObjectDtos(authoringObjectDtos, mappedAreaObjects);

                    var areaDto = new AreaDto
                    {
                        Id = EnsureScopeId(area.Id, $"area '{area.Name}'"),
                        ScopeKind = ScopeNodeKind.Area,
                        ParentCountryId = countryDto.Id,
                        Name = area.Name,
                        HideEmptyConfiguration = area.HideEmptyConfiguration,
                        ValidationErrors = area.ValidationErrors.ToList(),
                        AdjacencyMode = area.AdjacencyMode,
                        RoomDropBehavior = area.RoomDropBehavior,
                        TraversalModeOverride = area.TraversalModeOverride?.ToString(),
                        StartingRoomId = area.StartingRoomId,
                        GameProperties = area.Variables.Select(ToVariableDto).ToList(),
                        AdditionalVerbs = area.AdditionalVerbs.ToList(),
                        AdditionalDirectionals = area.AdditionalDirectionals.ToList(),
                        IgnoredValidationRuleIds = area.IgnoredValidationRuleIds.ToList(),
                        SoundEffectLibraryEntries = area.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
                        BaseObjectsIgnoredValidationRuleIds = area.BaseObjectsIgnoredValidationRuleIds.ToList(),
                        AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(area.AdditionalDirectionalTraversalMappings),
                        AvailableGameActions = ToCommandActionDtos(area.AvailableActions),
                        EventSubscriptions = area.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
                        TimerDefinitions = area.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
                        BaseObjectIds = mappedAreaBaseObjects
                            .Select(static obj => obj.Id)
                            .Where(static id => id != Guid.Empty)
                            .Distinct()
                            .ToList(),
                        BaseObjects = new List<ProjectGameObjectDto>(),
                        GameObjectIds = mappedAreaObjects
                            .Select(static obj => obj.Id)
                            .Where(static id => id != Guid.Empty)
                            .Distinct()
                            .ToList(),
                        GameObjects = new List<ProjectGameObjectDto>(),
                        RoomIds = area.Rooms.Select(room => room.Id).ToList(),
                        TraversalConnections = BuildTraversalConnectionsForPersistence(area)
                            .Select(ToTraversalConnectionDto)
                            .ToList(),
                        RoomPlacements = area.RoomPlacements.Select(placement => new RoomPlacementDto
                        {
                            RoomId = placement.RoomId,
                            X = placement.X,
                            Y = placement.Y,
                            FloorElevation = placement.FloorElevation == 0 ? null : placement.FloorElevation
                        }).ToList()
                    };

                    pendingWrites.Add((BuildAreaFilePath(projectFilePath, areaDto.Id), SerializeWithLinkedInstancePruning(areaDto)));
                }
            }
        }

        foreach (var objectDto in authoringObjectDtos.Values.OrderBy(static dto => dto.Id))
        {
            pendingWrites.Add((
                BuildAuthoringObjectFilePath(projectFilePath, objectDto.Id, objectDto.ScopeKind),
                SerializeWithLinkedInstancePruning(objectDto)));
        }

        pendingWrites.Add((
            BuildAuthoringIndexFilePath(projectFilePath),
            BuildAuthoringIndexHtml(projectFilePath, project, allRooms, authoringObjectDtos, mappedPhases)));

        foreach (var folderClear in pendingFolderClears)
        {
            Directory.CreateDirectory(folderClear.FolderPath);
            foreach (var existingFile in Directory.EnumerateFiles(folderClear.FolderPath, folderClear.SearchPattern))
            {
                File.Delete(existingFile);
            }
        }

        var legacyPhaseFolderPath = BuildLegacyPhasesFolderPath(projectFilePath);
        if (Directory.Exists(legacyPhaseFolderPath))
        {
            Directory.Delete(legacyPhaseFolderPath, recursive: true);
        }

        foreach (var pendingWrite in pendingWrites)
        {
            File.WriteAllText(pendingWrite.FilePath, pendingWrite.Content);
        }
    }

    public void SaveProjectUiState(string projectFilePath, ProjectUiState uiState)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            throw new InvalidOperationException("Project file path is required.");
        }

        var projectFolder = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            throw new InvalidOperationException("Project file path must include a directory.");
        }

        Directory.CreateDirectory(projectFolder);

        var projectState = new ProjectStateDto
        {
            UiState = ToProjectUiStateDto(uiState)
        };

        File.WriteAllText(BuildProjectStateFilePath(projectFilePath), JsonSerializer.Serialize(projectState, _jsonOptions));
    }

    public string ExportPhaseNarrativeReviewHtml(string projectFilePath, ProjectModel project)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            throw new InvalidOperationException("Project file path is required.");
        }

        var outputPath = BuildPhaseNarrativeReviewFilePath(projectFilePath);
        var html = BuildPhaseNarrativeReviewHtml(project);
        File.WriteAllText(outputPath, html);
        return outputPath;
    }

    public ProjectModel? TryLoadProjectModel(string projectFilePath)
    {
        if (!File.Exists(projectFilePath))
        {
            return null;
        }

        var json = File.ReadAllText(projectFilePath);
        var normalizedRootJson = NormalizeProjectRootDefaultTraversalModeJson(json);
        var dto = DeserializeWithFileContext<ProjectAuthoringRootDto>(projectFilePath, normalizedRootJson, _jsonReadOptions);
        if (dto is not null && !string.IsNullOrWhiteSpace(dto.Name))
        {
            var globalNodeFilePath = BuildProjectGlobalNodeFilePath(projectFilePath);
            if (!File.Exists(globalNodeFilePath))
            {
                return null;
            }

            ProjectGlobalNodeDto? globalNode;
            try
            {
                globalNode = TryLoadSidecar<ProjectGlobalNodeDto>(globalNodeFilePath);
            }
            catch
            {
                return null;
            }

            if (globalNode is null)
            {
                return null;
            }

            ApplyGlobalScopeKind(globalNode);
            var projectState = TryLoadSidecar<ProjectStateDto>(BuildProjectStateFilePath(projectFilePath));
            var resolvedUiState = projectState?.UiState;

            var authoringObjectDtosById = LoadAuthoringGameObjectDtos(projectFilePath)
                .Where(static obj => obj.Id != Guid.Empty)
                .GroupBy(static obj => obj.Id)
                .ToDictionary(static group => group.Key, static group => group.First());

            var roomTemplateDtos = LoadRoomTemplateSidecars(projectFilePath);
            var roomTemplateDtosById = roomTemplateDtos
                .Where(static room => room.Id != Guid.Empty)
                .GroupBy(static room => room.Id)
                .ToDictionary(static group => group.Key, static group => group.First());
            var phaseDtos = LoadPhaseSidecars(projectFilePath);

            var roomMap = new Dictionary<Guid, Room>();
            var roomDtos = LoadRoomSidecars(projectFilePath);
            var defaultRoomCanvasWidth = dto.RoomImageCanvasWidth > 0 ? dto.RoomImageCanvasWidth : 800;
            var defaultRoomCanvasHeight = dto.RoomImageCanvasHeight > 0 ? dto.RoomImageCanvasHeight : 600;
            foreach (var roomDto in roomDtos)
            {
                var room = ToRoomModel(roomDto, authoringObjectDtosById, defaultRoomCanvasWidth, defaultRoomCanvasHeight);
                roomMap[room.Id] = room;
            }

            var planetDtos = LoadScopeSidecarsWithFolderFallback(
                [BuildPlanetsFolderPath(projectFilePath)],
                PlanetFileSuffix,
                _jsonReadOptions,
                static (jsonText, options) => JsonSerializer.Deserialize<PlanetDto>(jsonText, options),
                static dto => dto.Id);
            var countryDtos = LoadScopeSidecarsWithFolderFallback(
                [BuildCountriesFolderPath(projectFilePath)],
                CountryFileSuffix,
                _jsonReadOptions,
                static (jsonText, options) => JsonSerializer.Deserialize<CountryDto>(jsonText, options),
                static dto => dto.Id);
            var areaDtos = LoadScopeSidecarsWithFolderFallback(
                [BuildAreasFolderPath(projectFilePath)],
                AreaFileSuffix,
                _jsonReadOptions,
                static (jsonText, options) => JsonSerializer.Deserialize<AreaDto>(jsonText, options),
                static dto => dto.Id);
            var procedureDtos = LoadScopeSidecarsWithFolderFallback(
                [BuildProceduresFolderPath(projectFilePath)],
                ProcedureFileSuffix,
                _jsonReadOptions,
                static (jsonText, options) => JsonSerializer.Deserialize<ProcedureDefinitionDto>(jsonText, options),
                static dto => dto.Id);

            var countriesById = countryDtos.ToDictionary(country => country.Id, country => country);
            var areasById = areaDtos.ToDictionary(area => area.Id, area => area);

            var loadedGlobalObjects = ResolveScopedObjectDtos(
                    globalNode.GameObjectIds,
                    globalNode.GameObjects,
                    authoringObjectDtosById)
                .Select(ToGameObjectModel)
                .ToList();

            var loadedObjectTemplates = ResolveScopedObjectDtos(
                    globalNode.ObjectTemplateIds,
                    globalNode.ObjectTemplates,
                    authoringObjectDtosById)
                .Select(static template => ApplyTemplateScopeKind(template))
                .Select(ToGameObjectModel)
                .ToList();

            var loadedBaseObjects = ResolveScopedObjectDtos(
                    globalNode.BaseObjectIds,
                    globalNode.BaseObjects,
                    authoringObjectDtosById)
                .Select(static template => ApplyTemplateScopeKind(template))
                .Select(ToGameObjectModel)
                .ToList();

            var roomTemplateFallback = (globalNode.RoomTemplates ?? new List<RoomDto>()).ToList();
            var loadedRoomTemplateDtos = ResolveScopedRoomDtos(
                globalNode.RoomTemplateIds,
                roomTemplateDtosById,
                roomTemplateFallback)
                .Select(static roomTemplate => ApplyRoomTemplateScopeKind(roomTemplate))
                .ToList();
            var loadedRoomTemplates = loadedRoomTemplateDtos
                .Select(roomTemplate => ToRoomModel(roomTemplate, authoringObjectDtosById, defaultRoomCanvasWidth, defaultRoomCanvasHeight))
                .ToList();
                var loadedPhaseBooks = ResolveScopedPhaseDtos(
                    globalNode.PhaseBookIds,
                    globalNode.PhaseBooks,
                    phaseDtos)
                .Select(ToPhaseNodeModel)
                .ToList();
            var unifiedGlobalIgnoredValidationRuleIds = (dto.GlobalIgnoredValidationRuleIds ?? new List<string>())
                .Concat(dto.PlayerIgnoredValidationRuleIds ?? new List<string>())
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            // Legacy inline player payload fields remain in DTO contract for tolerant reads,
            // but migrated projects must source player objects from global node/global objects.

            var loadedProject = new ProjectModel
            {
                Name = dto.Name,
                GameDisplayName = dto.GameDisplayName ?? string.Empty,
                GameSummary = dto.GameSummary ?? string.Empty,
                GamePreviewImages = (dto.GamePreviewImages ?? new List<string>())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim())
                    .ToList(),
                HideEmptyConfiguration = dto.HideEmptyConfiguration ?? false,
                AutoSaveSeconds = dto.AutoSaveSeconds,
                RoomImageCanvasWidth = dto.RoomImageCanvasWidth,
                RoomImageCanvasHeight = dto.RoomImageCanvasHeight,
                RoomDesignerGridCellSize = dto.RoomDesignerGridCellSize,
                StackScaleStepDefault = dto.StackScaleStepDefault ?? ProjectModel.DefaultStackScaleStep,
                MinStackScaleDefault = dto.MinStackScaleDefault ?? ProjectModel.DefaultMinStackScale,
                SimulatorReplayFilePath = dto.SimulatorReplayFilePath,
                SimulatorReplaySpeed = dto.SimulatorReplaySpeed,
                DefaultTraversalMode = dto.DefaultTraversalMode,
                CommandVerbs = (globalNode.AdditionalVerbs ?? new List<string>()).ToList(),
                Directionals = (globalNode.AdditionalDirectionals ?? new List<string>()).ToList(),
                DirectionalTraversalMappings = ToDirectionalTraversalMappings(globalNode.AdditionalDirectionalTraversalMappings),
                IgnoredValidationRuleIds = (dto.ProjectIgnoredValidationRuleIds ?? new List<string>()).ToList(),
                GlobalIgnoredValidationRuleIds = unifiedGlobalIgnoredValidationRuleIds,
                ObjectTemplatesIgnoredValidationRuleIds = (dto.ObjectTemplatesIgnoredValidationRuleIds ?? new List<string>()).ToList(),
                RoomTemplatesIgnoredValidationRuleIds = (dto.RoomTemplatesIgnoredValidationRuleIds ?? new List<string>()).ToList(),
                BaseObjectsIgnoredValidationRuleIds = (dto.BaseObjectsIgnoredValidationRuleIds ?? new List<string>()).ToList(),
                StartingPlanetName = dto.StartingPlanetName,
                PlayerCharacterObjectName = dto.PlayerCharacterObjectName,
                GlobalVariables = (globalNode.GameProperties ?? new List<GamePropertyDefinitionDto>())
                    .Select(ToVariableModel)
                    .ToList(),
                ProcedureIds = (globalNode.ProcedureIds ?? new List<Guid>())
                    .Where(static id => id != Guid.Empty)
                    .Distinct()
                    .ToList(),
                Procedures = procedureDtos.Select(ToProcedureDefinitionModel).ToList(),
                SharedVariables = (globalNode.SharedVariables ?? new List<SharedVariableDefinitionDto>())
                    .Select(ToSharedVariableModel)
                    .ToList(),
                GameObjects = loadedGlobalObjects,
                ObjectTemplates = loadedObjectTemplates,
                RoomTemplates = loadedRoomTemplates,
                BaseObjects = loadedBaseObjects,
                PhaseBooks = loadedPhaseBooks,
                StartingPhasePageId = globalNode.StartingPhasePageId,
                UiState = ToProjectUiStateModel(resolvedUiState),
                Planets = (globalNode.PlanetIds ?? new List<Guid>())
                    .Select(planetId => planetDtos.FirstOrDefault(planet => planet.Id == planetId))
                    .Where(static planet => planet is not null)
                    .Select(planet => BuildPlanetModel(planet!, countriesById, areasById, roomMap, authoringObjectDtosById))
                    .ToList()
            };

            loadedProject.GlobalScope.Name = "Global Objects";
            loadedProject.GlobalScope.IgnoredValidationRuleIds = unifiedGlobalIgnoredValidationRuleIds.ToList();
            loadedProject.GlobalScope.GameProperties = new List<GamePropertyDefinition>();
            var globalScopeActionDtos = (globalNode.AvailableGameActions ?? new List<CommandActionDto>()).ToList();

            loadedProject.GlobalScope.AvailableActions = ToCommandActionModels(globalScopeActionDtos);
            loadedProject.GlobalScope.EventSubscriptions = (globalNode.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>())
                .Select(ToEventSubscriptionModel)
                .ToList();
            loadedProject.GlobalScope.TimerDefinitions = (globalNode.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>())
                .Select(ToTimerDefinitionModel)
                .ToList();
            loadedProject.GlobalScope.SoundEffectLibraryEntries = (globalNode.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList();

            if (globalNode.ProcedureIds is not { Count: > 0 })
            {
                var objectProcedureIds = EnumerateGameObjectsRecursive(loadedProject.GlobalScope.GameObjects)
                    .Concat(EnumerateGameObjectsRecursive(loadedProject.ObjectTemplates))
                    .Concat(EnumerateGameObjectsRecursive(loadedProject.BaseObjects))
                    .Concat(loadedProject.Planets
                        .SelectMany(static planet => planet.Countries)
                        .SelectMany(static country => country.Areas)
                        .SelectMany(static area => area.Rooms)
                        .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects)))
                    .SelectMany(static obj => obj.ProcedureIds ?? new List<Guid>())
                    .Where(static id => id != Guid.Empty)
                    .ToHashSet();

                loadedProject.ProcedureIds = objectProcedureIds.Count == 0
                    ? loadedProject.Procedures
                        .Select(static procedure => procedure.Id)
                        .Where(static id => id != Guid.Empty)
                        .Distinct()
                        .ToList()
                    : loadedProject.Procedures
                        .Select(static procedure => procedure.Id)
                        .Where(id => id != Guid.Empty && !objectProcedureIds.Contains(id))
                        .Distinct()
                        .ToList();
            }

            NormalizeDefinitionCatalogObjectLinkMetadata(loadedProject);
            HydrateLinkedInstanceDefinitionOwnedFieldsFromDefinitions(loadedProject);
            RefreshRoomDerivedOccupancyCaches(loadedProject);
            SharedVariableReconciliationService.ReconcileInPlace(loadedProject);
            return loadedProject;
        }

        var metadata = DeserializeWithFileContext<ProjectMetadataDto>(projectFilePath, json, _jsonReadOptions);
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.Name))
        {
            return null;
        }

        var project = new ProjectModel
        {
            Name = metadata.Name,
            DefaultTraversalMode = AreaAdjacencyMode.EightDirectional,
            CommandVerbs = new List<string>(),
            Directionals = new List<string>(),
            DirectionalTraversalMappings = new List<DirectionalTraversalMapping>(),
            GameObjects = new List<GameObject>
            {
                new()
                {
                    Name = "Player",
                    ProducerNotes = string.Empty,
                    IsInventoriable = true,
                    IsOpenable = false,
                    IsOpenDefaultValue = false,
                    IsLockable = false,
                    IsLockedDefaultValue = false,
                    IsActivatable = false,
                    IsActiveDefaultValue = false,
                    IsHidable = false,
                    IsHiddenDefaultValue = false,
                    Description = string.Empty,
                    Commands = new List<string>(),
                    Variables = new List<GamePropertyDefinition>()
                }
            },
            BaseObjects = new List<GameObject>(),
            UiState = new ProjectUiState(),
            Planets = new List<Planet>()
        };

        project.GlobalScope.Name = "Global Objects";
        project.GlobalScope.GameProperties = new List<GamePropertyDefinition>();
        project.GlobalScope.AvailableActions = new List<CommandAction>();
        return project;
    }

    public ProjectGlobalNodeImportData? TryLoadGlobalNodeForImport(string globalNodeFilePath)
    {
        if (!File.Exists(globalNodeFilePath))
        {
            return null;
        }

        var globalNode = TryLoadSidecar<ProjectGlobalNodeDto>(globalNodeFilePath);
        if (globalNode is null)
        {
            return null;
        }

        ApplyGlobalScopeKind(globalNode);

        var diagnostics = new List<string>();
        var inferredProjectFilePath = TryBuildProjectFilePathFromGlobalNodeFilePath(globalNodeFilePath);
        if (string.IsNullOrWhiteSpace(inferredProjectFilePath))
        {
            diagnostics.Add("Unable to infer source project path from global node file name. Global-node-only hydration was used.");
        }
        else
        {
            AddImportLayoutDiagnostics(inferredProjectFilePath, diagnostics);
        }

        var authoringObjectDtosById = inferredProjectFilePath is null
            ? new Dictionary<Guid, ProjectGameObjectDto>()
            : LoadAuthoringGameObjectDtos(inferredProjectFilePath)
                .Where(static dto => dto.Id != Guid.Empty)
                .GroupBy(static dto => dto.Id)
                .ToDictionary(static group => group.Key, static group => group.First());

        var roomTemplateDtosById = inferredProjectFilePath is null
            ? new Dictionary<Guid, RoomDto>()
            : LoadRoomTemplateSidecars(inferredProjectFilePath)
                .Where(static dto => dto.Id != Guid.Empty)
                .GroupBy(static dto => dto.Id)
                .ToDictionary(static group => group.Key, static group => group.First());

        var resolvedObjectTemplateDtos = ResolveScopedObjectDtos(
                globalNode.ObjectTemplateIds,
                globalNode.ObjectTemplates,
                authoringObjectDtosById)
            .Select(static template => ApplyTemplateScopeKind(template))
            .ToList();

        AddMissingIdDiagnostics(
            globalNode.ObjectTemplateIds,
            resolvedObjectTemplateDtos.Select(static dto => dto.Id),
            "objectTemplateIds",
            diagnostics);

        var resolvedObjectTemplates = resolvedObjectTemplateDtos
            .Select(ToGameObjectModel)
            .ToList();

        var resolvedBaseObjectDtos = ResolveScopedObjectDtos(
                globalNode.BaseObjectIds,
                globalNode.BaseObjects,
                authoringObjectDtosById)
            .Select(static baseObject => ApplyTemplateScopeKind(baseObject))
            .ToList();

        AddMissingIdDiagnostics(
            globalNode.BaseObjectIds,
            resolvedBaseObjectDtos.Select(static dto => dto.Id),
            "baseObjectIds",
            diagnostics);

        var resolvedBaseObjects = resolvedBaseObjectDtos
            .Select(ToGameObjectModel)
            .ToList();

        var resolvedGlobalObjectDtos = ResolveScopedObjectDtos(
                globalNode.GameObjectIds,
                globalNode.GameObjects,
                authoringObjectDtosById)
            .ToList();

        AddMissingIdDiagnostics(
            globalNode.GameObjectIds,
            resolvedGlobalObjectDtos.Select(static dto => dto.Id),
            "gameObjectIds",
            diagnostics);

        var resolvedGlobalObjects = resolvedGlobalObjectDtos
            .Select(ToGameObjectModel)
            .ToList();

        var resolvedRoomTemplateDtos = ResolveScopedRoomDtos(
                globalNode.RoomTemplateIds,
                roomTemplateDtosById,
                globalNode.RoomTemplates)
            .Select(static roomTemplate => ApplyRoomTemplateScopeKind(roomTemplate))
            .ToList();

        AddMissingIdDiagnostics(
            globalNode.RoomTemplateIds,
            resolvedRoomTemplateDtos.Select(static dto => dto.Id),
            "roomTemplateIds",
            diagnostics);

        var resolvedRoomTemplates = resolvedRoomTemplateDtos
            .Select(template => ToRoomModel(template, authoringObjectDtosById, 800, 600))
            .ToList();

        return new ProjectGlobalNodeImportData
        {
            CommandVerbs = (globalNode.AdditionalVerbs ?? new List<string>()).ToList(),
            Directionals = (globalNode.AdditionalDirectionals ?? new List<string>()).ToList(),
            DirectionalTraversalMappings = ToDirectionalTraversalMappings(globalNode.AdditionalDirectionalTraversalMappings),
            GlobalAvailableActions = ToCommandActionModels(globalNode.AvailableGameActions),
            GlobalSoundEffectLibraryEntries = (globalNode.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            GlobalEventSubscriptions = (globalNode.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>())
                .Select(ToEventSubscriptionModel)
                .ToList(),
            GlobalTimerDefinitions = (globalNode.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>())
                .Select(ToTimerDefinitionModel)
                .ToList(),
            ObjectTemplates = resolvedObjectTemplates,
            RoomTemplates = resolvedRoomTemplates,
            BaseObjects = resolvedBaseObjects,
            GlobalObjects = resolvedGlobalObjects,
            Diagnostics = diagnostics
        };
    }

    private static void AddImportLayoutDiagnostics(string projectFilePath, List<string> diagnostics)
    {
        var canonicalFolders = new[]
        {
            BuildRoomsFolderPath(projectFilePath),
            BuildPlanetsFolderPath(projectFilePath),
            BuildCountriesFolderPath(projectFilePath),
            BuildAreasFolderPath(projectFilePath),
            BuildProceduresFolderPath(projectFilePath),
            BuildPhasesFolderPath(projectFilePath),
            BuildAuthoringGameObjectFolderPath(projectFilePath),
            BuildAuthoringTemplatesFolderPath(projectFilePath),
            BuildRoomTemplatesFolderPath(projectFilePath)
        };

        var legacyFolders = new[]
        {
            BuildLegacyRoomsFolderPath(projectFilePath),
            BuildLegacyPlanetsFolderPath(projectFilePath),
            BuildLegacyCountriesFolderPath(projectFilePath),
            BuildLegacyAreasFolderPath(projectFilePath),
            BuildLegacyProceduresFolderPath(projectFilePath),
            BuildLegacyPhasesFolderPath(projectFilePath)
        };

        var hasCanonical = canonicalFolders.Any(Directory.Exists);
        var hasLegacy = legacyFolders.Any(Directory.Exists);

        if (hasCanonical && hasLegacy)
        {
            diagnostics.Add("Mixed donor layout detected: both canonical and legacy scope folders are present. Canonical files are preferred.");
        }
        else if (!hasCanonical && hasLegacy)
        {
            diagnostics.Add("Legacy donor layout detected: legacy scope folders are present.");
        }
    }

    private static void AddMissingIdDiagnostics(
        IEnumerable<Guid>? requestedIds,
        IEnumerable<Guid> resolvedIds,
        string idFieldName,
        List<string> diagnostics)
    {
        if (requestedIds is null)
        {
            return;
        }

        var resolvedSet = resolvedIds
            .Where(static id => id != Guid.Empty)
            .ToHashSet();

        var missingIds = requestedIds
            .Where(static id => id != Guid.Empty)
            .Distinct()
            .Where(id => !resolvedSet.Contains(id))
            .Select(static id => id.ToString("D"))
            .ToList();

        if (missingIds.Count == 0)
        {
            return;
        }

        diagnostics.Add($"Missing referenced ids for {idFieldName}: {string.Join(", ", missingIds)}");
    }

    private static RoomDto ToRoomTemplateDto(Room room)
    {
        var roomObjects = room.GameObjects;
        return new RoomDto
        {
            Id = room.Id,
            ScopeKind = ScopeNodeKind.RoomTemplates,
            Name = room.Name,
            NameInGame = room.NameInGame,
            HideEmptyConfiguration = room.HideEmptyConfiguration,
            Description = room.Description,
            ProducerNotes = room.ProducerNotes,
            RoomImageCanvasWidth = room.RoomImageCanvasWidth,
            RoomImageCanvasHeight = room.RoomImageCanvasHeight,
            RoomDisplayMode = room.RoomDisplayMode,
            ValidationErrors = room.ValidationErrors.ToList(),
            TraversalModeOverride = room.TraversalModeOverride?.ToString(),
            AvailableGameActions = ToCommandActionDtos(room.AvailableActions),
            GameProperties = room.Variables.Select(ToVariableDto).ToList(),
            AdditionalVerbs = room.AdditionalVerbs.ToList(),
            AdditionalDirectionals = room.AdditionalDirectionals.ToList(),
            IgnoredValidationRuleIds = room.IgnoredValidationRuleIds.ToList(),
            SoundEffectLibraryEntries = room.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(room.AdditionalDirectionalTraversalMappings),
            TimerDefinitions = room.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
            GameObjects = roomObjects
                .Select((obj, index) => ToProjectGameObjectDto(
                    obj,
                    null,
                    ComputeRoomObjectRenderZOrder(index, roomObjects.Count),
                    ScopeNodeKind.Templates))
                .ToList(),
            Images = room.Images.Select(i => new RoomImageDto
            {
                Slot = i.Slot,
                OverlayRenderOrder = i.OverlayRenderOrder,
                OverlayOffsetX = i.OverlayOffsetX,
                OverlayOffsetY = i.OverlayOffsetY,
                OverlayRotationDegrees = i.OverlayRotationDegrees,
                FullImagePath = i.Image.FullImagePath,
                GrayMapImagePath = i.Image.GrayMapImagePath,
                NormalMapImagePath = i.Image.NormalMapImagePath
            }).ToList()
        };
    }

    public string ExportCleanProjectV1(string projectFilePath, ProjectModel project)
    {
        ValidateTraversalOrThrow(project, "clean export");
        RefreshRoomDerivedOccupancyCaches(project);

        var projectFolder = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            throw new InvalidOperationException("Project file path must include a directory.");
        }

        Directory.CreateDirectory(projectFolder);

        var cleanExportRootFolderPath = BuildCleanExportRootFolderPath(projectFilePath);
        Directory.CreateDirectory(cleanExportRootFolderPath);
        RemoveLegacySbePrefixedRuntimeArtifacts(projectFilePath);

        var cleanSharedImagesFolderPath = BuildCleanSharedImagesFolderPath(projectFilePath);
        if (Directory.Exists(cleanSharedImagesFolderPath))
        {
            Directory.Delete(cleanSharedImagesFolderPath, recursive: true);
        }

        Directory.CreateDirectory(cleanSharedImagesFolderPath);
        var imageAssetStager = new CleanExportImageAssetStager(cleanSharedImagesFolderPath, projectFolder);
        var cleanSharedSoundsFolderPath = BuildCleanSharedSoundsFolderPath(projectFilePath);
        if (Directory.Exists(cleanSharedSoundsFolderPath))
        {
            Directory.Delete(cleanSharedSoundsFolderPath, recursive: true);
        }

        Directory.CreateDirectory(cleanSharedSoundsFolderPath);
        var soundAssetStager = new CleanExportSoundAssetStager(cleanSharedSoundsFolderPath, projectFolder);
        var cleanExportObjectLookup = BuildCleanExportObjectLookup(project);

        GameObject? ResolveCleanExportDefinition(Guid linkedBaseObjectId)
        {
            return cleanExportObjectLookup.TryGetValue(linkedBaseObjectId, out var definition)
                ? definition
                : null;
        }

        var cleanProcedureDtos = project.Procedures
            .OrderBy(procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(procedure => procedure.Id)
            .Select(ToCleanProcedureDefinitionDto)
            .ToList();

        var runtimeGlobalVerbs = BuildRuntimeGlobalVerbVocabulary(project);

        static bool LooksLikePathToken(string value)
        {
            return Path.IsPathRooted(value)
                || value.Contains('/')
                || value.Contains('\\');
        }

        RuntimeSoundEffectLibraryEntryDto ToCleanSoundEntryForScope(
            SoundEffectLibraryEntry entry,
            string scopeReference)
        {
            var normalizedAssetRef = entry.AssetRef?.Trim() ?? string.Empty;
            var shouldStage = !string.IsNullOrWhiteSpace(normalizedAssetRef)
                && (File.Exists(normalizedAssetRef) || LooksLikePathToken(normalizedAssetRef));
            var reference = $"{scopeReference}:sound:{(entry.SoundEffectId == Guid.Empty ? entry.SoundEffectKey : entry.SoundEffectId.ToString("D"))}";
            return ToCleanSoundEffectLibraryEntryDto(
                entry,
                shouldStage ? soundAssetStager.ResolveAndStage : null,
                reference);
        }

        var projectDto = new RuntimeProjectDto
        {
            SchemaVersion = "1.0",
            Name = project.Name,
            GameDisplayName = string.IsNullOrWhiteSpace(project.GameDisplayName)
                ? null
                : project.GameDisplayName.Trim(),
            GameSummary = string.IsNullOrWhiteSpace(project.GameSummary)
                ? null
                : project.GameSummary.Trim(),
            GamePreviewImages = project.GamePreviewImages
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((value, index) => imageAssetStager.ResolveAndStage(value, $"project:previewImage:{index}", CleanPreviewImagesFolderName))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList(),
            AutoSaveSeconds = project.AutoSaveSeconds,
            RoomImageCanvasWidth = project.RoomImageCanvasWidth,
            RoomImageCanvasHeight = project.RoomImageCanvasHeight,
            RoomDesignerGridCellSize = project.RoomDesignerGridCellSize,
            AdditionalVerbs = runtimeGlobalVerbs,
            AdditionalDirectionals = project.Directionals.ToList(),
            AdditionalDirectionalTraversalMappings = NullIfEmpty(ToRuntimeDirectionalTraversalMappings(project.DirectionalTraversalMappings)),
            StartingPlanetName = project.StartingPlanetName,
            GameProperties = project.GlobalVariables.Select(ToCleanGamePropertyDto).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            SoundEffectLibraryEntries = NullIfEmpty(project.GlobalScope.SoundEffectLibraryEntries
                .Select(entry => ToCleanSoundEntryForScope(entry, "project:global"))
                .ToList()),
            ProcedureIds = NullIfEmpty(cleanProcedureDtos
                .Where(static procedure => procedure.Id != Guid.Empty)
                .Select(static procedure => procedure.Id)
                .Distinct()
                .ToList()),
            EventSubscriptions = NullIfEmpty(project.GlobalScope.EventSubscriptions
                .Select(ToRuntimeEventSubscriptionDto)
                .ToList()),
            TimerDefinitions = NullIfEmpty(project.GlobalScope.TimerDefinitions.Select(ToRuntimeTimerDefinitionDto).ToList()),
            AvailableGameActions = NullIfEmpty(ToCleanCommandActionDtos(project.GlobalScope.AvailableActions)
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Id)
                .ToList())
        };

        var mappedProjectObjects = project.GlobalScope.GameObjects
            .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
            .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
            .ToList();
        projectDto.SetAttachedGlobalObjects(mappedProjectObjects);

        var mappedProjectBaseObjects = NullIfEmpty(project.BaseObjects
            .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
            .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
            .ToList()) ?? new List<RuntimeGameObjectDto>();
        projectDto.BaseObjects = mappedProjectBaseObjects;

        var mappedProjectPlanets = project.Planets
            .OrderBy(planet => planet.Name, StringComparer.OrdinalIgnoreCase)
            .Select(planet =>
            {
                var mappedPlanet = new RuntimePlanetDto
                {
                    ScopeNodeId = planet.Id == Guid.Empty ? null : planet.Id,
                    Name = planet.Name,
                    StartingCountryName = planet.StartingCountryName,
                    GameProperties = planet.Variables.Select(ToCleanGamePropertyDto).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    SoundEffectLibraryEntries = NullIfEmpty(planet.SoundEffectLibraryEntries
                        .Select(entry => ToCleanSoundEntryForScope(entry, $"planet:{planet.Id:D}"))
                        .ToList()),
                    AdditionalVerbs = planet.AdditionalVerbs.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
                    AdditionalDirectionals = planet.AdditionalDirectionals.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
                    AdditionalDirectionalTraversalMappings = NullIfEmpty(ToRuntimeDirectionalTraversalMappings(planet.AdditionalDirectionalTraversalMappings)),
                    EventSubscriptions = NullIfEmpty(planet.EventSubscriptions.Select(ToRuntimeEventSubscriptionDto).ToList()),
                    TimerDefinitions = NullIfEmpty(planet.TimerDefinitions.Select(ToRuntimeTimerDefinitionDto).ToList()),
                    AvailableGameActions = ToCleanCommandActionDtos(planet.AvailableActions).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList()
                };

                var mappedPlanetBaseObjects = NullIfEmpty(planet.BaseObjects
                    .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
                    .ToList()) ?? new List<RuntimeGameObjectDto>();
                mappedPlanet.BaseObjects = mappedPlanetBaseObjects;

                var mappedPlanetObjects = NullIfEmpty(planet.GameObjects
                    .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
                    .ToList()) ?? new List<RuntimeGameObjectDto>();
                mappedPlanet.SetAttachedGameObjects(mappedPlanetObjects);

                var mappedPlanetCountries = planet.Countries
                    .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(country =>
                    {
                        var mappedCountryAreas = country.Areas
                            .OrderBy(area => area.Name, StringComparer.OrdinalIgnoreCase)
                            .Select(area =>
                            {
                                var mappedArea = new RuntimeAreaDto
                                {
                                    ScopeNodeId = area.Id == Guid.Empty ? null : area.Id,
                                    Name = area.Name,
                                    AdjacencyMode = area.AdjacencyMode,
                                    StartingRoomId = area.StartingRoomId,
                                    GameProperties = area.Variables.Select(ToCleanGamePropertyDto).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                                    SoundEffectLibraryEntries = NullIfEmpty(area.SoundEffectLibraryEntries
                                        .Select(entry => ToCleanSoundEntryForScope(entry, $"area:{area.Id:D}"))
                                        .ToList()),
                                    AdditionalVerbs = area.AdditionalVerbs.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
                                    AdditionalDirectionals = area.AdditionalDirectionals.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
                                    AdditionalDirectionalTraversalMappings = NullIfEmpty(ToRuntimeDirectionalTraversalMappings(area.AdditionalDirectionalTraversalMappings)),
                                    EventSubscriptions = NullIfEmpty(area.EventSubscriptions.Select(ToRuntimeEventSubscriptionDto).ToList()),
                                    TimerDefinitions = NullIfEmpty(area.TimerDefinitions.Select(ToRuntimeTimerDefinitionDto).ToList()),
                                    AvailableGameActions = ToCleanCommandActionDtos(area.AvailableActions).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList(),
                                    RoomIds = area.Rooms.Select(room => room.Id).OrderBy(id => id).ToList()
                                };

                                var mappedAreaBaseObjects = NullIfEmpty(area.BaseObjects
                                    .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
                                    .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
                                    .ToList()) ?? new List<RuntimeGameObjectDto>();
                                mappedArea.BaseObjects = mappedAreaBaseObjects;

                                var mappedAreaObjects = NullIfEmpty(area.GameObjects
                                    .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
                                    .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
                                    .ToList()) ?? new List<RuntimeGameObjectDto>();
                                mappedArea.SetAttachedGameObjects(mappedAreaObjects);

                                return mappedArea;
                            })
                            .ToList();

                        var mappedCountry = new RuntimeCountryDto
                        {
                            ScopeNodeId = country.Id == Guid.Empty ? null : country.Id,
                            Name = country.Name,
                            StartingAreaName = country.StartingAreaName,
                            GameProperties = country.Variables.Select(ToCleanGamePropertyDto).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                            SoundEffectLibraryEntries = NullIfEmpty(country.SoundEffectLibraryEntries
                                .Select(entry => ToCleanSoundEntryForScope(entry, $"country:{country.Id:D}"))
                                .ToList()),
                            AdditionalVerbs = country.AdditionalVerbs.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
                            AdditionalDirectionals = country.AdditionalDirectionals.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
                            AdditionalDirectionalTraversalMappings = NullIfEmpty(ToRuntimeDirectionalTraversalMappings(country.AdditionalDirectionalTraversalMappings)),
                            EventSubscriptions = NullIfEmpty(country.EventSubscriptions.Select(ToRuntimeEventSubscriptionDto).ToList()),
                            TimerDefinitions = NullIfEmpty(country.TimerDefinitions.Select(ToRuntimeTimerDefinitionDto).ToList()),
                            AvailableGameActions = ToCleanCommandActionDtos(country.AvailableActions).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList()
                        };
                        mappedCountry.SetAttachedAreas(mappedCountryAreas);

                        var mappedCountryBaseObjects = NullIfEmpty(country.BaseObjects
                            .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
                            .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
                            .ToList()) ?? new List<RuntimeGameObjectDto>();
                        mappedCountry.BaseObjects = mappedCountryBaseObjects;

                        var mappedCountryObjects = NullIfEmpty(country.GameObjects
                            .OrderBy(obj => obj.Name, StringComparer.OrdinalIgnoreCase)
                            .Select(obj => ToCleanGameObjectDto(obj, null, imageAssetStager.ResolveAndStage, soundAssetStager.ResolveAndStage, null, ResolveCleanExportDefinition))
                            .ToList()) ?? new List<RuntimeGameObjectDto>();
                        mappedCountry.SetAttachedGameObjects(mappedCountryObjects);

                        return mappedCountry;
                    })
                    .ToList();
                mappedPlanet.SetAttachedCountries(mappedPlanetCountries);

                return mappedPlanet;
            })
            .ToList();
        projectDto.SetAttachedPlanets(mappedProjectPlanets);

        var allRooms = project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .SelectMany(area => area.Rooms)
            .GroupBy(room => room.Id)
            .Select(group => group.First())
            .OrderBy(room => room.Id)
            .ToList();

        var projectPlanetIds = NullIfEmpty(mappedProjectPlanets
            .Select(static planet => planet.ScopeNodeId)
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .OrderBy(static id => id)
            .ToList());
        var projectGameObjectIds = NullIfEmpty(mappedProjectObjects
            .Select(static obj => obj.ScopeNodeId)
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .OrderBy(static id => id)
            .ToList());
        var projectBaseObjectIds = NullIfEmpty(mappedProjectBaseObjects
            .Select(static obj => obj.ScopeNodeId)
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .OrderBy(static id => id)
            .ToList());
        var mappedRuntimePhases = FlattenPhaseNodes(project.PhaseBooks)
            .Select(ToCleanRuntimePhaseNodeDto)
            .Where(static node => node.ScopeNodeId.HasValue && node.ScopeNodeId.Value != Guid.Empty)
            .OrderBy(static node => node.ScopeNodeId!.Value)
            .ToList();
        var projectPhaseBookIds = NullIfEmpty(project.PhaseBooks
            .Select(static phase => phase.Id)
            .Where(static id => id != Guid.Empty)
            .Distinct()
            .OrderBy(static id => id)
            .ToList());
        var startingPhasePageId = project.StartingPhasePageId == Guid.Empty
            ? null
            : project.StartingPhasePageId;

        projectDto.PlanetIds = projectPlanetIds ?? new List<Guid>();
        projectDto.SetAttachedPlanets(Array.Empty<RuntimePlanetDto>());
        projectDto.PhaseBookIds = projectPhaseBookIds;
        projectDto.StartingPhasePageId = startingPhasePageId;
        projectDto.SetAttachedPhaseBooks(Array.Empty<RuntimePhaseNodeDto>());
        projectDto.GameObjectIds = projectGameObjectIds;
        projectDto.SetAttachedGlobalObjects(Array.Empty<RuntimeGameObjectDto>());
        projectDto.BaseObjectIds = projectBaseObjectIds;
        projectDto.BaseObjects = Array.Empty<RuntimeGameObjectDto>();

        var cleanProjectFilePath = BuildCleanProjectFilePath(projectFilePath);
        File.WriteAllText(cleanProjectFilePath, SerializeWithLinkedInstancePruning(projectDto));

        var cleanNavigationFilePath = BuildCleanNavigationFilePath(projectFilePath);
        if (File.Exists(cleanNavigationFilePath))
        {
            File.Delete(cleanNavigationFilePath);
        }

        var legacyCleanRoomsFolderPath = BuildCleanRoomsFolderPath(projectFilePath);
        if (Directory.Exists(legacyCleanRoomsFolderPath))
        {
            Directory.Delete(legacyCleanRoomsFolderPath, recursive: true);
        }

        var scopeKindRoomFolderPath = BuildScopeKindRoomFolderPath(projectFilePath);
        Directory.CreateDirectory(scopeKindRoomFolderPath);
        foreach (var existing in Directory.EnumerateFiles(scopeKindRoomFolderPath, $"*{ScopeNodeFileSuffix}"))
        {
            File.Delete(existing);
        }

        var scopeKindBookFolderPath = BuildScopeKindBookFolderPath(projectFilePath);
        Directory.CreateDirectory(scopeKindBookFolderPath);
        foreach (var existing in Directory.EnumerateFiles(scopeKindBookFolderPath, $"*{ScopeNodeFileSuffix}"))
        {
            File.Delete(existing);
        }

        var scopeKindPlanetFolderPath = BuildScopeKindPlanetFolderPath(projectFilePath);
        Directory.CreateDirectory(scopeKindPlanetFolderPath);
        foreach (var existing in Directory.EnumerateFiles(scopeKindPlanetFolderPath, $"*{ScopeNodeFileSuffix}"))
        {
            File.Delete(existing);
        }

        var scopeKindCountryFolderPath = BuildScopeKindCountryFolderPath(projectFilePath);
        Directory.CreateDirectory(scopeKindCountryFolderPath);
        foreach (var existing in Directory.EnumerateFiles(scopeKindCountryFolderPath, $"*{ScopeNodeFileSuffix}"))
        {
            File.Delete(existing);
        }

        var scopeKindAreaFolderPath = BuildScopeKindAreaFolderPath(projectFilePath);
        Directory.CreateDirectory(scopeKindAreaFolderPath);
        foreach (var existing in Directory.EnumerateFiles(scopeKindAreaFolderPath, $"*{ScopeNodeFileSuffix}"))
        {
            File.Delete(existing);
        }

        var scopeKindGameObjectFolderPath = BuildScopeKindGameObjectFolderPath(projectFilePath);
        Directory.CreateDirectory(scopeKindGameObjectFolderPath);
        foreach (var existing in Directory.EnumerateFiles(scopeKindGameObjectFolderPath, $"*{ScopeNodeFileSuffix}"))
        {
            File.Delete(existing);
        }

        var cleanProcedureFolderPath = BuildCleanProcedureFolderPath(projectFilePath);
        Directory.CreateDirectory(cleanProcedureFolderPath);
        foreach (var existing in Directory.EnumerateFiles(cleanProcedureFolderPath, $"*{CleanProcedureFileSuffix}"))
        {
            File.Delete(existing);
        }

        var allAreas = mappedProjectPlanets
            .SelectMany(planet => planet.GetAttachedCountries())
            .SelectMany(country => country.GetAttachedAreas())
            .Where(area => area.ScopeNodeId.HasValue)
            .OrderBy(area => area.ScopeNodeId!.Value)
            .ToList();

        var areaPayloadByScopeNodeId = project.Planets
            .OrderBy(planet => planet.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(planet => planet.Countries
                .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
                .SelectMany(country => country.Areas
                    .Where(area => area.Id != Guid.Empty)
                    .OrderBy(area => area.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(area => new
                    {
                        AreaId = area.Id,
                        Links = BuildRuntimeRoomLinks(area.TraversalConnections)
                            .OrderBy(link => link.FromRoomId)
                            .ThenBy(link => link.ToRoomId)
                            .ThenBy(link => link.Direction.ToString(), StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        RoomPlacements = area.RoomPlacements
                            .OrderBy(placement => placement.RoomId)
                            .Select(placement => new RuntimeRoomPlacementDto
                            {
                                RoomId = placement.RoomId,
                                X = placement.X,
                                Y = placement.Y,
                                FloorElevation = placement.FloorElevation == 0 ? null : placement.FloorElevation
                            })
                            .ToList()
                        })))
                    .ToDictionary(entry => entry.AreaId, entry => (entry.Links, entry.RoomPlacements));

        var allCountries = mappedProjectPlanets
            .SelectMany(planet => planet.GetAttachedCountries())
            .Where(country => country.ScopeNodeId.HasValue)
            .OrderBy(country => country.ScopeNodeId!.Value)
            .ToList();

        var allPlanets = mappedProjectPlanets
            .Where(planet => planet.ScopeNodeId.HasValue)
            .OrderBy(planet => planet.ScopeNodeId!.Value)
            .ToList();

        foreach (var mappedObject in mappedProjectObjects
            .Where(static obj => obj.ScopeNodeId.HasValue)
            .OrderBy(static obj => obj.ScopeNodeId!.Value))
        {
            File.WriteAllText(
                BuildScopeKindGameObjectFilePath(projectFilePath, mappedObject.ScopeNodeId!.Value),
                SerializeWithLinkedInstancePruning(mappedObject));
        }

        foreach (var mappedBaseObject in mappedProjectBaseObjects
            .Where(static obj => obj.ScopeNodeId.HasValue)
            .OrderBy(static obj => obj.ScopeNodeId!.Value))
        {
            File.WriteAllText(
                BuildScopeKindGameObjectFilePath(projectFilePath, mappedBaseObject.ScopeNodeId!.Value),
                SerializeWithLinkedInstancePruning(mappedBaseObject));
        }

        foreach (var procedure in cleanProcedureDtos
            .Where(static procedure => procedure.Id != Guid.Empty)
            .OrderBy(static procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static procedure => procedure.Id))
        {
            File.WriteAllText(
                BuildCleanProcedureFilePath(projectFilePath, procedure.Id),
                JsonSerializer.Serialize(procedure, _jsonOptions));
        }

            foreach (var phase in mappedRuntimePhases)
            {
                File.WriteAllText(
                BuildScopeKindBookFilePath(projectFilePath, phase.ScopeNodeId!.Value),
                SerializeWithLinkedInstancePruning(phase));
            }

        foreach (var planet in allPlanets)
        {
            var scopeKindPlanet = planet;
            var mappedPlanetCountries = scopeKindPlanet.GetAttachedCountries().ToList();
            var mappedPlanetObjects = scopeKindPlanet.GetAttachedGameObjects().ToList();
            var mappedPlanetBaseObjects = (scopeKindPlanet.BaseObjects ?? Array.Empty<IRuntimeScopeNode>()).OfType<RuntimeGameObjectDto>().ToList();
            var planetCountryIds = NullIfEmpty(mappedPlanetCountries
                .Select(static country => country.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());
            var planetGameObjectIds = NullIfEmpty(mappedPlanetObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());
            var planetBaseObjectIds = NullIfEmpty(mappedPlanetBaseObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());

            scopeKindPlanet.CountryIds = planetCountryIds is null ? new List<Guid>() : planetCountryIds;
            scopeKindPlanet.SetAttachedCountries(Array.Empty<RuntimeCountryDto>());
            scopeKindPlanet.GameObjectIds = planetGameObjectIds;
            scopeKindPlanet.SetAttachedGameObjects(Array.Empty<RuntimeGameObjectDto>());
            scopeKindPlanet.BaseObjectIds = planetBaseObjectIds;
            scopeKindPlanet.BaseObjects = Array.Empty<RuntimeGameObjectDto>();

            File.WriteAllText(
                BuildScopeKindPlanetFilePath(projectFilePath, scopeKindPlanet.ScopeNodeId!.Value),
                SerializeWithLinkedInstancePruning(scopeKindPlanet));

            foreach (var mappedObject in mappedPlanetObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedObject));
            }

            foreach (var mappedBaseObject in mappedPlanetBaseObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedBaseObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedBaseObject));
            }
        }

        foreach (var country in allCountries)
        {
            var scopeKindCountry = country;
            var mappedCountryAreas = scopeKindCountry.GetAttachedAreas().ToList();
            var mappedCountryObjects = scopeKindCountry.GetAttachedGameObjects().ToList();
            var mappedCountryBaseObjects = (scopeKindCountry.BaseObjects ?? Array.Empty<IRuntimeScopeNode>()).OfType<RuntimeGameObjectDto>().ToList();
            var countryAreaIds = NullIfEmpty(mappedCountryAreas
                .Select(static area => area.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());
            var countryGameObjectIds = NullIfEmpty(mappedCountryObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());
            var countryBaseObjectIds = NullIfEmpty(mappedCountryBaseObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());

            scopeKindCountry.AreaIds = countryAreaIds is null ? new List<Guid>() : countryAreaIds;
            scopeKindCountry.SetAttachedAreas(Array.Empty<RuntimeAreaDto>());
            scopeKindCountry.GameObjectIds = countryGameObjectIds;
            scopeKindCountry.SetAttachedGameObjects(Array.Empty<RuntimeGameObjectDto>());
            scopeKindCountry.BaseObjectIds = countryBaseObjectIds;
            scopeKindCountry.BaseObjects = Array.Empty<RuntimeGameObjectDto>();

            File.WriteAllText(
                BuildScopeKindCountryFilePath(projectFilePath, scopeKindCountry.ScopeNodeId!.Value),
                SerializeWithLinkedInstancePruning(scopeKindCountry));

            foreach (var mappedObject in mappedCountryObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedObject));
            }

            foreach (var mappedBaseObject in mappedCountryBaseObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedBaseObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedBaseObject));
            }
        }

        foreach (var area in allAreas)
        {
            var scopeKindArea = area;
            var mappedAreaObjects = scopeKindArea.GetAttachedGameObjects().ToList();
            var mappedAreaBaseObjects = (scopeKindArea.BaseObjects ?? Array.Empty<IRuntimeScopeNode>()).OfType<RuntimeGameObjectDto>().ToList();
            var areaGameObjectIds = NullIfEmpty(mappedAreaObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());
            var areaBaseObjectIds = NullIfEmpty(mappedAreaBaseObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());

            scopeKindArea.GameObjectIds = areaGameObjectIds;
            scopeKindArea.SetAttachedGameObjects(Array.Empty<RuntimeGameObjectDto>());
            scopeKindArea.BaseObjectIds = areaBaseObjectIds;
            scopeKindArea.BaseObjects = Array.Empty<RuntimeGameObjectDto>();

            if (areaPayloadByScopeNodeId.TryGetValue(scopeKindArea.ScopeNodeId!.Value, out var areaPayload))
            {
                scopeKindArea.Links = areaPayload.Links;
                scopeKindArea.RoomPlacements = areaPayload.RoomPlacements;
            }

            File.WriteAllText(
                BuildScopeKindAreaFilePath(projectFilePath, scopeKindArea.ScopeNodeId!.Value),
                SerializeWithLinkedInstancePruning(scopeKindArea));

            foreach (var mappedObject in mappedAreaObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedObject));
            }

            foreach (var mappedBaseObject in mappedAreaBaseObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedBaseObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedBaseObject));
            }
        }

        foreach (var room in allRooms)
        {
            var roomObjects = room.GameObjects;
            var mappedRoomObjects = roomObjects
                .Select((obj, index) => ToCleanGameObjectDto(
                    obj,
                    null,
                    imageAssetStager.ResolveAndStage,
                    soundAssetStager.ResolveAndStage,
                    ComputeRoomObjectRenderZOrder(index, roomObjects.Count),
                    ResolveCleanExportDefinition))
                .ToList();

            var roomDto = new RuntimeRoomDto
            {
                ScopeNodeId = room.Id,
                Name = room.Name,
                NameInGame = room.NameInGame,
                Description = room.Description,
                RoomImageCanvasWidth = ResolveEffectiveRoomCanvasWidth(project, room),
                RoomImageCanvasHeight = ResolveEffectiveRoomCanvasHeight(project, room),
                RoomDisplayMode = ToRuntimeRoomImageDisplayMode(room.RoomDisplayMode),
                AvailableGameActions = ToCleanCommandActionDtos(room.AvailableActions).OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase).ThenBy(action => action.Id).ToList(),
                GameProperties = NullIfEmpty(room.Variables.Select(ToCleanGamePropertyDto).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList()),
                AdditionalVerbs = NullIfEmpty(room.AdditionalVerbs.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList()),
                AdditionalDirectionals = NullIfEmpty(room.AdditionalDirectionals.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList()),
                AdditionalDirectionalTraversalMappings = NullIfEmpty(ToRuntimeDirectionalTraversalMappings(room.AdditionalDirectionalTraversalMappings)),
                EventSubscriptions = NullIfEmpty(room.EventSubscriptions.Select(ToRuntimeEventSubscriptionDto).ToList()),
                TimerDefinitions = NullIfEmpty(room.TimerDefinitions.Select(ToRuntimeTimerDefinitionDto).ToList()),
                SoundEffectLibraryEntries = NullIfEmpty(room.SoundEffectLibraryEntries
                    .Select(entry => ToCleanSoundEntryForScope(entry, $"room:{room.Id:D}"))
                    .ToList()),
                Images = NullIfEmpty(room.Images
                    .OrderBy(image => image.Slot.ToString(), StringComparer.OrdinalIgnoreCase)
                    .Select(image => new RuntimeRoomImageDto
                    {
                        Slot = image.Slot,
                        OverlayRenderOrder = image.OverlayRenderOrder,
                        ImagePathSemantics = "runtimeExportRelative",
                        OverlayOffsetX = image.OverlayOffsetX,
                        OverlayOffsetY = image.OverlayOffsetY,
                        OverlayRotationDegrees = image.OverlayRotationDegrees,
                        FullImagePath = imageAssetStager.ResolveAndStage(image.Image.FullImagePath, $"room:{room.Id:N}:slot:{image.Slot}:full"),
                        GrayMapImagePath = imageAssetStager.ResolveAndStage(image.Image.GrayMapImagePath, $"room:{room.Id:N}:slot:{image.Slot}:gray"),
                        NormalMapImagePath = imageAssetStager.ResolveAndStage(image.Image.NormalMapImagePath, $"room:{room.Id:N}:slot:{image.Slot}:normal")
                    })
                    .ToList())
            };

            // Scope-kind room files persist id references for room-owned objects.
            roomDto.GameObjectIds = NullIfEmpty(mappedRoomObjects
                .Select(static obj => obj.ScopeNodeId)
                .Where(static id => id.HasValue)
                .Select(static id => id!.Value)
                .OrderBy(static id => id)
                .ToList());
            var serializedScopeKindRoom = SerializeWithLinkedInstancePruning(roomDto);
            File.WriteAllText(BuildScopeKindRoomFilePath(projectFilePath, room.Id), serializedScopeKindRoom);

            foreach (var mappedObject in mappedRoomObjects
                .Where(static obj => obj.ScopeNodeId.HasValue)
                .OrderBy(static obj => obj.ScopeNodeId!.Value))
            {
                File.WriteAllText(
                    BuildScopeKindGameObjectFilePath(projectFilePath, mappedObject.ScopeNodeId!.Value),
                    SerializeWithLinkedInstancePruning(mappedObject));
            }
        }

            StageTemplateImageAssets(project, cleanExportObjectLookup, imageAssetStager);

        StagePresentationCueCatalog(projectFilePath);
        imageAssetStager.WriteManifest(BuildCleanAssetsManifestFilePath(projectFilePath), _jsonOptions);
        soundAssetStager.MergeIntoManifest(BuildCleanAssetsManifestFilePath(projectFilePath), _jsonOptions);
        WriteRuntimeIndexFile(projectFilePath, project, allRooms);

        return cleanProjectFilePath;
    }

    private static void StageTemplateImageAssets(
        ProjectModel project,
        IReadOnlyDictionary<Guid, GameObject> objectLookup,
        CleanExportImageAssetStager imageAssetStager)
    {
        foreach (var template in EnumerateGameObjectsRecursive(project.ObjectTemplates))
        {
            StageGameObjectImageVariants(template, objectLookup, imageAssetStager, $"templateObject:{template.ObjectId:N}");
        }

        foreach (var templateRoom in project.RoomTemplates)
        {
            foreach (var image in templateRoom.Images)
            {
                imageAssetStager.ResolveAndStage(image.Image.FullImagePath, $"roomTemplate:{templateRoom.Id:N}:slot:{image.Slot}:full");
                imageAssetStager.ResolveAndStage(image.Image.GrayMapImagePath, $"roomTemplate:{templateRoom.Id:N}:slot:{image.Slot}:gray");
                imageAssetStager.ResolveAndStage(image.Image.NormalMapImagePath, $"roomTemplate:{templateRoom.Id:N}:slot:{image.Slot}:normal");
            }

            foreach (var templateObject in EnumerateGameObjectsRecursive(templateRoom.GameObjects))
            {
                StageGameObjectImageVariants(templateObject, objectLookup, imageAssetStager, $"roomTemplateObject:{templateObject.ObjectId:N}");
            }
        }
    }

    private static void StageGameObjectImageVariants(
        GameObject gameObject,
        IReadOnlyDictionary<Guid, GameObject> objectLookup,
        CleanExportImageAssetStager imageAssetStager,
        string referencePrefix)
    {
        var source = gameObject;
        if (gameObject.LinkedBaseObjectId.HasValue
            && objectLookup.TryGetValue(gameObject.LinkedBaseObjectId.Value, out var definition)
            && !ReferenceEquals(definition, gameObject))
        {
            source = definition;
        }

        foreach (var variant in source.ImageVariants.Where(static variant => !string.IsNullOrWhiteSpace(variant.VariantName)))
        {
            var variantName = variant.VariantName.Trim();
            imageAssetStager.ResolveAndStage(variant.FullImagePath, $"{referencePrefix}:variant:{variantName}:full");
        }
    }

    private static void WriteRuntimeIndexFile(string projectFilePath, ProjectModel project, IReadOnlyList<Room> allRooms)
    {
        var indexPath = BuildCleanRuntimeIndexFilePath(projectFilePath);
        var html = BuildRuntimeIndexHtml(projectFilePath, project, allRooms);
        File.WriteAllText(indexPath, html);
    }

    private static void WriteAuthoringIndexFile(
        string projectFilePath,
        ProjectModel project,
        IReadOnlyList<Room> allRooms,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById,
        IReadOnlyList<PhaseNodeDto>? phaseSidecarDtos = null)
    {
        var indexPath = BuildAuthoringIndexFilePath(projectFilePath);
        var html = BuildAuthoringIndexHtml(projectFilePath, project, allRooms, objectDtosById, phaseSidecarDtos);
        File.WriteAllText(indexPath, html);
    }

    private static string BuildAuthoringIndexHtml(
        string projectFilePath,
        ProjectModel project,
        IReadOnlyList<Room> allRooms,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById,
        IReadOnlyList<PhaseNodeDto>? phaseSidecarDtos = null)
    {
        var projectFolderPath = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var rows = new List<(string ScopePath, string ArtifactKind, string ScopeId, string RelativeFilePath)>();
        var referencedObjectIds = new HashSet<Guid>();

        static ScopeNodeKind ResolveEffectiveObjectScopeKind(GameObject gameObject, ScopeNodeKind fallback)
        {
            return gameObject.DesignerPersistenceScopeKind ?? fallback;
        }

        static string ResolveArtifactKind(ScopeNodeKind scopeKind)
        {
            return scopeKind.ToString();
        }

        void AddObjectRows(string scopePathPrefix, IEnumerable<GameObject> objects, ScopeNodeKind fallbackScopeKind)
        {
            foreach (var gameObject in EnumerateGameObjectsRecursive(objects)
                         .Where(static obj => obj.ObjectId != Guid.Empty)
                         .OrderBy(static obj => obj.ObjectId))
            {
                var effectiveScopeKind = ResolveEffectiveObjectScopeKind(gameObject, fallbackScopeKind);
                referencedObjectIds.Add(gameObject.ObjectId);
                rows.Add((
                    $"{scopePathPrefix}.{BuildScopePathSegment(gameObject.Name, gameObject.Name)}",
                    ResolveArtifactKind(effectiveScopeKind),
                    gameObject.ObjectId.ToString("D").ToUpperInvariant(),
                    Path.GetRelativePath(projectFolderPath, BuildAuthoringObjectFilePath(projectFilePath, gameObject.ObjectId, effectiveScopeKind)).Replace('\\', '/')));
            }
        }

        rows.Add(("project", ScopeNodeKind.Global.ToString(), string.Empty, Path.GetFileName(projectFilePath)));
        rows.Add(("project.globals", "GlobalsSidecar", string.Empty, Path.GetRelativePath(projectFolderPath, BuildProjectGlobalNodeFilePath(projectFilePath)).Replace('\\', '/')));
        rows.Add(("project.state", "ProjectState", string.Empty, Path.GetRelativePath(projectFolderPath, BuildProjectStateFilePath(projectFilePath)).Replace('\\', '/')));

        AddObjectRows("project.baseObjects", project.BaseObjects, ScopeNodeKind.Templates);
        AddObjectRows("project.globalObjects", project.GlobalScope.GameObjects, ScopeNodeKind.GameObject);
        AddObjectRows("project.objectTemplates", project.ObjectTemplates, ScopeNodeKind.Templates);

        var planetPathById = new Dictionary<Guid, string>();
        var countryPathById = new Dictionary<Guid, string>();
        var areaPathById = new Dictionary<Guid, string>();
        var roomPathById = new Dictionary<Guid, string>();

        foreach (var planet in project.Planets.OrderBy(static planet => planet.Name, StringComparer.OrdinalIgnoreCase))
        {
            var planetScopePath = $"project.planets.{BuildScopePathSegment(planet.Name, planet.Name)}";
            planetPathById[planet.Id] = planetScopePath;

            AddObjectRows($"{planetScopePath}.baseObjects", planet.BaseObjects, ScopeNodeKind.GameObject);
            AddObjectRows($"{planetScopePath}.gameObjects", planet.GameObjects, ScopeNodeKind.GameObject);

            foreach (var country in planet.Countries.OrderBy(static country => country.Name, StringComparer.OrdinalIgnoreCase))
            {
                var countryScopePath = $"{planetScopePath}.countries.{BuildScopePathSegment(country.Name, country.Name)}";
                countryPathById[country.Id] = countryScopePath;

                AddObjectRows($"{countryScopePath}.baseObjects", country.BaseObjects, ScopeNodeKind.GameObject);
                AddObjectRows($"{countryScopePath}.gameObjects", country.GameObjects, ScopeNodeKind.GameObject);

                foreach (var area in country.Areas.OrderBy(static area => area.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var areaScopePath = $"{countryScopePath}.areas.{BuildScopePathSegment(area.Name, area.Name)}";
                    areaPathById[area.Id] = areaScopePath;

                    AddObjectRows($"{areaScopePath}.baseObjects", area.BaseObjects, ScopeNodeKind.GameObject);
                    AddObjectRows($"{areaScopePath}.gameObjects", area.GameObjects, ScopeNodeKind.GameObject);

                    foreach (var room in area.Rooms.OrderBy(static room => room.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var roomScopePath = $"{areaScopePath}.rooms.{BuildScopePathSegment(room.Name, room.Name)}";
                        roomPathById[room.Id] = roomScopePath;
                        AddObjectRows($"{roomScopePath}.gameObjects", room.GameObjects, ScopeNodeKind.GameObject);
                    }
                }
            }
        }

        foreach (var procedure in (project.Procedures ?? new List<ProcedureDefinition>())
                     .Where(static procedure => procedure.Id != Guid.Empty)
                     .OrderBy(static procedure => procedure.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static procedure => procedure.Id))
        {
            rows.Add((
                $"project.procedures.{BuildScopePathSegment(procedure.Name, procedure.Name)}",
                "Procedure",
                procedure.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildProcedureFilePath(projectFilePath, procedure.Id)).Replace('\\', '/')));
        }

        foreach (var planet in project.Planets.OrderBy(static planet => planet.Id))
        {
            if (planet.Id == Guid.Empty)
            {
                continue;
            }

            rows.Add((
                $"project.planets.{BuildScopePathSegment(planet.Name, planet.Name)}",
                "Planet",
                planet.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildPlanetFilePath(projectFilePath, planet.Id)).Replace('\\', '/')));
        }

        foreach (var country in project.Planets
                     .SelectMany(static planet => planet.Countries)
                     .Where(static country => country.Id != Guid.Empty)
                     .OrderBy(static country => country.Id))
        {
            rows.Add((
                $"project.countries.{BuildScopePathSegment(country.Name, country.Name)}",
                "Country",
                country.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildCountryFilePath(projectFilePath, country.Id)).Replace('\\', '/')));
        }

        foreach (var area in project.Planets
                     .SelectMany(static planet => planet.Countries)
                     .SelectMany(static country => country.Areas)
                     .Where(static area => area.Id != Guid.Empty)
                     .OrderBy(static area => area.Id))
        {
            rows.Add((
                $"project.areas.{BuildScopePathSegment(area.Name, area.Name)}",
                "Area",
                area.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildAreaFilePath(projectFilePath, area.Id)).Replace('\\', '/')));
        }

        foreach (var room in allRooms
                     .Where(static room => room.Id != Guid.Empty)
                     .OrderBy(static room => room.Id))
        {
            var roomScopePath = roomPathById.TryGetValue(room.Id, out var knownRoomScopePath)
                ? knownRoomScopePath
                : $"project.rooms.{BuildScopePathSegment(room.Name, room.Name)}";

            rows.Add((
                roomScopePath,
                "Room",
                room.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildRoomFilePath(projectFilePath, room.Id)).Replace('\\', '/')));
        }

        foreach (var roomTemplate in project.RoomTemplates
                     .Where(static room => room.Id != Guid.Empty)
                     .OrderBy(static room => room.Id))
        {
            var roomTemplateScopePath = $"project.roomTemplates.{BuildScopePathSegment(roomTemplate.Name, roomTemplate.Name)}";
            rows.Add((
                roomTemplateScopePath,
                ScopeNodeKind.RoomTemplates.ToString(),
                roomTemplate.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildRoomTemplateFilePath(projectFilePath, roomTemplate.Id)).Replace('\\', '/')));

            AddObjectRows($"{roomTemplateScopePath}.templateObjects", roomTemplate.GameObjects, ScopeNodeKind.Templates);
        }

        void AddPhaseRows(PhaseNode phaseNode, string scopePathPrefix)
        {
            if (phaseNode.Id != Guid.Empty)
            {
                rows.Add((
                    scopePathPrefix,
                    "Phase",
                    phaseNode.Id.ToString("D").ToUpperInvariant(),
                    Path.GetRelativePath(projectFolderPath, BuildPhaseFilePath(projectFilePath, phaseNode.Id)).Replace('\\', '/')));
            }

            foreach (var child in phaseNode.Children)
            {
                AddPhaseRows(child, $"{scopePathPrefix}.children.{BuildScopePathSegment(child.DisplayName, child.PhaseKey)}");
            }
        }

        if (phaseSidecarDtos is { Count: > 0 })
        {
            foreach (var phaseDto in phaseSidecarDtos
                         .Where(static phase => phase.Id != Guid.Empty)
                         .OrderBy(static phase => phase.Id))
            {
                rows.Add((
                    $"project.phases.{BuildScopePathSegment(phaseDto.DisplayName, phaseDto.PhaseKey)}",
                    "Phase",
                    phaseDto.Id.ToString("D").ToUpperInvariant(),
                    Path.GetRelativePath(projectFolderPath, BuildPhaseFilePath(projectFilePath, phaseDto.Id)).Replace('\\', '/')));
            }
        }
        else
        {
            foreach (var book in project.PhaseBooks.Where(static phase => phase.Id != Guid.Empty).OrderBy(static phase => phase.Id))
            {
                AddPhaseRows(book, $"project.phases.{BuildScopePathSegment(book.DisplayName, book.PhaseKey)}");
            }
        }

        foreach (var objectPair in objectDtosById
                     .Where(static pair => pair.Key != Guid.Empty)
                     .OrderBy(static pair => pair.Key))
        {
            var objectId = objectPair.Key;
            if (referencedObjectIds.Contains(objectId))
            {
                continue;
            }

            var effectiveScopeKind = objectPair.Value.ScopeKind;
            rows.Add((
                $"project.unresolvedObjects.{objectId:N}",
                ResolveArtifactKind(effectiveScopeKind),
                objectId.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(projectFolderPath, BuildAuthoringObjectFilePath(projectFilePath, objectId, effectiveScopeKind)).Replace('\\', '/')));
        }

        var orderedRows = rows
            .OrderBy(static row => row.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.ArtifactKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.ScopeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\" />");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        html.AppendLine("  <title>Authoring Persistence Index</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; }");
        html.AppendLine("    h1 { margin: 0 0 8px 0; font-size: 24px; }");
        html.AppendLine("    p { margin: 0 0 16px 0; color: #4b5563; }");
        html.AppendLine("    table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("    th, td { border: 1px solid #d1d5db; padding: 8px; vertical-align: top; text-align: left; }");
        html.AppendLine("    th { background: #f3f4f6; }");
        html.AppendLine("    code { font-family: Consolas, monospace; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <h1>Authoring Persistence Index</h1>");
        html.AppendLine($"  <p>Generated: {WebUtility.HtmlEncode(DateTime.UtcNow.ToString("u"))}</p>");
        html.AppendLine("  <table>");
        html.AppendLine("    <thead><tr><th>Scope Path</th><th>Artifact Kind</th><th>File</th></tr></thead>");
        html.AppendLine("    <tbody>");

        foreach (var row in orderedRows)
        {
            var scopePath = WebUtility.HtmlEncode(row.ScopePath);
            var artifactKind = WebUtility.HtmlEncode(row.ArtifactKind);
            var href = WebUtility.HtmlEncode(row.RelativeFilePath);
            var fileText = WebUtility.HtmlEncode(row.RelativeFilePath);
            html.AppendLine($"      <tr><td><code>{scopePath}</code></td><td>{artifactKind}</td><td><a href=\"{href}\">{fileText}</a></td></tr>");
        }

        html.AppendLine("    </tbody>");
        html.AppendLine("  </table>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static string BuildRuntimeIndexHtml(string projectFilePath, ProjectModel project, IReadOnlyList<Room> allRooms)
    {
        var exportRootPath = BuildCleanExportRootFolderPath(projectFilePath);
        var rows = new List<(string ScopePath, string ScopeKind, string ScopeId, string RelativeFilePath)>();

        var cleanProjectFilePath = BuildCleanProjectFilePath(projectFilePath);
        rows.Add(("global", "Project", string.Empty, Path.GetRelativePath(exportRootPath, cleanProjectFilePath).Replace('\\', '/')));

        var planetPathById = new Dictionary<Guid, string>();
        var countryPathById = new Dictionary<Guid, string>();
        var areaPathById = new Dictionary<Guid, string>();
        var roomPathById = new Dictionary<Guid, string>();

        void AddGameObjectRows(string scopePathPrefix, IEnumerable<GameObject> objects)
        {
            foreach (var gameObject in EnumerateGameObjectsRecursive(objects)
                .Where(static obj => obj.ObjectId != Guid.Empty)
                .OrderBy(static obj => obj.ObjectId))
            {
                rows.Add((
                    $"{scopePathPrefix}.{BuildScopePathSegment(gameObject.Name, gameObject.Name)}",
                    ScopeNodeKind.GameObject.ToString(),
                    gameObject.ObjectId.ToString("D").ToUpperInvariant(),
                    Path.GetRelativePath(exportRootPath, BuildScopeKindGameObjectFilePath(projectFilePath, gameObject.ObjectId)).Replace('\\', '/')));
            }
        }

        foreach (var planet in project.Planets.OrderBy(planet => planet.Name, StringComparer.OrdinalIgnoreCase))
        {
            var planetSegment = BuildScopePathSegment(planet.Name, planet.Name);
            var planetScopePath = $"global.{planetSegment}";
            planetPathById[planet.Id] = planetScopePath;
            foreach (var country in planet.Countries.OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase))
            {
                var countrySegment = BuildScopePathSegment(country.Name, country.Name);
                var countryScopePath = $"{planetScopePath}.{countrySegment}";
                countryPathById[country.Id] = countryScopePath;
                foreach (var area in country.Areas.OrderBy(area => area.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var areaSegment = BuildScopePathSegment(area.Name, area.Name);
                    var areaScopePath = $"{countryScopePath}.{areaSegment}";
                    areaPathById[area.Id] = areaScopePath;
                    foreach (var room in area.Rooms.OrderBy(room => room.Id))
                    {
                        var roomSegment = BuildScopePathSegment(room.Name, room.Name);
                        roomPathById[room.Id] = $"{areaScopePath}.{roomSegment}";
                    }
                }
            }
        }

        AddGameObjectRows("global.baseObjects", project.BaseObjects);
        AddGameObjectRows("global.gameObjects", project.GlobalScope.GameObjects);

        foreach (var planet in project.Planets.OrderBy(planet => planet.Id))
        {
            if (planet.Id == Guid.Empty)
            {
                continue;
            }

            var planetScopePath = planetPathById.TryGetValue(planet.Id, out var knownPath)
                ? knownPath
                : $"global.unknown.{BuildScopePathSegment(planet.Name, planet.Name)}";

            rows.Add((
                planetScopePath,
                ScopeNodeKind.Planet.ToString(),
                planet.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(exportRootPath, BuildScopeKindPlanetFilePath(projectFilePath, planet.Id)).Replace('\\', '/')));

            AddGameObjectRows($"{planetScopePath}.baseObjects", planet.BaseObjects);
            AddGameObjectRows($"{planetScopePath}.gameObjects", planet.GameObjects);
        }

        foreach (var country in project.Planets
            .SelectMany(planet => planet.Countries)
            .OrderBy(country => country.Id))
        {
            if (country.Id == Guid.Empty)
            {
                continue;
            }

            var countryScopePath = countryPathById.TryGetValue(country.Id, out var knownPath)
                ? knownPath
                : $"global.unknown.{BuildScopePathSegment(country.Name, country.Name)}";

            rows.Add((
                countryScopePath,
                ScopeNodeKind.Country.ToString(),
                country.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(exportRootPath, BuildScopeKindCountryFilePath(projectFilePath, country.Id)).Replace('\\', '/')));

            AddGameObjectRows($"{countryScopePath}.baseObjects", country.BaseObjects);
            AddGameObjectRows($"{countryScopePath}.gameObjects", country.GameObjects);
        }

        foreach (var area in project.Planets
            .SelectMany(planet => planet.Countries)
            .SelectMany(country => country.Areas)
            .OrderBy(area => area.Id))
        {
            if (area.Id == Guid.Empty)
            {
                continue;
            }

            var areaScopePath = areaPathById.TryGetValue(area.Id, out var knownPath)
                ? knownPath
                : $"global.unknown.{BuildScopePathSegment(area.Name, area.Name)}";

            rows.Add((
                areaScopePath,
                ScopeNodeKind.Area.ToString(),
                area.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(exportRootPath, BuildScopeKindAreaFilePath(projectFilePath, area.Id)).Replace('\\', '/')));

            AddGameObjectRows($"{areaScopePath}.baseObjects", area.BaseObjects);
            AddGameObjectRows($"{areaScopePath}.gameObjects", area.GameObjects);
        }

        foreach (var room in allRooms.OrderBy(room => room.Id))
        {
            var roomScopePath = roomPathById.TryGetValue(room.Id, out var knownPath)
                ? knownPath
                : $"global.unknown.{BuildScopePathSegment(room.Name, room.Name)}";

            rows.Add((
                roomScopePath,
                ScopeNodeKind.Room.ToString(),
                room.Id.ToString("D").ToUpperInvariant(),
                Path.GetRelativePath(exportRootPath, BuildScopeKindRoomFilePath(projectFilePath, room.Id)).Replace('\\', '/')));

            AddGameObjectRows($"{roomScopePath}.gameObjects", room.GameObjects);
        }

        var orderedRows = rows
            .OrderBy(row => row.ScopePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ScopeKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ScopeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\" />");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        html.AppendLine("  <title>Runtime Export Index</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; }");
        html.AppendLine("    h1 { margin: 0 0 8px 0; font-size: 24px; }");
        html.AppendLine("    p { margin: 0 0 16px 0; color: #4b5563; }");
        html.AppendLine("    table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("    th, td { border: 1px solid #d1d5db; padding: 8px; vertical-align: top; text-align: left; }");
        html.AppendLine("    th { background: #f3f4f6; }");
        html.AppendLine("    code { font-family: Consolas, monospace; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <h1>Runtime Export Index</h1>");
        html.AppendLine($"  <p>Generated: {WebUtility.HtmlEncode(DateTime.UtcNow.ToString("u"))}</p>");
        html.AppendLine("  <table>");
        html.AppendLine("    <thead><tr><th>Scope Path</th><th>Scope Kind</th><th>File</th></tr></thead>");
        html.AppendLine("    <tbody>");

        foreach (var row in orderedRows)
        {
            var scopePath = WebUtility.HtmlEncode(row.ScopePath);
            var scopeKind = WebUtility.HtmlEncode(row.ScopeKind);
            var href = WebUtility.HtmlEncode(row.RelativeFilePath);
            var fileText = WebUtility.HtmlEncode(row.RelativeFilePath);
            html.AppendLine($"      <tr><td><code>{scopePath}</code></td><td>{scopeKind}</td><td><a href=\"{href}\">{fileText}</a></td></tr>");
        }

        html.AppendLine("    </tbody>");
        html.AppendLine("  </table>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static string BuildPhaseNarrativeReviewHtml(ProjectModel project)
    {
        static string Safe(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var soundById = project.GlobalScope.SoundEffectLibraryEntries
            .Where(static entry => entry.SoundEffectId != Guid.Empty)
            .GroupBy(static entry => entry.SoundEffectId)
            .ToDictionary(
                static group => group.Key,
                static group => group.First(),
                EqualityComparer<Guid>.Default);

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\" />");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        html.AppendLine("  <title>Phase Narrative Review</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    :root { --bg: #f8fafc; --card: #ffffff; --ink: #0f172a; --muted: #475569; --line: #dbe3ef; --accent: #0f766e; }");
        html.AppendLine("    body { margin: 0; padding: 24px; background: var(--bg); color: var(--ink); font-family: Segoe UI, Arial, sans-serif; }");
        html.AppendLine("    h1 { margin: 0; font-size: 28px; }");
        html.AppendLine("    .meta { margin-top: 6px; color: var(--muted); }");
        html.AppendLine("    .toolbar { margin: 14px 0 18px 0; display: flex; gap: 8px; }");
        html.AppendLine("    button { border: 1px solid var(--line); background: #fff; color: var(--ink); padding: 6px 10px; border-radius: 6px; cursor: pointer; }");
        html.AppendLine("    details.phase { margin: 10px 0; border: 1px solid var(--line); border-radius: 10px; background: var(--card); }");
        html.AppendLine("    summary { list-style: none; cursor: pointer; padding: 12px 14px; display: flex; align-items: baseline; justify-content: space-between; gap: 12px; }");
        html.AppendLine("    summary::-webkit-details-marker { display: none; }");
        html.AppendLine("    .summary-main { min-width: 0; }");
        html.AppendLine("    .tier { font-size: 11px; text-transform: uppercase; letter-spacing: 0.08em; color: var(--accent); margin-right: 8px; }");
        html.AppendLine("    .name { font-weight: 700; }");
        html.AppendLine("    .title { margin-left: 8px; color: var(--muted); font-style: italic; }");
        html.AppendLine("    .phase-key { color: #64748b; font-size: 12px; white-space: nowrap; text-align: right; }");
        html.AppendLine("    .content { padding: 0 14px 14px 14px; border-top: 1px solid var(--line); }");
        html.AppendLine("    .field { margin-top: 8px; }");
        html.AppendLine("    .label { color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: 0.06em; }");
        html.AppendLine("    .value { margin-top: 2px; white-space: pre-wrap; }");
        html.AppendLine("    .children { margin-top: 10px; margin-left: 14px; }");
        html.AppendLine("    .focus { outline: 2px solid #f59e0b; outline-offset: 2px; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <h1>Phase Narrative Review</h1>");
        html.AppendLine($"  <div class=\"meta\">Project: {Safe(project.Name)} | Generated: {Safe(DateTime.UtcNow.ToString("u"))}</div>");
        html.AppendLine("  <div class=\"toolbar\">");
        html.AppendLine("    <button type=\"button\" onclick=\"setAll(true)\">Expand All</button>");
        html.AppendLine("    <button type=\"button\" onclick=\"setAll(false)\">Collapse All</button>");
        html.AppendLine("  </div>");

        foreach (var book in project.PhaseBooks)
        {
            AppendPhaseNodeHtml(html, book, soundById, isRoot: true);
        }

        html.AppendLine("  <script>");
        html.AppendLine("    function setAll(open) { document.querySelectorAll('details.phase').forEach(d => d.open = open); }");
        html.AppendLine("    (function(){");
        html.AppendLine("      if (!location.hash) return;");
        html.AppendLine("      var target = document.getElementById(location.hash.slice(1));");
        html.AppendLine("      if (!target) return;");
        html.AppendLine("      var parent = target.parentElement;");
        html.AppendLine("      while (parent) { if (parent.tagName === 'DETAILS') parent.open = true; parent = parent.parentElement; }");
        html.AppendLine("      target.classList.add('focus');");
        html.AppendLine("      target.scrollIntoView({block:'start'});");
        html.AppendLine("    })();");
        html.AppendLine("  </script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendPhaseNodeHtml(
        StringBuilder html,
        PhaseNode node,
        IReadOnlyDictionary<Guid, SoundEffectLibraryEntry> soundById,
        bool isRoot)
    {
        static string Safe(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var nodeId = $"phase-{node.Id:N}";
        var title = string.IsNullOrWhiteSpace(node.Title) ? string.Empty : node.Title.Trim();
        var prologue = string.IsNullOrWhiteSpace(node.Prologue) ? string.Empty : node.Prologue.Trim();
        var narrative = string.IsNullOrWhiteSpace(node.Narrative) ? string.Empty : node.Narrative.Trim();
        var timerKey = string.IsNullOrWhiteSpace(node.PhaseAmbientTimerKey) ? string.Empty : node.PhaseAmbientTimerKey.Trim();

        var soundDisplay = string.Empty;
        if (node.PhaseAmbientSoundEffectId.HasValue && soundById.TryGetValue(node.PhaseAmbientSoundEffectId.Value, out var sound))
        {
            soundDisplay = string.IsNullOrWhiteSpace(sound.DisplayName)
                ? sound.SoundEffectKey
                : $"{sound.DisplayName} ({sound.SoundEffectKey})";
        }
        else if (node.PhaseAmbientSoundEffectId.HasValue)
        {
            soundDisplay = node.PhaseAmbientSoundEffectId.Value.ToString("D");
        }

        html.AppendLine($"<details class=\"phase\" {(isRoot ? "open" : string.Empty)}>");
        html.AppendLine($"  <summary id=\"{nodeId}\"><span class=\"summary-main\"><span class=\"tier\">{Safe(node.Tier.ToString())}</span><span class=\"name\">{Safe(string.IsNullOrWhiteSpace(node.DisplayName) ? node.PhaseKey : node.DisplayName)}</span>{(string.IsNullOrWhiteSpace(title) ? string.Empty : $"<span class=\"title\">{Safe(title)}</span>")}</span><span class=\"phase-key\">{Safe(node.PhaseKey)}</span></summary>");
        html.AppendLine("  <div class=\"content\">");
        if (!string.IsNullOrWhiteSpace(prologue))
        {
            html.AppendLine($"    <div class=\"field\"><div class=\"label\">Prologue</div><div class=\"value\">{Safe(prologue)}</div></div>");
        }

        if (!string.IsNullOrWhiteSpace(narrative))
        {
            html.AppendLine($"    <div class=\"field\"><div class=\"label\">Narrative</div><div class=\"value\">{Safe(narrative)}</div></div>");
        }

        if (!string.IsNullOrWhiteSpace(soundDisplay) || !string.IsNullOrWhiteSpace(timerKey) || node.PhaseAmbienceMode.HasValue)
        {
            html.AppendLine("    <div class=\"field\"><div class=\"label\">Ambience</div>");
            if (!string.IsNullOrWhiteSpace(soundDisplay))
            {
                html.AppendLine($"      <div class=\"value\">Sound: {Safe(soundDisplay)}</div>");
            }

            if (!string.IsNullOrWhiteSpace(timerKey))
            {
                html.AppendLine($"      <div class=\"value\">Timer Key: {Safe(timerKey)}</div>");
            }

            if (node.PhaseAmbienceMode.HasValue)
            {
                html.AppendLine($"      <div class=\"value\">Mode: {Safe(node.PhaseAmbienceMode.Value.ToString())}</div>");
            }

            html.AppendLine("    </div>");
        }

        if (node.Children.Count > 0)
        {
            html.AppendLine("    <div class=\"children\">");
            foreach (var child in node.Children)
            {
                AppendPhaseNodeHtml(html, child, soundById, isRoot: false);
            }

            html.AppendLine("    </div>");
        }

        html.AppendLine("  </div>");
        html.AppendLine("</details>");
    }

    private static string BuildScopePathSegment(string? preferredName, string? fallbackName)
    {
        var value = string.IsNullOrWhiteSpace(preferredName)
            ? fallbackName
            : preferredName;

        if (string.IsNullOrWhiteSpace(value))
        {
            return "unnamed";
        }

        return value.Trim().Replace('.', '_');
    }

    private static List<CommandAction> BuildRoomAvailableActions(RoomDto room)
    {
        return ToCommandActionModels(room.AvailableGameActions);
    }

    private static List<CommandActionDto> ToCommandActionDtos(IReadOnlyCollection<CommandAction>? actions)
    {
        var safeActions = (actions ?? Array.Empty<CommandAction>()).ToList();
        var actionLookup = safeActions
            .Where(static action => action.Id != Guid.Empty)
            .GroupBy(static action => action.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        return safeActions
            .Select(action => ToCommandActionDto(action, actionLookup))
            .ToList();
    }

    private static CommandActionDto ToCommandActionDto(CommandAction action, IReadOnlyDictionary<Guid, CommandAction> actionLookup)
    {
        var containerTransfer = ActionPayloadAccessors.GetContainerTransfer(action);
        var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(action);
        var materializeSourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(action);
        var procedureId = ActionPayloadAccessors.GetProcedureId(action);
        var startTimerKey = ActionPayloadAccessors.GetStartTimerKey(action);
        var startTimerOwnerScopeKindOverride = ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(action);
        var cancelTimerKey = ActionPayloadAccessors.GetCancelTimerKey(action);
        var cancelTimerScopeQualifierKind = ActionPayloadAccessors.GetCancelTimerScopeQualifierKind(action);
        var cancelTimerScopeQualifierId = ActionPayloadAccessors.GetCancelTimerScopeQualifierId(action);
        var compositeByTargetPayload = ActionPayloadAccessors.GetCompositeByTargetPayload(action);
        var compositeByPartsPayload = ActionPayloadAccessors.GetCompositeByPartsPayload(action);
        var breakCompositePayload = ActionPayloadAccessors.GetBreakCompositePayload(action);
        var movePayload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(action);
        var moveByPointsPayload = ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(action);
        var rotatePayload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(action);
        var stackPayload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(action);
        var setActivePayload = ActionPayloadAccessors.GetSetActiveRoomObjectPayload(action);
        var selectByPointPayload = ActionPayloadAccessors.GetSelectRoomObjectByPointPayload(action);
        var clearActivePayload = ActionPayloadAccessors.GetClearActiveRoomObjectsPayload(action);
        var clearSelectionsPayload = ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(action);

        return new CommandActionDto
        {
            Id = action.Id,
            Name = action.Name,
            ActionType = ParseCommandActionType(action.ActionType),
            NoVerbLinkage = action.NoVerbLinkage,
            VerbListText = action.VerbListText,
            Verbs = action.Verbs.Count > 1 ? action.Verbs.ToList() : null,
            Direction = NormalizeDirectionToken(action.DirectionQualifierText),
            OutcomeMessageMap = BuildOutcomeMessageMapForPersistence(action),
            OutcomeSoundEffectsMap = BuildOutcomeSoundEffectsMapForPersistence(action),
            Payload = action.ActionType switch
            {
                CommandActionType.EchoMessage => BuildEchoMessagePayloadElement(),
                CommandActionType.CheckGameProperty => BuildCheckGamePropertyPayloadElement(ActionPayloadAccessors.GetCheckPropertyName(action), ActionPayloadAccessors.GetCheckExpectedValue(action)),
                CommandActionType.ClearActiveRoomObjects => BuildClearActiveRoomObjectsPayloadElement(clearActivePayload.ClearScope),
                CommandActionType.CloseObject => BuildCloseObjectPayloadElement(),
                CommandActionType.InvokeProcedure => BuildInvokeProcedurePayloadElement(procedureId),
                CommandActionType.LockObject => BuildLockObjectPayloadElement(),
                CommandActionType.StartTimer => BuildStartTimerPayloadElement(startTimerKey, startTimerOwnerScopeKindOverride),
                CommandActionType.CancelTimer => BuildCancelTimerPayloadElement(cancelTimerKey, cancelTimerScopeQualifierKind, cancelTimerScopeQualifierId),
                CommandActionType.MaterializeObjectCopy => BuildMaterializeObjectCopyPayloadElement(materializeSourceObjectId),
                CommandActionType.MoveRoomObjectOnGrid => BuildMoveRoomObjectOnGridPayloadElement(movePayload),
                CommandActionType.MoveRoomObjectByPoints => BuildMoveRoomObjectByPointsPayloadElement(moveByPointsPayload),
                CommandActionType.RotateRoomObjectOnGrid => BuildRotateRoomObjectOnGridPayloadElement(rotatePayload),
                CommandActionType.StackRoomObjectOnAnother => BuildStackRoomObjectOnAnotherPayloadElement(stackPayload),
                CommandActionType.NavigateDirection => BuildNavigateDirectionPayloadElement(),
                CommandActionType.NavigateToAdjacent => BuildNavigateToAdjacentPayloadElement(),
                CommandActionType.OpenObject => BuildOpenObjectPayloadElement(),
                CommandActionType.PutObjectInContainer => BuildPutObjectInContainerPayloadElement(containerTransfer.TargetContainerId),
                CommandActionType.RemoveObjectFromContainer => BuildRemoveObjectFromContainerPayloadElement(containerTransfer.TargetContainerId),
                CommandActionType.BuildCompositeByTarget => BuildBuildCompositeByTargetPayloadElement(compositeByTargetPayload),
                CommandActionType.BuildCompositeByParts => BuildBuildCompositeByPartsPayloadElement(compositeByPartsPayload),
                CommandActionType.BreakCompositeItem => BuildBreakCompositeItemPayloadElement(breakCompositePayload),
                CommandActionType.SetActiveRoomObject => BuildSetActiveRoomObjectPayloadElement(setActivePayload.SelectionCueEffectKey),
                CommandActionType.SelectRoomObjectByPoint => BuildSelectRoomObjectByPointPayloadElement(selectByPointPayload.SelectionCueEffectKey),
                CommandActionType.SetGameProperty => BuildSetGamePropertyPayloadElement(ActionPayloadAccessors.GetSetPropertyName(action), ActionPayloadAccessors.GetSetPropertyValue(action)),
                CommandActionType.SetFlag => BuildSetFlagPayloadElement(ActionPayloadAccessors.GetSetFlagName(action), ActionPayloadAccessors.GetSetFlagValue(action)),
                CommandActionType.Synonym => BuildSynonymPayloadElement(synonymTargetActionId),
                CommandActionType.ClearRoomObjectSelections => BuildClearRoomObjectSelectionsPayloadElement(clearSelectionsPayload.ClearScope),
                CommandActionType.UnlockObject => BuildUnlockObjectPayloadElement(),
                CommandActionType.LinkedActions => BuildLinkedActionsPayloadElement(action, actionLookup),
                _ => null
            },
            ChildCommandForwardingMode = action.ChildCommandForwardingMode,
            SimilarChildDispatchMode = action.SimilarChildDispatchMode
        };
    }

    private static CommandAction ToCommandActionModel(CommandActionDto action)
    {
        var actionType = ParseCommandActionType(action.ActionType);
        var invokeProcedureIdFromPayload = TryGetInvokeProcedureIdFromPayload(action.Payload);
        var materializeSourceObjectIdFromPayload = TryGetMaterializeSourceObjectIdFromPayload(action.Payload);
        var startTimerKeyFromPayload = TryGetStartTimerKeyFromPayload(action.Payload);
        var startTimerOwnerScopeKindOverrideFromPayload = TryGetStartTimerOwnerScopeKindOverrideFromPayload(action.Payload);
        var cancelTimerKeyFromPayload = TryGetCancelTimerKeyFromPayload(action.Payload);
        var cancelTimerScopeQualifierKindFromPayload = TryGetCancelTimerScopeQualifierKindFromPayload(action.Payload);
        var cancelTimerScopeQualifierIdFromPayload = TryGetCancelTimerScopeQualifierIdFromPayload(action.Payload);
        var putObjectInContainerTargetContainerIdFromPayload = TryGetPutObjectInContainerTargetContainerIdFromPayload(action.Payload);
        var removeObjectFromContainerTargetContainerIdFromPayload = TryGetRemoveObjectFromContainerTargetContainerIdFromPayload(action.Payload);
        var moveDirectionTokenFromPayload = TryGetMoveDirectionTokenFromPayload(action.Payload);
        var moveDistanceInCellsFromPayload = TryGetMoveDistanceInCellsFromPayload(action.Payload);
        var moveAllowPartialMoveFromPayload = TryGetMoveAllowPartialMoveFromPayload(action.Payload);
        var moveAllowJumpOverFromPayload = TryGetMoveAllowJumpOverFromPayload(action.Payload);
        var moveVisualTransitionHintFromPayload = TryGetMoveVisualTransitionHintFromPayload(action.Payload);
        var moveTravelVisualizationModeFromPayload = TryGetMoveTravelVisualizationModeFromPayload(action.Payload);
        var moveByPointsTargetResolutionIntentFromPayload = TryGetMoveByPointsTargetResolutionIntentFromPayload(action.Payload);
        var moveByPointsAllowPartialMoveFromPayload = TryGetMoveByPointsAllowPartialMoveFromPayload(action.Payload);
        var moveByPointsAllowJumpOverFromPayload = TryGetMoveByPointsAllowJumpOverFromPayload(action.Payload);
        var moveByPointsVisualTransitionHintFromPayload = TryGetMoveByPointsVisualTransitionHintFromPayload(action.Payload);
        var moveByPointsTravelVisualizationModeFromPayload = TryGetMoveByPointsTravelVisualizationModeFromPayload(action.Payload);
        var rotateModeFromPayload = TryGetRotateModeFromPayload(action.Payload);
        var rotateTurnDegreesFromPayload = TryGetRotateTurnDegreesFromPayload(action.Payload);
        var rotateFacingDirectionTokenFromPayload = TryGetRotateFacingDirectionTokenFromPayload(action.Payload);
        var rotateVisualTransitionHintFromPayload = TryGetRotateVisualTransitionHintFromPayload(action.Payload);
        var stackVisualTransitionHintFromPayload = TryGetStackVisualTransitionHintFromPayload(action.Payload);
        var clearActiveScopeFromPayload = TryGetClearActiveRoomObjectsScopeFromPayload(action.Payload);
        var setActiveSelectionCueEffectKeyFromPayload = TryGetSetActiveSelectionCueEffectKeyFromPayload(action.Payload);
        var selectByPointSelectionCueEffectKeyFromPayload = TryGetSelectByPointSelectionCueEffectKeyFromPayload(action.Payload);
        var clearRoomObjectSelectionsScopeFromPayload = TryGetClearRoomObjectSelectionsScopeFromPayload(action.Payload);
        var checkPropertyNameFromPayload = TryGetCheckGamePropertyNameFromPayload(action.Payload);
        var checkExpectedValueFromPayload = TryGetCheckGamePropertyExpectedValueFromPayload(action.Payload);
        var setPropertyNameFromPayload = TryGetSetGamePropertyNameFromPayload(action.Payload);
        var setPropertyValueFromPayload = TryGetSetGamePropertyValueFromPayload(action.Payload);
        var setFlagNameFromPayload = TryGetSetFlagNameFromPayload(action.Payload);
        var setFlagValueFromPayload = TryGetSetFlagValueFromPayload(action.Payload);
        var compositeTargetObjectIdFromPayload = TryGetCompositeTargetObjectIdFromPayload(action.Payload);
        var compositeRecipeIdFromPayload = TryGetCompositeRecipeIdFromPayload(action.Payload);
        var compositeRequiredPartObjectIdsFromPayload = TryGetCompositeRequiredPartObjectIdsFromPayload(action.Payload);
        var compositeStrictPartCountEnforcementFromPayload = TryGetCompositeStrictPartCountEnforcementFromPayload(action.Payload);
        var compositeMinimumRequiredPartCountFromPayload = TryGetCompositeMinimumRequiredPartCountFromPayload(action.Payload);
        var compositeMatchModeFromPayload = TryGetCompositeMatchModeFromPayload(action.Payload);
        var compositeAmbiguityPolicyFromPayload = TryGetCompositeAmbiguityPolicyFromPayload(action.Payload);
        var compositePartConsumptionModeFromPayload = TryGetCompositePartConsumptionModeFromPayload(action.Payload);
        var synonymTargetActionIdFromPayload = TryGetSynonymTargetActionIdFromPayload(action.Payload);
        var linkedActionReferences = ResolveLinkedActionReferences(action);
        var outcomeMessageMap = NormalizeOutcomeMessageMap(actionType, action.OutcomeMessageMap);
        var outcomeSoundEffectsMap = NormalizeOutcomeSoundEffectsMap(actionType, action.OutcomeSoundEffectsMap);
        string ResolveOutcomeScript(string token)
        {
            return outcomeMessageMap.TryGetValue(token, out var script)
                ? script ?? string.Empty
                : string.Empty;
        }

        var compositeMatchMode = compositeMatchModeFromPayload ?? string.Empty;
        var compositeAmbiguityPolicy = compositeAmbiguityPolicyFromPayload ?? string.Empty;
        if (actionType == CommandActionType.BuildCompositeByParts)
        {
            if (string.IsNullOrWhiteSpace(compositeMatchMode))
            {
                compositeMatchMode = "AllRequired";
            }

            if (string.IsNullOrWhiteSpace(compositeAmbiguityPolicy))
            {
                compositeAmbiguityPolicy = "FailWithHint";
            }
        }

        var verbs = NormalizeActionVerbs(action.Verbs, action.VerbListText);

        var model = new CommandAction
        {
            Id = EnsureScopeId(action.Id, $"action '{action.Name}'"),
            Name = string.IsNullOrWhiteSpace(action.Name) ? "Action" : action.Name,
            ActionType = actionType,
            NoVerbLinkage = action.NoVerbLinkage,
            Verbs = verbs,
            DirectionQualifierText = NormalizeDirectionToken(action.Direction),
            TargetContainerId = actionType switch
            {
                CommandActionType.PutObjectInContainer => putObjectInContainerTargetContainerIdFromPayload ?? string.Empty,
                CommandActionType.RemoveObjectFromContainer => removeObjectFromContainerTargetContainerIdFromPayload ?? string.Empty,
                _ => string.Empty
            },
            SynonymTargetActionId = actionType == CommandActionType.Synonym
                ? synonymTargetActionIdFromPayload
                : null,
            MaterializeSourceObjectId = actionType == CommandActionType.MaterializeObjectCopy
                ? materializeSourceObjectIdFromPayload
                : null,
            ProcedureId = actionType == CommandActionType.InvokeProcedure
                ? invokeProcedureIdFromPayload
                : null,
            CompositeTargetObjectId = compositeTargetObjectIdFromPayload,
            CompositeRecipeId = compositeRecipeIdFromPayload,
            CompositeRequiredPartObjectIds = compositeRequiredPartObjectIdsFromPayload ?? new List<Guid>(),
            CompositeStrictPartCountEnforcement = compositeStrictPartCountEnforcementFromPayload,
            CompositeMinimumRequiredPartCount = compositeMinimumRequiredPartCountFromPayload,
            CompositeMatchMode = compositeMatchMode,
            CompositeAmbiguityPolicy = compositeAmbiguityPolicy,
            CompositePartConsumptionMode = compositePartConsumptionModeFromPayload ?? string.Empty,
            CompositeResolvedTargetOutputTemplate = string.Empty,
            ChildCommandForwardingMode = ParseChildCommandForwardingMode(action.ChildCommandForwardingMode),
            SimilarChildDispatchMode = ParseSimilarChildDispatchMode(action.SimilarChildDispatchMode),
            LinkedActions = linkedActionReferences,
            OutcomeMessageMap = outcomeMessageMap,
            OutcomeSoundEffectsMap = outcomeSoundEffectsMap,
            SelectionCueEffectKey = actionType == CommandActionType.SetActiveRoomObject
                ? setActiveSelectionCueEffectKeyFromPayload ?? string.Empty
                : string.Empty
        };

        if (model.ActionType == CommandActionType.MoveRoomObjectOnGrid)
        {
            var moveHint = moveVisualTransitionHintFromPayload
                ?? RuntimeMovementVisualTransitionHint.Medium;
            var moveTravelMode = moveTravelVisualizationModeFromPayload
                ?? RuntimeMovementTravelVisualizationMode.LegByLeg;
            var moveDirectionSource = moveDirectionTokenFromPayload ?? action.Direction;
            var moveDirection = NormalizeDirectionToken(moveDirectionSource);
            ActionPayloadAccessors.SetMoveRoomObjectOnGrid(
                model,
                moveDirection,
                moveDistanceInCellsFromPayload.GetValueOrDefault(1),
                moveAllowPartialMoveFromPayload.GetValueOrDefault(false),
                moveHint,
                moveAllowJumpOverFromPayload.GetValueOrDefault(false),
                moveTravelMode);
        }

        if (model.ActionType == CommandActionType.RotateRoomObjectOnGrid)
        {
            var rotateMode = rotateModeFromPayload
                ?? RuntimeRotateRoomObjectOnGridAttemptMode.Turn;
            var rotateHint = rotateVisualTransitionHintFromPayload
                ?? RuntimeMovementVisualTransitionHint.Medium;
            var rotateTurnDegrees = rotateMode == RuntimeRotateRoomObjectOnGridAttemptMode.Turn
                ? rotateTurnDegreesFromPayload
                : null;
            var rotateFacingDirection = rotateMode == RuntimeRotateRoomObjectOnGridAttemptMode.Face
                ? NormalizeDirectionToken(rotateFacingDirectionTokenFromPayload)
                : string.Empty;

            ActionPayloadAccessors.SetRotateRoomObjectOnGrid(
                model,
                rotateMode,
                rotateTurnDegrees,
                rotateFacingDirection,
                rotateHint);
        }

        if (model.ActionType == CommandActionType.MoveRoomObjectByPoints)
        {
            var moveHint = moveByPointsVisualTransitionHintFromPayload
                ?? RuntimeMovementVisualTransitionHint.Medium;
            var moveTravelMode = moveByPointsTravelVisualizationModeFromPayload
                ?? RuntimeMovementTravelVisualizationMode.LegByLeg;
            ActionPayloadAccessors.SetMoveRoomObjectByPoints(
                model,
                moveByPointsTargetResolutionIntentFromPayload ?? string.Empty,
                moveByPointsAllowPartialMoveFromPayload.GetValueOrDefault(false),
                moveByPointsAllowJumpOverFromPayload.GetValueOrDefault(false),
                moveHint,
                moveTravelMode);
        }

        if (model.ActionType == CommandActionType.StackRoomObjectOnAnother)
        {
            var stackHint = stackVisualTransitionHintFromPayload
                ?? RuntimeMovementVisualTransitionHint.Medium;

            ActionPayloadAccessors.SetStackRoomObjectOnAnother(
                model,
                stackHint);
        }

        if (model.ActionType == CommandActionType.SetActiveRoomObject)
        {
            ActionPayloadAccessors.SetSetActiveRoomObject(model, model.SelectionCueEffectKey ?? string.Empty);
        }

        if (model.ActionType == CommandActionType.SelectRoomObjectByPoint)
        {
            ActionPayloadAccessors.SetSelectRoomObjectByPoint(
                model,
                selectByPointSelectionCueEffectKeyFromPayload ?? string.Empty);
        }

        if (model.ActionType == CommandActionType.ClearActiveRoomObjects)
        {
            var clearScope = clearActiveScopeFromPayload
                ?? RuntimeClearActiveRoomObjectsScope.Both;
            ActionPayloadAccessors.SetClearActiveRoomObjects(model, clearScope);
        }

        if (model.ActionType == CommandActionType.ClearRoomObjectSelections)
        {
            ActionPayloadAccessors.SetClearRoomObjectSelections(
                model,
                clearRoomObjectSelectionsScopeFromPayload ?? ClearRoomObjectSelectionsScope.All);
        }

        if (model.NoVerbLinkage || model.Verbs.Count == 0)
        {
            model.NoVerbLinkage = true;
            model.Verbs = new List<string>();
        }

        if (model.ActionType == CommandActionType.EchoMessage)
        {
            ActionPayloadAccessors.SetEchoMessage(model, ResolveOutcomeScript("Success"));
        }

        if (model.ActionType == CommandActionType.CheckGameProperty)
        {
            ActionPayloadAccessors.SetCheckGameProperty(
                model,
                checkPropertyNameFromPayload ?? string.Empty,
                checkExpectedValueFromPayload.GetValueOrDefault(false));
        }

        if (model.ActionType == CommandActionType.SetFlag)
        {
            ActionPayloadAccessors.SetSetFlag(
                model,
                setFlagNameFromPayload ?? string.Empty,
                setFlagValueFromPayload.GetValueOrDefault(false));
        }

        if (model.ActionType == CommandActionType.SetGameProperty)
        {
            ActionPayloadAccessors.SetSetGameProperty(
                model,
                setPropertyNameFromPayload ?? string.Empty,
                setPropertyValueFromPayload ?? string.Empty);
        }

        if (model.ActionType == CommandActionType.MaterializeObjectCopy)
        {
            ActionPayloadAccessors.SetMaterializeSourceObjectId(model, model.MaterializeSourceObjectId);
        }

        if (model.ActionType == CommandActionType.InvokeProcedure)
        {
            ActionPayloadAccessors.SetProcedureId(model, model.ProcedureId);
        }

        if (model.ActionType == CommandActionType.StartTimer)
        {
            ActionPayloadAccessors.SetStartTimer(
                model,
                startTimerKeyFromPayload ?? string.Empty,
                startTimerOwnerScopeKindOverrideFromPayload);
        }

        if (model.ActionType == CommandActionType.CancelTimer)
        {
            ActionPayloadAccessors.SetCancelTimer(
                model,
                cancelTimerKeyFromPayload ?? string.Empty,
                cancelTimerScopeQualifierKindFromPayload,
                cancelTimerScopeQualifierIdFromPayload);
        }

        model.Payload = model.CreatePayloadSnapshot();

        return model;
    }

    private static List<CommandAction> ToCommandActionModels(IReadOnlyCollection<CommandActionDto>? actionDtos)
    {
        var safeDtos = (actionDtos ?? Array.Empty<CommandActionDto>()).ToList();
        var models = safeDtos
            .Select(ToCommandActionModel)
            .ToList();

        HydrateLinkedFlowGraphs(safeDtos, models);
        return models;
    }

    private static void HydrateLinkedFlowGraphs(IReadOnlyList<CommandActionDto> sourceDtos, IReadOnlyList<CommandAction> models)
    {
        if (sourceDtos.Count == 0 || models.Count == 0)
        {
            return;
        }

        var modelsById = models
            .Where(static model => model.Id != Guid.Empty)
            .GroupBy(static model => model.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        foreach (var model in models)
        {
            model.LinkedActions = new List<LinkedActionReference>();
        }

        foreach (var sourceDto in sourceDtos)
        {
            if (ParseCommandActionType(sourceDto.ActionType) != CommandActionType.LinkedActions)
            {
                continue;
            }

            if (!modelsById.TryGetValue(sourceDto.Id, out var rootAction))
            {
                continue;
            }

            var nodeDtos = TryGetLinkedActionsFromPayload(sourceDto.Payload);
            if (nodeDtos is not { Count: > 0 })
            {
                continue;
            }

            ApplyLinkedFlowNodesToActionGraph(rootAction, nodeDtos, modelsById);
        }
    }

    private static void ApplyLinkedFlowNodesToActionGraph(
        CommandAction rootAction,
        IReadOnlyList<ProjectCommandActionReferenceDto> nodeDtos,
        IReadOnlyDictionary<Guid, CommandAction> modelsById)
    {
        var orderedNodes = new List<ProjectCommandActionReferenceDto>();
        var seenNodeIds = new HashSet<int>();
        foreach (var nodeDto in nodeDtos)
        {
            if (nodeDto.NodeId < 1 || nodeDto.ActionId == Guid.Empty)
            {
                continue;
            }

            if (!modelsById.ContainsKey(nodeDto.ActionId) || !seenNodeIds.Add(nodeDto.NodeId))
            {
                continue;
            }

            orderedNodes.Add(nodeDto);
        }

        if (orderedNodes.Count == 0)
        {
            return;
        }

        var nodesById = orderedNodes.ToDictionary(static node => node.NodeId, static node => node);

        foreach (var node in orderedNodes)
        {
            if (!modelsById.TryGetValue(node.ActionId, out var parentAction))
            {
                continue;
            }

            var links = new List<LinkedActionReference>();
            AddBranchLinks(links, node.OnAlwaysNodeIds, LinkedActionRunWhen.Always, nodesById, modelsById);
            AddBranchLinks(links, node.OnSuccessNodeIds, LinkedActionRunWhen.OnSuccess, nodesById, modelsById);
            AddBranchLinks(links, node.OnFailureNodeIds, LinkedActionRunWhen.OnFailure, nodesById, modelsById);
            parentAction.LinkedActions = links;
        }

        var entryNode = orderedNodes[0];
        if (modelsById.ContainsKey(entryNode.ActionId))
        {
            rootAction.LinkedActions = new List<LinkedActionReference>
            {
                new()
                {
                    ActionId = entryNode.ActionId,
                    RunWhen = LinkedActionRunWhen.Always,
                    Order = 0
                }
            };
        }
    }

    private static void AddBranchLinks(
        List<LinkedActionReference> targetLinks,
        IReadOnlyList<int>? childNodeIds,
        LinkedActionRunWhen runWhen,
        IReadOnlyDictionary<int, ProjectCommandActionReferenceDto> nodesById,
        IReadOnlyDictionary<Guid, CommandAction> modelsById)
    {
        if (childNodeIds is null || childNodeIds.Count == 0)
        {
            return;
        }

        var order = 0;
        foreach (var childNodeId in childNodeIds)
        {
            if (!nodesById.TryGetValue(childNodeId, out var childNode)
                || childNode.ActionId == Guid.Empty
                || !modelsById.ContainsKey(childNode.ActionId))
            {
                continue;
            }

            targetLinks.Add(new LinkedActionReference
            {
                ActionId = childNode.ActionId,
                RunWhen = runWhen,
                Order = order++
            });
        }
    }

    private static ChildCommandForwardingMode ParseChildCommandForwardingMode(ChildCommandForwardingMode value)
    {
        return Enum.IsDefined(value)
            ? value
            : ChildCommandForwardingMode.None;
    }

    private static SimilarChildDispatchMode ParseSimilarChildDispatchMode(SimilarChildDispatchMode? value)
    {
        if (!value.HasValue)
        {
            return SimilarChildDispatchMode.SingleMatchingChild;
        }

        return Enum.IsDefined(value.Value)
            ? value.Value
            : SimilarChildDispatchMode.SingleMatchingChild;
    }

    private static LinkedActionRunWhen ParseLinkedActionRunWhen(LinkedActionRunWhen value)
    {
        return Enum.IsDefined(value)
            ? value
            : LinkedActionRunWhen.OnSuccess;
    }

    private static LinkedActionRunWhen ParseLinkedActionRunWhen(string? value)
    {
        if (Enum.TryParse<LinkedActionRunWhen>(value, out var parsed))
        {
            return parsed;
        }

        return LinkedActionRunWhen.OnSuccess;
    }

    private static CommandActionType ParseCommandActionType(CommandActionType value)
    {
        return Enum.IsDefined(value)
            ? value
            : CommandActionType.EchoMessage;
    }

    private static CommandActionType ParseCommandActionType(string? value)
    {
        if (Enum.TryParse<CommandActionType>(value, out var parsed))
        {
            return parsed;
        }

        return CommandActionType.EchoMessage;
    }

    private static string NormalizeDirectionToken(string? value)
    {
        var token = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        if (GameCommandDirectionFormatting.TryParseToken(token, out var parsed))
        {
            return parsed.ToToken();
        }

        if (Enum.TryParse<GameCommandDirection>(token, out var byEnumName))
        {
            return byEnumName.ToToken();
        }

        return token;
    }

    private static List<string> BuildRuntimeGlobalVerbVocabulary(ProjectModel project)
    {
        var verbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddNormalizedTokens(verbs, project.CommandVerbs);
        AddActionVerbs(verbs, project.GlobalScope.AvailableActions);

        AddObjectVerbs(verbs, project.GlobalScope.GameObjects);
        AddObjectVerbs(verbs, project.BaseObjects);

        foreach (var planet in project.Planets)
        {
            AddActionVerbs(verbs, planet.AvailableActions);
            AddObjectVerbs(verbs, planet.GameObjects);
            AddObjectVerbs(verbs, planet.BaseObjects);

            foreach (var country in planet.Countries)
            {
                AddActionVerbs(verbs, country.AvailableActions);
                AddObjectVerbs(verbs, country.GameObjects);
                AddObjectVerbs(verbs, country.BaseObjects);

                foreach (var area in country.Areas)
                {
                    AddActionVerbs(verbs, area.AvailableActions);
                    AddObjectVerbs(verbs, area.GameObjects);
                    AddObjectVerbs(verbs, area.BaseObjects);

                    foreach (var room in area.Rooms)
                    {
                        AddActionVerbs(verbs, room.AvailableActions);
                        AddObjectVerbs(verbs, room.GameObjects);
                    }
                }
            }
        }

        return verbs
            .OrderBy(static verb => verb, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddObjectVerbs(HashSet<string> target, IEnumerable<GameObject>? rootObjects)
    {
        foreach (var gameObject in EnumerateGameObjectsRecursive(rootObjects ?? Enumerable.Empty<GameObject>()))
        {
            AddActionVerbs(target, gameObject.AvailableActions);
            AddNormalizedTokens(target, gameObject.AdditionalVerbs);
        }
    }

    private static void AddActionVerbs(HashSet<string> target, IEnumerable<CommandAction>? actions)
    {
        foreach (var action in actions ?? Enumerable.Empty<CommandAction>())
        {
            foreach (var verb in NormalizeActionVerbs(action.Verbs, action.VerbListText))
            {
                target.Add(verb);
            }
        }
    }

    private static void AddNormalizedTokens(HashSet<string> target, IEnumerable<string>? tokens)
    {
        foreach (var token in tokens ?? Enumerable.Empty<string>())
        {
            var normalized = token?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                target.Add(normalized);
            }
        }
    }

    private static List<string> NormalizeActionVerbs(List<string>? verbs, string? verbListText)
    {
        var source = (verbs is { Count: > 0 }
            ? verbs
            : (verbListText ?? string.Empty)
                .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList())
            .Where(static token => !string.IsNullOrWhiteSpace(token));

        var normalized = new List<string>();
        foreach (var value in source)
        {
            var trimmed = value.Trim();
            if (normalized.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static Guid EnsureScopeId(Guid id, string entityLabel)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidOperationException($"Invalid data: {entityLabel} has an empty Id.");
        }

        return id;
    }

    private static List<T> LoadScopeSidecars<T>(
        string folderPath,
        string fileSuffix,
        JsonSerializerOptions options,
        Func<string, JsonSerializerOptions, T?> deserialize)
        where T : class
    {
        if (!Directory.Exists(folderPath))
        {
            return new List<T>();
        }

        var list = new List<T>();
        foreach (var filePath in Directory.EnumerateFiles(folderPath, $"*{fileSuffix}").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var json = File.ReadAllText(filePath);
            T? item;
            try
            {
                item = deserialize(json, options);
            }
            catch (Exception ex)
            {
                throw BuildDeserializationException(filePath, typeof(T), ex);
            }

            if (item is not null)
            {
                list.Add(item);
            }
        }

        return list;
    }

    private static List<T> LoadScopeSidecarsWithFolderFallback<T>(
        IReadOnlyList<string> folderPaths,
        string fileSuffix,
        JsonSerializerOptions options,
        Func<string, JsonSerializerOptions, T?> deserialize,
        Func<T, Guid> idSelector)
        where T : class
    {
        var values = new List<T>();
        var seenIds = new HashSet<Guid>();

        foreach (var folderPath in folderPaths)
        {
            foreach (var value in LoadScopeSidecars(folderPath, fileSuffix, options, deserialize))
            {
                var id = idSelector(value);
                if (id == Guid.Empty)
                {
                    values.Add(value);
                    continue;
                }

                if (seenIds.Add(id))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    private static ProjectEventSubscriptionDto ToProjectEventSubscriptionDto(EventSubscriptionDefinition definition)
    {
        var hasPerBindingMappings = (definition.ActionBindings ?? new List<EventActionBindingDefinition>())
            .Any(static binding => (binding.InputArgumentMappings?.Count ?? 0) > 0);
        var inputArgumentMappings = (definition.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
            .Select(static mapping =>
            {
                var normalizedMapping = mapping.CloneNormalized();
                normalizedMapping.ToPersisted(out var inputKey, out var outputValue);

                return new ProjectEventInputArgumentMappingDto
                {
                    InputEventPaylloadArgKey = inputKey,
                    OutputActionPayloadArgKey = outputValue
                };
            })
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
            .ToList();

        return new ProjectEventSubscriptionDto
        {
            Id = definition.Id,
            EventKey = definition.EventKey?.Trim() ?? string.Empty,
            SubscriptionName = string.IsNullOrWhiteSpace(definition.SubscriptionName)
                ? null
                : definition.SubscriptionName.Trim(),
            IsEnabled = definition.IsEnabled,
            Lane = string.IsNullOrWhiteSpace(definition.Lane) ? "foreground" : definition.Lane.Trim(),
            DispatchDisposition = definition.DispatchDisposition,
            SubscriptionVisibleWhenContained = definition.SubscriptionVisibleWhenContained,
            SubscriptionSourceMatchMode = definition.SubscriptionSourceMatchMode,
            SubscriptionSourceScopeNodeId = definition.SubscriptionSourceScopeNodeId,
            SubscriptionSecondarySourceMatchMode = definition.SubscriptionSecondarySourceMatchMode,
            SubscriptionSecondarySourceScopeNodeId = definition.SubscriptionSecondarySourceScopeNodeId,
            InputArgumentMappings = hasPerBindingMappings || inputArgumentMappings.Count == 0 ? null : inputArgumentMappings,
            ActionBindings = (definition.ActionBindings ?? new List<EventActionBindingDefinition>())
                .Select(ToProjectEventActionBindingDto)
                .ToList()
        };
    }

    private static ProjectEventActionBindingDto ToProjectEventActionBindingDto(EventActionBindingDefinition binding)
    {
        return new ProjectEventActionBindingDto
        {
            Order = binding.Order,
            IsEnabled = binding.IsEnabled,
            Condition = ToProjectEventBindingConditionDto(binding.Condition),
            Target = ToProjectEventBindingTargetDto(binding.Target),
            InputArgumentMappings = NullIfEmpty((binding.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
                .Select(static mapping =>
                {
                    var normalizedMapping = mapping.CloneNormalized();
                    normalizedMapping.ToPersisted(out var inputKey, out var outputValue);

                    return new ProjectEventInputArgumentMappingDto
                    {
                        InputEventPaylloadArgKey = inputKey,
                        OutputActionPayloadArgKey = outputValue
                    };
                })
                .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
                .ToList())
        };
    }

    private static ProjectEventBindingConditionDto ToProjectEventBindingConditionDto(EventBindingConditionDefinition? condition)
    {
        var safeCondition = condition ?? new EventBindingConditionDefinition();
        return new ProjectEventBindingConditionDto
        {
            QuantityEvaluationMode = safeCondition.QuantityEvaluationMode,
            Filters = (safeCondition.Filters ?? new List<EventBindingFilterConditionDefinition>())
                .Select(ToProjectEventBindingFilterConditionDto)
                .ToList()
        };
    }

    private static ProjectEventBindingFilterConditionDto ToProjectEventBindingFilterConditionDto(EventBindingFilterConditionDefinition filter)
    {
        return new ProjectEventBindingFilterConditionDto
        {
            VariableName = filter.VariableName?.Trim() ?? string.Empty,
            Operator = filter.Operator,
            ExpectedValue = filter.ExpectedValue
        };
    }

    private static ProjectEventBindingTargetDto ToProjectEventBindingTargetDto(EventBindingTargetDefinition? target)
    {
        var safeTarget = target ?? new EventBindingTargetDefinition();
        return new ProjectEventBindingTargetDto
        {
            ActionName = safeTarget.ActionName?.Trim() ?? string.Empty,
            OnMissingAction = string.IsNullOrWhiteSpace(safeTarget.OnMissingAction) ? "DiagnosticOnly" : safeTarget.OnMissingAction.Trim(),
            StopChainOnFailure = safeTarget.StopChainOnFailure
        };
    }

    private static EventSubscriptionDefinition ToEventSubscriptionModel(ProjectEventSubscriptionDto dto)
    {
        var eventKey = dto.EventKey?.Trim() ?? string.Empty;
        var subscriptionName = dto.SubscriptionName?.Trim() ?? string.Empty;
        var legacyMappings = (dto.InputArgumentMappings ?? new List<ProjectEventInputArgumentMappingDto>())
            .Select(static mapping => EventInputArgumentMappingDefinition.FromPersisted(
                mapping.InputEventPaylloadArgKey,
                mapping.OutputActionPayloadArgKey))
            .Where(static mapping =>
                mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
                    ? !string.IsNullOrWhiteSpace(mapping.OutputActionPayloadArgKey)
                    : !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
            .ToList();

        return new EventSubscriptionDefinition
        {
            Id = dto.Id,
            EventKey = eventKey,
            SubscriptionName = string.IsNullOrWhiteSpace(subscriptionName)
                ? eventKey
                : subscriptionName,
            IsEnabled = dto.IsEnabled ?? true,
            Lane = string.IsNullOrWhiteSpace(dto.Lane) ? "foreground" : dto.Lane.Trim(),
            DispatchDisposition = dto.DispatchDisposition ?? EventSubscriberDispatchDisposition.bubble,
            SubscriptionVisibleWhenContained = dto.SubscriptionVisibleWhenContained ?? false,
            SubscriptionSourceMatchMode = dto.SubscriptionSourceMatchMode ?? SubscriptionSourceMatchMode.AnySource,
            SubscriptionSourceScopeNodeId = dto.SubscriptionSourceScopeNodeId,
            SubscriptionSecondarySourceMatchMode = dto.SubscriptionSecondarySourceMatchMode ?? SubscriptionSourceMatchMode.AnySource,
            SubscriptionSecondarySourceScopeNodeId = dto.SubscriptionSecondarySourceScopeNodeId,
            InputArgumentMappings = legacyMappings,
            ActionBindings = (dto.ActionBindings ?? new List<ProjectEventActionBindingDto>())
                .Select(binding => ToEventActionBindingModel(binding, legacyMappings))
                .ToList()
        };
    }

    private static EventActionBindingDefinition ToEventActionBindingModel(
        ProjectEventActionBindingDto dto,
        IReadOnlyList<EventInputArgumentMappingDefinition> legacyMappings)
    {
        var mappedBindings = (dto.InputArgumentMappings ?? new List<ProjectEventInputArgumentMappingDto>())
            .Select(static mapping => EventInputArgumentMappingDefinition.FromPersisted(
                mapping.InputEventPaylloadArgKey,
                mapping.OutputActionPayloadArgKey))
            .Where(static mapping =>
                mapping.SourceType == EventInputArgumentMappingSourceType.ConstantValue
                    ? !string.IsNullOrWhiteSpace(mapping.OutputActionPayloadArgKey)
                    : !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
            .ToList();
        if (mappedBindings.Count == 0 && legacyMappings.Count > 0)
        {
            mappedBindings = legacyMappings
                .Select(static mapping => new EventInputArgumentMappingDefinition
                {
                    InputEventPaylloadArgKey = mapping.InputEventPaylloadArgKey,
                    OutputActionPayloadArgKey = mapping.OutputActionPayloadArgKey
                })
                .ToList();
        }

        return new EventActionBindingDefinition
        {
            Order = dto.Order,
            IsEnabled = dto.IsEnabled ?? true,
            Condition = ToEventBindingConditionModel(dto.Condition),
            Target = ToEventBindingTargetModel(dto.Target),
            InputArgumentMappings = mappedBindings
        };
    }

    private static EventBindingConditionDefinition ToEventBindingConditionModel(ProjectEventBindingConditionDto? dto)
    {
        var safeDto = dto ?? new ProjectEventBindingConditionDto();
        return new EventBindingConditionDefinition
        {
            QuantityEvaluationMode = safeDto.QuantityEvaluationMode,
            Filters = (safeDto.Filters ?? new List<ProjectEventBindingFilterConditionDto>())
                .Select(ToEventBindingFilterConditionModel)
                .ToList()
        };
    }

    private static EventBindingFilterConditionDefinition ToEventBindingFilterConditionModel(ProjectEventBindingFilterConditionDto dto)
    {
        return new EventBindingFilterConditionDefinition
        {
            VariableName = dto.VariableName?.Trim() ?? string.Empty,
            Operator = dto.Operator,
            ExpectedValue = dto.ExpectedValue
        };
    }

    private static EventBindingTargetDefinition ToEventBindingTargetModel(ProjectEventBindingTargetDto? dto)
    {
        var safeDto = dto ?? new ProjectEventBindingTargetDto();
        return new EventBindingTargetDefinition
        {
            ActionName = safeDto.ActionName?.Trim() ?? string.Empty,
            OnMissingAction = string.IsNullOrWhiteSpace(safeDto.OnMissingAction) ? "DiagnosticOnly" : safeDto.OnMissingAction.Trim(),
            StopChainOnFailure = safeDto.StopChainOnFailure
        };
    }

    private static RuntimeEventSubscriptionDto ToRuntimeEventSubscriptionDto(EventSubscriptionDefinition definition)
    {
        var eventKey = definition.EventKey?.Trim() ?? string.Empty;
        var hasPerBindingMappings = (definition.ActionBindings ?? new List<EventActionBindingDefinition>())
            .Any(static binding => (binding.InputArgumentMappings?.Count ?? 0) > 0);
        var inputArgumentMappings = (definition.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
            .Select(static mapping =>
            {
                var normalizedMapping = mapping.CloneNormalized();
                normalizedMapping.ToPersisted(out var inputKey, out var outputValue);

                return new RuntimeEventInputArgumentMappingDto
                {
                    InputEventPaylloadArgKey = inputKey,
                    OutputActionPayloadArgKey = outputValue
                };
            })
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
            .ToList();

        return new RuntimeEventSubscriptionDto
        {
            Id = definition.Id,
            EventKey = eventKey,
            IsEnabled = definition.IsEnabled,
            Lane = string.IsNullOrWhiteSpace(definition.Lane) ? "foreground" : definition.Lane.Trim(),
            DispatchDisposition = definition.DispatchDisposition,
            SubscriptionVisibleWhenContained = definition.SubscriptionVisibleWhenContained,
            SubscriptionSourceMatchMode = definition.SubscriptionSourceMatchMode,
            SubscriptionSourceScopeNodeId = definition.SubscriptionSourceScopeNodeId,
            SubscriptionSecondarySourceMatchMode = definition.SubscriptionSecondarySourceMatchMode,
            SubscriptionSecondarySourceScopeNodeId = definition.SubscriptionSecondarySourceScopeNodeId,
            InputArgumentMappings = hasPerBindingMappings || inputArgumentMappings.Count == 0 ? null : inputArgumentMappings,
            ActionBindings = (definition.ActionBindings ?? new List<EventActionBindingDefinition>())
                .Select(binding => ToRuntimeEventActionBindingDto(binding, definition.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>()))
                .ToList()
        };
    }

    private static RuntimeEventActionBindingDto ToRuntimeEventActionBindingDto(
        EventActionBindingDefinition binding,
        IReadOnlyList<EventInputArgumentMappingDefinition> legacyMappings)
    {
        var effectiveMappings = (binding.InputArgumentMappings ?? new List<EventInputArgumentMappingDefinition>())
            .Select(static mapping =>
            {
                var normalizedMapping = mapping.CloneNormalized();
                normalizedMapping.ToPersisted(out var inputKey, out var outputValue);

                return new RuntimeEventInputArgumentMappingDto
                {
                    InputEventPaylloadArgKey = inputKey,
                    OutputActionPayloadArgKey = outputValue
                };
            })
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
            .ToList();
        if (effectiveMappings.Count == 0 && legacyMappings.Count > 0)
        {
            effectiveMappings = legacyMappings
                .Select(static mapping =>
                {
                    var normalizedMapping = mapping.CloneNormalized();
                    normalizedMapping.ToPersisted(out var inputKey, out var outputValue);

                    return new RuntimeEventInputArgumentMappingDto
                    {
                        InputEventPaylloadArgKey = inputKey,
                        OutputActionPayloadArgKey = outputValue
                    };
                })
                .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.InputEventPaylloadArgKey))
                .ToList();
        }

        return new RuntimeEventActionBindingDto
        {
            Order = binding.Order,
            IsEnabled = binding.IsEnabled,
            Condition = new RuntimeEventBindingConditionDto
            {
                QuantityEvaluationMode = binding.Condition?.QuantityEvaluationMode ?? RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass,
                Filters = (binding.Condition?.Filters ?? new List<EventBindingFilterConditionDefinition>())
                    .Select(filter => new RuntimeEventBindingFilterConditionDto
                    {
                        VariableName = filter.VariableName?.Trim() ?? string.Empty,
                        Operator = filter.Operator,
                        ExpectedValue = filter.ExpectedValue
                    })
                    .ToList()
            },
            Target = new RuntimeEventBindingTargetDto
            {
                ActionName = binding.Target?.ActionName?.Trim() ?? string.Empty,
                OnMissingAction = string.IsNullOrWhiteSpace(binding.Target?.OnMissingAction) ? "DiagnosticOnly" : binding.Target.OnMissingAction.Trim(),
                StopChainOnFailure = binding.Target?.StopChainOnFailure ?? true
            },
            InputArgumentMappings = NullIfEmpty(effectiveMappings)
        };
    }

    private static ProjectTimerDefinitionDto ToProjectTimerDefinitionDto(RuntimeTimerDefinitionDto definition)
    {
        return new ProjectTimerDefinitionDto
        {
            TimerKey = definition.TimerKey?.Trim() ?? string.Empty,
            ScheduleAfterMs = definition.ScheduleAfterMs,
            FireMode = definition.FireMode,
            RepeatMode = definition.RepeatMode,
            RepeatProgressionMode = definition.RepeatProgressionMode,
            RepeatIntervalMs = definition.RepeatIntervalMs,
            RepeatIntervalStepMs = definition.RepeatIntervalStepMs,
            RepeatProgressionRate = definition.RepeatProgressionRate,
            RepeatIntervalMinMs = definition.RepeatIntervalMinMs,
            ShrinkingExpiresUnderMs = definition.ShrinkingExpiresUnderMs,
            TargetActionRef = definition.TargetActionRef?.Trim() ?? string.Empty,
            OnShrinkExpiryActionRef = string.IsNullOrWhiteSpace(definition.OnShrinkExpiryActionRef)
                ? null
                : definition.OnShrinkExpiryActionRef.Trim(),
            LifetimeOwnerType = definition.LifetimeOwnerType,
            ConflictBehavior = definition.ConflictBehavior,
            Enabled = definition.Enabled
        };
    }

    private static RuntimeTimerDefinitionDto ToTimerDefinitionModel(ProjectTimerDefinitionDto dto)
    {
        return new RuntimeTimerDefinitionDto
        {
            TimerKey = dto.TimerKey?.Trim() ?? string.Empty,
            ScheduleAfterMs = dto.ScheduleAfterMs,
            FireMode = dto.FireMode,
            RepeatMode = dto.RepeatMode,
            RepeatProgressionMode = dto.RepeatProgressionMode,
            RepeatIntervalMs = dto.RepeatIntervalMs,
            RepeatIntervalStepMs = dto.RepeatIntervalStepMs,
            RepeatProgressionRate = dto.RepeatProgressionRate,
            RepeatIntervalMinMs = dto.RepeatIntervalMinMs,
            ShrinkingExpiresUnderMs = dto.ShrinkingExpiresUnderMs,
            TargetActionRef = dto.TargetActionRef?.Trim() ?? string.Empty,
            OnShrinkExpiryActionRef = string.IsNullOrWhiteSpace(dto.OnShrinkExpiryActionRef)
                ? null
                : dto.OnShrinkExpiryActionRef.Trim(),
            LifetimeOwnerType = dto.LifetimeOwnerType,
            ConflictBehavior = dto.ConflictBehavior,
            Enabled = dto.Enabled
        };
    }

    private static RuntimeTimerDefinitionDto ToRuntimeTimerDefinitionDto(RuntimeTimerDefinitionDto definition)
    {
        return new RuntimeTimerDefinitionDto
        {
            TimerKey = definition.TimerKey?.Trim() ?? string.Empty,
            ScheduleAfterMs = definition.ScheduleAfterMs,
            FireMode = definition.FireMode,
            RepeatMode = definition.RepeatMode,
            RepeatProgressionMode = definition.RepeatProgressionMode,
            RepeatIntervalMs = definition.RepeatIntervalMs,
            RepeatIntervalStepMs = definition.RepeatIntervalStepMs,
            RepeatProgressionRate = definition.RepeatProgressionRate,
            RepeatIntervalMinMs = definition.RepeatIntervalMinMs,
            ShrinkingExpiresUnderMs = definition.ShrinkingExpiresUnderMs,
            TargetActionRef = definition.TargetActionRef?.Trim() ?? string.Empty,
            OnShrinkExpiryActionRef = string.IsNullOrWhiteSpace(definition.OnShrinkExpiryActionRef)
                ? null
                : definition.OnShrinkExpiryActionRef.Trim(),
            LifetimeOwnerType = definition.LifetimeOwnerType,
            ConflictBehavior = definition.ConflictBehavior,
            Enabled = definition.Enabled
        };
    }

    private static Planet BuildPlanetModel(
        PlanetDto dto,
        IReadOnlyDictionary<Guid, CountryDto> countriesById,
        IReadOnlyDictionary<Guid, AreaDto> areasById,
        Dictionary<Guid, Room> roomMap,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById)
    {
        var resolvedPlanetBaseObjects = ResolveScopedObjectDtos(dto.BaseObjectIds, dto.BaseObjects, objectDtosById);
        var resolvedPlanetObjects = ResolveScopedObjectDtos(dto.GameObjectIds, dto.GameObjects, objectDtosById);

        return new Planet
        {
            Id = EnsureScopeId(dto.Id, $"planet '{dto.Name}'"),
            Name = dto.Name,
            HideEmptyConfiguration = dto.HideEmptyConfiguration,
            ValidationErrors = (dto.ValidationErrors ?? new List<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList(),
            StartingCountryName = dto.StartingCountryName,
            Variables = (dto.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel).ToList(),
            AdditionalVerbs = (dto.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (dto.AdditionalDirectionals ?? new List<string>()).ToList(),
            IgnoredValidationRuleIds = (dto.IgnoredValidationRuleIds ?? new List<string>()).ToList(),
            SoundEffectLibraryEntries = (dto.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            BaseObjectsIgnoredValidationRuleIds = (dto.BaseObjectsIgnoredValidationRuleIds ?? new List<string>()).ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappings(dto.AdditionalDirectionalTraversalMappings),
            AvailableActions = ToCommandActionModels(dto.AvailableGameActions),
            EventSubscriptions = (dto.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>()).Select(ToEventSubscriptionModel).ToList(),
            TimerDefinitions = (dto.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>()).Select(ToTimerDefinitionModel).ToList(),
            BaseObjects = resolvedPlanetBaseObjects.Select(ToGameObjectModel).ToList(),
            GameObjects = resolvedPlanetObjects.Select(ToGameObjectModel).ToList(),
            Countries = dto.CountryIds
                .Select(countryId => countriesById.TryGetValue(countryId, out var country) ? country : null)
                .Where(static country => country is not null)
                .Select(country => BuildCountryModel(country!, areasById, roomMap, objectDtosById))
                .ToList()
        };
    }

    private static Country BuildCountryModel(
        CountryDto dto,
        IReadOnlyDictionary<Guid, AreaDto> areasById,
        Dictionary<Guid, Room> roomMap,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById)
    {
        var resolvedCountryBaseObjects = ResolveScopedObjectDtos(dto.BaseObjectIds, dto.BaseObjects, objectDtosById);
        var resolvedCountryObjects = ResolveScopedObjectDtos(dto.GameObjectIds, dto.GameObjects, objectDtosById);

        return new Country
        {
            Id = EnsureScopeId(dto.Id, $"country '{dto.Name}'"),
            Name = dto.Name,
            HideEmptyConfiguration = dto.HideEmptyConfiguration,
            ValidationErrors = (dto.ValidationErrors ?? new List<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList(),
            StartingAreaName = dto.StartingAreaName,
            Variables = (dto.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel).ToList(),
            AdditionalVerbs = (dto.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (dto.AdditionalDirectionals ?? new List<string>()).ToList(),
            IgnoredValidationRuleIds = (dto.IgnoredValidationRuleIds ?? new List<string>()).ToList(),
            SoundEffectLibraryEntries = (dto.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            BaseObjectsIgnoredValidationRuleIds = (dto.BaseObjectsIgnoredValidationRuleIds ?? new List<string>()).ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappings(dto.AdditionalDirectionalTraversalMappings),
            AvailableActions = ToCommandActionModels(dto.AvailableGameActions),
            EventSubscriptions = (dto.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>()).Select(ToEventSubscriptionModel).ToList(),
            TimerDefinitions = (dto.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>()).Select(ToTimerDefinitionModel).ToList(),
            BaseObjects = resolvedCountryBaseObjects.Select(ToGameObjectModel).ToList(),
            GameObjects = resolvedCountryObjects.Select(ToGameObjectModel).ToList(),
            Areas = dto.AreaIds
                .Select(areaId => areasById.TryGetValue(areaId, out var area) ? area : null)
                .Where(static area => area is not null)
                .Select(area => BuildAreaModel(area!, roomMap, objectDtosById))
                .ToList()
        };
    }

    private static Area BuildAreaModel(
        AreaDto dto,
        Dictionary<Guid, Room> roomMap,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById)
    {
        var traversalConnections = (dto.TraversalConnections ?? new List<TraversalConnectionDto>())
            .Select(ToTraversalConnectionModel)
            .ToList();
        var resolvedAreaBaseObjects = ResolveScopedObjectDtos(dto.BaseObjectIds, dto.BaseObjects, objectDtosById);
        var resolvedAreaObjects = ResolveScopedObjectDtos(dto.GameObjectIds, dto.GameObjects, objectDtosById);

        return new Area
        {
            Id = EnsureScopeId(dto.Id, $"area '{dto.Name}'"),
            Name = dto.Name,
            HideEmptyConfiguration = dto.HideEmptyConfiguration,
            ValidationErrors = (dto.ValidationErrors ?? new List<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList(),
            AdjacencyMode = dto.AdjacencyMode,
            RoomDropBehavior = dto.RoomDropBehavior,
            TraversalModeOverride = Enum.TryParse<AreaAdjacencyMode>(dto.TraversalModeOverride, out var traversalModeOverride)
                ? traversalModeOverride
                : null,
            StartingRoomId = dto.StartingRoomId,
            Variables = (dto.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel).ToList(),
            AdditionalVerbs = (dto.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (dto.AdditionalDirectionals ?? new List<string>()).ToList(),
            IgnoredValidationRuleIds = (dto.IgnoredValidationRuleIds ?? new List<string>()).ToList(),
            SoundEffectLibraryEntries = (dto.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            BaseObjectsIgnoredValidationRuleIds = (dto.BaseObjectsIgnoredValidationRuleIds ?? new List<string>()).ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappings(dto.AdditionalDirectionalTraversalMappings),
            AvailableActions = ToCommandActionModels(dto.AvailableGameActions),
            EventSubscriptions = (dto.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>()).Select(ToEventSubscriptionModel).ToList(),
            TimerDefinitions = (dto.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>()).Select(ToTimerDefinitionModel).ToList(),
            BaseObjects = resolvedAreaBaseObjects.Select(ToGameObjectModel).ToList(),
            GameObjects = resolvedAreaObjects.Select(ToGameObjectModel).ToList(),
            Rooms = ResolveAreaRooms(dto, roomMap),
            Links = new List<RoomLink>(),
            TraversalConnections = traversalConnections,
            RoomPlacements = (dto.RoomPlacements ?? new List<RoomPlacementDto>()).Select(placement => new AreaRoomPlacement
            {
                RoomId = placement.RoomId,
                X = placement.X,
                Y = placement.Y,
                FloorElevation = placement.FloorElevation ?? 0
            }).ToList()
        };
    }

    private static List<Room> ResolveAreaRooms(AreaDto area, Dictionary<Guid, Room> roomMap)
    {
        var resolved = new List<Room>();
        foreach (var roomId in area.RoomIds)
        {
            if (roomMap.TryGetValue(roomId, out var room))
            {
                resolved.Add(room);
            }
        }

        return resolved;
    }

    private static IReadOnlyList<RuntimeRoomLinkDto> BuildRuntimeRoomLinks(IEnumerable<TraversalConnection> connections)
    {
        var links = new List<RuntimeRoomLinkDto>();

        foreach (var connection in connections)
        {
            if (connection.RoomAId == Guid.Empty || connection.RoomBId == Guid.Empty)
            {
                continue;
            }

            if (connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayAtoB)
            {
                links.Add(BuildRuntimeRoomLink(
                    fromRoomId: connection.RoomAId,
                    toRoomId: connection.RoomBId,
                    direction: connection.BaseTraversalDirectionFromA,
                    state: connection.TraversalStateFromA,
                    openStateBindingMode: connection.OpenStateBindingMode,
                    pairedOpenableObjectId: connection.TraversalStateFromB?.OpenableObjectId,
                    presentationEffectKey: connection.PresentationEffectKey));
            }

            if (connection.TraversalAccessMode is TraversalAccessMode.TwoWay or TraversalAccessMode.OneWayBtoA)
            {
                links.Add(BuildRuntimeRoomLink(
                    fromRoomId: connection.RoomBId,
                    toRoomId: connection.RoomAId,
                    direction: InvertDirection(connection.BaseTraversalDirectionFromA),
                    state: connection.TraversalStateFromB,
                    openStateBindingMode: connection.OpenStateBindingMode,
                    pairedOpenableObjectId: connection.TraversalStateFromA?.OpenableObjectId,
                    presentationEffectKey: connection.PresentationEffectKey));
            }
        }

        return links;
    }

    private static RuntimeRoomLinkDto BuildRuntimeRoomLink(
        Guid fromRoomId,
        Guid toRoomId,
        Direction10 direction,
        TraversalLegState? state,
        OpenStateBindingMode openStateBindingMode,
        Guid? pairedOpenableObjectId,
        string? presentationEffectKey)
    {
        var normalizedState = state ?? new TraversalLegState();
        var gameProperties = BuildRuntimeLinkGameProperties(normalizedState);

        return new RuntimeRoomLinkDto
        {
            FromRoomId = fromRoomId,
            ToRoomId = toRoomId,
            Direction = direction,
            GameProperties = gameProperties,
            OpenableObjectId = normalizedState.OpenableObjectId,
            OpenStatePolicy = normalizedState.OpenStatePolicy,
            OpenStateBindingMode = openStateBindingMode,
            PairedOpenableObjectId = pairedOpenableObjectId,
            PresentationEffectKey = presentationEffectKey
        };
    }

    private static List<RuntimeGamePropertyDefinitionDto> BuildRuntimeLinkGameProperties(TraversalLegState state)
    {
        var normalizedVariables = NormalizeTraversalLegVariables(state.Variables ?? new List<GamePropertyDefinition>());
        var passableVariable = normalizedVariables.First(variable =>
            string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase));

        if (!passableVariable.SharedVariableId.HasValue && state.SharedVariableId.HasValue)
        {
            passableVariable.SharedVariableId = state.SharedVariableId;
        }

        return normalizedVariables
            .Select(ToCleanGamePropertyDto)
            .ToList();
    }

    private static Direction10 InvertDirection(Direction10 direction)
    {
        return direction switch
        {
            Direction10.North => Direction10.South,
            Direction10.NorthEast => Direction10.SouthWest,
            Direction10.East => Direction10.West,
            Direction10.SouthEast => Direction10.NorthWest,
            Direction10.South => Direction10.North,
            Direction10.SouthWest => Direction10.NorthEast,
            Direction10.West => Direction10.East,
            Direction10.NorthWest => Direction10.SouthEast,
            Direction10.Up => Direction10.Down,
            Direction10.Down => Direction10.Up,
            _ => Direction10.North
        };
    }

    private static List<TraversalConnection> BuildTraversalConnectionsForPersistence(Area area)
    {
        return area.TraversalConnections;
    }

    private static TraversalConnectionDto ToTraversalConnectionDto(TraversalConnection connection)
    {
        return new TraversalConnectionDto
        {
            TraversalConnectionId = connection.TraversalConnectionId,
            RoomAId = connection.RoomAId,
            RoomBId = connection.RoomBId,
            BaseTraversalDirectionFromA = connection.BaseTraversalDirectionFromA,
            TraversalModeOverride = connection.TraversalModeOverride,
            TraversalAccessMode = connection.TraversalAccessMode,
            PresentationEffectKey = connection.PresentationEffectKey,
            TraversalStateFromA = ToTraversalLegStateDto(connection.TraversalStateFromA),
            TraversalStateFromB = ToTraversalLegStateDto(connection.TraversalStateFromB),
            OpenStateBindingMode = connection.OpenStateBindingMode,
            GameProperties = NormalizeTraversalConnectionVariables(connection.Variables)
                .Select(ToVariableDto)
                .ToList()
        };
    }

    private static TraversalConnection ToTraversalConnectionModel(TraversalConnectionDto dto)
    {
        var traversalVariables = (dto.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel).ToList();
        var traversalStateFromA = ToTraversalLegStateModel(dto.TraversalStateFromA);
        var traversalStateFromB = ToTraversalLegStateModel(dto.TraversalStateFromB);

        ApplyTraversalPassableVariableCompatibilityToLegs(traversalVariables, traversalStateFromA, traversalStateFromB);

        return new TraversalConnection
        {
            TraversalConnectionId = EnsureScopeId(dto.TraversalConnectionId, "traversal connection"),
            RoomAId = dto.RoomAId,
            RoomBId = dto.RoomBId,
            BaseTraversalDirectionFromA = Enum.IsDefined(dto.BaseTraversalDirectionFromA)
                ? dto.BaseTraversalDirectionFromA
                : Direction10.North,
            TraversalModeOverride = dto.TraversalModeOverride.HasValue && Enum.IsDefined(dto.TraversalModeOverride.Value)
                ? dto.TraversalModeOverride.Value
                : null,
            TraversalAccessMode = Enum.IsDefined(dto.TraversalAccessMode)
                ? dto.TraversalAccessMode
                : TraversalAccessMode.TwoWay,
            PresentationEffectKey = dto.PresentationEffectKey,
            TraversalStateFromA = traversalStateFromA,
            TraversalStateFromB = traversalStateFromB,
            OpenStateBindingMode = ParseOpenStateBindingMode(dto.OpenStateBindingMode),
            Variables = NormalizeTraversalConnectionVariables(traversalVariables)
        };
    }

    private static OpenStateBindingMode ParseOpenStateBindingMode(OpenStateBindingMode? value)
    {
        return value.HasValue && Enum.IsDefined(value.Value)
            ? value.Value
            : OpenStateBindingMode.Independent;
    }

    private static OpenStateBindingMode ParseOpenStateBindingMode(string? value)
    {
        if (string.Equals(value?.Trim(), "SharedWithPairedOpenable", StringComparison.OrdinalIgnoreCase))
        {
            return OpenStateBindingMode.Together;
        }

        return Enum.TryParse<OpenStateBindingMode>(value, out var parsed)
            ? parsed
            : OpenStateBindingMode.Independent;
    }

    private static List<GamePropertyDefinition> NormalizeTraversalConnectionVariables(IEnumerable<GamePropertyDefinition> variables)
    {
        return variables
            .Where(variable => !string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void ApplyTraversalPassableVariableCompatibilityToLegs(
        IReadOnlyCollection<GamePropertyDefinition> traversalVariables,
        TraversalLegState legA,
        TraversalLegState legB)
    {
        var traversalPassableVariable = traversalVariables.FirstOrDefault(variable =>
            string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase));
        if (traversalPassableVariable is null)
        {
            return;
        }

        var defaultValue = string.IsNullOrWhiteSpace(traversalPassableVariable.DefaultValue)
            ? "true"
            : traversalPassableVariable.DefaultValue;

        ApplyPassableMigrationToLeg(legA, defaultValue, traversalPassableVariable.SharedVariableId);
        ApplyPassableMigrationToLeg(legB, defaultValue, traversalPassableVariable.SharedVariableId);
    }

    private static void ApplyPassableMigrationToLeg(TraversalLegState leg, string defaultValue, Guid? sharedVariableId)
    {
        var passableVariable = leg.Variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase));
        if (passableVariable is null)
        {
            passableVariable = new GamePropertyDefinition
            {
                Name = TraversalPassableVariableName,
                DefaultValue = defaultValue,
                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                Lifetime = GamePropertyLifetime.Singleton,
                SharedVariableId = sharedVariableId
            };
            leg.Variables.Add(passableVariable);
        }
        else
        {
            passableVariable.DefaultValue = defaultValue;
            passableVariable.ValueRestriction = GamePropertyValueRestriction.TrueFalse;
            passableVariable.Lifetime = GamePropertyLifetime.Singleton;
            passableVariable.SharedVariableId = sharedVariableId;
        }
    }

    private static TraversalLegStateDto ToTraversalLegStateDto(TraversalLegState state)
    {
        var normalizedVariables = NormalizeTraversalLegVariables(state.Variables);
        var passableVariable = normalizedVariables.FirstOrDefault(variable =>
            string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase));
        if (passableVariable is not null && !passableVariable.SharedVariableId.HasValue && state.SharedVariableId.HasValue)
        {
            passableVariable.SharedVariableId = state.SharedVariableId;
        }

        return new TraversalLegStateDto
        {
            OpenableObjectId = state.OpenableObjectId,
            OpenStatePolicy = state.OpenStatePolicy,
            SharedVariableId = state.SharedVariableId,
            AvailableGameActions = ToCommandActionDtos(state.AvailableActions),
            GameProperties = normalizedVariables
                .Select(ToVariableDto)
                .ToList()
        };
    }

    private static TraversalLegState ToTraversalLegStateModel(TraversalLegStateDto? dto)
    {
        if (dto is null)
        {
            return new TraversalLegState();
        }

        var variables = NormalizeTraversalLegVariables((dto.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel));
        var passableVariable = variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase));
        if (passableVariable is not null && !passableVariable.SharedVariableId.HasValue && dto.SharedVariableId.HasValue)
        {
            passableVariable.SharedVariableId = dto.SharedVariableId;
        }

        return new TraversalLegState
        {
            OpenableObjectId = dto.OpenableObjectId,
            OpenStatePolicy = dto.OpenStatePolicy.HasValue && Enum.IsDefined(dto.OpenStatePolicy.Value)
                ? dto.OpenStatePolicy.Value
                : OpenablePolicy.IgnoreOpenableState,
            SharedVariableId = dto.SharedVariableId,
            AvailableActions = ToCommandActionModels(dto.AvailableGameActions),
            Variables = variables
        };
    }

    private static List<GamePropertyDefinition> NormalizeTraversalLegVariables(IEnumerable<GamePropertyDefinition> variables)
    {
        var existingPassable = variables.FirstOrDefault(variable =>
            string.Equals(variable.Name, TraversalPassableVariableName, StringComparison.OrdinalIgnoreCase));

        if (existingPassable is not null)
        {
            existingPassable.Name = TraversalPassableVariableName;
            existingPassable.ValueRestriction = GamePropertyValueRestriction.TrueFalse;
            existingPassable.Lifetime = GamePropertyLifetime.Singleton;
            if (string.IsNullOrWhiteSpace(existingPassable.DefaultValue))
            {
                existingPassable.DefaultValue = "true";
            }

            return new List<GamePropertyDefinition> { existingPassable };
        }

        return new List<GamePropertyDefinition>
        {
            new()
            {
                Name = TraversalPassableVariableName,
                DefaultValue = "true",
                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                Lifetime = GamePropertyLifetime.Singleton
            }
        };
    }

    private static List<DirectionalTraversalMappingDto> ToDirectionalTraversalMappingDtos(IEnumerable<DirectionalTraversalMapping> mappings)
    {
        return mappings
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.Token))
            .GroupBy(static mapping => mapping.Token.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(static group => new DirectionalTraversalMappingDto
            {
                Token = group.Key,
                TraversalDirection = group.Last().TraversalDirection
            })
            .OrderBy(static mapping => mapping.Token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<RuntimeDirectionalTraversalMapping> ToRuntimeDirectionalTraversalMappings(IEnumerable<DirectionalTraversalMapping> mappings)
    {
        return mappings
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.Token))
            .GroupBy(static mapping => mapping.Token.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(static group => new RuntimeDirectionalTraversalMapping(group.Key, group.Last().TraversalDirection))
            .OrderBy(static mapping => mapping.Token, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static mapping => mapping.TraversalDirection)
            .ToList();
    }

    private static List<DirectionalTraversalMapping> ToDirectionalTraversalMappings(IEnumerable<DirectionalTraversalMappingDto>? mappings)
    {
        if (mappings is null)
        {
            return new List<DirectionalTraversalMapping>();
        }

        return mappings
            .Where(static mapping => !string.IsNullOrWhiteSpace(mapping.Token))
            .Select(static mapping => new DirectionalTraversalMapping
            {
                Token = mapping.Token.Trim(),
                TraversalDirection = mapping.TraversalDirection
            })
            .GroupBy(static mapping => mapping.Token, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .OrderBy(static mapping => mapping.Token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Room ToRoomModel(
        RoomDto room,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById,
        int defaultRoomCanvasWidth,
        int defaultRoomCanvasHeight)
    {
        var availableActions = BuildRoomAvailableActions(room);
        var roomDisplayMode = Enum.IsDefined(room.RoomDisplayMode)
            ? room.RoomDisplayMode
            : InferLegacyRoomDisplayMode(room.Images);
        var resolvedRoomObjects = ResolveScopedObjectDtos(room.GameObjectIds, room.GameObjects, objectDtosById);
        var roomObjects = resolvedRoomObjects.Select(ToGameObjectModel).ToList();

        if (resolvedRoomObjects.All(static obj => !obj.RenderZOrder.HasValue))
        {
            ApplyDerivedRoomRenderZOrders(roomObjects);
        }

        return new Room
        {
            Id = EnsureScopeId(room.Id, $"room '{room.Name}'"),
            DesignerPersistenceScopeKind = room.ScopeKind,
            Name = room.Name,
            NameInGame = room.NameInGame,
            HideEmptyConfiguration = room.HideEmptyConfiguration,
            Description = room.Description,
            ProducerNotes = room.ProducerNotes ?? string.Empty,
            RoomImageCanvasWidth = room.RoomImageCanvasWidth.GetValueOrDefault() > 0
                ? room.RoomImageCanvasWidth!.Value
                : (defaultRoomCanvasWidth > 0 ? defaultRoomCanvasWidth : 800),
            RoomImageCanvasHeight = room.RoomImageCanvasHeight.GetValueOrDefault() > 0
                ? room.RoomImageCanvasHeight!.Value
                : (defaultRoomCanvasHeight > 0 ? defaultRoomCanvasHeight : 600),
            RoomDisplayMode = roomDisplayMode,
            ValidationErrors = (room.ValidationErrors ?? new List<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList(),
            TraversalModeOverride = Enum.TryParse<AreaAdjacencyMode>(room.TraversalModeOverride, out var traversalModeOverride)
                ? traversalModeOverride
                : null,
            AvailableActions = new ObservableCollection<CommandAction>(availableActions),
            Commands = new List<string>(),
            GameObjects = roomObjects,
            Images = room.Images.Select(i => new RoomImageEntry
            {
                Slot = i.Slot,
                OverlayRenderOrder = i.OverlayRenderOrder > 0
                    ? i.OverlayRenderOrder
                    : RoomImageEntry.GetDefaultOverlayRenderOrder(i.Slot),
                OverlayOffsetX = i.OverlayOffsetX,
                OverlayOffsetY = i.OverlayOffsetY,
                OverlayRotationDegrees = i.OverlayRotationDegrees,
                Image = new RoomImageVariant
                {
                    FullImagePath = i.FullImagePath,
                    GrayMapImagePath = i.GrayMapImagePath,
                    NormalMapImagePath = i.NormalMapImagePath
                }
            }).ToList(),
            Variables = (room.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel).ToList(),
            AdditionalVerbs = (room.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (room.AdditionalDirectionals ?? new List<string>()).ToList(),
            IgnoredValidationRuleIds = (room.IgnoredValidationRuleIds ?? new List<string>()).ToList(),
            SoundEffectLibraryEntries = (room.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappings(room.AdditionalDirectionalTraversalMappings),
            EventSubscriptions = (room.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>())
                .Select(ToEventSubscriptionModel)
                .ToList(),
            TimerDefinitions = (room.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>())
                .Select(ToTimerDefinitionModel)
                .ToList()
        };
    }

    private static RuntimeRoomImageDisplayMode InferLegacyRoomDisplayMode(IEnumerable<RoomImageDto> images)
    {
        return images.Any(static image =>
            Enum.TryParse<RuntimeRoomImageDisplayMode>(image.DisplayMode, out var parsed)
            && parsed == RuntimeRoomImageDisplayMode.Overlay)
            ? RuntimeRoomImageDisplayMode.Overlay
            : RuntimeRoomImageDisplayMode.Independent;
    }

    private static ProjectGameObjectDto ToProjectGameObjectDto(
        GameObject obj,
        Guid? designTimeParentObjectId,
        int? renderZOrderOverride = null,
        ScopeNodeKind? scopeKindOverride = null)
    {
        var persistedLinkedBaseObjectId = NormalizePersistedLinkedBaseObjectId(obj);
        var normalizedVariants = NormalizeObjectImageVariants(obj.ImageVariants);
        return new ProjectGameObjectDto
        {
            Id = obj.ObjectId,
            ScopeKind = scopeKindOverride ?? obj.DesignerPersistenceScopeKind ?? ScopeNodeKind.GameObject,
            DesignTimeParentObjectId = designTimeParentObjectId,
            Name = obj.Name,
            HideEmptyConfiguration = obj.HideEmptyConfiguration,
            ObjectType = string.IsNullOrWhiteSpace(obj.ObjectType) ? null : obj.ObjectType,
            ProductionName = obj.ProducerNotes ?? string.Empty,
            NameInGame = string.IsNullOrWhiteSpace(obj.NameInGame) ? string.Empty : obj.NameInGame,
            NameSynonyms = NormalizeNameSynonyms(obj.NameSynonyms),
            ValidationErrors = obj.ValidationErrors.ToList(),
            IsInventoriable = obj.IsInventoriable,
            InventoryPointsDefaultValue = obj.InventoryPointsDefaultValue,
            IsContainer = obj.IsContainer,
            ContainerPointsDefaultValue = obj.ContainerPointsDefaultValue,
            IsOpenable = obj.IsOpenable,
            IsOpenDefaultValue = obj.IsOpenDefaultValue,
            IsLockable = obj.IsLockable,
            IsLockedDefaultValue = obj.IsLockedDefaultValue,
            IsActivatable = obj.IsActivatable,
            IsActiveDefaultValue = obj.IsActiveDefaultValue,
            IsHidable = obj.IsHidable,
            IsHiddenDefaultValue = obj.IsHiddenDefaultValue,
            IsQuantifiable = obj.IsQuantifiable,
            Quantity = obj.Quantity,
            QuantifiablePlacementDistributionMode = ParseQuantifiablePlacementDistributionMode(obj.QuantifiablePlacementDistributionMode),
            LinkedBaseObjectId = persistedLinkedBaseObjectId,
            LinkActionsToBaseObject = obj.LinkActionsToBaseObject,
            CompositeRecipe = ToProjectCompositeRecipeDto(obj),
            LockOperationRequirements = ToProjectLockOperationRequirementsDto(obj.LockOperationRequirements),
            ImageVariants = normalizedVariants
                .Select(static variant => new ProjectObjectImageVariantDto
                {
                    VariantName = variant.VariantName,
                    FullImagePath = variant.FullImagePath,
                    ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                    ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                    ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                    ImageScale = variant.ImageScale <= 0 ? 1 : variant.ImageScale,
                    IsDefault = variant.IsDefault
                })
                .ToList(),
            ImageVariantChooserScript = string.IsNullOrWhiteSpace(obj.ImageVariantChooserScript)
                ? null
                : obj.ImageVariantChooserScript,
            Appearance = ToProjectGameObjectAppearanceDto(obj),
            ImageRotationDegrees = obj.ImageRotationDegrees,
            PositionX = SanitizeCoordinateForPersistence(obj.PositionX),
            PositionY = SanitizeCoordinateForPersistence(obj.PositionY),
            RenderZOrder = renderZOrderOverride ?? (obj.RenderZOrder == 0 ? null : obj.RenderZOrder),
            AuthoredRenderOrder = obj.AuthoredRenderOrder == 0 ? null : obj.AuthoredRenderOrder,
            IncludeInPreview = obj.IncludeInPreview,
            Description = obj.Description,
            AdditionalVerbs = (obj.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (obj.AdditionalDirectionals ?? new List<string>()).ToList(),
            IgnoredValidationRuleIds = obj.IgnoredValidationRuleIds.ToList(),
            ProcedureIds = (obj.ProcedureIds ?? new List<Guid>())
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(obj.AdditionalDirectionalTraversalMappings),
            SoundEffectLibraryEntries = obj.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
            EventSubscriptions = obj.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
            TimerDefinitions = obj.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
            AvailableGameActions = persistedLinkedBaseObjectId.HasValue
                ? new List<CommandActionDto>()
                : ToCommandActionDtos(obj.AvailableActions),
            GameProperties = obj.Variables.Select(ToVariableDto).ToList(),
            InstanceOverrides = obj.InstanceOverrides.Count == 0
                ? null
                : obj.InstanceOverrides
                    .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            ContainedObjects = obj.ContainedObjects.Select(child => ToProjectGameObjectDto(child, obj.ObjectId, null, scopeKindOverride)).ToList()
        };
    }

    private static GameObject ToGameObjectModel(ProjectGameObjectDto obj)
    {
        var linkedBaseObjectId = obj.LinkedBaseObjectId;
        var compositeRecipe = obj.CompositeRecipe;
        var compositeRequiredParts = (compositeRecipe?.RequiredParts ?? new List<CompositePartRequirementDto>())
            .Where(part => part.PartObjectId != Guid.Empty)
            .Select(part => new CompositePartRequirement
            {
                PartObjectId = part.PartObjectId,
                PartObjectName = string.Empty,
                RequiredQuantity = part.RequiredQuantity < 1 ? 1 : part.RequiredQuantity,
                MatchKind = part.MatchKind ?? ProcedureParticipantMatchKind.ObjectId,
                MatchValue = part.MatchValue ?? part.PartObjectId.ToString("D"),
                SatisfactionMode = part.SatisfactionMode,
                ConsumptionPolicy = part.ConsumptionPolicy,
                OptionalPart = part.OptionalPart,
                VariableRequirements = (part.VariableRequirements ?? new List<ProjectLockParticipantVariableRequirementDto>())
                    .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                    .Select(static requirement => new LockParticipantVariableRequirement
                    {
                        VariableName = requirement.VariableName.Trim(),
                        Operator = requirement.Operator,
                        ExpectedValue = requirement.ExpectedValue,
                        QuantityEvaluationMode = requirement.QuantityEvaluationMode
                    })
                    .ToList()
            })
            .ToList();
        var hasCompositeRecipe = compositeRecipe is not null
            && (compositeRecipe.RecipeId != Guid.Empty
                || compositeRecipe.TargetObjectId != Guid.Empty
                || compositeRecipe.IsReversible
                || compositeRequiredParts.Count > 0
                || !string.IsNullOrWhiteSpace(compositeRecipe.PartRequirementMode)
                || compositeRecipe.MinimumRequiredPartCount.HasValue);
        var isCompositeTarget = hasCompositeRecipe && compositeRecipe!.TargetObjectId != Guid.Empty;
        var compositeRecipeId = isCompositeTarget
            ? EnsureScopeId(compositeRecipe!.RecipeId, $"composite target object '{obj.Name}' recipe")
            : Guid.Empty;
        var compositePartRequirementMode = string.IsNullOrWhiteSpace(compositeRecipe?.PartRequirementMode)
            ? "AllRequired"
            : compositeRecipe.PartRequirementMode;
        var compositeMinimumRequiredPartCount = compositeRecipe?.MinimumRequiredPartCount is > 0
            ? compositeRecipe.MinimumRequiredPartCount.Value
            : 1;
        var normalizedVariants = NormalizeObjectImageVariants((obj.ImageVariants ?? new List<ProjectObjectImageVariantDto>())
            .Select(variant => new ObjectImageVariant
            {
                VariantName = variant.VariantName,
                FullImagePath = variant.FullImagePath,
                ImageLocalAlignmentRotationDegrees = variant.ImageLocalAlignmentRotationDegrees.HasValue && double.IsFinite(variant.ImageLocalAlignmentRotationDegrees.Value)
                    ? variant.ImageLocalAlignmentRotationDegrees.Value
                    : 0,
                ImageLocalAlignmentOffsetX = variant.ImageLocalAlignmentOffsetX.HasValue && double.IsFinite(variant.ImageLocalAlignmentOffsetX.Value)
                    ? variant.ImageLocalAlignmentOffsetX.Value
                    : 0,
                ImageLocalAlignmentOffsetY = variant.ImageLocalAlignmentOffsetY.HasValue && double.IsFinite(variant.ImageLocalAlignmentOffsetY.Value)
                    ? variant.ImageLocalAlignmentOffsetY.Value
                    : 0,
                ImageScale = variant.ImageScale is > 0 ? variant.ImageScale.Value : 1,
                IsDefault = variant.IsDefault
            }));

        var gameObject = new GameObject
        {
            ObjectId = EnsureScopeId(obj.Id, $"object '{obj.Name}'"),
            DesignerPersistenceScopeKind = obj.ScopeKind,
            Name = obj.Name,
            HideEmptyConfiguration = obj.HideEmptyConfiguration,
            ObjectType = string.IsNullOrWhiteSpace(obj.ObjectType) ? obj.Name : obj.ObjectType,
            ProducerNotes = obj.ProductionName,
            NameInGame = obj.NameInGame ?? string.Empty,
            NameSynonyms = NormalizeNameSynonyms(obj.NameSynonyms),
            ValidationErrors = (obj.ValidationErrors ?? new List<string>())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList(),
            IsInventoriable = obj.IsInventoriable,
            InventoryPointsDefaultValue = obj.InventoryPointsDefaultValue,
            IsContainer = obj.IsContainer,
            ContainerPointsDefaultValue = obj.ContainerPointsDefaultValue,
            IsOpenable = obj.IsOpenable,
            IsOpenDefaultValue = obj.IsOpenDefaultValue,
            IsLockable = obj.IsLockable,
            IsLockedDefaultValue = obj.IsLockedDefaultValue,
            IsActivatable = obj.IsActivatable,
            IsActiveDefaultValue = obj.IsActiveDefaultValue,
            IsHidable = obj.IsHidable,
            IsHiddenDefaultValue = obj.IsHiddenDefaultValue,
            IsQuantifiable = obj.IsQuantifiable,
            Quantity = obj.Quantity ?? 1,
            QuantifiablePlacementDistributionMode = obj.QuantifiablePlacementDistributionMode?.ToString() ?? "GroupedStack",
            LinkedBaseObjectId = linkedBaseObjectId,
            LinkActionsToBaseObject = obj.LinkActionsToBaseObject == true || linkedBaseObjectId.HasValue,
            CompositeRecipeId = compositeRecipeId,
            IsCompositeTarget = isCompositeTarget,
            IsCompositeReversible = compositeRecipe?.IsReversible ?? false,
            CompositePartRequirementMode = compositePartRequirementMode,
            CompositeMinimumRequiredPartCount = compositeMinimumRequiredPartCount,
            LockOperationRequirements = ToLockOperationRequirementsModel(obj.LockOperationRequirements),
            ImageVariants = normalizedVariants,
            ImageVariantChooserScript = obj.ImageVariantChooserScript ?? string.Empty,
            ImageRotationDegrees = obj.ImageRotationDegrees,
            IsMovable = obj.Appearance?.IsMovable ?? false,
            IsMovableDefaultValue = obj.Appearance?.IsMovableDefaultValue ?? true,
            SpatialType = obj.Appearance?.SpatialType ?? RuntimeObjectSpatialTypes.SolidObject,
            StackGroup = obj.Appearance?.StackGroup ?? obj.Appearance?.LegacyStackOrder ?? 0,
            FootprintWidthCells = obj.Appearance?.FootprintWidthCells > 0 ? obj.Appearance.FootprintWidthCells : 1,
            FootprintHeightCells = obj.Appearance?.FootprintHeightCells > 0 ? obj.Appearance.FootprintHeightCells : 1,
            FootprintOrientation = NormalizeCardinalDirection(obj.Appearance?.FootprintOrientation),
            HeadingDirection = NormalizeHeadingDirection(obj.Appearance?.HeadingDirection),
            ObjectHeightUnits = obj.Appearance?.ObjectHeightUnits >= 0 ? obj.Appearance.ObjectHeightUnits : 1,
            HeightInRoom = obj.Appearance?.HeightInRoom ?? 0,
            AuthoredBaseHeightInRoom = obj.Appearance?.AuthoredBaseHeightInRoom ?? 0,
            IsHeightPinned = obj.Appearance?.IsHeightPinned ?? false,
            OccupiedCellIds = NormalizeOccupiedCellIds(obj.Appearance?.OccupiedCellIds),
            OccupancyDerivationSourceEcho = obj.Appearance?.OccupancyDerivationSourceEcho ?? string.Empty,
            StackScaleStepOverride = SanitizeOptionalFinite(obj.Appearance?.StackScaleStepOverride),
            MinStackScaleOverride = SanitizeOptionalFinite(obj.Appearance?.MinStackScaleOverride),
            MovementRestrictions = ToObjectMovementRestrictionsModel(obj.Appearance?.MovementRestrictions),
            PositionX = SanitizeCoordinateForPersistence(obj.PositionX),
            PositionY = SanitizeCoordinateForPersistence(obj.PositionY),
            RenderZOrder = obj.RenderZOrder ?? 0,
            AuthoredRenderOrder = obj.AuthoredRenderOrder ?? 0,
            IncludeInPreview = obj.IncludeInPreview,
            CompositeRequiredParts = compositeRequiredParts,
            Description = obj.Description,
            AdditionalVerbs = (obj.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (obj.AdditionalDirectionals ?? new List<string>()).ToList(),
            IgnoredValidationRuleIds = (obj.IgnoredValidationRuleIds ?? new List<string>()).ToList(),
            SoundEffectLibraryEntries = (obj.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            EventSubscriptions = (obj.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>())
                .Select(ToEventSubscriptionModel)
                .ToList(),
            TimerDefinitions = (obj.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>())
                .Select(ToTimerDefinitionModel)
                .ToList(),
            ProcedureIds = (obj.ProcedureIds ?? new List<Guid>())
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappings(obj.AdditionalDirectionalTraversalMappings),
            AvailableActions = ToCommandActionModels(obj.AvailableGameActions),
            Commands = obj.Commands?.ToList() ?? new List<string>(),
            InstanceOverrides = (obj.InstanceOverrides ?? new Dictionary<string, string?>())
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Variables = (obj.GameProperties ?? new List<GamePropertyDefinitionDto>()).Select(ToVariableModel).ToList(),
            ContainedObjects = (obj.ContainedObjects ?? new List<ProjectGameObjectDto>()).Select(ToGameObjectModel).ToList()
        };

        if (gameObject.LinkedBaseObjectId.HasValue
        && gameObject.LinkedBaseObjectId.Value == gameObject.ObjectId)
        {
            // Legacy guardrail: self-link indicates invalid linked-instance metadata.
            gameObject.LinkedBaseObjectId = null;
            gameObject.LinkActionsToBaseObject = false;
        }

        StripLinkedRoomInstanceActionsRecursively(gameObject);

        gameObject.InitializeFeatureFlagsFromKnownVariables();
        gameObject.ApplyFeatureVariableContract();
        return gameObject;
    }

    private static List<ObjectImageVariant> NormalizeObjectImageVariants(IEnumerable<ObjectImageVariant> variants)
    {
        var normalized = (variants ?? Array.Empty<ObjectImageVariant>())
            .Where(static variant => variant is not null && !string.IsNullOrWhiteSpace(variant.VariantName))
            .Select(variant => new ObjectImageVariant
            {
                VariantName = variant.VariantName.Trim(),
                FullImagePath = variant.FullImagePath?.Trim() ?? string.Empty,
                ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                ImageScale = variant.ImageScale <= 0 ? 1 : variant.ImageScale,
                IsDefault = variant.IsDefault
            })
            .GroupBy(static variant => variant.VariantName, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        if (normalized.Count == 0)
        {
            return normalized;
        }

        var defaultIndex = normalized.FindIndex(static variant => variant.IsDefault);
        if (defaultIndex >= 0)
        {
            for (var index = 0; index < normalized.Count; index++)
            {
                normalized[index].IsDefault = index == defaultIndex;
            }

            return normalized;
        }

        if (normalized.Count == 1)
        {
            normalized[0].IsDefault = true;
        }

        return normalized;
    }

    private static List<string> NormalizeNameSynonyms(IEnumerable<string>? synonyms)
    {
        return (synonyms ?? Array.Empty<string>())
            .Where(static synonym => !string.IsNullOrWhiteSpace(synonym))
            .Select(static synonym => synonym.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeOccupiedCellIds(IEnumerable<string>? cellIds)
    {
        return (cellIds ?? Array.Empty<string>())
            .Where(static cell => !string.IsNullOrWhiteSpace(cell))
            .Select(static cell => cell.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void HydrateLinkedInstanceDefinitionOwnedFieldsFromDefinitions(ProjectModel project)
    {
        var objectLookup = BuildCleanExportObjectLookup(project);
        var definitionCatalogObjectIds = BuildDefinitionCatalogObjectIds(project);
        foreach (var obj in EnumerateAllProjectObjects(project))
        {
            if (definitionCatalogObjectIds.Contains(obj.ObjectId))
            {
                // Catalog definitions are source objects, never linked instances.
                obj.LinkedBaseObjectId = null;
                obj.LinkActionsToBaseObject = false;
                continue;
            }

            if (!obj.LinkedBaseObjectId.HasValue)
            {
                continue;
            }

            if (!objectLookup.TryGetValue(obj.LinkedBaseObjectId.Value, out var definition)
                || ReferenceEquals(definition, obj))
            {
                continue;
            }

            obj.ImageVariants = definition.ImageVariants
                .Select(static variant => new ObjectImageVariant
                {
                    VariantName = variant.VariantName,
                    FullImagePath = variant.FullImagePath,
                    ImageLocalAlignmentRotationDegrees = variant.ImageLocalAlignmentRotationDegrees,
                    ImageLocalAlignmentOffsetX = variant.ImageLocalAlignmentOffsetX,
                    ImageLocalAlignmentOffsetY = variant.ImageLocalAlignmentOffsetY,
                    ImageScale = variant.ImageScale,
                    IsDefault = variant.IsDefault
                })
                .ToList();

            obj.ImageVariantChooserScript = definition.ImageVariantChooserScript;
            obj.IsMovable = definition.IsMovable;
            obj.IsMovableDefaultValue = definition.IsMovableDefaultValue;
            obj.SpatialType = definition.SpatialType;
            obj.StackGroup = definition.StackGroup;
            obj.FootprintWidthCells = definition.FootprintWidthCells;
            obj.FootprintHeightCells = definition.FootprintHeightCells;
            obj.FootprintOrientation = definition.FootprintOrientation;
            obj.HeadingDirection = definition.HeadingDirection;
            obj.ObjectHeightUnits = definition.ObjectHeightUnits;
            obj.HeightInRoom = definition.HeightInRoom;
            obj.AuthoredBaseHeightInRoom = definition.AuthoredBaseHeightInRoom;
            obj.IsHeightPinned = definition.IsHeightPinned;
            obj.OccupiedCellIds = NormalizeOccupiedCellIds(definition.OccupiedCellIds);
            obj.OccupancyDerivationSourceEcho = definition.OccupancyDerivationSourceEcho;
            obj.StackScaleStepOverride = definition.StackScaleStepOverride;
            obj.MinStackScaleOverride = definition.MinStackScaleOverride;
            obj.MovementRestrictions = CloneMovementRestrictionsForHydration(definition.MovementRestrictions);
        }
    }

    private static void NormalizeDefinitionCatalogObjectLinkMetadata(ProjectModel project)
    {
        foreach (var obj in EnumerateDefinitionCatalogObjects(project))
        {
            if (!obj.LinkedBaseObjectId.HasValue && !obj.LinkActionsToBaseObject)
            {
                continue;
            }

            obj.LinkedBaseObjectId = null;
            obj.LinkActionsToBaseObject = false;
        }
    }

    private static HashSet<Guid> BuildDefinitionCatalogObjectIds(ProjectModel project)
    {
        var ids = new HashSet<Guid>();

        void AddRecursive(GameObject obj)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                ids.Add(obj.ObjectId);
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddRecursive(child);
            }
        }

        void AddCatalog(IEnumerable<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                AddRecursive(obj);
            }
        }

        AddCatalog(project.ObjectTemplates);
        AddCatalog(project.BaseObjects);

        foreach (var planet in project.Planets)
        {
            AddCatalog(planet.BaseObjects);
            foreach (var country in planet.Countries)
            {
                AddCatalog(country.BaseObjects);
                foreach (var area in country.Areas)
                {
                    AddCatalog(area.BaseObjects);
                }
            }
        }

        return ids;
    }

    private static IEnumerable<GameObject> EnumerateDefinitionCatalogObjects(ProjectModel project)
    {
        foreach (var obj in EnumerateGameObjectsRecursive(project.ObjectTemplates))
        {
            yield return obj;
        }

        foreach (var obj in EnumerateGameObjectsRecursive(project.BaseObjects))
        {
            yield return obj;
        }

        foreach (var obj in project.Planets.SelectMany(static planet => planet.BaseObjects))
        {
            foreach (var nested in EnumerateGameObjectsRecursive([obj]))
            {
                yield return nested;
            }
        }

        foreach (var obj in project.Planets
                     .SelectMany(static planet => planet.Countries)
                     .SelectMany(static country => country.BaseObjects))
        {
            foreach (var nested in EnumerateGameObjectsRecursive([obj]))
            {
                yield return nested;
            }
        }

        foreach (var obj in project.Planets
                     .SelectMany(static planet => planet.Countries)
                     .SelectMany(static country => country.Areas)
                     .SelectMany(static area => area.BaseObjects))
        {
            foreach (var nested in EnumerateGameObjectsRecursive([obj]))
            {
                yield return nested;
            }
        }
    }

    private static ObjectMovementRestrictions? CloneMovementRestrictionsForHydration(ObjectMovementRestrictions? source)
    {
        if (source is null)
        {
            return null;
        }

        return new ObjectMovementRestrictions
        {
            MultiLegMaxTotalDistanceCells = source.MultiLegMaxTotalDistanceCells,
            FirstUnstacked = CloneMovementRestrictionCategoryForHydration(source.FirstUnstacked),
            FirstStacked = CloneMovementRestrictionCategoryForHydration(source.FirstStacked),
            SubsequentUnstacked = CloneMovementRestrictionCategoryForHydration(source.SubsequentUnstacked),
            SubsequentStacked = CloneMovementRestrictionCategoryForHydration(source.SubsequentStacked)
        };
    }

    private static ObjectMovementRestrictionCategory CloneMovementRestrictionCategoryForHydration(ObjectMovementRestrictionCategory source)
    {
        return new ObjectMovementRestrictionCategory
        {
            N = CloneMovementRestrictionRuleForHydration(source.N),
            NE = CloneMovementRestrictionRuleForHydration(source.NE),
            E = CloneMovementRestrictionRuleForHydration(source.E),
            SE = CloneMovementRestrictionRuleForHydration(source.SE),
            S = CloneMovementRestrictionRuleForHydration(source.S),
            SW = CloneMovementRestrictionRuleForHydration(source.SW),
            W = CloneMovementRestrictionRuleForHydration(source.W),
            NW = CloneMovementRestrictionRuleForHydration(source.NW)
        };
    }

    private static ObjectMovementRestrictionRule CloneMovementRestrictionRuleForHydration(ObjectMovementRestrictionRule source)
    {
        return new ObjectMovementRestrictionRule
        {
            MaxDistance = source.MaxDistance,
            AllowJumpOver = source.AllowJumpOver
        };
    }

    private static IEnumerable<GameObject> EnumerateAllProjectObjects(ProjectModel project)
    {
        foreach (var obj in EnumerateGameObjectsRecursive(project.GlobalScope.GameObjects))
        {
            yield return obj;
        }

        foreach (var obj in EnumerateGameObjectsRecursive(project.ObjectTemplates))
        {
            yield return obj;
        }

        foreach (var obj in EnumerateGameObjectsRecursive(project.BaseObjects))
        {
            yield return obj;
        }

        foreach (var obj in project.RoomTemplates
                     .SelectMany(static roomTemplate => EnumerateGameObjectsRecursive(roomTemplate.GameObjects)))
        {
            yield return obj;
        }

        foreach (var obj in project.Planets
                     .SelectMany(static planet => planet.Countries)
                     .SelectMany(static country => country.Areas)
                     .SelectMany(static area => area.Rooms)
                     .SelectMany(static room => EnumerateGameObjectsRecursive(room.GameObjects)))
        {
            yield return obj;
        }
    }

    private static void RefreshRoomDerivedOccupancyCaches(ProjectModel project)
    {
        foreach (var room in project.Planets
                     .SelectMany(static planet => planet.Countries)
                     .SelectMany(static country => country.Areas)
                     .SelectMany(static area => area.Rooms))
        {
            var effectiveCellSize = ResolveEffectiveRoomCellSize(project, room);
            foreach (var roomObject in room.GameObjects)
            {
                if (!ShouldRefreshDerivedOccupancy(roomObject))
                {
                    continue;
                }

                var normalizedFootprintWidth = roomObject.FootprintWidthCells < 1 ? 1 : roomObject.FootprintWidthCells;
                var normalizedFootprintHeight = roomObject.FootprintHeightCells < 1 ? 1 : roomObject.FootprintHeightCells;

                roomObject.OccupiedCellIds = DeriveOccupiedCellIds(
                    roomObject.PositionX,
                    roomObject.PositionY,
                    normalizedFootprintWidth,
                    normalizedFootprintHeight,
                    effectiveCellSize);

                roomObject.OccupancyDerivationSourceEcho = BuildOccupancyDerivationSourceEcho(
                    roomObject.PositionX,
                    roomObject.PositionY,
                    normalizedFootprintWidth,
                    normalizedFootprintHeight,
                    NormalizeCardinalDirection(roomObject.FootprintOrientation),
                    effectiveCellSize);
            }
        }
    }

    private static bool ShouldRefreshDerivedOccupancy(GameObject roomObject)
    {
        return roomObject.OccupiedCellIds.Count > 0
               || !string.IsNullOrWhiteSpace(roomObject.OccupancyDerivationSourceEcho);
    }

    private static int ResolveEffectiveRoomCellSize(ProjectModel project, Room room)
    {
        return project.RoomDesignerGridCellSize > 0
            ? project.RoomDesignerGridCellSize
            : 40;
    }

    private static int ResolveEffectiveRoomCanvasWidth(ProjectModel project, Room room)
    {
        return room.RoomImageCanvasWidth > 0
            ? room.RoomImageCanvasWidth
            : (project.RoomImageCanvasWidth > 0 ? project.RoomImageCanvasWidth : 800);
    }

    private static int ResolveEffectiveRoomCanvasHeight(ProjectModel project, Room room)
    {
        return room.RoomImageCanvasHeight > 0
            ? room.RoomImageCanvasHeight
            : (project.RoomImageCanvasHeight > 0 ? project.RoomImageCanvasHeight : 600);
    }

    private static List<string> DeriveOccupiedCellIds(
        double positionX,
        double positionY,
        int footprintWidthCells,
        int footprintHeightCells,
        int effectiveCellSize)
    {
        var cellSize = effectiveCellSize < 1 ? 1 : effectiveCellSize;
        var x = double.IsFinite(positionX) ? positionX : 0;
        var y = double.IsFinite(positionY) ? positionY : 0;

        var baseColumn = (int)Math.Floor(x / cellSize);
        var baseRow = (int)Math.Floor(y / cellSize);

        if (baseColumn < 0)
        {
            baseColumn = 0;
        }

        if (baseRow < 0)
        {
            baseRow = 0;
        }

        var width = footprintWidthCells < 1 ? 1 : footprintWidthCells;
        var height = footprintHeightCells < 1 ? 1 : footprintHeightCells;
        var cells = new List<string>(width * height);

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var cellColumn = ToLetterIdentifier(baseColumn + column);
                var cellRow = ToLetterIdentifier(baseRow + row);
                cells.Add($"{cellColumn}.{cellRow}");
            }
        }

        return cells;
    }

    private static string BuildOccupancyDerivationSourceEcho(
        double positionX,
        double positionY,
        int footprintWidthCells,
        int footprintHeightCells,
        string footprintOrientation,
        int effectiveCellSize)
    {
        var x = double.IsFinite(positionX) ? positionX : 0;
        var y = double.IsFinite(positionY) ? positionY : 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"x={x:0.###};y={y:0.###};anchor=TopLeft;footprint={footprintWidthCells}x{footprintHeightCells};orientation={footprintOrientation};cell={effectiveCellSize}");
    }

    private static string ToLetterIdentifier(int zeroBasedValue)
    {
        var value = zeroBasedValue < 0 ? 0 : zeroBasedValue;
        var characters = new Stack<char>();

        do
        {
            characters.Push((char)('A' + (value % 26)));
            value = (value / 26) - 1;
        }
        while (value >= 0);

        return new string(characters.ToArray());
    }

    private static ProjectGameObjectAppearanceDto? ToProjectGameObjectAppearanceDto(GameObject obj)
    {
        var normalizedSpatialType = NormalizeSpatialTypeName(obj.SpatialType);
        var appearance = new ProjectGameObjectAppearanceDto
        {
            SpatialType = ToRuntimeSpatialType(normalizedSpatialType),
            IsMovable = obj.IsMovable,
            IsMovableDefaultValue = obj.IsMovableDefaultValue,
            StackGroup = obj.StackGroup,
            FootprintWidthCells = obj.FootprintWidthCells,
            FootprintHeightCells = obj.FootprintHeightCells,
            FootprintOrientation = NormalizeCardinalDirection(obj.FootprintOrientation),
            HeadingDirection = NormalizeHeadingDirection(obj.HeadingDirection),
            ObjectHeightUnits = obj.ObjectHeightUnits,
            HeightInRoom = obj.HeightInRoom,
            AuthoredBaseHeightInRoom = obj.AuthoredBaseHeightInRoom,
            IsHeightPinned = obj.AuthoredBaseHeightInRoom == 0 ? false : obj.IsHeightPinned,
            OccupiedCellIds = NullIfEmpty(NormalizeOccupiedCellIds(obj.OccupiedCellIds)),
            OccupancyDerivationSourceEcho = string.IsNullOrWhiteSpace(obj.OccupancyDerivationSourceEcho)
                ? null
                : obj.OccupancyDerivationSourceEcho,
            StackScaleStepOverride = SanitizeOptionalFinite(obj.StackScaleStepOverride),
            MinStackScaleOverride = SanitizeOptionalFinite(obj.MinStackScaleOverride),
            MovementRestrictions = ToProjectObjectMovementRestrictionsDto(obj.MovementRestrictions)
        };

        return IsDefaultAppearance(
            appearance.SpatialType,
            appearance.IsMovable,
            appearance.IsMovableDefaultValue,
            appearance.StackGroup,
            appearance.FootprintWidthCells,
            appearance.FootprintHeightCells,
            appearance.FootprintOrientation,
            appearance.HeadingDirection,
            appearance.ObjectHeightUnits,
            appearance.HeightInRoom,
            appearance.AuthoredBaseHeightInRoom,
            appearance.IsHeightPinned ?? false,
            appearance.OccupiedCellIds,
            appearance.OccupancyDerivationSourceEcho,
            appearance.StackScaleStepOverride,
            appearance.MinStackScaleOverride,
            appearance.MovementRestrictions)
            ? null
            : appearance;
    }

    private static RuntimeGameObjectAppearanceDto? ToCleanGameObjectAppearanceDto(
        GameObject obj,
        GameObject effectiveDefinitionOwnedSource)
    {
        var normalizedSpatialType = NormalizeSpatialTypeName(effectiveDefinitionOwnedSource.SpatialType);
        var appearance = new RuntimeGameObjectAppearanceDto
        {
            SpatialType = ToRuntimeSpatialType(normalizedSpatialType),
            IsMovable = obj.IsMovable,
            IsMovableDefaultValue = obj.IsMovableDefaultValue,
            StackGroup = effectiveDefinitionOwnedSource.StackGroup,
            FootprintWidthCells = effectiveDefinitionOwnedSource.FootprintWidthCells,
            FootprintHeightCells = effectiveDefinitionOwnedSource.FootprintHeightCells,
            FootprintOrientation = ToCardinalDirection(effectiveDefinitionOwnedSource.FootprintOrientation),
            HeadingDirection = ToHeadingDirection(effectiveDefinitionOwnedSource.HeadingDirection),
            ObjectHeightUnits = effectiveDefinitionOwnedSource.ObjectHeightUnits,
            HeightInRoom = obj.HeightInRoom,
            AuthoredBaseHeightInRoom = obj.AuthoredBaseHeightInRoom,
            IsHeightPinned = obj.AuthoredBaseHeightInRoom == 0 ? null : obj.IsHeightPinned,
            OccupiedCellIds = NullIfEmpty(NormalizeOccupiedCellIds(obj.OccupiedCellIds)),
            OccupancyDerivationSourceEcho = string.IsNullOrWhiteSpace(obj.OccupancyDerivationSourceEcho)
                ? null
                : obj.OccupancyDerivationSourceEcho,
            StackScaleStepOverride = SanitizeOptionalFinite(effectiveDefinitionOwnedSource.StackScaleStepOverride),
            MinStackScaleOverride = SanitizeOptionalFinite(effectiveDefinitionOwnedSource.MinStackScaleOverride),
            MovementRestrictions = ToCleanObjectMovementRestrictionsDto(effectiveDefinitionOwnedSource.MovementRestrictions)
        };

        return IsDefaultAppearance(
            appearance.SpatialType,
            appearance.IsMovable,
            appearance.IsMovableDefaultValue,
            appearance.StackGroup,
            appearance.FootprintWidthCells,
            appearance.FootprintHeightCells,
            appearance.FootprintOrientation,
            appearance.HeadingDirection,
            appearance.ObjectHeightUnits,
            appearance.HeightInRoom,
            appearance.AuthoredBaseHeightInRoom,
            appearance.IsHeightPinned,
            appearance.OccupiedCellIds,
            appearance.OccupancyDerivationSourceEcho,
            appearance.StackScaleStepOverride,
            appearance.MinStackScaleOverride,
            appearance.MovementRestrictions)
            ? null
            : appearance;
    }

    private static bool IsDefaultAppearance(
        RuntimeObjectSpatialTypes spatialType,
        bool isMovable,
        bool? isMovableDefaultValue,
        int stackGroup,
        int footprintWidthCells,
        int footprintHeightCells,
        Direction8? footprintOrientation,
        Direction8? headingDirection,
        int objectHeightUnits,
        int heightInRoom,
        int authoredBaseHeightInRoom,
        bool? isHeightPinned,
        List<string>? occupiedCellIds,
        string? occupancyDerivationSourceEcho,
        double? stackScaleStepOverride,
        double? minStackScaleOverride,
        object? movementRestrictions)
    {
        return IsDefaultAppearance(
            spatialType,
            isMovable,
            isMovableDefaultValue,
            stackGroup,
            footprintWidthCells,
            footprintHeightCells,
            footprintOrientation?.ToString(),
            headingDirection?.ToString(),
            objectHeightUnits,
            heightInRoom,
            authoredBaseHeightInRoom,
            isHeightPinned ?? false,
            occupiedCellIds,
            occupancyDerivationSourceEcho,
            stackScaleStepOverride,
            minStackScaleOverride,
            movementRestrictions);
    }

    private static bool IsDefaultAppearance(
        RuntimeObjectSpatialTypes spatialType,
        bool isMovable,
        bool? isMovableDefaultValue,
        int stackGroup,
        int footprintWidthCells,
        int footprintHeightCells,
        string? footprintOrientation,
        string? headingDirection,
        int objectHeightUnits,
        int heightInRoom,
        int authoredBaseHeightInRoom,
        bool isHeightPinned,
        List<string>? occupiedCellIds,
        string? occupancyDerivationSourceEcho,
        double? stackScaleStepOverride,
        double? minStackScaleOverride,
        object? movementRestrictions)
    {
                 return spatialType == RuntimeObjectSpatialTypes.SolidObject
             && isMovable == false
               && (isMovableDefaultValue ?? true) == true
               && stackGroup == 0
               && footprintWidthCells == 1
               && footprintHeightCells == 1
               && string.Equals(NormalizeCardinalDirection(footprintOrientation), "N", StringComparison.Ordinal)
               && string.Equals(NormalizeHeadingDirection(headingDirection), "N", StringComparison.Ordinal)
               && objectHeightUnits == 1
               && heightInRoom == 0
               && authoredBaseHeightInRoom == 0
               && isHeightPinned == false
               && (occupiedCellIds is null || occupiedCellIds.Count == 0)
               && string.IsNullOrWhiteSpace(occupancyDerivationSourceEcho)
               && !stackScaleStepOverride.HasValue
               && !minStackScaleOverride.HasValue
               && movementRestrictions is null;
    }

    private static Direction8 ToCardinalDirection(string? direction)
    {
        return NormalizeCardinalDirection(direction) switch
        {
            "E" => Direction8.East,
            "S" => Direction8.South,
            "W" => Direction8.West,
            _ => Direction8.North
        };
    }

    private static Direction8 ToHeadingDirection(string? direction)
    {
        return NormalizeHeadingDirection(direction) switch
        {
            "NE" => Direction8.NorthEast,
            "E" => Direction8.East,
            "SE" => Direction8.SouthEast,
            "S" => Direction8.South,
            "SW" => Direction8.SouthWest,
            "W" => Direction8.West,
            "NW" => Direction8.NorthWest,
            _ => Direction8.North
        };
    }

    private static string NormalizeSpatialTypeName(RuntimeObjectSpatialTypes spatialType)
    {
        return spatialType == RuntimeObjectSpatialTypes.PassiveObject
            ? nameof(RuntimeObjectSpatialTypes.PassiveObject)
            : nameof(RuntimeObjectSpatialTypes.SolidObject);
    }

    private static string NormalizeSpatialTypeName(string? spatialType)
    {
        return string.Equals(spatialType?.Trim(), nameof(RuntimeObjectSpatialTypes.PassiveObject), StringComparison.OrdinalIgnoreCase)
            ? nameof(RuntimeObjectSpatialTypes.PassiveObject)
            : nameof(RuntimeObjectSpatialTypes.SolidObject);
    }

    private static bool IsPassiveSpatialType(string? spatialType)
    {
        return string.Equals(NormalizeSpatialTypeName(spatialType), nameof(RuntimeObjectSpatialTypes.PassiveObject), StringComparison.Ordinal);
    }

    private static RuntimeObjectSpatialTypes ToRuntimeSpatialType(string? spatialType)
    {
        return IsPassiveSpatialType(spatialType)
            ? RuntimeObjectSpatialTypes.PassiveObject
            : RuntimeObjectSpatialTypes.SolidObject;
    }

    private static RuntimeRoomImageDisplayMode ToRuntimeRoomImageDisplayMode(RuntimeRoomImageDisplayMode value)
    {
        return value switch
        {
            RuntimeRoomImageDisplayMode.Overlay => RuntimeRoomImageDisplayMode.Overlay,
            _ => RuntimeRoomImageDisplayMode.Independent
        };
    }

    private static ProjectObjectMovementRestrictionsDto? ToProjectObjectMovementRestrictionsDto(ObjectMovementRestrictions? source)
    {
        if (source is null)
        {
            return null;
        }

        var mapped = new ProjectObjectMovementRestrictionsDto
        {
            MultiLegMaxTotalDistanceCells = NormalizeMovementMaxDistance(source.MultiLegMaxTotalDistanceCells),
            FirstUnstacked = ToProjectObjectMovementRestrictionCategoryDto(source.FirstUnstacked),
            FirstStacked = ToProjectObjectMovementRestrictionCategoryDto(source.FirstStacked),
            SubsequentUnstacked = ToProjectObjectMovementRestrictionCategoryDto(source.SubsequentUnstacked),
            SubsequentStacked = ToProjectObjectMovementRestrictionCategoryDto(source.SubsequentStacked)
        };

        return mapped.HasAnyRule()
            ? mapped
            : null;
    }

    private static RuntimeObjectMovementRestrictionsDto? ToCleanObjectMovementRestrictionsDto(ObjectMovementRestrictions? source)
    {
        if (source is null)
        {
            return null;
        }

        var mapped = new RuntimeObjectMovementRestrictionsDto
        {
            MultiLegMaxTotalDistanceCells = NormalizeMovementMaxDistance(source.MultiLegMaxTotalDistanceCells),
            FirstUnstacked = ToCleanObjectMovementRestrictionCategoryDto(source.FirstUnstacked),
            FirstStacked = ToCleanObjectMovementRestrictionCategoryDto(source.FirstStacked),
            SubsequentUnstacked = ToCleanObjectMovementRestrictionCategoryDto(source.SubsequentUnstacked),
            SubsequentStacked = ToCleanObjectMovementRestrictionCategoryDto(source.SubsequentStacked)
        };

        return HasAnyRule(mapped)
            ? mapped
            : null;
    }

    private static ObjectMovementRestrictions? ToObjectMovementRestrictionsModel(ProjectObjectMovementRestrictionsDto? source)
    {
        if (source is null)
        {
            return null;
        }

        var mapped = new ObjectMovementRestrictions
        {
            MultiLegMaxTotalDistanceCells = NormalizeMovementMaxDistance(source.MultiLegMaxTotalDistanceCells),
            FirstUnstacked = ToObjectMovementRestrictionCategoryModel(source.FirstUnstacked),
            FirstStacked = ToObjectMovementRestrictionCategoryModel(source.FirstStacked),
            SubsequentUnstacked = ToObjectMovementRestrictionCategoryModel(source.SubsequentUnstacked),
            SubsequentStacked = ToObjectMovementRestrictionCategoryModel(source.SubsequentStacked)
        };

        return mapped.HasAnyRule()
            ? mapped
            : null;
    }

    private static ProjectObjectMovementRestrictionCategoryDto? ToProjectObjectMovementRestrictionCategoryDto(ObjectMovementRestrictionCategory source)
    {
        var mapped = new ProjectObjectMovementRestrictionCategoryDto
        {
            N = ToProjectObjectMovementRestrictionRuleDto(source.N),
            NE = ToProjectObjectMovementRestrictionRuleDto(source.NE),
            E = ToProjectObjectMovementRestrictionRuleDto(source.E),
            SE = ToProjectObjectMovementRestrictionRuleDto(source.SE),
            S = ToProjectObjectMovementRestrictionRuleDto(source.S),
            SW = ToProjectObjectMovementRestrictionRuleDto(source.SW),
            W = ToProjectObjectMovementRestrictionRuleDto(source.W),
            NW = ToProjectObjectMovementRestrictionRuleDto(source.NW)
        };

        return mapped.HasAnyRule()
            ? mapped
            : null;
    }

    private static RuntimeObjectMovementRestrictionCategoryDto? ToCleanObjectMovementRestrictionCategoryDto(ObjectMovementRestrictionCategory source)
    {
        var mapped = new RuntimeObjectMovementRestrictionCategoryDto
        {
            N = ToCleanObjectMovementRestrictionRuleDto(source.N),
            NE = ToCleanObjectMovementRestrictionRuleDto(source.NE),
            E = ToCleanObjectMovementRestrictionRuleDto(source.E),
            SE = ToCleanObjectMovementRestrictionRuleDto(source.SE),
            S = ToCleanObjectMovementRestrictionRuleDto(source.S),
            SW = ToCleanObjectMovementRestrictionRuleDto(source.SW),
            W = ToCleanObjectMovementRestrictionRuleDto(source.W),
            NW = ToCleanObjectMovementRestrictionRuleDto(source.NW)
        };

        return HasAnyRule(mapped)
            ? mapped
            : null;
    }

    private static ObjectMovementRestrictionCategory ToObjectMovementRestrictionCategoryModel(ProjectObjectMovementRestrictionCategoryDto? source)
    {
        return new ObjectMovementRestrictionCategory
        {
            N = ToObjectMovementRestrictionRuleModel(source?.N),
            NE = ToObjectMovementRestrictionRuleModel(source?.NE),
            E = ToObjectMovementRestrictionRuleModel(source?.E),
            SE = ToObjectMovementRestrictionRuleModel(source?.SE),
            S = ToObjectMovementRestrictionRuleModel(source?.S),
            SW = ToObjectMovementRestrictionRuleModel(source?.SW),
            W = ToObjectMovementRestrictionRuleModel(source?.W),
            NW = ToObjectMovementRestrictionRuleModel(source?.NW)
        };
    }

    private static ProjectObjectMovementRestrictionRuleDto? ToProjectObjectMovementRestrictionRuleDto(ObjectMovementRestrictionRule source)
    {
        var maxDistance = NormalizeMovementMaxDistance(source.MaxDistance);
        var allowJumpOver = source.AllowJumpOver;
        if (!maxDistance.HasValue && !allowJumpOver.HasValue)
        {
            return null;
        }

        return new ProjectObjectMovementRestrictionRuleDto
        {
            MaxDistance = maxDistance,
            AllowJumpOver = allowJumpOver
        };
    }

    private static RuntimeObjectMovementRestrictionRuleDto? ToCleanObjectMovementRestrictionRuleDto(ObjectMovementRestrictionRule source)
    {
        var maxDistance = NormalizeMovementMaxDistance(source.MaxDistance);
        var allowJumpOver = source.AllowJumpOver;
        if (!maxDistance.HasValue && !allowJumpOver.HasValue)
        {
            return null;
        }

        return new RuntimeObjectMovementRestrictionRuleDto
        {
            MaxDistance = maxDistance,
            AllowJumpOver = allowJumpOver
        };
    }

    private static ObjectMovementRestrictionRule ToObjectMovementRestrictionRuleModel(ProjectObjectMovementRestrictionRuleDto? source)
    {
        if (source is null)
        {
            return new ObjectMovementRestrictionRule();
        }

        return new ObjectMovementRestrictionRule
        {
            MaxDistance = NormalizeMovementMaxDistance(source.MaxDistance),
            AllowJumpOver = source.AllowJumpOver
        };
    }

    private static int? NormalizeMovementMaxDistance(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 0, 99);
    }

    private static bool HasAnyRule(RuntimeObjectMovementRestrictionsDto source)
    {
                return source.MultiLegMaxTotalDistanceCells.HasValue
                    || HasAnyRule(source.FirstUnstacked)
               || HasAnyRule(source.FirstStacked)
               || HasAnyRule(source.SubsequentUnstacked)
               || HasAnyRule(source.SubsequentStacked);
    }

    private static bool HasAnyRule(RuntimeObjectMovementRestrictionCategoryDto? source)
    {
        if (source is null)
        {
            return false;
        }

        return HasAnyRule(source.N)
               || HasAnyRule(source.NE)
               || HasAnyRule(source.E)
               || HasAnyRule(source.SE)
               || HasAnyRule(source.S)
               || HasAnyRule(source.SW)
               || HasAnyRule(source.W)
               || HasAnyRule(source.NW);
    }

    private static bool HasAnyRule(RuntimeObjectMovementRestrictionRuleDto? source)
    {
        return source is not null && (source.MaxDistance.HasValue || source.AllowJumpOver.HasValue);
    }

    private static string NormalizeCardinalDirection(string? direction)
    {
        var value = direction?.Trim().ToUpperInvariant();
        return value is "N" or "E" or "S" or "W"
            ? value
            : "N";
    }

    private static string NormalizeHeadingDirection(string? direction)
    {
        var value = direction?.Trim().ToUpperInvariant();
        return value is "N" or "NE" or "E" or "SE" or "S" or "SW" or "W" or "NW"
            ? value
            : "N";
    }

    private static double? SanitizeOptionalFinite(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value)
            ? value
            : null;
    }

    private static bool IsLinkedRoomInstance(GameObject obj)
    {
        return NormalizePersistedLinkedBaseObjectId(obj).HasValue;
    }

    private static Guid? NormalizePersistedLinkedBaseObjectId(GameObject obj)
    {
        if (!obj.LinkedBaseObjectId.HasValue || obj.LinkedBaseObjectId.Value == Guid.Empty)
        {
            return null;
        }

        return obj.LinkedBaseObjectId.Value == obj.ObjectId
            ? null
            : obj.LinkedBaseObjectId;
    }

    private static Dictionary<Guid, GameObject> BuildCleanExportObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        void AddGraph(IEnumerable<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                AddRecursive(obj);
            }
        }

        void AddRecursive(GameObject obj)
        {
            if (obj.ObjectId != Guid.Empty && !lookup.ContainsKey(obj.ObjectId))
            {
                lookup[obj.ObjectId] = obj;
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddRecursive(child);
            }
        }

        AddGraph(project.BaseObjects);
        AddGraph(project.ObjectTemplates);
        AddGraph(project.GlobalScope.GameObjects);
        foreach (var roomTemplate in project.RoomTemplates)
        {
            AddGraph(roomTemplate.GameObjects);
        }

        foreach (var planet in project.Planets)
        {
            AddGraph(planet.BaseObjects);
            AddGraph(planet.GameObjects);

            foreach (var country in planet.Countries)
            {
                AddGraph(country.BaseObjects);
                AddGraph(country.GameObjects);

                foreach (var area in country.Areas)
                {
                    AddGraph(area.BaseObjects);
                    AddGraph(area.GameObjects);

                    foreach (var room in area.Rooms)
                    {
                        AddGraph(room.GameObjects);
                    }
                }
            }
        }

        return lookup;
    }

    private static GameObject ResolveEffectiveDefinitionOwnedSource(
        GameObject obj,
        Func<Guid, GameObject?> resolveDefinition)
    {
        if (!obj.LinkedBaseObjectId.HasValue)
        {
            return obj;
        }

        var definition = resolveDefinition(obj.LinkedBaseObjectId.Value);
        return definition is not null && !ReferenceEquals(definition, obj)
            ? definition
            : obj;
    }

    private static ProjectCompositeRecipeDto? ToProjectCompositeRecipeDto(GameObject obj)
    {
        if (!obj.IsCompositeTarget)
        {
            return null;
        }

        return new ProjectCompositeRecipeDto
        {
            RecipeId = EnsureScopeId(obj.CompositeRecipeId, $"composite target object '{obj.Name}' recipe"),
            TargetObjectId = EnsureScopeId(obj.ObjectId, $"composite target object '{obj.Name}'"),
            IsReversible = obj.IsCompositeReversible,
            PartRequirementMode = string.IsNullOrWhiteSpace(obj.CompositePartRequirementMode)
                ? null
                : obj.CompositePartRequirementMode,
            MinimumRequiredPartCount = string.Equals(obj.CompositePartRequirementMode, "MinimumCount", StringComparison.OrdinalIgnoreCase)
                ? obj.CompositeMinimumRequiredPartCount
                : null,
            RequiredParts = obj.CompositeRequiredParts.Select(part => new CompositePartRequirementDto
            {
                PartObjectId = part.PartObjectId,
                RequiredQuantity = part.RequiredQuantity,
                MatchKind = part.MatchKind,
                MatchValue = part.MatchValue,
                SatisfactionMode = part.SatisfactionMode,
                ConsumptionPolicy = part.ConsumptionPolicy,
                OptionalPart = part.OptionalPart,
                VariableRequirements = (part.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                    .Select(static requirement => new ProjectLockParticipantVariableRequirementDto
                    {
                        VariableName = requirement.VariableName.Trim(),
                        Operator = requirement.Operator,
                        ExpectedValue = requirement.ExpectedValue,
                        QuantityEvaluationMode = requirement.QuantityEvaluationMode
                    })
                    .ToList()
            }).ToList(),
            ScopePolicy = null,
            BuildSuccessMessage = null,
            BuildFailureMessage = null,
            BreakSuccessMessage = null,
            BreakFailureMessage = null
        };
    }

    private static ProjectLockOperationRequirementsDto? ToProjectLockOperationRequirementsDto(LockOperationRequirements? requirements)
    {
        var normalized = NormalizeLockOperationRequirements(requirements);
        var hasData = normalized.UnlockKeyRequirements.Count > 0
            || normalized.RequireKeyForLockOperation
            || normalized.LockKeyRequirements.Count > 0;

        if (!hasData)
        {
            return null;
        }

        return new ProjectLockOperationRequirementsDto
        {
            UnlockKeyRequirements = normalized.UnlockKeyRequirements.Select(static requirement => new ProjectLockKeyRequirementDto
            {
                RequiredObjectId = requirement.RequiredObjectId,
                RequiredQuantity = requirement.RequiredQuantity,
                MatchKind = requirement.MatchKind,
                MatchValue = requirement.MatchValue,
                SatisfactionMode = requirement.SatisfactionMode,
                ConsumptionPolicy = requirement.ConsumptionPolicy,
                IsOptional = requirement.IsOptional,
                VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                    .Select(static item => new ProjectLockParticipantVariableRequirementDto
                    {
                        VariableName = item.VariableName.Trim(),
                        Operator = item.Operator,
                        ExpectedValue = item.ExpectedValue,
                        QuantityEvaluationMode = item.QuantityEvaluationMode
                    })
                    .ToList()
            }).ToList(),
            RequireKeyForLockOperation = normalized.RequireKeyForLockOperation,
            UseUnlockKeysForLockOperation = normalized.UseUnlockKeysForLockOperation,
            LockKeyRequirements = normalized.UseUnlockKeysForLockOperation
                ? new List<ProjectLockKeyRequirementDto>()
                : normalized.LockKeyRequirements.Select(static requirement => new ProjectLockKeyRequirementDto
            {
                RequiredObjectId = requirement.RequiredObjectId,
                RequiredQuantity = requirement.RequiredQuantity,
                MatchKind = requirement.MatchKind,
                MatchValue = requirement.MatchValue,
                SatisfactionMode = requirement.SatisfactionMode,
                ConsumptionPolicy = requirement.ConsumptionPolicy,
                IsOptional = requirement.IsOptional,
                VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                    .Select(static item => new ProjectLockParticipantVariableRequirementDto
                    {
                        VariableName = item.VariableName.Trim(),
                        Operator = item.Operator,
                        ExpectedValue = item.ExpectedValue,
                        QuantityEvaluationMode = item.QuantityEvaluationMode
                    })
                    .ToList()
            }).ToList()
        };
    }

    private static LockOperationRequirements ToLockOperationRequirementsModel(ProjectLockOperationRequirementsDto? dto)
    {
        if (dto is null)
        {
            return new LockOperationRequirements();
        }

        return NormalizeLockOperationRequirements(new LockOperationRequirements
        {
            UnlockKeyRequirements = (dto.UnlockKeyRequirements ?? new List<ProjectLockKeyRequirementDto>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = requirement.MatchValue ?? string.Empty,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<ProjectLockParticipantVariableRequirementDto>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new LockParticipantVariableRequirement
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList(),
            RequireKeyForLockOperation = dto.RequireKeyForLockOperation,
            UseUnlockKeysForLockOperation = dto.UseUnlockKeysForLockOperation,
            LockKeyRequirements = (dto.LockKeyRequirements ?? new List<ProjectLockKeyRequirementDto>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = requirement.MatchValue ?? string.Empty,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<ProjectLockParticipantVariableRequirementDto>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new LockParticipantVariableRequirement
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList()
        });
    }

    private static RuntimeLockOperationRequirementsDto? ToCleanLockOperationRequirementsDto(LockOperationRequirements? requirements)
    {
        var normalized = NormalizeLockOperationRequirements(requirements);
        var hasData = normalized.UnlockKeyRequirements.Count > 0
            || normalized.RequireKeyForLockOperation
            || normalized.LockKeyRequirements.Count > 0;
        if (!hasData)
        {
            return null;
        }

        return new RuntimeLockOperationRequirementsDto
        {
            UnlockKeyRequirements = normalized.UnlockKeyRequirements.Select(static requirement => new RuntimeLockKeyRequirementDto
            {
                ObjectId = requirement.RequiredObjectId,
                RequiredObjectId = requirement.RequiredObjectId,
                RequiredQuantity = requirement.RequiredQuantity,
                MatchKind = requirement.MatchKind,
                MatchValue = string.IsNullOrWhiteSpace(requirement.MatchValue)
                    ? requirement.RequiredObjectId.ToString("D")
                    : requirement.MatchValue,
                SatisfactionMode = requirement.SatisfactionMode,
                ConsumptionPolicy = requirement.ConsumptionPolicy,
                IsOptional = requirement.IsOptional,
                VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                    .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                    .Select(static item => new RuntimeLockParticipantVariableRequirementDto
                    {
                        VariableName = item.VariableName.Trim(),
                        Operator = item.Operator,
                        ExpectedValue = item.ExpectedValue,
                        QuantityEvaluationMode = item.QuantityEvaluationMode
                    })
                    .ToList()
            }).ToList(),
            RequireKeyForLockOperation = normalized.RequireKeyForLockOperation,
                    UseUnlockKeysForLockOperation = normalized.UseUnlockKeysForLockOperation,
                    LockKeyRequirements = normalized.RequireKeyForLockOperation && !normalized.UseUnlockKeysForLockOperation
                ? normalized.LockKeyRequirements.Select(static requirement => new RuntimeLockKeyRequirementDto
                {
                    ObjectId = requirement.RequiredObjectId,
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = string.IsNullOrWhiteSpace(requirement.MatchValue)
                        ? requirement.RequiredObjectId.ToString("D")
                        : requirement.MatchValue,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new RuntimeLockParticipantVariableRequirementDto
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                }).ToList()
                : null
        };
    }

    private static LockOperationRequirements NormalizeLockOperationRequirements(LockOperationRequirements? requirements)
    {
        var source = requirements ?? new LockOperationRequirements();
        return new LockOperationRequirements
        {
            UnlockKeyRequirements = (source.UnlockKeyRequirements ?? new List<LockKeyRequirement>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = string.IsNullOrWhiteSpace(requirement.MatchValue)
                        ? requirement.RequiredObjectId.ToString("D")
                        : requirement.MatchValue,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new LockParticipantVariableRequirement
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList(),
            RequireKeyForLockOperation = source.RequireKeyForLockOperation,
            UseUnlockKeysForLockOperation = source.UseUnlockKeysForLockOperation,
            LockKeyRequirements = (source.LockKeyRequirements ?? new List<LockKeyRequirement>())
                .Where(static requirement => requirement.RequiredObjectId != Guid.Empty)
                .Select(static requirement => new LockKeyRequirement
                {
                    RequiredObjectId = requirement.RequiredObjectId,
                    RequiredQuantity = requirement.RequiredQuantity < 1 ? 1 : requirement.RequiredQuantity,
                    MatchKind = requirement.MatchKind,
                    MatchValue = string.IsNullOrWhiteSpace(requirement.MatchValue)
                        ? requirement.RequiredObjectId.ToString("D")
                        : requirement.MatchValue,
                    SatisfactionMode = requirement.SatisfactionMode,
                    ConsumptionPolicy = requirement.ConsumptionPolicy,
                    IsOptional = requirement.IsOptional,
                    VariableRequirements = (requirement.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static item => !string.IsNullOrWhiteSpace(item.VariableName))
                        .Select(static item => new LockParticipantVariableRequirement
                        {
                            VariableName = item.VariableName.Trim(),
                            Operator = item.Operator,
                            ExpectedValue = item.ExpectedValue,
                            QuantityEvaluationMode = item.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private static double SanitizeCoordinateForPersistence(double value)
    {
        return double.IsFinite(value) ? value : 0;
    }

    private static void StripLinkedRoomInstanceActionsRecursively(GameObject obj)
    {
        if (IsLinkedRoomInstance(obj))
        {
            obj.AvailableActions.Clear();
        }

        foreach (var child in obj.ContainedObjects)
        {
            StripLinkedRoomInstanceActionsRecursively(child);
        }
    }

    private static RuntimeGameObjectDto ToCleanGameObjectDto(
        GameObject obj,
        Guid? designTimeParentObjectId,
        Func<string?, string, string>? resolveImagePath = null,
        Func<string?, string, string>? resolveSoundAssetRef = null,
        int? renderZOrderOverride = null,
        Func<Guid, GameObject?>? resolveDefinition = null)
    {
        var persistedLinkedBaseObjectId = NormalizePersistedLinkedBaseObjectId(obj);
        var objectIdentity = obj.ObjectId == Guid.Empty ? "unknown" : obj.ObjectId.ToString("N");
        var definitionResolver = resolveDefinition ?? (_ => null);
        var effectiveName = EffectiveObjectFieldResolver.GetEffectiveName(obj, definitionResolver);
        var effectiveNameInGame = EffectiveObjectFieldResolver.GetEffectiveNameInGame(obj, definitionResolver);
        var effectiveNameSynonyms = EffectiveObjectFieldResolver.GetEffectiveNameSynonyms(obj, definitionResolver);
        var effectiveDescription = EffectiveObjectFieldResolver.GetEffectiveDescription(obj, definitionResolver);
        var effectiveDefinitionOwnedSource = ResolveEffectiveDefinitionOwnedSource(obj, definitionResolver);
        var normalizedVariants = NormalizeObjectImageVariants(effectiveDefinitionOwnedSource.ImageVariants);

        return new RuntimeGameObjectDto
        {
            ScopeNodeId = obj.ObjectId == Guid.Empty ? null : obj.ObjectId,
            DesignTimeParentObjectId = designTimeParentObjectId,
            LinkedBaseObjectId = persistedLinkedBaseObjectId,
            LinkActionsToBaseObject = obj.LinkActionsToBaseObject || persistedLinkedBaseObjectId.HasValue,
            Name = effectiveName ?? string.Empty,
            ProductionName = obj.ProducerNotes ?? string.Empty,
            NameInGame = string.IsNullOrWhiteSpace(effectiveNameInGame) ? string.Empty : effectiveNameInGame,
            NameSynonyms = NullIfEmpty(NormalizeNameSynonyms(effectiveNameSynonyms)),
            IsInventoriable = obj.IsInventoriable,
            InventoryPointsDefaultValue = obj.InventoryPointsDefaultValue,
            IsContainer = obj.IsContainer,
            ContainerPointsDefaultValue = obj.ContainerPointsDefaultValue,
            IsOpenable = obj.IsOpenable,
            IsOpenDefaultValue = obj.IsOpenDefaultValue,
            IsLockable = obj.IsLockable,
            IsLockedDefaultValue = obj.IsLockedDefaultValue,
            IsActivatable = obj.IsActivatable,
            IsActiveDefaultValue = obj.IsActiveDefaultValue,
            IsHidable = obj.IsHidable,
            IsHiddenDefaultValue = obj.IsHiddenDefaultValue,
            IsQuantifiable = obj.IsQuantifiable,
            Quantity = obj.Quantity,
            QuantifiablePlacementDistributionMode = ParseQuantifiablePlacementDistributionMode(obj.QuantifiablePlacementDistributionMode),
            CompositeRecipe = obj.IsCompositeTarget
                ? new RuntimeCompositeRecipeDto
                {
                    RecipeId = obj.CompositeRecipeId,
                    TargetObjectId = Guid.Empty,
                    IsReversible = obj.IsCompositeReversible,
                    PartRequirementMode = ParseCompositePartRequirementMode(obj.CompositePartRequirementMode),
                    MinimumRequiredPartCount = string.Equals(obj.CompositePartRequirementMode, "MinimumCount", StringComparison.OrdinalIgnoreCase)
                        ? obj.CompositeMinimumRequiredPartCount
                        : null,
                    RequiredParts = obj.CompositeRequiredParts
                        .Where(part => part.PartObjectId != Guid.Empty)
                        .Select(part => new RuntimeCompositePartRequirementDto
                        {
                            PartObjectId = part.PartObjectId,
                            RequiredQuantity = part.RequiredQuantity,
                            MatchKind = part.MatchKind,
                            MatchValue = part.MatchValue,
                            SatisfactionMode = part.SatisfactionMode,
                            ConsumptionPolicy = part.ConsumptionPolicy,
                            OptionalPart = part.OptionalPart,
                            VariableRequirements = (part.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                                .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                                .Select(static requirement => new RuntimeLockParticipantVariableRequirementDto
                                {
                                    VariableName = requirement.VariableName.Trim(),
                                    Operator = requirement.Operator,
                                    ExpectedValue = requirement.ExpectedValue,
                                    QuantityEvaluationMode = requirement.QuantityEvaluationMode
                                })
                                .ToList()
                        }).ToList()
                }
                : null,
            LockOperationRequirements = ToCleanLockOperationRequirementsDto(obj.LockOperationRequirements),
            ImageVariants = normalizedVariants.Count == 0
                ? null
                : normalizedVariants
                    .Select(variant => new RuntimeObjectImageVariantDto
                    {
                        VariantName = variant.VariantName,
                        ImagePathSemantics = "runtimeExportRelative",
                        FullImagePath = string.IsNullOrWhiteSpace(variant.FullImagePath)
                            ? string.Empty
                            : (resolveImagePath is null
                                ? variant.FullImagePath
                                : resolveImagePath(variant.FullImagePath, $"object:{objectIdentity}:variant:{variant.VariantName}:full")),
                        ImageLocalAlignmentRotationDegrees = double.IsFinite(variant.ImageLocalAlignmentRotationDegrees) ? variant.ImageLocalAlignmentRotationDegrees : 0,
                        ImageLocalAlignmentOffsetX = double.IsFinite(variant.ImageLocalAlignmentOffsetX) ? variant.ImageLocalAlignmentOffsetX : 0,
                        ImageLocalAlignmentOffsetY = double.IsFinite(variant.ImageLocalAlignmentOffsetY) ? variant.ImageLocalAlignmentOffsetY : 0,
                        ImageScale = variant.ImageScale <= 0 ? 1 : variant.ImageScale,
                        IsDefault = variant.IsDefault
                    })
                    .ToList(),
            ImageRotationDegrees = obj.ImageRotationDegrees,
            RenderZOrder = renderZOrderOverride ?? (obj.RenderZOrder == 0 ? null : obj.RenderZOrder),
            AuthoredRenderOrder = obj.AuthoredRenderOrder == 0 ? null : obj.AuthoredRenderOrder,
            ImageVariantChooserScript = string.IsNullOrWhiteSpace(effectiveDefinitionOwnedSource.ImageVariantChooserScript)
                ? null
                : effectiveDefinitionOwnedSource.ImageVariantChooserScript,
            Appearance = ToCleanGameObjectAppearanceDto(obj, effectiveDefinitionOwnedSource),
            Description = effectiveDescription ?? string.Empty,
            AdditionalVerbs = NullIfEmpty(obj.AdditionalVerbs.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList()),
            AdditionalDirectionals = NullIfEmpty(obj.AdditionalDirectionals.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList()),
            AdditionalDirectionalTraversalMappings = NullIfEmpty(ToRuntimeDirectionalTraversalMappings(obj.AdditionalDirectionalTraversalMappings)),
            EventSubscriptions = NullIfEmpty(obj.EventSubscriptions.Select(ToRuntimeEventSubscriptionDto).ToList()),
            TimerDefinitions = NullIfEmpty(obj.TimerDefinitions.Select(ToRuntimeTimerDefinitionDto).ToList()),
            SoundEffectLibraryEntries = NullIfEmpty(obj.SoundEffectLibraryEntries
                .Select(entry => ToCleanSoundEffectLibraryEntryDto(
                    entry,
                    resolveSoundAssetRef,
                    $"object:{objectIdentity}:sound:{(entry.SoundEffectId == Guid.Empty ? entry.SoundEffectKey : entry.SoundEffectId.ToString("D"))}"))
                .ToList()),
            AvailableGameActions = NullIfEmpty((obj.LinkActionsToBaseObject || obj.LinkedBaseObjectId.HasValue)
                ? new List<RuntimeCommandActionDto>()
                : ToCleanCommandActionDtos(obj.AvailableActions)
                    .OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(action => action.Id)
                    .ToList()),
            GameProperties = NullIfEmpty(BuildCleanObjectGameProperties(obj, effectiveDefinitionOwnedSource)),
            ContainedObjects = NullIfEmpty(obj.ContainedObjects
                .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(child => ToCleanGameObjectDto(child, obj.ObjectId, resolveImagePath, resolveSoundAssetRef, null, resolveDefinition))
                .ToList())
        };
    }

    private static QuantifiablePlacementDistributionMode? ParseQuantifiablePlacementDistributionMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<QuantifiablePlacementDistributionMode>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static CompositePartRequirementMode? ParseCompositePartRequirementMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<CompositePartRequirementMode>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static int ComputeRoomObjectRenderZOrder(int siblingIndex, int siblingCount)
    {
        var normalizedCount = siblingCount < 1 ? 1 : siblingCount;
        var normalizedIndex = siblingIndex;
        if (normalizedIndex < 0)
        {
            normalizedIndex = 0;
        }
        else if (normalizedIndex >= normalizedCount)
        {
            normalizedIndex = normalizedCount - 1;
        }

        return RoomObjectRenderZBase + (normalizedCount - normalizedIndex);
    }

    private static void ApplyDerivedRoomRenderZOrders(IList<GameObject> roomObjects)
    {
        for (var index = 0; index < roomObjects.Count; index++)
        {
            roomObjects[index].RenderZOrder = ComputeRoomObjectRenderZOrder(index, roomObjects.Count);
        }
    }

    private static List<RuntimeGamePropertyDefinitionDto> BuildCleanObjectGameProperties(
        GameObject obj,
        GameObject effectiveDefinitionOwnedSource)
    {
        var properties = obj.Variables
            .Select(ToCleanGamePropertyDto)
            .ToList();

        UpsertCleanNumericGameProperty(properties, "positionX", SanitizeCoordinateForPersistence(obj.PositionX));
        UpsertCleanNumericGameProperty(properties, "positionY", SanitizeCoordinateForPersistence(obj.PositionY));
        UpsertCleanNumericGameProperty(properties, "imageRotationDegrees", double.IsFinite(obj.ImageRotationDegrees) ? obj.ImageRotationDegrees : 0);
        UpsertCleanNumericGameProperty(properties, "imageScale", effectiveDefinitionOwnedSource.ResolveImageScale(null));

        return properties
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void UpsertCleanNumericGameProperty(
        List<RuntimeGamePropertyDefinitionDto> properties,
        string name,
        double value)
    {
        var sanitized = double.IsFinite(value) ? value : 0;
        var existing = properties.FirstOrDefault(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            properties.Add(new RuntimeGamePropertyDefinitionDto
            {
                Id = Guid.Empty,
                Name = name,
                DefaultValue = sanitized.ToString(CultureInfo.InvariantCulture),
                ValueRestriction = GamePropertyValueRestriction.Numeric,
                Lifetime = GamePropertyLifetime.Singleton
            });
            return;
        }

        existing.DefaultValue = sanitized.ToString(CultureInfo.InvariantCulture);
        existing.ValueRestriction = GamePropertyValueRestriction.Numeric;
        existing.Lifetime = GamePropertyLifetime.Singleton;
    }

    private static GamePropertyDefinitionDto ToVariableDto(GamePropertyDefinition variable)
    {
        return new GamePropertyDefinitionDto
        {
            Id = variable.Id,
            Name = variable.Name,
            DefaultValue = variable.DefaultValue,
            ValueRestriction = variable.ValueRestriction,
            Lifetime = variable.Lifetime,
            SharedVariableId = variable.SharedVariableId
        };
    }

    private static SoundEffectLibraryEntryDto ToSoundEffectLibraryEntryDto(SoundEffectLibraryEntry entry)
    {
        var repeatMode = ParseSoundEffectRepeatMode(entry.RepeatMode);
        var repeatCount = entry.RepeatCount;
        var repeatDurationMs = entry.RepeatDurationMs;
        var repeatIntervalMs = entry.RepeatIntervalMs;
        var repeatCooldownMs = entry.RepeatCooldownMs;
        var replayPolicy = ParseSoundEffectReplayPolicy(entry.ReplayPolicy);
        NormalizeRepeatFieldsForPersistence(
            repeatMode,
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        return new SoundEffectLibraryEntryDto
        {
            SoundEffectId = entry.SoundEffectId,
            SoundEffectKey = entry.SoundEffectKey,
            DisplayName = entry.DisplayName,
            Category = entry.Category,
            AssetRef = entry.AssetRef,
            SoundEffectLane = entry.SoundEffectLane,
            BaseVolumeDb = entry.BaseVolumeDb,
            FadeInMs = entry.FadeInMs,
            FadeOutMs = entry.FadeOutMs,
            RepeatMode = repeatMode,
            ReplayPolicy = replayPolicy,
            RepeatCount = repeatCount,
            StartDelayMs = entry.StartDelayMs,
            RepeatIntervalMs = repeatIntervalMs,
            DurationMs = entry.DurationMs,
            MaxPlayDurationMs = entry.MaxPlayDurationMs,
            RepeatDurationMs = repeatDurationMs,
            RepeatCooldownMs = repeatCooldownMs,
            ConcurrencyGroup = entry.ConcurrencyGroup,
            ConcurrencyGroupImportance = entry.ConcurrencyGroupImportance,
            Importance = entry.Importance
        };
    }

    private static RuntimeSoundEffectLibraryEntryDto ToCleanSoundEffectLibraryEntryDto(
        SoundEffectLibraryEntry entry,
        Func<string?, string, string>? resolveSoundAssetRef,
        string reference)
    {
        var repeatMode = ParseSoundEffectRepeatMode(entry.RepeatMode);
        var repeatCount = entry.RepeatCount;
        var repeatDurationMs = entry.RepeatDurationMs;
        var repeatIntervalMs = entry.RepeatIntervalMs;
        var repeatCooldownMs = entry.RepeatCooldownMs;
        var replayPolicy = ParseSoundEffectReplayPolicy(entry.ReplayPolicy);
        NormalizeRepeatFieldsForPersistence(
            repeatMode,
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        var assetRef = entry.AssetRef?.Trim() ?? string.Empty;
        if (resolveSoundAssetRef is not null && !string.IsNullOrWhiteSpace(assetRef))
        {
            assetRef = resolveSoundAssetRef(assetRef, reference);
        }

        return new RuntimeSoundEffectLibraryEntryDto
        {
            SoundEffectId = entry.SoundEffectId,
            SoundEffectKey = entry.SoundEffectKey,
            DisplayName = entry.DisplayName,
            Category = entry.Category,
            AssetRef = assetRef,
            SoundEffectLane = entry.SoundEffectLane,
            BaseVolumeDb = entry.BaseVolumeDb,
            FadeInMs = entry.FadeInMs,
            FadeOutMs = entry.FadeOutMs,
            RepeatMode = repeatMode,
            ReplayPolicy = replayPolicy,
            RepeatCount = repeatCount,
            StartDelayMs = entry.StartDelayMs,
            RepeatIntervalMs = repeatIntervalMs,
            DurationMs = entry.DurationMs,
            MaxPlayDurationMs = entry.MaxPlayDurationMs,
            RepeatDurationMs = repeatDurationMs,
            RepeatCooldownMs = repeatCooldownMs,
            ConcurrencyGroup = entry.ConcurrencyGroup,
            ConcurrencyGroupImportance = entry.ConcurrencyGroupImportance,
            Importance = entry.Importance
        };
    }

    private static SharedVariableDefinitionDto ToSharedVariableDto(SharedVariableDefinition sharedVariable)
    {
        return new SharedVariableDefinitionDto
        {
            Id = sharedVariable.Id,
            Name = string.IsNullOrWhiteSpace(sharedVariable.Name)
                ? null
                : sharedVariable.Name
        };
    }

    private static SharedVariableParticipantDto ToSharedVariableParticipantDto(SharedVariableParticipant participant)
    {
        return new SharedVariableParticipantDto
        {
            Kind = participant.Kind,
            OwnerId = participant.OwnerId,
            VariableName = participant.VariableName,
            Leg = participant.Leg
        };
    }

    private static ProjectUiStateDto ToProjectUiStateDto(ProjectUiState uiState)
    {
        return new ProjectUiStateDto
        {
            PlanetName = uiState.PlanetName,
            CountryName = uiState.CountryName,
            AreaName = uiState.AreaName,
            MapDesignerAreaId = uiState.MapDesignerAreaId,
            RoomId = uiState.RoomId,
            SelectedWorkspaceTabIndex = uiState.SelectedWorkspaceTabIndex,
            LastSelectedNodePath = uiState.LastSelectedNodePath,
            LastTreeValidationActionId = uiState.LastTreeValidationActionId,
            LastTreeValidationCompletionMode = uiState.LastTreeValidationCompletionMode,
            QuickAccessRecentSectionRatio = uiState.QuickAccessRecentSectionRatio,
            RecentHierarchyNodes = (uiState.RecentHierarchyNodes ?? [])
                .Select(ToProjectHierarchyQuickAccessEntryDto)
                .ToList(),
            HierarchyBookmarks = (uiState.HierarchyBookmarks ?? [])
                .Select(ToProjectHierarchyQuickAccessEntryDto)
                .ToList()
        };
    }

    private static ProjectHierarchyQuickAccessEntryDto ToProjectHierarchyQuickAccessEntryDto(ProjectHierarchyQuickAccessEntry entry)
    {
        return new ProjectHierarchyQuickAccessEntryDto
        {
            NodePath = entry.NodePath,
            DisplayName = entry.DisplayName,
            NodeTypeLabel = entry.NodeTypeLabel
        };
    }

    private static ProjectUiState ToProjectUiStateModel(ProjectUiStateDto? uiState)
    {
        if (uiState is null)
        {
            return new ProjectUiState();
        }

        return new ProjectUiState
        {
            PlanetName = uiState.PlanetName,
            CountryName = uiState.CountryName,
            AreaName = uiState.AreaName,
            MapDesignerAreaId = uiState.MapDesignerAreaId,
            RoomId = uiState.RoomId,
            SelectedWorkspaceTabIndex = uiState.SelectedWorkspaceTabIndex,
            LastSelectedNodePath = uiState.LastSelectedNodePath,
            LastTreeValidationActionId = uiState.LastTreeValidationActionId,
            LastTreeValidationCompletionMode = uiState.LastTreeValidationCompletionMode,
            QuickAccessRecentSectionRatio = uiState.QuickAccessRecentSectionRatio is > 0 and < 1
                ? uiState.QuickAccessRecentSectionRatio
                : 0.5,
            RecentHierarchyNodes = (uiState.RecentHierarchyNodes ?? [])
                .Select(ToProjectHierarchyQuickAccessEntryModel)
                .ToList(),
            HierarchyBookmarks = (uiState.HierarchyBookmarks ?? [])
                .Select(ToProjectHierarchyQuickAccessEntryModel)
                .ToList()
        };
    }

    private static ProjectHierarchyQuickAccessEntry ToProjectHierarchyQuickAccessEntryModel(ProjectHierarchyQuickAccessEntryDto entry)
    {
        return new ProjectHierarchyQuickAccessEntry
        {
            NodePath = entry.NodePath,
            DisplayName = entry.DisplayName,
            NodeTypeLabel = entry.NodeTypeLabel
        };
    }

    private static RuntimeGamePropertyDefinitionDto ToCleanGamePropertyDto(GamePropertyDefinition variable)
    {
        var variableId = IsBuiltInObjectVariableName(variable.Name)
            ? Guid.Empty
            : variable.Id;

        return new RuntimeGamePropertyDefinitionDto
        {
            Id = variableId,
            Name = variable.Name,
            DefaultValue = variable.DefaultValue,
            ValueRestriction = variable.ValueRestriction,
            Lifetime = variable.Lifetime,
            SharedVariableId = variable.SharedVariableId
        };
    }

    private static bool IsBuiltInObjectVariableName(string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        return string.Equals(variableName, "isMovable", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "inventoryPoints", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "containerPoints", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "containerPointsRemaining", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "capacityPointShareDivider", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "isOpen", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "isLocked", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "isActive", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "isHidden", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "isQuantifiable", StringComparison.OrdinalIgnoreCase)
               || string.Equals(variableName, "quantity", StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeProcedureDefinitionDto ToCleanProcedureDefinitionDto(ProcedureDefinition procedure)
    {
        return new RuntimeProcedureDefinitionDto
        {
            Id = procedure.Id,
            Name = procedure.Name,
            ProcedureSummary = procedure.ProcedureSummary,
            ProcedureDescription = procedure.ProcedureDescription,
            Participants = (procedure.Participants ?? new List<ProcedureParticipantRequirement>())
                .Select(participant => new RuntimeProcedureParticipantRequirementDto
                {
                    ObjectId = participant.ObjectId,
                    MatchKind = participant.MatchKind,
                    MatchValue = participant.MatchValue,
                    SatisfactionMode = participant.SatisfactionMode,
                    Quantity = participant.Quantity,
                    ConsumptionPolicy = participant.ConsumptionPolicy,
                    OptionalPart = participant.OptionalPart,
                    VariableRequirements = (participant.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                        .Select(static requirement => new RuntimeLockParticipantVariableRequirementDto
                        {
                            VariableName = requirement.VariableName.Trim(),
                            Operator = requirement.Operator,
                            ExpectedValue = string.IsNullOrWhiteSpace(requirement.ExpectedValue)
                                ? null
                                : requirement.ExpectedValue,
                            QuantityEvaluationMode = requirement.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList(),
            ParticipantMutations = (procedure.ParticipantMutations ?? new List<ProcedureParticipantMutation>())
                .Select(mutation => new RuntimeProcedureParticipantMutationDto
                {
                    ObjectId = mutation.ObjectId,
                    Operation = mutation.Operation,
                    VariableName = mutation.VariableName,
                    Value = string.IsNullOrWhiteSpace(mutation.Value) ? null : mutation.Value,
                    Delta = mutation.Delta
                })
                .ToList()
        };
    }

    private static ProcedureDefinitionDto ToProcedureDefinitionDto(ProcedureDefinition procedure)
    {
        return new ProcedureDefinitionDto
        {
            Id = procedure.Id,
            Name = procedure.Name,
            ProcedureSummary = procedure.ProcedureSummary,
            ProcedureDescription = procedure.ProcedureDescription,
            Participants = (procedure.Participants ?? new List<ProcedureParticipantRequirement>())
                .Select(participant => new ProcedureParticipantRequirementDto
                {
                    ObjectId = participant.ObjectId,
                    MatchKind = participant.MatchKind,
                    MatchValue = participant.MatchValue,
                    SatisfactionMode = participant.SatisfactionMode,
                    Quantity = participant.Quantity,
                    ConsumptionPolicy = participant.ConsumptionPolicy,
                    OptionalPart = participant.OptionalPart,
                    VariableRequirements = (participant.VariableRequirements ?? new List<LockParticipantVariableRequirement>())
                        .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                        .Select(static requirement => new ProjectLockParticipantVariableRequirementDto
                        {
                            VariableName = requirement.VariableName.Trim(),
                            Operator = requirement.Operator,
                            ExpectedValue = string.IsNullOrWhiteSpace(requirement.ExpectedValue)
                                ? null
                                : requirement.ExpectedValue,
                            QuantityEvaluationMode = requirement.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList(),
            ParticipantMutations = (procedure.ParticipantMutations ?? new List<ProcedureParticipantMutation>())
                .Select(mutation => new ProcedureParticipantMutationDto
                {
                    ObjectId = mutation.ObjectId,
                    Operation = mutation.Operation,
                    VariableName = mutation.VariableName,
                    Value = mutation.Value,
                    Delta = mutation.Delta
                })
                .ToList()
        };
    }

    private static ProcedureDefinition ToProcedureDefinitionModel(ProcedureDefinitionDto dto)
    {
        return new ProcedureDefinition
        {
            Id = EnsureScopeId(dto.Id, $"procedure '{dto.Name}'"),
            Name = string.IsNullOrWhiteSpace(dto.Name) ? "Procedure" : dto.Name,
            ProcedureSummary = dto.ProcedureSummary ?? string.Empty,
            ProcedureDescription = dto.ProcedureDescription ?? string.Empty,
            Participants = (dto.Participants ?? new List<ProcedureParticipantRequirementDto>())
                .Select(participant => new ProcedureParticipantRequirement
                {
                    ObjectId = participant.ObjectId,
                    DisplayName = string.Empty,
                    MatchKind = participant.MatchKind,
                    MatchValue = participant.MatchValue ?? string.Empty,
                    SatisfactionMode = participant.SatisfactionMode,
                    Quantity = participant.Quantity,
                    ConsumptionPolicy = participant.ConsumptionPolicy,
                    OptionalPart = participant.OptionalPart,
                    VariableRequirements = (participant.VariableRequirements ?? new List<ProjectLockParticipantVariableRequirementDto>())
                        .Where(static requirement => !string.IsNullOrWhiteSpace(requirement.VariableName))
                        .Select(static requirement => new LockParticipantVariableRequirement
                        {
                            VariableName = requirement.VariableName.Trim(),
                            Operator = requirement.Operator,
                            ExpectedValue = requirement.ExpectedValue,
                            QuantityEvaluationMode = requirement.QuantityEvaluationMode
                        })
                        .ToList()
                })
                .ToList(),
            ParticipantMutations = (dto.ParticipantMutations ?? new List<ProcedureParticipantMutationDto>())
                .Select(mutation => new ProcedureParticipantMutation
                {
                    ObjectId = mutation.ObjectId,
                    Operation = mutation.Operation,
                    VariableName = mutation.VariableName ?? string.Empty,
                    Value = mutation.Value ?? string.Empty,
                    Delta = mutation.Delta
                })
                .ToList()
        };
    }

    private static List<RuntimeCommandActionDto> ToCleanCommandActionDtos(IReadOnlyCollection<CommandAction>? actions)
    {
        var safeActions = (actions ?? Array.Empty<CommandAction>()).ToList();
        var actionLookup = safeActions
            .Where(static action => action.Id != Guid.Empty)
            .GroupBy(static action => action.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        return safeActions
            .Select(action => ToCleanCommandActionDto(action, actionLookup))
            .ToList();
    }

    private static RuntimeCommandActionDto ToCleanCommandActionDto(CommandAction action, IReadOnlyDictionary<Guid, CommandAction> actionLookup)
    {
        var containerTransfer = ActionPayloadAccessors.GetContainerTransfer(action);
        var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(action);
        var materializeSourceObjectId = ActionPayloadAccessors.GetMaterializeSourceObjectId(action);
        var procedureId = ActionPayloadAccessors.GetProcedureId(action);
        var startTimerKey = ActionPayloadAccessors.GetStartTimerKey(action);
        var startTimerOwnerScopeKindOverride = ActionPayloadAccessors.GetStartTimerOwnerScopeKindOverride(action);
        var cancelTimerKey = ActionPayloadAccessors.GetCancelTimerKey(action);
        var cancelTimerScopeQualifierKind = ActionPayloadAccessors.GetCancelTimerScopeQualifierKind(action);
        var cancelTimerScopeQualifierId = ActionPayloadAccessors.GetCancelTimerScopeQualifierId(action);
        var compositeByTargetPayload = ActionPayloadAccessors.GetCompositeByTargetPayload(action);
        var compositeByPartsPayload = ActionPayloadAccessors.GetCompositeByPartsPayload(action);
        var breakCompositePayload = ActionPayloadAccessors.GetBreakCompositePayload(action);
        var movePayload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(action);
        var moveByPointsPayload = ActionPayloadAccessors.GetMoveRoomObjectByPointsPayload(action);
        var rotatePayload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(action);
        var stackPayload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(action);
        var setActivePayload = ActionPayloadAccessors.GetSetActiveRoomObjectPayload(action);
        var selectByPointPayload = ActionPayloadAccessors.GetSelectRoomObjectByPointPayload(action);
        var clearSelectionsPayload = ActionPayloadAccessors.GetClearRoomObjectSelectionsPayload(action);

        return new RuntimeCommandActionDto
        {
            Id = action.Id,
            Name = action.Name,
            ActionType = action.ActionType,
            NoVerbLinkage = action.NoVerbLinkage,
            VerbListText = action.VerbListText,
            Verbs = action.Verbs.Count > 1 ? action.Verbs.ToList() : null,
            Direction = NormalizeDirectionToken(action.DirectionQualifierText),
            OutcomeMessageMap = BuildOutcomeMessageMapForPersistence(action),
            OutcomeSoundEffectsMap = BuildCleanOutcomeSoundEffectsMapForPersistence(action),
            Payload = action.ActionType switch
            {
                CommandActionType.EchoMessage => BuildCleanEchoMessagePayloadElement(),
                CommandActionType.CheckGameProperty => BuildCleanCheckGamePropertyPayloadElement(ActionPayloadAccessors.GetCheckPropertyName(action), ActionPayloadAccessors.GetCheckExpectedValue(action)),
                CommandActionType.ClearActiveRoomObjects => BuildCleanClearActiveRoomObjectsPayloadElement(ActionPayloadAccessors.GetClearActiveRoomObjectsPayload(action).ClearScope),
                CommandActionType.CloseObject => BuildCleanCloseObjectPayloadElement(),
                CommandActionType.InvokeProcedure => BuildCleanInvokeProcedurePayloadElement(procedureId),
                CommandActionType.LockObject => BuildCleanLockObjectPayloadElement(),
                CommandActionType.StartTimer => BuildCleanStartTimerPayloadElement(startTimerKey, startTimerOwnerScopeKindOverride),
                CommandActionType.CancelTimer => BuildCleanCancelTimerPayloadElement(cancelTimerKey, cancelTimerScopeQualifierKind, cancelTimerScopeQualifierId),
                CommandActionType.MaterializeObjectCopy => BuildCleanMaterializeObjectCopyPayloadElement(materializeSourceObjectId),
                CommandActionType.MoveRoomObjectOnGrid => BuildCleanMoveRoomObjectOnGridPayloadElement(movePayload),
                CommandActionType.MoveRoomObjectByPoints => BuildCleanMoveRoomObjectByPointsPayloadElement(moveByPointsPayload),
                CommandActionType.RotateRoomObjectOnGrid => BuildCleanRotateRoomObjectOnGridPayloadElement(rotatePayload),
                CommandActionType.StackRoomObjectOnAnother => BuildCleanStackRoomObjectOnAnotherPayloadElement(stackPayload),
                CommandActionType.NavigateDirection => BuildCleanNavigateDirectionPayloadElement(),
                CommandActionType.NavigateToAdjacent => BuildCleanNavigateToAdjacentPayloadElement(),
                CommandActionType.OpenObject => BuildCleanOpenObjectPayloadElement(),
                CommandActionType.PutObjectInContainer => BuildCleanPutObjectInContainerPayloadElement(containerTransfer.TargetContainerId),
                CommandActionType.RemoveObjectFromContainer => BuildCleanRemoveObjectFromContainerPayloadElement(containerTransfer.TargetContainerId),
                CommandActionType.BuildCompositeByTarget => BuildCleanBuildCompositeByTargetPayloadElement(compositeByTargetPayload),
                CommandActionType.BuildCompositeByParts => BuildCleanBuildCompositeByPartsPayloadElement(compositeByPartsPayload),
                CommandActionType.BreakCompositeItem => BuildCleanBreakCompositeItemPayloadElement(breakCompositePayload),
                CommandActionType.SetActiveRoomObject => BuildCleanSetActiveRoomObjectPayloadElement(setActivePayload.SelectionCueEffectKey),
                CommandActionType.SelectRoomObjectByPoint => BuildCleanSelectRoomObjectByPointPayloadElement(selectByPointPayload.SelectionCueEffectKey),
                CommandActionType.SetGameProperty => BuildCleanSetGamePropertyPayloadElement(ActionPayloadAccessors.GetSetPropertyName(action), ActionPayloadAccessors.GetSetPropertyValue(action)),
                CommandActionType.SetFlag => BuildCleanSetFlagPayloadElement(ActionPayloadAccessors.GetSetFlagName(action), ActionPayloadAccessors.GetSetFlagValue(action)),
                CommandActionType.Synonym => BuildCleanSynonymPayloadElement(synonymTargetActionId),
                CommandActionType.ClearRoomObjectSelections => BuildCleanClearRoomObjectSelectionsPayloadElement(clearSelectionsPayload.ClearScope),
                CommandActionType.UnlockObject => BuildCleanUnlockObjectPayloadElement(),
                CommandActionType.LinkedActions => BuildCleanLinkedActionsPayloadElement(action, actionLookup),
                _ => null
            },
            ChildCommandForwardingMode = ParseChildCommandForwardingMode(action.ChildCommandForwardingMode),
            SimilarChildDispatchMode = action.SimilarChildDispatchMode == SimilarChildDispatchMode.SingleMatchingChild
                ? null
                : ParseSimilarChildDispatchMode(action.SimilarChildDispatchMode)
        };
    }

    private static List<T>? NullIfEmpty<T>(List<T> items)
    {
        return items.Count == 0 ? null : items;
    }

    private static Dictionary<string, string>? BuildOutcomeMessageMapForPersistence(CommandAction action)
    {
        var actionType = action.ActionType;
        var map = NormalizeOutcomeMessageMap(actionType, action.OutcomeMessageMap);

        if (map.Count == 0)
        {
            return null;
        }

        return map
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, List<OutcomeSoundEffectCueDto>>? BuildOutcomeSoundEffectsMapForPersistence(CommandAction action)
    {
        var actionType = action.ActionType;
        var map = NormalizeOutcomeSoundEffectsMap(actionType, action.OutcomeSoundEffectsMap);
        if (map.Count == 0)
        {
            return null;
        }

        return map
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Select(static cue => new OutcomeSoundEffectCueDto
                {
                    SoundEffectId = cue.SoundEffectId,
                    SoundEffectKeyHint = NullIfWhiteSpace(cue.SoundEffectKeyHint),
                    Enabled = cue.Enabled,
                    LateDeliveryPolicy = cue.LateDeliveryPolicy,
                    MaxLateMs = cue.MaxLateMs
                }).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, List<RuntimeOutcomeSoundEffectCueDto>>? BuildCleanOutcomeSoundEffectsMapForPersistence(CommandAction action)
    {
        var actionType = action.ActionType;
        var map = NormalizeOutcomeSoundEffectsMap(actionType, action.OutcomeSoundEffectsMap);
        if (map.Count == 0)
        {
            return null;
        }

        return map
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Select(static cue => new RuntimeOutcomeSoundEffectCueDto
                {
                    SoundEffectId = cue.SoundEffectId,
                    SoundEffectKeyHint = NullIfWhiteSpace(cue.SoundEffectKeyHint),
                    Enabled = cue.Enabled,
                    LateDeliveryPolicy = cue.LateDeliveryPolicy,
                    MaxLateMs = cue.MaxLateMs
                }).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> NormalizeOutcomeMessageMap(
        CommandActionType actionType,
        IReadOnlyDictionary<string, string>? source)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return normalized;
        }

        foreach (var (key, value) in source)
        {
            var token = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (RuntimeActionResultCodeRegistry.TryGetDescriptor(actionType, token, out var descriptor))
            {
                normalized[descriptor.Token] = value ?? string.Empty;
                continue;
            }

            normalized[token] = value ?? string.Empty;
        }

        return normalized;
    }

    private static Dictionary<string, List<OutcomeSoundEffectCue>> NormalizeOutcomeSoundEffectsMap(
        CommandActionType actionType,
        IReadOnlyDictionary<string, List<OutcomeSoundEffectCueDto>>? source)
    {
        var normalized = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return normalized;
        }

        foreach (var (key, value) in source)
        {
            var token = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (RuntimeActionResultCodeRegistry.TryGetDescriptor(actionType, token, out var descriptor))
            {
                token = descriptor.Token;
            }

            normalized[token] = (value ?? new List<OutcomeSoundEffectCueDto>())
                .Where(static cue => cue is not null && cue.SoundEffectId != Guid.Empty)
                .Select(static cue => new OutcomeSoundEffectCue
                {
                    SoundEffectId = cue.SoundEffectId,
                    SoundEffectKeyHint = cue.SoundEffectKeyHint,
                    Enabled = cue.Enabled,
                    LateDeliveryPolicy = cue.LateDeliveryPolicy,
                    MaxLateMs = cue.MaxLateMs
                })
                .ToList();
        }

        return normalized;
    }

    private static Dictionary<string, List<OutcomeSoundEffectCue>> NormalizeOutcomeSoundEffectsMap(
        CommandActionType actionType,
        IReadOnlyDictionary<string, List<OutcomeSoundEffectCue>>? source)
    {
        var normalized = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return normalized;
        }

        foreach (var (key, value) in source)
        {
            var token = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (RuntimeActionResultCodeRegistry.TryGetDescriptor(actionType, token, out var descriptor))
            {
                token = descriptor.Token;
            }

            normalized[token] = (value ?? new List<OutcomeSoundEffectCue>())
                .Where(static cue => cue is not null && cue.SoundEffectId != Guid.Empty)
                .Select(CommandAction.CloneOutcomeSoundEffectCue)
                .ToList();
        }

        return normalized;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool IsCompositeAction(CommandActionType actionType)
    {
        return actionType == CommandActionType.BuildCompositeByTarget
               || actionType == CommandActionType.BuildCompositeByParts
               || actionType == CommandActionType.BreakCompositeItem;
    }

    private static GamePropertyDefinition ToVariableModel(GamePropertyDefinitionDto variable)
    {
        return new GamePropertyDefinition
        {
            Id = EnsureScopeId(variable.Id, $"variable '{variable.Name}'"),
            Name = string.IsNullOrWhiteSpace(variable.Name) ? "newVariable" : variable.Name,
            DefaultValue = variable.DefaultValue,
            ValueRestriction = Enum.IsDefined(variable.ValueRestriction) ? variable.ValueRestriction : GamePropertyValueRestriction.Unrestricted,
            Lifetime = Enum.IsDefined(variable.Lifetime) ? variable.Lifetime : GamePropertyLifetime.Singleton,
            SharedVariableId = variable.SharedVariableId
        };
    }

    private static SoundEffectLibraryEntry ToSoundEffectLibraryEntryModel(SoundEffectLibraryEntryDto dto)
    {
        return new SoundEffectLibraryEntry
        {
            SoundEffectId = dto.SoundEffectId == Guid.Empty ? Guid.NewGuid() : dto.SoundEffectId,
            SoundEffectKey = dto.SoundEffectKey ?? string.Empty,
            DisplayName = dto.DisplayName ?? string.Empty,
            Category = dto.Category,
            AssetRef = dto.AssetRef ?? string.Empty,
            SoundEffectLane = dto.SoundEffectLane,
            BaseVolumeDb = dto.BaseVolumeDb,
            FadeInMs = dto.FadeInMs,
            FadeOutMs = dto.FadeOutMs,
            RepeatMode = dto.RepeatMode.ToString(),
            ReplayPolicy = dto.ReplayPolicy.ToString(),
            RepeatCount = dto.RepeatCount,
            StartDelayMs = dto.StartDelayMs,
            RepeatIntervalMs = dto.RepeatIntervalMs,
            DurationMs = dto.DurationMs,
            MaxPlayDurationMs = dto.MaxPlayDurationMs,
            RepeatDurationMs = dto.RepeatDurationMs,
            RepeatCooldownMs = dto.RepeatCooldownMs,
            ConcurrencyGroup = dto.ConcurrencyGroup,
            ConcurrencyGroupImportance = dto.ConcurrencyGroupImportance,
            Importance = dto.Importance
        };
    }

    private static SoundEffectRepeatMode ParseSoundEffectRepeatMode(string? repeatMode)
    {
        return repeatMode?.Trim() switch
        {
            "RepeatCount" or "Count" => SoundEffectRepeatMode.RepeatCount,
            "RepeatForDuration" or "Duration" or "Loop" => SoundEffectRepeatMode.RepeatForDuration,
            "UntilCanceled" or "UntilCancelled" or "UntilCancel" => SoundEffectRepeatMode.UntilCanceled,
            _ => SoundEffectRepeatMode.None
        };
    }

    private static SoundEffectReplayPolicy ParseSoundEffectReplayPolicy(string? replayPolicy)
    {
        return replayPolicy?.Trim() switch
        {
            "CancelPreviousAtNextPlay" => SoundEffectReplayPolicy.CancelPreviousAtNextPlay,
            "IgnoreIfAlreadyPlaying" => SoundEffectReplayPolicy.IgnoreIfAlreadyPlaying,
            _ => SoundEffectReplayPolicy.PlayAgain
        };
    }

    private static void NormalizeRepeatFieldsForPersistence(
        SoundEffectRepeatMode repeatMode,
        ref int? repeatCount,
        ref int? repeatDurationMs,
        ref int? repeatIntervalMs,
        ref int? repeatCooldownMs)
    {
        switch (repeatMode)
        {
            case SoundEffectRepeatMode.None:
                repeatCount = null;
                repeatDurationMs = null;
                repeatIntervalMs = null;
                repeatCooldownMs = null;
                return;

            case SoundEffectRepeatMode.RepeatCount:
                repeatDurationMs = null;
                return;

            case SoundEffectRepeatMode.RepeatForDuration:
                repeatCount = null;
                return;

            case SoundEffectRepeatMode.UntilCanceled:
                repeatCount = null;
                repeatDurationMs = null;
                return;
        }
    }

    private static List<LinkedActionReference> NormalizeLinkedActionReferences(IEnumerable<LinkedActionReference>? links)
    {
        return (links ?? Array.Empty<LinkedActionReference>())
            .Where(static link => link.ActionId != Guid.Empty)
            .Select(link => new LinkedActionReference
            {
                ActionId = link.ActionId,
                RunWhen = ParseLinkedActionRunWhen(link.RunWhen),
                Order = Math.Max(0, link.Order)
            })
            .OrderBy(static link => link.Order)
            .ThenBy(static link => link.ActionId)
            .ToList();
    }

    private static void AddNodeIdsForBranch(
        List<int> targetNodeIds,
        IEnumerable<LinkedActionReference> links,
        LinkedActionRunWhen runWhen,
        IReadOnlyDictionary<Guid, int> nodeIdsByActionId)
    {
        foreach (var link in links.Where(link => link.RunWhen == runWhen))
        {
            if (!nodeIdsByActionId.TryGetValue(link.ActionId, out var childNodeId))
            {
                continue;
            }

            targetNodeIds.Add(childNodeId);
        }
    }

    private static List<ProjectCommandActionReferenceDto> BuildProjectLinkedFlowNodes(
        CommandAction rootAction,
        IReadOnlyDictionary<Guid, CommandAction> actionLookup)
    {
        var rootLinks = NormalizeLinkedActionReferences(rootAction.LinkedActions);
        if (rootLinks.Count == 0)
        {
            return new List<ProjectCommandActionReferenceDto>();
        }

        var orderedActionIds = new List<Guid>();
        var nodeIdsByActionId = new Dictionary<Guid, int>();
        var queuedActionIds = new HashSet<Guid>();
        var pending = new Queue<Guid>();

        foreach (var rootLink in rootLinks)
        {
            if (!actionLookup.ContainsKey(rootLink.ActionId) || !queuedActionIds.Add(rootLink.ActionId))
            {
                continue;
            }

            pending.Enqueue(rootLink.ActionId);
        }

        while (pending.Count > 0)
        {
            var actionId = pending.Dequeue();
            if (nodeIdsByActionId.ContainsKey(actionId) || !actionLookup.TryGetValue(actionId, out var action))
            {
                continue;
            }

            var nextNodeId = nodeIdsByActionId.Count + 1;
            nodeIdsByActionId[actionId] = nextNodeId;
            orderedActionIds.Add(actionId);

            foreach (var link in NormalizeLinkedActionReferences(action.LinkedActions))
            {
                if (!actionLookup.ContainsKey(link.ActionId) || queuedActionIds.Contains(link.ActionId))
                {
                    continue;
                }

                queuedActionIds.Add(link.ActionId);
                pending.Enqueue(link.ActionId);
            }
        }

        if (orderedActionIds.Count == 0)
        {
            return new List<ProjectCommandActionReferenceDto>();
        }

        var nodesByActionId = new Dictionary<Guid, ProjectCommandActionReferenceDto>();
        foreach (var actionId in orderedActionIds)
        {
            if (!actionLookup.TryGetValue(actionId, out var action))
            {
                continue;
            }

            var actionLinks = NormalizeLinkedActionReferences(action.LinkedActions);
            var node = new ProjectCommandActionReferenceDto
            {
                NodeId = nodeIdsByActionId[actionId],
                ActionId = actionId,
                OnAlwaysNodeIds = new List<int>(),
                OnSuccessNodeIds = new List<int>(),
                OnFailureNodeIds = new List<int>()
            };

            AddNodeIdsForBranch(node.OnAlwaysNodeIds, actionLinks, LinkedActionRunWhen.Always, nodeIdsByActionId);
            AddNodeIdsForBranch(node.OnSuccessNodeIds, actionLinks, LinkedActionRunWhen.OnSuccess, nodeIdsByActionId);
            AddNodeIdsForBranch(node.OnFailureNodeIds, actionLinks, LinkedActionRunWhen.OnFailure, nodeIdsByActionId);
            nodesByActionId[actionId] = node;
        }

        var entryLink = rootLinks.FirstOrDefault(link => nodesByActionId.ContainsKey(link.ActionId));
        if (entryLink is not null && nodesByActionId.TryGetValue(entryLink.ActionId, out var entryNode))
        {
            foreach (var extraRootLink in rootLinks.Skip(1))
            {
                if (!nodeIdsByActionId.TryGetValue(extraRootLink.ActionId, out var extraNodeId))
                {
                    continue;
                }

                var targetList = extraRootLink.RunWhen switch
                {
                    LinkedActionRunWhen.OnSuccess => entryNode.OnSuccessNodeIds,
                    LinkedActionRunWhen.OnFailure => entryNode.OnFailureNodeIds,
                    _ => entryNode.OnAlwaysNodeIds
                };

                if (!targetList.Contains(extraNodeId))
                {
                    targetList.Add(extraNodeId);
                }
            }
        }

        return orderedActionIds
            .Where(nodesByActionId.ContainsKey)
            .Select(actionId => nodesByActionId[actionId])
            .ToList();
    }

    private static ProjectCommandActionPayloadBaseDto? BuildLinkedActionsPayloadElement(
        CommandAction rootAction,
        IReadOnlyDictionary<Guid, CommandAction> actionLookup)
    {
        var nodeDtos = BuildProjectLinkedFlowNodes(rootAction, actionLookup);
        if (nodeDtos.Count == 0)
        {
            return null;
        }

        return new ProjectLinkedFlowActionPayloadDto
        {
            LinkedActions = nodeDtos
        };
    }

    private static ProjectCommandActionPayloadBaseDto? BuildSynonymPayloadElement(Guid? targetActionId)
    {
        if (!targetActionId.HasValue || targetActionId.Value == Guid.Empty)
        {
            return null;
        }

        return new ProjectSynonymActionPayloadDto
        {
            TargetActionId = targetActionId.Value
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildSetFlagPayloadElement(string flagName, bool flagValue)
    {
        return new ProjectSetFlagActionPayloadDto
        {
            FlagName = flagName,
            FlagValue = flagValue
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildCheckGamePropertyPayloadElement(string propertyName, bool expectedValue)
    {
        return new ProjectCheckGamePropertyActionPayloadDto
        {
            PropertyName = propertyName,
            ExpectedValue = expectedValue
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildSetGamePropertyPayloadElement(string propertyName, string propertyValue)
    {
        return new ProjectSetGamePropertyActionPayloadDto
        {
            PropertyName = propertyName,
            PropertyValue = propertyValue
        };
    }

    private static ProjectCommandActionPayloadBaseDto? BuildInvokeProcedurePayloadElement(Guid? procedureId)
    {
        if (!procedureId.HasValue || procedureId.Value == Guid.Empty)
        {
            return null;
        }

        return new ProjectInvokeProcedureActionPayloadDto
        {
            ProcedureId = procedureId.Value
        };
    }

    private static ProjectCommandActionPayloadBaseDto? BuildMaterializeObjectCopyPayloadElement(Guid? sourceObjectId)
    {
        if (!sourceObjectId.HasValue || sourceObjectId.Value == Guid.Empty)
        {
            return null;
        }

        return new ProjectMaterializeObjectCopyActionPayloadDto
        {
            MaterializeSourceObjectId = sourceObjectId.Value
        };
    }

    private static ProjectCommandActionPayloadBaseDto? BuildStartTimerPayloadElement(string timerKey, TimerOwnerType? ownerScopeKindOverride)
    {
        var normalizedTimerKey = timerKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTimerKey))
        {
            return null;
        }

        return new ProjectStartTimerActionPayloadDto
        {
            TimerKey = normalizedTimerKey,
            OwnerScopeKindOverride = ownerScopeKindOverride
        };
    }

    private static ProjectCommandActionPayloadBaseDto? BuildCancelTimerPayloadElement(string timerKey, TimerOwnerType? scopeQualifierKind, Guid? scopeQualifierId)
    {
        var normalizedTimerKey = timerKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTimerKey))
        {
            return null;
        }

        return new ProjectCancelTimerActionPayloadDto
        {
            TimerKey = normalizedTimerKey,
            ScopeQualifierKind = scopeQualifierKind,
            ScopeQualifierId = scopeQualifierId
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildSetActiveRoomObjectPayloadElement(string selectionCueEffectKey)
    {
        return new ProjectSetActiveRoomObjectActionPayloadDto
        {
            SelectionCueEffectKey = selectionCueEffectKey ?? string.Empty
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildSelectRoomObjectByPointPayloadElement(string selectionCueEffectKey)
    {
        return new ProjectSelectRoomObjectByPointActionPayloadDto
        {
            SelectionCueEffectKey = selectionCueEffectKey ?? string.Empty
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildClearActiveRoomObjectsPayloadElement(RuntimeClearActiveRoomObjectsScope clearScope)
    {
        return new ProjectClearActiveRoomObjectsActionPayloadDto
        {
            ClearScope = clearScope.ToString()
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildClearRoomObjectSelectionsPayloadElement(ClearRoomObjectSelectionsScope clearScope)
    {
        return new ProjectClearRoomObjectSelectionsActionPayloadDto
        {
            ClearScope = clearScope
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildPutObjectInContainerPayloadElement(string targetContainerId)
    {
        return new ProjectPutObjectInContainerActionPayloadDto
        {
            TargetContainerId = targetContainerId ?? string.Empty
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildRemoveObjectFromContainerPayloadElement(string targetContainerId)
    {
        return new ProjectRemoveObjectFromContainerActionPayloadDto
        {
            TargetContainerId = targetContainerId ?? string.Empty
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildNavigateDirectionPayloadElement()
    {
        return new ProjectNavigateDirectionActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildNavigateToAdjacentPayloadElement()
    {
        return new ProjectNavigateToAdjacentActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildEchoMessagePayloadElement()
    {
        return new ProjectEchoMessageActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildOpenObjectPayloadElement()
    {
        return new ProjectOpenObjectActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildCloseObjectPayloadElement()
    {
        return new ProjectCloseObjectActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildUnlockObjectPayloadElement()
    {
        return new ProjectUnlockObjectActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildLockObjectPayloadElement()
    {
        return new ProjectLockObjectActionPayloadDto();
    }

    private static ProjectCommandActionPayloadBaseDto BuildMoveRoomObjectOnGridPayloadElement(MoveRoomObjectOnGridPayload movePayload)
    {
        return new ProjectMoveRoomObjectOnGridActionPayloadDto
        {
            DirectionToken = movePayload.DirectionToken ?? string.Empty,
            DistanceInCells = movePayload.DistanceInCells,
            AllowPartialMove = movePayload.AllowPartialMove,
            AllowJumpOver = movePayload.AllowJumpOver,
            VisualTransitionHint = movePayload.VisualTransitionHint.ToString(),
            TravelVisualizationMode = movePayload.TravelVisualizationMode.ToString()
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildMoveRoomObjectByPointsPayloadElement(MoveRoomObjectByPointsPayload movePayload)
    {
        return new ProjectMoveRoomObjectByPointsActionPayloadDto
        {
            TargetResolutionIntent = movePayload.TargetResolutionIntent ?? string.Empty,
            AllowPartialMove = movePayload.AllowPartialMove,
            AllowJumpOver = movePayload.AllowJumpOver,
            VisualTransitionHint = movePayload.VisualTransitionHint.ToString(),
            TravelVisualizationMode = movePayload.TravelVisualizationMode.ToString()
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildRotateRoomObjectOnGridPayloadElement(RotateRoomObjectOnGridPayload rotatePayload)
    {
        return new ProjectRotateRoomObjectOnGridActionPayloadDto
        {
            Mode = rotatePayload.Mode.ToString(),
            TurnDegrees = rotatePayload.TurnDegrees ?? 90,
            FacingDirectionToken = rotatePayload.FacingDirectionToken ?? string.Empty,
            VisualTransitionHint = rotatePayload.VisualTransitionHint.ToString()
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildStackRoomObjectOnAnotherPayloadElement(StackRoomObjectOnAnotherPayload stackPayload)
    {
        return new ProjectStackRoomObjectOnAnotherActionPayloadDto
        {
            VisualTransitionHint = stackPayload.VisualTransitionHint.ToString()
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildBuildCompositeByTargetPayloadElement(CompositeByTargetPayload payload)
    {
        return new ProjectBuildCompositeByTargetActionPayloadDto
        {
            CompositeTargetObjectId = payload.CompositeTargetObjectId ?? Guid.Empty,
            CompositeRecipeId = payload.CompositeRecipeId ?? Guid.Empty,
            CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds?.ToList() ?? new List<Guid>(),
            CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement.GetValueOrDefault(false),
            CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount.GetValueOrDefault(0),
            CompositePartConsumptionMode = string.IsNullOrWhiteSpace(payload.CompositePartConsumptionMode)
                ? "ContainedInComposite"
                : payload.CompositePartConsumptionMode
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildBuildCompositeByPartsPayloadElement(CompositeByPartsPayload payload)
    {
        return new ProjectBuildCompositeByPartsActionPayloadDto
        {
            CompositeTargetObjectId = payload.CompositeTargetObjectId ?? Guid.Empty,
            CompositeRecipeId = payload.CompositeRecipeId ?? Guid.Empty,
            CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds?.ToList() ?? new List<Guid>(),
            CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement.GetValueOrDefault(false),
            CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount.GetValueOrDefault(0),
            CompositeMatchMode = string.IsNullOrWhiteSpace(payload.CompositeMatchMode)
                ? "AllRequired"
                : payload.CompositeMatchMode,
            CompositeAmbiguityPolicy = string.IsNullOrWhiteSpace(payload.CompositeAmbiguityPolicy)
                ? "FailWithHint"
                : payload.CompositeAmbiguityPolicy,
            CompositePartConsumptionMode = string.IsNullOrWhiteSpace(payload.CompositePartConsumptionMode)
                ? "ContainedInComposite"
                : payload.CompositePartConsumptionMode
        };
    }

    private static ProjectCommandActionPayloadBaseDto BuildBreakCompositeItemPayloadElement(BreakCompositePayload payload)
    {
        return new ProjectBreakCompositeItemActionPayloadDto
        {
            CompositeTargetObjectId = payload.CompositeTargetObjectId ?? Guid.Empty,
            CompositePartConsumptionMode = string.IsNullOrWhiteSpace(payload.CompositePartConsumptionMode)
                ? "ContainedInComposite"
                : payload.CompositePartConsumptionMode
        };
    }

    private static RuntimeCommandActionPayloadBaseDto? BuildCleanLinkedActionsPayloadElement(
        CommandAction rootAction,
        IReadOnlyDictionary<Guid, CommandAction> actionLookup)
    {
        var nodeDtos = BuildProjectLinkedFlowNodes(rootAction, actionLookup);
        if (nodeDtos.Count == 0)
        {
            return null;
        }

        return new RuntimeLinkedFlowActionPayloadDto
        {
            LinkedActions = nodeDtos.Select(static link => new RuntimeCommandActionReferenceDto
            {
                NodeId = link.NodeId,
                ActionId = link.ActionId,
                OnAlwaysNodeIds = link.OnAlwaysNodeIds.ToList(),
                OnSuccessNodeIds = link.OnSuccessNodeIds.ToList(),
                OnFailureNodeIds = link.OnFailureNodeIds.ToList()
            }).ToList()
        };
    }

    private static RuntimeCommandActionPayloadBaseDto? BuildCleanSynonymPayloadElement(Guid? targetActionId)
    {
        if (!targetActionId.HasValue || targetActionId.Value == Guid.Empty)
        {
            return null;
        }

        return new RuntimeSynonymActionPayloadDto
        {
            TargetActionId = targetActionId.Value
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanSetFlagPayloadElement(string flagName, bool flagValue)
    {
        return new RuntimeSetFlagActionPayloadDto
        {
            FlagName = flagName,
            FlagValue = flagValue
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanCheckGamePropertyPayloadElement(string propertyName, bool expectedValue)
    {
        return new RuntimeCheckGamePropertyActionPayloadDto
        {
            PropertyName = propertyName,
            ExpectedValue = expectedValue
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanSetGamePropertyPayloadElement(string propertyName, string propertyValue)
    {
        return new RuntimeSetGamePropertyActionPayloadDto
        {
            PropertyName = propertyName,
            PropertyValue = propertyValue
        };
    }

    private static RuntimeCommandActionPayloadBaseDto? BuildCleanInvokeProcedurePayloadElement(Guid? procedureId)
    {
        if (!procedureId.HasValue || procedureId.Value == Guid.Empty)
        {
            return null;
        }

        return new RuntimeInvokeProcedureActionPayloadDto
        {
            ProcedureId = procedureId.Value
        };
    }

    private static RuntimeCommandActionPayloadBaseDto? BuildCleanMaterializeObjectCopyPayloadElement(Guid? sourceObjectId)
    {
        if (!sourceObjectId.HasValue || sourceObjectId.Value == Guid.Empty)
        {
            return null;
        }

        return new RuntimeMaterializeObjectCopyActionPayloadDto
        {
            MaterializeSourceObjectId = sourceObjectId.Value
        };
    }

    private static RuntimeCommandActionPayloadBaseDto? BuildCleanStartTimerPayloadElement(string timerKey, TimerOwnerType? ownerScopeKindOverride)
    {
        var normalizedTimerKey = timerKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTimerKey))
        {
            return null;
        }

        return new RuntimeStartTimerActionPayloadDto
        {
            TimerKey = normalizedTimerKey,
            OwnerScopeKindOverride = ownerScopeKindOverride
        };
    }

    private static RuntimeCommandActionPayloadBaseDto? BuildCleanCancelTimerPayloadElement(string timerKey, TimerOwnerType? scopeQualifierKind, Guid? scopeQualifierId)
    {
        var normalizedTimerKey = timerKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTimerKey))
        {
            return null;
        }

        return new RuntimeCancelTimerActionPayloadDto
        {
            TimerKey = normalizedTimerKey,
            ScopeQualifierKind = scopeQualifierKind,
            ScopeQualifierId = scopeQualifierId
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanSetActiveRoomObjectPayloadElement(string selectionCueEffectKey)
    {
        return new RuntimeSetActiveRoomObjectActionPayloadDto
        {
            SelectionCueEffectKey = selectionCueEffectKey ?? string.Empty
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanSelectRoomObjectByPointPayloadElement(string selectionCueEffectKey)
    {
        return new RuntimeSelectRoomObjectByPointActionPayloadDto
        {
            SelectionCueEffectKey = selectionCueEffectKey ?? string.Empty
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanClearActiveRoomObjectsPayloadElement(RuntimeClearActiveRoomObjectsScope clearScope)
    {
        return new RuntimeClearActiveRoomObjectsActionPayloadDto
        {
            ClearScope = clearScope.ToString()
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanClearRoomObjectSelectionsPayloadElement(ClearRoomObjectSelectionsScope clearScope)
    {
        return new RuntimeClearRoomObjectSelectionsActionPayloadDto
        {
            ClearScope = clearScope
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanPutObjectInContainerPayloadElement(string targetContainerId)
    {
        return new RuntimePutObjectInContainerActionPayloadDto
        {
            TargetContainerId = targetContainerId ?? string.Empty
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanRemoveObjectFromContainerPayloadElement(string targetContainerId)
    {
        return new RuntimeRemoveObjectFromContainerActionPayloadDto
        {
            TargetContainerId = targetContainerId ?? string.Empty
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanNavigateDirectionPayloadElement()
    {
        return new RuntimeNavigateDirectionActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanNavigateToAdjacentPayloadElement()
    {
        return new RuntimeNavigateToAdjacentActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanEchoMessagePayloadElement()
    {
        return new RuntimeEchoMessageActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanOpenObjectPayloadElement()
    {
        return new RuntimeOpenObjectActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanCloseObjectPayloadElement()
    {
        return new RuntimeCloseObjectActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanUnlockObjectPayloadElement()
    {
        return new RuntimeUnlockObjectActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanLockObjectPayloadElement()
    {
        return new RuntimeLockObjectActionPayloadDto();
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanMoveRoomObjectOnGridPayloadElement(MoveRoomObjectOnGridPayload movePayload)
    {
        return new RuntimeMoveRoomObjectOnGridActionPayloadDto
        {
            DirectionToken = movePayload.DirectionToken ?? string.Empty,
            DistanceInCells = movePayload.DistanceInCells,
            AllowPartialMove = movePayload.AllowPartialMove,
            AllowJumpOver = movePayload.AllowJumpOver,
            VisualTransitionHint = movePayload.VisualTransitionHint.ToString(),
            TravelVisualizationMode = movePayload.TravelVisualizationMode.ToString()
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanMoveRoomObjectByPointsPayloadElement(MoveRoomObjectByPointsPayload movePayload)
    {
        return new RuntimeMoveRoomObjectByPointsActionPayloadDto
        {
            TargetResolutionIntent = movePayload.TargetResolutionIntent ?? string.Empty,
            AllowPartialMove = movePayload.AllowPartialMove,
            AllowJumpOver = movePayload.AllowJumpOver,
            VisualTransitionHint = movePayload.VisualTransitionHint.ToString(),
            TravelVisualizationMode = movePayload.TravelVisualizationMode.ToString()
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanRotateRoomObjectOnGridPayloadElement(RotateRoomObjectOnGridPayload rotatePayload)
    {
        return new RuntimeRotateRoomObjectOnGridActionPayloadDto
        {
            Mode = rotatePayload.Mode.ToString(),
            TurnDegrees = rotatePayload.TurnDegrees ?? 90,
            FacingDirectionToken = rotatePayload.FacingDirectionToken ?? string.Empty,
            VisualTransitionHint = rotatePayload.VisualTransitionHint.ToString()
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanStackRoomObjectOnAnotherPayloadElement(StackRoomObjectOnAnotherPayload stackPayload)
    {
        return new RuntimeStackRoomObjectOnAnotherActionPayloadDto
        {
            VisualTransitionHint = stackPayload.VisualTransitionHint.ToString()
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanBuildCompositeByTargetPayloadElement(CompositeByTargetPayload payload)
    {
        return new RuntimeBuildCompositeByTargetActionPayloadDto
        {
            CompositeTargetObjectId = payload.CompositeTargetObjectId ?? Guid.Empty,
            CompositeRecipeId = payload.CompositeRecipeId ?? Guid.Empty,
            CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds?.ToList() ?? new List<Guid>(),
            CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement.GetValueOrDefault(false),
            CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount.GetValueOrDefault(0),
            CompositePartConsumptionMode = string.IsNullOrWhiteSpace(payload.CompositePartConsumptionMode)
                ? "ContainedInComposite"
                : payload.CompositePartConsumptionMode
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanBuildCompositeByPartsPayloadElement(CompositeByPartsPayload payload)
    {
        return new RuntimeBuildCompositeByPartsActionPayloadDto
        {
            CompositeTargetObjectId = payload.CompositeTargetObjectId ?? Guid.Empty,
            CompositeRecipeId = payload.CompositeRecipeId ?? Guid.Empty,
            CompositeRequiredPartObjectIds = payload.CompositeRequiredPartObjectIds?.ToList() ?? new List<Guid>(),
            CompositeStrictPartCountEnforcement = payload.CompositeStrictPartCountEnforcement.GetValueOrDefault(false),
            CompositeMinimumRequiredPartCount = payload.CompositeMinimumRequiredPartCount.GetValueOrDefault(0),
            CompositeMatchMode = string.IsNullOrWhiteSpace(payload.CompositeMatchMode)
                ? "AllRequired"
                : payload.CompositeMatchMode,
            CompositeAmbiguityPolicy = string.IsNullOrWhiteSpace(payload.CompositeAmbiguityPolicy)
                ? "FailWithHint"
                : payload.CompositeAmbiguityPolicy,
            CompositePartConsumptionMode = string.IsNullOrWhiteSpace(payload.CompositePartConsumptionMode)
                ? "ContainedInComposite"
                : payload.CompositePartConsumptionMode
        };
    }

    private static RuntimeCommandActionPayloadBaseDto BuildCleanBreakCompositeItemPayloadElement(BreakCompositePayload payload)
    {
        return new RuntimeBreakCompositeItemActionPayloadDto
        {
            CompositeTargetObjectId = payload.CompositeTargetObjectId ?? Guid.Empty,
            CompositePartConsumptionMode = string.IsNullOrWhiteSpace(payload.CompositePartConsumptionMode)
                ? "ContainedInComposite"
                : payload.CompositePartConsumptionMode
        };
    }

    private static string? TryGetMoveDirectionTokenFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectOnGridActionPayloadDto movePayload)
        {
            return null;
        }

        return movePayload.DirectionToken;
    }

    private static int? TryGetMoveDistanceInCellsFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectOnGridActionPayloadDto movePayload)
        {
            return null;
        }

        return movePayload.DistanceInCells;
    }

    private static bool? TryGetMoveAllowPartialMoveFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectOnGridActionPayloadDto movePayload)
        {
            return null;
        }

        return movePayload.AllowPartialMove;
    }

    private static bool? TryGetMoveAllowJumpOverFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectOnGridActionPayloadDto movePayload)
        {
            return null;
        }

        return movePayload.AllowJumpOver;
    }

    private static RuntimeMovementVisualTransitionHint? TryGetMoveVisualTransitionHintFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectOnGridActionPayloadDto movePayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeMovementVisualTransitionHint>(movePayload.VisualTransitionHint, true, out var parsedHint)
            ? parsedHint
            : null;
    }

    private static RuntimeMovementTravelVisualizationMode? TryGetMoveTravelVisualizationModeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectOnGridActionPayloadDto movePayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeMovementTravelVisualizationMode>(movePayload.TravelVisualizationMode, true, out var parsedMode)
            ? parsedMode
            : null;
    }

    private static string? TryGetMoveByPointsTargetResolutionIntentFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectByPointsActionPayloadDto moveByPointsPayload)
        {
            return null;
        }

        return moveByPointsPayload.TargetResolutionIntent;
    }

    private static bool? TryGetMoveByPointsAllowPartialMoveFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectByPointsActionPayloadDto moveByPointsPayload)
        {
            return null;
        }

        return moveByPointsPayload.AllowPartialMove;
    }

    private static bool? TryGetMoveByPointsAllowJumpOverFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectByPointsActionPayloadDto moveByPointsPayload)
        {
            return null;
        }

        return moveByPointsPayload.AllowJumpOver;
    }

    private static RuntimeMovementVisualTransitionHint? TryGetMoveByPointsVisualTransitionHintFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectByPointsActionPayloadDto moveByPointsPayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeMovementVisualTransitionHint>(moveByPointsPayload.VisualTransitionHint, true, out var parsedHint)
            ? parsedHint
            : null;
    }

    private static RuntimeMovementTravelVisualizationMode? TryGetMoveByPointsTravelVisualizationModeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMoveRoomObjectByPointsActionPayloadDto moveByPointsPayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeMovementTravelVisualizationMode>(moveByPointsPayload.TravelVisualizationMode, true, out var parsedMode)
            ? parsedMode
            : null;
    }

    private static string? TryGetPutObjectInContainerTargetContainerIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectPutObjectInContainerActionPayloadDto putObjectInContainerPayload)
        {
            return null;
        }

        return putObjectInContainerPayload.TargetContainerId;
    }

    private static string? TryGetRemoveObjectFromContainerTargetContainerIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectRemoveObjectFromContainerActionPayloadDto removeObjectFromContainerPayload)
        {
            return null;
        }

        return removeObjectFromContainerPayload.TargetContainerId;
    }

    private static RuntimeRotateRoomObjectOnGridAttemptMode? TryGetRotateModeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectRotateRoomObjectOnGridActionPayloadDto rotatePayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeRotateRoomObjectOnGridAttemptMode>(rotatePayload.Mode, true, out var parsedMode)
            ? parsedMode
            : null;
    }

    private static int? TryGetRotateTurnDegreesFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectRotateRoomObjectOnGridActionPayloadDto rotatePayload)
        {
            return null;
        }

        return rotatePayload.TurnDegrees > 0
            ? rotatePayload.TurnDegrees
            : null;
    }

    private static string? TryGetRotateFacingDirectionTokenFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectRotateRoomObjectOnGridActionPayloadDto rotatePayload)
        {
            return null;
        }

        return rotatePayload.FacingDirectionToken;
    }

    private static RuntimeMovementVisualTransitionHint? TryGetRotateVisualTransitionHintFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectRotateRoomObjectOnGridActionPayloadDto rotatePayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeMovementVisualTransitionHint>(rotatePayload.VisualTransitionHint, true, out var parsedHint)
            ? parsedHint
            : null;
    }

    private static RuntimeMovementVisualTransitionHint? TryGetStackVisualTransitionHintFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectStackRoomObjectOnAnotherActionPayloadDto stackPayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeMovementVisualTransitionHint>(stackPayload.VisualTransitionHint, true, out var parsedHint)
            ? parsedHint
            : null;
    }

    private static Guid? TryGetCompositeTargetObjectIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        return payload switch
        {
            ProjectBuildCompositeByTargetActionPayloadDto buildByTarget when buildByTarget.CompositeTargetObjectId != Guid.Empty => buildByTarget.CompositeTargetObjectId,
            ProjectBuildCompositeByPartsActionPayloadDto buildByParts when buildByParts.CompositeTargetObjectId != Guid.Empty => buildByParts.CompositeTargetObjectId,
            ProjectBreakCompositeItemActionPayloadDto breakComposite when breakComposite.CompositeTargetObjectId != Guid.Empty => breakComposite.CompositeTargetObjectId,
            _ => null
        };
    }

    private static Guid? TryGetCompositeRecipeIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        return payload switch
        {
            ProjectBuildCompositeByTargetActionPayloadDto buildByTarget when buildByTarget.CompositeRecipeId != Guid.Empty => buildByTarget.CompositeRecipeId,
            ProjectBuildCompositeByPartsActionPayloadDto buildByParts when buildByParts.CompositeRecipeId != Guid.Empty => buildByParts.CompositeRecipeId,
            _ => null
        };
    }

    private static List<Guid>? TryGetCompositeRequiredPartObjectIdsFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        return payload switch
        {
            ProjectBuildCompositeByTargetActionPayloadDto buildByTarget => buildByTarget.CompositeRequiredPartObjectIds.ToList(),
            ProjectBuildCompositeByPartsActionPayloadDto buildByParts => buildByParts.CompositeRequiredPartObjectIds.ToList(),
            _ => null
        };
    }

    private static bool? TryGetCompositeStrictPartCountEnforcementFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        return payload switch
        {
            ProjectBuildCompositeByTargetActionPayloadDto buildByTarget => buildByTarget.CompositeStrictPartCountEnforcement,
            ProjectBuildCompositeByPartsActionPayloadDto buildByParts => buildByParts.CompositeStrictPartCountEnforcement,
            _ => null
        };
    }

    private static int? TryGetCompositeMinimumRequiredPartCountFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        return payload switch
        {
            ProjectBuildCompositeByTargetActionPayloadDto buildByTarget => buildByTarget.CompositeMinimumRequiredPartCount,
            ProjectBuildCompositeByPartsActionPayloadDto buildByParts => buildByParts.CompositeMinimumRequiredPartCount,
            _ => null
        };
    }

    private static string? TryGetCompositeMatchModeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectBuildCompositeByPartsActionPayloadDto buildByParts)
        {
            return null;
        }

        return buildByParts.CompositeMatchMode;
    }

    private static string? TryGetCompositeAmbiguityPolicyFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectBuildCompositeByPartsActionPayloadDto buildByParts)
        {
            return null;
        }

        return buildByParts.CompositeAmbiguityPolicy;
    }

    private static string? TryGetCompositePartConsumptionModeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        return payload switch
        {
            ProjectBuildCompositeByTargetActionPayloadDto buildByTarget => buildByTarget.CompositePartConsumptionMode,
            ProjectBuildCompositeByPartsActionPayloadDto buildByParts => buildByParts.CompositePartConsumptionMode,
            ProjectBreakCompositeItemActionPayloadDto breakComposite => breakComposite.CompositePartConsumptionMode,
            _ => null
        };
    }

    private static string? TryGetSetActiveSelectionCueEffectKeyFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectSetActiveRoomObjectActionPayloadDto setActivePayload)
        {
            return null;
        }

        return setActivePayload.SelectionCueEffectKey;
    }

    private static string? TryGetSelectByPointSelectionCueEffectKeyFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectSelectRoomObjectByPointActionPayloadDto selectByPointPayload)
        {
            return null;
        }

        return selectByPointPayload.SelectionCueEffectKey;
    }

    private static RuntimeClearActiveRoomObjectsScope? TryGetClearActiveRoomObjectsScopeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectClearActiveRoomObjectsActionPayloadDto clearActivePayload)
        {
            return null;
        }

        return Enum.TryParse<RuntimeClearActiveRoomObjectsScope>(clearActivePayload.ClearScope, true, out var parsedScope)
            ? parsedScope
            : null;
    }

    private static ClearRoomObjectSelectionsScope? TryGetClearRoomObjectSelectionsScopeFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectClearRoomObjectSelectionsActionPayloadDto clearPayload)
        {
            return null;
        }

        return clearPayload.ClearScope;
    }

    private static Guid? TryGetMaterializeSourceObjectIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectMaterializeObjectCopyActionPayloadDto materializePayload)
        {
            return null;
        }

        return materializePayload.MaterializeSourceObjectId == Guid.Empty
            ? null
            : materializePayload.MaterializeSourceObjectId;
    }

    private static Guid? TryGetInvokeProcedureIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectInvokeProcedureActionPayloadDto invokeProcedurePayload)
        {
            return null;
        }

        return invokeProcedurePayload.ProcedureId == Guid.Empty
            ? null
            : invokeProcedurePayload.ProcedureId;
    }

    private static string? TryGetStartTimerKeyFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectStartTimerActionPayloadDto startTimerPayload)
        {
            return null;
        }

        return startTimerPayload.TimerKey;
    }

    private static TimerOwnerType? TryGetStartTimerOwnerScopeKindOverrideFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectStartTimerActionPayloadDto startTimerPayload)
        {
            return null;
        }

        return startTimerPayload.OwnerScopeKindOverride;
    }

    private static string? TryGetCancelTimerKeyFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectCancelTimerActionPayloadDto cancelTimerPayload)
        {
            return null;
        }

        return cancelTimerPayload.TimerKey;
    }

    private static TimerOwnerType? TryGetCancelTimerScopeQualifierKindFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectCancelTimerActionPayloadDto cancelTimerPayload)
        {
            return null;
        }

        return cancelTimerPayload.ScopeQualifierKind;
    }

    private static Guid? TryGetCancelTimerScopeQualifierIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectCancelTimerActionPayloadDto cancelTimerPayload)
        {
            return null;
        }

        return cancelTimerPayload.ScopeQualifierId;
    }

    private static string? TryGetSetGamePropertyNameFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectSetGamePropertyActionPayloadDto setGamePropertyPayload)
        {
            return null;
        }

        return setGamePropertyPayload.PropertyName;
    }

    private static string? TryGetSetGamePropertyValueFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectSetGamePropertyActionPayloadDto setGamePropertyPayload)
        {
            return null;
        }

        return setGamePropertyPayload.PropertyValue;
    }

    private static string? TryGetCheckGamePropertyNameFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectCheckGamePropertyActionPayloadDto checkGamePropertyPayload)
        {
            return null;
        }

        return checkGamePropertyPayload.PropertyName;
    }

    private static bool? TryGetCheckGamePropertyExpectedValueFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectCheckGamePropertyActionPayloadDto checkGamePropertyPayload)
        {
            return null;
        }

        return checkGamePropertyPayload.ExpectedValue;
    }

    private static string? TryGetSetFlagNameFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectSetFlagActionPayloadDto setFlagPayload)
        {
            return null;
        }

        return setFlagPayload.FlagName;
    }

    private static bool? TryGetSetFlagValueFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectSetFlagActionPayloadDto setFlagPayload)
        {
            return null;
        }

        return setFlagPayload.FlagValue;
    }

    private static Guid? TryGetSynonymTargetActionIdFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is ProjectSynonymActionPayloadDto synonym
            && synonym.TargetActionId != Guid.Empty)
        {
            return synonym.TargetActionId;
        }

        return null;
    }

    private static List<ProjectCommandActionReferenceDto>? TryGetLinkedActionsFromPayload(ProjectCommandActionPayloadBaseDto? payload)
    {
        if (payload is not ProjectLinkedFlowActionPayloadDto linkedFlowPayload)
        {
            return null;
        }

        return linkedFlowPayload.LinkedActions
            .Where(static link => link.ActionId != Guid.Empty && link.NodeId > 0)
            .Select(static link => new ProjectCommandActionReferenceDto
            {
                NodeId = link.NodeId,
                ActionId = link.ActionId,
                OnAlwaysNodeIds = (link.OnAlwaysNodeIds ?? new List<int>())
                    .Where(static id => id > 0)
                    .Distinct()
                    .ToList(),
                OnSuccessNodeIds = (link.OnSuccessNodeIds ?? new List<int>())
                    .Where(static id => id > 0)
                    .Distinct()
                    .ToList(),
                OnFailureNodeIds = (link.OnFailureNodeIds ?? new List<int>())
                    .Where(static id => id > 0)
                    .Distinct()
                    .ToList()
            })
            .ToList();
    }

    private static List<LinkedActionReference> ResolveLinkedActionReferences(CommandActionDto action)
    {
        var nodeDtos = TryGetLinkedActionsFromPayload(action.Payload);
        if (nodeDtos is not { Count: > 0 })
        {
            return new List<LinkedActionReference>();
        }

        var entryNode = nodeDtos[0];
        if (entryNode.ActionId == Guid.Empty)
        {
            return new List<LinkedActionReference>();
        }

        return new List<LinkedActionReference>
        {
            new()
            {
                ActionId = entryNode.ActionId,
                RunWhen = LinkedActionRunWhen.Always,
                Order = 0
            }
        };
    }

    private static SharedVariableDefinition ToSharedVariableModel(SharedVariableDefinitionDto dto)
    {
        return new SharedVariableDefinition
        {
            Id = EnsureScopeId(dto.Id, $"shared variable '{dto.Name ?? dto.Id.ToString("N")}'"),
            Name = dto.Name ?? string.Empty,
            DefaultValue = dto.DefaultValue ?? string.Empty,
            ValueRestriction = Enum.TryParse<GamePropertyValueRestriction>(dto.ValueRestriction, out var parsedRestriction)
                ? parsedRestriction
                : GamePropertyValueRestriction.Unrestricted,
            Participants = (dto.Participants ?? []).Select(ToSharedVariableParticipantModel).ToList()
        };
    }

    private static SharedVariableParticipant ToSharedVariableParticipantModel(SharedVariableParticipantDto dto)
    {
        return new SharedVariableParticipant
        {
            Kind = dto.Kind,
            OwnerId = dto.OwnerId,
            VariableName = dto.VariableName,
            Leg = dto.Leg
        };
    }

    private T? TryLoadSidecar<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = File.ReadAllText(filePath);
        return DeserializeWithFileContext<T>(filePath, json, _jsonReadOptions);
    }

    private List<RoomDto> LoadRoomSidecars(string projectFilePath)
    {
        var roomDtos = new List<RoomDto>();
        var seenRoomIds = new HashSet<Guid>();
        var roomFolderPaths = new[] { BuildRoomsFolderPath(projectFilePath) };

        foreach (var roomsFolderPath in roomFolderPaths)
        {
            if (!Directory.Exists(roomsFolderPath))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(roomsFolderPath, $"*{RoomFileSuffix}").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var json = File.ReadAllText(filePath);
                var dto = DeserializeWithFileContext<RoomDto>(filePath, json, _jsonReadOptions);
                if (dto is null)
                {
                    continue;
                }

                if (dto.Id != Guid.Empty && !seenRoomIds.Add(dto.Id))
                {
                    continue;
                }

                roomDtos.Add(dto);
            }
        }

        return roomDtos;
    }

    private List<RoomDto> LoadRoomTemplateSidecars(string projectFilePath)
    {
        var roomTemplateDtos = new List<RoomDto>();
        var roomTemplatesFolderPath = BuildRoomTemplatesFolderPath(projectFilePath);
        if (!Directory.Exists(roomTemplatesFolderPath))
        {
            return roomTemplateDtos;
        }

        var seenTemplateIds = new HashSet<Guid>();
        foreach (var filePath in Directory.EnumerateFiles(roomTemplatesFolderPath, $"*{RoomTemplateFileSuffix}")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var json = File.ReadAllText(filePath);
            var dto = DeserializeWithFileContext<RoomDto>(filePath, json, _jsonReadOptions);
            if (dto is null)
            {
                continue;
            }

            if (dto.Id != Guid.Empty && !seenTemplateIds.Add(dto.Id))
            {
                continue;
            }

            roomTemplateDtos.Add(dto);
        }

        return roomTemplateDtos;
    }

    private List<ProjectGameObjectDto> LoadAuthoringGameObjectDtos(string projectFilePath)
    {
        var objectDtos = new List<ProjectGameObjectDto>();
        var objectFolderPaths = new[]
        {
            BuildAuthoringTemplatesFolderPath(projectFilePath),
            BuildAuthoringGameObjectFolderPath(projectFilePath)
        };

        var seenObjectIds = new HashSet<Guid>();
        foreach (var objectFolderPath in objectFolderPaths)
        {
            if (!Directory.Exists(objectFolderPath))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(objectFolderPath, $"*{AuthoringGameObjectFileSuffix}")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var json = File.ReadAllText(filePath);
                var dto = DeserializeWithFileContext<ProjectGameObjectDto>(filePath, json, _jsonReadOptions);
                if (dto is null || dto.Id == Guid.Empty)
                {
                    continue;
                }

                if (!seenObjectIds.Add(dto.Id))
                {
                    continue;
                }

                objectDtos.Add(dto);
            }
        }

        return objectDtos;
    }

    private List<PhaseNodeDto> LoadPhaseSidecars(string projectFilePath)
    {
        var phaseDtos = new List<PhaseNodeDto>();
        var phaseFolderPaths = new[]
        {
            BuildPhasesFolderPath(projectFilePath),
            BuildLegacyPhasesFolderPath(projectFilePath)
        };

        var seenPhaseIds = new HashSet<Guid>();
        foreach (var phaseFolderPath in phaseFolderPaths)
        {
            if (!Directory.Exists(phaseFolderPath))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(phaseFolderPath, $"*{PhaseFileSuffix}")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var json = File.ReadAllText(filePath);
                var dto = DeserializeWithFileContext<PhaseNodeDto>(filePath, json, _jsonReadOptions);
                if (dto is null || dto.Id == Guid.Empty)
                {
                    continue;
                }

                if (!seenPhaseIds.Add(dto.Id))
                {
                    continue;
                }

                phaseDtos.Add(dto);
            }
        }

        return phaseDtos;
    }

    private static T? DeserializeWithFileContext<T>(string filePath, string json, JsonSerializerOptions options)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (Exception ex)
        {
            throw BuildDeserializationException(filePath, typeof(T), ex);
        }
    }

    private static InvalidOperationException BuildDeserializationException(string filePath, Type targetType, Exception ex)
    {
        if (ex is JsonException jsonEx)
        {
            var jsonPath = string.IsNullOrWhiteSpace(jsonEx.Path) ? "$" : jsonEx.Path;
            var line = jsonEx.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "?";
            var bytePos = jsonEx.BytePositionInLine?.ToString(CultureInfo.InvariantCulture) ?? "?";
            return new InvalidOperationException(
                $"Failed to deserialize '{filePath}' as {targetType.Name}. JSON path={jsonPath}; line={line}; byte={bytePos}. {jsonEx.Message}",
                ex);
        }

        return new InvalidOperationException(
            $"Failed to deserialize '{filePath}' as {targetType.Name}. {ex.Message}",
            ex);
    }

    private static List<ProjectGameObjectDto> ResolveScopedObjectDtos(
        List<Guid>? objectIds,
        List<ProjectGameObjectDto>? embeddedObjects,
        IReadOnlyDictionary<Guid, ProjectGameObjectDto> objectDtosById)
    {
        var resolved = new List<ProjectGameObjectDto>();
        var seenIds = new HashSet<Guid>();

        if (objectIds is { Count: > 0 })
        {
            foreach (var id in objectIds.Where(static id => id != Guid.Empty))
            {
                if (!seenIds.Add(id))
                {
                    continue;
                }

                if (objectDtosById.TryGetValue(id, out var objectDto))
                {
                    resolved.Add(objectDto);
                }
            }
        }

        foreach (var embedded in embeddedObjects ?? new List<ProjectGameObjectDto>())
        {
            if (embedded.Id != Guid.Empty && !seenIds.Add(embedded.Id))
            {
                continue;
            }

            resolved.Add(embedded);
        }

        return resolved;
    }

    private static ProjectGameObjectDto ApplyTemplateScopeKind(ProjectGameObjectDto template)
    {
        template.ScopeKind = ScopeNodeKind.Templates;

        foreach (var child in template.ContainedObjects ?? new List<ProjectGameObjectDto>())
        {
            ApplyTemplateScopeKind(child);
        }

        return template;
    }

    private static ProjectGlobalNodeDto ApplyGlobalScopeKind(ProjectGlobalNodeDto globalNode)
    {
        globalNode.ScopeKind = ScopeNodeKind.Global;
        return globalNode;
    }

    private static RoomDto ApplyRoomTemplateScopeKind(RoomDto roomTemplate)
    {
        roomTemplate.ScopeKind = ScopeNodeKind.RoomTemplates;

        foreach (var templateObject in roomTemplate.GameObjects)
        {
            ApplyTemplateScopeKind(templateObject);
        }

        return roomTemplate;
    }

    private static List<RoomDto> ResolveScopedRoomDtos(
        List<Guid>? roomIds,
        IReadOnlyDictionary<Guid, RoomDto> roomDtosById,
        List<RoomDto>? embeddedRooms)
    {
        var resolved = new List<RoomDto>();
        var seenIds = new HashSet<Guid>();

        if (roomIds is { Count: > 0 })
        {
            foreach (var id in roomIds.Where(static id => id != Guid.Empty))
            {
                if (!seenIds.Add(id))
                {
                    continue;
                }

                if (roomDtosById.TryGetValue(id, out var roomDto))
                {
                    resolved.Add(roomDto);
                }
            }
        }

        foreach (var embedded in embeddedRooms ?? new List<RoomDto>())
        {
            if (embedded.Id != Guid.Empty && !seenIds.Add(embedded.Id))
            {
                continue;
            }

            resolved.Add(embedded);
        }

        return resolved;
    }

    private static PhaseNodeDto ToPhaseNodeDto(PhaseNode phaseNode)
    {
        var dto = new PhaseNodeDto
        {
            Id = phaseNode.Id,
            ScopeKind = phaseNode.ScopeKind,
            PhaseTier = phaseNode.Tier,
            PhaseKey = phaseNode.PhaseKey,
            DisplayName = phaseNode.DisplayName,
            Title = string.IsNullOrWhiteSpace(phaseNode.Title) ? null : phaseNode.Title,
            TitlePresentationCueEffectKey = string.IsNullOrWhiteSpace(phaseNode.TitlePresentationCueEffectKey) ? null : phaseNode.TitlePresentationCueEffectKey,
            Prologue = string.IsNullOrWhiteSpace(phaseNode.Prologue) ? null : phaseNode.Prologue,
            ProloguePresentationCueEffectKey = string.IsNullOrWhiteSpace(phaseNode.ProloguePresentationCueEffectKey) ? null : phaseNode.ProloguePresentationCueEffectKey,
            Narrative = string.IsNullOrWhiteSpace(phaseNode.Narrative) ? null : phaseNode.Narrative,
            NarrativePresentationCueEffectKey = string.IsNullOrWhiteSpace(phaseNode.NarrativePresentationCueEffectKey) ? null : phaseNode.NarrativePresentationCueEffectKey,
            PhaseAmbientSoundEffectId = phaseNode.PhaseAmbientSoundEffectId,
            PhaseAmbientTimerKey = string.IsNullOrWhiteSpace(phaseNode.PhaseAmbientTimerKey) ? null : phaseNode.PhaseAmbientTimerKey,
            PhaseAmbienceMode = phaseNode.PhaseAmbienceMode,
            ParentPhaseId = phaseNode.ParentScope is PhaseNode parent ? parent.Id : null,
            ChildPhaseIds = phaseNode.Children
                .Select(static child => child.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            GameProperties = phaseNode.Variables.Select(ToVariableDto).ToList(),
            AvailableGameActions = ToCommandActionDtos(phaseNode.AvailableActions),
            EventSubscriptions = phaseNode.EventSubscriptions.Select(ToProjectEventSubscriptionDto).ToList(),
            TimerDefinitions = phaseNode.TimerDefinitions.Select(ToProjectTimerDefinitionDto).ToList(),
            SoundEffectLibraryEntries = phaseNode.SoundEffectLibraryEntries.Select(ToSoundEffectLibraryEntryDto).ToList(),
            AdditionalVerbs = phaseNode.AdditionalVerbs.ToList(),
            AdditionalDirectionals = phaseNode.AdditionalDirectionals.ToList(),
            AdditionalDirectionalTraversalMappings = ToDirectionalTraversalMappingDtos(phaseNode.DirectionalTraversalMappings)
        };

        dto.Phases = phaseNode.Children.Select(ToPhaseNodeDto).ToList();
        return dto;
    }

    private static PhaseNode ToPhaseNodeModel(PhaseNodeDto dto)
    {
        var phaseNode = new PhaseNode
        {
            Id = dto.Id == Guid.Empty
                ? Guid.NewGuid()
                : dto.Id,
            Tier = dto.PhaseTier,
            PhaseKey = dto.PhaseKey ?? string.Empty,
            DisplayName = dto.DisplayName ?? string.Empty,
            Title = dto.Title,
            TitlePresentationCueEffectKey = dto.TitlePresentationCueEffectKey,
            Prologue = dto.Prologue,
            ProloguePresentationCueEffectKey = dto.ProloguePresentationCueEffectKey,
            Narrative = dto.Narrative,
            NarrativePresentationCueEffectKey = dto.NarrativePresentationCueEffectKey,
            PhaseAmbientSoundEffectId = dto.PhaseAmbientSoundEffectId,
            PhaseAmbientTimerKey = dto.PhaseAmbientTimerKey,
            PhaseAmbienceMode = dto.PhaseAmbienceMode,
            Variables = (dto.GameProperties ?? new List<GamePropertyDefinitionDto>())
                .Select(ToVariableModel)
                .ToList(),
            AvailableActions = ToCommandActionModels(dto.AvailableGameActions),
            EventSubscriptions = (dto.EventSubscriptions ?? new List<ProjectEventSubscriptionDto>())
                .Select(ToEventSubscriptionModel)
                .ToList(),
            TimerDefinitions = (dto.TimerDefinitions ?? new List<ProjectTimerDefinitionDto>())
                .Select(ToTimerDefinitionModel)
                .ToList(),
            SoundEffectLibraryEntries = (dto.SoundEffectLibraryEntries ?? new List<SoundEffectLibraryEntryDto>())
                .Select(ToSoundEffectLibraryEntryModel)
                .ToList(),
            AdditionalVerbs = (dto.AdditionalVerbs ?? new List<string>()).ToList(),
            AdditionalDirectionals = (dto.AdditionalDirectionals ?? new List<string>()).ToList(),
            DirectionalTraversalMappings = ToDirectionalTraversalMappings(dto.AdditionalDirectionalTraversalMappings)
        };

        foreach (var childDto in dto.Phases ?? new List<PhaseNodeDto>())
        {
            var childNode = ToPhaseNodeModel(childDto);
            phaseNode.AddChildScope(childNode);
        }

        return phaseNode;
    }

    private static List<PhaseNodeDto> ResolveScopedPhaseDtos(
        List<Guid>? phaseBookIds,
        List<PhaseNodeDto>? embeddedPhaseBooks,
        List<PhaseNodeDto>? sidecarPhases)
    {
        var embedded = embeddedPhaseBooks ?? new List<PhaseNodeDto>();
        var byId = new Dictionary<Guid, PhaseNodeDto>();

        foreach (var phase in FlattenPhaseDtos(embedded)
                     .Where(static phase => phase.Id != Guid.Empty))
        {
            byId[phase.Id] = phase;
        }

        foreach (var phase in sidecarPhases ?? new List<PhaseNodeDto>())
        {
            if (phase.Id == Guid.Empty)
            {
                continue;
            }

            // Sidecar phase files are canonical when present.
            byId[phase.Id] = phase;
        }

        if (phaseBookIds is not { Count: > 0 })
        {
            if (embedded.Count > 0)
            {
                return embedded;
            }

            return byId.Values
                .Where(static phase => !phase.ParentPhaseId.HasValue || phase.ParentPhaseId.Value == Guid.Empty)
                .OrderBy(static phase => phase.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(root => BuildResolvedPhaseTree(root, byId, new HashSet<Guid>()))
                .ToList();
        }

        var resolved = new List<PhaseNodeDto>();
        var seenIds = new HashSet<Guid>();
        foreach (var id in phaseBookIds.Where(static id => id != Guid.Empty))
        {
            if (!seenIds.Add(id))
            {
                continue;
            }

            if (byId.TryGetValue(id, out var phaseDto))
            {
                resolved.Add(BuildResolvedPhaseTree(phaseDto, byId, new HashSet<Guid>()));
            }
        }

        return resolved.Count > 0
            ? resolved
            : embedded;
    }

    private static IEnumerable<PhaseNodeDto> FlattenPhaseDtos(IEnumerable<PhaseNodeDto> roots)
    {
        var stack = new Stack<PhaseNodeDto>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            var children = current.Phases ?? new List<PhaseNodeDto>();
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    private static IEnumerable<PhaseNode> FlattenPhaseNodes(IEnumerable<PhaseNode> roots)
    {
        var stack = new Stack<PhaseNode>((roots ?? Array.Empty<PhaseNode>()).Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static RuntimePhaseNodeDto ToCleanRuntimePhaseNodeDto(PhaseNode phaseNode)
    {
        return new RuntimePhaseNodeDto
        {
            ScopeNodeId = phaseNode.Id,
            ScopeKind = phaseNode.ScopeKind,
            Name = phaseNode.ScopeName,
            NameInGame = phaseNode.ScopeName,
            PhaseTier = phaseNode.Tier,
            PhaseKey = phaseNode.PhaseKey,
            DisplayName = phaseNode.DisplayName,
            Title = string.IsNullOrWhiteSpace(phaseNode.Title) ? null : phaseNode.Title,
            TitlePresentationCueEffectKey = string.IsNullOrWhiteSpace(phaseNode.TitlePresentationCueEffectKey) ? null : phaseNode.TitlePresentationCueEffectKey,
            Prologue = string.IsNullOrWhiteSpace(phaseNode.Prologue) ? null : phaseNode.Prologue,
            ProloguePresentationCueEffectKey = string.IsNullOrWhiteSpace(phaseNode.ProloguePresentationCueEffectKey) ? null : phaseNode.ProloguePresentationCueEffectKey,
            Narrative = string.IsNullOrWhiteSpace(phaseNode.Narrative) ? null : phaseNode.Narrative,
            NarrativePresentationCueEffectKey = string.IsNullOrWhiteSpace(phaseNode.NarrativePresentationCueEffectKey) ? null : phaseNode.NarrativePresentationCueEffectKey,
            ParentPhaseId = phaseNode.ParentScope is PhaseNode parent ? parent.Id : null,
            ChildPhaseIds = phaseNode.Children
                .Select(static child => child.Id)
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            PhaseAmbientSoundEffectId = phaseNode.PhaseAmbientSoundEffectId,
            PhaseAmbientTimerKey = string.IsNullOrWhiteSpace(phaseNode.PhaseAmbientTimerKey) ? null : phaseNode.PhaseAmbientTimerKey,
            PhaseAmbienceMode = phaseNode.PhaseAmbienceMode,
            GameProperties = NullIfEmpty(phaseNode.Variables
                .Select(ToCleanGamePropertyDto)
                .OrderBy(static variable => variable.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()),
            AdditionalVerbs = NullIfEmpty(phaseNode.AdditionalVerbs
                .OrderBy(static verb => verb, StringComparer.OrdinalIgnoreCase)
                .ToList()),
            AdditionalDirectionals = NullIfEmpty(phaseNode.AdditionalDirectionals
                .OrderBy(static directional => directional, StringComparer.OrdinalIgnoreCase)
                .ToList()),
            AdditionalDirectionalTraversalMappings = NullIfEmpty(phaseNode.DirectionalTraversalMappings
                .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection))
                .OrderBy(static mapping => mapping.Token, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static mapping => mapping.TraversalDirection)
                .ToList()),
            AvailableGameActions = NullIfEmpty(ToCleanCommandActionDtos(phaseNode.AvailableActions)
                .OrderBy(static action => action.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static action => action.Id)
                .ToList()),
            EventSubscriptions = NullIfEmpty(phaseNode.EventSubscriptions
                .Select(ToRuntimeEventSubscriptionDto)
                .ToList()),
            TimerDefinitions = NullIfEmpty(phaseNode.TimerDefinitions
                .Select(ToRuntimeTimerDefinitionDto)
                .ToList()),
            SoundEffectLibraryEntries = NullIfEmpty(phaseNode.SoundEffectLibraryEntries
                .Select(entry => ToCleanSoundEffectLibraryEntryDto(entry, null, $"phase:{phaseNode.Id:D}"))
                .ToList())
        };
    }

    private static PhaseNodeDto BuildResolvedPhaseTree(
        PhaseNodeDto source,
        IReadOnlyDictionary<Guid, PhaseNodeDto> byId,
        HashSet<Guid> ancestry)
    {
        if (source.Id != Guid.Empty && !ancestry.Add(source.Id))
        {
            return ToPhaseSidecarDto(source);
        }

        var resolved = ToPhaseSidecarDto(source);
        var children = new List<PhaseNodeDto>();

        if (resolved.ChildPhaseIds is { Count: > 0 })
        {
            foreach (var childId in resolved.ChildPhaseIds.Where(static id => id != Guid.Empty).Distinct())
            {
                if (byId.TryGetValue(childId, out var child))
                {
                    children.Add(BuildResolvedPhaseTree(child, byId, ancestry));
                }
            }
        }
        else if (source.Phases is { Count: > 0 })
        {
            foreach (var child in source.Phases)
            {
                children.Add(BuildResolvedPhaseTree(child, byId, ancestry));
            }
        }

        resolved.Phases = children;

        if (source.Id != Guid.Empty)
        {
            ancestry.Remove(source.Id);
        }

        return resolved;
    }

    private static PhaseNodeDto ToPhaseSidecarDto(PhaseNodeDto source)
    {
        return new PhaseNodeDto
        {
            Id = source.Id,
            ScopeKind = source.ScopeKind,
            PhaseTier = source.PhaseTier,
            PhaseKey = source.PhaseKey,
            DisplayName = source.DisplayName,
            Title = source.Title,
            TitlePresentationCueEffectKey = source.TitlePresentationCueEffectKey,
            Prologue = source.Prologue,
            ProloguePresentationCueEffectKey = source.ProloguePresentationCueEffectKey,
            Narrative = source.Narrative,
            NarrativePresentationCueEffectKey = source.NarrativePresentationCueEffectKey,
            ParentPhaseId = source.ParentPhaseId,
            ChildPhaseIds = (source.ChildPhaseIds ?? new List<Guid>())
                .Where(static id => id != Guid.Empty)
                .Distinct()
                .ToList(),
            PhaseAmbientSoundEffectId = source.PhaseAmbientSoundEffectId,
            PhaseAmbientTimerKey = source.PhaseAmbientTimerKey,
            PhaseAmbienceMode = source.PhaseAmbienceMode,
            BaseObjectIds = source.BaseObjectIds,
            GameObjectIds = source.GameObjectIds,
            ProcedureIds = source.ProcedureIds,
            GameProperties = source.GameProperties,
            AdditionalVerbs = source.AdditionalVerbs,
            AdditionalDirectionals = source.AdditionalDirectionals,
            AdditionalDirectionalTraversalMappings = source.AdditionalDirectionalTraversalMappings,
            AvailableGameActions = source.AvailableGameActions,
            EventSubscriptions = source.EventSubscriptions,
            TimerDefinitions = source.TimerDefinitions,
            SoundEffectLibraryEntries = source.SoundEffectLibraryEntries,
            Phases = new List<PhaseNodeDto>()
        };
    }

    private static void RegisterAuthoringObjectDtos(
        Dictionary<Guid, ProjectGameObjectDto> objectDtosById,
        IEnumerable<ProjectGameObjectDto> objects)
    {
        foreach (var objectDto in objects)
        {
            if (objectDto.Id == Guid.Empty)
            {
                continue;
            }

            objectDtosById[objectDto.Id] = objectDto;
        }
    }

    public string? TryReadProjectName(string projectFolder)
    {
        var metadataPath = Path.Combine(projectFolder, "project.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = File.ReadAllText(metadataPath);
        var metadata = DeserializeWithFileContext<ProjectMetadataDto>(metadataPath, json, _jsonReadOptions);
        return string.IsNullOrWhiteSpace(metadata?.Name) ? null : metadata.Name;
    }

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static string BuildProjectFilePath(string projectFolder, string projectName)
    {
        return Path.Combine(projectFolder, $"{Sanitize(projectName)}{ProjectFileExtension}");
    }

    private static string BuildRoomsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RoomFolderName);
    }

    private static string BuildRoomFilePath(string projectFilePath, Guid roomId)
    {
        return Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{roomId:N}{RoomFileSuffix}");
    }

    private static string BuildPlanetsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, PlanetFolderName);
    }

    private static string BuildPlanetFilePath(string projectFilePath, Guid planetId)
    {
        return Path.Combine(BuildPlanetsFolderPath(projectFilePath), $"{planetId:N}{PlanetFileSuffix}");
    }

    private static string BuildCountriesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, CountryFolderName);
    }

    private static string BuildCountryFilePath(string projectFilePath, Guid countryId)
    {
        return Path.Combine(BuildCountriesFolderPath(projectFilePath), $"{countryId:N}{CountryFileSuffix}");
    }

    private static string BuildAreasFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, AreaFolderName);
    }

    private static string BuildProceduresFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, ProcedureFolderName);
    }

    private static string BuildPhasesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, PhaseFolderName);
    }

    private static string BuildLegacyPhasesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, LegacyPhaseFolderName);
    }

    private static string BuildLegacyRoomsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{LegacyRoomsFolderSuffix}");
    }

    private static string BuildLegacyPlanetsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{LegacyPlanetsFolderSuffix}");
    }

    private static string BuildLegacyCountriesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{LegacyCountriesFolderSuffix}");
    }

    private static string BuildLegacyAreasFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{LegacyAreasFolderSuffix}");
    }

    private static string BuildLegacyProceduresFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{LegacyProceduresFolderSuffix}");
    }

    private static List<string> GetDesignerScopeFolderReadCandidates(string canonicalFolderPath, string legacyFolderPath)
    {
        var candidates = new List<string> { canonicalFolderPath };
        if (!string.Equals(canonicalFolderPath, legacyFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(legacyFolderPath);
        }

        return candidates;
    }

    private static string BuildProcedureFilePath(string projectFilePath, Guid procedureId)
    {
        return Path.Combine(BuildProceduresFolderPath(projectFilePath), $"{procedureId:N}{ProcedureFileSuffix}");
    }

    private static string BuildPhaseFilePath(string projectFilePath, Guid phaseId)
    {
        return Path.Combine(BuildPhasesFolderPath(projectFilePath), $"{phaseId:N}{PhaseFileSuffix}");
    }

    private static string BuildAreaFilePath(string projectFilePath, Guid areaId)
    {
        return Path.Combine(BuildAreasFolderPath(projectFilePath), $"{areaId:N}{AreaFileSuffix}");
    }

    private static string BuildProjectStateFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{ProjectStateFileSuffix}");
    }

    private static string BuildProjectGlobalNodeFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}{ProjectGlobalsFileSuffix}");
    }

    private static string? TryBuildProjectFilePathFromGlobalNodeFilePath(string globalNodeFilePath)
    {
        if (string.IsNullOrWhiteSpace(globalNodeFilePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(globalNodeFilePath);
        if (!fileName.EndsWith(ProjectGlobalsFileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(globalNodeFilePath) ?? string.Empty;
        var baseName = fileName[..^ProjectGlobalsFileSuffix.Length];

        // Canonical mapping: <name>.sbe.globals.json -> <name>.sbe.json
        if (baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase))
        {
            var canonicalCandidate = Path.Combine(folder, $"{baseName}.json");
            if (File.Exists(canonicalCandidate))
            {
                return canonicalCandidate;
            }
        }

        var defaultCandidate = Path.Combine(folder, $"{baseName}{ProjectFileExtension}");
        return File.Exists(defaultCandidate) ? defaultCandidate : null;
    }

    private static string BuildAuthoringGameObjectFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, AuthoringGameObjectFolderName);
    }

    private static string BuildAuthoringTemplatesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, AuthoringTemplateFolderName);
    }

    private static string BuildAuthoringObjectFolderPath(string projectFilePath, ScopeNodeKind? scopeKind)
    {
        return scopeKind == ScopeNodeKind.Templates
            ? BuildAuthoringTemplatesFolderPath(projectFilePath)
            : BuildAuthoringGameObjectFolderPath(projectFilePath);
    }

    private static string BuildAuthoringGameObjectFilePath(string projectFilePath, Guid objectId)
    {
        return Path.Combine(BuildAuthoringGameObjectFolderPath(projectFilePath), $"{objectId:N}{AuthoringGameObjectFileSuffix}");
    }

    private static string BuildAuthoringObjectFilePath(string projectFilePath, Guid objectId, ScopeNodeKind? scopeKind)
    {
        return Path.Combine(BuildAuthoringObjectFolderPath(projectFilePath, scopeKind), $"{objectId:N}{AuthoringGameObjectFileSuffix}");
    }

    private static string BuildRoomTemplatesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, RoomTemplateFolderName);
    }

    private static string BuildRoomTemplateFilePath(string projectFilePath, Guid roomTemplateId)
    {
        return Path.Combine(BuildRoomTemplatesFolderPath(projectFilePath), $"{roomTemplateId:N}{RoomTemplateFileSuffix}");
    }

    private static string BuildAuthoringIndexFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, AuthoringIndexFileName);
    }

    private static string BuildPhaseNarrativeReviewFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, PhaseNarrativeReviewFileName);
    }

    private static string BuildCleanExportRootFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, CleanExportFolderName);
    }

    private static string BuildCleanProjectFilePath(string projectFilePath)
    {
        var folder = BuildCleanExportRootFolderPath(projectFilePath);
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, $"{baseName}{CleanProjectFileSuffix}");
    }

    private static string BuildCleanNavigationFilePath(string projectFilePath)
    {
        var folder = BuildCleanExportRootFolderPath(projectFilePath);
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, $"{baseName}{CleanNavigationFileSuffix}");
    }

    private static string BuildCleanRoomsFolderPath(string projectFilePath)
    {
        var folder = BuildCleanExportRootFolderPath(projectFilePath);
        var baseName = BuildRuntimeProjectBaseName(projectFilePath);
        return Path.Combine(folder, $"{baseName}{CleanRoomsFolderSuffix}");
    }

    private static string BuildCleanProcedureFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), CleanProcedureFolderName);
    }

    private static string BuildCleanProcedureFilePath(string projectFilePath, Guid procedureId)
    {
        return Path.Combine(BuildCleanProcedureFolderPath(projectFilePath), $"{procedureId:N}{CleanProcedureFileSuffix}");
    }

    private static string BuildRuntimeProjectBaseName(string projectFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : baseName;
    }

    private static string BuildCleanAssetsFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), CleanAssetsFolderName);
    }

    private static string BuildCleanSharedImagesFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanAssetsFolderPath(projectFilePath), CleanImagesFolderName, CleanSharedImagesFolderName);
    }

    private static string BuildCleanSharedSoundsFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanAssetsFolderPath(projectFilePath), CleanSoundsFolderName, CleanSharedSoundsFolderName);
    }

    private static string BuildCleanAssetsManifestFilePath(string projectFilePath)
    {
        return Path.Combine(BuildCleanAssetsFolderPath(projectFilePath), CleanAssetsManifestFileName);
    }

    private static string BuildCleanPresentationCueCatalogFilePath(string projectFilePath)
    {
        return Path.Combine(BuildCleanAssetsFolderPath(projectFilePath), CleanPresentationCuesFolderName, PresentationCueCatalogFileName);
    }

    private static string BuildPresentationCueCatalogSourcePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Config", PresentationCueCatalogFileName);
    }

    private static void StagePresentationCueCatalog(string projectFilePath)
    {
        var sourcePath = BuildPresentationCueCatalogSourcePath();
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"Presentation cue catalog file not found at '{sourcePath}'.",
                sourcePath);
        }

        var destinationPath = BuildCleanPresentationCueCatalogFilePath(projectFilePath);
        var destinationFolderPath = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationFolderPath))
        {
            Directory.CreateDirectory(destinationFolderPath);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static string BuildCleanRuntimeIndexFilePath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), CleanRuntimeIndexFileName);
    }

    private static void RemoveLegacySbePrefixedRuntimeArtifacts(string projectFilePath)
    {
        var exportRootPath = BuildCleanExportRootFolderPath(projectFilePath);
        var legacyBaseName = Path.GetFileNameWithoutExtension(projectFilePath);
        var runtimeBaseName = BuildRuntimeProjectBaseName(projectFilePath);
        if (string.Equals(legacyBaseName, runtimeBaseName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var legacyFiles = new[]
        {
            Path.Combine(exportRootPath, $"{legacyBaseName}{CleanProjectFileSuffix}"),
            Path.Combine(exportRootPath, $"{legacyBaseName}{CleanNavigationFileSuffix}"),
            Path.Combine(exportRootPath, $"{legacyBaseName}.load-diagnostics.csv"),
            Path.Combine(exportRootPath, $"{legacyBaseName}.sbr.runtime.direct-command-echoes.json")
        };

        foreach (var path in legacyFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        var legacyRoomsFolder = Path.Combine(exportRootPath, $"{legacyBaseName}{CleanRoomsFolderSuffix}");
        if (Directory.Exists(legacyRoomsFolder))
        {
            Directory.Delete(legacyRoomsFolder, recursive: true);
        }
    }

    private static string BuildScopeKindRoomFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), ScopeKindRoomFolderName);
    }

    private static string BuildScopeKindBookFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), ScopeKindBookFolderName);
    }

    private static string BuildScopeKindGameObjectFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), ScopeKindGameObjectFolderName);
    }

    private static string BuildScopeKindPlanetFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), ScopeKindPlanetFolderName);
    }

    private static string BuildScopeKindCountryFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), ScopeKindCountryFolderName);
    }

    private static string BuildScopeKindPlanetFilePath(string projectFilePath, Guid planetId)
    {
        return Path.Combine(BuildScopeKindPlanetFolderPath(projectFilePath), $"{planetId:D}".ToUpperInvariant() + ScopeNodeFileSuffix);
    }

    private static string BuildScopeKindCountryFilePath(string projectFilePath, Guid countryId)
    {
        return Path.Combine(BuildScopeKindCountryFolderPath(projectFilePath), $"{countryId:D}".ToUpperInvariant() + ScopeNodeFileSuffix);
    }

    private static string BuildScopeKindAreaFolderPath(string projectFilePath)
    {
        return Path.Combine(BuildCleanExportRootFolderPath(projectFilePath), ScopeKindAreaFolderName);
    }

    private static string BuildScopeKindAreaFilePath(string projectFilePath, Guid areaId)
    {
        return Path.Combine(BuildScopeKindAreaFolderPath(projectFilePath), $"{areaId:D}".ToUpperInvariant() + ScopeNodeFileSuffix);
    }

    private static string BuildScopeKindBookFilePath(string projectFilePath, Guid phaseId)
    {
        return Path.Combine(BuildScopeKindBookFolderPath(projectFilePath), $"{phaseId:D}".ToUpperInvariant() + ScopeNodeFileSuffix);
    }

    private static string BuildScopeKindRoomFilePath(string projectFilePath, Guid roomId)
    {
        return Path.Combine(BuildScopeKindRoomFolderPath(projectFilePath), $"{roomId:D}".ToUpperInvariant() + ScopeNodeFileSuffix);
    }

    private static string BuildScopeKindGameObjectFilePath(string projectFilePath, Guid gameObjectId)
    {
        return Path.Combine(BuildScopeKindGameObjectFolderPath(projectFilePath), $"{gameObjectId:D}".ToUpperInvariant() + ScopeNodeFileSuffix);
    }

    private static string BuildCleanRoomFilePath(string projectFilePath, Guid roomId)
    {
        return Path.Combine(BuildCleanRoomsFolderPath(projectFilePath), $"{roomId:N}{CleanRoomFileSuffix}");
    }

    private static void ValidateTraversalOrThrow(ProjectModel project, string operationName)
    {
        var issues = BuildTraversalValidationIssues(project);
        var errors = issues.Where(static issue => issue.Severity == ValidationSeverity.Error).ToList();

        if (errors.Count == 0)
        {
            return;
        }

        var lines = errors
            .Select(issue =>
            {
                var line = $"- [{issue.Severity}] {issue.Path}: {issue.Description}";
                return string.IsNullOrWhiteSpace(issue.Hint)
                    ? line
                    : line + $" Fix: {issue.Hint}";
            })
            .ToList();

        var message = $"Traversal validation failed during {operationName}. Resolve the following issues before continuing:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
        throw new InvalidOperationException(message);
    }

    private static IReadOnlyList<ValidationIssue> BuildTraversalValidationIssues(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalRequireOpenSharedIsOpenRule());
        registry.Register(new TraversalConnectionIntegrityRule());
        registry.Register(new TraversalDirectionalTargetUniquenessRule());
        registry.Register(new TraversalLegPassableContractRule());
        registry.Register(new TraversalDoorLinkIntegrityRule());

        var engine = new ValidationEngine(registry);
        var execution = engine.Execute(new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport,
            ProjectFilePath: null));

        return execution.Issues;
    }

    private static string NormalizeToRelativeAssetPath(string filePath)
    {
        return filePath.Replace('\\', '/');
    }

    private static string SanitizeImageStem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "image";
        }

        var sanitized = Sanitize(value.Trim());
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }

    private static string DetectCanonicalImageExtension(string sourcePath, byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x38
            && (bytes[4] == 0x37 || bytes[4] == 0x39)
            && bytes[5] == 0x61)
        {
            return ".gif";
        }

        if (bytes.Length >= 2
            && bytes[0] == 0x42
            && bytes[1] == 0x4D)
        {
            return ".bmp";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            return ".webp";
        }

        var extension = Path.GetExtension(sourcePath);
        return string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : extension.ToLowerInvariant();
    }

}




