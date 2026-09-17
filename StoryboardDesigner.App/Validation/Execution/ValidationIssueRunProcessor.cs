using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Execution;

public static class ValidationIssueRunProcessor
{
    public static ValidationIssueRunProcessingResult Process(
        ProjectModel project,
        IReadOnlyList<ValidationIssue> issues,
        ValidationExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(request);

        ScopeHierarchy.AttachParents(project);

        var scopedIssues = FilterIssuesForScope(project, issues, request);
        var unsuppressedIssues = ApplyIgnoreRules(project, scopedIssues);
        var truncated = ApplyCompletionMode(
            unsuppressedIssues,
            request.CompletionMode,
            out var stoppedEarly,
            out var remainingIssueCountAtStop,
            out var stopRuleId);
        return new ValidationIssueRunProcessingResult(truncated, stoppedEarly, remainingIssueCountAtStop, stopRuleId);
    }

    private static IReadOnlyList<ValidationIssue> FilterIssuesForScope(
        ProjectModel project,
        IReadOnlyList<ValidationIssue> issues,
        ValidationExecutionRequest request)
    {
        if (request.ExecutionKind == ValidationExecutionKind.WholeProject)
        {
            return issues;
        }

        if (request.RootScope is null)
        {
            return issues;
        }

        var allowedScopes = new HashSet<ScopeNodeBase>();
        if (!request.IncludeDescendants || request.ExecutionKind == ValidationExecutionKind.ScopedNodeOnly)
        {
            allowedScopes.Add(request.RootScope);
        }
        else
        {
            foreach (var scope in EnumerateScopeSubtree(request.RootScope))
            {
                allowedScopes.Add(scope);
            }
        }

        var pathLookup = BuildScopePathLookup(project);

        return issues
            .Where(issue =>
            {
                var issueScope = ResolveScopeNodeForIssuePath(issue.Path, pathLookup);
                return issueScope is not null && allowedScopes.Contains(issueScope);
            })
            .ToList();
    }

    private static IReadOnlyList<ValidationIssue> ApplyCompletionMode(
        IReadOnlyList<ValidationIssue> issues,
        ValidationCompletionMode completionMode,
        out bool stoppedEarly,
        out int remainingIssueCountAtStop,
        out string? stopRuleId)
    {
        stoppedEarly = false;
        remainingIssueCountAtStop = 0;
        stopRuleId = null;

        if (completionMode != ValidationCompletionMode.StopOnFirstBlocking)
        {
            return issues;
        }

        var firstBlockingIndex = issues
            .Select((issue, index) => new { issue, index })
            .FirstOrDefault(entry => entry.issue.Severity == ValidationSeverity.Error)
            ?.index;

        if (!firstBlockingIndex.HasValue)
        {
            return issues;
        }

        var allowedCount = firstBlockingIndex.Value + 1;
        if (allowedCount >= issues.Count)
        {
            return issues;
        }

        stoppedEarly = true;
        remainingIssueCountAtStop = issues.Count - allowedCount;
        stopRuleId = issues[firstBlockingIndex.Value].RuleId;
        return issues.Take(allowedCount).ToList();
    }

    private static IReadOnlyList<ValidationIssue> ApplyIgnoreRules(ProjectModel project, IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count == 0)
        {
            return issues;
        }

        var projectIgnoredRuleIds = new HashSet<string>(
            (project.IgnoredValidationRuleIds ?? new List<string>())
            .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .Select(static ruleId => ruleId.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (projectIgnoredRuleIds.Count == 0)
        {
            var pathLookupWithoutProjectIgnores = BuildScopePathLookup(project);
            return issues
                .Where(issue => !IsSuppressedByScope(issue, pathLookupWithoutProjectIgnores))
                .ToList();
        }

        var pathLookup = BuildScopePathLookup(project);

        return issues
            .Where(issue => !projectIgnoredRuleIds.Contains(issue.RuleId)
                            && !IsSuppressedByScope(issue, pathLookup))
            .ToList();
    }

    private static bool IsSuppressedByScope(ValidationIssue issue, IReadOnlyDictionary<string, ScopeNodeBase> pathLookup)
    {
        var issueScope = ResolveScopeNodeForIssuePath(issue.Path, pathLookup);
        if (issueScope is null)
        {
            return false;
        }

        if (IsRuleIgnoredAtScope(issueScope, issue.RuleId))
        {
            return true;
        }

        // Catalog-level ignore lists are intended to suppress issues for entries within that catalog.
        foreach (var ancestor in issueScope.EnumerateAncestors())
        {
            if (ancestor is not ScopeNodeBase ancestorScope || !IsCatalogScope(ancestorScope))
            {
                continue;
            }

            if (IsRuleIgnoredAtScope(ancestorScope, issue.RuleId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRuleIgnoredAtScope(ScopeNodeBase scope, string ruleId)
    {
        if (scope.IgnoredValidationRuleIds is null || string.IsNullOrWhiteSpace(ruleId))
        {
            return false;
        }

        return scope.IgnoredValidationRuleIds.Any(ignored =>
            string.Equals(ignored?.Trim(), ruleId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCatalogScope(ScopeNodeBase scope)
    {
        return scope is ObjectTemplatesScopeNode
               || scope is RoomTemplatesScopeNode
               || scope is BaseObjectsScopeNode
               || scope is ScopeAttachedBaseObjectsNode;
    }

    private static IEnumerable<ScopeNodeBase> EnumerateScopeSubtree(ScopeNodeBase root)
    {
        yield return root;

        foreach (var child in root.ChildScopes.OfType<ScopeNodeBase>())
        {
            foreach (var descendant in EnumerateScopeSubtree(child))
            {
                yield return descendant;
            }
        }
    }

    private static Dictionary<string, ScopeNodeBase> BuildScopePathLookup(ProjectModel project)
    {
        var map = new Dictionary<string, ScopeNodeBase>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in EnumerateScopeNodes(project))
        {
            foreach (var alias in BuildScopePathAliases(project, node))
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                map.TryAdd(alias, node);
            }
        }

        return map;
    }

    private static IEnumerable<ScopeNodeBase> EnumerateScopeNodes(ProjectModel project)
    {
        yield return project;
        yield return new ObjectTemplatesScopeNode(project);
        yield return new RoomTemplatesScopeNode(project);
        yield return new BaseObjectsScopeNode(project);

        foreach (var planet in project.Planets)
        {
            yield return planet;
            yield return CreateScopeBaseObjectsNode(planet.BaseObjects, planet.BaseObjectsIgnoredValidationRuleIds, planet);

            foreach (var country in planet.Countries)
            {
                yield return country;
                yield return CreateScopeBaseObjectsNode(country.BaseObjects, country.BaseObjectsIgnoredValidationRuleIds, country);

                foreach (var area in country.Areas)
                {
                    yield return area;
                    yield return CreateScopeBaseObjectsNode(area.BaseObjects, area.BaseObjectsIgnoredValidationRuleIds, area);

                    foreach (var room in area.Rooms)
                    {
                        yield return room;

                        foreach (var roomObject in EnumerateGameObjects(room.GameObjects))
                        {
                            yield return roomObject;
                        }
                    }
                }
            }
        }

        foreach (var globalObject in EnumerateGameObjects(project.GlobalScope.GameObjects))
        {
            yield return globalObject;
        }

        foreach (var templateObject in EnumerateGameObjects(project.ObjectTemplates))
        {
            yield return templateObject;
        }

        foreach (var templateRoom in project.RoomTemplates)
        {
            yield return templateRoom;

            foreach (var roomObject in EnumerateGameObjects(templateRoom.GameObjects))
            {
                yield return roomObject;
            }
        }

        foreach (var baseObject in EnumerateGameObjects(project.BaseObjects))
        {
            yield return baseObject;
        }

        foreach (var baseObject in project.Planets.SelectMany(planet => EnumerateGameObjects(planet.BaseObjects)))
        {
            yield return baseObject;
        }

        foreach (var baseObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => EnumerateGameObjects(country.BaseObjects)))
        {
            yield return baseObject;
        }

        foreach (var baseObject in project.Planets
                     .SelectMany(planet => planet.Countries)
                     .SelectMany(country => country.Areas)
                     .SelectMany(area => EnumerateGameObjects(area.BaseObjects)))
        {
            yield return baseObject;
        }
    }

    private static IEnumerable<GameObject> EnumerateGameObjects(IEnumerable<GameObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;

            foreach (var child in EnumerateGameObjects(root.ContainedObjects))
            {
                yield return child;
            }
        }
    }

    private static ScopeNodeBase CreateScopeBaseObjectsNode(List<GameObject> objects, List<string> ignoredValidationRuleIds, ScopeNodeBase parentScope)
    {
        return new ScopeAttachedBaseObjectsNode(objects, ignoredValidationRuleIds, parentScope);
    }

    private static IEnumerable<string> BuildScopePathAliases(ProjectModel project, ScopeNodeBase node)
    {
        var names = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(scope => scope.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (names.Count == 0)
        {
            yield break;
        }

        yield return string.Join(" / ", names);

        if (node is ProjectModel)
        {
            yield return "Global";
        }

        if (node is ObjectTemplatesScopeNode)
        {
            yield return "Global / Object Templates";
            yield return "Global / Templates";
            yield return "Templates";
        }

        if (node is BaseObjectsScopeNode)
        {
            yield return "Global / Base Objects";
            yield return "Base Objects";
        }

        if (node is GameObject && names.Count >= 2)
        {
            yield return string.Join(" / ", names.Skip(1));

            if (node.EnumerateSelfAndAncestors().Any(scope => scope is ObjectTemplatesScopeNode))
            {
                var templateParts = names.ToList();
                templateParts[1] = "Templates";
                yield return string.Join(" / ", templateParts);
                yield return string.Join(" / ", templateParts.Skip(1));

                if (templateParts.Count >= 3
                    && string.Equals(templateParts[0], "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return string.Join(" / ", new[] { "Global" }.Concat(templateParts.Skip(2)));
                }
            }

            if (node.EnumerateSelfAndAncestors().Any(scope => scope is BaseObjectsScopeNode))
            {
                var baseParts = names.ToList();
                baseParts[1] = "Base Objects";
                yield return string.Join(" / ", baseParts);
                yield return string.Join(" / ", baseParts.Skip(1));

                if (baseParts.Count >= 3
                    && string.Equals(baseParts[0], "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return string.Join(" / ", new[] { "Global" }.Concat(baseParts.Skip(2)));
                }
            }
        }

        if (node is Area areaNode
            && areaNode.ParentScope is Country country
            && country.ParentScope is Planet planet)
        {
            yield return $"Project/{project.Name}/Planet/{planet.ScopeName}/Country/{country.ScopeName}/Area/{areaNode.ScopeName}";
        }

        if (node is Country countryNode
            && countryNode.ParentScope is Planet parentPlanet)
        {
            yield return $"Project/{project.Name}/Planet/{parentPlanet.ScopeName}/Country/{countryNode.ScopeName}";
        }

        if (node is Planet planetNode)
        {
            yield return $"Project/{project.Name}/Planet/{planetNode.ScopeName}";
        }

        if (node is Room roomNode
            && roomNode.ParentScope is Area parentArea
            && parentArea.ParentScope is Country parentCountry
            && parentCountry.ParentScope is Planet grandPlanet)
        {
            yield return $"Project/{project.Name}/Planet/{grandPlanet.ScopeName}/Country/{parentCountry.ScopeName}/Area/{parentArea.ScopeName}/Room/{roomNode.ScopeName}";
        }

        if (node is GameObject objectNode
            && objectNode.ParentScope is ScopeNodeBase parentScope
            && parentScope is ProjectModel)
        {
            yield return $"Global / {objectNode.ScopeName}";
        }
    }

    private static ScopeNodeBase? ResolveScopeNodeForIssuePath(string issuePath, IReadOnlyDictionary<string, ScopeNodeBase> pathToNode)
    {
        if (string.IsNullOrWhiteSpace(issuePath))
        {
            return null;
        }

        var normalizedPath = issuePath.Trim();
        if (pathToNode.TryGetValue(normalizedPath, out var exactMatch))
        {
            return exactMatch;
        }

        if (!normalizedPath.StartsWith("Global / ", StringComparison.OrdinalIgnoreCase)
            && pathToNode.TryGetValue($"Global / {normalizedPath}", out var prefixedMatch))
        {
            return prefixedMatch;
        }

        if (normalizedPath.StartsWith("Global / ", StringComparison.OrdinalIgnoreCase))
        {
            var unprefixed = normalizedPath["Global / ".Length..].Trim();
            if (pathToNode.TryGetValue(unprefixed, out var unprefixedMatch))
            {
                return unprefixedMatch;
            }
        }

        return pathToNode
            .Where(pair =>
                normalizedPath.StartsWith(pair.Key + " / ", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(pair.Key + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }
}


