using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;
using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

/// <summary>
/// Verifies sound-effect IDs against the same current/ancestor/descendant scope choices
/// exposed by the Designer, instead of treating every project sound as reachable.
/// </summary>
public sealed class ScopedSoundEffectReferenceRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new(
        "ACT-019",
        "Scoped Sound Effect Reference Reachability",
        ValidationSeverity.Error,
        "Actions");

    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        Enum.GetValues<ScopeNodeKind>().ToHashSet();

    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        var locationsById = ruleContext.SharedState.GetOrAdd(
            "ACT:SoundEffectReferenceLocations",
            () => BuildSoundEffectLocations(ruleContext.Project));

        if (ruleContext.CandidateNode is ValidationActionCandidateNode actionCandidate)
        {
            var action = actionCandidate.Action;
            var availableIds = BuildAvailableSoundEffectIds(ruleContext.Project, actionCandidate.OwnerScope);
            var path = ActionRuleSupport.BuildActionIssuePath(actionCandidate.OwnerScopePath, action);

            foreach (var (outcome, cues) in action.OutcomeSoundEffectsMap)
            {
                for (var index = 0; index < cues.Count; index++)
                {
                    var cue = cues[index];
                    if (cue.SoundEffectId == Guid.Empty
                        || !locationsById.TryGetValue(cue.SoundEffectId, out var locations)
                        || availableIds.Contains(cue.SoundEffectId))
                    {
                        // ACT-015 reports project-wide missing action outcome sounds.
                        continue;
                    }

                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        path,
                        $"action '{DisplayName(action)}' outcome '{outcome}' sound cue #{index + 1} references sound effect '{DisplaySoundEffect(locations[0].Entry)}' defined at {FormatLocations(locations)}, outside the sound-effect scopes available from '{BuildScopePath(actionCandidate.OwnerScope)}'.",
                        "Choose a sound effect owned by this scope, an ancestor, or a permitted child scope, or move the sound-effect definition into a reachable scope.");
                }
            }

            yield break;
        }

        if (ruleContext.CandidateNode is not ScopeNodeBase ownerScope)
        {
            yield break;
        }

        if (ownerScope is GameObject { IsRoomInstance: true })
        {
            yield break;
        }

        var availableForSequences = BuildAvailableSoundEffectIds(ruleContext.Project, ownerScope);
        var scopePath = BuildScopePath(ownerScope);
        foreach (var sequence in ownerScope.SequenceDefinitions ?? new List<Storyboard.Shared.DesignerContracts.ProjectSequenceDefinitionDto>())
        {
            var sequenceName = string.IsNullOrWhiteSpace(sequence.DisplayName) ? "(unnamed sequence)" : sequence.DisplayName.Trim();
            var branchNames = (sequence.Branches ?? new List<Storyboard.Shared.DesignerContracts.ProjectSequenceBranchDto>())
                .Where(static branch => !string.IsNullOrWhiteSpace(branch.BranchId))
                .GroupBy(static branch => branch.BranchId, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => string.IsNullOrWhiteSpace(group.First().DisplayName) ? group.Key : group.First().DisplayName.Trim(),
                    StringComparer.Ordinal);
            for (var index = 0; index < sequence.Steps.Count; index++)
            {
                var step = sequence.Steps[index];
                if (step.StepType != SequenceStepType.Sound
                    || !step.SoundEffectId.HasValue
                    || step.SoundEffectId.Value == Guid.Empty)
                {
                    continue;
                }

                var branchName = !string.IsNullOrWhiteSpace(step.BranchId)
                    && branchNames.TryGetValue(step.BranchId, out var resolvedBranchName)
                        ? resolvedBranchName
                        : string.IsNullOrWhiteSpace(step.BranchId) ? "(unnamed branch)" : step.BranchId;
                var stepPath = $"{scopePath} / Sequence '{sequenceName}' / Branch '{branchName}' / Step {index + 1} ({step.StepId:D})";
                if (!locationsById.TryGetValue(step.SoundEffectId.Value, out var locations))
                {
                    yield return new ValidationIssue(
                        Metadata.RuleId,
                        Metadata.DefaultSeverity,
                        stepPath,
                        $"sequence '{sequenceName}' branch '{branchName}' step {index + 1} ('{step.StepId:D}') references sound effect id '{step.SoundEffectId.Value:D}', which is not defined in the project.",
                        "Choose an existing sound effect from this sequence's sound-effect choices.");
                    continue;
                }

                if (availableForSequences.Contains(step.SoundEffectId.Value))
                {
                    continue;
                }

                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    stepPath,
                    $"sequence '{sequenceName}' branch '{branchName}' step {index + 1} ('{step.StepId:D}') references sound effect '{DisplaySoundEffect(locations[0].Entry)}' defined at {FormatLocations(locations)}, outside the sound-effect scopes available from '{scopePath}'.",
                    "Choose a sound effect owned by this scope, an ancestor, or a permitted child scope, or move the sound-effect definition into a reachable scope.");
            }
        }
    }

    private static HashSet<Guid> BuildAvailableSoundEffectIds(ProjectModel project, ScopeNodeBase ownerScope)
    {
        var ids = new HashSet<Guid>();

        foreach (var scope in ownerScope.EnumerateSelfAndAncestors().OfType<ScopeNodeBase>())
        {
            AddSoundEffectIds(GetOwnedSoundEffects(project, scope), ids);
        }

        if (ownerScope is GameObject currentObject)
        {
            foreach (var descendant in EnumerateObjects(currentObject.ContainedObjects))
            {
                AddSoundEffectIds(descendant.SoundEffectLibraryEntries, ids);
            }
        }
        else if (ownerScope is Room currentRoom)
        {
            foreach (var roomObject in EnumerateObjects(currentRoom.GameObjects))
            {
                AddSoundEffectIds(roomObject.SoundEffectLibraryEntries, ids);
            }
        }

        // Global sound effects are offered to every scope by the authoring picker.
        AddSoundEffectIds(project.GlobalScope.SoundEffectLibraryEntries, ids);
        return ids;
    }

    private static IEnumerable<SoundEffectLibraryEntry> GetOwnedSoundEffects(ProjectModel project, ScopeNodeBase scope)
    {
        return scope switch
        {
            ProjectModel => project.GlobalScope.SoundEffectLibraryEntries,
            Planet planet => planet.SoundEffectLibraryEntries,
            Country country => country.SoundEffectLibraryEntries,
            Area area => area.SoundEffectLibraryEntries,
            Room room => room.SoundEffectLibraryEntries,
            GameObject gameObject => gameObject.SoundEffectLibraryEntries,
            _ => Array.Empty<SoundEffectLibraryEntry>()
        };
    }

    private static Dictionary<Guid, List<SoundEffectLocation>> BuildSoundEffectLocations(ProjectModel project)
    {
        var locations = new Dictionary<Guid, List<SoundEffectLocation>>();

        void AddEntries(IEnumerable<SoundEffectLibraryEntry> entries, string scopePath)
        {
            foreach (var entry in entries)
            {
                if (entry.SoundEffectId == Guid.Empty)
                {
                    continue;
                }

                if (!locations.TryGetValue(entry.SoundEffectId, out var matches))
                {
                    matches = new List<SoundEffectLocation>();
                    locations.Add(entry.SoundEffectId, matches);
                }

                matches.Add(new SoundEffectLocation(entry, scopePath));
            }
        }

        void Visit(IScopedAwareNode node)
        {
            if (node is ScopeNodeBase scope)
            {
                AddEntries(GetOwnedSoundEffects(project, scope), BuildScopePath(scope));
            }

            foreach (var child in node.ChildScopes)
            {
                Visit(child);
            }
        }

        Visit(project);
        return locations;
    }

    private static void AddSoundEffectIds(IEnumerable<SoundEffectLibraryEntry> entries, ISet<Guid> target)
    {
        foreach (var entry in entries)
        {
            if (entry.SoundEffectId != Guid.Empty)
            {
                target.Add(entry.SoundEffectId);
            }
        }
    }

    private static IEnumerable<GameObject> EnumerateObjects(IEnumerable<GameObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in EnumerateObjects(root.ContainedObjects))
            {
                yield return child;
            }
        }
    }

    private static string BuildScopePath(IScopedAwareNode scope)
    {
        return string.Join(" / ", scope
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static node => node.ScopeName)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
    }

    private static string DisplayName(CommandAction action)
    {
        return string.IsNullOrWhiteSpace(action.Name) ? "(unnamed action)" : action.Name.Trim();
    }

    private static string DisplaySoundEffect(SoundEffectLibraryEntry entry)
    {
        var displayName = entry.DisplayName?.Trim();
        return string.IsNullOrWhiteSpace(displayName) ? entry.SoundEffectKey : displayName;
    }

    private static string FormatLocations(IReadOnlyList<SoundEffectLocation> locations)
    {
        return string.Join(", ", locations.Select(static location => $"'{location.ScopePath}'"));
    }

    private sealed record SoundEffectLocation(SoundEffectLibraryEntry Entry, string ScopePath);
}
