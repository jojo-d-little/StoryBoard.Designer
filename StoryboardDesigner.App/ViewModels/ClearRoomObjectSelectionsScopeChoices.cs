using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.ViewModels;

internal static class ClearRoomObjectSelectionsScopeChoices
{
    public static IReadOnlyList<ClearRoomObjectSelectionsScope> ClearActiveRoomObjects { get; } =
    [
        ClearRoomObjectSelectionsScope.All,
        ClearRoomObjectSelectionsScope.PrimaryOnly,
        ClearRoomObjectSelectionsScope.SecondaryOnly
    ];

    public static IReadOnlyList<ClearRoomObjectSelectionsScope> ClearRoomObjectSelections { get; } =
        Enum.GetValues<ClearRoomObjectSelectionsScope>();
}
