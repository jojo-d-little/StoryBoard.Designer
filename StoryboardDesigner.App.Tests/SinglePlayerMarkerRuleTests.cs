using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class SinglePlayerMarkerRuleTests
{
    [Fact]
    public void Evaluate_ReportsIssue_WhenNoObjectMarkedIsPlayer()
    {
        var project = BuildProjectWithPlayerObjects(
            new GameObject
            {
                Name = "Hero",
                IsContainer = true,
                ContainerPointsDefaultValue = 3,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "false",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            });

        var issues = Execute(project);

        var issue = Assert.Single(issues);
        Assert.Equal("PROJ-004", issue.RuleId);
        Assert.Equal("Project", issue.Path);
        Assert.Contains("No game object is marked as player", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsIssue_WhenMultipleObjectsMarkedIsPlayer()
    {
        var project = BuildProjectWithPlayerObjects(
            new GameObject
            {
                Name = "Hero",
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            },
            new GameObject
            {
                Name = "Sidekick",
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            });

        var issues = Execute(project);

        var issue = Assert.Single(issues);
        Assert.Equal("PROJ-004", issue.RuleId);
        Assert.Contains("Multiple game objects are marked as player", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Global / Hero", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Global / Sidekick", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_NoIssue_WhenExactlyOneObjectMarkedIsPlayer()
    {
        var project = BuildProjectWithPlayerObjects(
            new GameObject
            {
                Name = "Hero",
                IsContainer = true,
                ContainerPointsDefaultValue = 3,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            },
            new GameObject
            {
                Name = "Sidekick",
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "false",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            });

        var issues = Execute(project);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_ReportsIssue_WhenMarkedPlayerIsNotContainer()
    {
        var project = BuildProjectWithPlayerObjects(
            new GameObject
            {
                Name = "Hero",
                IsContainer = false,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            });

        var issues = Execute(project);

        var issue = Assert.Single(issues);
        Assert.Equal("PROJ-004", issue.RuleId);
        Assert.Equal("Global / Hero", issue.Path);
        Assert.Contains("not configured as a container", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_NoIssue_WhenMarkedPlayerIsContainerWithPositiveCapacity()
    {
        var project = BuildProjectWithPlayerObjects(
            new GameObject
            {
                Name = "Hero",
                IsContainer = true,
                ContainerPointsDefaultValue = 5,
                Variables =
                [
                    new GamePropertyDefinition
                    {
                        Name = "isPlayer",
                        DefaultValue = "true",
                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                    }
                ]
            });

        var issues = Execute(project);

        Assert.Empty(issues);
    }

    private static ProjectModel BuildProjectWithPlayerObjects(params GameObject[] playerObjects)
    {
        return new ProjectModel
        {
            Name = "SinglePlayerRuleFixture",
            GlobalObjectScopeName = "Global Objects",
            GameObjects = playerObjects.ToList()
        };
    }

    private static List<StoryboardDesigner.App.Validation.Contracts.ValidationIssue> Execute(ProjectModel project)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new SinglePlayerMarkerRule());

        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();
    }
}
