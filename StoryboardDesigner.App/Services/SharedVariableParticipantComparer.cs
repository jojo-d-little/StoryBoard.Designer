using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

internal sealed class SharedVariableParticipantComparer : IEqualityComparer<SharedVariableParticipant>
{
    internal static readonly SharedVariableParticipantComparer Instance = new();

    public bool Equals(SharedVariableParticipant? x, SharedVariableParticipant? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.OwnerId == y.OwnerId
            && string.Equals(x.Kind, y.Kind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.VariableName, y.VariableName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Leg ?? string.Empty, y.Leg ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(SharedVariableParticipant obj)
    {
        return HashCode.Combine(
            obj.OwnerId,
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Kind ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.VariableName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Leg ?? string.Empty));
    }
}
