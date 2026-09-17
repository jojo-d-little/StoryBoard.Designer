namespace StoryboardDesigner.App.Services;

/// <summary>
/// Provides authoring-time phase text presentation cue options.
/// </summary>
public interface IPhaseTextPresentationCueCatalogService
{
    /// <summary>
    /// Gets the available text presentation cue options for phase text fields.
    /// </summary>
    /// <returns>Ordered list of text cue options.</returns>
    IReadOnlyList<PhaseTextPresentationCueOption> GetTextCueOptions();
}
