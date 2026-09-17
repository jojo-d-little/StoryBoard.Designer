namespace StoryboardDesigner.App.Models;

public sealed class ProcedureParticipantMutation
{
    public Guid ObjectId { get; set; }
    public string ParticipantDisplayName { get; set; } = string.Empty;
    public ProcedureParticipantMutationOperation Operation { get; set; }
    public string VariableName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double? Delta { get; set; }

    public string MutationSummary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(ParticipantDisplayName) ? string.Empty : ParticipantDisplayName.Trim();
            var key = ObjectId == Guid.Empty
                ? "obj (not set)"
                : string.IsNullOrWhiteSpace(name)
                    ? $"obj {ObjectId:N}"
                    : $"{name} (obj {ObjectId:N})";
            var variable = string.IsNullOrWhiteSpace(VariableName) ? "(variable)" : VariableName.Trim();
            return $"{key}: {Operation} {variable}";
        }
    }
}