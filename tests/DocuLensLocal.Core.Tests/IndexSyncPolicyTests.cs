using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class IndexSyncPolicyTests
{
    [Fact]
    public void auto_syncs_when_index_is_complete_and_new_files_exist()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensSync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var settings = new AppSettings
            {
                IndexFolder = folder,
                IndexCompleted = true,
                IndexingInProgress = false,
            };
            var plan = new IndexSyncPlan(NewCount: 2, ChangedCount: 0, RemovedCount: 0);

            Assert.True(plan.NeedsWork);
            Assert.True(IndexSyncPolicy.ShouldAutoSync(settings, plan));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void does_not_auto_sync_when_nothing_changed_or_first_run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensSync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var idle = new IndexSyncPlan(0, 0, 0);
            Assert.False(idle.NeedsWork);
            Assert.False(IndexSyncPolicy.ShouldAutoSync(new AppSettings
            {
                IndexFolder = folder,
                IndexCompleted = true,
            }, idle));
            Assert.False(IndexSyncPolicy.ShouldAutoSync(new AppSettings
            {
                IndexFolder = folder,
                IndexCompleted = false,
            }, new IndexSyncPlan(3, 0, 0)));
            Assert.False(IndexSyncPolicy.ShouldAutoSync(new AppSettings
            {
                IndexFolder = folder,
                IndexCompleted = true,
                IndexingInProgress = true,
            }, new IndexSyncPlan(1, 0, 0)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void freshness_treats_empty_body_as_unchanged_when_size_and_mtime_match()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensSync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "scan.pdf");
        File.WriteAllText(path, "%PDF-1.4 stub\n");
        var info = new FileInfo(path);
        var existing = new IndexedDocument
        {
            FilePath = path,
            SizeBytes = info.Length,
            LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            IndexedAtUtc = DateTimeOffset.UtcNow,
            BodyText = "",
            Status = "filename_only",
        };

        try
        {
            Assert.True(IndexFreshness.IsUnchanged(existing, info));
            Assert.False(IndexFreshness.CanReuse(existing, info));
            Assert.True(IndexFreshness.ShouldSkipOnIncremental(existing, info, IndexableFileKind.Pdf));
            Assert.False(IndexFreshness.NeedsBodyRetry(existing, path));
            Assert.False(IndexFreshness.IsUnchanged(null, info));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void empty_xlsx_and_hwp_bodies_are_retried_on_incremental_sync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensSync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var xlsx = Path.Combine(dir, "견적.xlsx");
        var hwp = Path.Combine(dir, "공문.hwp");
        File.WriteAllText(xlsx, "stub-xlsx");
        File.WriteAllText(hwp, "stub-hwp");
        var xlsxInfo = new FileInfo(xlsx);
        var hwpInfo = new FileInfo(hwp);
        var emptyXlsx = new IndexedDocument
        {
            FilePath = xlsx,
            SizeBytes = xlsxInfo.Length,
            LastWriteTimeUtc = new DateTimeOffset(xlsxInfo.LastWriteTimeUtc, TimeSpan.Zero),
            IndexedAtUtc = DateTimeOffset.UtcNow,
            BodyText = "",
            Status = "filename_only",
        };
        var emptyHwp = new IndexedDocument
        {
            FilePath = hwp,
            SizeBytes = hwpInfo.Length,
            LastWriteTimeUtc = new DateTimeOffset(hwpInfo.LastWriteTimeUtc, TimeSpan.Zero),
            IndexedAtUtc = DateTimeOffset.UtcNow,
            BodyText = "",
            Status = "filename_only",
        };

        try
        {
            Assert.False(IndexFreshness.ShouldSkipOnIncremental(emptyXlsx, xlsxInfo, IndexableFileKind.Xlsx));
            Assert.True(IndexFreshness.NeedsBodyRetry(emptyXlsx, xlsx));
            Assert.False(IndexFreshness.ShouldSkipOnIncremental(emptyHwp, hwpInfo, IndexableFileKind.Hwp));
            Assert.True(IndexFreshness.NeedsBodyRetry(emptyHwp, hwp));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
