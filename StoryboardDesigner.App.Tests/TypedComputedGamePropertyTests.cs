using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TypedComputedGamePropertyTests
{
    [Fact]
    public void IntegrityRule_AcceptsLegacyBooleanAndOrderedNumericAndStringBranches()
    {
        var project = BuildValidProject();

        var issues = new ComputedGamePropertyIntegrityRule()
            .Evaluate(ValidationRuleTestContext.Create(project))
            .ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void IntegrityRule_ReportsMissingElseMissingOutputAndTypeMismatch()
    {
        var project = new ProjectModel();
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "score",
            DefaultValue = "1",
            ValueRestriction = GamePropertyValueRestriction.Numeric
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "name",
            DefaultValue = "Alex",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "badScore",
            DefaultValue = "0",
            ValueRestriction = GamePropertyValueRestriction.Numeric,
            Computation = new ProjectGamePropertyComputationDto
            {
                ResultKind = "Numeric",
                ResultBranches =
                [
                    new ProjectGamePropertyResultBranchDto
                    {
                        Condition = Exists("score"),
                        Result = new ProjectGamePropertyOperandDto
                        {
                            GamePropertySource = new ProjectGamePropertySourceReferenceDto { PropertyName = "missing" }
                        }
                    },
                    new ProjectGamePropertyResultBranchDto
                    {
                        Condition = Exists("score"),
                        Result = new ProjectGamePropertyOperandDto
                        {
                            GamePropertySource = new ProjectGamePropertySourceReferenceDto { PropertyName = "name" }
                        }
                    }
                ]
            }
        });

        var descriptions = new ComputedGamePropertyIntegrityRule()
            .Evaluate(ValidationRuleTestContext.Create(project))
            .Select(static issue => issue.Description)
            .ToList();

        Assert.Contains(descriptions, static description => description.Contains("does not resolve", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(descriptions, static description => description.Contains("requires Numeric", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(descriptions, static description => description.Contains("final else result", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProjectJson_RoundTripsTypedComputationBranchesAndPropertyOutputsInOrder()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var path = Path.Combine(tempRoot, "TypedComputed.sbe.json");
            var project = BuildValidProject();
            project.Name = "TypedComputed";

            service.SaveProjectModel(path, project);
            var loaded = service.TryLoadProjectModel(path);

            Assert.NotNull(loaded);
            Assert.NotNull(Assert.Single(loaded!.GlobalVariables, static property => property.Name == "legacyBoolean").Computation!.Condition);
            var restoredNumeric = Assert.Single(loaded.GlobalVariables, static property => property.Name == "scoreAdjusted").Computation!;
            Assert.Equal("Numeric", restoredNumeric.ResultKind);
            Assert.Equal("8.25", restoredNumeric.ResultBranches!.Single().Result!.Value);
            Assert.Equal("score", restoredNumeric.ElseResult!.GamePropertySource!.PropertyName);
            var restored = Assert.Single(loaded!.GlobalVariables, static property => property.Name == "scoreLabel").Computation!;
            Assert.Equal("String", restored.ResultKind);
            Assert.Equal(2, restored.ResultBranches!.Count);
            Assert.Equal("enabled", restored.ResultBranches[0].Condition!.GamePropertySource!.PropertyName);
            Assert.Equal("score", restored.ResultBranches[1].Condition!.GamePropertySource!.PropertyName);
            Assert.Equal("High", restored.ResultBranches[0].Result!.Value);
            Assert.Equal("name", restored.ResultBranches[1].Result!.GamePropertySource!.PropertyName);
            Assert.Equal("Unknown", restored.ElseResult!.Value);

            var runtimePath = service.ExportCleanProjectV1(path, loaded);
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath));
            var runtimeProperties = runtime.RootElement.GetProperty("gameProperties")
                .EnumerateArray()
                .ToDictionary(static property => property.GetProperty("name").GetString()!, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("Numeric", runtimeProperties["scoreAdjusted"].GetProperty("computation").GetProperty("resultKind").GetString());
            Assert.Equal(2, runtimeProperties["scoreLabel"].GetProperty("computation").GetProperty("resultBranches").GetArrayLength());
            Assert.Equal("Text", runtimeProperties["scoreLabel"].GetProperty("computation").GetProperty("elseResult").GetProperty("valueKind").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static ProjectModel BuildValidProject()
    {
        var project = new ProjectModel();
        project.GlobalVariables.Add(new GamePropertyDefinition { Name = "enabled", DefaultValue = "true", ValueRestriction = GamePropertyValueRestriction.TrueFalse });
        project.GlobalVariables.Add(new GamePropertyDefinition { Name = "score", DefaultValue = "4.5", ValueRestriction = GamePropertyValueRestriction.Numeric });
        project.GlobalVariables.Add(new GamePropertyDefinition { Name = "name", DefaultValue = "Alex", ValueRestriction = GamePropertyValueRestriction.Unrestricted });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "legacyBoolean",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Computation = new ProjectGamePropertyComputationDto { ResultKind = "Boolean", Condition = Exists("enabled") }
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "scoreAdjusted",
            DefaultValue = "0",
            ValueRestriction = GamePropertyValueRestriction.Numeric,
            Computation = new ProjectGamePropertyComputationDto
            {
                ResultKind = "Numeric",
                ResultBranches =
                [
                    new ProjectGamePropertyResultBranchDto
                    {
                        Condition = Exists("enabled"),
                        Result = new ProjectGamePropertyOperandDto { ValueKind = "Numeric", Value = "8.25" }
                    }
                ],
                ElseResult = new ProjectGamePropertyOperandDto { GamePropertySource = new ProjectGamePropertySourceReferenceDto { PropertyName = "score" } }
            }
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "scoreLabel",
            DefaultValue = string.Empty,
            ValueRestriction = GamePropertyValueRestriction.Unrestricted,
            Computation = new ProjectGamePropertyComputationDto
            {
                ResultKind = "String",
                ResultBranches =
                [
                    new ProjectGamePropertyResultBranchDto
                    {
                        Condition = Exists("enabled"),
                        Result = new ProjectGamePropertyOperandDto { ValueKind = "Text", Value = "High" }
                    },
                    new ProjectGamePropertyResultBranchDto
                    {
                        Condition = Exists("score"),
                        Result = new ProjectGamePropertyOperandDto { GamePropertySource = new ProjectGamePropertySourceReferenceDto { PropertyName = "name" } }
                    }
                ],
                ElseResult = new ProjectGamePropertyOperandDto { ValueKind = "Text", Value = "Unknown" }
            }
        });
        return project;
    }

    private static ProjectGamePropertyConditionDto Exists(string propertyName) => new()
    {
        Kind = "Exists",
        GamePropertySource = new ProjectGamePropertySourceReferenceDto { PropertyName = propertyName }
    };
}
