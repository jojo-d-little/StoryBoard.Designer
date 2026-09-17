using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SharedVariableConsistencyRulesTests
{
    [Fact]
    public void InitialValueConsistency_ReportsWarning_WhenSharedParticipantsDisagreeOnDefaultValue()
    {
        var variableA = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        var variableB = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isPassable",
            DefaultValue = "true",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };

        var shared = new SharedVariableDefinition
        {
            Id = Guid.NewGuid(),
            Name = "DoorState"
        };

        variableA.SharedVariableId = shared.Id;
        variableB.SharedVariableId = shared.Id;

        var project = new ProjectModel
        {
            GlobalVariables = [variableA, variableB],
            SharedVariables = [shared]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableInitialValueConsistencyRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-005", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("DoorState", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("isOpen='false'", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("isPassable='true'", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitialValueConsistency_DoesNotReport_WhenSharedParticipantsUseSameDefaultValue()
    {
        var variableA = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        var variableB = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isPassable",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };

        var sharedId = Guid.NewGuid();
        variableA.SharedVariableId = sharedId;
        variableB.SharedVariableId = sharedId;

        var project = new ProjectModel
        {
            GlobalVariables = [variableA, variableB],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = sharedId,
                    Name = "DoorState"
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableInitialValueConsistencyRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void RestrictionConsistency_ReportsWarning_WhenSharedParticipantsDisagreeOnRestriction()
    {
        var variableA = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        var variableB = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isPassable",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.Unrestricted
        };

        var shared = new SharedVariableDefinition
        {
            Id = Guid.NewGuid(),
            Name = "DoorState"
        };

        variableA.SharedVariableId = shared.Id;
        variableB.SharedVariableId = shared.Id;

        var project = new ProjectModel
        {
            GlobalVariables = [variableA, variableB],
            SharedVariables = [shared]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableValueRestrictionConsistencyRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-006", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("DoorState", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrueFalse", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unrestricted", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyParticipantDrift_ReportsWarning_WhenPersistedParticipantsDoNotMatchLinkedVariables()
    {
        var linkedVariable = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };

        var staleVariableId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        linkedVariable.SharedVariableId = sharedId;

        var shared = new SharedVariableDefinition
        {
            Id = sharedId,
            Name = "DoorState",
            Participants =
            [
                new SharedVariableParticipant
                {
                    Kind = "global",
                    OwnerId = staleVariableId,
                    VariableName = "legacyName"
                }
            ]
        };

        var project = new ProjectModel
        {
            GlobalVariables = [linkedVariable],
            SharedVariables = [shared]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableLegacyParticipantDriftRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-007", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("DoorState", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stale=1", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing=1", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyParticipantDrift_ReportsMissingParticipant_WhenLinkedVariableIsOnAreaScopedObject()
    {
        var sharedId = Guid.NewGuid();
        var areaObject = new GameObject
        {
            Name = "Area Cache",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "isOpen",
                    SharedVariableId = sharedId,
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ]
        };

        var shared = new SharedVariableDefinition
        {
            Id = sharedId,
            Name = "DoorState",
            Participants =
            [
                new SharedVariableParticipant
                {
                    Kind = "global",
                    OwnerId = Guid.NewGuid(),
                    VariableName = "legacyName"
                }
            ]
        };

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Terra",
                    Countries =
                    [
                        new Country
                        {
                            Name = "USA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Lab",
                                    GameObjects = [areaObject]
                                }
                            ]
                        }
                    ]
                }
            ],
            SharedVariables = [shared]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableLegacyParticipantDriftRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-007", issue.RuleId);
        Assert.Contains("missing=1", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OrphanMetadataShell_ReportsWarning_WhenSharedHasNoLinkedVariables()
    {
        var shared = new SharedVariableDefinition
        {
            Id = Guid.NewGuid(),
            Name = "UnusedShare"
        };

        var project = new ProjectModel
        {
            SharedVariables = [shared]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableOrphanMetadataShellRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-008", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("UnusedShare", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateDisplayName_ReportsWarning_WhenNamesRepeatAcrossIds()
    {
        var project = new ProjectModel
        {
            SharedVariables =
            [
                new SharedVariableDefinition { Id = Guid.NewGuid(), Name = "Door State" },
                new SharedVariableDefinition { Id = Guid.NewGuid(), Name = "door state" }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableDuplicateDisplayNameRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-009", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Door State", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestrictionConsistency_DoesNotReport_WhenSharedParticipantsUseSameRestriction()
    {
        var variableA = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };
        var variableB = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isPassable",
            DefaultValue = "false",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        };

        var sharedId = Guid.NewGuid();
        variableA.SharedVariableId = sharedId;
        variableB.SharedVariableId = sharedId;

        var project = new ProjectModel
        {
            GlobalVariables = [variableA, variableB],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = sharedId,
                    Name = "DoorState"
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableValueRestrictionConsistencyRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
    }
}