using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class ReleaseHistoryTests
{
    [Fact]
    public void known_releases_cover_0_1_0_through_0_1_11_newest_first()
    {
        var versions = ReleaseHistory.Known.Select(note => note.Version).ToArray();

        Assert.Equal(["0.1.11", "0.1.10", "0.1.9", "0.1.8", "0.1.7", "0.1.6", "0.1.5", "0.1.4", "0.1.3", "0.1.2", "0.1.1", "0.1.0"], versions);
    }

    [Fact]
    public void known_releases_keep_short_korean_notes()
    {
        Assert.Equal("OCR 속도 개선", Note("0.1.11").SummaryKo);
        Assert.Equal("본문 자동 인덱스·내장 OCR", Note("0.1.10").SummaryKo);
        Assert.Equal("버스광고 붙여 검색", Note("0.1.9").SummaryKo);
        Assert.Equal("버튼·목록 글자 표시", Note("0.1.8").SummaryKo);
        Assert.Equal("본문 검색·OCR·근거 문장", Note("0.1.7").SummaryKo);
        Assert.Equal("Mac에서 개발·실행", Note("0.1.6").SummaryKo);
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
