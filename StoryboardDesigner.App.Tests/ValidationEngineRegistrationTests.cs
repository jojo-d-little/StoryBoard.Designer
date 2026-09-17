using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Validation.Rules.Project;
using StoryboardDesigner.App.Validation.Rules.Scripting;

namespace StoryboardDesigner.App.Tests;

public sealed class ValidationEngineRegistrationTests
{
    [Fact]
    public void Execute_WithRegisteredInventoriableGlobalUniquenessRule_RunsRuleAndReturnsIssues()
    {
        var project = new ProjectModel
        {
            GameObjects = [new GameObject { Name = "Key", IsInventoriable = true }],
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects = [new GameObject { Name = "Key", IsInventoriable = true }]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new InventoriableGlobalUniquenessRule());

        var engine = new ValidationEngine(registry);

        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("OBJ-002", result.ExecutedRuleIds);
        Assert.Empty(result.SkippedRules);
        Assert.Equal(ValidationExecutionKind.WholeProject, result.ExecutionKind);
        Assert.Equal("Global", result.RootScopePath);
        Assert.True(result.IncludeDescendants);
        Assert.True(result.CandidateNodeCount > 0);
        Assert.False(result.StoppedEarly);
        Assert.Null(result.StopRuleId);
        Assert.Equal(0, result.RemainingRuleCountAtStop);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("OBJ-002", issue.RuleId);
    }

    [Fact]
    public void Execute_WithRuleUnsupportedByScopedRoot_RecordsUnsupportedScopeSkip()
    {
        var project = new ProjectModel();
        var room = new Room { Name = "Scoped Room" };
        var registry = new ValidationRuleRegistry();
        registry.Register(new InventoriableGlobalUniquenessRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(
            project,
            ValidationExecutionKind.ScopedNodeOnly,
            room,
            IncludeDescendants: false,
            CompletionMode: ValidationCompletionMode.FullReport));

        Assert.Empty(result.ExecutedRuleIds);
        var skipped = Assert.Single(result.SkippedRules);
        Assert.Equal("OBJ-002", skipped.RuleId);
        Assert.Equal(ValidationSkipReason.UnsupportedScopeType, skipped.SkipReason);
        Assert.Equal("Scoped Room", result.RootScopePath);
        Assert.Equal(1, result.CandidateNodeCount);
    }

    [Fact]
    public void Execute_WithIneligibleCandidate_RecordsSkipReasonFromEligibility()
    {
        var project = new ProjectModel();
        var registry = new ValidationRuleRegistry();
        registry.Register(new AlwaysIneligibleProjectRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.ExecutedRuleIds);
        var skipped = Assert.Single(result.SkippedRules);
        Assert.Equal("TEST-INELIGIBLE", skipped.RuleId);
        Assert.Equal(ValidationSkipReason.MissingRequiredFacet, skipped.SkipReason);
        Assert.Equal("Missing scripted content facet.", skipped.SkipDetail);
    }

    [Fact]
    public void Execute_WithRegisteredInventoriableGlobalUniquenessRule_ReturnsIssue()
    {
        var project = new ProjectModel
        {
            GameObjects = [new GameObject { Name = "Key", IsInventoriable = true }],
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "RoomA",
                                            GameObjects = [new GameObject { Name = "Key", IsInventoriable = true }]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new InventoriableGlobalUniquenessRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("OBJ-002", result.ExecutedRuleIds);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("OBJ-002", issue.RuleId);
    }

    [Fact]
    public void Register_DuplicateRuleId_Throws()
    {
        var registry = new ValidationRuleRegistry();
        registry.Register(new InventoriableGlobalUniquenessRule());

        Assert.Throws<InvalidOperationException>(() => registry.Register(new InventoriableGlobalUniquenessRule()));
    }

    [Fact]
    public void Execute_WithRegisteredScriptUnknownReferenceRule_ReturnsWarningIssue()
    {
        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area A",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "Room A",
                                            AvailableActions =
                                            [
                                                new CommandAction
                                                {
                                                    ActionType = CommandActionType.EchoMessage,
                                                    EchoMessage = "ECHO {missingToken}",
                                                    Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptUnknownReferenceRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("SCR-001", result.ExecutedRuleIds);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("SCR-001", issue.RuleId);
    }

    [Fact]
    public void Execute_WithRegisteredEventSubscriptionManifestEventKeyRule_ReturnsWarningIssue()
    {
        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Planet",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "Room",
                                            EventSubscriptions =
                                            [
                                                new EventSubscriptionDefinition
                                                {
                                                    EventKey = "event.not.in.manifest",
                                                    ActionBindings =
                                                    [
                                                        new EventActionBindingDefinition
                                                        {
                                                            Order = 1,
                                                            Target = new EventBindingTargetDefinition { ActionName = "Inspect" }
                                                        }
                                                    ]
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new EventSubscriptionManifestEventKeyRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("EVT-001", result.ExecutedRuleIds);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("EVT-001", issue.RuleId);
    }

    [Fact]
    public void Execute_ScriptUnknownReferenceRule_IgnoresStaleLocalActionsOnLinkedRoomInstance()
    {
        var baseObject = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Marbles",
            AvailableActions = new List<CommandAction>()
        };

        var linkedCopy = new GameObject
        {
            ObjectId = Guid.NewGuid(),
            Name = "Marbles",
            LinkedBaseObjectId = baseObject.ObjectId,
            LinkActionsToBaseObject = true,
            AvailableActions =
            [
                new CommandAction
                {
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "ECHO {missingToken}",
                    Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                }
            ]
        };

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "Planet A",
                    Countries =
                    [
                        new Country
                        {
                            Name = "Country A",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "Area A",
                                    Rooms =
                                    [
                                        new Room
                                        {
                                            Name = "Room A",
                                            GameObjects = [baseObject]
                                        },
                                        new Room
                                        {
                                            Name = "Room B",
                                            GameObjects = [linkedCopy]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new ScriptUnknownReferenceRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Empty(result.Issues);
        Assert.DoesNotContain("SCR-001", result.ExecutedRuleIds);
        Assert.Contains(result.SkippedRules, skipped =>
            string.Equals(skipped.RuleId, "SCR-001", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_WithRegisteredSharedVariableSingleMembershipRule_ReturnsProjectIssue()
    {
        var variable = new GamePropertyDefinition
        {
            Id = Guid.NewGuid(),
            Name = "isOpen"
        };

        var project = new ProjectModel
        {
            GlobalVariables = [variable],
            SharedVariables =
            [
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareA",
                    Participants = [new SharedVariableParticipant { Kind = "global", OwnerId = variable.Id, VariableName = variable.Name }]
                },
                new SharedVariableDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "ShareB",
                    Participants = [new SharedVariableParticipant { Kind = "global", OwnerId = variable.Id, VariableName = variable.Name }]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new SharedVariableSingleMembershipRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("PROJ-003", result.ExecutedRuleIds);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROJ-003", issue.RuleId);
    }

    [Fact]
    public void Execute_WithRegisteredTraversalRequireOpenSharedIsOpenRule_ReturnsTraversalWarning()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        roomA.GameObjects.Add(new GameObject { Name = "North Door", IsOpenable = true });

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms = [roomA, roomB],
                                    TraversalConnections =
                                    [
                                        new TraversalConnection
                                        {
                                            RoomAId = roomA.Id,
                                            RoomBId = roomB.Id,
                                            TraversalStateFromA = new TraversalLegState
                                            {
                                                OpenStatePolicy = OpenablePolicy.RequireOpen,
                                                Variables =
                                                [
                                                    new GamePropertyDefinition
                                                    {
                                                        Name = "isPassable",
                                                        DefaultValue = "true",
                                                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                                                    }
                                                ]
                                            },
                                            TraversalStateFromB = new TraversalLegState
                                            {
                                                Variables =
                                                [
                                                    new GamePropertyDefinition
                                                    {
                                                        Name = "isPassable",
                                                        DefaultValue = "true",
                                                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                                                    }
                                                ]
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalRequireOpenSharedIsOpenRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("TRV-001", result.ExecutedRuleIds);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("TRV-001", issue.RuleId);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Execute_WithRegisteredTraversalParityRules_ExecutesEachRule()
    {
        var roomA = new Room { Name = "A" };
        var roomB = new Room { Name = "B" };
        roomA.GameObjects.Add(new GameObject { Name = "South Door", IsOpenable = true });

        var project = new ProjectModel
        {
            Planets =
            [
                new Planet
                {
                    Name = "PlanetA",
                    Countries =
                    [
                        new Country
                        {
                            Name = "CountryA",
                            Areas =
                            [
                                new Area
                                {
                                    Name = "AreaA",
                                    Rooms = [roomA, roomB],
                                    TraversalConnections =
                                    [
                                        new TraversalConnection
                                        {
                                            RoomAId = roomA.Id,
                                            RoomBId = roomB.Id,
                                            TraversalModeOverride = AreaAdjacencyMode.FourDirectional,
                                            BaseTraversalDirectionFromA = Direction10.NorthEast,
                                            TraversalStateFromA = new TraversalLegState
                                            {
                                                OpenStatePolicy = OpenablePolicy.RequireOpen,
                                                Variables =
                                                [
                                                    new GamePropertyDefinition
                                                    {
                                                        Name = "isPassable",
                                                        DefaultValue = "maybe",
                                                        ValueRestriction = GamePropertyValueRestriction.TrueFalse,
                                                        SharedVariableId = Guid.NewGuid()
                                                    }
                                                ]
                                            },
                                            TraversalStateFromB = new TraversalLegState
                                            {
                                                Variables =
                                                [
                                                    new GamePropertyDefinition
                                                    {
                                                        Name = "isPassable",
                                                        DefaultValue = "true",
                                                        ValueRestriction = GamePropertyValueRestriction.TrueFalse
                                                    }
                                                ]
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var registry = new ValidationRuleRegistry();
        registry.Register(new TraversalRequireOpenSharedIsOpenRule());
        registry.Register(new TraversalConnectionIntegrityRule());
        registry.Register(new TraversalLegPassableContractRule());
        registry.Register(new TraversalDoorLinkIntegrityRule());
        registry.Register(new TraversalDoorOpenStateLinkConventionRule());

        var engine = new ValidationEngine(registry);
        var result = engine.Execute(new ValidationExecutionRequest(project));

        Assert.Contains("TRV-001", result.ExecutedRuleIds);
        Assert.Contains("TRV-002", result.ExecutedRuleIds);
        Assert.Contains("TRV-003", result.ExecutedRuleIds);
        Assert.Contains("TRV-004", result.ExecutedRuleIds);
        Assert.Contains("TRV-005", result.ExecutedRuleIds);
    }

    [Fact]
    public void Execute_WhenRuleDoesNotDeclareSupportedScopeTypes_Throws()
    {
        var project = new ProjectModel();
        var registry = new ValidationRuleRegistry();
        registry.Register(new EmptyScopeRule());

        var engine = new ValidationEngine(registry);

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Execute(new ValidationExecutionRequest(project)));
        Assert.Contains("TEST-EMPTY", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EmptyScopeRule : IValidationRule
    {
        public ValidationRuleMetadata Metadata { get; } = new(
            RuleId: "TEST-EMPTY",
            Title: "Empty Scope Rule",
            DefaultSeverity: ValidationSeverity.Warning,
            Category: "Tests");

        public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;

        public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
        {
            yield break;
        }
    }

    private sealed class AlwaysIneligibleProjectRule : IValidationRule
    {
        public ValidationRuleMetadata Metadata { get; } = new(
            RuleId: "TEST-INELIGIBLE",
            Title: "Always Ineligible",
            DefaultSeverity: ValidationSeverity.Warning,
            Category: "Tests");

        public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = new HashSet<ScopeNodeKind>
        {
            ScopeNodeKind.Global
        };

        public ValidationEligibilityResult CanEvaluate(ValidationEligibilityContext context)
        {
            return new ValidationEligibilityResult(
                IsEligible: false,
                SkipReason: ValidationSkipReason.MissingRequiredFacet,
                SkipDetail: "Missing scripted content facet.");
        }

        public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
        {
            yield break;
        }
    }
}

