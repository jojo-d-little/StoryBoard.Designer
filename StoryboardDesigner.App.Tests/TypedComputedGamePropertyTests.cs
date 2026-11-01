using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class TypedComputedGamePropertyTests
{
    [Fact]
    public void IntegrityRule_AcceptsUnifiedBooleanNumericAndStringExpressions()
    {
        var issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(BuildValidProject())).ToList();
        Assert.Empty(issues);
    }

    [Fact]
    public void IntegrityRule_ReportsMalformedNestedNodesAndMissingSources()
    {
        var project = new ProjectModel();
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "computed", DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Computation = new ProjectGamePropertyComputationV2Dto
            {
                OutputType = "Boolean",
                Expression = new ProjectGamePropertyExpressionDto
                {
                    Kind = "and",
                    Operands = [ExistsExpression("notDefined")]
                }
            }
        });
        var descriptions = new ComputedGamePropertyIntegrityRule()
            .Evaluate(ValidationRuleTestContext.Create(project)).Select(static issue => issue.Description).ToList();
        Assert.Contains(descriptions, static description => description.Contains("at least two operands", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(descriptions, static description => description.Contains("does not resolve", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArithmeticIntegrity_AcceptsCoercibleNestedOperatorsAndWarnsForZeroDivisor()
    {
        var project = BuildValidProject();
        var computed = Assert.Single(project.GlobalVariables, static item => item.Name == "scoreArithmetic");
        computed.Computation!.Expression = new ProjectGamePropertyExpressionDto
        {
            Kind = "add",
            Left = new ProjectGamePropertyExpressionDto
            {
                Kind = "subtract",
                Left = Literal(true),
                Right = Literal("2")
            },
            Right = new ProjectGamePropertyExpressionDto
            {
                Kind = "multiply",
                Left = PropertyExpression("score"),
                Right = new ProjectGamePropertyExpressionDto
                {
                    Kind = "divide",
                    Left = Literal(6d),
                    Right = Literal(3d)
                }
            }
        };

        var issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();
        Assert.Empty(issues);

        computed.Computation.Expression = new ProjectGamePropertyExpressionDto
        {
            Kind = "divide",
            Left = Literal(10d),
            Right = Literal(false)
        };
        issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();
        var warning = Assert.Single(issues);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.Contains("zero divisor", warning.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArithmeticIntegrity_RejectsKnownInvalidStringConversionAndMissingOperands()
    {
        var project = BuildValidProject();
        var computed = Assert.Single(project.GlobalVariables, static item => item.Name == "scoreArithmetic");
        computed.Computation!.Expression = new ProjectGamePropertyExpressionDto
        {
            Kind = "add",
            Left = Literal("not a number"),
            Right = Literal(1d)
        };

        var descriptions = new ComputedGamePropertyIntegrityRule()
            .Evaluate(ValidationRuleTestContext.Create(project)).Select(static issue => issue.Description).ToList();
        Assert.Contains(descriptions, static description => description.Contains("String literal used by arithmetic", StringComparison.OrdinalIgnoreCase));

        computed.Computation.Expression.Right = null!;
        descriptions = new ComputedGamePropertyIntegrityRule()
            .Evaluate(ValidationRuleTestContext.Create(project)).Select(static issue => issue.Description).ToList();
        Assert.Contains(descriptions, static description => description.Contains("requires both operands", StringComparison.OrdinalIgnoreCase));

        computed.Computation.Expression = new ProjectGamePropertyExpressionDto
        {
            Kind = "multiply",
            Left = Literal(double.MaxValue),
            Right = Literal(2d)
        };
        descriptions = new ComputedGamePropertyIntegrityRule()
            .Evaluate(ValidationRuleTestContext.Create(project)).Select(static issue => issue.Description).ToList();
        Assert.Contains(descriptions, static description => description.Contains("constant arithmetic result must be finite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComputationFormatter_ShowsNestedArithmeticGroupingAndOperators()
    {
        var expression = new ProjectGamePropertyExpressionDto
        {
            Kind = "divide",
            Left = new ProjectGamePropertyExpressionDto { Kind = "add", Left = Literal(5d), Right = Literal(3d) },
            Right = new ProjectGamePropertyExpressionDto
            {
                Kind = "multiply",
                Left = Literal(2d),
                Right = new ProjectGamePropertyExpressionDto { Kind = "subtract", Left = Literal(9d), Right = Literal(1d) }
            }
        };

        Assert.Equal("((5 + 3) / (2 * (9 - 1)))", GamePropertyComputationFormatter.Format(expression));
    }

    [Fact]
    public void Cloner_RemapsOwnerIdsAcrossNestedExpressionSources()
    {
        var oldOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var computation = new ProjectGamePropertyComputationV2Dto
        {
            OutputType = "Numeric",
            Expression = new ProjectGamePropertyExpressionDto
            {
                Kind = "select",
                Branches = [new ProjectGamePropertySelectBranchDto
                {
                    When = new ProjectGamePropertyExpressionDto { Kind = "exists", Source = new ProjectGamePropertySourceReferenceDto { OwnerId = oldOwner, PropertyName = "present" } },
                    Value = new ProjectGamePropertyExpressionDto { Kind = "property", Source = new ProjectGamePropertySourceReferenceDto { OwnerId = oldOwner, PropertyName = "score" } }
                }],
                Else = Literal(0d)
            }
        };

        var clone = GamePropertyComputationCloner.Clone(computation)!;
        GamePropertyComputationCloner.RemapOwnerIds(clone, new Dictionary<Guid, Guid> { [oldOwner] = newOwner });
        Assert.Equal(oldOwner, computation.Expression!.Branches![0].Value!.Source!.OwnerId);
        Assert.Equal(newOwner, clone.Expression!.Branches![0].When!.Source!.OwnerId);
        Assert.Equal(newOwner, clone.Expression.Branches[0].Value!.Source!.OwnerId);
    }

    [Fact]
    public void ProjectJson_RoundTripsV2AndExportsOnlyV2Computation()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var path = Path.Combine(tempRoot, "UnifiedComputed.sbe.json");
            var project = BuildValidProject();
            project.Name = "UnifiedComputed";
            service.SaveProjectModel(path, project);
            var savedText = File.ReadAllText(Path.Combine(tempRoot, "UnifiedComputed.sbe.globals.json"));
            Assert.Contains("\"computationV2\"", savedText);
            Assert.DoesNotContain("\"computation\"", savedText);

            var loaded = service.TryLoadProjectModel(path)!;
            var restored = Assert.Single(loaded.GlobalVariables, static item => item.Name == "scoreAdjusted").Computation!;
            Assert.Equal("Numeric", restored.OutputType);
            Assert.Equal("select", restored.Expression!.Kind);
            Assert.Equal("8.25", restored.Expression.Branches!.Single().Value!.Value!.ToString());
            Assert.Equal("score", restored.Expression.Else!.Source!.PropertyName);

            var runtimePath = service.ExportCleanProjectV1(path, loaded);
            using var runtime = JsonDocument.Parse(File.ReadAllText(runtimePath));
            var runtimeProperty = runtime.RootElement.GetProperty("gameProperties").EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == "scoreAdjusted");
            Assert.True(runtimeProperty.TryGetProperty("computationV2", out _));
            Assert.False(runtimeProperty.TryGetProperty("computation", out _));

            var runtimeArithmetic = runtime.RootElement.GetProperty("gameProperties").EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == "scoreArithmetic");
            var arithmeticExpression = runtimeArithmetic.GetProperty("computationV2").GetProperty("expression");
            Assert.Equal("divide", arithmeticExpression.GetProperty("kind").GetString());
            Assert.Equal("add", arithmeticExpression.GetProperty("left").GetProperty("kind").GetString());
            Assert.Equal("subtract", arithmeticExpression.GetProperty("left").GetProperty("right").GetProperty("kind").GetString());
            Assert.Equal("multiply", arithmeticExpression.GetProperty("right").GetProperty("kind").GetString());
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
            Name = "isEnabled", DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Computation = new ProjectGamePropertyComputationV2Dto { OutputType = "Boolean", Expression = ExistsExpression("enabled") }
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "scoreAdjusted", DefaultValue = "0", ValueRestriction = GamePropertyValueRestriction.Numeric,
            Computation = new ProjectGamePropertyComputationV2Dto
            {
                OutputType = "Numeric",
                Expression = new ProjectGamePropertyExpressionDto
                {
                    Kind = "select",
                    Branches = [new ProjectGamePropertySelectBranchDto { When = ExistsExpression("enabled"), Value = Literal(8.25d) }],
                    Else = PropertyExpression("score")
                }
            }
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "scoreLabel", DefaultValue = string.Empty, ValueRestriction = GamePropertyValueRestriction.Unrestricted,
            Computation = new ProjectGamePropertyComputationV2Dto
            {
                OutputType = "String",
                Expression = new ProjectGamePropertyExpressionDto
                {
                    Kind = "select",
                    Branches = [new ProjectGamePropertySelectBranchDto { When = ExistsExpression("enabled"), Value = Literal("High") }],
                    Else = PropertyExpression("name")
                }
            }
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "scoreArithmetic", DefaultValue = "0", ValueRestriction = GamePropertyValueRestriction.Numeric,
            Computation = new ProjectGamePropertyComputationV2Dto
            {
                OutputType = "Numeric",
                Expression = new ProjectGamePropertyExpressionDto
                {
                    Kind = "divide",
                    Left = new ProjectGamePropertyExpressionDto
                    {
                        Kind = "add",
                        Left = Literal(8d),
                        Right = new ProjectGamePropertyExpressionDto { Kind = "subtract", Left = Literal(5d), Right = Literal(1d) }
                    },
                    Right = new ProjectGamePropertyExpressionDto { Kind = "multiply", Left = Literal(2d), Right = Literal(3d) }
                }
            }
        });
        return project;
    }

    private static ProjectGamePropertyExpressionDto ExistsExpression(string name) => new() { Kind = "exists", Source = new ProjectGamePropertySourceReferenceDto { PropertyName = name } };
    private static ProjectGamePropertyExpressionDto PropertyExpression(string name) => new() { Kind = "property", Source = new ProjectGamePropertySourceReferenceDto { PropertyName = name } };
    private static ProjectGamePropertyExpressionDto Literal(object value) => new() { Kind = "literal", Value = value };
}
