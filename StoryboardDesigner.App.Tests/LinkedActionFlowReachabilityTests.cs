using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.ViewModels;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public sealed class LinkedActionFlowReachabilityTests
{
    [Fact]
    public void MainValidationRegistry_BuildsWithUniqueActionRuleIds()
    {
        var createRegistry = typeof(MainWindowViewModel).GetMethod(
            "CreateValidationRuleRegistry",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(createRegistry);
        var registry = Assert.IsType<ValidationRuleRegistry>(createRegistry!.Invoke(null, [true]));
        var actionRuleIds = registry.Rules
            .Where(static rule => rule.Metadata.RuleId.StartsWith("ACT-", StringComparison.Ordinal))
            .Select(static rule => rule.Metadata.RuleId)
            .ToArray();
        Assert.Equal(actionRuleIds.Length, actionRuleIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(registry.Rules, static rule => rule.Metadata.RuleId == "ACT-026");
    }

    [Fact]
    public void Analyze_RetainsEveryBranchAndAuthoredOrder_AndReportsUnreachableNodes()
    {
        var definition = MultiBranchDefinition(includeOrphan: true);

        var analysis = LinkedActionFlowReachability.Analyze(definition);

        Assert.Equal(new[] { 1, 4, 2, 3, 6 }, analysis.ReachableNodes.Select(static node => node.NodeId));
        Assert.Equal(new[] { 9 }, analysis.OrphanedNodes.Select(static node => node.NodeId));
        Assert.Equal(LinkedActionRunWhen.OnSuccess, definition.Nodes[0].Links[0].RunWhen);
        Assert.Equal(LinkedActionRunWhen.Always, definition.Nodes[0].Links[1].RunWhen);
        Assert.Equal(LinkedActionRunWhen.Always, definition.Nodes[0].Links[2].RunWhen);
        Assert.Equal(new Guid("00000000-0000-0000-0000-00000000002A"), definition.Nodes.Single(static node => node.NodeId == 2).TargetObjectId);
    }

    [Fact]
    public void PruneUnreachableNodes_RemovesSubtreeAfterItsOnlyLinkIsRemoved()
    {
        var definition = MultiBranchDefinition(includeOrphan: false);
        definition.Nodes.Single(static node => node.NodeId == 2).Links.Add(new LinkedActionFlowLink { TargetNodeId = 5 });
        definition.Nodes.Add(new LinkedActionFlowNode { NodeId = 5, ActionId = Guid.NewGuid() });
        definition.Nodes.Single(static node => node.NodeId == 1).Links.RemoveAll(static link => link.TargetNodeId == 2);

        var pruned = LinkedActionFlowReachability.PruneUnreachableNodes(definition);

        Assert.Equal(new[] { 2, 5 }, pruned.Select(static node => node.NodeId));
        Assert.Equal(new[] { 1, 4, 3, 6 }, definition.Nodes.Select(static node => node.NodeId));
    }

    [Fact]
    public void OrphanRule_ReportsIdsAndActionNamesAsNonBlockingWarning()
    {
        var linked = new CommandAction
        {
            Name = "setup",
            ActionType = CommandActionType.LinkedActions,
            LinkedFlowDefinition = MultiBranchDefinition(includeOrphan: true)
        };
        var actions = linked.LinkedFlowDefinition.Nodes.Select(node => new CommandAction
        {
            Id = node.ActionId,
            Name = node.NodeId == 9 ? "Old disconnected step" : $"Step {node.NodeId}",
            ActionType = CommandActionType.EchoMessage
        }).Append(linked).ToArray();

        var registry = new ValidationRuleRegistry();
        registry.Register(new LinkedActionOrphanNodesRule());
        var room = new Room { Name = "Room", AvailableActions = new ObservableCollection<CommandAction>(actions) };
        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        var project = new ProjectModel { StartingPlanetName = planet.Name, Planets = [planet] };
        var issues = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project)).Issues;

        var issue = Assert.Single(issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("node 9", issue.Description, StringComparison.Ordinal);
        Assert.Contains("Old disconnected step", issue.Description, StringComparison.Ordinal);
        Assert.Contains("excluded from saved and exported flows", issue.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectAndRuntimeExports_RetainEveryReachableBranchAndExcludeOrphans()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var nodeActions = Enumerable.Range(1, 6)
                .Select(index => new CommandAction { Name = $"Step {index}", ActionType = CommandActionType.EchoMessage })
                .ToArray();
            var definition = MultiBranchDefinition(includeOrphan: true);
            foreach (var node in definition.Nodes)
            {
                var index = node.NodeId switch { 9 => 4, 6 => 5, _ => node.NodeId - 1 };
                node.ActionId = nodeActions[index].Id;
            }
            var flow = new CommandAction { Name = "Flow", ActionType = CommandActionType.LinkedActions, LinkedFlowDefinition = definition };
            var room = new Room { Name = "Room", AvailableActions = [flow, .. nodeActions] };
            var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
            var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
            var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };
            var project = new ProjectModel { Name = "Flow", StartingPlanetName = planet.Name, Planets = [planet] };
            var projectPath = Path.Combine(tempRoot, "Flow.sbe.json");

            service.SaveProjectModel(projectPath, project);
            using var projectDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, "Room", $"{room.Id:N}.room.json")));
            var projectFlow = projectDoc.RootElement.GetProperty("availableGameActions").EnumerateArray()
                .Single(action => action.GetProperty("name").GetString() == "Flow").GetProperty("payload").GetProperty("linkedActions");
            AssertExportedFlow(projectFlow, nodeActions, includeOrphan: false);

            _ = service.ExportCleanProjectV1(projectPath, project);
            var runtimeRoomPath = FindRuntimeFlowPath(tempRoot);
            using var runtimeDoc = JsonDocument.Parse(File.ReadAllText(runtimeRoomPath));
            var runtimeFlow = runtimeDoc.RootElement.GetProperty("availableGameActions").EnumerateArray()
                .Single(action => action.GetProperty("name").GetString() == "Flow").GetProperty("payload").GetProperty("linkedActions");
            AssertExportedFlow(runtimeFlow, nodeActions, includeOrphan: false);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ImportRetainsLegacyOrphansForDiagnosis_ResaveOmitsThem()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var step = new CommandAction { Name = "Step", ActionType = CommandActionType.EchoMessage };
            var flow = new CommandAction
            {
                Name = "Flow",
                ActionType = CommandActionType.LinkedActions,
                LinkedFlowDefinition = new LinkedActionFlowDefinition
                {
                    EntryNodeId = 1,
                    Nodes = [new LinkedActionFlowNode { NodeId = 1, ActionId = step.Id }]
                }
            };
            var room = new Room { Name = "Room", AvailableActions = [flow, step] };
            var area = new Area { Name = "Area", Rooms = [room], StartingRoomId = room.Id };
            var country = new Country { Name = "Country", Areas = [area], StartingAreaName = area.Name };
            var planet = new Planet { Name = "Planet", Countries = [country], StartingCountryName = country.Name };
            var project = new ProjectModel { Name = "LegacyFlow", StartingPlanetName = planet.Name, Planets = [planet] };
            var projectPath = Path.Combine(tempRoot, "LegacyFlow.sbe.json");
            var roomPath = Path.Combine(tempRoot, "Room", $"{room.Id:N}.room.json");
            service.SaveProjectModel(projectPath, project);

            var roomJson = JsonNode.Parse(File.ReadAllText(roomPath))!.AsObject();
            var flowJson = roomJson["availableGameActions"]!.AsArray()
                .OfType<JsonObject>().Single(action => action["name"]?.GetValue<string>() == "Flow");
            flowJson["payload"]!["linkedActions"]!.AsArray().Add(new JsonObject
            {
                ["nodeId"] = 99,
                ["actionId"] = step.Id,
                ["onAlwaysNodeIds"] = new JsonArray(),
                ["onSuccessNodeIds"] = new JsonArray(),
                ["onFailureNodeIds"] = new JsonArray()
            });
            File.WriteAllText(roomPath, roomJson.ToJsonString(JsonExportService.CreateJsonOptions()));

            var imported = service.TryLoadProjectModel(projectPath)!;
            var importedFlow = Assert.Single(Assert.Single(Assert.Single(Assert.Single(imported.Planets).Countries).Areas).Rooms)
                .AvailableActions.Single(action => action.Name == "Flow");
            Assert.Contains(importedFlow.LinkedFlowDefinition.Nodes, static node => node.NodeId == 99);

            service.SaveProjectModel(projectPath, imported);
            var reloaded = service.TryLoadProjectModel(projectPath)!;
            var reloadedFlow = Assert.Single(Assert.Single(Assert.Single(Assert.Single(reloaded.Planets).Countries).Areas).Rooms)
                .AvailableActions.Single(action => action.Name == "Flow");
            Assert.DoesNotContain(reloadedFlow.LinkedFlowDefinition.Nodes, static node => node.NodeId == 99);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void AssertExportedFlow(JsonElement nodes, IReadOnlyList<CommandAction> actions, bool includeOrphan)
    {
        var items = nodes.EnumerateArray().ToArray();
        Assert.Equal(includeOrphan ? 6 : 5, items.Length);
        Assert.Equal(new[] { 1, 4, 2, 3, 6 }, items.Take(5).Select(item => item.GetProperty("nodeId").GetInt32()));
        var entry = items[0];
        Assert.Equal(new[] { 6, 4 }, entry.GetProperty("onAlwaysNodeIds").EnumerateArray().Select(static id => id.GetInt32()));
        Assert.Equal(new[] { 2 }, entry.GetProperty("onSuccessNodeIds").EnumerateArray().Select(static id => id.GetInt32()));
        Assert.Equal(new[] { 3 }, entry.GetProperty("onFailureNodeIds").EnumerateArray().Select(static id => id.GetInt32()));
        Assert.Equal(actions[1].Id, items.Single(item => item.GetProperty("nodeId").GetInt32() == 2).GetProperty("actionId").GetGuid());
        Assert.Equal(new Guid("00000000-0000-0000-0000-00000000002A"), items.Single(item => item.GetProperty("nodeId").GetInt32() == 2).GetProperty("targetObjectId").GetGuid());
        Assert.DoesNotContain(items, item => item.GetProperty("nodeId").GetInt32() == 9);
    }

    private static string FindRuntimeFlowPath(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.runtime.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("availableGameActions", out var actions)
                && actions.EnumerateArray().Any(action => action.GetProperty("name").GetString() == "Flow"))
            {
                return path;
            }
        }
        throw new Xunit.Sdk.XunitException("Runtime export did not contain the Flow action.");
    }

    private static LinkedActionFlowDefinition MultiBranchDefinition(bool includeOrphan)
    {
        var definition = new LinkedActionFlowDefinition
        {
            EntryNodeId = 1,
            Nodes =
            [
                new LinkedActionFlowNode
                {
                    NodeId = 1,
                    ActionId = Guid.NewGuid(),
                    Links =
                    [
                        new LinkedActionFlowLink { TargetNodeId = 2, RunWhen = LinkedActionRunWhen.OnSuccess, Order = 3 },
                        new LinkedActionFlowLink { TargetNodeId = 4, RunWhen = LinkedActionRunWhen.Always, Order = 1 },
                        new LinkedActionFlowLink { TargetNodeId = 6, RunWhen = LinkedActionRunWhen.Always, Order = 0 },
                        new LinkedActionFlowLink { TargetNodeId = 3, RunWhen = LinkedActionRunWhen.OnFailure, Order = 2 }
                    ]
                },
                new LinkedActionFlowNode { NodeId = 4, ActionId = Guid.NewGuid() },
                new LinkedActionFlowNode { NodeId = 2, ActionId = Guid.NewGuid(), TargetObjectId = new Guid("00000000-0000-0000-0000-00000000002A") },
                new LinkedActionFlowNode { NodeId = 3, ActionId = Guid.NewGuid() },
                new LinkedActionFlowNode { NodeId = 6, ActionId = Guid.NewGuid() }
            ]
        };
        if (includeOrphan)
        {
            definition.Nodes.Add(new LinkedActionFlowNode { NodeId = 9, ActionId = Guid.NewGuid() });
        }
        return definition;
    }
}
