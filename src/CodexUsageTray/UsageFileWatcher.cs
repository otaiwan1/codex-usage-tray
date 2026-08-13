namespace CodexUsageTray;

public sealed class UsageFileWatcher : IDisposable
{
    private readonly FileSystemWatcher? watcher;
    private readonly System.Threading.Timer debounceTimer;
    private readonly object gate = new();
    private readonly HashSet<string> pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public UsageFileWatcher(string sessionsDirectory)
    {
        debounceTimer = new System.Threading.Timer(OnDebounceElapsed);

        if (!Directory.Exists(sessionsDirectory))
        {
            return;
        }

        watcher = new FileSystemWatcher(sessionsDirectory, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;
    }

    public event EventHandler<string?>? UsageFileChanged;

    private void OnChanged(object sender, FileSystemEventArgs eventArgs) => Queue(eventArgs.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs eventArgs) => Queue(eventArgs.FullPath);

    private void Queue(string path)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            pendingPaths.Add(path);
            debounceTimer.Change(TimeSpan.FromMilliseconds(750), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        string[] paths;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            paths = pendingPaths.ToArray();
            pendingPaths.Clear();
        }

        foreach (var path in paths)
        {
            UsageFileChanged?.Invoke(this, path);
        }
    }

    private void OnError(object sender, ErrorEventArgs eventArgs) => UsageFileChanged?.Invoke(this, null);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        watcher?.Dispose();
        debounceTimer.Dispose();
    }
}
