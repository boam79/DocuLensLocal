using System.Collections.Concurrent;
using DocuLensLocal.Core;
using Microsoft.Data.Sqlite;

namespace DocuLensLocal.Core.Tests;

public class IndexingServiceTests : IDisposable
{
    private readonly string _userData;
    private readonly string _pdfRoot;

    public IndexingServiceTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocuLensLocalTests", Guid.NewGuid().ToString("N"));
        _userData = Path.Combine(root, "userdata");
        _pdfRoot = Path.Combine(root, "pdfs");
        Directory.CreateDirectory(_userData);
        Directory.CreateDirectory(_pdfRoot);
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_userData)?.FullName;
        if (root is null || !Directory.Exists(root))
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }
    }

    [Fact]
    public async Task finds_pdfs_recursively_ignoring_case_and_skips_other_files()
    {
        WriteStubPdf(_pdfRoot, "alpha.pdf");
        WriteStubPdf(Path.Combine(_pdfRoot, "nested"), "BETA.PDF");
        WriteStubPdf(Path.Combine(_pdfRoot, "nested", "deep"), "gamma.Pdf");
        File.WriteAllText(Path.Combine(_pdfRoot, "notes.txt"), "not a pdf");
        File.WriteAllText(Path.Combine(_pdfRoot, "image.png"), "nope");

        var service = new IndexingService(_userData);
        var result = await service.Start(_pdfRoot);

        Assert.Equal(3, result.FoundCount);
        Assert.Equal(3, result.ProcessedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Documents.Count);
        Assert.All(result.Documents, doc => Assert.EndsWith(".pdf", doc.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task does_not_modify_original_pdf_bytes_or_mtime()
    {
        var path = WriteStubPdf(_pdfRoot, "keep-me.pdf");
        var originalBytes = File.ReadAllBytes(path);
        var originalMtime = File.GetLastWriteTimeUtc(path);
        var originalSize = new FileInfo(path).Length;

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal(originalMtime, File.GetLastWriteTimeUtc(path));
        Assert.Equal(originalSize, new FileInfo(path).Length);
    }

    [Fact]
    public async Task stores_path_size_and_mtime_in_sqlite_under_userdata()
    {
        var path = WriteStubPdf(_pdfRoot, "stored.pdf");
        File.SetLastWriteTimeUtc(path, new DateTime(2024, 3, 15, 8, 30, 0, DateTimeKind.Utc));
        var size = new FileInfo(path).Length;

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        Assert.Equal(Path.Combine(_userData, "index.db"), service.IndexDatabasePath);
        Assert.True(File.Exists(service.IndexDatabasePath));
        Assert.DoesNotContain(
            $"{Path.DirectorySeparatorChar}current{Path.DirectorySeparatorChar}",
            service.IndexDatabasePath,
            StringComparison.OrdinalIgnoreCase);

        var stored = service.GetIndexedDocuments();
        var doc = Assert.Single(stored);
        Assert.Equal(Path.GetFullPath(path), doc.FilePath);
        Assert.Equal(size, doc.SizeBytes);
        Assert.Equal(new DateTimeOffset(2024, 3, 15, 8, 30, 0, TimeSpan.Zero), doc.LastWriteTimeUtc);
        Assert.Equal("filename_only", doc.Status);
        Assert.Equal(string.Empty, doc.BodyText);
    }

    [Fact]
    public async Task reports_progress_found_processed_current_file_and_errors()
    {
        WriteStubPdf(_pdfRoot, "one.pdf");
        var missingDuringProcess = WriteStubPdf(_pdfRoot, "two.pdf");
        WriteStubPdf(_pdfRoot, "three.pdf");

        var snapshots = new ConcurrentQueue<IndexingProgress>();
        var progress = new SynchronousProgress<IndexingProgress>(p =>
        {
            snapshots.Enqueue(Clone(p));
            if (p.FoundCount == 3 && p.ProcessedCount == 0 && File.Exists(missingDuringProcess))
            {
                File.Delete(missingDuringProcess);
            }
        });

        var service = new IndexingService(_userData);
        var result = await service.Start(_pdfRoot, progress);

        Assert.True(snapshots.Count >= 3);
        Assert.Contains(snapshots, p => p.FoundCount == 3 && p.ProcessedCount == 0);
        Assert.Contains(snapshots, p => !string.IsNullOrWhiteSpace(p.CurrentFile));
        Assert.Contains(snapshots, p => p.ProcessedCount > 0);
        Assert.True(result.IsCompleted);
        Assert.Equal(3, result.FoundCount);
        Assert.Equal(2, result.ProcessedCount);
        var error = Assert.Single(result.Errors);
        Assert.Contains("two.pdf", error.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Equal(2, service.GetIndexedDocuments().Count);
    }

    [Fact]
    public async Task cancellation_keeps_already_indexed_files()
    {
        for (var i = 0; i < 8; i++)
        {
            WriteStubPdf(_pdfRoot, $"file-{i}.pdf");
        }

        using var cts = new CancellationTokenSource();
        var progress = new SynchronousProgress<IndexingProgress>(p =>
        {
            if (p.ProcessedCount >= 1)
            {
                cts.Cancel();
            }
        });

        var service = new IndexingService(_userData);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.Start(_pdfRoot, progress, cts.Token));

        var stored = service.GetIndexedDocuments();
        Assert.InRange(stored.Count, 1, 7);
    }

    [Fact]
    public async Task start_after_cancel_skips_files_already_extracted_and_finishes_the_rest()
    {
        for (var i = 0; i < 8; i++)
        {
            WriteStubPdf(_pdfRoot, $"file-{i}.pdf");
        }

        var extractor = new CountingExtractor("본문 계약 조항");
        var service = new IndexingService(_userData, extractor);
        using var cts = new CancellationTokenSource();
        var progress = new SynchronousProgress<IndexingProgress>(p =>
        {
            if (p.ProcessedCount >= 2)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.Start(_pdfRoot, progress, cts.Token));

        var storedAfterCancel = service.GetIndexedDocuments().Count;
        Assert.InRange(storedAfterCancel, 2, 7);
        Assert.Equal(storedAfterCancel, extractor.Calls);

        var result = await service.Start(_pdfRoot);

        Assert.True(result.IsCompleted);
        Assert.Equal(8, result.FoundCount);
        Assert.Equal(8, service.GetIndexedDocuments().Count);
        Assert.Equal(8, extractor.Calls);
        Assert.All(service.GetIndexedDocuments(), doc => Assert.Equal("본문 계약 조항", doc.BodyText));
    }

    [Fact]
    public async Task empty_folder_completes_with_zero_documents()
    {
        var service = new IndexingService(_userData);
        var result = await service.Start(_pdfRoot);

        Assert.Equal(0, result.FoundCount);
        Assert.Equal(0, result.ProcessedCount);
        Assert.True(result.IsCompleted);
        Assert.Empty(service.GetIndexedDocuments());
    }

    [Fact]
    public async Task missing_folder_throws()
    {
        var service = new IndexingService(_userData);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => service.Start(Path.Combine(_pdfRoot, "does-not-exist")));
    }

    [Fact]
    public async Task files_are_findable_by_filename_after_stub_index()
    {
        WriteStubPdf(_pdfRoot, "NDA-A사.pdf");
        WriteStubPdf(Path.Combine(_pdfRoot, "mou"), "MOU-2024.pdf");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var hits = service.SearchByFileName("mou");
        var hit = Assert.Single(hits);
        Assert.Contains("MOU-2024.pdf", hit.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task natural_language_query_matches_concatenated_and_split_filename_keywords()
    {
        WriteStubPdf(_pdfRoot, "서울버스광고견적서.pdf");
        WriteStubPdf(_pdfRoot, "버스_광고_계약.pdf");
        WriteStubPdf(_pdfRoot, "NDA-A사.pdf");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var hits = service.SearchByFileName("버스 광고 찾아줘");
        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.FilePath.Contains("서울버스광고견적서.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.FilePath.Contains("버스_광고_계약.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hits, h => h.FilePath.Contains("NDA-A사.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hits, h => h.FilePath.Contains("버스 광고 찾아줘", StringComparison.Ordinal));
    }

    [Fact]
    public async Task natural_language_prefers_and_when_a_file_has_all_tokens()
    {
        WriteStubPdf(_pdfRoot, "버스만.pdf");
        WriteStubPdf(_pdfRoot, "광고만.pdf");
        WriteStubPdf(_pdfRoot, "버스와광고.pdf");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var hit = Assert.Single(service.SearchByFileName("버스 광고 찾아줘"));
        Assert.Contains("버스와광고.pdf", hit.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task natural_language_falls_back_to_or_when_and_is_empty()
    {
        WriteStubPdf(_pdfRoot, "버스일정.pdf");
        WriteStubPdf(_pdfRoot, "광고견적.pdf");
        WriteStubPdf(_pdfRoot, "NDA.pdf");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var hits = service.SearchByFileName("버스 광고 찾아줘");
        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.FilePath.Contains("버스일정.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.FilePath.Contains("광고견적.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hits, h => h.FilePath.Contains("NDA.pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task coverage_treats_stub_pdfs_as_filename_only_so_backfill_can_run()
    {
        WriteStubPdf(_pdfRoot, "empty-body.pdf");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var coverage = service.GetCoverage();
        Assert.Equal(1, coverage.DocumentCount);
        Assert.Equal(0, coverage.BodyCount);
        Assert.True(IndexBackfillPolicy.ShouldBackfill(coverage, _pdfRoot));
    }

    [Fact]
    public async Task skips_extractor_when_size_mtime_and_body_are_unchanged()
    {
        WriteStubPdf(_pdfRoot, "keep.pdf");
        var extractor = new CountingExtractor("본문 계약 조항");
        var service = new IndexingService(_userData, extractor);

        await service.Start(_pdfRoot);
        await service.Start(_pdfRoot);

        Assert.Equal(1, extractor.Calls);
        Assert.Equal("본문 계약 조항", Assert.Single(service.GetIndexedDocuments()).BodyText);
    }

    [Fact]
    public async Task clear_index_removes_search_rows_but_not_original_files()
    {
        var path = WriteStubPdf(_pdfRoot, "keep-me.pdf");
        var originalBytes = File.ReadAllBytes(path);
        var originalMtime = File.GetLastWriteTimeUtc(path);
        var extractor = new CountingExtractor("본문 계약");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);
        Assert.Single(service.Search("계약"));

        var removed = service.ClearIndex();

        Assert.Equal(1, removed);
        Assert.Empty(service.GetIndexedDocuments());
        Assert.Empty(service.Search("계약"));
        Assert.Equal(0, service.GetCoverage().DocumentCount);
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal(originalMtime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task rebuild_clears_then_reextracts_unchanged_files()
    {
        WriteStubPdf(_pdfRoot, "keep.pdf");
        var extractor = new CountingExtractor("본문 계약 조항");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);

        var result = await service.Rebuild(_pdfRoot);

        Assert.Equal(2, extractor.Calls);
        Assert.True(result.IsCompleted);
        Assert.Equal(1, result.FoundCount);
        Assert.Equal("본문 계약 조항", Assert.Single(service.GetIndexedDocuments()).BodyText);
        Assert.Single(service.Search("계약"));
    }

    [Fact]
    public async Task incremental_pass_indexes_only_newly_added_files()
    {
        WriteStubPdf(_pdfRoot, "already.pdf");
        var extractor = new CountingExtractor("본문 계약 조항");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);
        Assert.Equal(1, extractor.Calls);

        WriteStubPdf(_pdfRoot, "added.pdf");
        var plan = service.PlanSync(_pdfRoot);
        Assert.Equal(1, plan.NewCount);
        Assert.Equal(0, plan.ChangedCount);
        Assert.True(plan.NeedsWork);

        var result = await service.Start(_pdfRoot, progress: null, CancellationToken.None, IndexPass.NewAndChanged);

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.FoundCount);
        Assert.Equal(2, extractor.Calls);
        Assert.Equal(2, service.GetIndexedDocuments().Count);
        Assert.Contains(service.GetIndexedDocuments(), doc => doc.FilePath.Contains("added.pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task incremental_pass_skips_unchanged_files_even_without_body()
    {
        WriteStubPdf(_pdfRoot, "scan.pdf");
        var extractor = new CountingExtractor("");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);
        Assert.Equal(1, extractor.Calls);

        var plan = service.PlanSync(_pdfRoot);
        Assert.False(plan.NeedsWork);

        await service.Start(_pdfRoot, progress: null, CancellationToken.None, IndexPass.NewAndChanged);

        Assert.Equal(1, extractor.Calls);
        Assert.Single(service.GetIndexedDocuments());
    }

    [Fact]
    public async Task incremental_pass_rereads_empty_xlsx_body()
    {
        TestOfficeFactory.WriteXlsx(_pdfRoot, "견적.xlsx", "본 버스 광고 계약");
        var extractor = new QueueExtractor("", "본 버스 광고 계약");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);
        Assert.Equal("", Assert.Single(service.GetIndexedDocuments()).BodyText);
        Assert.Equal(1, extractor.Calls);

        var plan = service.PlanSync(_pdfRoot);
        Assert.Equal(1, plan.ChangedCount);
        Assert.True(plan.NeedsWork);

        await service.Start(_pdfRoot, progress: null, CancellationToken.None, IndexPass.NewAndChanged);

        Assert.Equal(2, extractor.Calls);
        var stored = Assert.Single(service.GetIndexedDocuments());
        Assert.Contains("버스 광고", stored.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task incremental_pass_reextracts_when_file_contents_change()
    {
        var path = WriteStubPdf(_pdfRoot, "changed.pdf");
        var extractor = new CountingExtractor("본문 계약 조항");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);
        Assert.Equal(1, extractor.Calls);

        File.WriteAllText(path, "%PDF-1.4 stub for indexing tests\n% bigger\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(2));

        var plan = service.PlanSync(_pdfRoot);
        Assert.Equal(1, plan.ChangedCount);
        Assert.True(plan.NeedsWork);

        await service.Start(_pdfRoot, progress: null, CancellationToken.None, IndexPass.NewAndChanged);

        Assert.Equal(2, extractor.Calls);
    }

    [Fact]
    public async Task start_drops_documents_that_are_no_longer_in_the_folder()
    {
        WriteStubPdf(_pdfRoot, "keep.pdf");
        var gone = WriteStubPdf(_pdfRoot, "gone.pdf");
        var service = new IndexingService(_userData, new CountingExtractor("본문"));
        await service.Start(_pdfRoot);
        Assert.Equal(2, service.GetIndexedDocuments().Count);

        File.Delete(gone);
        await service.Start(_pdfRoot);

        var remaining = Assert.Single(service.GetIndexedDocuments());
        Assert.Contains("keep.pdf", remaining.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void clear_index_is_safe_when_database_is_missing()
    {
        var service = new IndexingService(_userData);
        Assert.Equal(0, service.ClearIndex());
        Assert.Empty(service.GetIndexedDocuments());
    }

    [Fact]
    public async Task reextracts_when_previous_body_was_empty()
    {
        WriteStubPdf(_pdfRoot, "scan-later.pdf");
        await new IndexingService(_userData, new CountingExtractor("")).Start(_pdfRoot);

        var extractor = new CountingExtractor("OCR 이후에 생긴 본문");
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);

        Assert.Equal(1, extractor.Calls);
        Assert.Equal("OCR 이후에 생긴 본문", Assert.Single(service.GetIndexedDocuments()).BodyText);
    }

    [Fact]
    public async Task concatenated_bus_ad_query_matches_split_filenames()
    {
        WriteStubPdf(_pdfRoot, "버스일정.pdf");
        WriteStubPdf(_pdfRoot, "광고견적.pdf");
        WriteStubPdf(_pdfRoot, "NDA.pdf");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var hits = service.SearchByFileName("버스광고 찾아줘");
        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.FilePath.Contains("버스일정.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.FilePath.Contains("광고견적.pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void default_constructor_uses_apppaths_userdata()
    {
        var service = new IndexingService();
        Assert.Equal(AppPaths.IndexDatabase, service.IndexDatabasePath);
        Assert.Contains("userdata", service.IndexDatabasePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string WriteStubPdf(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "%PDF-1.4 stub for indexing tests\n");
        return path;
    }

    private sealed class CountingExtractor : IPdfContentExtractor
    {
        private readonly string _body;

        public CountingExtractor(string body) => _body = body;

        public int Calls { get; private set; }

        public PdfExtractedContent Extract(string pdfPath, CancellationToken cancellationToken)
        {
            Calls++;
            return new(_body, PageCount: 1, OcrPageCount: 0, [new PdfPageContent(1, _body, "")]);
        }
    }

    private sealed class QueueExtractor : IPdfContentExtractor
    {
        private readonly Queue<string> _bodies;

        public QueueExtractor(params string[] bodies) => _bodies = new Queue<string>(bodies);

        public int Calls { get; private set; }

        public PdfExtractedContent Extract(string pdfPath, CancellationToken cancellationToken)
        {
            Calls++;
            var body = _bodies.Count > 0 ? _bodies.Dequeue() : string.Empty;
            return new(body, PageCount: 1, OcrPageCount: 0, [new PdfPageContent(1, body, "")]);
        }
    }

    private static IndexingProgress Clone(IndexingProgress progress) => new()
    {
        FoundCount = progress.FoundCount,
        ProcessedCount = progress.ProcessedCount,
        CurrentFile = progress.CurrentFile,
        Errors = progress.Errors.ToArray(),
        IsCompleted = progress.IsCompleted,
    };

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }
}
