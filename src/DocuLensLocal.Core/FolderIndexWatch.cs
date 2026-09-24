namespace DocuLensLocal.Core;

public sealed class FolderIndexWatch : IDisposable
{
    private readonly DebouncedAction _debounced;
    private readonly List<FileSystemWatcher> _watchers = [];
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

    public string? Folder => Folders.Count > 0 ? Folders[0] : null;

    public IReadOnlyList<string> Folders { get; private set; } = [];

    public void SetFolder(string? folder) =>
        SetFolders(string.IsNullOrWhiteSpace(folder) ? [] : [folder]);

    public void SetFolders(IEnumerable<string?>? folders)
    {
        StopWatchers();
        var existing = IndexFolderList.Existing(folders);
        Folders = existing;
        foreach (var folder in existing)
        {
            _watchers.Add(CreateWatcher(folder));
        }
    }

    public void Ping() => HandlePath(null);

    public void HandlePath(string? path)
    {
        if (IndexWatchPolicy.ShouldWatchPath(path))
        {
            _debounced.Ping();
        }
    }

    public void Stop() => StopWatchers();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatchers();
        _debounced.Dispose();
    }

    private FileSystemWatcher CreateWatcher(string folder)
    {
        var watcher = new FileSystemWatcher(folder)
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
        watcher.Created += OnChanged;
        watcher.Changed += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnChanged;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnChanged;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }

        _watchers.Clear();
        Folders = [];
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => HandlePath(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        HandlePath(e.OldFullPath);
        HandlePath(e.FullPath);
    }
}
