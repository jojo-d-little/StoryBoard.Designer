using Storyboard.Shared.GameManager;
using Storyboard.Shared.GameServices;

namespace StoryboardDesigner.App.Tests;

internal sealed class CleanProjectRuntimeLoadedGameLoader : IRuntimeLoadedGameLoader
{
    private readonly IGameProjectRuntimeLoaderService _runtimeLoaderService;

    public CleanProjectRuntimeLoadedGameLoader(IGameProjectRuntimeLoaderService runtimeLoaderService)
    {
        _runtimeLoaderService = runtimeLoaderService;
    }

    public RuntimeLoadedGame? TryLoad(string projectFilePath)
    {
        var runtimeBootstrap = _runtimeLoaderService.TryLoadRuntimeBootstrap(projectFilePath);
        if (runtimeBootstrap is null)
        {
            return null;
        }

        var runtimeSnapshot = _runtimeLoaderService.TryLoadRuntimeSnapshot(runtimeBootstrap);
        if (runtimeSnapshot is null)
        {
            return null;
        }

        return new RuntimeLoadedGame(
            SourceProjectFilePath: projectFilePath,
            WorldSnapshot: runtimeSnapshot,
            LoadDiagnostics: runtimeBootstrap.Diagnostics);
    }
}
