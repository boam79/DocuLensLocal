namespace DocuLensLocal.Core;

public sealed class IndexingService
{
    private readonly IPdfContentExtractor _extractor;

    public IndexingService()
        : this(AppPaths.UserData)
    {
    }

    public IndexingService(string userDataDirectory)
        : this(userDataDirectory, new CompositeDocumentExtractor())
    {
    }

    public IndexingService(string userDataDirectory, IPdfContentExtractor extractor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDataDirectory);
        ArgumentNullException.ThrowIfNull(extractor);
        UserDataDirectory = userDataDirectory;
        IndexDatabasePath = Path.Combine(userDataDirectory, "index.db");
        _extractor = extractor;
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

        var files = await Task.Run(
            () => DiscoverIndexableFiles(folderPath).ToArray(),
            cancellationToken).ConfigureAwait(false);

        var errors = new List<IndexingError>();
        var documents = new List<IndexedDocument>();
        Report(progress, files.Length, processedCount: 0, currentFile: null, "문서를 찾는 중", errors, completed: false);

        using var store = new DocumentIndexStore(IndexDatabasePath);
        var previous = store.GetAll().ToDictionary(doc => doc.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, files.Length, documents.Count, file, "본문 추출·OCR", errors, completed: false);

            try
            {
                previous.TryGetValue(file, out var existing);
                var document = IndexFileReadOnly(file, existing, cancellationToken);
                store.Upsert(document);
                documents.Add(document);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                errors.Add(new IndexingError
                {
                    FilePath = file,
                    Message = ex.Message,
                });
            }

            Report(progress, files.Length, documents.Count, file, "본문 추출·OCR", errors, completed: false);
        }

        var result = new IndexingResult
        {
            FoundCount = files.Length,
            ProcessedCount = documents.Count,
            Errors = errors.ToArray(),
            Documents = documents.ToArray(),
            IsCompleted = true,
        };
        Report(progress, result.FoundCount, result.ProcessedCount, currentFile: null, "완료", errors, completed: true);
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

    public IReadOnlyList<IndexedDocument> SearchByFileName(string query) =>
        Search(query).Select(hit => hit.Document).ToList();

    public IReadOnlyList<SearchHit> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !File.Exists(IndexDatabasePath))
        {
            return [];
        }

        using var store = new DocumentIndexStore(IndexDatabasePath);
        return store.Search(query);
    }

    public IndexCoverage GetCoverage()
    {
        var documents = GetIndexedDocuments();
        return new IndexCoverage(
            documents.Count,
            documents.Count(doc => !string.IsNullOrWhiteSpace(doc.BodyText)),
            documents.Sum(doc => doc.OcrPageCount),
            CompositeOcrEngine.CreateDefault().IsAvailable);
    }

    private static IEnumerable<string> DiscoverIndexableFiles(string folderPath) =>
        Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(IndexableFiles.IsIndexable)
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private IndexedDocument IndexFileReadOnly(string path, IndexedDocument? existing, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Document was removed before it could be indexed.", path);
        }

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            // Read-only probe so the original file is never opened for write.
        }

        var info = new FileInfo(path);
        if (IndexFreshness.CanReuse(existing, info))
        {
            return existing!;
        }

        var extracted = _extractor.Extract(path, cancellationToken);
        var hasBody = !string.IsNullOrWhiteSpace(extracted.BodyText);
        var status = extracted.OcrPageCount > 0
            ? "ocr"
            : hasBody
                ? "indexed"
                : "filename_only";

        return new IndexedDocument
        {
            FilePath = info.FullName,
            SizeBytes = info.Length,
            LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            IndexedAtUtc = DateTimeOffset.UtcNow,
            BodyText = extracted.BodyText,
            PageCount = extracted.PageCount,
            OcrPageCount = extracted.OcrPageCount,
            Status = status,
        };
    }

    private void Report(
        IProgress<IndexingProgress>? progress,
        int foundCount,
        int processedCount,
        string? currentFile,
        string? phaseKo,
        IReadOnlyList<IndexingError> errors,
        bool completed)
    {
        var snapshot = new IndexingProgress
        {
            FoundCount = foundCount,
            ProcessedCount = processedCount,
            CurrentFile = currentFile,
            PhaseKo = phaseKo,
            Errors = errors.ToArray(),
            IsCompleted = completed,
        };
        progress?.Report(snapshot);
        ProgressChanged?.Invoke(this, snapshot);
    }
}
