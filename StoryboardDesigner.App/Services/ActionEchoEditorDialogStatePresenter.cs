using System.Collections.ObjectModel;

namespace StoryboardDesigner.App.Services;

public sealed record ActionEchoEditorDialogUiState(
    bool HasSelection,
    bool IsUnsupportedWarningVisible,
    string UnsupportedWarningText);

public static class ActionEchoEditorDialogStatePresenter
{
    public static ActionEchoEditorDialogUiState Build(
        ReadOnlyCollection<ActionEchoEditorEntry> entries,
        ActionEchoEditorEntry? selectedEntry)
    {
        var unsupportedCount = entries.Count(static entry => !entry.IsSupported);
        var warningVisible = unsupportedCount > 0;
        var warningText = UnsupportedOutcomeEntryWarningTextFormatter.BuildBannerText(unsupportedCount);

        return new ActionEchoEditorDialogUiState(
            HasSelection: selectedEntry is not null,
            IsUnsupportedWarningVisible: warningVisible,
            UnsupportedWarningText: warningText);
    }
}
