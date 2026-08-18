using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class AppVersionFormatterTests
{
    [Fact]
    public void informational_version_wins_and_strips_git_metadata()
    {
        var display = AppVersionFormatter.DisplayVersion("0.1.3+abc1234", new Version(0, 1, 3, 0));

        Assert.Equal("0.1.3", display);
    }

    [Fact]
    public void plain_informational_version_is_kept()
    {
        Assert.Equal("0.1.3", AppVersionFormatter.DisplayVersion("0.1.3", null));
    }

    [Fact]
    public void assembly_version_is_used_when_informational_is_missing()
    {
        var display = AppVersionFormatter.DisplayVersion(null, new Version(0, 1, 3, 0));

        Assert.Equal("0.1.3", display);
    }
}
