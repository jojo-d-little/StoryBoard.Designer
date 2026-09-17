using System.Text.Json;
using System.Text.Json.Nodes;
using Storyboard.Shared.GameServices.Actions;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Tests;

public sealed class StarterProjectTemplateParityTests
{
    [Fact]
    public void BigHeadStartTemplates_OutcomeMessageMaps_ContainRegisteredResultCodeEntries()
    {
        var repoRoot = ResolveRepositoryRoot();
        var starterTemplateDir = Path.Combine(repoRoot, "StoryboardDesigner.App", "StarterProjects", "BigHeadStart", "Templates");

        Assert.True(Directory.Exists(starterTemplateDir), $"Starter template directory missing: {starterTemplateDir}");

        var issues = new List<string>();
        foreach (var templateFilePath in Directory.GetFiles(starterTemplateDir, "*.object.json", SearchOption.TopDirectoryOnly))
        {
            var root = JsonNode.Parse(File.ReadAllText(templateFilePath)) as JsonObject;
            Assert.NotNull(root);

            var templateName = root["name"]?.GetValue<string>()?.Trim() ?? Path.GetFileNameWithoutExtension(templateFilePath);
            var availableGameActions = root["availableGameActions"] as JsonArray;
            if (availableGameActions is null)
            {
                continue;
            }

            for (var actionIndex = 0; actionIndex < availableGameActions.Count; actionIndex++)
            {
                if (availableGameActions[actionIndex] is not JsonObject action)
                {
                    continue;
                }

                var actionName = action["name"]?.GetValue<string>()?.Trim() ?? $"action[{actionIndex}]";
                var actionTypeText = action["actionType"]?.GetValue<string>()?.Trim() ?? string.Empty;
                if (!Enum.TryParse<CommandActionType>(actionTypeText, ignoreCase: true, out var actionType))
                {
                    continue;
                }

                var expectedTokens = RuntimeCommandActionExecutor
                    .GetSupportedResultCodes(actionType)
                    .Select(static descriptor => descriptor.Token)
                    .Where(static token => !string.IsNullOrWhiteSpace(token))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (expectedTokens.Count == 0)
                {
                    continue;
                }

                var outcomeMap = action["outcomeMessageMap"] as JsonObject;
                if (outcomeMap is null)
                {
                    issues.Add($"Template '{templateName}' action '{actionName}' ({actionType}) is missing outcomeMessageMap.");
                    continue;
                }

                var keys = outcomeMap
                    .Select(static pair => pair.Key)
                    .Where(static key => !string.IsNullOrWhiteSpace(key))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var token in expectedTokens)
                {
                    if (!keys.Contains(token))
                    {
                        issues.Add($"Template '{templateName}' action '{actionName}' ({actionType}) missing outcomeMessageMap['{token}'] in file '{Path.GetFileName(templateFilePath)}'.");
                    }
                }
            }
        }

        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues));
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "StoryboardDesigner.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from AppContext.BaseDirectory.");
    }
}
