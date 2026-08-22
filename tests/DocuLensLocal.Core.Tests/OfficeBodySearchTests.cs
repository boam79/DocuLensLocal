using DocuLensLocal.Core;
using Microsoft.Data.Sqlite;

namespace DocuLensLocal.Core.Tests;

public class IndexableFilesTests
{
    [Theory]
    [InlineData("계약.pdf", IndexableFileKind.Pdf, "PDF")]
    [InlineData("견적.DOCX", IndexableFileKind.Docx, "DOCX")]
    [InlineData("old.doc", IndexableFileKind.Doc, "DOC")]
    [InlineData("공문.hwpx", IndexableFileKind.Hwpx, "HWPX")]
    [InlineData("한글.hwp", IndexableFileKind.Hwp, "HWP")]
    [InlineData("견적.xlsx", IndexableFileKind.Xlsx, "XLSX")]
    [InlineData("매크로.XLSM", IndexableFileKind.Xlsm, "XLSM")]
    [InlineData("old.xls", IndexableFileKind.Xls, "XLS")]
    [InlineData("notes.txt", IndexableFileKind.Unknown, "파일")]
    [InlineData("~$lock.docx", IndexableFileKind.Unknown, "파일")]
    [InlineData("~$lock.xlsx", IndexableFileKind.Unknown, "파일")]
    [InlineData("~lock.hwp", IndexableFileKind.Unknown, "파일")]
    public void classifies_supported_extensions_and_skips_lock_files(string path, IndexableFileKind kind, string badge)
    {
        Assert.Equal(kind, IndexableFiles.KindOf(path));
        Assert.Equal(badge, IndexableFiles.Badge(path));
        Assert.Equal(kind != IndexableFileKind.Unknown, IndexableFiles.IsIndexable(path));
    }

    [Theory]
    [InlineData("/docs/a.pdf", SearchFormatFilter.All, true)]
    [InlineData("/docs/a.pdf", SearchFormatFilter.Pdf, true)]
    [InlineData("/docs/a.pdf", SearchFormatFilter.Word, false)]
    [InlineData("/docs/memo.docx", SearchFormatFilter.Word, true)]
    [InlineData("/docs/old.doc", SearchFormatFilter.Word, true)]
    [InlineData("/docs/memo.docx", SearchFormatFilter.Pdf, false)]
    [InlineData("/docs/공문.hwp", SearchFormatFilter.Hangul, true)]
    [InlineData("/docs/공문.hwpx", SearchFormatFilter.Hangul, true)]
    [InlineData("/docs/공문.hwp", SearchFormatFilter.Excel, false)]
    [InlineData("/docs/견적.xlsx", SearchFormatFilter.Excel, true)]
    [InlineData("/docs/매크로.xlsm", SearchFormatFilter.Excel, true)]
    [InlineData("/docs/old.xls", SearchFormatFilter.Excel, true)]
    [InlineData("/docs/견적.xlsx", SearchFormatFilter.Hangul, false)]
    [InlineData("/docs/a.pdf", SearchFormatFilter.Pdf | SearchFormatFilter.Excel, true)]
    [InlineData("/docs/견적.xlsx", SearchFormatFilter.Pdf | SearchFormatFilter.Excel, true)]
    [InlineData("/docs/memo.docx", SearchFormatFilter.Pdf | SearchFormatFilter.Excel, false)]
    public void matches_search_format_groups(string path, SearchFormatFilter filter, bool expected)
    {
        Assert.Equal(expected, IndexableFiles.Matches(path, filter));
    }
}

public class OfficeBodySearchTests : IDisposable
{
    private readonly string _userData;
    private readonly string _docsRoot;

    public OfficeBodySearchTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocuLensOfficeTests", Guid.NewGuid().ToString("N"));
        _userData = Path.Combine(root, "userdata");
        _docsRoot = Path.Combine(root, "docs");
        Directory.CreateDirectory(_userData);
        Directory.CreateDirectory(_docsRoot);
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_userData)?.FullName;
        if (root is null || !Directory.Exists(root))
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task discovers_word_and_hwp_files_and_skips_txt_png_and_word_locks()
    {
        TestPdfFactory.WriteDigitalPdf(_docsRoot, "keep.pdf", "pdf body");
        TestOfficeFactory.WriteDocx(_docsRoot, "memo.docx", "docx body");
        TestOfficeFactory.WriteLegacyDoc(_docsRoot, "legacy.doc", "doc body");
        TestOfficeFactory.WriteHwpx(_docsRoot, "notice.hwpx", "hwpx body");
        TestOfficeFactory.WriteHwp(_docsRoot, "hangul.hwp", "hwp body");
        TestOfficeFactory.WriteXlsx(_docsRoot, "quote.xlsx", "xlsx body");
        TestOfficeFactory.WriteLegacyXls(_docsRoot, "legacy.xls", "xls body");
        File.WriteAllText(Path.Combine(_docsRoot, "notes.txt"), "not a document");
        File.WriteAllText(Path.Combine(_docsRoot, "image.png"), "nope");
        TestOfficeFactory.WriteDocx(_docsRoot, "~$memo.docx", "should skip");

        var service = new IndexingService(_userData);
        var result = await service.Start(_docsRoot);

        Assert.Equal(7, result.FoundCount);
        Assert.Equal(7, result.ProcessedCount);
        Assert.Empty(result.Errors);
        Assert.DoesNotContain(result.Documents, doc => doc.FilePath.Contains("notes.txt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Documents, doc => Path.GetFileName(doc.FilePath).StartsWith("~$", StringComparison.Ordinal));
    }

    [Fact]
    public async Task docx_body_is_searchable_even_when_filename_does_not_match()
    {
        TestOfficeFactory.WriteDocx(_docsRoot, "내부문서.docx", "본 버스 광고 계약 조항은 을의 의무를 정한다.");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        var hit = Assert.Single(service.Search("버스 광고"));
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Contains("버스 광고 계약", hit.Snippet, StringComparison.Ordinal);
        Assert.Contains("내부문서.docx", hit.Document.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("indexed", hit.Document.Status);
        Assert.Equal("DOCX", IndexableFiles.Badge(hit.Document.FilePath));
    }

    [Fact]
    public async Task hwpx_body_is_searchable()
    {
        TestOfficeFactory.WriteHwpx(_docsRoot, "공문.hwpx", "부대 시설 사용 계약");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        var hit = Assert.Single(service.Search("부대"));
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Contains("부대 시설", hit.Snippet, StringComparison.Ordinal);
        Assert.Equal("HWPX", IndexableFiles.Badge(hit.Document.FilePath));
    }

    [Fact]
    public async Task hwp_body_is_searchable()
    {
        TestOfficeFactory.WriteHwp(_docsRoot, "내부한글.hwp", "본 버스 광고 계약 조항은 을의 의무를 정한다.");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        var hit = Assert.Single(service.Search("버스 광고"));
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Contains("버스 광고", hit.Snippet, StringComparison.Ordinal);
        Assert.Equal("HWP", IndexableFiles.Badge(hit.Document.FilePath));
        Assert.Equal("indexed", hit.Document.Status);
    }

    [Fact]
    public async Task legacy_doc_body_is_searchable()
    {
        TestOfficeFactory.WriteLegacyDoc(_docsRoot, "old-memo.doc", "본 버스 광고 계약 조항은 을의 의무를 정한다.");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        var hit = Assert.Single(service.Search("버스 광고"));
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Contains("버스 광고", hit.Snippet, StringComparison.Ordinal);
        Assert.Equal("DOC", IndexableFiles.Badge(hit.Document.FilePath));
    }

    [Fact]
    public async Task xlsx_body_is_searchable_even_when_filename_does_not_match()
    {
        TestOfficeFactory.WriteXlsx(_docsRoot, "내부표.xlsx", "본 버스 광고 계약 조항은 을의 의무를 정한다.");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        var hit = Assert.Single(service.Search("버스 광고"));
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Contains("버스 광고 계약", hit.Snippet, StringComparison.Ordinal);
        Assert.Contains("내부표.xlsx", hit.Document.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("indexed", hit.Document.Status);
        Assert.Equal("XLSX", IndexableFiles.Badge(hit.Document.FilePath));
    }

    [Fact]
    public async Task xlsm_and_legacy_xls_bodies_are_searchable()
    {
        TestOfficeFactory.WriteXlsx(_docsRoot, "견적.xlsm", "본 버스 광고 계약 조항은 을의 의무를 정한다.");
        TestOfficeFactory.WriteLegacyXls(_docsRoot, "견적.xls", "부대 시설 사용 계약");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        var xlsm = Assert.Single(service.Search("버스 광고"));
        Assert.Equal(SearchMatchKind.Body, xlsm.MatchKind);
        Assert.Equal("XLSM", IndexableFiles.Badge(xlsm.Document.FilePath));

        var xls = Assert.Single(service.Search("부대"));
        Assert.Equal(SearchMatchKind.Body, xls.MatchKind);
        Assert.Contains("부대 시설", xls.Snippet, StringComparison.Ordinal);
        Assert.Equal("XLS", IndexableFiles.Badge(xls.Document.FilePath));
    }

    [Fact]
    public async Task search_can_limit_hits_to_the_selected_format()
    {
        TestPdfFactory.WriteDigitalPdf(_docsRoot, "busad.pdf", "busad body");
        TestOfficeFactory.WriteDocx(_docsRoot, "busad.docx", "busad body");
        TestOfficeFactory.WriteLegacyDoc(_docsRoot, "busad.doc", "busad body");
        TestOfficeFactory.WriteHwp(_docsRoot, "busad.hwp", "busad body");
        TestOfficeFactory.WriteHwpx(_docsRoot, "busad.hwpx", "busad body");
        TestOfficeFactory.WriteXlsx(_docsRoot, "busad.xlsx", "busad body");
        TestOfficeFactory.WriteLegacyXls(_docsRoot, "busad.xls", "busad body");

        var service = new IndexingService(_userData);
        await service.Start(_docsRoot);

        Assert.Equal(7, service.Search("busad").Count);
        Assert.Equal(7, service.Search("busad", SearchFormatFilter.All).Count);

        var pdf = Assert.Single(service.Search("busad", SearchFormatFilter.Pdf));
        Assert.EndsWith(".pdf", pdf.Document.FilePath, StringComparison.OrdinalIgnoreCase);

        var word = service.Search("busad", SearchFormatFilter.Word);
        Assert.Equal(2, word.Count);
        Assert.All(word, hit => Assert.True(
            hit.Document.FilePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            || hit.Document.FilePath.EndsWith(".doc", StringComparison.OrdinalIgnoreCase)));

        var hangul = service.Search("busad", SearchFormatFilter.Hangul);
        Assert.Equal(2, hangul.Count);
        Assert.All(hangul, hit => Assert.True(
            hit.Document.FilePath.EndsWith(".hwp", StringComparison.OrdinalIgnoreCase)
            || hit.Document.FilePath.EndsWith(".hwpx", StringComparison.OrdinalIgnoreCase)));

        var excel = service.Search("busad", SearchFormatFilter.Excel);
        Assert.Equal(2, excel.Count);
        Assert.All(excel, hit => Assert.True(
            hit.Document.FilePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            || hit.Document.FilePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(excel, hit => hit.Document.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

        var pdfAndExcel = service.Search("busad", SearchFormatFilter.Pdf | SearchFormatFilter.Excel);
        Assert.Equal(3, pdfAndExcel.Count);
        Assert.All(pdfAndExcel, hit => Assert.True(
            hit.Document.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || hit.Document.FilePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            || hit.Document.FilePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(pdfAndExcel, hit => hit.Document.FilePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(pdfAndExcel, hit => hit.Document.FilePath.EndsWith(".hwp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void xlsx_rich_shared_string_runs_are_joined_for_search()
    {
        var path = TestOfficeFactory.WriteXlsxRichRuns(_docsRoot, "리치.xlsx", "버스 ", "광고 계약");

        var text = ExcelZipTextExtractor.Extract(path);

        Assert.Contains("버스 광고 계약", text, StringComparison.Ordinal);
    }

    [Fact]
    public void xlsx_text_is_read_while_another_handle_holds_write_share()
    {
        var path = TestOfficeFactory.WriteXlsx(_docsRoot, "견적.xlsx", "본 버스 광고 계약 조항은 을의 의무를 정한다.");
        using var excelLike = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        var text = ExcelZipTextExtractor.Extract(path);

        Assert.Contains("버스 광고 계약", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task incomplete_xlsx_is_not_stuck_and_is_indexed_when_the_real_file_arrives()
    {
        var path = Path.Combine(_docsRoot, "견적.xlsx");
        File.WriteAllBytes(path, "not-a-zip"u8.ToArray());

        var service = new IndexingService(_userData);
        var first = await service.Start(_docsRoot, progress: null, CancellationToken.None, IndexPass.NewAndChanged);

        Assert.Empty(service.GetIndexedDocuments());
        Assert.NotEmpty(first.Errors);
        Assert.True(service.PlanSync(_docsRoot).NeedsWork);

        File.Delete(path);
        TestOfficeFactory.WriteXlsx(_docsRoot, "견적.xlsx", "본 버스 광고 계약 조항은 을의 의무를 정한다.");
        await service.Start(_docsRoot, progress: null, CancellationToken.None, IndexPass.NewAndChanged);

        var hit = Assert.Single(service.Search("버스 광고"));
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Equal("XLSX", IndexableFiles.Badge(hit.Document.FilePath));
    }

    [Fact]
    public void hwp_and_hwpx_text_is_read_while_another_handle_holds_write_share()
    {
        var hwp = TestOfficeFactory.WriteHwp(_docsRoot, "공문.hwp", "부대 시설 사용 계약");
        var hwpx = TestOfficeFactory.WriteHwpx(_docsRoot, "공문.hwpx", "버스 광고 계약");
        using var hwpHandle = new FileStream(hwp, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        using var hwpxHandle = new FileStream(hwpx, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        Assert.Contains("부대 시설", HwpBinaryTextExtractor.Extract(hwp), StringComparison.Ordinal);
        Assert.Contains("버스 광고 계약", ZipOfficeTextExtractor.Extract(hwpx), StringComparison.Ordinal);
    }

    [Fact]
    public async Task does_not_modify_original_office_bytes_or_mtime()
    {
        var docx = TestOfficeFactory.WriteDocx(_docsRoot, "keep.docx", "keep body");
        var hwp = TestOfficeFactory.WriteHwp(_docsRoot, "keep.hwp", "keep hwp");
        var xlsx = TestOfficeFactory.WriteXlsx(_docsRoot, "keep.xlsx", "keep excel");
        var originalDocx = File.ReadAllBytes(docx);
        var originalHwp = File.ReadAllBytes(hwp);
        var originalXlsx = File.ReadAllBytes(xlsx);
        var docxMtime = File.GetLastWriteTimeUtc(docx);
        var hwpMtime = File.GetLastWriteTimeUtc(hwp);
        var xlsxMtime = File.GetLastWriteTimeUtc(xlsx);

        await new IndexingService(_userData).Start(_docsRoot);

        Assert.Equal(originalDocx, File.ReadAllBytes(docx));
        Assert.Equal(originalHwp, File.ReadAllBytes(hwp));
        Assert.Equal(originalXlsx, File.ReadAllBytes(xlsx));
        Assert.Equal(docxMtime, File.GetLastWriteTimeUtc(docx));
        Assert.Equal(hwpMtime, File.GetLastWriteTimeUtc(hwp));
        Assert.Equal(xlsxMtime, File.GetLastWriteTimeUtc(xlsx));
    }
}
