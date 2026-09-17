using Storyboard.Shared.GameStateData;

namespace Storyboard.Shared.GameServices;

public interface IGameProjectRuntimeLoaderService : IRuntimeProjectLoaderService
{
    RuntimeGameWorldSnapshot? TryLoadRuntimeSnapshot(CleanRuntimeBootstrap runtimeBootstrap);
}
