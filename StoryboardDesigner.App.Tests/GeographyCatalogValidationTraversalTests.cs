using Storyboard.Shared.DesignerContracts;
using Storyboard.Shared.RuntimeContracts.Dtos;
using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Actions;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class GeographyCatalogValidationTraversalTests
{
    [Fact]
    public void WholeProjectValidation_ReportsDisconnectedArrivalOnAreaBaseDefinition_NotLinkedPlacement()
    {
        var elevator = new GameObject { Name = "Elevator" };
        var arrival = DisconnectedArrival();
        elevator.SequenceDefinitions.Add(arrival);
        var placement = new GameObject
        {
            Name = "Elevator placement",
            LinkedBaseObjectId = elevator.ObjectId,
            LinkActionsToBaseObject = true
        };
        var area = new Area
        {
            Name = "Birmingham Area",
            BaseObjects = [elevator],
            Rooms = [new Room { Name = "Lift Room", GameObjects = [placement] }]
        };
        var project = ProjectWithArea(area);

        var registry = new ValidationRuleRegistry();
        registry.Register(new SequenceDefinitionIntegrityRule());
        registry.Register(new LinkedBaseObjectReferenceExistsRule());
        var result = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project));
        var rootIssue = Assert.Single(result.Issues, issue =>
            issue.RuleId == "SEQ-001" && issue.Description.Contains("2 root steps", StringComparison.Ordinal));

        Assert.Contains("Elevator", rootIssue.Path);
        Assert.Contains("Arrival", rootIssue.Path);
        Assert.Contains("Global / Birmingham / West Midlands / Birmingham Area", rootIssue.Path);
        Assert.Equal(ValidationSeverity.Error, rootIssue.Severity);
        Assert.Contains(result.Issues, issue => issue.RuleId == "SEQ-001"
                                                 && issue.Description.Contains("IsArrivalNeeded", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.RuleId == "SEQ-001"
                                                 && issue.Description.Contains("GrowElevator", StringComparison.Ordinal));
        Assert.Contains("SEQ-001", result.ExecutedRuleIds);
        Assert.Contains("OBJ-004", result.ExecutedRuleIds);
        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "OBJ-004");
        Assert.DoesNotContain(result.Issues, issue => issue.Path.Contains("Elevator placement", StringComparison.Ordinal));

        var growStep = arrival.Steps.Single(step => step.InlineAction?.Name == "GrowElevator");
        var arrivalStep = arrival.Steps.Single(step => step.InlineAction?.Name == "IsArrivalNeeded");
        arrivalStep.ContinuationMode = ContinuationMode.Continue;
        arrivalStep.OnContinueStepId = growStep.StepId;

        var connectedResult = Execute(project);
        Assert.DoesNotContain(connectedResult.Issues, issue =>
            issue.RuleId == "SEQ-001" && issue.Description.Contains("root steps", StringComparison.Ordinal));
    }

    [Fact]
    public void WholeProjectValidation_VisitsGlobalAndGeographyCatalogsAndNestedDefinitionsOnce()
    {
        var expectedSequenceNames = new HashSet<string>(StringComparer.Ordinal);
        GameObject Definition(string name)
        {
            var obj = new GameObject { Name = name };
            obj.SequenceDefinitions.Add(DisconnectedArrival(name));
            expectedSequenceNames.Add(name);
            return obj;
        }

        var globalBase = Definition("Global Base");
        var globalObjectTemplate = Definition("Global Object Template");
        var globalRoomObject = Definition("Global Room Object");
        var globalRoomTemplate = new Room { Name = "Global Room Template", GameObjects = [globalRoomObject] };
        globalRoomTemplate.SequenceDefinitions.Add(DisconnectedArrival("Global Room Template"));
        expectedSequenceNames.Add("Global Room Template");

        var planetBase = Definition("Planet Base");
        var planetTemplate = Definition("Planet Template");
        var planetTemplateChild = Definition("Planet Template Child");
        planetTemplate.ContainedObjects.Add(planetTemplateChild);
        var planetRoomObject = Definition("Planet Room Object");
        var planetRoomTemplate = new Room { Name = "Planet Room Template", GameObjects = [planetRoomObject] };
        planetRoomTemplate.SequenceDefinitions.Add(DisconnectedArrival("Planet Room Template"));
        expectedSequenceNames.Add("Planet Room Template");

        var countryBase = Definition("Country Base");
        var countryTemplate = Definition("Country Template");
        var countryTemplateChild = Definition("Country Template Child");
        countryTemplate.ContainedObjects.Add(countryTemplateChild);
        var countryRoomObject = Definition("Country Room Object");
        var roomTemplatePropertyId = Guid.NewGuid();
        countryRoomObject.Variables.Add(new GamePropertyDefinition { Id = roomTemplatePropertyId, Name = "roomTemplateValue" });
        var countryRoomTemplate = new Room { Name = "Country Room Template", GameObjects = [countryRoomObject] };
        countryRoomTemplate.SequenceDefinitions.Add(DisconnectedArrival("Country Room Template"));
        expectedSequenceNames.Add("Country Room Template");

        var areaBase = Definition("Area Base");
        var areaTemplate = Definition("Area Template");
        var areaTemplateChild = Definition("Area Template Child");
        areaTemplate.ContainedObjects.Add(areaTemplateChild);
        var areaRoomObject = Definition("Area Room Object");
        var areaRoomTemplate = new Room { Name = "Area Room Template", GameObjects = [areaRoomObject] };
        areaRoomTemplate.SequenceDefinitions.Add(DisconnectedArrival("Area Room Template"));
        expectedSequenceNames.Add("Area Room Template");

        var area = new Area
        {
            Name = "Catalog Area",
            BaseObjects = [areaBase],
            GameObjects = [areaBase], // Same definition can be encountered through both lists.
            ObjectTemplates = [areaTemplate],
            RoomTemplates = [areaRoomTemplate]
        };
        var country = new Country
        {
            Name = "Catalog Country",
            BaseObjects = [countryBase],
            ObjectTemplates = [countryTemplate],
            RoomTemplates = [countryRoomTemplate],
            Areas = [area]
        };
        var planet = new Planet
        {
            Name = "Catalog Planet",
            BaseObjects = [planetBase],
            ObjectTemplates = [planetTemplate],
            RoomTemplates = [planetRoomTemplate],
            Countries = [country]
        };
        var project = new ProjectModel
        {
            BaseObjects = [globalBase],
            ObjectTemplates = [globalObjectTemplate],
            RoomTemplates = [globalRoomTemplate],
            Planets = [planet]
        };

        var result = Execute(project);
        var sequenceIssues = result.Issues.Where(issue => issue.RuleId == "SEQ-001").ToList();
        var lookup = ValidationLookupService.Build(project);

        Assert.Contains("SEQ-001", result.ExecutedRuleIds);
        Assert.True(lookup.TryGetPropertyByVariableId(roomTemplatePropertyId, out var roomTemplateProperty));
        Assert.Equal("roomTemplateValue", roomTemplateProperty.VariableName);
        Assert.All(expectedSequenceNames, name => Assert.Contains(sequenceIssues, issue => issue.Path.Contains($"'{name}'", StringComparison.Ordinal)));
        Assert.Equal(expectedSequenceNames.Count, sequenceIssues
            .Where(issue => issue.Description.Contains("2 root steps", StringComparison.Ordinal))
            .Select(issue => issue.Path)
            .Distinct(StringComparer.Ordinal)
            .Count());
        Assert.Equal(3, sequenceIssues.Count(issue => issue.Path.Contains("Area Base", StringComparison.Ordinal)
                                                      && issue.Description.Contains("root", StringComparison.OrdinalIgnoreCase)));

        var planetRecursive = Execute(project, ValidationExecutionKind.ScopedFromNode, planet, includeDescendants: true);
        Assert.All(expectedSequenceNames.Except(["Global Base", "Global Object Template", "Global Room Object", "Global Room Template"]), name =>
            Assert.Contains(planetRecursive.Issues, issue => issue.RuleId == "SEQ-001" && issue.Path.Contains($"'{name}'", StringComparison.Ordinal)));
        Assert.DoesNotContain(planetRecursive.Issues, issue => issue.Path.Contains("Global Base", StringComparison.Ordinal));

        var areaRecursive = Execute(project, ValidationExecutionKind.ScopedFromNode, area, includeDescendants: true);
        Assert.Contains(areaRecursive.Issues, issue => issue.Path.Contains("Area Base", StringComparison.Ordinal));
        Assert.Contains(areaRecursive.Issues, issue => issue.Path.Contains("Area Template Child", StringComparison.Ordinal));
        Assert.Contains(areaRecursive.Issues, issue => issue.Path.Contains("Area Room Object", StringComparison.Ordinal));
        Assert.DoesNotContain(areaRecursive.Issues, issue => issue.Path.Contains("Country Base", StringComparison.Ordinal));
    }

    [Fact]
    public void RecursiveCountryValidation_IncludesItsCatalogsAndDescendantCatalogs_ButNonrecursiveDoesNot()
    {
        var countryBase = new GameObject { Name = "Country Base" };
        countryBase.SequenceDefinitions.Add(DisconnectedArrival("Country Base"));
        var countryTemplate = new GameObject { Name = "Country Template" };
        countryTemplate.SequenceDefinitions.Add(DisconnectedArrival("Country Template"));
        var areaBase = new GameObject { Name = "Area Base" };
        areaBase.SequenceDefinitions.Add(DisconnectedArrival("Area Base"));
        var planetBase = new GameObject { Name = "Planet Base" };
        planetBase.SequenceDefinitions.Add(DisconnectedArrival("Planet Base"));

        var area = new Area { Name = "Nested Area", BaseObjects = [areaBase] };
        var country = new Country
        {
            Name = "Recursive Country",
            BaseObjects = [countryBase],
            ObjectTemplates = [countryTemplate],
            Areas = [area]
        };
        var planet = new Planet { Name = "Parent Planet", BaseObjects = [planetBase], Countries = [country] };
        var project = new ProjectModel { Planets = [planet] };

        var recursive = Execute(project, ValidationExecutionKind.ScopedFromNode, country, includeDescendants: true);
        Assert.Contains(recursive.Issues, issue => issue.Path.Contains("Country Base", StringComparison.Ordinal));
        Assert.Contains(recursive.Issues, issue => issue.Path.Contains("Country Template", StringComparison.Ordinal));
        Assert.Contains(recursive.Issues, issue => issue.Path.Contains("Area Base", StringComparison.Ordinal));
        Assert.DoesNotContain(recursive.Issues, issue => issue.Path.Contains("Planet Base", StringComparison.Ordinal));

        var nonrecursive = Execute(project, ValidationExecutionKind.ScopedFromNode, country, includeDescendants: false);
        Assert.DoesNotContain(nonrecursive.Issues, issue => issue.RuleId == "SEQ-001");
        Assert.Equal(1, nonrecursive.CandidateNodeCount);
    }

    [Fact]
    public void GeographyBaseObjectCatalogIgnore_SuppressesSequenceIssuesForThatCatalog()
    {
        var baseObject = new GameObject { Name = "Ignored Base" };
        baseObject.SequenceDefinitions.Add(DisconnectedArrival("Ignored Arrival"));
        var area = new Area
        {
            Name = "Ignore Area",
            BaseObjects = [baseObject],
            BaseObjectsIgnoredValidationRuleIds = ["SEQ-001"]
        };
        var result = Execute(ProjectWithArea(area));

        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "SEQ-001");
        Assert.Contains("SEQ-001", result.ExecutedRuleIds);
    }

    [Fact]
    public void SequenceActionReachability_RecognizesActionsOwnedByGeographyBaseDefinitions()
    {
        var action = new CommandAction { Name = "Is arrival needed" };
        var elevator = new GameObject { Name = "Elevator" };
        elevator.AvailableActions.Add(action);
        elevator.SequenceDefinitions.Add(new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = "Use Elevator",
            Branches =
            [
                new ProjectSequenceBranchDto
                {
                    BranchId = "main",
                    DisplayName = "Main branch",
                    DisplayColor = "#64748B"
                }
            ],
            Steps =
            [
                new ProjectSequenceStepDto
                {
                    StepId = Guid.NewGuid(),
                    StepType = SequenceStepType.Action,
                    BranchId = "main",
                    ActionId = action.Id,
                    ContinuationMode = ContinuationMode.End
                }
            ]
        });
        var project = ProjectWithArea(new Area { Name = "Birmingham Area", BaseObjects = [elevator] });
        var registry = new ValidationRuleRegistry();
        registry.Register(new SequenceActionReferenceReachabilityRule());

        var result = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(
            project,
            CompletionMode: ValidationCompletionMode.FullReport));

        Assert.Contains("SEQ-003", result.ExecutedRuleIds);
        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "SEQ-003");
    }

    [Fact]
    public void LinkedPlacement_ResolvesActionsFromGeographyOwnedBaseObject()
    {
        var targetInOtherRoom = new GameObject { Name = "Other Room Target" };
        var baseObject = new GameObject { Name = "Area Base Definition" };
        baseObject.AvailableActions.Add(new CommandAction
        {
            Name = "Move to other room target",
            ActionType = CommandActionType.MoveRoomObjectOnGrid,
            MoveTargetObjectId = targetInOtherRoom.ObjectId
        });
        var placement = new GameObject
        {
            Name = "Area Base Placement",
            LinkedBaseObjectId = baseObject.ObjectId,
            LinkActionsToBaseObject = true
        };
        var area = new Area
        {
            Name = "Action Lookup Area",
            BaseObjects = [baseObject],
            Rooms =
            [
                new Room { Name = "Action Owner Room", GameObjects = [placement] },
                new Room { Name = "Other Room", GameObjects = [targetInOtherRoom] }
            ]
        };
        var project = ProjectWithArea(area);
        var registry = new ValidationRuleRegistry();
        registry.Register(new ActionObjectTargetReachabilityRule());

        var result = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(project));

        Assert.Contains("ACT-018", result.ExecutedRuleIds);
        Assert.Contains(result.Issues, issue => issue.RuleId == "ACT-018"
                                                 && issue.Path.Contains("Area Base Placement", StringComparison.Ordinal)
                                                 && issue.Description.Contains("Other Room Target", StringComparison.Ordinal));
    }

    [Fact]
    public void InheritedActionTargetLookup_IncludesGeographyOwnedBaseDefinitions()
    {
        var targetDefinition = new GameObject { Name = "Door Target Definition" };
        var actionDefinition = new GameObject { Name = "Elevator Definition" };
        actionDefinition.AvailableActions.Add(new CommandAction
        {
            Name = "Open left door",
            ActionType = CommandActionType.MoveRoomObjectOnGrid,
            MoveTargetObjectId = targetDefinition.ObjectId
        });
        var placement = new GameObject
        {
            Name = "Elevator Placement",
            LinkedBaseObjectId = actionDefinition.ObjectId,
            LinkActionsToBaseObject = true
        };
        var area = new Area
        {
            Name = "Elevator Area",
            BaseObjects = [actionDefinition, targetDefinition],
            Rooms = [new Room { Name = "Foyer", GameObjects = [placement] }]
        };
        var registry = new ValidationRuleRegistry();
        registry.Register(new ActionObjectTargetReachabilityRule());

        var result = new ValidationEngine(registry).Execute(new ValidationExecutionRequest(ProjectWithArea(area)));

        Assert.Contains("ACT-018", result.ExecutedRuleIds);
        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "ACT-018");
    }

    private static ValidationExecutionResult Execute(
        ProjectModel project,
        ValidationExecutionKind executionKind = ValidationExecutionKind.WholeProject,
        ScopeNodeBase? root = null,
        bool includeDescendants = true)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new SequenceDefinitionIntegrityRule());
        return new ValidationEngine(registry).Execute(new ValidationExecutionRequest(
            project,
            executionKind,
            root,
            includeDescendants,
            ValidationCompletionMode.FullReport));
    }

    private static ProjectModel ProjectWithArea(Area area) => new()
    {
        Name = "Validation Project",
        Planets =
        [
            new Planet
            {
                Name = "Birmingham",
                Countries =
                [
                    new Country
                    {
                        Name = "West Midlands",
                        Areas = [area]
                    }
                ]
            }
        ]
    };

    private static ProjectSequenceDefinitionDto DisconnectedArrival(string name = "Arrival")
    {
        return new ProjectSequenceDefinitionDto
        {
            SequenceId = Guid.NewGuid(),
            DisplayName = name,
            Branches =
            [
                new ProjectSequenceBranchDto
                {
                    BranchId = "main",
                    DisplayName = "Main",
                    DisplayColor = "#64748B"
                }
            ],
            Steps =
            [
                ActionStep("IsArrivalNeeded"),
                ActionStep("GrowElevator")
            ]
        };
    }

    private static ProjectSequenceStepDto ActionStep(string actionName) => new()
    {
        StepId = Guid.NewGuid(),
        StepType = SequenceStepType.Action,
        BranchId = "main",
        ContinuationMode = ContinuationMode.End,
        InlineAction = JsonExportService.ToCommandActionDtoForSequence(new CommandAction { Name = actionName }, [])
    };
}
