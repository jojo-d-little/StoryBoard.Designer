using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.Tests;

public sealed class ValidationIssueRunProcessorTests
{
    [Fact]
    public void Process_ScopedNodeOnly_FiltersToRootScope()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out var room, out var roomObject);
        var roomPath = "Planet A / Country A / Area A / Room A";
        var roomObjectPath = roomObject.Name is { Length: > 0 } objectName
            ? $"{roomPath} / {objectName}"
            : $"{roomPath} / RoomObject";

        var issues = new List<ValidationIssue>
        {
            new("RULE-1", ValidationSeverity.Warning, roomPath, "Room issue"),
            new("RULE-3", ValidationSeverity.Warning, roomObjectPath, "Descendant issue"),
            new("RULE-4", ValidationSeverity.Warning, "Global / GlobalObject", "External scope issue")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.ScopedNodeOnly,
            room,
            IncludeDescendants: false,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Single(result.Issues);
        Assert.Contains(result.Issues, issue => issue.Path == roomPath);
        Assert.DoesNotContain(result.Issues, issue => issue.Path == roomObjectPath);
        Assert.DoesNotContain(result.Issues, issue => issue.Path == "Global / GlobalObject");
        Assert.False(result.StoppedEarly);
        Assert.Equal(0, result.RemainingIssueCountAtStop);
        Assert.Null(result.StopRuleId);
    }

    [Fact]
    public void Process_ScopedFromNode_WithDescendants_IncludesSubtreeAndExcludesExternalScopes()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out var room, out var roomObject);
        var roomPath = "Planet A / Country A / Area A / Room A";
        var roomObjectPath = roomObject.Name is { Length: > 0 } objectName
            ? $"{roomPath} / {objectName}"
            : $"{roomPath} / RoomObject";

        var issues = new List<ValidationIssue>
        {
            new("RULE-1", ValidationSeverity.Warning, roomPath, "Room issue"),
            new("RULE-2", ValidationSeverity.Warning, roomObjectPath, "Descendant issue"),
            new("RULE-3", ValidationSeverity.Warning, "Global / GlobalObject", "External scope issue")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.ScopedFromNode,
            room,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.Path == roomPath);
        Assert.Contains(result.Issues, issue => issue.Path == roomObjectPath);
        Assert.DoesNotContain(result.Issues, issue => issue.Path == "Global / GlobalObject");
        Assert.False(result.StoppedEarly);
        Assert.Equal(0, result.RemainingIssueCountAtStop);
        Assert.Null(result.StopRuleId);
    }

    [Fact]
    public void Process_StopOnFirstBlocking_TruncatesAtFirstErrorAndTracksRemainder()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out _, out _);
        var issues = new List<ValidationIssue>
        {
            new("RULE-1", ValidationSeverity.Warning, "Project", "Warning issue"),
            new("RULE-2", ValidationSeverity.Error, "Planet A / Country A / Area A / Room A", "Blocking issue"),
            new("RULE-3", ValidationSeverity.Warning, "Global / GlobalObject", "Trailing issue")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.StopOnFirstBlocking);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.True(result.StoppedEarly);
        Assert.Equal(1, result.RemainingIssueCountAtStop);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("RULE-1", result.Issues[0].RuleId);
        Assert.Equal("RULE-2", result.Issues[1].RuleId);
        Assert.Equal("RULE-2", result.StopRuleId);
    }

    [Fact]
    public void Process_WhenRuleIgnoredAtProjectLevel_SuppressesIssue()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out _, out _);
        project.IgnoredValidationRuleIds.Add("RULE-IGN");

        var issues = new List<ValidationIssue>
        {
            new("RULE-IGN", ValidationSeverity.Error, "Planet A / Country A / Area A / Room A", "Suppressed by project."),
            new("RULE-OK", ValidationSeverity.Warning, "Planet A / Country A / Area A / Room A", "Still visible.")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Single(result.Issues);
        Assert.Equal("RULE-OK", result.Issues[0].RuleId);
    }

    [Fact]
    public void Process_WhenRuleIgnoredAtAncestorScope_DoesNotSuppressDescendantIssue()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out var room, out _);
        room.IgnoredValidationRuleIds.Add("RULE-ROOM");

        var issues = new List<ValidationIssue>
        {
            new("RULE-ROOM", ValidationSeverity.Error, "Planet A / Country A / Area A / Room A / RoomObject", "Suppressed by room ancestor."),
            new("RULE-OTHER", ValidationSeverity.Warning, "Planet A / Country A / Area A / Room A / RoomObject", "Still visible.")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.RuleId == "RULE-ROOM");
        Assert.Contains(result.Issues, issue => issue.RuleId == "RULE-OTHER");
    }

    [Fact]
    public void Process_WhenRuleIgnoredAtExactScope_SuppressesOnlyThatScopeIssue()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out _, out var roomObject);
        roomObject.IgnoredValidationRuleIds.Add("RULE-OBJ");

        var issues = new List<ValidationIssue>
        {
            new("RULE-OBJ", ValidationSeverity.Error, "Planet A / Country A / Area A / Room A / RoomObject", "Suppressed at object scope."),
            new("RULE-OBJ", ValidationSeverity.Error, "Planet A / Country A / Area A / Room A", "Not suppressed at room scope.")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Single(result.Issues);
        Assert.Equal("Planet A / Country A / Area A / Room A", result.Issues[0].Path);
    }

    [Fact]
    public void Process_WhenRuleIgnoredAtObjectTemplatesCatalog_SuppressesTemplateItemIssue()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out _, out _);
        project.ObjectTemplates.Add(new GameObject
        {
            Name = "TemplateObject",
            ProducerNotes = "Template object notes"
        });
        project.ObjectTemplatesIgnoredValidationRuleIds.Add("RULE-TEMPLATE");

        ScopeHierarchy.AttachParents(project);

        var issues = new List<ValidationIssue>
        {
            new("RULE-TEMPLATE", ValidationSeverity.Error, "Global / Templates / TemplateObject", "Suppressed by templates catalog."),
            new("RULE-OTHER", ValidationSeverity.Warning, "Global / Templates / TemplateObject", "Still visible.")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Single(result.Issues);
        Assert.Equal("RULE-OTHER", result.Issues[0].RuleId);
    }

    [Fact]
    public void Process_WhenRuleIgnoredAtRoomTemplatesCatalog_SuppressesTemplateRoomDescendantIssue()
    {
        var project = BuildProjectWithRoomAndGlobalObject(out _, out _);
        var templateRoom = new Room
        {
            Name = "Template Room",
            GameObjects =
            [
                new GameObject
                {
                    Name = "TemplateObject"
                }
            ]
        };

        project.RoomTemplates.Add(templateRoom);
        project.RoomTemplatesIgnoredValidationRuleIds.Add("RULE-TEMPLATE-ROOM");

        ScopeHierarchy.AttachParents(project);

        var issues = new List<ValidationIssue>
        {
            new("RULE-TEMPLATE-ROOM", ValidationSeverity.Error, "Global / Room Templates / Template Room / TemplateObject", "Suppressed by room templates catalog."),
            new("RULE-OTHER", ValidationSeverity.Warning, "Global / Room Templates / Template Room / TemplateObject", "Still visible.")
        };

        var request = new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.WholeProject,
            RootScope: null,
            IncludeDescendants: true,
            CompletionMode: ValidationCompletionMode.FullReport);

        var result = ValidationIssueRunProcessor.Process(project, issues, request);

        Assert.Single(result.Issues);
        Assert.Equal("RULE-OTHER", result.Issues[0].RuleId);
    }

    private static ProjectModel BuildProjectWithRoomAndGlobalObject(out Room room, out GameObject roomObject)
    {
        roomObject = new GameObject { Name = "RoomObject", ProducerNotes = "Room object notes" };

        room = new Room
        {
            Name = "Room A",
            GameObjects = [roomObject]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room],
            StartingRoomId = room.Id
        };

        var country = new Country
        {
            Name = "Country A",
            Areas = [area],
            StartingAreaName = "Area A"
        };

        var planet = new Planet
        {
            Name = "Planet A",
            Countries = [country],
            StartingCountryName = "Country A"
        };

        var project = new ProjectModel
        {
            Name = "Validation Scope Project",
            StartingPlanetName = "Planet A",
            Planets = [planet],
            GameObjects =
            [
                new GameObject
                {
                    Name = "GlobalObject",
                    ProducerNotes = "Global object notes"
                }
            ]
        };

        ScopeHierarchy.AttachParents(project);
        return project;
    }
}