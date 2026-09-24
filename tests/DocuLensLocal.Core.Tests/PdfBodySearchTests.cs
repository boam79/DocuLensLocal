using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class EvidenceSnippetTests
{
    [Fact]
    public void surrounds_the_first_matching_token()
    {
        var body = "甲과 乙은 버스 광고 계약을 체결한다. 계약 기간은 2024년이다.";

        var snippet = EvidenceSnippet.From(body, ["버스", "광고"]);

        Assert.Contains("버스 광고 계약", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("찾아줘", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void empty_body_returns_empty()
    {
        Assert.Equal(string.Empty, EvidenceSnippet.From("  ", ["버스"]));
    }

    [Fact]
    public void highlight_marks_each_token_in_the_snippet()
    {
        var spans = EvidenceSnippet.Highlight("본 버스 광고 계약", ["버스", "광고"]);

        Assert.Equal(
            [
                new SnippetSpan("본 ", false),
                new SnippetSpan("버스", true),
                new SnippetSpan(" ", false),
                new SnippetSpan("광고", true),
                new SnippetSpan(" 계약", false),
            ],
            spans);
    }

    [Fact]
    public void highlight_without_tokens_is_plain_text()
    {
        var spans = EvidenceSnippet.Highlight("버스 광고", []);

        Assert.Equal([new SnippetSpan("버스 광고", false)], spans);
    }
}

public class PdfBodySearchTests : IDisposable
{
    private readonly string _userData;
    private readonly string _pdfRoot;

    public PdfBodySearchTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocuLensBodyTests", Guid.NewGuid().ToString("N"));
        _userData = Path.Combine(root, "userdata");
        _pdfRoot = Path.Combine(root, "pdfs");
        Directory.CreateDirectory(_userData);
        Directory.CreateDirectory(_pdfRoot);
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_userData)?.FullName;
        if (root is not null && Directory.Exists(root))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task digital_pdf_body_is_searchable_even_when_filename_does_not_match()
    {
        TestPdfFactory.WriteDigitalPdf(_pdfRoot, "NDA-only.pdf", "This advertising contract covers bus media placement.");

        var service = new IndexingService(_userData);
        await service.Start(_pdfRoot);

        var hits = service.Search("bus advertising");
        var hit = Assert.Single(hits);
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.Equal("본문", hit.MatchLabelKo);
        Assert.Contains("advertising contract", hit.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NDA-only.pdf", hit.Document.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("indexed", hit.Document.Status);
        Assert.True(hit.Document.PageCount >= 1);
    }

    [Fact]
    public async Task korean_nl_query_finds_body_text_not_just_filename()
    {
        File.WriteAllText(Path.Combine(_pdfRoot, "내부문서.pdf"), "%PDF-1.4 placeholder\n");
        var extractor = new StubExtractor("본 버스 광고 계약 조항은 을의 의무를 정한다.", ocrPages: 0);
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);

        var hits = service.Search("버스 광고 찾아줘");
        var hit = Assert.Single(hits);
        Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
        Assert.DoesNotContain("버스", Path.GetFileName(hit.Document.FilePath), StringComparison.Ordinal);
        Assert.Contains("버스 광고 계약", hit.Snippet, StringComparison.Ordinal);
        Assert.Equal("indexed", hit.Document.Status);
    }

    [Fact]
    public async Task injected_ocr_text_is_indexed_and_searchable()
    {
        File.WriteAllText(Path.Combine(_pdfRoot, "scan.pdf"), "%PDF-1.4 not a real scan\n");
        var extractor = new StubExtractor("스캔된 버스 광고 견적 본문", ocrPages: 1);
        var service = new IndexingService(_userData, extractor);
        await service.Start(_pdfRoot);

        var hit = Assert.Single(service.Search("버스 광고 찾아줘"));
        Assert.Equal("ocr", hit.Document.Status);
        Assert.Equal(1, hit.Document.OcrPageCount);
        Assert.Contains("OCR", hit.MatchLabelKo, StringComparison.Ordinal);
        Assert.Contains("견적", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task original_pdf_bytes_stay_untouched_after_text_extract()
    {
        var path = TestPdfFactory.WriteDigitalPdf(_pdfRoot, "keep.pdf", "Confidential memorandum of understanding.");
        var original = File.ReadAllBytes(path);
        var mtime = File.GetLastWriteTimeUtc(path);

        await new IndexingService(_userData).Start(_pdfRoot);

        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(mtime, File.GetLastWriteTimeUtc(path));
    }

    private sealed class StubExtractor : IPdfContentExtractor
    {
        private readonly string _body;
        private readonly int _ocrPages;

        public StubExtractor(string body, int ocrPages)
        {
            _body = body;
            _ocrPages = ocrPages;
        }

        public PdfExtractedContent Extract(string pdfPath, CancellationToken cancellationToken) =>
            new(_body, PageCount: 1, _ocrPages, [new PdfPageContent(1, "", _body)]);
    }
}

public class TesseractOcrEngineTests
{
    [Fact]
    public void printed_english_png_is_readable_when_tesseract_is_installed()
    {
        if (!TesseractCliOcrEngine.IsOnPath)
        {
            return;
        }

        var engine = new TesseractCliOcrEngine();
        Assert.True(engine.IsAvailable);
        var png = TestPdfFactory.RenderOpaquePng("ADVERTISING");
        var text = engine.RecognizePng(png);
        Assert.Contains("ADVERTISING", text, StringComparison.OrdinalIgnoreCase);
    }
}

public class PdfPigExtractorOcrBranchTests
{
    [Fact]
    public void ocr_runs_when_page_has_almost_no_letters_and_engine_is_available()
    {
        var ocr = new FixedOcrEngine("OCR_BUS_CONTRACT");
        var raster = new FixedRasterizer(TestPdfFactory.RenderOpaquePng("x"));
        var extractor = new PdfPigContentExtractor(ocr, raster);
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensOcrBranch", Guid.NewGuid().ToString("N"));
            var pdf = TestPdfFactory.WriteDigitalPdf(dir, "letters.pdf", "This memorandum of understanding contains enough digital letters to skip OCR.");

        try
        {
            var extracted = extractor.Extract(pdf);
            Assert.Contains("memorandum", extracted.BodyText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, extracted.OcrPageCount);
            Assert.False(ocr.Called);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class FixedOcrEngine : IOcrEngine
    {
        private readonly string _text;
        public bool Called { get; private set; }
        public bool IsAvailable => true;
        public FixedOcrEngine(string text) => _text = text;
        public string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken)
        {
            Called = true;
            return _text;
        }
    }

    private sealed class FixedRasterizer : IPdfPageRasterizer
    {
        private readonly byte[] _png;
        public FixedRasterizer(byte[] png) => _png = png;
        public byte[] RenderPng(string pdfPath, int pageNumber, CancellationToken cancellationToken) => _png;
    }
}
