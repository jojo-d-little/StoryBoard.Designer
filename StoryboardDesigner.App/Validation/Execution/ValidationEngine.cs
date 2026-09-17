using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public sealed class ValidationEngine
{
    private readonly ValidationRuleRegistry _registry;

    public ValidationEngine(ValidationRuleRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ValidationExecutionResult Execute(ValidationExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawIssues = new List<ValidationIssue>();
        var executedRuleIds = new List<string>();
        var skippedRules = new List<ValidationSkippedRuleInfo>();
        var requestRoot = request.RootScope ?? request.Project;
        var lookup = ValidationLookupService.Build(request.Project);
        var sharedState = new ValidationSharedState();
        var activeProfile = ValidationProfile.Unspecified;
        var isRecursivePass = request.IncludeDescendants;
        var isFullProject = request.ExecutionKind == ValidationExecutionKind.WholeProject
                            || (requestRoot.ScopeKind == ScopeNodeKind.Global && request.IncludeDescendants);
        var objectLookup = BuildObjectLookup(request.Project);
        var candidateNodes = EnumerateCandidateNodes(requestRoot, request.IncludeDescendants, objectLookup).ToList();
        var rootScopePath = BuildRootScopePath(requestRoot);

        foreach (var rule in _registry.Rules)
        {
            var supportedKinds = rule.SupportedCandidateScopeKinds;
            if (supportedKinds.Count == 0 && !rule.SupportsActionCandidates)
            {
                throw new InvalidOperationException(
                    $"Validation rule '{rule.Metadata.RuleId}' must declare at least one supported candidate scope kind or support action candidates.");
            }

            var executed = false;
            var hadSupportedCandidate = false;
            ValidationEligibilityResult? firstIneligible = null;
            foreach (var candidate in candidateNodes)
            {
                if (!IsSupportedCandidate(rule, candidate, supportedKinds))
                {
                    continue;
                }

                hadSupportedCandidate = true;

                var eligibility = BuildEligibility(request, candidate, requestRoot, activeProfile, isRecursivePass, isFullProject);
                var eligibilityResult = rule.CanEvaluate(eligibility);
                if (!eligibilityResult.IsEligible)
                {
                    firstIneligible ??= eligibilityResult;
                    continue;
                }

                var context = BuildContext(request, candidate, requestRoot, activeProfile, sharedState, lookup, isRecursivePass, isFullProject);
                rawIssues.AddRange(rule.Evaluate(context));

                if (!executed)
                {
                    executedRuleIds.Add(rule.Metadata.RuleId);
                    executed = true;
                }
            }

            if (!executed)
            {
                if (!hadSupportedCandidate)
                {
                    skippedRules.Add(new ValidationSkippedRuleInfo(
                        rule.Metadata.RuleId,
                        ValidationSkipReason.UnsupportedScopeType,
                        "No candidate nodes matched this rule's supported scope kinds for the current run scope."));
                }
                else
                {
                    skippedRules.Add(new ValidationSkippedRuleInfo(
                        rule.Metadata.RuleId,
                        firstIneligible?.SkipReason ?? ValidationSkipReason.MissingRequiredFacet,
                        firstIneligible?.SkipDetail));
                }
            }
        }

        var processed = ValidationIssueRunProcessor.Process(request.Project, rawIssues, request);
        var remainingRuleCountAtStop = 0;
        if (processed.StoppedEarly && !string.IsNullOrWhiteSpace(processed.StopRuleId))
        {
            var stopIndex = _registry.Rules
                .Select((entry, index) => new { entry, index })
                .FirstOrDefault(entry => string.Equals(entry.entry.Metadata.RuleId, processed.StopRuleId, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (stopIndex.HasValue)
            {
                remainingRuleCountAtStop = Math.Max(0, _registry.Rules.Count - (stopIndex.Value + 1));
            }
        }

        return new ValidationExecutionResult(
            processed.Issues,
            executedRuleIds,
            skippedRules,
            request.ExecutionKind,
            rootScopePath,
            request.IncludeDescendants,
            candidateNodes.Count,
            processed.StoppedEarly,
            processed.RemainingIssueCountAtStop,
            processed.StopRuleId,
            remainingRuleCountAtStop);
    }

    private static ValidationRuleContext BuildContext(
        ValidationExecutionRequest request,
        ScopeNodeBase candidate,
        ScopeNodeBase requestRoot,
        ValidationProfile activeProfile,
        IValidationSharedState sharedState,
        IValidationLookupService lookup,
        bool isRecursivePass,
        bool isFullProject)
    {
        return new ValidationRuleContext(
            request.Project,
            candidate,
            requestRoot,
            request,
            activeProfile,
            request.IncludeDescendants,
            isRecursivePass,
            isFullProject,
            lookup,
            sharedState);
    }

    private static ValidationEligibilityContext BuildEligibility(
        ValidationExecutionRequest request,
        ScopeNodeBase candidate,
        ScopeNodeBase requestRoot,
        ValidationProfile activeProfile,
        bool isRecursivePass,
        bool isFullProject)
    {
        return new ValidationEligibilityContext(
            request,
            candidate,
            requestRoot,
            isRecursivePass,
            isFullProject,
            activeProfile,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            BuildNodeFacets(candidate));
    }

    private static bool IsSupportedCandidate(
        IValidationRule rule,
        ScopeNodeBase candidate,
        IReadOnlySet<ScopeNodeKind> supportedKinds)
    {
        if (candidate is ValidationActionCandidateNode)
        {
            return rule.SupportsActionCandidates;
        }

        var normalizedKind = candidate.ScopeKind == ScopeNodeKind.RoomTemplates
            ? ScopeNodeKind.Templates
            : candidate.ScopeKind;
        return supportedKinds.Contains(normalizedKind);
    }

    private static IEnumerable<ScopeNodeBase> EnumerateCandidateNodes(
        ScopeNodeBase root,
        bool includeDescendants,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        var rootPath = string.Equals(root.ScopeName, "Global", StringComparison.OrdinalIgnoreCase)
            ? "Global"
            : root.ScopeName;

        foreach (var candidate in EnumerateCandidateNodesCore(root, includeDescendants, rootPath, objectLookup))
        {
            yield return candidate;
        }
    }

    private static string BuildRootScopePath(ScopeNodeBase root)
    {
        if (root is ProjectModel)
        {
            return "Global";
        }

        var names = root
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name));

        return string.Join(" / ", names);
    }

    private static IEnumerable<ScopeNodeBase> EnumerateCandidateNodesCore(
        ScopeNodeBase node,
        bool includeDescendants,
        string currentPath,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        yield return node;

        foreach (var actionNode in BuildActionCandidateNodes(node, currentPath, objectLookup))
        {
            yield return actionNode;
        }

        if (!includeDescendants)
        {
            yield break;
        }

        foreach (var child in node.ChildScopes.OfType<ScopeNodeBase>())
        {
            var childPath = AppendPath(currentPath, child);
            foreach (var nested in EnumerateCandidateNodesCore(child, includeDescendants: true, childPath, objectLookup))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<ValidationActionCandidateNode> BuildActionCandidateNodes(
        ScopeNodeBase owner,
        string ownerScopePath,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        foreach (var action in GetActions(owner, objectLookup))
        {
            yield return new ValidationActionCandidateNode(action, owner, ownerScopePath);
        }
    }

    private static string AppendPath(string currentPath, ScopeNodeBase child)
    {
        if (IsPathAliasBoundaryScope(child))
        {
            return currentPath;
        }

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return child.ScopeName;
        }

        return currentPath + " / " + child.ScopeName;
    }

    private static bool IsPathAliasBoundaryScope(ScopeNodeBase scope)
    {
        return scope is ObjectTemplatesScopeNode
               || scope is RoomTemplatesScopeNode;
    }

    private static IEnumerable<CommandAction> GetActions(
        ScopeNodeBase owner,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        return owner switch
        {
            Planet planet => planet.AvailableActions,
            Country country => country.AvailableActions,
            Area area => area.AvailableActions,
            Room room => room.AvailableActions,
            GameObject gameObject => ResolveEffectiveObjectActions(gameObject, objectLookup),
            _ => Array.Empty<CommandAction>()
        };
    }

    private static IReadOnlyDictionary<Guid, GameObject> BuildObjectLookup(ProjectModel project)
    {
        var lookup = new Dictionary<Guid, GameObject>();

        void AddObject(GameObject obj)
        {
            if (obj.ObjectId != Guid.Empty)
            {
                lookup.TryAdd(obj.ObjectId, obj);
            }

            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child);
            }
        }

        foreach (var template in project.ObjectTemplates)
        {
            AddObject(template);
        }

        foreach (var templateRoomObject in project.RoomTemplates.SelectMany(room => room.GameObjects))
        {
            AddObject(templateRoomObject);
        }

        foreach (var playerObject in project.GlobalScope.GameObjects)
        {
            AddObject(playerObject);
        }

        foreach (var roomObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => area.Rooms)
                     .SelectMany(room => room.GameObjects))
        {
            AddObject(roomObject);
        }

        return lookup;
    }

    private static IReadOnlyList<CommandAction> ResolveEffectiveObjectActions(
        GameObject obj,
        IReadOnlyDictionary<Guid, GameObject> objectLookup)
    {
        if (!obj.LinkActionsToBaseObject || !obj.LinkedBaseObjectId.HasValue)
        {
            return obj.AvailableActions;
        }

        var visited = new HashSet<Guid>();
        var current = obj;

        while (current.LinkActionsToBaseObject && current.LinkedBaseObjectId.HasValue)
        {
            var baseId = current.LinkedBaseObjectId.Value;
            if (!visited.Add(baseId)
                || !objectLookup.TryGetValue(baseId, out var baseObject)
                || ReferenceEquals(baseObject, current))
            {
                break;
            }

            current = baseObject;
        }

        return current.AvailableActions;
    }

    private static ValidationNodeFacetSet BuildNodeFacets(ScopeNodeBase candidate)
    {
        if (candidate is ValidationActionCandidateNode actionNode)
        {
            var hasScript = actionNode.Action.GetScriptFields()
                .Any(static field => !string.IsNullOrWhiteSpace(field.Value));

            return new ValidationNodeFacetSet(HasActions: true, HasTraversalLegs: false, HasScriptContent: hasScript);
        }

        var hasActions = candidate switch
        {
            Planet planet => planet.AvailableActions.Count > 0,
            Country country => country.AvailableActions.Count > 0,
            Area area => area.AvailableActions.Count > 0,
            Room room => room.AvailableActions.Count > 0,
            GameObject gameObject => gameObject.AvailableActions.Count > 0,
            _ => false
        };

        var hasTraversalLegs = candidate is Area areaNode && areaNode.TraversalConnections.Count > 0;
        var hasScriptContent = hasActions;

        return new ValidationNodeFacetSet(hasActions, hasTraversalLegs, hasScriptContent);
    }
}


