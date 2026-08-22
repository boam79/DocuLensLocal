namespace DocuLensLocal.Core;

public sealed class IndexingProgress
{
    public int FoundCount { get; init; }
    public int ProcessedCount { get; init; }
    public string? CurrentFile { get; init; }
    public string? PhaseKo { get; init; }
    public IReadOnlyList<IndexingError> Errors { get; init; } = [];
    public bool IsCompleted { get; init; }
}

public sealed class IndexingError
{
    public required string FilePath { get; init; }
    public required string Message { get; init; }
}

public sealed class IndexingResult
{
    public int FoundCount { get; init; }
    public int ProcessedCount { get; init; }
    public IReadOnlyList<IndexingError> Errors { get; init; } = [];
    public IReadOnlyList<IndexedDocument> Documents { get; init; } = [];
    public bool IsCompleted { get; init; }
}

public sealed class IndexedDocument
{
    public required string FilePath { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
    public DateTimeOffset IndexedAtUtc { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public int OcrPageCount { get; init; }

    /// <summary>filename_only, indexed, or ocr — metadata is always stored.</summary>
    public string Status { get; init; } = "indexed";
}

public sealed record IndexCoverage(
    int DocumentCount,
    int BodyCount,
    int OcrPageCount,
    bool OcrEngineAvailable);

public enum IndexPass
{
    FillMissingBody,
    NewAndChanged,
}

public sealed record IndexSyncPlan(int NewCount, int ChangedCount, int RemovedCount)
{
    public int WorkCount => NewCount + ChangedCount + RemovedCount;

    public bool NeedsWork => WorkCount > 0;
}
