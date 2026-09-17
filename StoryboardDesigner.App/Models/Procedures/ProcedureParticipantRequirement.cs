namespace StoryboardDesigner.App.Models;

public sealed class ProcedureParticipantRequirement
{
    public Guid ObjectId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public ProcedureParticipantMatchKind MatchKind { get; set; } = ProcedureParticipantMatchKind.ObjectId;
    public string MatchValue { get; set; } = string.Empty;
    public ProcedureParticipantSatisfactionMode SatisfactionMode { get; set; } = ProcedureParticipantSatisfactionMode.ExplicitMentionRequired;
    public int Quantity { get; set; } = 1;
    public ProcedureParticipantConsumptionPolicy ConsumptionPolicy { get; set; } = ProcedureParticipantConsumptionPolicy.None;
    public bool OptionalPart { get; set; }
    public List<LockParticipantVariableRequirement> VariableRequirements { get; set; } = new();

    public string ParticipantSummary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim();

            if (MatchKind == ProcedureParticipantMatchKind.ObjectId
                && Guid.TryParse(MatchValue, out var objectId))
            {
                var summary = string.IsNullOrWhiteSpace(name)
                    ? $"obj {objectId:N}"
                    : $"{name} (obj {objectId:N})";
                return OptionalPart ? $"{summary} (optional)" : summary;
            }

            var kind = MatchKind.ToString();
            var value = string.IsNullOrWhiteSpace(MatchValue) ? "(not set)" : MatchValue.Trim();
            var fallbackSummary = string.IsNullOrWhiteSpace(name)
                ? $"{kind}={value}"
                : $"{name}: {kind}={value}";
            return OptionalPart ? $"{fallbackSummary} (optional)" : fallbackSummary;
        }
    }
}