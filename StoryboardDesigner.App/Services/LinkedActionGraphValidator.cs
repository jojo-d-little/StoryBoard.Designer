using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public static class LinkedActionGraphValidator
{
    public static List<string> Validate(
        IReadOnlyCollection<CommandAction> actions,
        string scopeLabel,
        IReadOnlyCollection<CommandAction>? additionalLinkTargetActions = null)
    {
        var errors = new List<string>();
        if (actions.Count == 0)
        {
            return errors;
        }

        var actionById = actions.ToDictionary(action => action.Id, action => action);
        var linkTargetById = actions.ToDictionary(action => action.Id, action => action);
        if (additionalLinkTargetActions is not null)
        {
            foreach (var externalAction in additionalLinkTargetActions)
            {
                if (!linkTargetById.ContainsKey(externalAction.Id))
                {
                    linkTargetById[externalAction.Id] = externalAction;
                }
            }
        }

        var linkedTargets = new HashSet<Guid>();

        foreach (var action in actions)
        {
            if (action.ActionType == CommandActionType.Synonym)
            {
                var synonymTargetActionId = ActionPayloadAccessors.GetSynonymTargetActionId(action);

                if (!synonymTargetActionId.HasValue)
                {
                    errors.Add($"- {scopeLabel}: synonym action '{action.Name}' requires a target action.");
                }
                else if (synonymTargetActionId.Value == action.Id)
                {
                    errors.Add($"- {scopeLabel}: synonym action '{action.Name}' cannot target itself.");
                }
                else if (!actionById.ContainsKey(synonymTargetActionId.Value))
                {
                    errors.Add($"- {scopeLabel}: synonym action '{action.Name}' targets missing action id '{synonymTargetActionId.Value}'.");
                }
            }

            foreach (var link in action.LinkedActions)
            {
                linkedTargets.Add(link.ActionId);
                if (link.ActionId == action.Id)
                {
                    errors.Add($"- {scopeLabel}: action '{action.Name}' cannot link to itself.");
                }

                if (!linkTargetById.ContainsKey(link.ActionId))
                {
                    errors.Add($"- {scopeLabel}: action '{action.Name}' links to missing action id '{link.ActionId}'.");
                    continue;
                }

                if (linkTargetById[link.ActionId].ActionType == CommandActionType.Synonym)
                {
                    errors.Add($"- {scopeLabel}: linked flow cannot target synonym action '{linkTargetById[link.ActionId].Name}'.");
                }
            }
        }

        foreach (var synonymAction in actions.Where(action => action.ActionType == CommandActionType.Synonym))
        {
            if (linkedTargets.Contains(synonymAction.Id))
            {
                errors.Add($"- {scopeLabel}: synonym action '{synonymAction.Name}' cannot be used in linked-action flow.");
            }
        }

        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();
        var stack = new Stack<Guid>();

        foreach (var action in actions)
        {
            if (!visited.Contains(action.Id))
            {
                Visit(action.Id);
            }
        }

        return errors;

        void Visit(Guid actionId)
        {
            if (visiting.Contains(actionId))
            {
                var path = stack.Reverse().Concat(new[] { actionId }).ToList();
                errors.Add($"- {scopeLabel}: linked-action cycle detected ({FormatPath(path)}).");
                return;
            }

            if (visited.Contains(actionId))
            {
                return;
            }

            if (!actionById.TryGetValue(actionId, out var action))
            {
                return;
            }

            visiting.Add(actionId);
            stack.Push(actionId);

            foreach (var targetId in action.LinkedActions
                         .Select(link => link.ActionId)
                         .Where(actionById.ContainsKey)
                         .Distinct())
            {
                Visit(targetId);
            }

            stack.Pop();
            visiting.Remove(actionId);
            visited.Add(actionId);
        }

        string FormatPath(List<Guid> ids)
        {
            var names = ids
                .Select(id => actionById.TryGetValue(id, out var node)
                    ? $"{node.Name} ({id:N})"
                    : id.ToString("N"))
                .ToList();
            return string.Join(" -> ", names);
        }
    }
}
