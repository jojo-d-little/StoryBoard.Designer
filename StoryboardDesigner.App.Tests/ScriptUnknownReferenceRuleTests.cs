using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Execution;
using StoryboardDesigner.App.Validation.Rules.Scripting;

namespace StoryboardDesigner.App.Tests;

public sealed class ScriptUnknownReferenceRuleTests
{
    [Fact]
    public void Evaluate_ReportsWarning_WhenEchoScriptUsesUnknownReference()
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
                                                    Name = "Look",
                                                    ActionType = CommandActionType.EchoMessage,
                                                    EchoMessage = "ECHO {unknownToken}",
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-001", issue.RuleId);
        Assert.Equal(StoryboardDesigner.App.Validation.Contracts.ValidationSeverity.Warning, issue.Severity);
        Assert.Contains("Unknown reference", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Global / Planet A / Country A / Area A / Room A / Action:Look", issue.Path);
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenEchoScriptUsesKnownReference()
    {
        var room = new Room
        {
            Name = "Room A",
            Variables = [new GamePropertyDefinition { Name = "temperature" }],
            AvailableActions =
            [
                new CommandAction
                {
                    Name = "Look",
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "ECHO {temperature}",
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
                                    Rooms = [room]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenEchoScriptUsesManifestAnchorRootedReference()
    {
        var room = new Room
        {
            Name = "Room A",
            AvailableActions =
            [
                new CommandAction
                {
                    Name = "Look",
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "ECHO {currentCountry.alertLevel}",
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
                                    Rooms = [room]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenEchoScriptUsesNearByQuantityReference()
    {
        var room = new Room
        {
            Name = "Room A",
            GameObjects =
            [
                new GameObject
                {
                    Name = "Flowers",
                    IsQuantifiable = true,
                    Quantity = 3,
                    AvailableActions =
                    [
                        new CommandAction
                        {
                            Name = "Look",
                            ActionType = CommandActionType.EchoMessage,
                            EchoMessage = "ECHO {self.nearByQuantity}",
                            Payload = new StoryboardDesigner.App.Models.EchoPayload(),
                        }
                    ]
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
                                    Rooms = [room]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenPayloadEchoScriptUsesUnknownReference()
    {
        var action = new CommandAction
        {
            Name = "Look",
            ActionType = CommandActionType.EchoMessage,
            EchoMessage = "ECHO {unknownPayloadToken}",
            Payload = new EchoPayload()
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
                                            Variables = [new GamePropertyDefinition { Name = "temperature" }],
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        var issue = Assert.Single(issues);
        Assert.Equal("SCR-001", issue.RuleId);
        Assert.Contains("Unknown reference", issue.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unknownPayloadToken", issue.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenAreaScopedObjectScriptUsesItsKnownVariable()
    {
        var areaObject = new GameObject
        {
            Name = "AreaLantern",
            Variables =
            [
                new GamePropertyDefinition
                {
                    Name = "isLit",
                    DefaultValue = "false",
                    ValueRestriction = GamePropertyValueRestriction.TrueFalse
                }
            ],
            AvailableActions =
            [
                new CommandAction
                {
                    Name = "Inspect",
                    ActionType = CommandActionType.EchoMessage,
                    EchoMessage = "ECHO {isLit}",
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
                                    GameObjects = [areaObject],
                                    Rooms = [new Room { Name = "Room A" }]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenCompositePayloadUsesActionToken()
    {
        var action = new CommandAction
        {
            Name = "Assemble",
            ActionType = CommandActionType.BuildCompositeByParts,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Failure"] = "Missing {currentAction.missingParts[0]}"
            },
            Payload = new CompositeByPartsPayload(
                CompositeTargetObjectId: null,
                CompositeRecipeId: null,
                CompositeRequiredPartObjectIds: [],
                CompositeStrictPartCountEnforcement: null,
                CompositeMinimumRequiredPartCount: null,
                CompositeMatchMode: string.Empty,
                CompositeAmbiguityPolicy: string.Empty,
                CompositePartConsumptionMode: "ContainedInComposite",
                CompositeResolvedTargetOutputTemplate: string.Empty)
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenCompositePayloadUsesCurrentActionAnchoredToken()
    {
        var action = new CommandAction
        {
            Name = "Assemble",
            ActionType = CommandActionType.BuildCompositeByParts,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Failure"] = "Missing {currentAction.missingParts[0]}"
            },
            Payload = new CompositeByPartsPayload(
                CompositeTargetObjectId: null,
                CompositeRecipeId: null,
                CompositeRequiredPartObjectIds: [],
                CompositeStrictPartCountEnforcement: null,
                CompositeMinimumRequiredPartCount: null,
                CompositeMatchMode: string.Empty,
                CompositeAmbiguityPolicy: string.Empty,
                CompositePartConsumptionMode: "ContainedInComposite",
                CompositeResolvedTargetOutputTemplate: string.Empty)
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenCompositePayloadUsesTargetContainerNameToken()
    {
        var action = new CommandAction
        {
            Name = "Assemble",
            ActionType = CommandActionType.BuildCompositeByParts,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "Built {currentAction.targetContainerName}"
            },
            Payload = new CompositeByPartsPayload(
                CompositeTargetObjectId: null,
                CompositeRecipeId: null,
                CompositeRequiredPartObjectIds: [],
                CompositeStrictPartCountEnforcement: null,
                CompositeMinimumRequiredPartCount: null,
                CompositeMatchMode: string.Empty,
                CompositeAmbiguityPolicy: string.Empty,
                CompositePartConsumptionMode: "ContainedInComposite",
                CompositeResolvedTargetOutputTemplate: string.Empty)
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenCompositePayloadUsesLegacyMissingPartsAlias()
    {
        var action = new CommandAction
        {
            Name = "Assemble",
            ActionType = CommandActionType.BuildCompositeByParts,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Failure"] = "Missing {missingParts[0]}"
            },
            Payload = new CompositeByPartsPayload(
                CompositeTargetObjectId: null,
                CompositeRecipeId: null,
                CompositeRequiredPartObjectIds: [],
                CompositeStrictPartCountEnforcement: null,
                CompositeMinimumRequiredPartCount: null,
                CompositeMatchMode: string.Empty,
                CompositeAmbiguityPolicy: string.Empty,
                CompositePartConsumptionMode: "ContainedInComposite",
                CompositeResolvedTargetOutputTemplate: string.Empty)
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Contains(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenCompositePayloadUsesLegacyActionPropertyPrefix()
    {
        var action = new CommandAction
        {
            Name = "Assemble",
            ActionType = CommandActionType.BuildCompositeByParts,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Failure"] = "Missing {actionProperty.missingParts[0]}"
            },
            Payload = new CompositeByPartsPayload(
                CompositeTargetObjectId: null,
                CompositeRecipeId: null,
                CompositeRequiredPartObjectIds: [],
                CompositeStrictPartCountEnforcement: null,
                CompositeMinimumRequiredPartCount: null,
                CompositeMatchMode: string.Empty,
                CompositeAmbiguityPolicy: string.Empty,
                CompositePartConsumptionMode: "ContainedInComposite",
                CompositeResolvedTargetOutputTemplate: string.Empty)
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Contains(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenPutObjectInContainerUsesActionContainerTokens()
    {
        var action = new CommandAction
        {
            Name = "put_ball",
            ActionType = CommandActionType.PutObjectInContainer,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "ECHO Stored {currentAction.primaryitem} in {currentAction.targetContainer}",
                ["Failure"] = string.Empty
            },
            Payload = new ContainerTransferPayload(
                TargetContainerId: "the player")
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenOutcomeMessageMapScriptUsesUnknownReference()
    {
        var action = new CommandAction
        {
            Name = "set_flag",
            ActionType = CommandActionType.SetGameProperty,
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "ECHO {unknownOutcomeToken}"
            },
            Payload = new SetGamePropertyPayload("doorOpen", "true")
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Contains(issues, issue =>
            issue.RuleId == "SCR-001"
            && issue.Description.Contains("unknownOutcomeToken", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenNavigateOutcomeUsesCanonicalNavigateTokens()
    {
        var action = new CommandAction
        {
            Name = "go_north",
            ActionType = CommandActionType.NavigateDirection,
            Payload = new NavigatePayload(),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "Moved={currentAction.Success} code={currentAction.ResultCode} from={priorRoom.Name} to={currentRoom.Name}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenNavigateOutcomeUsesUnknownNavigateAlias()
    {
        var action = new CommandAction
        {
            Name = "go_north",
            ActionType = CommandActionType.NavigateDirection,
            Payload = new NavigatePayload(),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "Bad token {currentAction.ResultCodeLegacy}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Contains(issues, issue =>
            issue.RuleId == "SCR-001"
            && issue.Description.Contains("currentAction.ResultCodeLegacy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenNavigateOutcomeUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "go_north",
            ActionType = CommandActionType.NavigateDirection,
            Payload = new NavigatePayload(),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Moved"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenBuildCompositeByTargetOutcomeUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "build_target",
            ActionType = CommandActionType.BuildCompositeByTarget,
            Payload = new CompositeByTargetPayload(null, null, [], null, null, string.Empty),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenBuildCompositeByPartsOutcomeUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "build_parts",
            ActionType = CommandActionType.BuildCompositeByParts,
            Payload = new CompositeByPartsPayload(null, null, [], null, null, string.Empty, string.Empty, string.Empty, string.Empty),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Failure"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenBreakCompositeItemOutcomeUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "break_item",
            ActionType = CommandActionType.BreakCompositeItem,
            Payload = new BreakCompositePayload(null, null, [], null, null, string.Empty),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenPutObjectInContainerOutcomeUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "put_item",
            ActionType = CommandActionType.PutObjectInContainer,
            Payload = new ContainerTransferPayload(string.Empty),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_DoesNotReportWarning_WhenRemoveObjectFromContainerOutcomeUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "remove_item",
            ActionType = CommandActionType.RemoveObjectFromContainer,
            Payload = new ContainerTransferPayload(string.Empty),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.DoesNotContain(issues, issue => issue.RuleId == "SCR-001");
    }

    [Fact]
    public void Evaluate_ReportsWarning_WhenNonOptedInActionUsesActionAll()
    {
        var action = new CommandAction
        {
            Name = "set_flag",
            ActionType = CommandActionType.SetGameProperty,
            Payload = new SetGamePropertyPayload("lamp.isLit", "true"),
            OutcomeMessageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Success"] = "debug={currentAction.all}"
            }
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
                                            AvailableActions = [action]
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
        var issues = engine.Execute(new ValidationExecutionRequest(project)).Issues.ToList();

        Assert.Contains(issues, issue =>
            issue.RuleId == "SCR-001"
            && issue.Description.Contains("action.all", StringComparison.OrdinalIgnoreCase));
    }
}


