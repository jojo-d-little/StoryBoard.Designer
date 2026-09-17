using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

public sealed class OutcomeSoundEffectReferenceExistsRule : IValidationRule
{
    public ValidationRuleMetadata Metadata { get; } = new("ACT-015", "Outcome Sound Effect Reference Exists", ValidationSeverity.Warning, "Actions");
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } = EmptyScopeNodeKindSet.Instance;
    public bool SupportsActionCandidates => true;

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext ruleContext)
    {
        if (!ActionRuleSupport.TryGetCandidateContext(ruleContext, out var context))
        {
            yield break;
        }

        var action = context.Action;
        if (action.OutcomeSoundEffectsMap.Count == 0)
        {
            yield break;
        }

        var knownSoundEffectIds = ruleContext.SharedState.GetOrAdd(
            "ACT:KnownProjectSoundEffectIds",
            () => BuildKnownSoundEffectIdSet(ruleContext.Project));

        foreach (var (resultCode, cues) in action.OutcomeSoundEffectsMap)
        {
            var token = resultCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            for (var cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                var cue = cues[cueIndex];
                if (cue.SoundEffectId == Guid.Empty)
                {
                    continue;
                }

                if (knownSoundEffectIds.Contains(cue.SoundEffectId))
                {
                    continue;
                }

                var hint = string.IsNullOrWhiteSpace(cue.SoundEffectKeyHint)
                    ? string.Empty
                    : $" (hint: '{cue.SoundEffectKeyHint.Trim()}')";

                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    ActionRuleSupport.BuildActionIssuePath(context.Path, action),
                    $"Action '{action.Name}' result code '{token}' cue #{cueIndex + 1} references missing soundEffectId '{cue.SoundEffectId:D}'{hint}.",
                    "Pick an existing sound effect in the Result Code Sound Effects tab or remove the stale cue.");
            }
        }
    }

    private static HashSet<Guid> BuildKnownSoundEffectIdSet(ProjectModel project)
    {
        var known = new HashSet<Guid>();

        static void AddEntries(IEnumerable<SoundEffectLibraryEntry> entries, ISet<Guid> set)
        {
            foreach (var entry in entries)
            {
                if (entry.SoundEffectId != Guid.Empty)
                {
                    set.Add(entry.SoundEffectId);
                }
            }
        }

        static void AddObjectRecursive(GameObject gameObject, ISet<Guid> set)
        {
            AddEntries(gameObject.SoundEffectLibraryEntries, set);
            foreach (var child in gameObject.ContainedObjects)
            {
                AddObjectRecursive(child, set);
            }
        }

        AddEntries(project.GlobalScope.SoundEffectLibraryEntries, known);
        AddEntries(project.RoomTemplates.SelectMany(static room => room.SoundEffectLibraryEntries), known);

        foreach (var root in project.GlobalScope.GameObjects)
        {
            AddObjectRecursive(root, known);
        }

        foreach (var root in project.ObjectTemplates)
        {
            AddObjectRecursive(root, known);
        }

        foreach (var root in project.BaseObjects)
        {
            AddObjectRecursive(root, known);
        }

        foreach (var planet in project.Planets)
        {
            AddEntries(planet.SoundEffectLibraryEntries, known);
            foreach (var root in planet.GameObjects)
            {
                AddObjectRecursive(root, known);
            }

            foreach (var root in planet.BaseObjects)
            {
                AddObjectRecursive(root, known);
            }

            foreach (var country in planet.Countries)
            {
                AddEntries(country.SoundEffectLibraryEntries, known);
                foreach (var root in country.GameObjects)
                {
                    AddObjectRecursive(root, known);
                }

                foreach (var root in country.BaseObjects)
                {
                    AddObjectRecursive(root, known);
                }

                foreach (var area in country.Areas)
                {
                    AddEntries(area.SoundEffectLibraryEntries, known);
                    foreach (var root in area.GameObjects)
                    {
                        AddObjectRecursive(root, known);
                    }

                    foreach (var root in area.BaseObjects)
                    {
                        AddObjectRecursive(root, known);
                    }

                    foreach (var room in area.Rooms)
                    {
                        AddEntries(room.SoundEffectLibraryEntries, known);
                        foreach (var root in room.GameObjects)
                        {
                            AddObjectRecursive(root, known);
                        }
                    }
                }
            }
        }

        return known;
    }
}
