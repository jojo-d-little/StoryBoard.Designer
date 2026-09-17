using Storyboard.Shared.GameStateData;

namespace Storyboard.Shared.GameServices;

public sealed class GameProjectRuntimeLoaderService : IGameProjectRuntimeLoaderService
{
    private readonly ICleanExportDataReader _cleanExportDataReader;
    private readonly ICleanRuntimeBootstrapBuilder _cleanRuntimeBootstrapBuilder;
    private readonly ICleanRuntimeBootstrapSnapshotMapper _snapshotMapper;

    public GameProjectRuntimeLoaderService(
        ICleanExportDataReader? cleanExportDataReader = null,
        ICleanRuntimeBootstrapBuilder? cleanRuntimeBootstrapBuilder = null,
        ICleanRuntimeBootstrapSnapshotMapper? snapshotMapper = null)
    {
        _cleanExportDataReader = cleanExportDataReader ?? new CleanExportDataReader();
        _cleanRuntimeBootstrapBuilder = cleanRuntimeBootstrapBuilder ?? new CleanRuntimeBootstrapBuilder();
        _snapshotMapper = snapshotMapper ?? new CleanRuntimeBootstrapSnapshotMapper();
    }

    public CleanRuntimeBootstrap? TryLoadRuntimeBootstrap(string projectFilePath)
    {
        var cleanExport = _cleanExportDataReader.TryRead(projectFilePath);
        if (cleanExport is null)
        {
            return null;
        }

        return _cleanRuntimeBootstrapBuilder.TryBuild(cleanExport);
    }

    public RuntimeGameWorldSnapshot? TryLoadRuntimeSnapshot(CleanRuntimeBootstrap runtimeBootstrap)
    {
        return _snapshotMapper.TryMap(runtimeBootstrap);
    }
}
