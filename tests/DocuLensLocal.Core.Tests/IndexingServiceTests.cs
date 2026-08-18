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
        Assert.Equal("indexed", doc.Status);
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
