namespace DocuLensLocal.Core;

public sealed class FolderIndexWatch : IDisposable
{
    private readonly DebouncedAction _debounced;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public FolderIndexWatch(TimeSpan debounce, Action onIdle)
    {
        ArgumentNullException.ThrowIfNull(onIdle);
        _debounced = new DebouncedAction(debounce, _ =>
        {
            onIdle();
            return Task.CompletedTask;
        });
    }

    public string? Folder { get; private set; }

    public void SetFolder(string? folder)
    {
        StopWatcher();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        Folder = folder;
        _watcher = new FileSystemWatcher(folder)
        {
            Filter = IndexWatchPolicy.FileWatcherFilter,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    public void Ping() => HandlePath(null);

    public void HandlePath(string? path)
    {
        if (IndexWatchPolicy.ShouldWatchPath(path))
        {
            _debounced.Ping();
        }
    }

    public void Stop() => StopWatcher();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatcher();
        _debounced.Dispose();
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            Folder = null;
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnChanged;
        _watcher.Changed -= OnChanged;
        _watcher.Deleted -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
        _watcher = null;
        Folder = null;
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => HandlePath(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        HandlePath(e.OldFullPath);
        HandlePath(e.FullPath);
    }
}
