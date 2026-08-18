namespace DocuLensLocal.Core;

public sealed class IndexingProgress
{
    public int FoundCount { get; init; }
    public int ProcessedCount { get; init; }
    public string? CurrentFile { get; init; }
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

    /// <summary>Path/filename index is complete. PDFium text extract is still pending.</summary>
    public string Status { get; init; } = "indexed";
}
