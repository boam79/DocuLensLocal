using System.Text.Json;
using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class IndexFolderListTests
{
    [Fact]
    public void old_settings_json_with_one_folder_still_resolves()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"IndexFolder\":\"C:\\\\docs\"}");

        Assert.NotNull(settings);
        var folders = IndexFolderList.FromSettings(settings);
        Assert.Equal(@"C:\docs", Assert.Single(folders));
    }

    [Fact]
    public void apply_writes_first_folder_for_old_readers_and_the_full_list()
    {
        var settings = new AppSettings();
        IndexFolderList.Apply(settings, [@"C:\contracts", @"D:\scan"]);

        Assert.Equal(@"C:\contracts", settings.IndexFolder);
        Assert.Equal([@"C:\contracts", @"D:\scan"], settings.IndexFolders);
        Assert.Equal([@"C:\contracts", @"D:\scan"], IndexFolderList.FromSettings(settings));
    }

    [Fact]
    public void from_settings_merges_legacy_field_then_list_without_duplicates()
    {
        var settings = new AppSettings
        {
            IndexFolder = @"C:\docs",
            IndexFolders = [@"C:\docs", @"D:\other"],
        };

        Assert.Equal([@"C:\docs", @"D:\other"], IndexFolderList.FromSettings(settings));
    }

    [Fact]
    public void normalize_drops_blank_and_duplicate_paths()
    {
        var folders = IndexFolderList.Normalize("  ", @"C:\docs", @"c:\docs", null, @"D:\scan");

        Assert.Equal([@"C:\docs", @"D:\scan"], folders);
    }

    [Fact]
    public void nested_child_is_dropped_when_a_parent_is_already_listed()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocuLensFolders", Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "scan");
        Directory.CreateDirectory(child);

        try
        {
            var folders = IndexFolderList.Normalize(root, child);

            Assert.Equal(Path.GetFullPath(root), Assert.Single(folders));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void adding_a_parent_replaces_the_nested_child()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocuLensFolders", Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "scan");
        Directory.CreateDirectory(child);

        try
        {
            var folders = IndexFolderList.Add([child], root);

            Assert.Equal(Path.GetFullPath(root), Assert.Single(folders));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void remove_keeps_the_other_folders()
    {
        var remaining = IndexFolderList.Remove([@"C:\a", @"D:\b"], @"C:\a");

        Assert.Equal(@"D:\b", Assert.Single(remaining));
        Assert.Empty(IndexFolderList.Remove([@"C:\a"], @"C:\a"));
    }

    [Fact]
    public void header_line_mentions_extra_folders()
    {
        Assert.Equal(InfoStatusCopy.NoFolder, IndexFolderList.HeaderLine([]));
        Assert.Equal(@"C:\a", IndexFolderList.HeaderLine([@"C:\a"]));
        Assert.Equal(@"C:\a 외 2개", IndexFolderList.HeaderLine([@"C:\a", @"D:\b", @"E:\c"]));
    }

    [Fact]
    public void list_line_joins_every_folder()
    {
        Assert.Equal(InfoStatusCopy.NoFolder, IndexFolderList.ListLine([]));
        Assert.Equal($"C:\\a{Environment.NewLine}D:\\b", IndexFolderList.ListLine([@"C:\a", @"D:\b"]));
    }

    [Fact]
    public void any_exists_is_true_when_at_least_one_folder_is_on_disk()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensFolders", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            Assert.True(IndexFolderList.AnyExists([Path.Combine(folder, "missing"), folder]));
            Assert.False(IndexFolderList.AnyExists([Path.Combine(folder, "missing")]));
            Assert.Equal(Path.GetFullPath(folder), Assert.Single(IndexFolderList.Existing([folder, Path.Combine(folder, "missing")])));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
