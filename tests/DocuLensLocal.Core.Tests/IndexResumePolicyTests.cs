using System.Text.Json;
using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class IndexResumePolicyTests
{
    [Fact]
    public void resumes_when_indexing_was_in_progress_and_folder_still_exists()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensResume", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var settings = new AppSettings
            {
                IndexFolder = folder,
                IndexingInProgress = true,
                IndexCompleted = false,
            };

            Assert.True(IndexResumePolicy.ShouldResume(settings));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void does_not_resume_when_indexing_finished_or_folder_is_gone()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensResume", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            Assert.False(IndexResumePolicy.ShouldResume(new AppSettings
            {
                IndexFolder = folder,
                IndexingInProgress = false,
            }));
            Assert.False(IndexResumePolicy.ShouldResume(new AppSettings
            {
                IndexFolder = Path.Combine(folder, "missing"),
                IndexingInProgress = true,
            }));
            Assert.False(IndexResumePolicy.ShouldResume(new AppSettings
            {
                IndexFolder = null,
                IndexingInProgress = true,
            }));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void resumes_even_when_some_body_text_is_already_indexed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensResume", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var settings = new AppSettings
            {
                IndexFolder = folder,
                IndexingInProgress = true,
            };
            var coverage = new IndexCoverage(276, 40, 12, true);

            Assert.True(IndexResumePolicy.ShouldResume(settings));
            Assert.False(IndexBackfillPolicy.ShouldBackfill(coverage, folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void old_settings_json_without_in_progress_flag_defaults_false()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"IndexFolder\":\"C:\\\\docs\"}");

        Assert.NotNull(settings);
        Assert.False(settings.IndexingInProgress);
    }

    [Fact]
    public void start_then_finish_clears_in_progress_and_marks_completed()
    {
        var settings = new AppSettings { IndexingInProgress = false, IndexCompleted = false };

        IndexingRunState.OnStarted(settings);
        Assert.True(settings.IndexingInProgress);

        IndexingRunState.OnFinished(settings, completed: true);
        Assert.False(settings.IndexingInProgress);
        Assert.True(settings.IndexCompleted);
    }
}
