using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class ReleaseHistoryTests
{
    [Fact]
    public void known_releases_cover_0_1_0_through_0_1_5_newest_first()
    {
        var versions = ReleaseHistory.Known.Select(note => note.Version).ToArray();

        Assert.Equal(["0.1.5", "0.1.4", "0.1.3", "0.1.2", "0.1.1", "0.1.0"], versions);
    }

    [Fact]
    public void known_releases_keep_short_korean_notes()
    {
        Assert.Equal("자연어 파일명 검색", Note("0.1.5").SummaryKo);
        Assert.Equal("정보 탭·모던 UI", Note("0.1.4").SummaryKo);
        Assert.Equal("인덱싱 완료 후 검색 본화면", Note("0.1.3").SummaryKo);
        Assert.Equal("인덱싱 버튼", Note("0.1.2").SummaryKo);
        Assert.Equal("스플래시/폴더선택", Note("0.1.1").SummaryKo);
        Assert.Equal("splash 없음", Note("0.1.0").SummaryKo);
    }

    private static ReleaseNote Note(string version) =>
        ReleaseHistory.Known.Single(item => item.Version == version);
}
