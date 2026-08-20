using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class OfficeOcrTests
{
    [Fact]
    public void sparse_docx_image_is_ocrd_and_searchable()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensOfficeOcr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = TestPdfFactory.RenderOpaquePng("scan");
            var path = TestOfficeFactory.WriteDocxWithImage(dir, "내부문서.docx", ".", png);
            var ocr = new CountingOcr("본 버스 광고 계약");
            var extractor = new CompositeDocumentExtractor(new PdfPigContentExtractor(), ocr);

            var extracted = extractor.Extract(path);

            Assert.Equal(1, ocr.Calls);
            Assert.Contains("버스 광고 계약", extracted.BodyText, StringComparison.Ordinal);
            Assert.True(extracted.OcrPageCount >= 1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void rich_docx_skips_image_ocr()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensOfficeOcr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = TestPdfFactory.RenderOpaquePng("scan");
            var digital = new string('가', 120);
            var path = TestOfficeFactory.WriteDocxWithImage(dir, "긴글.docx", digital, png);
            var ocr = new CountingOcr("이 글자는 들어가면 안 됨");
            var extractor = new CompositeDocumentExtractor(new PdfPigContentExtractor(), ocr);

            var extracted = extractor.Extract(path);

            Assert.Equal(0, ocr.Calls);
            Assert.Contains(digital, extracted.BodyText, StringComparison.Ordinal);
            Assert.Equal(0, extracted.OcrPageCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void sparse_hwpx_and_hwp_images_are_ocrd()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensOfficeOcr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = TestPdfFactory.RenderOpaquePng("scan");
            var hwpx = TestOfficeFactory.WriteHwpxWithImage(dir, "스캔.hwpx", "", png);
            var hwp = TestOfficeFactory.WriteHwpWithImage(dir, "스캔.hwp", "", png);
            var ocr = new CountingOcr("부대 시설");
            var extractor = new CompositeDocumentExtractor(new PdfPigContentExtractor(), ocr);

            var hwpxText = extractor.Extract(hwpx);
            var hwpText = extractor.Extract(hwp);

            Assert.True(ocr.Calls >= 2);
            Assert.Contains("부대", hwpxText.BodyText, StringComparison.Ordinal);
            Assert.Contains("부대", hwpText.BodyText, StringComparison.Ordinal);
            Assert.True(hwpxText.OcrPageCount >= 1);
            Assert.True(hwpText.OcrPageCount >= 1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task indexed_scan_docx_is_found_by_ocr_text()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocuLensOfficeOcrIdx", Guid.NewGuid().ToString("N"));
        var userData = Path.Combine(root, "userdata");
        var docs = Path.Combine(root, "docs");
        Directory.CreateDirectory(userData);
        Directory.CreateDirectory(docs);
        try
        {
            var png = TestPdfFactory.RenderOpaquePng("scan");
            TestOfficeFactory.WriteDocxWithImage(docs, "내부문서.docx", ".", png);
            var extractor = new CompositeDocumentExtractor(new PdfPigContentExtractor(), new CountingOcr("본 버스 광고 계약"));
            var service = new IndexingService(userData, extractor);
            await service.Start(docs);

            var hit = Assert.Single(service.Search("버스 광고"));
            Assert.Equal(SearchMatchKind.Body, hit.MatchKind);
            Assert.Contains("OCR", hit.MatchLabelKo, StringComparison.Ordinal);
        }
        finally
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

    private sealed class CountingOcr : IOcrEngine
    {
        private readonly string _text;

        public CountingOcr(string text) => _text = text;

        public bool IsAvailable => true;

        public int Calls { get; private set; }

        public string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.True(pngBytes.Length >= OfficeBodyExtractor.MinImageBytes);
            return _text;
        }
    }
}
