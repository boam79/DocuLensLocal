namespace DocuLensLocal.Core;

public sealed class IndexingService
{
    public IndexingService()
        : this(AppPaths.UserData)
    {
    }

    public IndexingService(string userDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDataDirectory);
        UserDataDirectory = userDataDirectory;
        IndexDatabasePath = Path.Combine(userDataDirectory, "index.db");
    }

    public string UserDataDirectory { get; }

    public string IndexDatabasePath { get; }

    public event EventHandler<IndexingProgress>? ProgressChanged;

    public Task<IndexingResult> Start(string folderPath, CancellationToken cancellationToken = default) =>
        Start(folderPath, progress: null, cancellationToken);

    public async Task<IndexingResult> Start(
        string folderPath,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Indexing folder not found: {folderPath}");
        }

        Directory.CreateDirectory(UserDataDirectory);

        var pdfs = await Task.Run(
            () => DiscoverPdfs(folderPath).ToArray(),
            cancellationToken).ConfigureAwait(false);

        var errors = new List<IndexingError>();
        var documents = new List<IndexedDocument>();
        Report(progress, pdfs.Length, processedCount: 0, currentFile: null, errors, completed: false);

        using var store = new DocumentIndexStore(IndexDatabasePath);

        foreach (var pdf in pdfs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, pdfs.Length, documents.Count, pdf, errors, completed: false);

            try
            {
                var document = IndexPdfReadOnly(pdf);
                store.Upsert(document);
                documents.Add(document);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                errors.Add(new IndexingError
                {
                    FilePath = pdf,
                    Message = ex.Message,
                });
            }

            Report(progress, pdfs.Length, documents.Count, pdf, errors, completed: false);
        }

        var result = new IndexingResult
        {
            FoundCount = pdfs.Length,
            ProcessedCount = documents.Count,
            Errors = errors.ToArray(),
            Documents = documents.ToArray(),
            IsCompleted = true,
        };
        Report(progress, result.FoundCount, result.ProcessedCount, currentFile: null, errors, completed: true);
        return result;
    }

    public IReadOnlyList<IndexedDocument> GetIndexedDocuments()
    {
        if (!File.Exists(IndexDatabasePath))
        {
            return [];
        }

        using var store = new DocumentIndexStore(IndexDatabasePath);
        return store.GetAll();
    }

    public IReadOnlyList<IndexedDocument> SearchByFileName(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !File.Exists(IndexDatabasePath))
        {
            return [];
        }

        using var store = new DocumentIndexStore(IndexDatabasePath);
        return store.SearchByFileName(query);
    }

    private static IEnumerable<string> DiscoverPdfs(string folderPath) =>
        Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private static IndexedDocument IndexPdfReadOnly(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("PDF was removed before it could be indexed.", path);
        }

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            // Read-only probe so the original file is never opened for write.
        }

        var info = new FileInfo(path);

        // TODO: extract text with PDFium for digital PDFs; queue scan pages for OCR.
        return new IndexedDocument
        {
            FilePath = info.FullName,
            SizeBytes = info.Length,
            LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            IndexedAtUtc = DateTimeOffset.UtcNow,
            Status = "indexed",
        };
    }

    private void Report(
        IProgress<IndexingProgress>? progress,
        int foundCount,
        int processedCount,
        string? currentFile,
        IReadOnlyList<IndexingError> errors,
        bool completed)
    {
        var snapshot = new IndexingProgress
        {
            FoundCount = foundCount,
            ProcessedCount = processedCount,
            CurrentFile = currentFile,
            Errors = errors.ToArray(),
            IsCompleted = completed,
        };
        progress?.Report(snapshot);
        ProgressChanged?.Invoke(this, snapshot);
    }
}
