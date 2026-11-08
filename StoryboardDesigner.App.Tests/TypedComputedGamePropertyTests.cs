using System.Text.Json;
using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Rules.Project;
using StoryboardDesigner.App.ViewModels;

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
    public void SharedComputedWriterCount_IgnoresInheritedPlacementViewsOfBaseInternalGroup()
    {
        var groupId = Guid.NewGuid();
        var computed = ComputedBoolean("DoorComputed", groupId);
        var (project, baseObject, room) = BuildLinkedPlacements(computed);
        room.GameObjects.Add(new GameObject { Name = "Elevator B", LinkedBaseObjectId = baseObject.ObjectId });

        var issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.DoesNotContain(issues, issue => issue.Description.Contains("more than one computed writer", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(room.GameObjects[0].Variables);
        Assert.Empty(room.GameObjects[1].Variables);
        Assert.Equal(groupId, computed.SharedVariableId);
    }

    [Fact]
    public void SharedComputedWriterCount_ReportsTwoComputedWritersAuthoredInBase()
    {
        var groupId = Guid.NewGuid();
        var first = ComputedBoolean("DoorComputed", groupId);
        var second = ComputedBoolean("DoorComputedMirror", groupId);
        var (project, _, _) = BuildLinkedPlacements(first);
        project.BaseObjects.Single().Variables.Add(second);

        var issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.Contains(issues, issue => issue.Description.Contains("more than one computed writer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InstanceSpecificShareWithAnotherComputedWriter_IsRejectedByComputedValidation()
    {
        var internalGroupId = Guid.NewGuid();
        var baseProperty = ComputedBoolean("DoorComputed", internalGroupId);
        var (project, baseObject, room) = BuildLinkedPlacements(baseProperty);
        var placement = room.GameObjects.Single();
        var otherWriter = new GameObject
        {
            Name = "Sensor",
            Variables = [ComputedBoolean("SensorComputed", null)]
        };
        room.GameObjects.Add(otherWriter);
        var node = CreatePlacementPropertyNode(project, placement);
        var treeContext = new TreeContextInteractionServiceStub
        {
            ChooseVariableHandler = (choices, _, _, _) =>
                (true, choices.Single(choice => choice.OwnerVariable.Contains("SensorComputed", StringComparison.OrdinalIgnoreCase)).Value)
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());

        Assert.True(viewModel.ExecuteTreeContextAction("share-game-property", node));

        var local = Assert.Single(placement.Variables);
        Assert.NotEqual(baseProperty.Id, local.Id);
        Assert.Equal(internalGroupId, baseProperty.SharedVariableId);
        Assert.Equal(local.SharedVariableId, otherWriter.Variables.Single().SharedVariableId);
        var placementLocalId = local.Id;
        var groupId = local.SharedVariableId;
        var issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();
        Assert.Contains(issues,
            issue => issue.Description.Contains("more than one computed writer", StringComparison.OrdinalIgnoreCase));
        Assert.Same(local, Assert.Single(placement.Variables));
        Assert.Equal(placementLocalId, local.Id);
        Assert.Equal(groupId, local.SharedVariableId);
        Assert.Equal(internalGroupId, baseProperty.SharedVariableId);
    }

    [Fact]
    public void ExternalGroupWithTwoComputedPlacementWriters_IsReported()
    {
        var baseGroupId = Guid.NewGuid();
        var baseProperty = ComputedBoolean("DoorComputed", baseGroupId);
        var (project, baseObject, room) = BuildLinkedPlacements(baseProperty);
        var secondPlacement = new GameObject { Name = "Elevator B", LinkedBaseObjectId = baseObject.ObjectId };
        room.GameObjects.Add(secondPlacement);
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, new TreeContextInteractionServiceStub(), new ProjectUiServiceStub());
        var firstNode = CreatePlacementPropertyNode(project, room.GameObjects[0]);
        var secondNode = CreatePlacementPropertyNode(project, secondPlacement);
        Assert.True(viewModel.SaveInstancePropertyValue(firstNode, "false"));
        Assert.True(viewModel.SaveInstancePropertyValue(secondNode, "true"));
        var externalGroupId = Guid.NewGuid();
        room.GameObjects[0].Variables.Single().SharedVariableId = externalGroupId;
        secondPlacement.Variables.Single().SharedVariableId = externalGroupId;
        var firstLocal = room.GameObjects[0].Variables.Single();
        var secondLocal = secondPlacement.Variables.Single();
        var firstId = firstLocal.Id;
        var secondId = secondLocal.Id;

        var issues = new ComputedGamePropertyIntegrityRule().Evaluate(ValidationRuleTestContext.Create(project)).ToList();

        Assert.Contains(issues, issue => issue.Description.Contains("more than one computed writer", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(firstId, firstLocal.Id);
        Assert.Equal(secondId, secondLocal.Id);
        Assert.Equal(externalGroupId, firstLocal.SharedVariableId);
        Assert.Equal(externalGroupId, secondLocal.SharedVariableId);
        Assert.Equal(baseGroupId, baseProperty.SharedVariableId);
    }

    [Fact]
    public void IntegrityRule_ReportsMalformedNestedNodesAndMissingSources()
    {
        var project = new ProjectModel();
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "computed", DefaultValue = "false", ValueRestriction = GamePropertyValueRestriction.TrueFalse,
            Computation = new ProjectGamePropertyComputationDto
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
        var computation = new ProjectGamePropertyComputationDto
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
    public void ProjectJson_RoundTripsFinalComputationField()
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
            Assert.Contains("\"computation\"", savedText);
            Assert.DoesNotContain("\"computationV2\"", savedText);

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
            Assert.True(runtimeProperty.TryGetProperty("computation", out _));
            Assert.False(runtimeProperty.TryGetProperty("computationV2", out _));

            var runtimeArithmetic = runtime.RootElement.GetProperty("gameProperties").EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == "scoreArithmetic");
            var arithmeticExpression = runtimeArithmetic.GetProperty("computation").GetProperty("expression");
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
            Computation = new ProjectGamePropertyComputationDto { OutputType = "Boolean", Expression = ExistsExpression("enabled") }
        });
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "scoreAdjusted", DefaultValue = "0", ValueRestriction = GamePropertyValueRestriction.Numeric,
            Computation = new ProjectGamePropertyComputationDto
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
            Computation = new ProjectGamePropertyComputationDto
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
            Computation = new ProjectGamePropertyComputationDto
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

    private static GamePropertyDefinition ComputedBoolean(string name, Guid? sharedId) => new()
    {
        Name = name,
        DefaultValue = "false",
        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
        SharedVariableId = sharedId,
        Computation = new ProjectGamePropertyComputationDto
        {
            OutputType = "Boolean",
            Expression = Literal(false)
        }
    };

    private static (ProjectModel Project, GameObject BaseObject, Room Room) BuildLinkedPlacements(GamePropertyDefinition baseProperty)
    {
        var baseObject = new GameObject { Name = "Elevator", Variables = [baseProperty] };
        var placement = new GameObject { Name = "Elevator A", LinkedBaseObjectId = baseObject.ObjectId };
        var room = new Room { Name = "Foyer", GameObjects = [placement] };
        var project = new ProjectModel
        {
            BaseObjects = [baseObject],
            Planets = [new Planet { Name = "Earth", Countries = [new Country { Name = "Country", Areas = [new Area { Name = "Area", Rooms = [room] }] }] }]
        };
        return (project, baseObject, room);
    }

    private static GamePropertyNodeViewModel CreatePlacementPropertyNode(ProjectModel project, GameObject placement)
    {
        ScopeHierarchy.AttachParents(project);
        var root = new ProjectRootNodeViewModel(project);
        var method = typeof(MainWindowViewModel).GetMethod("CreateVariablesNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var container = Assert.IsType<GamePropertiesContainerNodeViewModel>(method.Invoke(null,
            [root, placement, placement.Variables, PropertyResolutionScope.Object, null, null]));
        return Assert.Single(container.Children.OfType<GamePropertyNodeViewModel>());
    }

    private static ProjectGamePropertyExpressionDto ExistsExpression(string name) => new() { Kind = "exists", Source = new ProjectGamePropertySourceReferenceDto { PropertyName = name } };
    private static ProjectGamePropertyExpressionDto PropertyExpression(string name) => new() { Kind = "property", Source = new ProjectGamePropertySourceReferenceDto { PropertyName = name } };
    private static ProjectGamePropertyExpressionDto Literal(object value) => new() { Kind = "literal", Value = value };
}
