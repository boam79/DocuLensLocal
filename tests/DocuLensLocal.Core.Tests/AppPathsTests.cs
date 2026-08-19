using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void settings_file_lives_outside_current_install_folder()
    {
        Assert.Contains("userdata", AppPaths.SettingsFile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"{Path.DirectorySeparatorChar}current{Path.DirectorySeparatorChar}",
            AppPaths.SettingsFile,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void index_database_lives_in_userdata_not_current()
    {
        Assert.Equal(Path.Combine(AppPaths.UserData, "index.db"), AppPaths.IndexDatabase);
        Assert.Contains("userdata", AppPaths.IndexDatabase, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"{Path.DirectorySeparatorChar}current{Path.DirectorySeparatorChar}",
            AppPaths.IndexDatabase,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void userdata_follows_os_local_application_data()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DocuLensLocal",
            "userdata");

        Assert.Equal(expected, AppPaths.UserData);
        Assert.False(
            AppPaths.UserData.Contains(
                $"{Path.DirectorySeparatorChar}current{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase));
    }
}
