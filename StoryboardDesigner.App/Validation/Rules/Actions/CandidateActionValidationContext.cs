using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

internal sealed record CandidateActionValidationContext(
    string Path,
    CommandAction Action,
    IReadOnlyList<CommandAction> Actions,
    IReadOnlyDictionary<Guid, CommandAction> ActionById,
    IReadOnlyDictionary<Guid, CommandAction> LinkTargetById);
