using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public static class GamePropertyLifetimeValues
{
    public static IReadOnlyList<GamePropertyLifetime> All { get; } = Enum.GetValues<GamePropertyLifetime>();
}
