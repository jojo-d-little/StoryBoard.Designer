using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Validation.Contracts;

namespace StoryboardDesigner.App.Validation.Rules.Project;

public sealed class ProcedureParticipantObjectReferenceExistsRule : IValidationRule
{
    public IReadOnlySet<ScopeNodeKind> SupportedCandidateScopeKinds { get; } =
        new HashSet<ScopeNodeKind> { ScopeNodeKind.Global };

    public ValidationRuleMetadata Metadata { get; } = new(
        RuleId: "PROJ-010",
        Title: "Procedure Participant Object Reference Exists",
        DefaultSeverity: ValidationSeverity.Error,
        Category: "Project");

    public IEnumerable<ValidationIssue> Evaluate(ValidationRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CandidateNode is not ProjectModel)
        {
            yield break;
        }

        var objectIds = BuildProjectObjectIdSet(context.Project);

        foreach (var procedure in context.Project.Procedures)
        {
            var procedureLabel = NormalizeName(procedure.Name, "Procedure");

            for (var i = 0; i < procedure.Participants.Count; i++)
            {
                var participant = procedure.Participants[i];
                if (participant.ObjectId == Guid.Empty || objectIds.Contains(participant.ObjectId))
                {
                    continue;
                }

                var participantLabel = NormalizeName(participant.DisplayName, $"participant[{i + 1}]");
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    $"Project / Procedures / {procedureLabel}",
                    $"Procedure '{procedureLabel}' includes participant '{participantLabel}' referencing missing object id '{participant.ObjectId:N}'.",
                    "Open Procedure Designer and select an object that exists in the project.");
            }

            for (var i = 0; i < procedure.ParticipantMutations.Count; i++)
            {
                var mutation = procedure.ParticipantMutations[i];
                if (mutation.ObjectId == Guid.Empty || objectIds.Contains(mutation.ObjectId))
                {
                    continue;
                }

                var mutationLabel = NormalizeName(mutation.ParticipantDisplayName, $"mutation[{i + 1}]");
                yield return new ValidationIssue(
                    Metadata.RuleId,
                    Metadata.DefaultSeverity,
                    $"Project / Procedures / {procedureLabel}",
                    $"Procedure '{procedureLabel}' includes mutation participant '{mutationLabel}' referencing missing object id '{mutation.ObjectId:N}'.",
                    "Open Procedure Designer and update the mutation participant to an existing project object.");
            }
        }
    }

    private static HashSet<Guid> BuildProjectObjectIdSet(ProjectModel project)
    {
        var ids = new HashSet<Guid>();

        static void AddRange(HashSet<Guid> target, IEnumerable<GameObject> roots)
        {
            foreach (var root in roots)
            {
                if (root.ObjectId != Guid.Empty)
                {
                    target.Add(root.ObjectId);
                }

                AddRange(target, root.ContainedObjects);
            }
        }

        AddRange(ids, project.GlobalScope.GameObjects);
        AddRange(ids, project.ObjectTemplates);
        AddRange(ids, project.BaseObjects);
        AddRange(ids, project.RoomTemplates.SelectMany(static room => room.GameObjects));

        foreach (var planet in project.Planets)
        {
            AddRange(ids, planet.BaseObjects);
            AddRange(ids, planet.GameObjects);

            foreach (var country in planet.Countries)
            {
                AddRange(ids, country.BaseObjects);
                AddRange(ids, country.GameObjects);

                foreach (var area in country.Areas)
                {
                    AddRange(ids, area.BaseObjects);
                    AddRange(ids, area.GameObjects);
                    AddRange(ids, area.Rooms.SelectMany(static room => room.GameObjects));
                }
            }
        }

        return ids;
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}