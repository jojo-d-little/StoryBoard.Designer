namespace StoryboardDesigner.App.Services;

public sealed class TraversalWizardDialogResult
{
    public static TraversalWizardDialogResult Empty { get; } = new()
    {
        Rows = Array.Empty<TraversalWizardDirectionChoice>()
    };

    public required IReadOnlyList<TraversalWizardDirectionChoice> Rows { get; init; }
}
