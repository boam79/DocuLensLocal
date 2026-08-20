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
            Assert.False(IndexFreshness.IsUnchanged(null, info));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
