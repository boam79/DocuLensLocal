using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class IndexWatchPolicyTests
{
    [Fact]
    public void watches_pdf_word_hangul_and_folders_but_not_notes()
    {
        Assert.True(IndexWatchPolicy.ShouldWatchPath("/docs/계약.pdf"));
        Assert.True(IndexWatchPolicy.ShouldWatchPath("/docs/내부문서.docx"));
        Assert.True(IndexWatchPolicy.ShouldWatchPath("/docs/스캔.hwp"));
        Assert.True(IndexWatchPolicy.ShouldWatchPath("/docs/견적.xlsx"));
        Assert.True(IndexWatchPolicy.ShouldWatchPath("/docs/legacy.xls"));
        Assert.True(IndexWatchPolicy.ShouldWatchPath(null));
        Assert.False(IndexWatchPolicy.ShouldWatchPath("/docs/메모.txt"));
        Assert.False(IndexWatchPolicy.ShouldWatchPath("/docs/~$잠금.docx"));
        Assert.False(IndexWatchPolicy.ShouldWatchPath("/docs/~$잠금.xlsx"));
    }

    [Fact]
    public void watches_folder_only_after_indexing_completed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensWatch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            Assert.True(IndexWatchPolicy.ShouldWatchFolder(new AppSettings
            {
                IndexFolder = folder,
                IndexCompleted = true,
            }));
            Assert.False(IndexWatchPolicy.ShouldWatchFolder(new AppSettings
            {
                IndexFolder = folder,
                IndexCompleted = false,
            }));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

public class DebouncedActionTests
{
    [Fact]
    public async Task coalesces_rapid_pings_into_one_run()
    {
        var runs = 0;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var debounce = new DebouncedAction(TimeSpan.FromMilliseconds(80), _ =>
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                done.TrySetResult();
            }

            return Task.CompletedTask;
        });

        debounce.Ping();
        debounce.Ping();
        debounce.Ping();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task ignored_paths_do_not_start_a_watch_sync()
    {
        var pings = 0;
        using var watch = new FolderIndexWatch(TimeSpan.FromMilliseconds(40), () => Interlocked.Increment(ref pings));

        watch.HandlePath("/docs/메모.txt");
        watch.HandlePath("/docs/~$잠금.docx");
        await Task.Delay(120);

        Assert.Equal(0, pings);

        watch.HandlePath("/docs/추가.pdf");
        await Task.Delay(120);

        Assert.Equal(1, pings);
    }
}
