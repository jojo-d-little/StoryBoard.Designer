using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class CompositePartRequirement
{
    public Guid PartObjectId { get; set; }
    public string PartObjectName { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; } = 1;
    public ProcedureParticipantMatchKind MatchKind { get; set; } = ProcedureParticipantMatchKind.ObjectId;
    public string MatchValue { get; set; } = string.Empty;
    public ProcedureParticipantSatisfactionMode SatisfactionMode { get; set; } = ProcedureParticipantSatisfactionMode.PossessionRequired;
    public ProcedureParticipantConsumptionPolicy ConsumptionPolicy { get; set; } = ProcedureParticipantConsumptionPolicy.None;
    public bool OptionalPart { get; set; }
    public List<LockParticipantVariableRequirement> VariableRequirements { get; set; } = new();

    public string ParticipantSummary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(PartObjectName)
                ? "(unnamed object)"
                : PartObjectName.Trim();
            var identity = MatchKind == ProcedureParticipantMatchKind.ObjectId
                ? (PartObjectId == Guid.Empty ? "(no object)" : PartObjectId.ToString("D"))
                : (string.IsNullOrWhiteSpace(MatchValue) ? "(no match value)" : MatchValue.Trim());
            var optionalText = OptionalPart ? "optional" : "required";
            return $"{name} | {identity} qty {Math.Max(1, RequiredQuantity)} {optionalText}";
        }
    }
}
