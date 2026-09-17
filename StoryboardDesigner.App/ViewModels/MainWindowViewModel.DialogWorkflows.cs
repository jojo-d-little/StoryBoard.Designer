using System.Windows.Input;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.GameServices.References;
using StoryboardDesigner.App.Validation.Execution;

namespace StoryboardDesigner.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private sealed record ScopedActionEditTarget(
        string ScopeLabel,
        PropertyResolutionScope VariableScope,
        IList<CommandAction> Actions,
        IScopedAwareNode? ScopeNode,
        IReadOnlyList<GamePropertyChoiceItem>? VariableChoices = null);

    private RelayCommand _editSelectedNodeScopedActionsCommand = null!;

    public ICommand EditSelectedNodeScopedActionsCommand => _editSelectedNodeScopedActionsCommand;

    private void InitializeDialogWorkflowCommands()
    {
        _editSelectedNodeScopedActionsCommand = new RelayCommand(EditSelectedNodeScopedActionsExecute, CanEditSelectedNodeScopedActionsExecute);
    }

    private void RefreshDialogWorkflowCommandStates()
    {
        _editSelectedNodeScopedActionsCommand.RaiseCanExecuteChanged();
    }

    private void EditSelectedNodeScopedActionsExecute()
    {
        var target = ResolveScopedActionEditTarget(SelectedNode);
        if (target is null)
        {
            if (TryResolveLockedActionScopeMessage(SelectedNode, out var lockedMessage))
            {
                ExportStatus = lockedMessage;
                _projectUiService.ShowInformation(lockedMessage, "Linked Actions");
            }

            return;
        }

        var variableChoices = target.VariableChoices
            ?? (target.ScopeNode is null
                ? Array.Empty<GamePropertyChoiceItem>()
                : GetVariableChoicesForScopeNode(target.ScopeNode, target.VariableScope));
        var echoReferenceTokens = variableChoices
            .Select(choice => choice.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Concat(RuntimeAnchorReferenceTokenCatalog.BuildIntrinsicAnchorTokens())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var verbSuggestions = GetScopedVerbSuggestions(target.ScopeNode);
        var directionalSuggestions = GetScopedDirectionalSuggestions(target.ScopeNode);
        var additionalLinkTargetActions = ResolveAdditionalLinkTargetActions(target.Actions);
        var containerTargetChoices = GetContainerTargetChoicesForScopeNode(target.ScopeNode);
        var materializeSourceObjectChoices = GetMaterializeSourceObjectChoicesForScopeNode(target.ScopeNode);
        var procedureChoices = GetProcedureChoicesForScopeNode(target.ScopeNode);
        var compositeRecipeChoices = GetCompositeRecipeChoicesForScopeNode(target.ScopeNode);
        var soundEffectChoices = GetSoundEffectChoicesForScopeNode(target.ScopeNode);
        var timerKeySuggestions = GetTimerKeySuggestionsForScopeNode(target.ScopeNode);

        if (!_dialogWorkflowService.EditScopedActions(
                target.ScopeLabel,
                target.VariableScope,
            target.ScopeNode?.ScopeName ?? string.Empty,
                target.Actions,
            additionalLinkTargetActions,
                containerTargetChoices,
                materializeSourceObjectChoices,
                procedureChoices,
                compositeRecipeChoices,
                soundEffectChoices,
                variableChoices,
                echoReferenceTokens,
                timerKeySuggestions,
                verbSuggestions,
                directionalSuggestions,
                workingActions => ValidateScopedActionsWithPipeline(target, workingActions)))
        {
            return;
        }

        RefreshScopedActionNodesForSelection();

        NotifyProjectEdited();
    }

    private bool CanEditSelectedNodeScopedActionsExecute()
    {
        return ResolveScopedActionEditTarget(SelectedNode) is not null
               || TryResolveLockedActionScopeMessage(SelectedNode, out _);
    }

    private static bool IsLinkedRoomInstanceActionScopeLocked(GameObject gameObject)
    {
        return gameObject.IsRoomInstance && gameObject.LinkActionsToBaseObject;
    }

    private bool TryResolveLockedActionScopeMessage(HierarchyNodeViewModel? node, out string message)
    {
        message = string.Empty;
        if (node is null)
        {
            return false;
        }

        var gameObject = node switch
        {
            GameObjectNodeViewModel objectNode => objectNode.GameObject,
            GlobalObjectNodeViewModel objectNode => objectNode.GameObject,
            TemplateGameObjectNodeViewModel objectNode => objectNode.GameObject,
            _ => null
        } ?? GetAncestry(node)
            .Select(static ancestor => ancestor switch
            {
                GameObjectNodeViewModel objectNode => objectNode.GameObject,
                GlobalObjectNodeViewModel playerObjectNode => playerObjectNode.GameObject,
                TemplateGameObjectNodeViewModel templateObjectNode => templateObjectNode.GameObject,
                _ => null
            })
            .FirstOrDefault();

        if (gameObject is null || !IsLinkedRoomInstanceActionScopeLocked(gameObject))
        {
            return false;
        }

        var objectName = string.IsNullOrWhiteSpace(gameObject.ScopeName)
            ? "This room instance"
            : $"'{gameObject.ScopeName}'";

        if (TryResolveLinkedBaseObjectScopePath(gameObject, out var basePath))
        {
            message = $"This is a linked item.\n\nBase item path:\n{basePath}\n\nEdit actions on the base item for {objectName}.";
            return true;
        }

        message = $"This is a linked item. Edit actions on the base item.";
        return true;
    }

    private bool TryResolveLinkedBaseObjectScopePath(GameObject linkedObject, out string basePath)
    {
        basePath = string.Empty;

        if (!linkedObject.LinkedBaseObjectId.HasValue)
        {
            return false;
        }

        var lookup = BuildObjectScopePathLookup();
        if (!lookup.TryGetValue(linkedObject.LinkedBaseObjectId.Value, out var current))
        {
            return false;
        }

        var visited = new HashSet<Guid>();
        while (current.Object.LinkActionsToBaseObject
               && current.Object.LinkedBaseObjectId.HasValue
               && visited.Add(current.Object.ObjectId)
               && lookup.TryGetValue(current.Object.LinkedBaseObjectId.Value, out var next)
               && !ReferenceEquals(next.Object, current.Object))
        {
            current = next;
        }

        basePath = current.ScopePath;
        return !string.IsNullOrWhiteSpace(basePath);
    }

    private Dictionary<Guid, (GameObject Object, string ScopePath)> BuildObjectScopePathLookup()
    {
        var lookup = new Dictionary<Guid, (GameObject Object, string ScopePath)>();

        void AddObject(GameObject obj, string scopePath)
        {
            if (obj.ObjectId != Guid.Empty && !lookup.ContainsKey(obj.ObjectId))
            {
                lookup[obj.ObjectId] = (obj, scopePath);
            }

            var objectName = GetScopeLabel(obj.ScopeName, "Unnamed Object");
            foreach (var child in obj.ContainedObjects)
            {
                AddObject(child, $"{scopePath} / {objectName}");
            }
        }

        foreach (var template in _project.ObjectTemplates)
        {
            var templateName = GetScopeLabel(template.ScopeName, "Unnamed Object");
            AddObject(template, $"Object Templates / {templateName}");
        }

        foreach (var baseObject in _project.BaseObjects)
        {
            var baseName = GetScopeLabel(baseObject.ScopeName, "Unnamed Object");
            AddObject(baseObject, $"Base Objects / {baseName}");
        }

        foreach (var planet in _project.Planets)
        {
            var planetName = GetScopeLabel(planet.ScopeName, "Unnamed Planet");
            foreach (var baseObject in planet.BaseObjects)
            {
                var baseName = GetScopeLabel(baseObject.ScopeName, "Unnamed Object");
                AddObject(baseObject, $"{planetName} / Base Objects / {baseName}");
            }

            foreach (var country in planet.Countries)
            {
                var countryName = GetScopeLabel(country.ScopeName, "Unnamed Country");
                foreach (var baseObject in country.BaseObjects)
                {
                    var baseName = GetScopeLabel(baseObject.ScopeName, "Unnamed Object");
                    AddObject(baseObject, $"{planetName} / {countryName} / Base Objects / {baseName}");
                }

                foreach (var area in country.Areas)
                {
                    var areaName = GetScopeLabel(area.ScopeName, "Unnamed Area");
                    foreach (var baseObject in area.BaseObjects)
                    {
                        var baseName = GetScopeLabel(baseObject.ScopeName, "Unnamed Object");
                        AddObject(baseObject, $"{planetName} / {countryName} / {areaName} / Base Objects / {baseName}");
                    }
                }
            }
        }

        foreach (var globalObject in _project.GlobalScope.GameObjects)
        {
            var objectName = GetScopeLabel(globalObject.ScopeName, "Unnamed Object");
            AddObject(globalObject, $"Global / {objectName}");
        }

        foreach (var planet in _project.Planets)
        {
            var planetName = GetScopeLabel(planet.ScopeName, "Unnamed Planet");
            foreach (var country in planet.Countries)
            {
                var countryName = GetScopeLabel(country.ScopeName, "Unnamed Country");
                foreach (var area in country.Areas)
                {
                    var areaName = GetScopeLabel(area.ScopeName, "Unnamed Area");
                    foreach (var room in area.Rooms)
                    {
                        var roomName = GetScopeLabel(room.ScopeName, "Unnamed Room");
                        foreach (var roomObject in room.GameObjects)
                        {
                            var objectName = GetScopeLabel(roomObject.ScopeName, "Unnamed Object");
                            AddObject(roomObject, $"{planetName} / {countryName} / {areaName} / {roomName} / {objectName}");
                        }
                    }
                }
            }
        }

        return lookup;
    }

    private static string GetScopeLabel(string? value, string fallback)
    {
        var label = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(label) ? fallback : label;
    }

    private IReadOnlyList<string> GetScopedVerbSuggestions(IScopedAwareNode? scopeNode)
    {
        return scopeNode.GetValidVerbs();
    }

    private IReadOnlyList<string> GetScopedDirectionalSuggestions(IScopedAwareNode? scopeNode)
    {
        return scopeNode.GetValidDirectionalQualifiers(includeBuiltIns: true);
    }

    private ScopedActionEditTarget? ResolveScopedActionEditTarget(HierarchyNodeViewModel? node)
    {
        if (node is null)
        {
            return null;
        }

        ScopedActionEditTarget BuildTraversalLegTarget(TraversalLegNodeViewModel traversalLegNode)
        {
            return new ScopedActionEditTarget(
                $"Traversal Leg - {traversalLegNode.Direction} to {traversalLegNode.DestinationRoomName}",
                PropertyResolutionScope.Room,
                traversalLegNode.LegState.AvailableActions,
                traversalLegNode.RoomNode.Room,
                BuildTraversalLegVariableChoices(traversalLegNode));
        }

        var ancestry = GetAncestry(node);

        if (node is ScopedActionsNodeViewModel { Parent: TraversalLegNodeViewModel traversalLegParent })
        {
            return BuildTraversalLegTarget(traversalLegParent);
        }

        if (node is ScopedActionEntryNodeViewModel { ParentActionsNode.Parent: TraversalLegNodeViewModel traversalLegParentFromEntry })
        {
            return BuildTraversalLegTarget(traversalLegParentFromEntry);
        }

        if (node is ScopedActionsNodeViewModel { Parent: ProjectRootNodeViewModel })
        {
            return new ScopedActionEditTarget("Global", PropertyResolutionScope.Global, _project.GlobalScope.AvailableActions, CreateGlobalScopeNodeForDialogWorkflows());
        }

        if (node is ScopedActionEntryNodeViewModel { ParentActionsNode.Parent: ProjectRootNodeViewModel })
        {
            return new ScopedActionEditTarget("Global", PropertyResolutionScope.Global, _project.GlobalScope.AvailableActions, CreateGlobalScopeNodeForDialogWorkflows());
        }

        if (node is ProjectRootNodeViewModel)
        {
            return new ScopedActionEditTarget("Global", PropertyResolutionScope.Global, _project.GlobalScope.AvailableActions, CreateGlobalScopeNodeForDialogWorkflows());
        }

        if (node is PlanetNodeViewModel planetNode)
        {
            return new ScopedActionEditTarget($"Planet - {planetNode.Planet.ScopeName}", PropertyResolutionScope.Planet, planetNode.Planet.AvailableActions, planetNode.Planet);
        }

        if (node is CountryNodeViewModel countryNode)
        {
            return new ScopedActionEditTarget($"Country - {countryNode.Country.ScopeName}", PropertyResolutionScope.Country, countryNode.Country.AvailableActions, countryNode.Country);
        }

        if (node is AreaNodeViewModel areaNode)
        {
            return new ScopedActionEditTarget($"Area - {areaNode.Area.ScopeName}", PropertyResolutionScope.Area, areaNode.Area.AvailableActions, areaNode.Area);
        }

        if (node is RoomNodeViewModel roomNode)
        {
            return new ScopedActionEditTarget($"Room - {roomNode.Room.ScopeName}", PropertyResolutionScope.Room, roomNode.Room.AvailableActions, roomNode.Room);
        }

        if (node is TemplateRoomNodeViewModel templateRoomNode)
        {
            return new ScopedActionEditTarget($"Template Room - {templateRoomNode.Room.ScopeName}", PropertyResolutionScope.Room, templateRoomNode.Room.AvailableActions, templateRoomNode.Room);
        }

        if (node is TraversalLegNodeViewModel traversalLegNode)
        {
            return BuildTraversalLegTarget(traversalLegNode);
        }

        if (node is RoomGameObjectsNodeViewModel objectsNode)
        {
            return new ScopedActionEditTarget($"Room - {objectsNode.RoomNode.Room.ScopeName}", PropertyResolutionScope.Room, objectsNode.RoomNode.Room.AvailableActions, objectsNode.RoomNode.Room);
        }

        if (node is GameObjectNodeViewModel objectNode)
        {
            if (IsLinkedRoomInstanceActionScopeLocked(objectNode.GameObject))
            {
                return null;
            }

            return new ScopedActionEditTarget($"Game Object - {objectNode.GameObject.ScopeName}", PropertyResolutionScope.Object, objectNode.GameObject.AvailableActions, objectNode.GameObject);
        }

        if (node is GlobalObjectNodeViewModel playerObjectNode)
        {
            if (IsLinkedRoomInstanceActionScopeLocked(playerObjectNode.GameObject))
            {
                return null;
            }

            return new ScopedActionEditTarget($"Game Object - {playerObjectNode.GameObject.ScopeName}", PropertyResolutionScope.Object, playerObjectNode.GameObject.AvailableActions, playerObjectNode.GameObject);
        }

        if (node is TemplateGameObjectNodeViewModel templateObjectNode)
        {
            if (IsLinkedRoomInstanceActionScopeLocked(templateObjectNode.GameObject))
            {
                return null;
            }

            return new ScopedActionEditTarget($"Template Object - {templateObjectNode.GameObject.ScopeName}", PropertyResolutionScope.Object, templateObjectNode.GameObject.AvailableActions, templateObjectNode.GameObject);
        }

        var objectAncestor = ancestry.OfType<GameObjectNodeViewModel>().FirstOrDefault();
        if (objectAncestor is not null)
        {
            if (IsLinkedRoomInstanceActionScopeLocked(objectAncestor.GameObject))
            {
                return null;
            }

            return new ScopedActionEditTarget($"Game Object - {objectAncestor.GameObject.ScopeName}", PropertyResolutionScope.Object, objectAncestor.GameObject.AvailableActions, objectAncestor.GameObject);
        }

        var playerObjectAncestor = ancestry.OfType<GlobalObjectNodeViewModel>().FirstOrDefault();
        if (playerObjectAncestor is not null)
        {
            if (IsLinkedRoomInstanceActionScopeLocked(playerObjectAncestor.GameObject))
            {
                return null;
            }

            return new ScopedActionEditTarget($"Game Object - {playerObjectAncestor.GameObject.ScopeName}", PropertyResolutionScope.Object, playerObjectAncestor.GameObject.AvailableActions, playerObjectAncestor.GameObject);
        }

        var templateObjectAncestor = ancestry.OfType<TemplateGameObjectNodeViewModel>().FirstOrDefault();
        if (templateObjectAncestor is not null)
        {
            if (IsLinkedRoomInstanceActionScopeLocked(templateObjectAncestor.GameObject))
            {
                return null;
            }

            return new ScopedActionEditTarget($"Template Object - {templateObjectAncestor.GameObject.ScopeName}", PropertyResolutionScope.Object, templateObjectAncestor.GameObject.AvailableActions, templateObjectAncestor.GameObject);
        }

        var traversalLegAncestor = ancestry.OfType<TraversalLegNodeViewModel>().FirstOrDefault();
        if (traversalLegAncestor is not null)
        {
            return BuildTraversalLegTarget(traversalLegAncestor);
        }

        var roomAncestor = ancestry.OfType<RoomNodeViewModel>().FirstOrDefault();
        if (roomAncestor is not null)
        {
            return new ScopedActionEditTarget($"Room - {roomAncestor.Room.ScopeName}", PropertyResolutionScope.Room, roomAncestor.Room.AvailableActions, roomAncestor.Room);
        }

        var templateRoomAncestor = ancestry.OfType<TemplateRoomNodeViewModel>().FirstOrDefault();
        if (templateRoomAncestor is not null)
        {
            return new ScopedActionEditTarget($"Template Room - {templateRoomAncestor.Room.ScopeName}", PropertyResolutionScope.Room, templateRoomAncestor.Room.AvailableActions, templateRoomAncestor.Room);
        }

        var areaAncestor = ancestry.OfType<AreaNodeViewModel>().FirstOrDefault();
        if (areaAncestor is not null)
        {
            return new ScopedActionEditTarget($"Area - {areaAncestor.Area.ScopeName}", PropertyResolutionScope.Area, areaAncestor.Area.AvailableActions, areaAncestor.Area);
        }

        var countryAncestor = ancestry.OfType<CountryNodeViewModel>().FirstOrDefault();
        if (countryAncestor is not null)
        {
            return new ScopedActionEditTarget($"Country - {countryAncestor.Country.ScopeName}", PropertyResolutionScope.Country, countryAncestor.Country.AvailableActions, countryAncestor.Country);
        }

        var planetAncestor = ancestry.OfType<PlanetNodeViewModel>().FirstOrDefault();
        if (planetAncestor is not null)
        {
            return new ScopedActionEditTarget($"Planet - {planetAncestor.Planet.ScopeName}", PropertyResolutionScope.Planet, planetAncestor.Planet.AvailableActions, planetAncestor.Planet);
        }

        return null;
    }

    private ScopeNodeBase CreateGlobalScopeNodeForDialogWorkflows()
    {
        return _project;
    }

    private IReadOnlyList<GamePropertyChoiceItem> BuildTraversalLegVariableChoices(TraversalLegNodeViewModel traversalLegNode)
    {
        var roomChoices = GetVariableChoicesForScopeNode(traversalLegNode.RoomNode.Room, PropertyResolutionScope.Room).ToList();
        var choices = new List<GamePropertyChoiceItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legScopePath = BuildScopePathFromNode(traversalLegNode.RoomNode.Room, $"Traversal Leg ({traversalLegNode.Direction})");

        foreach (var variable in traversalLegNode.LegState.Variables)
        {
            var variableName = variable.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            var selfValue = $"self.{variableName}";
            if (seen.Add(selfValue))
            {
                choices.Add(new GamePropertyChoiceItem
                {
                    Value = selfValue,
                    ScopePath = legScopePath,
                    OwnerVariable = BuildOwnerVariable("self", variableName),
                    Scope = PropertyResolutionScope.Room,
                    ValueRestriction = variable.ValueRestriction,
                    Priority = 0,
                    Relation = GamePropertyChoiceRelation.Self
                });
            }

            if (seen.Add(variableName))
            {
                choices.Add(new GamePropertyChoiceItem
                {
                    Value = variableName,
                    ScopePath = legScopePath,
                    OwnerVariable = BuildOwnerVariable("TraversalLeg", variableName),
                    Scope = PropertyResolutionScope.Room,
                    ValueRestriction = variable.ValueRestriction,
                    Priority = 1,
                    Relation = GamePropertyChoiceRelation.Self
                });
            }
        }

        foreach (var choice in roomChoices)
        {
            if (seen.Add(choice.Value))
            {
                choices.Add(choice);
            }
        }

        return choices;
    }

    private IReadOnlyList<CommandAction> ResolveAdditionalLinkTargetActions(IList<CommandAction> currentScopeActions)
    {
        var globalObjectActions = _project.GlobalScope.GameObjects
            .SelectMany(obj => obj.AvailableActions)
            .ToList();

        if (globalObjectActions.Count == 0)
        {
            return Array.Empty<CommandAction>();
        }

        var currentIds = currentScopeActions
            .Select(action => action.Id)
            .ToHashSet();

        return globalObjectActions
            .Where(action => !currentIds.Contains(action.Id))
            .ToList();
    }

    private IReadOnlyList<ProjectValidationIssue> ValidateScopedActionsWithPipeline(
        ScopedActionEditTarget target,
        IReadOnlyList<CommandAction> workingActions)
    {
        if (target.ScopeNode is not ScopeNodeBase scopeNode)
        {
            return Array.Empty<ProjectValidationIssue>();
        }

        var originalActions = target.Actions.ToList();
        var replacementActions = CloneActions(workingActions);

        try
        {
            target.Actions.Clear();
            foreach (var action in replacementActions)
            {
                target.Actions.Add(action);
            }

            ScopeHierarchy.AttachParents(_project);

            var request = new ValidationExecutionRequest(
                _project,
                ValidationExecutionKind.ScopedNodeOnly,
                scopeNode,
                IncludeDescendants: false,
                CompletionMode: ValidationCompletionMode.FullReport);

            var result = BuildProjectValidationIssues(request);
            return result.ProcessingResult.Issues
                .Where(issue => TryGetActionPathParts(issue.Path, out _, out _))
                .Select(MapValidationIssue)
                .ToList();
        }
        finally
        {
            target.Actions.Clear();
            foreach (var action in originalActions)
            {
                target.Actions.Add(action);
            }

            ScopeHierarchy.AttachParents(_project);
        }
    }
}



