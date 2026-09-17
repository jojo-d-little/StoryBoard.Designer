using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;
using Storyboard.Shared.GameServices.References;

namespace StoryboardDesigner.App.Validation.Rules.Scripting;

internal static class ScriptRuleSupport
{
    internal static IReadOnlyList<string> BuildKnownReferenceTokens(ProjectModel project)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "self.name",
            "self.nameInGame",
            "self.objectName"
        };

        foreach (var anchorToken in RuntimeAnchorReferenceTokenCatalog.BuildIntrinsicAnchorTokens())
        {
            tokens.Add(anchorToken);
        }

        foreach (var variable in project.GlobalVariables)
        {
            AddVariableToken(tokens, variable.Name);
        }

        foreach (var globalObject in EnumerateGameObjects(project.GlobalScope.GameObjects))
        {
            AddObjectVariableTokens(tokens, globalObject);
        }

        foreach (var templateObject in EnumerateGameObjects(project.ObjectTemplates))
        {
            AddObjectVariableTokens(tokens, templateObject);
        }

        foreach (var planet in project.Planets)
        {
            AddScopedVariables(tokens, planet.Variables);
            foreach (var planetObject in EnumerateGameObjects(planet.GameObjects))
            {
                AddObjectVariableTokens(tokens, planetObject);
            }

            foreach (var country in planet.Countries)
            {
                AddScopedVariables(tokens, country.Variables);
                foreach (var countryObject in EnumerateGameObjects(country.GameObjects))
                {
                    AddObjectVariableTokens(tokens, countryObject);
                }

                foreach (var area in country.Areas)
                {
                    AddScopedVariables(tokens, area.Variables);
                    foreach (var areaObject in EnumerateGameObjects(area.GameObjects))
                    {
                        AddObjectVariableTokens(tokens, areaObject);
                    }

                    foreach (var room in area.Rooms)
                    {
                        AddScopedVariables(tokens, room.Variables);

                        foreach (var roomObject in EnumerateGameObjects(room.GameObjects))
                        {
                            AddObjectVariableTokens(tokens, roomObject);
                        }
                    }
                }
            }
        }

        return tokens.OrderBy(static token => token, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static IEnumerable<(CommandAction Action, string Path)> EnumerateActionContexts(ProjectModel project)
    {
        foreach (var globalObject in EnumerateGameObjects(project.GlobalScope.GameObjects))
        {
            var objectPath = $"Global / {globalObject.Name}";
            foreach (var action in globalObject.AvailableActions)
            {
                yield return (action, objectPath);
            }
        }

        foreach (var templateObject in EnumerateGameObjects(project.ObjectTemplates))
        {
            var objectPath = $"Global / {templateObject.Name}";
            foreach (var action in templateObject.AvailableActions)
            {
                yield return (action, objectPath);
            }
        }

        foreach (var planet in project.Planets)
        {
            foreach (var action in planet.AvailableActions)
            {
                yield return (action, $"Global / {planet.Name}");
            }

            foreach (var planetObject in EnumerateGameObjects(planet.GameObjects))
            {
                var objectPath = $"Global / {planet.Name} / {planetObject.Name}";
                foreach (var action in planetObject.AvailableActions)
                {
                    yield return (action, objectPath);
                }
            }

            foreach (var country in planet.Countries)
            {
                foreach (var action in country.AvailableActions)
                {
                    yield return (action, $"Global / {planet.Name} / {country.Name}");
                }

                foreach (var countryObject in EnumerateGameObjects(country.GameObjects))
                {
                    var objectPath = $"Global / {planet.Name} / {country.Name} / {countryObject.Name}";
                    foreach (var action in countryObject.AvailableActions)
                    {
                        yield return (action, objectPath);
                    }
                }

                foreach (var area in country.Areas)
                {
                    foreach (var action in area.AvailableActions)
                    {
                        yield return (action, $"Global / {planet.Name} / {country.Name} / {area.Name}");
                    }

                    foreach (var areaObject in EnumerateGameObjects(area.GameObjects))
                    {
                        var objectPath = $"Global / {planet.Name} / {country.Name} / {area.Name} / {areaObject.Name}";
                        foreach (var action in areaObject.AvailableActions)
                        {
                            yield return (action, objectPath);
                        }
                    }

                    foreach (var room in area.Rooms)
                    {
                        foreach (var action in room.AvailableActions)
                        {
                            yield return (action, $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name}");
                        }

                        foreach (var roomObject in EnumerateGameObjects(room.GameObjects))
                        {
                            var objectPath = $"Global / {planet.Name} / {country.Name} / {area.Name} / {room.Name} / {roomObject.Name}";
                            foreach (var action in roomObject.AvailableActions)
                            {
                                yield return (action, objectPath);
                            }
                        }
                    }
                }
            }
        }
    }

    internal static IEnumerable<Validation.Contracts.ValidationIssue> AnalyzeScriptField(
        string actionScopePath,
        string actionName,
        string fieldName,
        string scriptText,
        IReadOnlyList<string> referenceTokens,
        string ruleId,
        Validation.Contracts.ValidationSeverity severity,
        Func<string, bool> includeMessage)
    {
        if (string.IsNullOrWhiteSpace(scriptText))
        {
            yield break;
        }

        var diagnostics = ActionScriptEditorDiagnosticsAnalyzer.Analyze(scriptText, referenceTokens);
        foreach (var warning in diagnostics.Warnings.Where(message => includeMessage(message)))
        {
            yield return BuildIssue(ruleId, severity, actionScopePath, actionName, fieldName, warning);
        }

        foreach (var error in diagnostics.Errors.Where(message => includeMessage(message)))
        {
            yield return BuildIssue(ruleId, severity, actionScopePath, actionName, fieldName, error);
        }
    }

    internal static bool IsIfElseBlockIntegrityDiagnostic(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("missing ENDIF", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Duplicate ELSE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ELSE IF cannot appear after ELSE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Use ENDIF to close IF/ELSE blocks", StringComparison.OrdinalIgnoreCase)
               || message.Contains("without a matching IF", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ENDIF found without", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ELSE found without", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ELSE IF found without", StringComparison.OrdinalIgnoreCase)
               || message.Contains("IF block is missing ENDIF", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildActionIssuePath(string scopePath, string actionName)
    {
        var prefix = string.IsNullOrWhiteSpace(scopePath) ? "Project" : scopePath.Trim();
        var actionLabel = string.IsNullOrWhiteSpace(actionName) ? "(unnamed action)" : actionName.Trim();
        return $"{prefix} / Action:{actionLabel}";
    }

    private static Validation.Contracts.ValidationIssue BuildIssue(
        string ruleId,
        Validation.Contracts.ValidationSeverity severity,
        string actionScopePath,
        string actionName,
        string fieldName,
        string message)
    {
        return new Validation.Contracts.ValidationIssue(
            ruleId,
            severity,
                BuildActionIssuePath(actionScopePath, actionName),
            $"Action '{(string.IsNullOrWhiteSpace(actionName) ? "(unnamed action)" : actionName)}' {fieldName}: {message}");
    }

    private static void AddScopedVariables(HashSet<string> tokens, IEnumerable<GamePropertyDefinition> variables)
    {
        foreach (var variable in variables)
        {
            AddVariableToken(tokens, variable.Name);
        }
    }

    private static void AddVariableToken(HashSet<string> tokens, string? variableName)
    {
        var name = variableName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        tokens.Add(name);
    }

    private static void AddObjectVariableTokens(HashSet<string> tokens, GameObject obj)
    {
        var objectToken = obj.Name?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(objectToken))
        {
            tokens.Add($"{objectToken}.description");
        }

        if (obj.IsQuantifiable)
        {
            tokens.Add("self.nearByQuantity");

            if (!string.IsNullOrWhiteSpace(objectToken))
            {
                tokens.Add($"{objectToken}.nearByQuantity");
            }
        }

        foreach (var variable in obj.Variables)
        {
            var variableName = variable.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            tokens.Add(variableName);
            tokens.Add($"self.{variableName}");

            if (!string.IsNullOrWhiteSpace(objectToken))
            {
                tokens.Add($"{objectToken}.{variableName}");
            }
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
}
