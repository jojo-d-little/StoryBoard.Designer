using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Storyboard.Shared.GameServices.RuntimeContext;
using Storyboard.Shared.GameStateData;
using Storyboard.Shared.RuntimeContracts.Dtos;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public class JsonExportServiceProjectStateTests
{
    [Fact]
    public void SaveProjectModel_WritesUiStateToStateSidecar_AndOmitsUiStateFromProjectFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "StateSplit.sbe.json");
            var model = new ProjectModel
            {
                Name = "StateSplit",
                AutoSaveSeconds = 42,
                UiState = new ProjectUiState
                {
                    PlanetName = "Earth",
                    CountryName = "USA",
                    AreaName = "Area-51",
                    MapDesignerAreaId = Guid.NewGuid(),
                    RoomId = Guid.NewGuid(),
                    SelectedWorkspaceTabIndex = 1
                }
            };

            service.SaveProjectModel(projectFilePath, model);

            var projectJson = File.ReadAllText(projectFilePath, Encoding.UTF8);
            Assert.DoesNotContain("\"uiState\"", projectJson, StringComparison.OrdinalIgnoreCase);

            var stateFilePath = BuildProjectStateFilePath(projectFilePath);
            Assert.True(File.Exists(stateFilePath));

            var stateJson = File.ReadAllText(stateFilePath, Encoding.UTF8);
            Assert.Contains("\"uiState\"", stateJson, StringComparison.OrdinalIgnoreCase);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal("Earth", loaded!.UiState.PlanetName);
            Assert.Equal("USA", loaded.UiState.CountryName);
            Assert.Equal("Area-51", loaded.UiState.AreaName);
            Assert.Equal(model.UiState.MapDesignerAreaId, loaded.UiState.MapDesignerAreaId);
            Assert.Equal(model.UiState.SelectedWorkspaceTabIndex, loaded.UiState.SelectedWorkspaceTabIndex);
            Assert.Equal(model.UiState.RoomId, loaded.UiState.RoomId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsPhaseHierarchyAndStartingPage()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "PhaseRoundTrip.sbe.json");
            var page = new PhaseNode
            {
                Id = Guid.NewGuid(),
                Tier = PhaseTier.Page,
                PhaseKey = "page-1",
                DisplayName = "Page 1",
                Title = "Arrival",
                TitlePresentationCueEffectKey = "text.hudoverlay.fadein.auto.2500",
                Prologue = "The story begins.",
                ProloguePresentationCueEffectKey = "text.hudoverlay.autoscroll.manualdismiss",
                Narrative = "You step into the first room.",
                NarrativePresentationCueEffectKey = "text.narrativedialog.archive"
            };
            var chapter = new PhaseNode
            {
                Id = Guid.NewGuid(),
                Tier = PhaseTier.Chapter,
                PhaseKey = "chapter-1",
                DisplayName = "Chapter 1"
            };
            var book = new PhaseNode
            {
                Id = Guid.NewGuid(),
                Tier = PhaseTier.Book,
                PhaseKey = "book-1",
                DisplayName = "Book 1"
            };

            chapter.AddChildScope(page);
            book.AddChildScope(chapter);

            var model = new ProjectModel
            {
                Name = "PhaseRoundTrip",
                PhaseBooks = new List<PhaseNode> { book },
                StartingPhasePageId = page.Id
            };

            service.SaveProjectModel(projectFilePath, model);

            var phaseFolderPath = BuildPhasesFolderPath(projectFilePath);
            Assert.True(Directory.Exists(phaseFolderPath));
            var phaseFiles = Directory.EnumerateFiles(phaseFolderPath, "*.phase.json", SearchOption.TopDirectoryOnly).ToList();
            Assert.Equal(3, phaseFiles.Count);

            var globalsJson = File.ReadAllText(BuildProjectGlobalsFilePath(projectFilePath), Encoding.UTF8);
            using var globalsDoc = JsonDocument.Parse(globalsJson);
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("phaseBookIds").GetArrayLength());
            Assert.Equal(0, globalsDoc.RootElement.GetProperty("phaseBooks").GetArrayLength());

            var authoringIndex = File.ReadAllText(BuildAuthoringIndexFilePath(projectFilePath), Encoding.UTF8);
            Assert.Contains("Book/", authoringIndex, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".phase.json", authoringIndex, StringComparison.OrdinalIgnoreCase);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(page.Id, loaded!.StartingPhasePageId);
            Assert.Single(loaded.PhaseBooks);

            var loadedBook = loaded.PhaseBooks.Single();
            Assert.Equal(PhaseTier.Book, loadedBook.Tier);
            Assert.Equal("book-1", loadedBook.PhaseKey);
            Assert.Single(loadedBook.Children);

            var loadedChapter = loadedBook.Children.Single();
            Assert.Equal(PhaseTier.Chapter, loadedChapter.Tier);
            Assert.Equal("chapter-1", loadedChapter.PhaseKey);
            Assert.Single(loadedChapter.Children);

            var loadedPage = loadedChapter.Children.Single();
            Assert.Equal(PhaseTier.Page, loadedPage.Tier);
            Assert.Equal("page-1", loadedPage.PhaseKey);
            Assert.Equal("Arrival", loadedPage.Title);
            Assert.Equal("text.hudoverlay.fadein.auto.2500", loadedPage.TitlePresentationCueEffectKey);
            Assert.Equal("The story begins.", loadedPage.Prologue);
            Assert.Equal("text.hudoverlay.autoscroll.manualdismiss", loadedPage.ProloguePresentationCueEffectKey);
            Assert.Equal("You step into the first room.", loadedPage.Narrative);
            Assert.Equal("text.narrativedialog.archive", loadedPage.NarrativePresentationCueEffectKey);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportPhaseNarrativeReviewHtml_UsesHeaderKeyAndOmitsDuplicateKeyTitleDetailRows()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "PhaseNarrativeReview.sbe.json");
            var page = new PhaseNode
            {
                Id = Guid.NewGuid(),
                Tier = PhaseTier.Page,
                PhaseKey = "page-1",
                DisplayName = "Page 1",
                Title = "The Cellar",
                Prologue = "A damp draft spills in from the cracked stairs.",
                Narrative = "An old lantern swings with each step downward."
            };

            var chapter = new PhaseNode
            {
                Id = Guid.NewGuid(),
                Tier = PhaseTier.Chapter,
                PhaseKey = "chapter-1",
                DisplayName = "Chapter 1",
                Title = "Descent"
            };

            var book = new PhaseNode
            {
                Id = Guid.NewGuid(),
                Tier = PhaseTier.Book,
                PhaseKey = "book-1",
                DisplayName = "Book 1"
            };

            chapter.AddChildScope(page);
            book.AddChildScope(chapter);

            var model = new ProjectModel
            {
                Name = "PhaseNarrativeReview",
                PhaseBooks = [book]
            };

            var htmlPath = service.ExportPhaseNarrativeReviewHtml(projectFilePath, model);
            Assert.True(File.Exists(htmlPath));

            var html = File.ReadAllText(htmlPath, Encoding.UTF8);

            Assert.Contains("<span class=\"phase-key\">chapter-1</span>", html, StringComparison.Ordinal);
            Assert.Contains("<span class=\"phase-key\">page-1</span>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<div class=\"label\">Key</div>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<div class=\"label\">Title</div>", html, StringComparison.Ordinal);
            Assert.Contains("<div class=\"label\">Narrative</div>", html, StringComparison.Ordinal);

            Assert.True(
                Regex.IsMatch(
                    html,
                    "<summary id=\\\"phase-[^\\\"]+\\\">.*?<span class=\\\"name\\\">Chapter 1</span>.*?<span class=\\\"title\\\">Descent</span>.*?<span class=\\\"phase-key\\\">chapter-1</span>",
                    RegexOptions.Singleline),
                "Expected chapter summary to keep title in header and key right-aligned in the same row.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_IgnoresInlineUiState_WhenStateSidecarIsMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "LegacyState.sbe.json");
            var roomId = Guid.NewGuid();
            var json = $$"""
            {
              "name": "LegacyState",
              "autoSaveSeconds": 60,
              "commandVerbs": [],
              "directionals": [],
              "startingPlanetName": "",
              "gameProperties": [],
              "uiState": {
                "planetName": "Mars",
                "countryName": "Olympus",
                "areaName": "Mons",
                "roomId": "{{roomId}}",
                "selectedWorkspaceTabIndex": 2
              },
                            "planetIds": []
            }
            """;

            File.WriteAllText(projectFilePath, json, Encoding.UTF8);
            File.WriteAllText(BuildProjectGlobalsFilePath(projectFilePath), "{}", Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(string.Empty, loaded!.UiState.PlanetName);
            Assert.Equal(string.Empty, loaded.UiState.CountryName);
            Assert.Equal(string.Empty, loaded.UiState.AreaName);
            Assert.Null(loaded.UiState.RoomId);
            Assert.Equal(0, loaded.UiState.SelectedWorkspaceTabIndex);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectUiState_WritesLatestStateWithoutFullProjectSave()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "UiStateOnly.sbe.json");
            var project = new ProjectModel { Name = "UiStateOnly" };
            service.SaveProjectModel(projectFilePath, project);

            var updatedUiState = new ProjectUiState
            {
                PlanetName = "Earth",
                CountryName = "USA",
                AreaName = "Birmingham",
                RoomId = Guid.NewGuid(),
                SelectedWorkspaceTabIndex = 1,
                LastSelectedNodePath = "planet:abc/group:children/room:def"
            };

            service.SaveProjectUiState(projectFilePath, updatedUiState);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(updatedUiState.PlanetName, loaded!.UiState.PlanetName);
            Assert.Equal(updatedUiState.CountryName, loaded.UiState.CountryName);
            Assert.Equal(updatedUiState.AreaName, loaded.UiState.AreaName);
            Assert.Equal(updatedUiState.RoomId, loaded.UiState.RoomId);
            Assert.Equal(updatedUiState.SelectedWorkspaceTabIndex, loaded.UiState.SelectedWorkspaceTabIndex);
            Assert.Equal(updatedUiState.LastSelectedNodePath, loaded.UiState.LastSelectedNodePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsSimulatorReplaySettings_InProjectFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "SimulatorReplaySettings.sbe.json");
            var model = new ProjectModel
            {
                Name = "SimulatorReplaySettings",
                SimulatorReplayFilePath = @"replays\deep-playthrough.sbe.sim.json",
                SimulatorReplaySpeed = 1.75
            };

            service.SaveProjectModel(projectFilePath, model);

            var json = File.ReadAllText(projectFilePath, Encoding.UTF8);
            Assert.Contains("\"simulatorReplayFilePath\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"simulatorReplaySpeed\"", json, StringComparison.OrdinalIgnoreCase);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(model.SimulatorReplayFilePath, loaded!.SimulatorReplayFilePath);
            Assert.Equal(model.SimulatorReplaySpeed, loaded.SimulatorReplaySpeed);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsProjectStackScaleDefaults_InProjectFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "StackScaleDefaults.sbe.json");
            var model = new ProjectModel
            {
                Name = "StackScaleDefaults",
                StackScaleStepDefault = 0.09,
                MinStackScaleDefault = 0.62
            };

            service.SaveProjectModel(projectFilePath, model);

            var json = File.ReadAllText(projectFilePath, Encoding.UTF8);
            Assert.Contains("\"stackScaleStepDefault\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"minStackScaleDefault\"", json, StringComparison.OrdinalIgnoreCase);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(model.StackScaleStepDefault, loaded!.StackScaleStepDefault);
            Assert.Equal(model.MinStackScaleDefault, loaded.MinStackScaleDefault);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsEventSubscriptionInputMappings_PerActionBinding()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "EventBindingMappings.sbe.json");
            var model = new ProjectModel
            {
                Name = "EventBindingMappings"
            };

            model.GlobalScope.EventSubscriptions.Add(new EventSubscriptionDefinition
            {
                Id = Guid.NewGuid(),
                EventKey = "item_moved",
                ActionBindings =
                [
                    new EventActionBindingDefinition
                    {
                        Order = 1,
                        Target = new EventBindingTargetDefinition { ActionName = "schedule_hammer_sound" },
                        InputArgumentMappings =
                        [
                            new EventInputArgumentMappingDefinition
                            {
                                InputEventPaylloadArgKey = "estimatedMoveDurationMs",
                                OutputActionPayloadArgKey = "timer.start.scheduleAfterMs"
                            }
                        ]
                    },
                    new EventActionBindingDefinition
                    {
                        Order = 2,
                        Target = new EventBindingTargetDefinition { ActionName = "play_hammer_sound" },
                        InputArgumentMappings =
                        [
                            new EventInputArgumentMappingDefinition
                            {
                                SourceType = EventInputArgumentMappingSourceType.ConstantValue,
                                OutputActionPayloadArgKey = "sound.play.overrideSoundEffectKey",
                                ConstantValue = "sound.effect.hammer.hit"
                            }
                        ]
                    }
                ]
            });

            service.SaveProjectModel(projectFilePath, model);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var subscription = Assert.Single(loaded!.GlobalScope.EventSubscriptions);
            Assert.Equal("item_moved", subscription.EventKey);
            Assert.Equal(2, subscription.ActionBindings.Count);

            var firstBinding = subscription.ActionBindings[0];
            var firstMapping = Assert.Single(firstBinding.InputArgumentMappings);
            Assert.Equal("estimatedMoveDurationMs", firstMapping.InputEventPaylloadArgKey);
            Assert.Equal("timer.start.scheduleAfterMs", firstMapping.OutputActionPayloadArgKey);

            var secondBinding = subscription.ActionBindings[1];
            var secondMapping = Assert.Single(secondBinding.InputArgumentMappings);
            Assert.Equal(EventInputArgumentMappingSourceType.ConstantValue, secondMapping.SourceType);
            Assert.Equal("sound.play.overrideSoundEffectKey", secondMapping.OutputActionPayloadArgKey);
            Assert.Equal("sound.effect.hammer.hit", secondMapping.ConstantValue);
            Assert.True(string.IsNullOrWhiteSpace(secondMapping.InputEventPaylloadArgKey));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsGamePresentationMetadata_InProjectFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "GamePresentationMetadata.sbe.json");
            var model = new ProjectModel
            {
                Name = "GamePresentationMetadata",
                GameDisplayName = "The Bent Brass Key",
                GameSummary = "A short mystery adventure in a locked manor.",
                GamePreviewImages =
                [
                    @"media\preview\cover.png",
                    @"media\preview\hallway.png"
                ]
            };

            service.SaveProjectModel(projectFilePath, model);

            var json = File.ReadAllText(projectFilePath, Encoding.UTF8);
            Assert.Contains("\"gameDisplayName\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"gameSummary\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"gamePreviewImages\"", json, StringComparison.OrdinalIgnoreCase);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(model.GameDisplayName, loaded!.GameDisplayName);
            Assert.Equal(model.GameSummary, loaded.GameSummary);
            Assert.Equal(model.GamePreviewImages, loaded.GamePreviewImages);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_UsesBuiltInStackScaleDefaults_WhenLegacyFieldsMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "LegacyStackScaleDefaults.sbe.json");
            var json = """
            {
              "name": "LegacyStackScaleDefaults",
              "autoSaveSeconds": 60,
              "roomImageCanvasWidth": 800,
              "roomImageCanvasHeight": 600,
              "roomDesignerGridCellSize": 40,
              "commandVerbs": [],
              "directionals": [],
              "gameProperties": [],
              "planetIds": []
            }
            """;

            File.WriteAllText(projectFilePath, json, Encoding.UTF8);
                        File.WriteAllText(BuildProjectGlobalsFilePath(projectFilePath), "{}", Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(ProjectModel.DefaultStackScaleStep, loaded!.StackScaleStepDefault);
            Assert.Equal(ProjectModel.DefaultMinStackScale, loaded.MinStackScaleDefault);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_DefaultTraversalModeFallsBackToFourDirectional_WhenMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "MissingTraversalMode.sbe.json");
            var json = """
            {
              "name": "MissingTraversalMode"
            }
            """;

            File.WriteAllText(projectFilePath, json, Encoding.UTF8);
            File.WriteAllText(BuildProjectGlobalsFilePath(projectFilePath), "{}", Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(AreaAdjacencyMode.FourDirectional, loaded!.DefaultTraversalMode);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_DefaultTraversalModeFallsBackToFourDirectional_WhenMalformed()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectFilePath = Path.Combine(tempRoot, "MalformedTraversalMode.sbe.json");
            var json = """
            {
              "name": "MalformedTraversalMode",
              "defaultTraversalMode": "DiagonalOnly"
            }
            """;

            File.WriteAllText(projectFilePath, json, Encoding.UTF8);
            File.WriteAllText(BuildProjectGlobalsFilePath(projectFilePath), "{}", Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(AreaAdjacencyMode.FourDirectional, loaded!.DefaultTraversalMode);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_PreservesObjectIdentity_WhenLegacyRoomObjectUsesObjectIdField()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var expectedObjectId = Guid.NewGuid();
            var room = new Room
            {
                Name = "LegacyObjectIdRoom",
                GameObjects =
                [
                    new GameObject
                    {
                        ObjectId = expectedObjectId,
                        Name = "LegacyObject",
                        ProducerNotes = "legacy"
                    }
                ]
            };

            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "LegacyObjectIdAlias",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "LegacyObjectIdAlias.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var objectFilePath = BuildAuthoringGameObjectFilePath(projectFilePath, expectedObjectId);
            var firstObject = JsonNode.Parse(File.ReadAllText(objectFilePath, Encoding.UTF8))?.AsObject()
                ?? throw new InvalidOperationException("Failed to parse object payload.");

            var persistedId = firstObject["id"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Missing object id.");
            firstObject.Remove("id");
            firstObject["objectId"] = persistedId;

            File.WriteAllText(objectFilePath, firstObject.ToJsonString(), Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedObject = loaded!.Planets
                .SelectMany(static p => p.Countries)
                .SelectMany(static c => c.Areas)
                .SelectMany(static a => a.Rooms)
                .SelectMany(static r => r.GameObjects)
                .Single(static obj => string.Equals(obj.Name, "LegacyObject", StringComparison.Ordinal));

            Assert.Equal(expectedObjectId, loadedObject.ObjectId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_PreservesSharedVariableJsonShape()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sharedId = Guid.NewGuid();

            var variableA = new GamePropertyDefinition
            {
                Id = Guid.NewGuid(),
                Name = "isOpen",
                DefaultValue = "false",
                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                SharedVariableId = sharedId
            };
            var variableB = new GamePropertyDefinition
            {
                Id = Guid.NewGuid(),
                Name = "isPassable",
                DefaultValue = "false",
                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                SharedVariableId = sharedId
            };

            var project = new ProjectModel
            {
                Name = "SharedShapeGuard",
                GlobalVariables = new List<GamePropertyDefinition> { variableA, variableB },
                SharedVariables =
                [
                    new SharedVariableDefinition
                    {
                        Id = sharedId,
                        Name = "DoorState",
                        DefaultValue = "false",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                        Participants = []
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "SharedShapeGuard.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var globalNodeFilePath = BuildProjectGlobalsFilePath(projectFilePath);
            using var projectDoc = JsonDocument.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8));
            using var globalNodeDoc = JsonDocument.Parse(File.ReadAllText(globalNodeFilePath, Encoding.UTF8));
            Assert.False(projectDoc.RootElement.TryGetProperty("sharedVariables", out _));

            var sharedVariables = globalNodeDoc.RootElement.GetProperty("sharedVariables");
            var shared = Assert.Single(sharedVariables.EnumerateArray());

            var expectedSharedKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "id",
                "name"
            };
            var actualSharedKeys = shared
                .EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(expectedSharedKeys, actualSharedKeys);

            Assert.Equal(sharedId.ToString(), shared.GetProperty("id").GetString());
            Assert.Equal("DoorState", shared.GetProperty("name").GetString());

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedShared = Assert.Single(loaded!.SharedVariables);
            Assert.Equal(sharedId, loadedShared.Id);
            Assert.Equal("DoorState", loadedShared.Name);

            var loadedParticipants = loadedShared.Participants;
            Assert.Equal(2, loadedParticipants.Count);
            Assert.All(loadedParticipants, participant => Assert.Equal("global", participant.Kind));
            Assert.Contains(loadedParticipants, participant => participant.OwnerId == variableA.Id && participant.VariableName == variableA.Name);
            Assert.Contains(loadedParticipants, participant => participant.OwnerId == variableB.Id && participant.VariableName == variableB.Name);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_SharedVariableSection_IsDeterministicAcrossRoundTripSaves()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sharedA = Guid.NewGuid();
            var sharedB = Guid.NewGuid();

            var variableA = new GamePropertyDefinition
            {
                Id = Guid.NewGuid(),
                Name = "flagA",
                DefaultValue = "false",
                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                SharedVariableId = sharedB
            };
            var variableB = new GamePropertyDefinition
            {
                Id = Guid.NewGuid(),
                Name = "flagB",
                DefaultValue = "false",
                ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                SharedVariableId = sharedA
            };

            var project = new ProjectModel
            {
                Name = "DeterministicSharedOrder",
                GlobalVariables = [variableA, variableB],
                SharedVariables =
                [
                    new SharedVariableDefinition { Id = sharedB, Name = "Beta" },
                    new SharedVariableDefinition { Id = sharedA, Name = "Alpha" }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "DeterministicSharedOrder.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var firstSaveJson = File.ReadAllText(projectFilePath, Encoding.UTF8);
            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            service.SaveProjectModel(projectFilePath, loaded!);
            var secondSaveJson = File.ReadAllText(projectFilePath, Encoding.UTF8);
            var globalNodeFilePath = BuildProjectGlobalsFilePath(projectFilePath);
            var globalNodeJson = File.ReadAllText(globalNodeFilePath, Encoding.UTF8);

            Assert.Equal(firstSaveJson, secondSaveJson);

            using var doc = JsonDocument.Parse(globalNodeJson);
            var ids = doc.RootElement
                .GetProperty("sharedVariables")
                .EnumerateArray()
                .Select(element => Guid.Parse(element.GetProperty("id").GetString()!))
                .ToList();

            var expected = ids.OrderBy(id => id).ToList();
            Assert.Equal(expected, ids);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_UsesPayloadBackedActionValues_AcrossActionFamilies()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var echoAction = new CommandAction
            {
                Name = "Echo",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "legacy-echo",
                Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Success"] = "payload-echo"
                }
            };
            echoAction.EchoMessage = "payload-echo";

            var checkAction = new CommandAction
            {
                Name = "Check",
                ActionType = CommandActionType.CheckGameProperty,
                FlagName = "legacy-flag",
                FlagValue = false
            };
            checkAction.Payload = new CheckGamePropertyPayload("payload-flag", true);

            var setFlagAction = new CommandAction
            {
                Name = "SetFlag",
                ActionType = CommandActionType.SetFlag,
                FlagName = "legacy-set-flag",
                FlagValue = false
            };
            setFlagAction.Payload = new SetFlagPayload("payload-set-flag", true);

            var setAction = new CommandAction
            {
                Name = "Set",
                ActionType = CommandActionType.SetGameProperty,
                GamePropertyName = "legacy-property",
                GamePropertyValue = "legacy-value"
            };
            setAction.Payload = new SetGamePropertyPayload("payload-property", "payload-value");

            var synonymTargetId = Guid.NewGuid();
            var synonymAction = new CommandAction
            {
                Name = "Synonym",
                ActionType = CommandActionType.Synonym,
                SynonymTargetActionId = Guid.NewGuid()
            };
            synonymAction.Payload = new SynonymPayload(synonymTargetId);

            var containerAction = new CommandAction
            {
                Name = "Put",
                ActionType = CommandActionType.PutObjectInContainer,
                TargetContainerId = "legacy-container",
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Success"] = "legacy-success",
                    ["Failure"] = "legacy-failure"
                }
            };
            containerAction.TargetContainerId = "payload-container";
            containerAction.SetOutcomeScript("Success", "payload-success");
            containerAction.SetOutcomeScript("Failure", "payload-failure");
            containerAction.Payload = new ContainerTransferPayload("payload-container");

            var navigateAction = new CommandAction
            {
                Name = "Navigate",
                ActionType = CommandActionType.NavigateDirection,
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Moved"] = "payload-moved",
                    ["DirectionRequired"] = "payload-direction-required"
                },
                OutcomeSoundEffectsMap = new Dictionary<string, List<OutcomeSoundEffectCue>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Moved"] =
                    [
                        new OutcomeSoundEffectCue
                        {
                            SoundEffectId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                            SoundEffectKeyHint = "footstep-light",
                            Enabled = true
                        },
                        new OutcomeSoundEffectCue
                        {
                            SoundEffectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                            SoundEffectKeyHint = "room-transition",
                            Enabled = false
                        }
                    ]
                }
            };
            navigateAction.SetOutcomeScript("Moved", "payload-moved");
            navigateAction.SetOutcomeScript("DirectionRequired", "payload-direction-required");
            navigateAction.SetOutcomeScript("NoExitInDirection", "payload-no-exit");
            navigateAction.SetOutcomeScript("NoTraversableExit", "payload-no-traversable");
            navigateAction.SetOutcomeScript("AmbiguousTraversal", "payload-ambiguous");
            navigateAction.SetOutcomeScript("TraversalBlocked", "payload-blocked");
            navigateAction.SetOutcomeScript("MoveFailed", "payload-move-failed");
            navigateAction.Payload = new NavigatePayload();

            var moveAction = new CommandAction
            {
                Name = "Move",
                ActionType = CommandActionType.MoveRoomObjectOnGrid
            };
            ActionPayloadAccessors.SetMoveRoomObjectOnGrid(
                moveAction,
                "north",
                3,
                true,
                RuntimeMovementVisualTransitionHint.Fast,
                allowJumpOver: true,
                travelVisualizationMode: RuntimeMovementTravelVisualizationMode.Direct);

            var rotateAction = new CommandAction
            {
                Name = "Rotate",
                ActionType = CommandActionType.RotateRoomObjectOnGrid
            };
            ActionPayloadAccessors.SetRotateRoomObjectOnGrid(
                rotateAction,
                RuntimeRotateRoomObjectOnGridAttemptMode.Face,
                null,
                "southwest",
                RuntimeMovementVisualTransitionHint.Slow);

            var stackAction = new CommandAction
            {
                Name = "Stack",
                ActionType = CommandActionType.StackRoomObjectOnAnother
            };
            ActionPayloadAccessors.SetStackRoomObjectOnAnother(
                stackAction,
                RuntimeMovementVisualTransitionHint.Quantum);

            var compositeTargetId = Guid.NewGuid();
            var compositePartA = Guid.NewGuid();
            var compositePartB = Guid.NewGuid();
            var compositeRecipeId = Guid.NewGuid();
            var compositePartsAction = new CommandAction
            {
                Name = "CompositeParts",
                ActionType = CommandActionType.BuildCompositeByParts,
                CompositeMatchMode = "legacy",
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Success"] = "payload-composite-success",
                    ["Failure"] = "payload-composite-failure",
                    ["IncompleteRecipe"] = "payload-incomplete",
                    ["SpecifyTarget"] = "payload-specify-target",
                    ["TargetMismatch"] = "payload-target-mismatch",
                    ["NoMatchingRecipe"] = "payload-no-match"
                }
            };
            compositePartsAction.Payload = new CompositeByPartsPayload(
                compositeTargetId,
                compositeRecipeId,
                [compositePartA, compositePartB],
                true,
                2,
                "payload-match-mode",
                "payload-ambiguity",
                "payload-consumption",
                "payload-template");

            var materializeSourceObjectId = Guid.NewGuid();
            var materializeAction = new CommandAction
            {
                Name = "Materialize",
                ActionType = CommandActionType.MaterializeObjectCopy,
                MaterializeSourceObjectId = Guid.NewGuid()
            };
            materializeAction.Payload = new MaterializeObjectCopyPayload(materializeSourceObjectId);

            var invokeProcedureId = Guid.NewGuid();
            var invokeProcedureAction = new CommandAction
            {
                Name = "Invoke",
                ActionType = CommandActionType.InvokeProcedure,
                ProcedureId = null
            };
            invokeProcedureAction.Payload = new InvokeProcedurePayload(invokeProcedureId);

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [echoAction, checkAction, setFlagAction, setAction, synonymAction, containerAction, navigateAction, moveAction, rotateAction, stackAction, compositePartsAction, materializeAction, invokeProcedureAction]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "PayloadFirst",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "PayloadFirst.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8));
            var actions = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().ToList();

            var echoJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Echo", StringComparison.Ordinal));
            var checkJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Check", StringComparison.Ordinal));
            var setFlagJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "SetFlag", StringComparison.Ordinal));
            var setJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Set", StringComparison.Ordinal));
            var synonymJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Synonym", StringComparison.Ordinal));
            var containerJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Put", StringComparison.Ordinal));
            var navigateJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Navigate", StringComparison.Ordinal));
            var moveJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Move", StringComparison.Ordinal));
            var rotateJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Rotate", StringComparison.Ordinal));
            var stackJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Stack", StringComparison.Ordinal));
            var compositeJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "CompositeParts", StringComparison.Ordinal));
            var materializeJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Materialize", StringComparison.Ordinal));
            var invokeJson = actions.Single(action => string.Equals(action.GetProperty("name").GetString(), "Invoke", StringComparison.Ordinal));

            var checkPayload = checkJson.GetProperty("payload");
            var setFlagPayload = setFlagJson.GetProperty("payload");
            var setPayload = setJson.GetProperty("payload");
            var synonymPayload = synonymJson.GetProperty("payload");
            var containerPayload = containerJson.GetProperty("payload");
            var movePayload = moveJson.GetProperty("payload");
            var rotatePayload = rotateJson.GetProperty("payload");
            var stackPayload = stackJson.GetProperty("payload");
            var materializePayload = materializeJson.GetProperty("payload");
            var invokePayload = invokeJson.GetProperty("payload");

            Assert.False(echoJson.TryGetProperty("echoMessage", out _));
            Assert.Equal("payload-flag", checkPayload.GetProperty("propertyName").GetString());
            Assert.True(checkPayload.GetProperty("expectedValue").GetBoolean());
            Assert.Equal("payload-set-flag", setFlagPayload.GetProperty("flagName").GetString());
            Assert.True(setFlagPayload.GetProperty("flagValue").GetBoolean());
            Assert.Equal("payload-property", setPayload.GetProperty("propertyName").GetString());
            Assert.Equal("payload-value", setPayload.GetProperty("propertyValue").GetString());
            Assert.Equal(synonymTargetId.ToString(), synonymPayload.GetProperty("targetActionId").GetString());
            Assert.Equal("payload-container", containerPayload.GetProperty("targetContainerId").GetString());
            Assert.False(containerJson.TryGetProperty("putInContainerSuccessMessage", out _));
            Assert.False(containerJson.TryGetProperty("putInContainerFailureMessage", out _));
            Assert.Equal(materializeSourceObjectId.ToString(), materializePayload.GetProperty("materializeSourceObjectId").GetString());
            Assert.False(navigateJson.TryGetProperty("navigateMovedEchoMessage", out _));
            Assert.False(navigateJson.TryGetProperty("navigateDirectionRequiredEchoMessage", out _));
            Assert.False(navigateJson.TryGetProperty("navigateNoExitInDirectionEchoMessage", out _));
            Assert.False(navigateJson.TryGetProperty("navigateNoTraversableExitEchoMessage", out _));
            Assert.False(navigateJson.TryGetProperty("navigateAmbiguousTraversalEchoMessage", out _));
            Assert.False(navigateJson.TryGetProperty("navigateTraversalBlockedEchoMessage", out _));
            Assert.False(navigateJson.TryGetProperty("navigateMoveFailedEchoMessage", out _));
            Assert.Equal("north", movePayload.GetProperty("directionToken").GetString());
            Assert.Equal(3, movePayload.GetProperty("distanceInCells").GetInt32());
            Assert.True(movePayload.GetProperty("allowPartialMove").GetBoolean());
            Assert.True(movePayload.GetProperty("allowJumpOver").GetBoolean());
            Assert.Equal("Fast", movePayload.GetProperty("visualTransitionHint").GetString());
            Assert.Equal("Direct", movePayload.GetProperty("travelVisualizationMode").GetString());
            Assert.Equal("Face", rotatePayload.GetProperty("mode").GetString());
            Assert.Equal("SW", rotatePayload.GetProperty("facingDirectionToken").GetString());
            Assert.Equal("Slow", rotatePayload.GetProperty("visualTransitionHint").GetString());
            Assert.False(rotateJson.TryGetProperty("rotateTurnDegrees", out _));
            Assert.Equal("Quantum", stackPayload.GetProperty("visualTransitionHint").GetString());
            Assert.Equal(invokeProcedureId.ToString(), invokePayload.GetProperty("procedureId").GetString());
            Assert.False(compositeJson.TryGetProperty("compositeMatchMode", out _));
            Assert.False(compositeJson.TryGetProperty("compositeAmbiguityPolicy", out _));
            Assert.False(compositeJson.TryGetProperty("compositeSuccessEchoMessage", out _));
            Assert.False(compositeJson.TryGetProperty("compositeFailureEchoMessage", out _));
            Assert.False(compositeJson.TryGetProperty("compositeIncompleteRecipeEchoMessage", out _));
            Assert.False(compositeJson.TryGetProperty("compositeSpecifyTargetEchoMessage", out _));
            Assert.False(compositeJson.TryGetProperty("compositeTargetMismatchEchoMessage", out _));
            Assert.False(compositeJson.TryGetProperty("compositeNoMatchingRecipeEchoMessage", out _));

            var navigateOutcomeMap = navigateJson.GetProperty("outcomeMessageMap");
            Assert.Equal("payload-moved", navigateOutcomeMap.GetProperty("Moved").GetString());
            Assert.Equal("payload-direction-required", navigateOutcomeMap.GetProperty("DirectionRequired").GetString());

            var containerOutcomeMap = containerJson.GetProperty("outcomeMessageMap");
            Assert.Equal("payload-success", containerOutcomeMap.GetProperty("Success").GetString());
            Assert.Equal("payload-failure", containerOutcomeMap.GetProperty("Failure").GetString());

            var compositeOutcomeMap = compositeJson.GetProperty("outcomeMessageMap");
            Assert.Equal("payload-composite-success", compositeOutcomeMap.GetProperty("Success").GetString());
            Assert.Equal("payload-composite-failure", compositeOutcomeMap.GetProperty("Failure").GetString());
            Assert.Equal("payload-incomplete", compositeOutcomeMap.GetProperty("IncompleteRecipe").GetString());
            Assert.Equal("payload-specify-target", compositeOutcomeMap.GetProperty("SpecifyTarget").GetString());
            Assert.Equal("payload-target-mismatch", compositeOutcomeMap.GetProperty("TargetMismatch").GetString());
            Assert.Equal("payload-no-match", compositeOutcomeMap.GetProperty("NoMatchingRecipe").GetString());

            var navigateOutcomeSoundEffects = navigateJson.GetProperty("outcomeSoundEffectsMap").GetProperty("Moved").EnumerateArray().ToList();
            Assert.Equal(2, navigateOutcomeSoundEffects.Count);
            Assert.Equal("11111111-2222-3333-4444-555555555555", navigateOutcomeSoundEffects[0].GetProperty("soundEffectId").GetString());
            Assert.Equal("footstep-light", navigateOutcomeSoundEffects[0].GetProperty("soundEffectKeyHint").GetString());
            Assert.True(navigateOutcomeSoundEffects[0].GetProperty("enabled").GetBoolean());
            Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", navigateOutcomeSoundEffects[1].GetProperty("soundEffectId").GetString());
            Assert.Equal("room-transition", navigateOutcomeSoundEffects[1].GetProperty("soundEffectKeyHint").GetString());
            Assert.False(navigateOutcomeSoundEffects[1].GetProperty("enabled").GetBoolean());

            var requiredParts = compositeJson.GetProperty("payload").GetProperty("compositeRequiredPartObjectIds").EnumerateArray().Select(value => value.GetString()).ToList();
            Assert.Contains(compositePartA.ToString(), requiredParts);
            Assert.Contains(compositePartB.ToString(), requiredParts);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedPlanet = Assert.Single(loaded!.Planets);
            var loadedCountry = Assert.Single(loadedPlanet.Countries);
            var loadedArea = Assert.Single(loadedCountry.Areas);
            var loadedRoom = Assert.Single(loadedArea.Rooms);
            var loadedEcho = loadedRoom.AvailableActions.Single(action => action.Name == "Echo");
            var loadedCheck = loadedRoom.AvailableActions.Single(action => action.Name == "Check");
            var loadedSetFlag = loadedRoom.AvailableActions.Single(action => action.Name == "SetFlag");
            var loadedSet = loadedRoom.AvailableActions.Single(action => action.Name == "Set");
            var loadedSynonym = loadedRoom.AvailableActions.Single(action => action.Name == "Synonym");
            var loadedContainer = loadedRoom.AvailableActions.Single(action => action.Name == "Put");
            var loadedNavigate = loadedRoom.AvailableActions.Single(action => action.Name == "Navigate");
            var loadedMove = loadedRoom.AvailableActions.Single(action => action.Name == "Move");
            var loadedRotate = loadedRoom.AvailableActions.Single(action => action.Name == "Rotate");
            var loadedStack = loadedRoom.AvailableActions.Single(action => action.Name == "Stack");
            var loadedComposite = loadedRoom.AvailableActions.Single(action => action.Name == "CompositeParts");
            var loadedMaterialize = loadedRoom.AvailableActions.Single(action => action.Name == "Materialize");
            var loadedInvoke = loadedRoom.AvailableActions.Single(action => action.Name == "Invoke");

            Assert.Equal("payload-echo", ActionPayloadAccessors.GetEchoMessage(loadedEcho));
            Assert.Equal("payload-flag", ActionPayloadAccessors.GetCheckPropertyName(loadedCheck));
            Assert.True(ActionPayloadAccessors.GetCheckExpectedValue(loadedCheck));
            Assert.Equal("payload-set-flag", ActionPayloadAccessors.GetSetFlagName(loadedSetFlag));
            Assert.True(ActionPayloadAccessors.GetSetFlagValue(loadedSetFlag));
            Assert.Equal("payload-property", ActionPayloadAccessors.GetSetPropertyName(loadedSet));
            Assert.Equal("payload-value", ActionPayloadAccessors.GetSetPropertyValue(loadedSet));
            Assert.Equal(synonymTargetId, loadedSynonym.SynonymTargetActionId);
            Assert.Equal("payload-container", loadedContainer.TargetContainerId);
            Assert.Equal("payload-success", loadedContainer.GetOutcomeScript("Success"));
            Assert.Equal("payload-failure", loadedContainer.GetOutcomeScript("Failure"));
            Assert.Equal(materializeSourceObjectId, ActionPayloadAccessors.GetMaterializeSourceObjectId(loadedMaterialize));
            Assert.Equal("payload-moved", loadedNavigate.OutcomeMessageMap["Moved"]);
            Assert.Equal("payload-direction-required", loadedNavigate.OutcomeMessageMap["DirectionRequired"]);
            Assert.True(loadedNavigate.OutcomeSoundEffectsMap.TryGetValue("Moved", out var loadedMovedCues));
            Assert.NotNull(loadedMovedCues);
            Assert.Equal(2, loadedMovedCues!.Count);
            Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), loadedMovedCues[0].SoundEffectId);
            Assert.Equal("footstep-light", loadedMovedCues[0].SoundEffectKeyHint);
            Assert.True(loadedMovedCues[0].Enabled);
            Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), loadedMovedCues[1].SoundEffectId);
            Assert.Equal("room-transition", loadedMovedCues[1].SoundEffectKeyHint);
            Assert.False(loadedMovedCues[1].Enabled);
            var loadedMovePayload = ActionPayloadAccessors.GetMoveRoomObjectOnGridPayload(loadedMove);
            Assert.Equal("north", loadedMovePayload.DirectionToken);
            Assert.Equal(3, loadedMovePayload.DistanceInCells);
            Assert.True(loadedMovePayload.AllowPartialMove);
            Assert.True(loadedMovePayload.AllowJumpOver);
            Assert.Equal(RuntimeMovementVisualTransitionHint.Fast, loadedMovePayload.VisualTransitionHint);
            Assert.Equal(RuntimeMovementTravelVisualizationMode.Direct, loadedMovePayload.TravelVisualizationMode);
            var loadedRotatePayload = ActionPayloadAccessors.GetRotateRoomObjectOnGridPayload(loadedRotate);
            Assert.Equal(RuntimeRotateRoomObjectOnGridAttemptMode.Face, loadedRotatePayload.Mode);
            Assert.Null(loadedRotatePayload.TurnDegrees);
            Assert.Equal("SW", loadedRotatePayload.FacingDirectionToken);
            Assert.Equal(RuntimeMovementVisualTransitionHint.Slow, loadedRotatePayload.VisualTransitionHint);
            var loadedStackPayload = ActionPayloadAccessors.GetStackRoomObjectOnAnotherPayload(loadedStack);
            Assert.Equal(RuntimeMovementVisualTransitionHint.Quantum, loadedStackPayload.VisualTransitionHint);
            var loadedCompositePayload = ActionPayloadAccessors.GetCompositeByPartsPayload(loadedComposite);
            Assert.Equal("payload-match-mode", loadedCompositePayload.CompositeMatchMode);
            Assert.Equal("payload-ambiguity", loadedCompositePayload.CompositeAmbiguityPolicy);
            Assert.Equal("payload-composite-success", loadedComposite.OutcomeMessageMap["Success"]);
            Assert.Equal("payload-composite-failure", loadedComposite.OutcomeMessageMap["Failure"]);
            Assert.Equal(invokeProcedureId, ActionPayloadAccessors.GetProcedureId(loadedInvoke));
            Assert.Contains(compositePartA, loadedCompositePayload.CompositeRequiredPartObjectIds);
            Assert.Contains(compositePartB, loadedCompositePayload.CompositeRequiredPartObjectIds);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_PreservesCompositeActionIdsWithoutObjectIdNormalization_OnSaveAndReload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = "Room A"
            };

            var partA = new GameObject
            {
                Name = "Froghair",
                NameInGame = "Froghair",
                ObjectId = Guid.NewGuid()
            };
            var partB = new GameObject
            {
                Name = "TeaLeaves",
                NameInGame = "TeaLeaves",
                ObjectId = Guid.NewGuid()
            };
            var target = new GameObject
            {
                Name = "SmokeBall",
                NameInGame = "SmokeBall",
                ObjectId = Guid.NewGuid(),
                IsCompositeTarget = true,
                CompositeRequiredParts =
                [
                    new CompositePartRequirement { PartObjectId = partA.ObjectId, RequiredQuantity = 1 },
                    new CompositePartRequirement { PartObjectId = partB.ObjectId, RequiredQuantity = 1 }
                ]
            };

            var partAObjectId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
            var partBObjectId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
            var targetObjectId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

            var compositeAction = new CommandAction
            {
                Name = "Build SmokeBall",
                ActionType = CommandActionType.BuildCompositeByParts,
                Payload = new CompositeByPartsPayload(
                    targetObjectId,
                    target.CompositeRecipeId,
                    [partAObjectId, partBObjectId],
                    false,
                    null,
                    "ExactPartSet",
                    "FailWithHint",
                    "ContainedInComposite",
                    string.Empty)
            };

            room.GameObjects = [partA, partB, target];
            room.AvailableActions = [compositeAction];

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "CompositeNormalization",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "CompositeNormalization.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8));
            var savedAction = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().Single();

            var savedPayload = savedAction.GetProperty("payload");

            Assert.Equal(targetObjectId.ToString(), savedPayload.GetProperty("compositeTargetObjectId").GetString());
            var savedParts = savedPayload.GetProperty("compositeRequiredPartObjectIds")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToList();
            Assert.Contains(partAObjectId.ToString(), savedParts);
            Assert.Contains(partBObjectId.ToString(), savedParts);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedPlanet = Assert.Single(loaded!.Planets);
            var loadedCountry = Assert.Single(loadedPlanet.Countries);
            var loadedArea = Assert.Single(loadedCountry.Areas);
            var loadedRoom = Assert.Single(loadedArea.Rooms);
            var loadedAction = Assert.Single(loadedRoom.AvailableActions);

            Assert.Equal(targetObjectId, loadedAction.CompositeTargetObjectId);
            Assert.Contains(partAObjectId, loadedAction.CompositeRequiredPartObjectIds);
            Assert.Contains(partBObjectId, loadedAction.CompositeRequiredPartObjectIds);
            Assert.DoesNotContain(partA.ObjectId, loadedAction.CompositeRequiredPartObjectIds);
            Assert.DoesNotContain(partB.ObjectId, loadedAction.CompositeRequiredPartObjectIds);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_PersistsObjectNameSynonyms_RoundTrip()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = "Room A",
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Knife",
                        NameInGame = "Kitchen Knife",
                        NameSynonyms = ["blade", "shiv", "blade"]
                    }
                ]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "NameSynonymRoundTrip",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "NameSynonymRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var savedObjectPath = BuildAuthoringGameObjectFilePath(projectFilePath, room.GameObjects.Single().ObjectId);
            using var objectDoc = JsonDocument.Parse(File.ReadAllText(savedObjectPath, Encoding.UTF8));
            var savedObject = objectDoc.RootElement;
            var savedSynonyms = savedObject.GetProperty("nameSynonyms")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            Assert.Equal(2, savedSynonyms.Count);
            Assert.Contains("blade", savedSynonyms, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("shiv", savedSynonyms, StringComparer.OrdinalIgnoreCase);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedPlanet = Assert.Single(loaded!.Planets);
            var loadedCountry = Assert.Single(loadedPlanet.Countries);
            var loadedArea = Assert.Single(loadedCountry.Areas);
            var loadedRoom = Assert.Single(loadedArea.Rooms);
            var loadedObject = Assert.Single(loadedRoom.GameObjects);

            Assert.Equal(2, loadedObject.NameSynonyms.Count);
            Assert.Contains("blade", loadedObject.NameSynonyms, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("shiv", loadedObject.NameSynonyms, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_DoesNotPersistCompositeRecipeId_OnNonTargetParts()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var part = new GameObject
            {
                Name = "Part",
                NameInGame = "Part",
                ObjectId = Guid.NewGuid(),
                IsCompositeTarget = false,
                CompositeRecipeId = Guid.NewGuid()
            };

            var target = new GameObject
            {
                Name = "Target",
                NameInGame = "Target",
                ObjectId = Guid.NewGuid(),
                IsCompositeTarget = true,
                CompositeRecipeId = Guid.NewGuid(),
                CompositeRequiredParts =
                [
                    new CompositePartRequirement
                    {
                        PartObjectId = part.ObjectId,
                        RequiredQuantity = 1
                    }
                ]
            };

            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = "Room A",
                GameObjects = [part, target]
            };
            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "NoPartRecipeKnowledge",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "NoPartRecipeKnowledge.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using var partDoc = JsonDocument.Parse(File.ReadAllText(BuildAuthoringGameObjectFilePath(projectFilePath, part.ObjectId), Encoding.UTF8));
            using var targetDoc = JsonDocument.Parse(File.ReadAllText(BuildAuthoringGameObjectFilePath(projectFilePath, target.ObjectId), Encoding.UTF8));
            var savedPart = partDoc.RootElement;
            var savedTarget = targetDoc.RootElement;

            Assert.False(savedPart.TryGetProperty("compositeRecipe", out _));

            Assert.True(savedTarget.TryGetProperty("compositeRecipe", out var savedTargetCompositeRecipe));
            Assert.NotEqual(Guid.Empty.ToString(), savedTargetCompositeRecipe.GetProperty("recipeId").GetString());

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedPlanet = Assert.Single(loaded!.Planets);
            var loadedCountry = Assert.Single(loadedPlanet.Countries);
            var loadedArea = Assert.Single(loadedCountry.Areas);
            var loadedRoom = Assert.Single(loadedArea.Rooms);

            var loadedPart = loadedRoom.GameObjects.Single(obj => obj.Name == part.Name);
            var loadedTarget = loadedRoom.GameObjects.Single(obj => obj.Name == target.Name);

            Assert.Equal(Guid.Empty, loadedPart.CompositeRecipeId);
            Assert.NotEqual(Guid.Empty, loadedTarget.CompositeRecipeId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndRuntimeExport_PersistDesignTimeParentObjectId_ForContainedObjects()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var child = new GameObject
            {
                Name = "Child",
                NameInGame = "Child",
                ObjectId = Guid.NewGuid()
            };

            var parent = new GameObject
            {
                Name = "Parent",
                NameInGame = "Parent",
                ObjectId = Guid.NewGuid(),
                ContainedObjects = new List<GameObject> { child }
            };

            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = "Room A",
                GameObjects = new List<GameObject> { parent }
            };

            var area = new Area { Name = "Area A", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country A", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet A", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "ParentIdPersistence",
                Planets = new List<Planet> { planet },
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "ParentIdPersistence.sbe.json");
            service.SaveProjectModel(projectFilePath, project);
            service.ExportCleanProjectV1(projectFilePath, project);

            using var parentDoc = JsonDocument.Parse(File.ReadAllText(BuildAuthoringGameObjectFilePath(projectFilePath, parent.ObjectId), Encoding.UTF8));
            var savedParent = parentDoc.RootElement;
            var savedChild = savedParent.GetProperty("containedObjects").EnumerateArray().Single();
            Assert.Equal(parent.ObjectId.ToString(), savedChild.GetProperty("designTimeParentObjectId").GetString());

            var parentScopePath = BuildScopeKindGameObjectPath(projectFilePath, parent.ObjectId);
            using var parentScopeDoc = JsonDocument.Parse(File.ReadAllText(parentScopePath, Encoding.UTF8));
            var cleanChild = parentScopeDoc.RootElement.GetProperty("containedObjects").EnumerateArray().Single();
            Assert.Equal(parent.ObjectId.ToString(), cleanChild.GetProperty("designTimeParentObjectId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_WritesSuccessEchoMessage_AndOmitsLegacyOptionalEchoField()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var action = new CommandAction
            {
                Name = "Set",
                ActionType = CommandActionType.SetGameProperty,
                GamePropertyName = "doorState",
                GamePropertyValue = "open",
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Success"] = "canonical success echo",
                    ["Failure"] = string.Empty
                }
            };

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [action]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "OutcomeEchoFallback",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "OutcomeEchoFallback.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8));
            var savedAction = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().Single();

            Assert.False(savedAction.TryGetProperty("optionalEchoMessage", out _));
            var outcomeMap = savedAction.GetProperty("outcomeMessageMap");
            Assert.Equal("canonical success echo", outcomeMap.GetProperty("Success").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadProjectModel_PersistsOutcomeMessageMap_WithCanonicalSupportedTokensAndUnsupportedEntries()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var action = new CommandAction
            {
                Name = "Set",
                ActionType = CommandActionType.SetGameProperty,
                GamePropertyName = "doorState",
                GamePropertyValue = "open",
                OutcomeMessageMap = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["success"] = string.Empty,
                    ["CustomFutureCode"] = "future text"
                }
            };

            var room = new Room
            {
                Name = "Room A",
                AvailableActions = [action]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "OutcomeMapPersistence",
                Planets = [planet],
                StartingPlanetName = planet.Name
            };

            var projectFilePath = Path.Combine(tempRoot, "OutcomeMapPersistence.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8));
            var savedAction = roomDoc.RootElement.GetProperty("availableGameActions").EnumerateArray().Single();

            var map = savedAction.GetProperty("outcomeMessageMap");
            Assert.Equal(string.Empty, map.GetProperty("Success").GetString());
            Assert.False(map.TryGetProperty("Failure", out _));
            Assert.Equal("future text", map.GetProperty("CustomFutureCode").GetString());

            var loaded = service.TryLoadProjectModel(projectFilePath);
            var loadedAction = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .AvailableActions.Single();

            Assert.True(loadedAction.OutcomeMessageMap.ContainsKey("Success"));
            Assert.Equal(string.Empty, loadedAction.OutcomeMessageMap["Success"]);
            Assert.Equal("future text", loadedAction.OutcomeMessageMap["CustomFutureCode"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_WritesScopeNodesToSidecars_AndOmitsInlineHierarchyFromProjectFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "RoomA" };
            var roomB = new Room { Name = "RoomB" };
            var area = new Area
            {
                Name = "TestArea",
                Rooms = new List<Room> { roomA, roomB },
                Links = new List<RoomLink>
                {
                    new()
                    {
                        FromRoomId = roomA.Id,
                        ToRoomId = roomB.Id,
                        Direction = Direction.North
                    }
                }
            };

            var country = new Country { Name = "TestCountry", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "TestPlanet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "LegacyShape",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LegacyShape.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8));
            Assert.False(projectDoc.RootElement.TryGetProperty("planetIds", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("planets", out _));

            var globalNodePath = BuildProjectGlobalsFilePath(projectFilePath);
            using var globalNodeDoc = JsonDocument.Parse(File.ReadAllText(globalNodePath, Encoding.UTF8));
            var globalPlanetIds = globalNodeDoc.RootElement.GetProperty("planetIds");
            Assert.Equal(JsonValueKind.Array, globalPlanetIds.ValueKind);
            Assert.Single(globalPlanetIds.EnumerateArray());

            var planetsFolderPath = BuildPlanetsFolderPath(projectFilePath);
            var countriesFolderPath = BuildCountriesFolderPath(projectFilePath);
            var areasFolderPath = BuildAreasFolderPath(projectFilePath);
            Assert.True(Directory.Exists(planetsFolderPath));
            Assert.True(Directory.Exists(countriesFolderPath));
            Assert.True(Directory.Exists(areasFolderPath));

            var planetFiles = Directory.GetFiles(planetsFolderPath, "*.planet.json");
            var countryFiles = Directory.GetFiles(countriesFolderPath, "*.country.json");
            var areaFiles = Directory.GetFiles(areasFolderPath, "*.area.json");
            Assert.Single(planetFiles);
            Assert.Single(countryFiles);
            Assert.Single(areaFiles);

            using var areaDoc = JsonDocument.Parse(File.ReadAllText(areaFiles[0], Encoding.UTF8));
            Assert.False(areaDoc.RootElement.TryGetProperty("links", out _));
            Assert.True(areaDoc.RootElement.TryGetProperty("traversalConnections", out var traversalConnections));
            Assert.Equal(JsonValueKind.Array, traversalConnections.ValueKind);
            Assert.Empty(traversalConnections.EnumerateArray());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_IgnoresLegacyScopeFolderLayout_WhenCanonicalFoldersAreMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room { Name = "LegacyRoom" };
            var area = new Area { Name = "LegacyArea", Rooms = new List<Room> { room } };
            var country = new Country { Name = "LegacyCountry", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "LegacyPlanet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "LegacyLayoutFallback",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LegacyLayoutFallback.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            MoveFolder(BuildRoomsFolderPath(projectFilePath), BuildLegacyRoomsFolderPath(projectFilePath));
            MoveFolder(BuildPlanetsFolderPath(projectFilePath), BuildLegacyPlanetsFolderPath(projectFilePath));
            MoveFolder(BuildCountriesFolderPath(projectFilePath), BuildLegacyCountriesFolderPath(projectFilePath));
            MoveFolder(BuildAreasFolderPath(projectFilePath), BuildLegacyAreasFolderPath(projectFilePath));
            MoveFolder(BuildProceduresFolderPath(projectFilePath), BuildLegacyProceduresFolderPath(projectFilePath));

            var loaded = service.TryLoadProjectModel(projectFilePath);

            Assert.NotNull(loaded);
            Assert.Empty(loaded!.Planets);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        static void MoveFolder(string sourcePath, string destinationPath)
        {
            if (!Directory.Exists(sourcePath))
            {
                return;
            }

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, recursive: true);
            }

            Directory.Move(sourcePath, destinationPath);
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsLinkedRoomInstanceMetadata()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var baseObjectId = Guid.NewGuid();
            var roomInstanceObject = new GameObject
            {
                Name = "Marbles",
                ProducerNotes = "marbles",
                IsQuantifiable = true,
                Quantity = 4,
                QuantifiablePlacementDistributionMode = "GroupedStack",
                ObjectId = Guid.NewGuid(),
                LinkedBaseObjectId = baseObjectId,
                LinkActionsToBaseObject = true
            };

            var room = new Room
            {
                Name = "Lab",
                GameObjects = new List<GameObject>
                {
                    roomInstanceObject
                }
            };

            var area = new Area { Name = "Floor", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "LinkedInstanceRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedInstanceRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedRoomObject = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single(obj => string.Equals(obj.ProducerNotes, "marbles", StringComparison.OrdinalIgnoreCase));

            Assert.NotEqual(Guid.Empty, loadedRoomObject.ObjectId);
            Assert.Equal(baseObjectId, loadedRoomObject.LinkedBaseObjectId);
            Assert.Equal(baseObjectId, loadedRoomObject.LinkedBaseObjectId);
            Assert.True(loadedRoomObject.LinkActionsToBaseObject);
            Assert.True(loadedRoomObject.IsRoomInstance);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsObjectTypeLinkedBaseObjectIdAndInstanceOverrides()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var linkedBaseObjectId = Guid.NewGuid();
            var roomInstanceObject = new GameObject
            {
                Name = "Standard Door",
                ObjectType = "Area.StandardDoor",
                ProducerNotes = "door-instance",
                LinkedBaseObjectId = linkedBaseObjectId,
                LinkActionsToBaseObject = true,
                InstanceOverrides = new Dictionary<string, string?>
                {
                    ["NameInGame"] = "North Door",
                    ["Description"] = "Leads into the lab"
                }
            };

            var room = new Room
            {
                Name = "Lab",
                GameObjects = new List<GameObject>
                {
                    roomInstanceObject
                }
            };

            var area = new Area { Name = "Floor", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "ObjectTypeAndOverridesRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "ObjectTypeAndOverridesRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var savedObjectPath = BuildAuthoringGameObjectFilePath(projectFilePath, roomInstanceObject.ObjectId);
            using (var objectDoc = JsonDocument.Parse(File.ReadAllText(savedObjectPath, Encoding.UTF8)))
            {
                var savedObject = objectDoc.RootElement;
                Assert.Equal("Area.StandardDoor", savedObject.GetProperty("objectType").GetString());
                Assert.Equal(linkedBaseObjectId.ToString(), savedObject.GetProperty("linkedBaseObjectId").GetString());
                var overrides = savedObject.GetProperty("instanceOverrides");
                Assert.Equal("North Door", overrides.GetProperty("NameInGame").GetString());
                Assert.Equal("Leads into the lab", overrides.GetProperty("Description").GetString());
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedRoomObject = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single(obj => string.Equals(obj.ProducerNotes, "door-instance", StringComparison.OrdinalIgnoreCase));

            Assert.Equal("Area.StandardDoor", loadedRoomObject.ObjectType);
            Assert.Equal(linkedBaseObjectId, loadedRoomObject.LinkedBaseObjectId);
            Assert.Equal("North Door", loadedRoomObject.InstanceOverrides["NameInGame"]);
            Assert.Equal("Leads into the lab", loadedRoomObject.InstanceOverrides["Description"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_LinkedRoomInstance_OmitsDefinitionOwnedFieldsFromRoomJson()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var linkedBaseObjectId = Guid.NewGuid();
            var roomInstanceObject = new GameObject
            {
                Name = "Standard Door",
                ObjectType = "Area.StandardDoor",
                ProducerNotes = "door-instance-prune",
                LinkedBaseObjectId = linkedBaseObjectId,
                LinkActionsToBaseObject = true,
                NameInGame = "North Door",
                NameSynonyms = ["portal"],
                Description = "Leads into the lab",
                IsInventoriable = true,
                AdditionalVerbs = ["inspect"],
                ImageVariantChooserScript = "return 'default';",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = "images/door.png",
                        IsDefault = true
                    }
                ],
                InstanceOverrides = new Dictionary<string, string?>
                {
                    ["NameInGame"] = "North Door",
                    ["Description"] = "Leads into the lab"
                }
            };

            var room = new Room
            {
                Name = "Lab",
                GameObjects = new List<GameObject>
                {
                    roomInstanceObject
                }
            };

            var area = new Area { Name = "Floor", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "LinkedInstancePrune",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedInstancePrune.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var savedObjectPath = BuildAuthoringGameObjectFilePath(projectFilePath, roomInstanceObject.ObjectId);
            using var objectDoc = JsonDocument.Parse(File.ReadAllText(savedObjectPath, Encoding.UTF8));
            var savedObject = objectDoc.RootElement;

            Assert.Equal(linkedBaseObjectId.ToString(), savedObject.GetProperty("linkedBaseObjectId").GetString());
            Assert.True(savedObject.GetProperty("linkActionsToBaseObject").GetBoolean());
            Assert.True(savedObject.TryGetProperty("instanceOverrides", out _));
            Assert.Equal("Standard Door", savedObject.GetProperty("name").GetString());
            Assert.Equal("Area.StandardDoor", savedObject.GetProperty("objectType").GetString());

            Assert.Equal("North Door", savedObject.GetProperty("nameInGame").GetString());
            Assert.False(savedObject.TryGetProperty("nameSynonyms", out _));
            Assert.False(savedObject.TryGetProperty("description", out _));
            Assert.False(savedObject.TryGetProperty("isInventoriable", out _));
            Assert.False(savedObject.TryGetProperty("additionalVerbs", out _));
            Assert.False(savedObject.TryGetProperty("imageVariants", out _));
            Assert.False(savedObject.TryGetProperty("imageVariantChooserScript", out _));
            Assert.False(savedObject.TryGetProperty("availableGameActions", out _));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_LinkedRoomInstance_ReloadsWithDefinitionImageVariants()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var linkedBaseObjectId = Guid.NewGuid();
            var baseObject = new GameObject
            {
                ObjectId = linkedBaseObjectId,
                Name = "Base Tile",
                ImageVariantChooserScript = "return 'default';",
                FootprintWidthCells = 2,
                FootprintHeightCells = 3,
                FootprintOrientation = "E",
                StackGroup = 4,
                ObjectHeightUnits = 5,
                HeightInRoom = 2,
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = "images/base-tile.png",
                        IsDefault = true
                    }
                ]
            };

            var linkedInstance = new GameObject
            {
                Name = "Linked Tile",
                LinkedBaseObjectId = linkedBaseObjectId,
                LinkActionsToBaseObject = true
            };

            var room = new Room { Name = "Board", GameObjects = [linkedInstance] };
            var area = new Area { Name = "Floor", BaseObjects = [baseObject], Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };

            var project = new ProjectModel
            {
                Name = "LinkedReloadImageHydration",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedReloadImageHydration.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedLinked = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single();

            Assert.Equal(linkedBaseObjectId, loadedLinked.LinkedBaseObjectId);
            Assert.Equal("return 'default';", loadedLinked.ImageVariantChooserScript);
            Assert.Single(loadedLinked.ImageVariants);
            Assert.Equal("default", loadedLinked.ImageVariants[0].VariantName);
            Assert.Equal("images/base-tile.png", loadedLinked.ImageVariants[0].FullImagePath);
            Assert.Equal("images/base-tile.png", loadedLinked.ResolveDefaultImagePath());
            Assert.Equal(2, loadedLinked.FootprintWidthCells);
            Assert.Equal(3, loadedLinked.FootprintHeightCells);
            Assert.Equal("E", loadedLinked.FootprintOrientation);
            Assert.Equal(4, loadedLinked.StackGroup);
            Assert.Equal(5, loadedLinked.ObjectHeightUnits);
            Assert.Equal(2, loadedLinked.HeightInRoom);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_LinkedRoomTemplateInstance_ReloadsWithDefinitionFootprint()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var linkedBaseObjectId = Guid.NewGuid();
            var baseObject = new GameObject
            {
                ObjectId = linkedBaseObjectId,
                Name = "Base Door",
                FootprintWidthCells = 6,
                FootprintHeightCells = 1,
                FootprintOrientation = "N"
            };

            var linkedTemplateInstance = new GameObject
            {
                Name = "Template Door",
                LinkedBaseObjectId = linkedBaseObjectId,
                LinkActionsToBaseObject = true
            };

            var roomTemplate = new Room
            {
                Name = "Room_4_Doors",
                GameObjects = [linkedTemplateInstance]
            };

            var project = new ProjectModel
            {
                Name = "LinkedRoomTemplateFootprintHydration",
                BaseObjects = [baseObject],
                RoomTemplates = [roomTemplate]
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedRoomTemplateFootprintHydration.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedTemplateDoor = loaded!.RoomTemplates
                .SelectMany(template => template.GameObjects)
                .Single();

            Assert.Equal(linkedBaseObjectId, loadedTemplateDoor.LinkedBaseObjectId);
            Assert.Equal(6, loadedTemplateDoor.FootprintWidthCells);
            Assert.Equal(1, loadedTemplateDoor.FootprintHeightCells);
            Assert.Equal("N", loadedTemplateDoor.FootprintOrientation);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_LinkedInstance_HydratesFromDefinitionLocatedInRoomTemplate()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var linkedBaseObjectId = Guid.NewGuid();
            var templateDefinition = new GameObject
            {
                ObjectId = linkedBaseObjectId,
                Name = "Template Door Definition",
                FootprintWidthCells = 4,
                FootprintHeightCells = 2,
                FootprintOrientation = "W"
            };

            var sourceTemplate = new Room
            {
                Name = "SourceTemplate",
                GameObjects = [templateDefinition]
            };

            var linkedInstance = new GameObject
            {
                Name = "Linked Door Instance",
                LinkedBaseObjectId = linkedBaseObjectId,
                LinkActionsToBaseObject = true
            };

            var targetTemplate = new Room
            {
                Name = "TargetTemplate",
                GameObjects = [linkedInstance]
            };

            var project = new ProjectModel
            {
                Name = "LinkedRoomTemplateDefinitionLookup",
                RoomTemplates = [sourceTemplate, targetTemplate]
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedRoomTemplateDefinitionLookup.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedLinked = loaded!.RoomTemplates
                .Single(template => string.Equals(template.Name, "TargetTemplate", StringComparison.Ordinal))
                .GameObjects
                .Single();

            Assert.Equal(linkedBaseObjectId, loadedLinked.LinkedBaseObjectId);
            Assert.Equal(4, loadedLinked.FootprintWidthCells);
            Assert.Equal(2, loadedLinked.FootprintHeightCells);
            Assert.Equal("W", loadedLinked.FootprintOrientation);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsBaseObject_MovementRestrictionDirectionalRule()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var baseObject = new GameObject
            {
                Name = "Base Tile",
                IsMovable = true,
                MovementRestrictions = new ObjectMovementRestrictions
                {
                    FirstStacked = new ObjectMovementRestrictionCategory
                    {
                        N = new ObjectMovementRestrictionRule
                        {
                            MaxDistance = 1,
                            AllowJumpOver = false
                        }
                    }
                }
            };

            var area = new Area { Name = "Floor", BaseObjects = new List<GameObject> { baseObject } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "MovementRestrictionsDirectionalRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "MovementRestrictionsDirectionalRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedBase = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.BaseObjects)
                .Single();

            Assert.NotNull(loadedBase.MovementRestrictions);
            Assert.Equal(1, loadedBase.MovementRestrictions!.FirstStacked.N.MaxDistance);
            Assert.Equal(false, loadedBase.MovementRestrictions.FirstStacked.N.AllowJumpOver);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsObjectAppearanceFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configuredObject = new GameObject
            {
                Name = "Appearance Object",
                ProducerNotes = "appearance-roundtrip",
                ImageRotationDegrees = 42.5,
                PositionX = 10,
                PositionY = 20,
                FootprintWidthCells = 2,
                FootprintHeightCells = 1,
                FootprintOrientation = "N",
                AuthoredRenderOrder = 1205,
                AuthoredBaseHeightInRoom = 3,
                IsHeightPinned = true,
                OccupiedCellIds = ["A.A", "B.A"],
                OccupancyDerivationSourceEcho = "x=10;y=20;anchor=TopLeft;footprint=2x1;orientation=N;cell=40",
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        FullImagePath = @"C:\images\object-main.png",
                        ImageScale = 1.25,
                        IsDefault = true
                    }
                ]
            };

            var room = new Room
            {
                Name = "Visual Room",
                GameObjects = new List<GameObject> { configuredObject }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "ObjectAppearanceRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "ObjectAppearanceRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var savedObjectPath = BuildAuthoringGameObjectFilePath(projectFilePath, configuredObject.ObjectId);
            using (var objectDoc = JsonDocument.Parse(File.ReadAllText(savedObjectPath, Encoding.UTF8)))
            {
                var savedObject = objectDoc.RootElement;
                var savedVariants = savedObject.GetProperty("imageVariants").EnumerateArray().ToArray();
                Assert.Single(savedVariants);
                Assert.Equal(@"default", savedVariants[0].GetProperty("variantName").GetString());
                Assert.Equal(@"C:\images\object-main.png", savedVariants[0].GetProperty("fullImagePath").GetString());
                Assert.Equal(1.25, savedVariants[0].GetProperty("imageScale").GetDouble(), 3);

                Assert.Equal(1205, savedObject.GetProperty("authoredRenderOrder").GetInt32());
                var appearance = savedObject.GetProperty("appearance");
                Assert.Equal(3, appearance.GetProperty("authoredBaseHeightInRoom").GetInt32());
                Assert.True(appearance.GetProperty("isHeightPinned").GetBoolean());
                var occupiedCells = appearance.GetProperty("occupiedCellIds").EnumerateArray().Select(item => item.GetString()).ToArray();
                Assert.Equal(new[] { "A.A", "B.A" }, occupiedCells);
                Assert.Equal("x=10;y=20;anchor=TopLeft;footprint=2x1;orientation=N;cell=40", appearance.GetProperty("occupancyDerivationSourceEcho").GetString());
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedObject = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single(obj => string.Equals(obj.ProducerNotes, "appearance-roundtrip", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(@"C:\images\object-main.png", loadedObject.ResolveDefaultImagePath());
            Assert.Equal(42.5, loadedObject.ImageRotationDegrees, 3);
            Assert.Equal(1.25, loadedObject.ResolveImageScale(null), 3);
            Assert.Equal(1205, loadedObject.AuthoredRenderOrder);
            Assert.Equal(3, loadedObject.AuthoredBaseHeightInRoom);
            Assert.True(loadedObject.IsHeightPinned);
            Assert.Equal(new[] { "A.A", "B.A" }, loadedObject.OccupiedCellIds);
            Assert.Equal("x=10;y=20;anchor=TopLeft;footprint=2x1;orientation=N;cell=40", loadedObject.OccupancyDerivationSourceEcho);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_PassiveSpatialType_IsPersistedAndRoundTrips()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configuredObject = new GameObject
            {
                Name = "Passive Rug",
                ProducerNotes = "passive-spatialtype-roundtrip",
                SpatialType = RuntimeObjectSpatialTypes.PassiveObject,
                StackGroup = 4,
                PositionX = 10,
                PositionY = 20
            };

            var room = new Room
            {
                Name = "Visual Room",
                GameObjects = new List<GameObject> { configuredObject }
            };

            var area = new Area { Name = "Area", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "PassiveSpatialTypeRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "PassiveSpatialTypeRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var savedObjectPath = BuildAuthoringGameObjectFilePath(projectFilePath, configuredObject.ObjectId);
            using (var objectDoc = JsonDocument.Parse(File.ReadAllText(savedObjectPath, Encoding.UTF8)))
            {
                var savedObject = objectDoc.RootElement;
                var appearance = savedObject.GetProperty("appearance");
                Assert.Equal(nameof(RuntimeObjectSpatialTypes.PassiveObject), appearance.GetProperty("spatialType").GetString());
                if (appearance.TryGetProperty("stackGroup", out var persistedStackGroup))
                {
                    Assert.Equal(0, persistedStackGroup.GetInt32());
                }
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedObject = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single(obj => string.Equals(obj.ProducerNotes, "passive-spatialtype-roundtrip", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(RuntimeObjectSpatialTypes.PassiveObject, loadedObject.SpatialType);
            Assert.Equal(0, loadedObject.StackGroup);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RetainsLinkedRoomInstancesInTargetRoom_AfterReload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var baseObjectId = Guid.NewGuid();
            var baseObject = new GameObject
            {
                ObjectId = baseObjectId,
                Name = "Marbles",
                ProducerNotes = "marbles-base",
                IsQuantifiable = true,
                Quantity = 2,
                QuantifiablePlacementDistributionMode = "GroupedStack"
            };

            var linkedCopy = new GameObject
            {
                ObjectId = Guid.NewGuid(),
                Name = "Marbles",
                ProducerNotes = "marbles-linked-room-b",
                IsQuantifiable = true,
                Quantity = 3,
                QuantifiablePlacementDistributionMode = "GroupedStack",
                LinkedBaseObjectId = baseObjectId,
                LinkActionsToBaseObject = true
            };

            var sourceRoom = new Room
            {
                Name = "Source Room",
                GameObjects = new List<GameObject> { baseObject }
            };

            var targetRoom = new Room
            {
                Name = "Target Room",
                GameObjects = new List<GameObject> { linkedCopy }
            };

            var area = new Area
            {
                Name = "Floor",
                Rooms = new List<Room> { sourceRoom, targetRoom }
            };

            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "LinkedRoomRetain",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedRoomRetain.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedTargetRoom = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .Single(r => string.Equals(r.Name, "Target Room", StringComparison.Ordinal));

            var loadedLinkedCopy = loadedTargetRoom.GameObjects
                .Single(obj => string.Equals(obj.ProducerNotes, "marbles-linked-room-b", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(baseObjectId, loadedLinkedCopy.LinkedBaseObjectId);
            Assert.True(loadedLinkedCopy.LinkActionsToBaseObject);
            Assert.True(loadedLinkedCopy.IsRoomInstance);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_StripsLinkedRoomInstanceLocalActions_AfterReload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var baseObjectId = Guid.NewGuid();
            var baseAction = new CommandAction
            {
                Name = "baseAction",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "base",
                Payload = new StoryboardDesigner.App.Models.EchoPayload(),
            };

            var staleLinkedAction = new CommandAction
            {
                Name = "staleLinkedAction",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "stale",
                Payload = new StoryboardDesigner.App.Models.EchoPayload(),
            };

            var baseObject = new GameObject
            {
                ObjectId = baseObjectId,
                Name = "Marbles",
                ProducerNotes = "marbles-base",
                IsQuantifiable = true,
                Quantity = 1,
                QuantifiablePlacementDistributionMode = "GroupedStack",
                AvailableActions = new List<CommandAction> { baseAction }
            };

            var linkedCopy = new GameObject
            {
                ObjectId = Guid.NewGuid(),
                Name = "Marbles",
                ProducerNotes = "marbles-linked-room-b",
                IsQuantifiable = true,
                Quantity = 3,
                QuantifiablePlacementDistributionMode = "GroupedStack",
                LinkedBaseObjectId = baseObjectId,
                LinkActionsToBaseObject = true,
                AvailableActions = new List<CommandAction> { staleLinkedAction }
            };

            var sourceRoom = new Room
            {
                Name = "Source Room",
                GameObjects = new List<GameObject> { baseObject }
            };

            var targetRoom = new Room
            {
                Name = "Target Room",
                GameObjects = new List<GameObject> { linkedCopy }
            };

            var area = new Area
            {
                Name = "Floor",
                Rooms = new List<Room> { sourceRoom, targetRoom }
            };

            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "LinkedRoomStripActions",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "LinkedRoomStripActions.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedSourceRoom = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .Single(r => string.Equals(r.Name, "Source Room", StringComparison.Ordinal));

            var loadedTargetRoom = loaded.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .Single(r => string.Equals(r.Name, "Target Room", StringComparison.Ordinal));

            var loadedBase = loadedSourceRoom.GameObjects
                .Single(obj => string.Equals(obj.ProducerNotes, "marbles-base", StringComparison.OrdinalIgnoreCase));
            var loadedLinked = loadedTargetRoom.GameObjects
                .Single(obj => string.Equals(obj.ProducerNotes, "marbles-linked-room-b", StringComparison.OrdinalIgnoreCase));

            Assert.Single(loadedBase.AvailableActions);
            Assert.Empty(loadedLinked.AvailableActions);
            Assert.True(loadedLinked.LinkActionsToBaseObject);
            Assert.Equal(baseObjectId, loadedLinked.LinkedBaseObjectId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RetainsContainedIndividualInstances_AsOneBasePlusLinkedSiblings_AfterReload()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var containedBaseId = Guid.NewGuid();
            var containedBase = new GameObject
            {
                ObjectId = containedBaseId,
                Name = "Coin",
                ProducerNotes = "coin-contained-base",
                IsQuantifiable = true,
                Quantity = 1,
                QuantifiablePlacementDistributionMode = "IndividualInstances",
                LinkedBaseObjectId = null,
                LinkActionsToBaseObject = false
            };

            var linkedContainedOne = new GameObject
            {
                ObjectId = Guid.NewGuid(),
                Name = "Coin",
                ProducerNotes = "coin-contained-linked-1",
                IsQuantifiable = true,
                Quantity = 1,
                QuantifiablePlacementDistributionMode = "IndividualInstances",
                LinkedBaseObjectId = containedBaseId,
                LinkActionsToBaseObject = true
            };

            var linkedContainedTwo = new GameObject
            {
                ObjectId = Guid.NewGuid(),
                Name = "Coin",
                ProducerNotes = "coin-contained-linked-2",
                IsQuantifiable = true,
                Quantity = 1,
                QuantifiablePlacementDistributionMode = "IndividualInstances",
                LinkedBaseObjectId = containedBaseId,
                LinkActionsToBaseObject = true
            };

            var container = new GameObject
            {
                Name = "Pouch",
                ProducerNotes = "coin-container",
                IsContainer = true,
                ContainedObjects = new List<GameObject>
                {
                    containedBase,
                    linkedContainedOne,
                    linkedContainedTwo
                }
            };

            var room = new Room
            {
                Name = "Vault",
                GameObjects = new List<GameObject> { container }
            };

            var area = new Area { Name = "Floor", Rooms = new List<Room> { room } };
            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "ContainedIndividualRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "ContainedIndividualRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedContainer = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single(obj => string.Equals(obj.ProducerNotes, "coin-container", StringComparison.OrdinalIgnoreCase));

            var loadedContainedBase = loadedContainer.ContainedObjects
                .Single(obj => string.Equals(obj.ProducerNotes, "coin-contained-base", StringComparison.OrdinalIgnoreCase));
            var loadedLinkedContained = loadedContainer.ContainedObjects
                .Where(obj => string.Equals(obj.Name, "Coin", StringComparison.OrdinalIgnoreCase)
                              && obj.ProducerNotes.StartsWith("coin-contained-linked", StringComparison.OrdinalIgnoreCase))
                .OrderBy(obj => obj.ProducerNotes, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.Equal(2, loadedLinkedContained.Count);

            Assert.Null(loadedContainedBase.LinkedBaseObjectId);
            Assert.False(loadedContainedBase.LinkActionsToBaseObject);
            Assert.False(loadedContainedBase.IsRoomInstance);
            Assert.Equal(1, loadedContainedBase.Quantity);
            Assert.Equal("IndividualInstances", loadedContainedBase.QuantifiablePlacementDistributionMode);

            Assert.All(loadedLinkedContained, linked =>
            {
                Assert.Equal(containedBaseId, linked.LinkedBaseObjectId);
                Assert.True(linked.LinkActionsToBaseObject);
                Assert.True(linked.IsRoomInstance);
                Assert.Equal(1, linked.Quantity);
                Assert.Equal("IndividualInstances", linked.QuantifiablePlacementDistributionMode);
            });

            Assert.Equal(1, loadedContainer.ContainedObjects.Count(obj => !obj.IsRoomInstance && string.Equals(obj.Name, "Coin", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsTraversalScaffolding_UsingCanonicalTraversalConnectionsOnly()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room
            {
                Name = "RoomA",
                TraversalModeOverride = AreaAdjacencyMode.EightDirectional
            };

            var roomB = new Room
            {
                Name = "RoomB"
            };

            var area = new Area
            {
                Name = "TraversalArea",
                AdjacencyMode = AreaAdjacencyMode.FourDirectional,
                TraversalModeOverride = AreaAdjacencyMode.FourDirectional,
                Rooms = new List<Room> { roomA, roomB },
                TraversalConnections = new List<TraversalConnection>
                {
                    new()
                    {
                        TraversalConnectionId = Guid.NewGuid(),
                        RoomAId = roomA.Id,
                        RoomBId = roomB.Id,
                        BaseTraversalDirectionFromA = Direction10.NorthEast,
                        TraversalModeOverride = AreaAdjacencyMode.EightDirectional,
                        TraversalAccessMode = TraversalAccessMode.OneWayAtoB
                    }
                }
            };

            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };

            var project = new ProjectModel
            {
                Name = "TraversalScaffoldRoundTrip",
                DefaultTraversalMode = AreaAdjacencyMode.FourDirectional,
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "TraversalScaffoldRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal(AreaAdjacencyMode.FourDirectional, loaded!.DefaultTraversalMode);

            var loadedArea = loaded.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .Single(a => string.Equals(a.Name, "TraversalArea", StringComparison.Ordinal));

            Assert.Equal(AreaAdjacencyMode.FourDirectional, loadedArea.TraversalModeOverride);
            Assert.Empty(loadedArea.Links);

            Assert.Single(loadedArea.TraversalConnections);
            var connection = loadedArea.TraversalConnections[0];
            Assert.Equal(roomA.Id, connection.RoomAId);
            Assert.Equal(roomB.Id, connection.RoomBId);
            Assert.Equal(TraversalAccessMode.OneWayAtoB, connection.TraversalAccessMode);
            Assert.Equal(Direction10.NorthEast, connection.BaseTraversalDirectionFromA);
            Assert.Equal(AreaAdjacencyMode.EightDirectional, connection.TraversalModeOverride);

            var loadedRoomA = loadedArea.Rooms.Single(r => string.Equals(r.Name, "RoomA", StringComparison.Ordinal));
            Assert.Equal(AreaAdjacencyMode.EightDirectional, loadedRoomA.TraversalModeOverride);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsAreaRoomPlacementFloorElevation_WithLegacyDefaultCompatibility()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomA = new Room { Name = "RoomA" };
            var roomB = new Room { Name = "RoomB" };
            var area = new Area
            {
                Name = "AreaWithFloors",
                Rooms = new List<Room> { roomA, roomB },
                RoomPlacements = new List<AreaRoomPlacement>
                {
                    new() { RoomId = roomA.Id, X = 40, Y = 80, FloorElevation = 0 },
                    new() { RoomId = roomB.Id, X = 140, Y = 80, FloorElevation = 3 }
                }
            };

            var country = new Country { Name = "Country", Areas = new List<Area> { area } };
            var planet = new Planet { Name = "Planet", Countries = new List<Country> { country } };
            var project = new ProjectModel
            {
                Name = "AreaRoomPlacementFloorRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "AreaRoomPlacementFloorRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var areaFilePath = Directory.GetFiles(BuildAreasFolderPath(projectFilePath), "*.area.json", SearchOption.TopDirectoryOnly)
                .Single();

            using (var areaDoc = JsonDocument.Parse(File.ReadAllText(areaFilePath, Encoding.UTF8)))
            {
                var roomPlacements = areaDoc.RootElement.GetProperty("roomPlacements");
                var placementsByRoomId = roomPlacements.EnumerateArray()
                    .ToDictionary(
                        p => p.GetProperty("roomId").GetGuid(),
                        p => p);

                Assert.Equal(2, placementsByRoomId.Count);
                Assert.False(placementsByRoomId[roomA.Id].TryGetProperty("floorElevation", out _));
                Assert.Equal(3, placementsByRoomId[roomB.Id].GetProperty("floorElevation").GetInt32());
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedArea = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .Single(a => string.Equals(a.Name, "AreaWithFloors", StringComparison.Ordinal));

            var loadedPlacementsByRoomId = loadedArea.RoomPlacements.ToDictionary(placement => placement.RoomId);
            Assert.Equal(0, loadedPlacementsByRoomId[roomA.Id].FloorElevation);
            Assert.Equal(3, loadedPlacementsByRoomId[roomB.Id].FloorElevation);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsDirectionalTraversalMappings_AcrossScopes()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var roomObject = new GameObject
            {
                Name = "Door Object",
                AdditionalDirectionals = new List<string> { "thru" },
                AdditionalDirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "thru", TraversalDirection = Direction10.East }
                }
            };

            var room = new Room
            {
                Name = "Room",
                AdditionalDirectionals = new List<string> { "forward" },
                AdditionalDirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "forward", TraversalDirection = Direction10.North }
                },
                GameObjects = new List<GameObject> { roomObject }
            };

            var area = new Area
            {
                Name = "Area",
                AdditionalDirectionals = new List<string> { "into" },
                AdditionalDirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "into", TraversalDirection = Direction10.South }
                },
                Rooms = new List<Room> { room }
            };

            var country = new Country
            {
                Name = "Country",
                AdditionalDirectionals = new List<string> { "portal" },
                AdditionalDirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "portal", TraversalDirection = Direction10.West }
                },
                Areas = new List<Area> { area }
            };

            var planet = new Planet
            {
                Name = "Planet",
                AdditionalDirectionals = new List<string> { "ascend" },
                AdditionalDirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "ascend", TraversalDirection = Direction10.NorthEast }
                },
                Countries = new List<Country> { country }
            };

            var project = new ProjectModel
            {
                Name = "DirectionalMappingRoundTrip",
                Directionals = new List<string> { "north", "forward" },
                DirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "north", TraversalDirection = Direction10.North },
                    new() { Token = "forward", TraversalDirection = Direction10.North }
                },
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "DirectionalMappingRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            AssertMapping(loaded!.DirectionalTraversalMappings, "north", Direction10.North);
            AssertMapping(loaded.DirectionalTraversalMappings, "forward", Direction10.North);

            var loadedPlanet = Assert.Single(loaded.Planets);
            AssertMapping(loadedPlanet.AdditionalDirectionalTraversalMappings, "ascend", Direction10.NorthEast);

            var loadedCountry = Assert.Single(loadedPlanet.Countries);
            AssertMapping(loadedCountry.AdditionalDirectionalTraversalMappings, "portal", Direction10.West);

            var loadedArea = Assert.Single(loadedCountry.Areas);
            AssertMapping(loadedArea.AdditionalDirectionalTraversalMappings, "into", Direction10.South);

            var loadedRoom = Assert.Single(loadedArea.Rooms);
            AssertMapping(loadedRoom.AdditionalDirectionalTraversalMappings, "forward", Direction10.North);

            var loadedObject = Assert.Single(loadedRoom.GameObjects);
            AssertMapping(loadedObject.AdditionalDirectionalTraversalMappings, "thru", Direction10.East);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_WritesGlobalsToSidecar_AndTryLoadPrefersGlobalsSidecarValues()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalsSidecar",
                CommandVerbs = new List<string> { "look" },
                Directionals = new List<string> { "north" },
                DirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "north", TraversalDirection = Direction10.North }
                },
                ObjectTemplates = new List<GameObject>
                {
                    new() { Name = "Template Lantern", ProducerNotes = "Template Lantern" }
                },
                RoomTemplates = new List<Room>
                {
                    new() { Name = "Template Room Alpha", Description = "Room template" }
                },
                GlobalObjectScopeName = "Global Objects",
                GameObjects = new List<GameObject>
                {
                    new() { Name = "Global Lantern", ProducerNotes = "Global Lantern" }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalsSidecar.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var globalsPath = BuildProjectGlobalsFilePath(projectFilePath);
            Assert.True(File.Exists(globalsPath));

            using (var globalsDoc = JsonDocument.Parse(File.ReadAllText(globalsPath, Encoding.UTF8)))
            {
                Assert.Equal("global", globalsDoc.RootElement.GetProperty("scopeKind").GetString(), ignoreCase: true);
            }

            var projectJson = File.ReadAllText(projectFilePath, Encoding.UTF8);
            projectJson = projectJson.Replace("\"look\"", "\"inspect\"", StringComparison.Ordinal);
            projectJson = projectJson.Replace("\"north\"", "\"east\"", StringComparison.Ordinal);
            File.WriteAllText(projectFilePath, projectJson, Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Contains(loaded!.CommandVerbs, verb => string.Equals(verb, "look", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.Directionals, directional => string.Equals(directional, "north", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.ObjectTemplates, obj => string.Equals(obj.Name, "Template Lantern", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.RoomTemplates, room => string.Equals(room.Name, "Template Room Alpha", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.GameObjects, obj => string.Equals(obj.Name, "Global Lantern", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_DoesNotFallbackToRootGlobalOwnedField_WhenGlobalNodeExistsButFieldIsMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalNodeAuthoritative",
                CommandVerbs = new List<string> { "look" },
                Directionals = new List<string> { "north" }
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalNodeAuthoritative.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var projectRoot = JsonNode.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8))!.AsObject();
            projectRoot["commandVerbs"] = new JsonArray("inspect");
            File.WriteAllText(projectFilePath, projectRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var globalNodePath = BuildProjectGlobalsFilePath(projectFilePath);
            var globalNodeRoot = JsonNode.Parse(File.ReadAllText(globalNodePath, Encoding.UTF8))!.AsObject();
            globalNodeRoot.Remove("additionalVerbs");
            File.WriteAllText(globalNodePath, globalNodeRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Empty(loaded!.CommandVerbs);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_DoesNotFallbackToRootForNonTopologyGlobalFields_WhenGlobalNodeExistsButFieldsAreMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var procedureId = Guid.NewGuid();
            var sharedId = Guid.NewGuid();
            var actionId = Guid.NewGuid();

            var project = new ProjectModel
            {
                Name = "GlobalNodeAuthoritativeManyFields",
                CommandVerbs = new List<string> { "look" },
                Directionals = new List<string> { "north" },
                DirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "north", TraversalDirection = Direction10.North }
                },
                ProcedureIds = [procedureId],
                SharedVariables =
                [
                    new SharedVariableDefinition
                    {
                        Id = sharedId,
                        Name = "DoorState"
                    }
                ],
                GlobalObjectAvailableActions =
                [
                    new CommandAction
                    {
                        Id = actionId,
                        Name = "Global Look",
                        ActionType = CommandActionType.EchoMessage,
                        Verbs = ["look"],
                        EchoMessage = "look",
                        Payload = new StoryboardDesigner.App.Models.EchoPayload()
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalNodeAuthoritativeManyFields.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var projectRoot = JsonNode.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8))!.AsObject();
            projectRoot["directionals"] = new JsonArray("east");
            projectRoot["procedureIds"] = new JsonArray(procedureId.ToString("D"));
            projectRoot["sharedVariables"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = sharedId.ToString("D"),
                    ["name"] = "LegacyShared"
                }
            };
            projectRoot["availableGameActions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = actionId.ToString("D"),
                    ["name"] = "Legacy Action",
                    ["actionType"] = "EchoMessage",
                    ["verbs"] = new JsonArray("inspect"),
                    ["echoMessage"] = "legacy"
                }
            };
            File.WriteAllText(projectFilePath, projectRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var globalNodePath = BuildProjectGlobalsFilePath(projectFilePath);
            var globalNodeRoot = JsonNode.Parse(File.ReadAllText(globalNodePath, Encoding.UTF8))!.AsObject();
            globalNodeRoot.Remove("additionalDirectionals");
            globalNodeRoot.Remove("procedureIds");
            globalNodeRoot.Remove("sharedVariables");
            globalNodeRoot.Remove("availableGameActions");
            File.WriteAllText(globalNodePath, globalNodeRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Empty(loaded!.Directionals);
            Assert.Empty(loaded.ProcedureIds);
            Assert.Empty(loaded.SharedVariables);
            Assert.Empty(loaded.GlobalObjectAvailableActions);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_DoesNotFallbackToRootPlanetIds_WhenGlobalNodeExistsButPlanetIdsMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalNodeAuthoritativePlanetIds",
                Planets =
                [
                    new Planet
                    {
                        Name = "Planet One"
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalNodeAuthoritativePlanetIds.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var projectRoot = JsonNode.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8))!.AsObject();
            var sourcePlanetId = project.Planets.Single().Id;
            projectRoot["planetIds"] = new JsonArray(sourcePlanetId.ToString("D"));
            File.WriteAllText(projectFilePath, projectRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var globalNodePath = BuildProjectGlobalsFilePath(projectFilePath);
            var globalNodeRoot = JsonNode.Parse(File.ReadAllText(globalNodePath, Encoding.UTF8))!.AsObject();
            globalNodeRoot.Remove("planetIds");
            File.WriteAllText(globalNodePath, globalNodeRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Empty(loaded!.Planets);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_WritesProceduresToSidecars_AndRoundTripsScopeOwnership()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var globalProcedureId = Guid.NewGuid();
            var objectProcedureId = Guid.NewGuid();

            var project = new ProjectModel
            {
                Name = "ProcedureSidecarRoundTrip",
                ProcedureIds = [globalProcedureId],
                Procedures =
                [
                    new ProcedureDefinition
                    {
                        Id = globalProcedureId,
                        Name = "Global Procedure",
                        ProcedureSummary = "Global procedure summary"
                    },
                    new ProcedureDefinition
                    {
                        Id = objectProcedureId,
                        Name = "Object Procedure",
                        ProcedureSummary = "Object procedure summary"
                    }
                ],
                GlobalObjectScopeName = "Global Objects",
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Owner Object",
                        ProcedureIds = [objectProcedureId]
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "ProcedureSidecarRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var proceduresFolderPath = BuildProceduresFolderPath(projectFilePath);
            Assert.True(Directory.Exists(proceduresFolderPath));

            var procedureFiles = Directory
                .GetFiles(proceduresFolderPath, "*.procedure.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToList();
            Assert.Equal(2, procedureFiles.Count);
            Assert.Contains($"{globalProcedureId:N}.procedure.json", procedureFiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains($"{objectProcedureId:N}.procedure.json", procedureFiles, StringComparer.OrdinalIgnoreCase);

            var projectJson = File.ReadAllText(projectFilePath, Encoding.UTF8);
            Assert.DoesNotContain("Global procedure summary", projectJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Object procedure summary", projectJson, StringComparison.OrdinalIgnoreCase);

            using (var projectDoc = JsonDocument.Parse(projectJson))
            {
                Assert.False(projectDoc.RootElement.TryGetProperty("procedureIds", out _));
            }

            using (var globalNodeDoc = JsonDocument.Parse(File.ReadAllText(BuildProjectGlobalsFilePath(projectFilePath), Encoding.UTF8)))
            {
                var sidecarProcedureIds = globalNodeDoc.RootElement.GetProperty("procedureIds");
                Assert.Equal(JsonValueKind.Array, sidecarProcedureIds.ValueKind);
                Assert.Contains(sidecarProcedureIds.EnumerateArray(), value => value.GetGuid() == globalProcedureId);
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            Assert.Contains(globalProcedureId, loaded!.ProcedureIds);
            Assert.Equal(2, loaded.Procedures.Count);
            Assert.Contains(loaded.Procedures, procedure => procedure.Id == globalProcedureId);
            Assert.Contains(loaded.Procedures, procedure => procedure.Id == objectProcedureId);

            var loadedOwnerObject = Assert.Single(loaded.GameObjects);
            Assert.Contains(objectProcedureId, loadedOwnerObject.ProcedureIds);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_DoesNotFallbackToInlineProcedures_WhenProcedureSidecarsMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var inlineProcedureId = Guid.NewGuid();
            var project = new ProjectModel
            {
                Name = "ProcedureInlineFallback",
                Procedures =
                [
                    new ProcedureDefinition
                    {
                        Id = inlineProcedureId,
                        Name = "Inline Procedure",
                        ProcedureSummary = "inline-fallback"
                    }
                ],
                GlobalObjectScopeName = "Global Objects",
                GameObjects =
                [
                    new GameObject
                    {
                        Name = "Owner Object",
                        ProcedureIds = [inlineProcedureId]
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "ProcedureInlineFallback.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var projectJsonNode = JsonNode.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8))?.AsObject();
            Assert.NotNull(projectJsonNode);

            projectJsonNode!.Remove("procedureIds");
            projectJsonNode["procedures"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = inlineProcedureId,
                    ["name"] = "Inline Procedure",
                    ["procedureSummary"] = "inline-fallback",
                    ["procedureDescription"] = "",
                    ["participants"] = new JsonArray(),
                    ["participantMutations"] = new JsonArray()
                }
            };

            File.WriteAllText(projectFilePath, projectJsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            var proceduresFolderPath = BuildProceduresFolderPath(projectFilePath);
            if (Directory.Exists(proceduresFolderPath))
            {
                Directory.Delete(proceduresFolderPath, recursive: true);
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            Assert.Empty(loaded!.Procedures);
            Assert.DoesNotContain(inlineProcedureId, loaded.ProcedureIds);

            var loadedOwnerObject = Assert.Single(loaded.GameObjects);
            Assert.Contains(inlineProcedureId, loadedOwnerObject.ProcedureIds);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_PreservesPlayerSelectionFromGlobalsSidecar_WhenLegacyPlayerPayloadFieldsAreEmpty()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var playerAction = new CommandAction
            {
                Name = "Inventory",
                ActionType = CommandActionType.EchoMessage,
                EchoMessage = "You check your pockets.",
                Payload = new StoryboardDesigner.App.Models.EchoPayload(),
            };

            var project = new ProjectModel
            {
                Name = "MigratedPlayerSidecar",
                PlayerCharacterObjectName = "Hero",
                GlobalObjectScopeName = "Global Objects",
                GameObjects = new List<GameObject>
                {
                    new()
                    {
                        Name = "Hero",
                        ProducerNotes = "primary player object",
                        Variables = new List<GamePropertyDefinition>
                        {
                            new()
                            {
                                Name = "isPlayer",
                                DefaultValue = "true",
                                Lifetime = GamePropertyLifetime.Singleton,
                                ValueRestriction = GamePropertyValueRestriction.TrueFalse
                            }
                        },
                        AvailableActions = new List<CommandAction> { playerAction }
                    }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "MigratedPlayerSidecar.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using (var projectDoc = JsonDocument.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8)))
            {
                Assert.False(projectDoc.RootElement.TryGetProperty("playerGameProperties", out _));
                Assert.False(projectDoc.RootElement.TryGetProperty("availableGameActions", out _));
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);
            Assert.Equal("Hero", loaded!.PlayerCharacterObjectName);

            var loadedPlayerObject = Assert.Single(loaded.GameObjects);
            Assert.Equal("Hero", loadedPlayerObject.Name);
            Assert.Contains(loadedPlayerObject.Variables, variable =>
                string.Equals(variable.Name, "isPlayer", StringComparison.OrdinalIgnoreCase)
                && string.Equals(variable.DefaultValue, "true", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loadedPlayerObject.AvailableActions, action =>
                string.Equals(action.Name, "Inventory", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_ReturnsNull_WhenGlobalsSidecarIsMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
                        var projectFilePath = Path.Combine(tempRoot, "LegacyGlobals.sbe.json");
                        var globalProcedureId = Guid.NewGuid();
                        var sharedVariableId = Guid.NewGuid();
                        var json = $$"""
            {
              "name": "LegacyGlobals",
              "autoSaveSeconds": 60,
              "defaultTraversalMode": "EightDirectional",
              "commandVerbs": ["inspect"],
              "directionals": ["east"],
              "directionalTraversalMappings": [
                {
                  "token": "east",
                  "traversalDirection": "East"
                }
              ],
              "gameProperties": [],
              "playerGameProperties": [],
              "availableGameActions": [],
              "gameObjects": [],
              "playerGameObjects": [],
              "objectTemplates": [],
                            "procedureIds": ["{{globalProcedureId}}"],
                            "sharedVariables": [
                                {
                                    "id": "{{sharedVariableId}}",
                                    "name": "LegacyShared"
                                }
                            ],
              "planetIds": []
            }
            """;

            File.WriteAllText(projectFilePath, json, Encoding.UTF8);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.Null(loaded);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadProjectModel_PersistsGlobalScopedActions_InGlobalsSidecar()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var globalAction = new CommandAction
            {
                Name = "Global Examine",
                ActionType = CommandActionType.EchoMessage,
                Verbs = ["examine"],
                EchoMessage = "global examine",
                Payload = new StoryboardDesigner.App.Models.EchoPayload()
            };

            var project = new ProjectModel
            {
                Name = "GlobalActionsRoundTrip",
                CommandVerbs = ["examine"],
                GlobalObjectScopeName = "Global Objects",
                GlobalObjectAvailableActions = [globalAction],
                GameObjects = new List<GameObject>()
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalActionsRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using (var sidecarDoc = JsonDocument.Parse(File.ReadAllText(BuildProjectGlobalsFilePath(projectFilePath), Encoding.UTF8)))
            {
                var actions = sidecarDoc.RootElement.GetProperty("availableGameActions");
                Assert.Equal(1, actions.GetArrayLength());
                Assert.Equal("Global Examine", actions[0].GetProperty("name").GetString());
            }

            using (var projectDoc = JsonDocument.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8)))
            {
                Assert.False(projectDoc.RootElement.TryGetProperty("availableGameActions", out _));
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);

            Assert.NotNull(loaded);
            var loadedAction = Assert.Single(loaded!.GlobalObjectAvailableActions);
            Assert.Equal("Global Examine", loadedAction.Name);
            Assert.Equal(CommandActionType.EchoMessage, loadedAction.ActionType);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadProjectModel_KeepsGlobalScopedAndGlobalObjectActionsDistinct_WhenNamesMatch()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var globalScopeAction = new CommandAction
            {
                Name = "Inspect",
                ActionType = CommandActionType.EchoMessage,
                Verbs = ["inspect"],
                EchoMessage = "global-scope",
                Payload = new StoryboardDesigner.App.Models.EchoPayload()
            };

            var globalObjectAction = new CommandAction
            {
                Name = "Inspect",
                ActionType = CommandActionType.EchoMessage,
                Verbs = ["inspect"],
                EchoMessage = "global-object",
                Payload = new StoryboardDesigner.App.Models.EchoPayload()
            };

            var globalObject = new GameObject
            {
                Name = "Pack",
                AvailableActions = [globalObjectAction]
            };

            var project = new ProjectModel
            {
                Name = "GlobalActionSeparationRoundTrip",
                CommandVerbs = ["inspect"],
                GlobalObjectAvailableActions = [globalScopeAction],
                GameObjects = [globalObject]
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalActionSeparationRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);

            Assert.NotNull(loaded);

            var loadedGlobalScopeAction = Assert.Single(loaded!.GlobalObjectAvailableActions);
            var loadedGlobalObject = Assert.Single(loaded.GameObjects);
            var loadedGlobalObjectAction = Assert.Single(loadedGlobalObject.AvailableActions);

            Assert.Equal("Inspect", loadedGlobalScopeAction.Name);
            Assert.Equal("global-scope", loadedGlobalScopeAction.EchoMessage);

            Assert.Equal("Inspect", loadedGlobalObjectAction.Name);
            Assert.Equal("global-object", loadedGlobalObjectAction.EchoMessage);

            Assert.NotEqual(loadedGlobalScopeAction.Id, loadedGlobalObjectAction.Id);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

        [Fact]
        public void TryLoadProjectModel_ReturnsNullForLegacyProjectFileAvailableGameActions_WhenGlobalsSidecarIsMissing()
        {
                var service = new JsonExportService();
                var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                        var projectFilePath = Path.Combine(tempRoot, "LegacyGlobalActions.sbe.json");
                        var json = """
                        {
                            "name": "LegacyGlobalActions",
                            "autoSaveSeconds": 60,
                            "defaultTraversalMode": "EightDirectional",
                            "commandVerbs": ["inspect"],
                            "directionals": [],
                            "directionalTraversalMappings": [],
                            "gameProperties": [],
                            "availableGameActions": [
                                {
                                    "name": "Legacy Inspect",
                                    "actionType": "EchoMessage",
                                    "verbs": ["inspect"],
                                    "echoMessage": "legacy-action"
                                }
                            ],
                            "gameObjects": [],
                            "objectTemplates": [],
                            "sharedVariables": [],
                            "planetIds": []
                        }
                        """;

                        File.WriteAllText(projectFilePath, json, Encoding.UTF8);

                        var loaded = service.TryLoadProjectModel(projectFilePath);
                        Assert.Null(loaded);
                }
                finally
                {
                        if (Directory.Exists(tempRoot))
                        {
                                Directory.Delete(tempRoot, recursive: true);
                        }
                }
        }

        [Fact]
        public void TryLoadProjectModel_ReturnsNullForLegacyInlinePlayerPayload_WhenGlobalsSidecarIsMissing()
        {
                var service = new JsonExportService();
                var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                        var projectFilePath = Path.Combine(tempRoot, "LegacyInlinePlayerPayload.sbe.json");
                        var json = """
                        {
                            "name": "LegacyInlinePlayerPayload",
                            "autoSaveSeconds": 60,
                            "defaultTraversalMode": "EightDirectional",
                            "commandVerbs": [],
                            "directionals": [],
                            "directionalTraversalMappings": [],
                            "gameProperties": [],
                            "playerGameProperties": [
                                {
                                    "name": "isPlayer",
                                    "defaultValue": "true",
                                    "lifetime": "Singleton",
                                    "valueRestriction": "TrueFalse"
                                }
                            ],
                            "availableGameActions": [
                                {
                                    "name": "Inventory",
                                    "actionType": "EchoMessage",
                                    "echoMessage": "legacy-inline"
                                }
                            ],
                            "gameObjects": [],
                            "playerGameObjects": [],
                            "objectTemplates": [],
                            "sharedVariables": [],
                            "planetIds": []
                        }
                        """;

                        File.WriteAllText(projectFilePath, json, Encoding.UTF8);

                        var loaded = service.TryLoadProjectModel(projectFilePath);
                        Assert.Null(loaded);
                }
                finally
                {
                        if (Directory.Exists(tempRoot))
                        {
                                Directory.Delete(tempRoot, recursive: true);
                        }
                }
        }

    [Fact]
    public void SaveProjectModel_NormalizesSoundEffectRepeatFields_ByRepeatMode()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Room A",
                SoundEffectLibraryEntries =
                [
                    new SoundEffectLibraryEntry
                    {
                        SoundEffectId = Guid.NewGuid(),
                        SoundEffectKey = "room-loop",
                        DisplayName = "Room Loop",
                        AssetRef = "room-loop.wav",
                        RepeatMode = "RepeatForDuration",
                        RepeatCount = 9,
                        RepeatDurationMs = 2500,
                        RepeatIntervalMs = 180,
                        RepeatCooldownMs = 400
                    }
                ]
            };

            var area = new Area { Name = "Area A", Rooms = [room] };
            var country = new Country { Name = "Country A", Areas = [area] };
            var planet = new Planet { Name = "Planet A", Countries = [country] };

            var project = new ProjectModel
            {
                Name = "SoundRepeatNormalization",
                Planets = [planet]
            };

            project.GlobalScope.SoundEffectLibraryEntries.Add(new SoundEffectLibraryEntry
            {
                SoundEffectId = Guid.NewGuid(),
                SoundEffectKey = "global-once",
                DisplayName = "Global Once",
                AssetRef = "global-once.wav",
                RepeatMode = "None",
                RepeatCount = 3,
                RepeatDurationMs = 9000,
                RepeatIntervalMs = 220,
                RepeatCooldownMs = 350
            });

            var projectFilePath = Path.Combine(tempRoot, "SoundRepeatNormalization.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using (var globalNodeDoc = JsonDocument.Parse(File.ReadAllText(BuildProjectGlobalsFilePath(projectFilePath), Encoding.UTF8)))
            {
                var globalEntry = globalNodeDoc.RootElement
                    .GetProperty("soundEffectLibraryEntries")
                    .EnumerateArray()
                    .Single();

                Assert.Equal("None", globalEntry.GetProperty("repeatMode").GetString());
                Assert.False(globalEntry.TryGetProperty("repeatCount", out _));
                Assert.False(globalEntry.TryGetProperty("repeatDurationMs", out _));
                Assert.False(globalEntry.TryGetProperty("repeatIntervalMs", out _));
                Assert.False(globalEntry.TryGetProperty("repeatCooldownMs", out _));
            }

            var roomFilePath = Path.Combine(BuildRoomsFolderPath(projectFilePath), $"{room.Id:N}.room.json");
            using (var roomDoc = JsonDocument.Parse(File.ReadAllText(roomFilePath, Encoding.UTF8)))
            {
                var roomEntry = roomDoc.RootElement
                    .GetProperty("soundEffectLibraryEntries")
                    .EnumerateArray()
                    .Single();

                Assert.Equal("RepeatForDuration", roomEntry.GetProperty("repeatMode").GetString());
                Assert.False(roomEntry.TryGetProperty("repeatCount", out _));
                Assert.Equal(2500, roomEntry.GetProperty("repeatDurationMs").GetInt32());
                Assert.Equal(180, roomEntry.GetProperty("repeatIntervalMs").GetInt32());
                Assert.Equal(400, roomEntry.GetProperty("repeatCooldownMs").GetInt32());
            }

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedGlobalEntry = Assert.Single(loaded!.GlobalScope.SoundEffectLibraryEntries);
            Assert.Equal("None", loadedGlobalEntry.RepeatMode);
            Assert.Null(loadedGlobalEntry.RepeatCount);
            Assert.Null(loadedGlobalEntry.RepeatDurationMs);
            Assert.Null(loadedGlobalEntry.RepeatIntervalMs);
            Assert.Null(loadedGlobalEntry.RepeatCooldownMs);

            var loadedRoom = Assert.Single(loaded.Planets[0].Countries[0].Areas[0].Rooms);
            var loadedRoomEntry = Assert.Single(loadedRoom.SoundEffectLibraryEntries);
            Assert.Equal("RepeatForDuration", loadedRoomEntry.RepeatMode);
            Assert.Null(loadedRoomEntry.RepeatCount);
            Assert.Equal(2500, loadedRoomEntry.RepeatDurationMs);
            Assert.Equal(180, loadedRoomEntry.RepeatIntervalMs);
            Assert.Equal(400, loadedRoomEntry.RepeatCooldownMs);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadGlobalsForImport_ReadsGlobalsSidecarOnly()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalsOnlySource",
                CommandVerbs = new List<string> { "look" },
                Directionals = new List<string> { "north" },
                DirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "north", TraversalDirection = Direction10.North }
                },
                EventSubscriptions = new List<EventSubscriptionDefinition>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventKey = "OnGlobalPing",
                        SubscriptionName = "global-ping",
                        ActionBindings =
                        [
                            new EventActionBindingDefinition
                            {
                                Order = 1,
                                Target = new EventBindingTargetDefinition
                                {
                                    ActionName = "look"
                                }
                            }
                        ]
                    }
                },
                TimerDefinitions = new List<RuntimeTimerDefinitionDto>
                {
                    new()
                    {
                        TimerKey = "global.timer",
                        ScheduleAfterMs = 1000,
                        FireMode = TimerFireMode.OneShot,
                        TargetActionRef = "look",
                        LifetimeOwnerType = TimerOwnerType.Room,
                        ConflictBehavior = TimerConflictBehavior.ReplaceExisting,
                        Enabled = true
                    }
                },
                ObjectTemplates = new List<GameObject>
                {
                    new() { Name = "Template Crate", ProducerNotes = "Template Crate" }
                },
                BaseObjects = new List<GameObject>
                {
                    new() { Name = "Base Crate", ProducerNotes = "Base Crate" }
                },
                RoomTemplates = new List<Room>
                {
                    new() { Name = "Template Room Beta", Description = "Room template" }
                },
                GlobalObjectScopeName = "Global Objects",
                GlobalScopeSoundEffectLibraryEntries = new List<SoundEffectLibraryEntry>
                {
                    new()
                    {
                        SoundEffectId = Guid.NewGuid(),
                        SoundEffectKey = "sfx.global.ping",
                        DisplayName = "Global Ping",
                        AssetRef = "audio/sfx/global-ping.wav"
                    }
                },
                GameObjects = new List<GameObject>
                {
                    new() { Name = "Global Lantern", ProducerNotes = "Global Lantern" }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalsOnlySource.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var globalsFilePath = BuildProjectGlobalsFilePath(projectFilePath);
            var loaded = service.TryLoadGlobalNodeForImport(globalsFilePath);
            Assert.NotNull(loaded);
            Assert.Contains("look", loaded!.CommandVerbs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("north", loaded.Directionals, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(loaded.DirectionalTraversalMappings, mapping =>
                string.Equals(mapping.Token, "north", StringComparison.OrdinalIgnoreCase)
                && mapping.TraversalDirection == Direction10.North);
            Assert.Contains(loaded.ObjectTemplates, obj => string.Equals(obj.Name, "Template Crate", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.BaseObjects, obj => string.Equals(obj.Name, "Base Crate", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.RoomTemplates, room => string.Equals(room.Name, "Template Room Beta", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.GlobalObjects, obj => string.Equals(obj.Name, "Global Lantern", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(loaded.GlobalSoundEffectLibraryEntries, entry => string.Equals(entry.SoundEffectKey, "sfx.global.ping", StringComparison.Ordinal));
            Assert.Contains(loaded.GlobalEventSubscriptions, entry => string.Equals(entry.EventKey, "OnGlobalPing", StringComparison.Ordinal));
            Assert.Contains(loaded.GlobalTimerDefinitions, entry => string.Equals(entry.TimerKey, "global.timer", StringComparison.Ordinal));
            Assert.Empty(loaded.Diagnostics);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadGlobalsForImport_AddsDiagnostic_WhenProjectPathCannotBeInferred()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalsInferenceFallback",
                GameObjects = new List<GameObject>
                {
                    new() { Name = "Global Compass", ProducerNotes = "Global Compass" }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalsInferenceFallback.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var globalsFilePath = BuildProjectGlobalsFilePath(projectFilePath);
            var detachedGlobalsPath = Path.Combine(tempRoot, "detached.globals.json");
            File.Copy(globalsFilePath, detachedGlobalsPath, overwrite: true);

            var loaded = service.TryLoadGlobalNodeForImport(detachedGlobalsPath);
            Assert.NotNull(loaded);
            Assert.Contains(loaded!.Diagnostics, message =>
                message.Contains("Unable to infer source project path", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadGlobalsForImport_AddsDiagnostic_WhenMixedCanonicalAndLegacyLayoutsArePresent()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalsMixedLayout",
                GameObjects = new List<GameObject>
                {
                    new() { Name = "Global Compass", ProducerNotes = "Global Compass" }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalsMixedLayout.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            Directory.CreateDirectory(BuildLegacyRoomsFolderPath(projectFilePath));

            var loaded = service.TryLoadGlobalNodeForImport(BuildProjectGlobalsFilePath(projectFilePath));
            Assert.NotNull(loaded);
            Assert.Contains(loaded!.Diagnostics, message =>
                message.Contains("Mixed donor layout detected", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadGlobalsForImport_AddsDiagnostic_WhenReferencedTemplateIdIsMissingFromObjectSidecars()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var project = new ProjectModel
            {
                Name = "GlobalsMissingId",
                ObjectTemplates = new List<GameObject>
                {
                    new() { Name = "Template Crate", ProducerNotes = "Template Crate" }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "GlobalsMissingId.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var globalsFilePath = BuildProjectGlobalsFilePath(projectFilePath);
            var globalsNode = JsonNode.Parse(File.ReadAllText(globalsFilePath, Encoding.UTF8))?.AsObject()
                ?? throw new InvalidOperationException("Failed to parse globals sidecar.");

            var missingTemplateId = Guid.NewGuid();
            var templateIds = globalsNode["objectTemplateIds"]?.AsArray()
                ?? throw new InvalidOperationException("Missing objectTemplateIds array.");
            templateIds.Add(missingTemplateId);

            File.WriteAllText(globalsFilePath, globalsNode.ToJsonString(), Encoding.UTF8);

            var loaded = service.TryLoadGlobalNodeForImport(globalsFilePath);
            Assert.NotNull(loaded);
            Assert.Contains(loaded!.Diagnostics, message =>
                message.Contains("Missing referenced ids for objectTemplateIds", StringComparison.OrdinalIgnoreCase)
                && message.Contains(missingTemplateId.ToString("D"), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_DoesNotDuplicateGlobalsContent_InMainProjectFile()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sharedVariableId = Guid.NewGuid();
            var globalProcedureId = Guid.NewGuid();
            var globalActionId = Guid.NewGuid();

            var project = new ProjectModel
            {
                Name = "NoMainDup",
                CommandVerbs = new List<string> { "look" },
                Directionals = new List<string> { "north" },
                DirectionalTraversalMappings = new List<DirectionalTraversalMapping>
                {
                    new() { Token = "north", TraversalDirection = Direction10.North }
                },
                ObjectTemplates = new List<GameObject>
                {
                    new() { Name = "Template Crate", ProducerNotes = "Template Crate" }
                },
                RoomTemplates = new List<Room>
                {
                    new() { Name = "Template Room Gamma", Description = "Room template" }
                },
                GlobalObjectScopeName = "Global Objects",
                GlobalObjectAvailableActions = new List<CommandAction>
                {
                    new()
                    {
                        Id = globalActionId,
                        Name = "Global Look",
                        ActionType = CommandActionType.EchoMessage,
                        Verbs = new List<string> { "look" },
                        EchoMessage = "global-look",
                        Payload = new StoryboardDesigner.App.Models.EchoPayload()
                    }
                },
                GameObjects = new List<GameObject>
                {
                    new() { Name = "Global Lantern", ProducerNotes = "Global Lantern" }
                },
                SharedVariables =
                [
                    new SharedVariableDefinition
                    {
                        Id = sharedVariableId,
                        Name = "SharedDoorState"
                    }
                ],
                ProcedureIds = new List<Guid> { globalProcedureId },
                Planets = new List<Planet>
                {
                    new() { Name = "Planet One" }
                }
            };

            var projectFilePath = Path.Combine(tempRoot, "NoMainDup.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            using var projectDoc = JsonDocument.Parse(File.ReadAllText(projectFilePath, Encoding.UTF8));

            Assert.False(projectDoc.RootElement.TryGetProperty("commandVerbs", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("directionals", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("directionalTraversalMappings", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("gameObjects", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("objectTemplates", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("roomTemplates", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("availableGameActions", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("procedureIds", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("sharedVariables", out _));
            Assert.False(projectDoc.RootElement.TryGetProperty("planetIds", out _));

            var globalsFilePath = BuildProjectGlobalsFilePath(projectFilePath);
            using var globalsDoc = JsonDocument.Parse(File.ReadAllText(globalsFilePath, Encoding.UTF8));
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("additionalVerbs").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("additionalDirectionals").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("additionalDirectionalTraversalMappings").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("gameObjectIds").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("objectTemplateIds").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("roomTemplateIds").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("availableGameActions").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("procedureIds").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("sharedVariables").GetArrayLength());
            Assert.Equal(1, globalsDoc.RootElement.GetProperty("planetIds").GetArrayLength());
            Assert.Equal(0, globalsDoc.RootElement.GetProperty("gameObjects").GetArrayLength());
            Assert.Equal(0, globalsDoc.RootElement.GetProperty("objectTemplates").GetArrayLength());
            Assert.Equal(0, globalsDoc.RootElement.GetProperty("roomTemplates").GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveProjectModel_RoundTripsObjectLockOperationRequirements()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var unlockKeyA = Guid.NewGuid();
            var unlockKeyB = Guid.NewGuid();
            var lockKey = Guid.NewGuid();

            var lockableObject = new GameObject
            {
                Name = "Vault Door",
                IsLockable = true,
                LockOperationRequirements = new LockOperationRequirements
                {
                    UnlockKeyRequirements =
                    [
                        new LockKeyRequirement
                        {
                            RequiredObjectId = unlockKeyA,
                            RequiredQuantity = 2,
                            IsOptional = true,
                            VariableRequirements =
                            [
                                new LockParticipantVariableRequirement
                                {
                                    VariableName = "isBroken",
                                    Operator = RuntimeVariableComparisonOperator.Equals,
                                    ExpectedValue = "false",
                                    QuantityEvaluationMode = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass
                                }
                            ]
                        },
                        new LockKeyRequirement { RequiredObjectId = unlockKeyB, RequiredQuantity = 1 }
                    ],
                    RequireKeyForLockOperation = true,
                    UseUnlockKeysForLockOperation = false,
                    LockKeyRequirements =
                    [
                        new LockKeyRequirement { RequiredObjectId = lockKey, RequiredQuantity = 1 }
                    ]
                }
            };

            var room = new Room
            {
                Name = "Vault",
                GameObjects = [lockableObject]
            };
            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "LockRequirementsRoundTrip",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "LockRequirementsRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedObject = loaded!.Planets
                .SelectMany(p => p.Countries)
                .SelectMany(c => c.Areas)
                .SelectMany(a => a.Rooms)
                .SelectMany(r => r.GameObjects)
                .Single(obj => string.Equals(obj.Name, "Vault Door", StringComparison.Ordinal));

            var requirements = loadedObject.LockOperationRequirements;
            Assert.NotNull(requirements);
            Assert.True(requirements.RequireKeyForLockOperation);
            Assert.False(requirements.UseUnlockKeysForLockOperation);

            Assert.Equal(2, requirements.UnlockKeyRequirements.Count);
            Assert.Contains(requirements.UnlockKeyRequirements, item => item.RequiredObjectId == unlockKeyA && item.RequiredQuantity == 2);
            Assert.Contains(requirements.UnlockKeyRequirements, item => item.RequiredObjectId == unlockKeyB && item.RequiredQuantity == 1);

            var optionalWithVariable = requirements.UnlockKeyRequirements.Single(item => item.RequiredObjectId == unlockKeyA);
            Assert.True(optionalWithVariable.IsOptional);
            Assert.Single(optionalWithVariable.VariableRequirements);
            Assert.Equal("isBroken", optionalWithVariable.VariableRequirements[0].VariableName);

            Assert.Single(requirements.LockKeyRequirements);
            Assert.Contains(requirements.LockKeyRequirements, item => item.RequiredObjectId == lockKey && item.RequiredQuantity == 1);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryLoadProjectModel_WhenRoomSidecarIsMalformed_ThrowsWithExactFilePath()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "MalformedRoom"
            };
            var area = new Area { Name = "Area", Rooms = [room] };
            var country = new Country { Name = "Country", Areas = [area] };
            var planet = new Planet { Name = "Planet", Countries = [country] };
            var project = new ProjectModel
            {
                Name = "MalformedRoomSidecar",
                Planets = [planet]
            };

            var projectFilePath = Path.Combine(tempRoot, "MalformedRoomSidecar.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var roomsFolderPath = BuildRoomsFolderPath(projectFilePath);
            var roomFilePath = Directory.EnumerateFiles(roomsFolderPath, "*.room.json", SearchOption.TopDirectoryOnly)
                .Single();

            // Force a hard parse failure to validate per-file diagnostics.
            File.WriteAllText(roomFilePath, "{\"id\":", Encoding.UTF8);

            var ex = Assert.Throws<InvalidOperationException>(() => service.TryLoadProjectModel(projectFilePath));
            Assert.Contains("Failed to deserialize", ex.Message, StringComparison.Ordinal);
            Assert.Contains(roomFilePath, ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RoomDto", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void AssertMapping(IEnumerable<DirectionalTraversalMapping> mappings, string token, Direction10 direction)
    {
        var mapping = mappings.Single(m => string.Equals(m.Token, token, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(direction, mapping.TraversalDirection);
    }

    private static string BuildProjectStateFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.state.json");
    }

    private static string BuildProjectGlobalsFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.globals.json");
    }

    private static string BuildAuthoringIndexFilePath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "authoring-index.html");
    }

    private static string BuildPlanetsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Planet");
    }

    private static string BuildLegacyPlanetsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.planets");
    }

    private static string BuildProceduresFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Procedure");
    }

    private static string BuildPhasesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Book");
    }

    private static string BuildLegacyProceduresFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.procedures");
    }

    private static string BuildRoomsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Room");
    }

    private static string BuildAuthoringGameObjectFilePath(string projectFilePath, Guid objectId)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "GameObject", $"{objectId:N}.object.json");
    }

    private static string BuildLegacyRoomsFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.rooms");
    }

    private static string BuildCountriesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Country");
    }

    private static string BuildLegacyCountriesFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.countries");
    }

    private static string BuildAreasFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "Area");
    }

    private static string BuildLegacyAreasFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return Path.Combine(folder, $"{baseName}.areas");
    }

    private static string BuildRuntimeExportRootFolderPath(string projectFilePath)
    {
        var folder = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        return Path.Combine(folder, "GameRuntimeJson");
    }

    private static string BuildScopeKindGameObjectPath(string projectFilePath, Guid objectId)
    {
        return Path.Combine(
            BuildRuntimeExportRootFolderPath(projectFilePath),
            "GameObject",
            objectId.ToString("D").ToUpperInvariant() + ".runtime.json");
    }

    private static string BuildRuntimeProjectBaseName(string projectFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(projectFilePath);
        return baseName.EndsWith(".sbe", StringComparison.OrdinalIgnoreCase)
            ? baseName[..^4]
            : baseName;
    }
}


