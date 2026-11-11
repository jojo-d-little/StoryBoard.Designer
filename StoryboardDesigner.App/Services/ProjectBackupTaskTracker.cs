namespace StoryboardDesigner.App.Services;

internal static class ProjectBackupTaskTracker
{
    private static readonly object Sync = new();
    private static readonly List<Task> PendingTasks = new();

    public static Task<T> Run<T>(Func<T> action)
    {
        var task = Task.Run(action);
        lock (Sync)
        {
            PendingTasks.RemoveAll(static pending => pending.IsCompleted);
            PendingTasks.Add(task);
        }

        return task;
    }

    public static void WaitForPending(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            Task[] pending;
            lock (Sync)
            {
                PendingTasks.RemoveAll(static task => task.IsCompleted);
                pending = PendingTasks.ToArray();
            }

            if (pending.Length == 0)
            {
                return;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                Task.WaitAll(pending, remaining);
            }
            catch (AggregateException)
            {
                // Backup task continuations report their own failures.
            }
        }
    }
}
