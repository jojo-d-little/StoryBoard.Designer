using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class RuntimeExportIdUniquenessRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenScopeIdsAreDuplicatedAcrossRuntimeNodes()
    {
        var duplicateId = Guid.NewGuid();

        var room = new Room
        {
            Name = "Room A",
            Id = duplicateId
        };

        var area = new Area
        {
            Name = "Area A",
            Id = duplicateId,
            Rooms = [room]
        };

        var project = BuildProject(area);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RuntimeExportIdUniquenessRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-013", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains($"{duplicateId:N}", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicated", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsIssue_WhenGameObjectIdsAreDuplicatedInContainedTree()
    {
        var duplicateId = Guid.NewGuid();
        var child = new GameObject
        {
            Name = "Child",
            ObjectId = duplicateId
        };

        var rootA = new GameObject
        {
            Name = "Root A",
            ContainedObjects = [child]
        };

        var rootB = new GameObject
        {
            Name = "Root B",
            ObjectId = duplicateId
        };

        var room = new Room
        {
            Name = "Room A",
            GameObjects = [rootA, rootB]
        };

        var area = new Area
        {
            Name = "Area A",
            Rooms = [room]
        };

        var project = BuildProject(area);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RuntimeExportIdUniquenessRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-013", issue.RuleId);
        Assert.Contains("GameObject", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenRuntimeScopeIdsAreUnique()
    {
        var room = new Room { Name = "Room A" };
        var area = new Area { Name = "Area A", Rooms = [room] };
        var project = BuildProject(area);

        var registry = new ValidationRuleRegistry();
        registry.Register(new RuntimeExportIdUniquenessRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-013", result.ExecutedRuleIds);
    }

    private static ProjectModel BuildProject(Area area)
    {
        var country = new Country { Name = "Country A", Areas = [area] };
        var planet = new Planet { Name = "Planet A", Countries = [country] };

        return new ProjectModel
        {
            Name = "RuntimeIdUniquenessProject",
            Planets = [planet]
        };
    }
}