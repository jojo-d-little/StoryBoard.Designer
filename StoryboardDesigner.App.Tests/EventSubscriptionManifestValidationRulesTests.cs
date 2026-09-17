using Storyboard.Shared.RuntimeContracts.Enums;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Project;

namespace StoryboardDesigner.App.Tests;

public sealed class EventSubscriptionManifestValidationRulesTests
{
    [Fact]
    public void EventKeyRule_ReportsIssue_WhenEventKeyIsNotInManifest()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "event.does.not.exist",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionManifestEventKeyRule());

        Assert.Contains(result.ExecutedRuleIds, id => id == "EVT-001");
        Assert.Contains(result.Issues, issue => issue.RuleId == "EVT-001");
    }

    [Fact]
    public void FilterVariableSyntaxRule_ReportsIssue_ForAnchorPathMissingVariableSegment()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "currentCommand::primaryCommandObject",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionFilterVariableSyntaxRule());

        Assert.Contains(result.ExecutedRuleIds, id => id == "EVT-002");
        var issue = Assert.Single(result.Issues, issue => issue.RuleId == "EVT-002");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    [Fact]
    public void AnchorSubPropertyRule_DoesNotReportIssue_WhenAnchorSubPropertyCatalogIsUnavailable()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "currentAction::primaryCommandObject.isBent",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionAnchorSubPropertyReferenceRule());

        Assert.Contains(result.ExecutedRuleIds, id => id == "EVT-003");
        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "EVT-003");
    }

    [Fact]
    public void AnchorSubPropertyRule_ReportsHardError_ForUnknownAnchorRoot()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "notAnAnchor::scope.value",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionAnchorSubPropertyReferenceRule());

        var issue = Assert.Single(result.Issues, issue => issue.RuleId == "EVT-003");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    [Fact]
    public void DuplicateFilterRule_ReportsIssue_WhenEquivalentRowsRepeat()
    {
        var duplicateFilter = new EventBindingFilterConditionDefinition
        {
            VariableName = "procedureId",
            Operator = RuntimeVariableComparisonOperator.Equals,
            ExpectedValue = "true"
        };

        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            duplicateFilter,
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = duplicateFilter.VariableName,
                                Operator = duplicateFilter.Operator,
                                ExpectedValue = duplicateFilter.ExpectedValue
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionDuplicateFilterConditionRule());

        Assert.Contains(result.ExecutedRuleIds, id => id == "EVT-005");
        Assert.Contains(result.Issues, issue => issue.RuleId == "EVT-005");
    }

    [Fact]
    public void AmbiguousPathRule_ReportsIssue_ForLegacySubPropertyPathWithoutAnchorRoot()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "primaryCommandObject.isBent",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionAmbiguousAnchorLikePathRule());

        Assert.Contains(result.ExecutedRuleIds, id => id == "EVT-006");
        Assert.Contains(result.Issues, issue => issue.RuleId == "EVT-006");
    }

    [Fact]
    public void AnchorDynamicTailRule_ReportsSoftIssue_ForDynamicTailAfterValidSubProperty()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "currentAction::scope.inventory.lastAdded.name",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionAnchorDynamicTailReferenceRule());

        var issue = Assert.Single(result.Issues, issue => issue.RuleId == "EVT-007");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void AnchorDynamicTailRule_DoesNotReportIssue_ForIntrinsicNameLeaves()
    {
        var project = CreateProjectWithSubscription(new EventSubscriptionDefinition
        {
            EventKey = "procedure.started",
            ActionBindings =
            [
                new EventActionBindingDefinition
                {
                    Order = 1,
                    Target = new EventBindingTargetDefinition { ActionName = "Inspect" },
                    Condition = new EventBindingConditionDefinition
                    {
                        Filters =
                        [
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "currentAction::scope.nameInGame",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            },
                            new EventBindingFilterConditionDefinition
                            {
                                VariableName = "currentAction::scope.name",
                                Operator = RuntimeVariableComparisonOperator.Equals,
                                ExpectedValue = "true"
                            }
                        ]
                    }
                }
            ]
        });

        var result = Evaluate(project, new EventSubscriptionAnchorDynamicTailReferenceRule());

        Assert.DoesNotContain(result.Issues, issue => issue.RuleId == "EVT-007");
    }

    private static ValidationExecutionResult Evaluate(ProjectModel project, IValidationRule rule)
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(rule);
        var engine = new ValidationEngine(registry);
        return engine.Execute(new ValidationExecutionRequest(project));
    }

    private static ProjectModel CreateProjectWithSubscription(EventSubscriptionDefinition subscription)
    {
        var room = new Room
        {
            Name = "Event Room",
            EventSubscriptions = [subscription]
        };

        var area = new Area { Name = "Area", Rooms = [room] };
        var country = new Country { Name = "Country", Areas = [area] };
        var planet = new Planet { Name = "Planet", Countries = [country] };
        return new ProjectModel { Name = "Events", Planets = [planet] };
    }
}
