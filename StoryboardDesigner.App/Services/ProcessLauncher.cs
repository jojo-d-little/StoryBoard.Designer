using System.Diagnostics;

namespace StoryboardDesigner.App.Services;

internal sealed class ProcessLauncher : IProcessLauncher
{
    public IManagedProcess Start(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Process did not start: {startInfo.FileName}");
        return new ManagedProcess(process);
    }

    private sealed class ManagedProcess : IManagedProcess
    {
        private readonly Process _process;

        public ManagedProcess(Process process)
        {
            _process = process;
        }

        public int? ProcessId
        {
            get
            {
                try
                {
                    return _process.Id;
                }
                catch
                {
                    return null;
                }
            }
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch
                {
                    return true;
                }
            }
        }

        public void Kill()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between the state check and Kill.
            }
            finally
            {
                _process.Dispose();
            }
        }
    }
}
