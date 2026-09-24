using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class UpdateNotesPolicyTests
{
    [Fact]
    public void startup_notes_prefer_pending_text()
    {
        var notes = UpdateNotesPolicy.StartupNotes("0.1.28\n정보 탭 상태", "0.1.27", "0.1.28");

        Assert.Equal("0.1.28\n정보 탭 상태", notes);
    }

    [Fact]
    public void startup_notes_list_items_after_a_setup_exe_upgrade()
    {
        var notes = UpdateNotesPolicy.StartupNotes(null, "0.1.27", "0.1.28");

        Assert.Contains("0.1.28", notes, StringComparison.Ordinal);
        Assert.Contains("정보 탭 상태", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("0.1.27\n", notes, StringComparison.Ordinal);
    }

    [Fact]
    public void first_run_does_not_dump_the_whole_history()
    {
        Assert.Null(UpdateNotesPolicy.StartupNotes(null, null, "0.1.28"));
        Assert.Null(UpdateNotesPolicy.StartupNotes(null, "  ", "0.1.28"));
    }

    [Fact]
    public void same_version_does_not_show_notes()
    {
        Assert.Null(UpdateNotesPolicy.StartupNotes(null, "0.1.28", "0.1.28"));
    }

    [Fact]
    public void available_prompt_includes_the_update_items()
    {
        var text = UpdateNotesPolicy.AvailablePrompt("0.1.27", "0.1.28", indexingNow: false);

        Assert.Contains("설치할까요", text, StringComparison.Ordinal);
        Assert.Contains("0.1.28", text, StringComparison.Ordinal);
        Assert.Contains("정보 탭 상태", text, StringComparison.Ordinal);
        Assert.DoesNotContain("이어서", text, StringComparison.Ordinal);
    }
}
