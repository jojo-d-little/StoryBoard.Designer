using System.Text.Json;
using System.Text.Json.Nodes;
using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;

namespace StoryboardDesigner.App.Tests;

public sealed class CaptureObjectsOnObjectFootprintAuthoringTests
{
    [Fact]
    public void MatchModeChoices_ExplainBothContractModes()
    {
        Assert.Collection(
            CaptureFootprintMatchModeChoices.All,
            anyOverlap =>
            {
                Assert.Equal(FootprintMatchMode.AnyOverlap, anyOverlap.Value);
                Assert.Equal("Any overlap", anyOverlap.DisplayName);
                Assert.Contains("one or more grid cells", anyOverlap.Explanation, StringComparison.Ordinal);
            },
            fullyContained =>
            {
                Assert.Equal(FootprintMatchMode.FullyContained, fullyContained.Value);
                Assert.Equal("Fully contained", fullyContained.DisplayName);
                Assert.Contains("entire footprint", fullyContained.Explanation, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void NewCaptureActionDefaultsToAnyOverlap()
    {
        var payload = Assert.IsType<CaptureObjectsOnObjectFootprintPayload>(
            CommandActionPayloadCoordinator.CreateDefaultPayload(CommandActionType.CaptureObjectsOnObjectFootprint));

        Assert.Equal(FootprintMatchMode.AnyOverlap, payload.FootprintMatchMode);
    }

    [Theory]
    [InlineData(FootprintMatchMode.AnyOverlap)]
    [InlineData(FootprintMatchMode.FullyContained)]
    public void SaveAndLoadProject_PreservesSelectedMatchMode(FootprintMatchMode mode)
    {
        var service = new JsonExportService();
        var action = CreateAction("Capture", mode);
        var project = new ProjectModel { Name = "Capture match mode", GlobalObjectAvailableActions = [action] };
        var tempRoot = CreateTempRoot();
        var projectPath = Path.Combine(tempRoot, "project.sbe.json");
        try
        {
            service.SaveProjectModel(projectPath, project);

            var actionFile = FindCaptureActionFile(tempRoot);
            var json = File.ReadAllText(actionFile);
            Assert.Contains("footprintMatchMode", json, StringComparison.Ordinal);
            Assert.Contains(mode.ToString(), json, StringComparison.Ordinal);
            var loaded = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var loadedPayload = Assert.IsType<CaptureObjectsOnObjectFootprintPayload>(
                Assert.Single(loaded.GlobalObjectAvailableActions).Payload);

            Assert.Equal(mode, loadedPayload.FootprintMatchMode);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadingProjectWithMissingMatchMode_KeepsItMissingForValidation()
    {
        var service = new JsonExportService();
        var project = new ProjectModel
        {
            Name = "Missing capture mode",
            GlobalObjectAvailableActions = [CreateAction("Capture", FootprintMatchMode.AnyOverlap)]
        };
        var tempRoot = CreateTempRoot();
        var projectPath = Path.Combine(tempRoot, "project.sbe.json");
        try
        {
            service.SaveProjectModel(projectPath, project);
            var actionFile = FindCaptureActionFile(tempRoot);
            var root = JsonNode.Parse(File.ReadAllText(actionFile))!;
            Assert.True(RemoveFootprintMatchModeFromCapturePayload(root));
            File.WriteAllText(actionFile, root.ToJsonString());

            var loaded = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var payload = Assert.IsType<CaptureObjectsOnObjectFootprintPayload>(
                Assert.Single(loaded.GlobalObjectAvailableActions).Payload);

            Assert.Null(payload.FootprintMatchMode);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadingProjectWithUnsupportedMatchMode_KeepsItInvalidForValidation()
    {
        var service = new JsonExportService();
        var project = new ProjectModel
        {
            Name = "Unsupported capture mode",
            GlobalObjectAvailableActions = [CreateAction("Capture", FootprintMatchMode.AnyOverlap)]
        };
        var tempRoot = CreateTempRoot();
        var projectPath = Path.Combine(tempRoot, "project.sbe.json");
        try
        {
            service.SaveProjectModel(projectPath, project);
            var actionFile = FindCaptureActionFile(tempRoot);
            var root = JsonNode.Parse(File.ReadAllText(actionFile))!;
            Assert.True(SetFootprintMatchMode(root, "FutureMode"));
            File.WriteAllText(actionFile, root.ToJsonString());

            var loaded = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(projectPath));
            var payload = Assert.IsType<CaptureObjectsOnObjectFootprintPayload>(
                Assert.Single(loaded.GlobalObjectAvailableActions).Payload);

            Assert.Null(payload.FootprintMatchMode);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(FootprintMatchMode.AnyOverlap)]
    [InlineData(FootprintMatchMode.FullyContained)]
    public void ProjectAndRuntimeExport_PreserveMatchMode(FootprintMatchMode mode)
    {
        var action = CreateAction("Capture", mode);

        var projectPayload = Assert.IsType<ProjectCaptureObjectsOnObjectFootprintActionPayloadDto>(
            JsonExportService.BuildCaptureObjectsOnObjectFootprintPayloadElement(action));
        var runtimePayload = Assert.IsType<RuntimeCaptureObjectsOnObjectFootprintActionPayloadDto>(
            JsonExportService.BuildCleanCaptureObjectsOnObjectFootprintPayloadElement(action));
        var projectJson = JsonSerializer.Serialize(projectPayload, JsonExportService.CreateJsonOptions());
        var runtimeJson = JsonSerializer.Serialize(runtimePayload, JsonExportService.CreateJsonOptions());
        var loadedRuntimePayload = JsonSerializer.Deserialize<RuntimeCaptureObjectsOnObjectFootprintActionPayloadDto>(
            runtimeJson,
            JsonExportService.CreateJsonOptions());

        Assert.Equal(mode, projectPayload.FootprintMatchMode);
        Assert.Equal(mode, runtimePayload.FootprintMatchMode);
        Assert.Contains($"\"footprintMatchMode\":\"{mode}\"", projectJson, StringComparison.Ordinal);
        Assert.Contains($"\"footprintMatchMode\":\"{mode}\"", runtimeJson, StringComparison.Ordinal);
        Assert.Equal(mode, loadedRuntimePayload?.FootprintMatchMode);
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData(-1, "not supported")]
    public void Validation_ReportsMissingOrUnsupportedMatchMode(int? modeValue, string expectedMessage)
    {
        var payloadMode = modeValue.HasValue ? (FootprintMatchMode?)modeValue.Value : null;
        var action = CreateAction("Capture", payloadMode);
        var room = new Room { Name = "Room", AvailableActions = [action] };
        var project = new ProjectModel
        {
            Name = "Capture validation",
            Planets = [new Planet { Countries = [new Country { Areas = [new Area { Rooms = [room] }] }] }]
        };
        ScopeHierarchy.AttachParents(project);
        var registry = new ValidationRuleRegistry();
        registry.Register(new CaptureFootprintMatchModeRule());

        var issue = Assert.Single(new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues);

        Assert.Equal("ACT-027", issue.RuleId);
        Assert.Contains(expectedMessage, issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Choose Any overlap or Fully contained", issue.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_ReportsMissingMatchModeOnInlineSequenceCapture()
    {
        var inlineCapture = new CommandActionDto
        {
            Id = Guid.NewGuid(),
            Name = "Capture inline",
            ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
            Payload = new ProjectCaptureObjectsOnObjectFootprintActionPayloadDto
            {
                ObjectSetReference = new ProjectObjectSetReferenceDto { ObjectSetKey = "captured" },
                TargetObjectId = Guid.NewGuid(),
                FootprintMatchMode = (FootprintMatchMode)(-1)
            }
        };
        var room = new Room
        {
            Name = "Room",
            SequenceDefinitions =
            [
                new ProjectSequenceDefinitionDto
                {
                    SequenceId = Guid.NewGuid(),
                    DisplayName = "Inline capture",
                    Steps =
                    [
                        new ProjectSequenceStepDto
                        {
                            StepId = Guid.NewGuid(),
                            BranchId = "main",
                            StepType = SequenceStepType.Action,
                            ContinuationMode = ContinuationMode.End,
                            InlineAction = inlineCapture
                        }
                    ]
                }
            ]
        };
        var project = new ProjectModel
        {
            Name = "Inline capture validation",
            Planets = [new Planet { Countries = [new Country { Areas = [new Area { Rooms = [room] }] }] }]
        };
        ScopeHierarchy.AttachParents(project);
        var registry = new ValidationRuleRegistry();
        registry.Register(new CaptureFootprintMatchModeRule());

        var issue = Assert.Single(new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues);

        Assert.Equal("ACT-027", issue.RuleId);
        Assert.Contains("required", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sequence:Inline capture", issue.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void OneTimeMigration_AddsAnyOverlapOnlyForMissingCaptureModesAndIsIdempotent()
    {
        const string json = """{"actions":[{"payload":{"$payloadType":"captureObjectsOnObjectFootprint","objectSetReference":{"objectSetKey":"old"}}},{"payload":{"$payloadType":"captureObjectsOnObjectFootprint","footprintMatchMode":"FullyContained"}},{"payload":{"$payloadType":"checkObjectSetContainsObject"}}]}""";

        var first = CaptureFootprintMatchModeMigration.AddExplicitAnyOverlap(json);
        var second = CaptureFootprintMatchModeMigration.AddExplicitAnyOverlap(first.Json);
        var root = JsonNode.Parse(first.Json)!;
        var payloads = root["actions"]!.AsArray().Select(static action => action!["payload"]!).ToArray();

        Assert.Equal(1, first.UpdatedPayloadCount);
        Assert.Equal(0, second.UpdatedPayloadCount);
        Assert.Equal("AnyOverlap", payloads[0]["footprintMatchMode"]!.GetValue<string>());
        Assert.Equal("FullyContained", payloads[1]["footprintMatchMode"]!.GetValue<string>());
        Assert.Null(payloads[2]["footprintMatchMode"]);
    }

    private static CommandAction CreateAction(string name, FootprintMatchMode? mode) => new()
    {
        Name = name,
        ActionType = CommandActionType.CaptureObjectsOnObjectFootprint,
        NoVerbLinkage = true,
        Payload = new CaptureObjectsOnObjectFootprintPayload("captured", Guid.NewGuid(), mode)
    };

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string FindCaptureActionFile(string root) => Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
        .First(path => File.ReadAllText(path).Contains("captureObjectsOnObjectFootprint", StringComparison.OrdinalIgnoreCase));

    private static bool RemoveFootprintMatchModeFromCapturePayload(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var isCapturePayload = obj.TryGetPropertyValue("$payloadType", out var type)
                && string.Equals(type?.GetValue<string>(), "captureObjectsOnObjectFootprint", StringComparison.OrdinalIgnoreCase);
            if (isCapturePayload)
            {
                obj.Remove("footprintMatchMode");
                return true;
            }

            var removed = false;
            foreach (var child in obj.ToList())
            {
                if (child.Value is not null) removed |= RemoveFootprintMatchModeFromCapturePayload(child.Value);
            }
            return removed;
        }
        else if (node is JsonArray array)
        {
            var removed = false;
            foreach (var child in array)
            {
                if (child is not null) removed |= RemoveFootprintMatchModeFromCapturePayload(child);
            }
            return removed;
        }

        return false;
    }

    private static bool SetFootprintMatchMode(JsonNode node, string mode)
    {
        if (node is JsonObject obj)
        {
            var isCapturePayload = obj.TryGetPropertyValue("$payloadType", out var type)
                && string.Equals(type?.GetValue<string>(), "captureObjectsOnObjectFootprint", StringComparison.OrdinalIgnoreCase);
            if (isCapturePayload)
            {
                obj["footprintMatchMode"] = mode;
                return true;
            }

            foreach (var child in obj.ToList())
            {
                if (child.Value is not null && SetFootprintMatchMode(child.Value, mode)) return true;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null && SetFootprintMatchMode(child, mode)) return true;
            }
        }

        return false;
    }
}
