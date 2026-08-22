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

    public Task<IndexingResult> Start(
        string folderPath,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken = default) =>
        Start(folderPath, progress, cancellationToken, IndexPass.FillMissingBody);

    public async Task<IndexingResult> Start(
        string folderPath,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken,
        IndexPass pass)
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
        var phaseKo = pass == IndexPass.NewAndChanged ? "새 파일만 읽는 중" : "본문 추출·OCR";
        Report(progress, files.Length, processedCount: 0, currentFile: null, "문서를 찾는 중", errors, completed: false);

        using var store = new DocumentIndexStore(IndexDatabasePath);
        var previous = store.GetAll().ToDictionary(doc => doc.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, files.Length, documents.Count, file, phaseKo, errors, completed: false);

            try
            {
                previous.TryGetValue(file, out var existing);
                var document = IndexFileReadOnly(file, existing, cancellationToken, pass);
                store.Upsert(document);
                documents.Add(document);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException)
            {
                errors.Add(new IndexingError
                {
                    FilePath = file,
                    Message = ex.Message,
                });
            }

            Report(progress, files.Length, documents.Count, file, phaseKo, errors, completed: false);
        }

        store.KeepOnly(files);

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

    public IndexSyncPlan PlanSync(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!Directory.Exists(folderPath))
        {
            return new IndexSyncPlan(0, 0, 0);
        }

        var files = DiscoverIndexableFiles(folderPath).ToArray();
        if (!File.Exists(IndexDatabasePath))
        {
            return new IndexSyncPlan(files.Length, 0, 0);
        }

        using var store = new DocumentIndexStore(IndexDatabasePath);
        var previous = store.GetAll().ToDictionary(doc => doc.FilePath, StringComparer.OrdinalIgnoreCase);
        var newCount = 0;
        var changedCount = 0;
        foreach (var file in files)
        {
            if (!previous.TryGetValue(file, out var existing))
            {
                newCount++;
                continue;
            }

            if (!IndexFreshness.IsUnchanged(existing, new FileInfo(file)))
            {
                changedCount++;
                continue;
            }

            if (IndexFreshness.NeedsBodyRetry(existing, file))
            {
                changedCount++;
            }
        }

        var fileSet = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        var removedCount = previous.Keys.Count(path => !fileSet.Contains(path));
        return new IndexSyncPlan(newCount, changedCount, removedCount);
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

    public IReadOnlyList<SearchHit> Search(string query) =>
        Search(query, SearchFormatFilter.All);

    public IReadOnlyList<SearchHit> Search(string query, SearchFormatFilter format)
    {
        if (string.IsNullOrWhiteSpace(query) || !File.Exists(IndexDatabasePath))
        {
            return [];
        }

        using var store = new DocumentIndexStore(IndexDatabasePath);
        var hits = store.Search(query);
        if (format == SearchFormatFilter.All)
        {
            return hits;
        }

        return hits.Where(hit => IndexableFiles.Matches(hit.Document.FilePath, format)).ToList();
    }

    public int ClearIndex()
    {
        if (!File.Exists(IndexDatabasePath))
        {
            return 0;
        }

        using var store = new DocumentIndexStore(IndexDatabasePath);
        return store.DeleteAll();
    }

    public Task<IndexingResult> Rebuild(string folderPath, CancellationToken cancellationToken = default) =>
        Rebuild(folderPath, progress: null, cancellationToken);

    public async Task<IndexingResult> Rebuild(
        string folderPath,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        Report(progress, foundCount: 0, processedCount: 0, currentFile: null, "검색 목록을 지우는 중", [], completed: false);
        ClearIndex();
        return await Start(folderPath, progress, cancellationToken).ConfigureAwait(false);
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

    private IndexedDocument IndexFileReadOnly(string path, IndexedDocument? existing, CancellationToken cancellationToken, IndexPass pass)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Document was removed before it could be indexed.", path);
        }

        using (OfficeFileAccess.OpenRead(path))
        {
            // Read-only probe so the original file is never opened for write.
        }

        var info = new FileInfo(path);
        var skipExtract = pass == IndexPass.NewAndChanged
            ? IndexFreshness.ShouldSkipOnIncremental(existing, info, IndexableFiles.KindOf(path))
            : IndexFreshness.CanReuse(existing, info);
        if (skipExtract)
        {
            return existing!;
        }

        var extracted = ExtractWithRetry(path, cancellationToken);
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

    private PdfExtractedContent ExtractWithRetry(string path, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return _extractor.Extract(path, cancellationToken);
            }
            catch (Exception ex) when (OfficeFileAccess.IsTransient(ex))
            {
                last = ex;
                if (attempt == 5)
                {
                    break;
                }

                Thread.Sleep(200);
            }
        }

        throw last ?? new IOException($"Could not read {path}");
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
