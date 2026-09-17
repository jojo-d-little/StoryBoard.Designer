using System.Text;
using System.Text.Json;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public sealed class JsonExportServiceEventSubscriptionPersistenceTests
{
    private const string RuntimeExportFolderName = "GameRuntimeJson";

    [Fact]
    public void SaveProjectModel_RoundTripsScopeEventSubscriptions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Event Room"
            };
            var roomSubscription = BuildSampleSubscription("player.room.entered", "Inspect Room Action");
            roomSubscription.SubscriptionName = "Room Enter Inspect";
            room.EventSubscriptions.Add(roomSubscription);

            var area = new Area
            {
                Name = "Event Area",
                Rooms = new List<Room> { room }
            };
            var country = new Country
            {
                Name = "Event Country",
                Areas = new List<Area> { area }
            };
            var planet = new Planet
            {
                Name = "Event Planet",
                Countries = new List<Country> { country }
            };

            var project = new ProjectModel
            {
                Name = "EventRoundTrip",
                Planets = new List<Planet> { planet }
            };
            var globalSubscription = BuildSampleSubscription("procedure.completed", "Celebrate Action");
            globalSubscription.SubscriptionName = "Global Celebrate";
            project.GlobalScope.EventSubscriptions.Add(globalSubscription);

            var projectFilePath = Path.Combine(tempRoot, "EventRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedGlobalSubscription = Assert.Single(loaded!.GlobalScope.EventSubscriptions);
            Assert.Equal("procedure.completed", loadedGlobalSubscription.EventKey);
            Assert.Equal("Global Celebrate", loadedGlobalSubscription.SubscriptionName);
            Assert.Equal(EventSubscriberDispatchDisposition.consume, loadedGlobalSubscription.DispatchDisposition);
            Assert.Equal("Celebrate Action", loadedGlobalSubscription.ActionBindings[0].Target.ActionName);

            var loadedRoom = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedRoomSubscription = Assert.Single(loadedRoom.EventSubscriptions);
            Assert.Equal("player.room.entered", loadedRoomSubscription.EventKey);
            Assert.Equal("Room Enter Inspect", loadedRoomSubscription.SubscriptionName);
            Assert.Equal(RuntimeVariableComparisonOperator.Equals, loadedRoomSubscription.ActionBindings[0].Condition.Filters[0].Operator);
            Assert.Equal("true", loadedRoomSubscription.ActionBindings[0].Condition.Filters[0].ExpectedValue);
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
    public void ExportRuntimeProjectV1_EmitsScopeEventSubscriptions()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Event Room"
            };
            room.EventSubscriptions.Add(BuildSampleSubscription("player.room.entered", "Inspect Room Action"));

            var area = new Area
            {
                Name = "Event Area",
                Rooms = new List<Room> { room }
            };
            var country = new Country
            {
                Name = "Event Country",
                Areas = new List<Area> { area }
            };
            var planet = new Planet
            {
                Name = "Event Planet",
                Countries = new List<Country> { country }
            };

            var project = new ProjectModel
            {
                Name = "EventRuntimeExport",
                Planets = new List<Planet> { planet }
            };
            project.GlobalScope.EventSubscriptions.Add(BuildSampleSubscription("procedure.completed", "Celebrate Action"));

            var projectFilePath = Path.Combine(tempRoot, "EventRuntimeExport.sbe.json");
            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project);

            using var rootDoc = JsonDocument.Parse(File.ReadAllText(cleanProjectPath, Encoding.UTF8));
            var rootSubscriptions = rootDoc.RootElement.GetProperty("eventSubscriptions");
            Assert.Equal(1, rootSubscriptions.GetArrayLength());
            Assert.Equal("procedure.completed", rootSubscriptions[0].GetProperty("eventKey").GetString());

            var roomScopePath = Path.Combine(
                Path.GetDirectoryName(projectFilePath)!,
                RuntimeExportFolderName,
                "Room",
                $"{room.Id:D}.runtime.json");
            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomScopePath, Encoding.UTF8));
            var roomSubscriptions = roomDoc.RootElement.GetProperty("eventSubscriptions");
            Assert.Equal(1, roomSubscriptions.GetArrayLength());
            Assert.Equal("player.room.entered", roomSubscriptions[0].GetProperty("eventKey").GetString());
            Assert.Equal("Inspect Room Action", roomSubscriptions[0]
                .GetProperty("actionBindings")[0]
                .GetProperty("target")
                .GetProperty("actionName")
                .GetString());
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
    public void SaveProjectModel_LoadProjectModel_DefaultsSubscriptionNameToEventKey_WhenNameMissing()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Event Room"
            };

            var subscription = BuildSampleSubscription("player.room.entered", "Inspect Room Action");
            subscription.SubscriptionName = string.Empty;
            room.EventSubscriptions.Add(subscription);

            var project = new ProjectModel
            {
                Name = "EventSubscriptionNameFallback",
                Planets =
                [
                    new Planet
                    {
                        Name = "Planet",
                        Countries =
                        [
                            new Country
                            {
                                Name = "Country",
                                Areas =
                                [
                                    new Area
                                    {
                                        Name = "Area",
                                        Rooms = [room]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "EventSubscriptionNameFallback.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedSubscription = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .EventSubscriptions.Single();

            Assert.Equal("player.room.entered", loadedSubscription.EventKey);
            Assert.Equal("player.room.entered", loadedSubscription.SubscriptionName);
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
    public void SaveProjectModel_RoundTripsPayloadAndAnchoredFilterVariables()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Event Room"
            };

            room.EventSubscriptions.Add(new EventSubscriptionDefinition
            {
                Id = Guid.NewGuid(),
                EventKey = "procedure.started",
                IsEnabled = true,
                Lane = "foreground",
                DispatchDisposition = EventSubscriberDispatchDisposition.consume,
                ActionBindings =
                [
                    new EventActionBindingDefinition
                    {
                        Order = 1,
                        IsEnabled = true,
                        Condition = new EventBindingConditionDefinition
                        {
                            QuantityEvaluationMode = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass,
                            Filters =
                            [
                                new EventBindingFilterConditionDefinition
                                {
                                    VariableName = "procedureId",
                                    Operator = RuntimeVariableComparisonOperator.Equals,
                                    ExpectedValue = "true"
                                },
                                new EventBindingFilterConditionDefinition
                                {
                                    VariableName = "currentCommand::primaryCommandObject.isBent",
                                    Operator = RuntimeVariableComparisonOperator.Equals,
                                    ExpectedValue = "true"
                                }
                            ]
                        },
                        Target = new EventBindingTargetDefinition
                        {
                            ActionName = "Inspect",
                            OnMissingAction = "DiagnosticOnly",
                            StopChainOnFailure = true
                        }
                    }
                ]
            });

            var area = new Area
            {
                Name = "Event Area",
                Rooms = new List<Room> { room }
            };
            var country = new Country
            {
                Name = "Event Country",
                Areas = new List<Area> { area }
            };
            var planet = new Planet
            {
                Name = "Event Planet",
                Countries = new List<Country> { country }
            };

            var project = new ProjectModel
            {
                Name = "EventSyntaxRoundTrip",
                Planets = new List<Planet> { planet }
            };

            var projectFilePath = Path.Combine(tempRoot, "EventSyntaxRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedFilters = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .EventSubscriptions.Single()
                .ActionBindings.Single()
                .Condition.Filters;

            Assert.Equal(2, loadedFilters.Count);
            Assert.Equal("procedureId", loadedFilters[0].VariableName);
            Assert.Equal("currentCommand::primaryCommandObject.isBent", loadedFilters[1].VariableName);

            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project);
            var roomScopePath = Path.Combine(
                Path.GetDirectoryName(projectFilePath)!,
                RuntimeExportFolderName,
                "Room",
                $"{room.Id:D}.runtime.json");

            Assert.True(File.Exists(cleanProjectPath));
            Assert.True(File.Exists(roomScopePath));

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomScopePath, Encoding.UTF8));
            var filtersJson = roomDoc.RootElement
                .GetProperty("eventSubscriptions")[0]
                .GetProperty("actionBindings")[0]
                .GetProperty("condition")
                .GetProperty("filters");

            Assert.Equal("procedureId", filtersJson[0].GetProperty("variableName").GetString());
            Assert.Equal("currentCommand::primaryCommandObject.isBent", filtersJson[1].GetProperty("variableName").GetString());
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
    public void SaveProjectModel_AndExport_RoundTripsSubscriptionSourceAndVisibilityFields()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var explicitSourceScopeId = Guid.NewGuid();
            var room = new Room
            {
                Name = "Event Room"
            };

            var subscription = BuildSampleSubscription("player.room.entered", "Inspect Room Action");
            subscription.SubscriptionVisibleWhenContained = true;
            subscription.SubscriptionSourceMatchMode = SubscriptionSourceMatchMode.ExplicitSourceId;
            subscription.SubscriptionSourceScopeNodeId = explicitSourceScopeId;
            room.EventSubscriptions.Add(subscription);

            var project = new ProjectModel
            {
                Name = "EventSubscriptionSourceVisibilityRoundTrip",
                Planets =
                [
                    new Planet
                    {
                        Name = "Planet",
                        Countries =
                        [
                            new Country
                            {
                                Name = "Country",
                                Areas =
                                [
                                    new Area
                                    {
                                        Name = "Area",
                                        Rooms = [room]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "EventSubscriptionSourceVisibilityRoundTrip.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var loadedSubscription = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .EventSubscriptions.Single();

            Assert.True(loadedSubscription.SubscriptionVisibleWhenContained);
            Assert.Equal(SubscriptionSourceMatchMode.ExplicitSourceId, loadedSubscription.SubscriptionSourceMatchMode);
            Assert.Equal(explicitSourceScopeId, loadedSubscription.SubscriptionSourceScopeNodeId);

            var cleanProjectPath = service.ExportCleanProjectV1(projectFilePath, project);
            Assert.True(File.Exists(cleanProjectPath));

            var roomScopePath = Path.Combine(
                Path.GetDirectoryName(projectFilePath)!,
                RuntimeExportFolderName,
                "Room",
                $"{room.Id:D}.runtime.json");
            Assert.True(File.Exists(roomScopePath));

            using var roomDoc = JsonDocument.Parse(File.ReadAllText(roomScopePath, Encoding.UTF8));
            var exportedSubscription = roomDoc.RootElement
                .GetProperty("eventSubscriptions")[0];

            Assert.True(exportedSubscription.GetProperty("subscriptionVisibleWhenContained").GetBoolean());
            Assert.Equal(
                "ExplicitSourceId",
                exportedSubscription.GetProperty("subscriptionSourceMatchMode").GetString());
            Assert.Equal(
                explicitSourceScopeId.ToString("D"),
                exportedSubscription.GetProperty("subscriptionSourceScopeNodeId").GetString());
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
    public void LegacyDottedFilterVariable_LoadsAndCanBeUpgradedToAnchoredSyntax()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var room = new Room
            {
                Name = "Event Room"
            };
            room.EventSubscriptions.Add(new EventSubscriptionDefinition
            {
                Id = Guid.NewGuid(),
                EventKey = "player_room_entered",
                ActionBindings =
                [
                    new EventActionBindingDefinition
                    {
                        Order = 1,
                        Condition = new EventBindingConditionDefinition
                        {
                            Filters =
                            [
                                new EventBindingFilterConditionDefinition
                                {
                                    VariableName = "primaryCommandObject.isBent",
                                    Operator = RuntimeVariableComparisonOperator.Equals,
                                    ExpectedValue = "true"
                                }
                            ]
                        },
                        Target = new EventBindingTargetDefinition
                        {
                            ActionName = "Inspect"
                        }
                    }
                ]
            });

            var project = new ProjectModel
            {
                Name = "LegacyEventSyntax",
                Planets =
                [
                    new Planet
                    {
                        Name = "Planet",
                        Countries =
                        [
                            new Country
                            {
                                Name = "Country",
                                Areas =
                                [
                                    new Area
                                    {
                                        Name = "Area",
                                        Rooms = [room]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            var projectFilePath = Path.Combine(tempRoot, "LegacyEventSyntax.sbe.json");
            service.SaveProjectModel(projectFilePath, project);

            var loaded = service.TryLoadProjectModel(projectFilePath);
            Assert.NotNull(loaded);

            var legacyFilter = loaded!
                .Planets.Single()
                .Countries.Single()
                .Areas.Single()
                .Rooms.Single()
                .EventSubscriptions.Single()
                .ActionBindings.Single()
                .Condition.Filters.Single();

            Assert.Equal("primaryCommandObject.isBent", legacyFilter.VariableName);

            var preUpgrade = EvaluateEventVariableRules(loaded);
            Assert.Contains(preUpgrade.Issues, issue => issue.RuleId == "EVT-006");

            legacyFilter.VariableName = VariableReferenceSyntax.BuildAnchoredReference(
                "primaryCommandObject",
                "isBent");

            var postUpgrade = EvaluateEventVariableRules(loaded);
            Assert.DoesNotContain(postUpgrade.Issues, issue => issue.RuleId == "EVT-006");
            Assert.DoesNotContain(postUpgrade.Issues, issue => issue.RuleId == "EVT-003");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static ValidationExecutionResult EvaluateEventVariableRules(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new EventSubscriptionManifestEventKeyRule());
        registry.Register(new EventSubscriptionFilterVariableSyntaxRule());
        registry.Register(new EventSubscriptionAnchorSubPropertyReferenceRule());
        registry.Register(new EventSubscriptionAmbiguousAnchorLikePathRule());
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }

    private static EventSubscriptionDefinition BuildSampleSubscription(string eventKey, string actionName)
    {
        return new EventSubscriptionDefinition
        {
            Id = Guid.NewGuid(),
            EventKey = eventKey,
            SubscriptionName = eventKey,
            IsEnabled = true,
            Lane = "foreground",
            DispatchDisposition = EventSubscriberDispatchDisposition.consume,
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    IsEnabled = true,
                    Condition = new EventBindingConditionDefinition
                    {
                        QuantityEvaluationMode = RuntimeVariableQuantityEvaluationMode.AllResolvedMustPass,
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "action.trigger.isEventFired",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    },
                    Target = new EventBindingTargetDefinition
                    {
                        ActionName = actionName,
                        OnMissingAction = "DiagnosticOnly",
                        StopChainOnFailure = true
                    }
                }
            ]
        };
    }
}
