using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class ProcedureRequiresMutationRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenProcedureHasNoMutations()
    {
        var project = new ProjectModel
        {
            Procedures =
            [
                new ProcedureDefinition
                {
                    Name = "Inspect Room"
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ProcedureRequiresMutationRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-011", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("Project / Procedures / Inspect Room", issue.Path);
    }

    [Fact]
    public void Evaluate_DoesNotReport_WhenProcedureHasMutation()
    {
        var project = new ProjectModel
        {
            Procedures =
            [
                new ProcedureDefinition
                {
                    Name = "Unlock Door",
                    ParticipantMutations =
                    [
                        new ProcedureParticipantMutation
                        {
                            ObjectId = Guid.NewGuid(),
                            Operation = ProcedureParticipantMutationOperation.SetVariable,
                            VariableName = "isOpen",
                            Value = "true"
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ProcedureRequiresMutationRule());
        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
        Assert.Contains("PROJ-011", result.ExecutedRuleIds);
    }
}