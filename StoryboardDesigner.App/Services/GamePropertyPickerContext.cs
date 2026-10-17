using Storyboard.Shared.DesignerContracts;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Services;

/// <summary>
/// Seeds the shared property picker with the project and the scope being edited.
/// The picker asks the project context for choices when it opens; callers do not
/// need to build a choice list themselves.
/// </summary>
public sealed record GamePropertyPickerContext(
    ProjectModel Project,
    IScopedAwareNode? CurrentScope,
    PropertyResolutionScope InitializationScope,
    Func<bool, IReadOnlyList<GamePropertyChoiceItem>> DiscoverChoices,
    bool BooleanOnly = false,
    bool AllowReferenceModes = false,
    Guid? RelativeOwnerId = null,
    IReadOnlyList<string>? AnchorKeys = null,
    ProjectGamePropertySourceReferenceDto? SelectedReference = null,
    bool AllowFixedReferenceMode = true);
