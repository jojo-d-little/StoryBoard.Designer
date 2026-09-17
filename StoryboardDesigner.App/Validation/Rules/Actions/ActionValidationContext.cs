using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Rules.Actions;

internal sealed record ActionValidationContext(
    string Path,
    IReadOnlyList<CommandAction> Actions,
    IReadOnlyDictionary<Guid, CommandAction> ActionById,
    IReadOnlyDictionary<Guid, CommandAction> LinkTargetById);
