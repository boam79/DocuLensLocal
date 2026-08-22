using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class InfoStatusCopyTests
{
    [Fact]
    public void headline_and_update_caption_are_plain_korean()
    {
        Assert.Equal("이 컴퓨터에서만 찾습니다", InfoStatusCopy.Headline);
        Assert.Equal("새 버전이 있으면 여기서 받습니다.", InfoStatusCopy.UpdateCaption);
        Assert.DoesNotContain("GitHub", InfoStatusCopy.UpdateCaption, StringComparison.Ordinal);
        Assert.Equal("이전 버전", InfoStatusCopy.OlderHistoryHeader);
    }

    [Fact]
    public void tips_cover_filters_open_and_wrong_folder()
    {
        Assert.Equal(3, InfoStatusCopy.Tips.Count);
        Assert.Contains("여러 개", InfoStatusCopy.Tips[0], StringComparison.Ordinal);
        Assert.Contains("열기", InfoStatusCopy.Tips[1], StringComparison.Ordinal);
        Assert.Contains("폴더", InfoStatusCopy.Tips[2], StringComparison.Ordinal);
    }

    [Fact]
    public void folder_line_falls_back_when_empty()
    {
        Assert.Equal("아직 폴더를 고르지 않았습니다.", InfoStatusCopy.FolderLine(null));
        Assert.Equal("아직 폴더를 고르지 않았습니다.", InfoStatusCopy.FolderLine("  "));
        Assert.Equal(@"C:\docs", InfoStatusCopy.FolderLine(@"C:\docs"));
    }

    [Fact]
    public void status_labels_use_counts_when_documents_exist()
    {
        var coverage = new IndexCoverage(278, 200, 707, OcrEngineAvailable: true);

        Assert.Equal("278개", InfoStatusCopy.DocumentCount(coverage));
        Assert.Equal("본문 200건", InfoStatusCopy.BodyLabel(coverage));
        Assert.Equal("OCR 707쪽", InfoStatusCopy.OcrLabel(coverage));
    }

    [Fact]
    public void status_labels_stay_honest_when_empty()
    {
        var coverage = new IndexCoverage(0, 0, 0, OcrEngineAvailable: false);

        Assert.Equal("문서 없음", InfoStatusCopy.DocumentCount(coverage));
        Assert.Equal("본문 없음", InfoStatusCopy.BodyLabel(coverage));
        Assert.Equal("OCR 엔진 없음", InfoStatusCopy.OcrLabel(coverage));
    }

    [Fact]
    public void ocr_label_says_none_when_engine_is_ready_but_no_pages()
    {
        Assert.Equal("OCR 없음", InfoStatusCopy.OcrLabel(new IndexCoverage(10, 10, 0, true)));
    }
}
