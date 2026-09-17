using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class LockKeyRequirement
{
    public Guid RequiredObjectId { get; set; }
    public string RequiredObjectName { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; } = 1;
    public ProcedureParticipantMatchKind MatchKind { get; set; } = ProcedureParticipantMatchKind.ObjectId;
    public string MatchValue { get; set; } = string.Empty;
    public ProcedureParticipantSatisfactionMode SatisfactionMode { get; set; } = ProcedureParticipantSatisfactionMode.PossessionRequired;
    public ProcedureParticipantConsumptionPolicy ConsumptionPolicy { get; set; } = ProcedureParticipantConsumptionPolicy.None;
    public bool IsOptional { get; set; }
    public List<LockParticipantVariableRequirement> VariableRequirements { get; set; } = new();

    public string ParticipantSummary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(RequiredObjectName)
                ? "(unnamed object)"
                : RequiredObjectName.Trim();
            var identity = MatchKind == ProcedureParticipantMatchKind.ObjectId
                ? (RequiredObjectId == Guid.Empty ? "(no object)" : RequiredObjectId.ToString("D"))
                : (string.IsNullOrWhiteSpace(MatchValue) ? "(no match value)" : MatchValue.Trim());
            var optionalText = IsOptional ? "optional" : "required";
            return $"{name} | {identity} qty {Math.Max(1, RequiredQuantity)} {optionalText}";
        }
    }
}
