using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class IndexBackfillPolicyTests
{
    [Fact]
    public void backfills_when_files_exist_but_body_text_was_never_extracted()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensBackfill", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var coverage = new IndexCoverage(DocumentCount: 276, BodyCount: 0, OcrPageCount: 0, OcrEngineAvailable: false);

            Assert.True(IndexBackfillPolicy.ShouldBackfill(coverage, folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void does_not_backfill_when_body_index_already_has_text()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DocuLensBackfill", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var coverage = new IndexCoverage(DocumentCount: 276, BodyCount: 12, OcrPageCount: 0, OcrEngineAvailable: true);

            Assert.False(IndexBackfillPolicy.ShouldBackfill(coverage, folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void does_not_backfill_without_documents_or_a_real_folder()
    {
        Assert.False(IndexBackfillPolicy.ShouldBackfill(
            new IndexCoverage(0, 0, 0, false),
            Path.GetTempPath()));
        Assert.False(IndexBackfillPolicy.ShouldBackfill(
            new IndexCoverage(10, 0, 0, true),
            null));
        Assert.False(IndexBackfillPolicy.ShouldBackfill(
            new IndexCoverage(10, 0, 0, true),
            Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"))));
    }
}

public class SearchStatusFormatterTests
{
    [Fact]
    public void coverage_does_not_claim_ocr_when_no_engine_is_available()
    {
        var text = SearchStatusFormatter.Coverage(new IndexCoverage(276, 0, 0, OcrEngineAvailable: false));

        Assert.Contains("276건", text, StringComparison.Ordinal);
        Assert.Contains("본문 0건", text, StringComparison.Ordinal);
        Assert.Contains("OCR 엔진 없음", text, StringComparison.Ordinal);
    }

    [Fact]
    public void coverage_shows_ocr_page_count_when_engine_is_ready()
    {
        var text = SearchStatusFormatter.Coverage(new IndexCoverage(276, 200, 15, OcrEngineAvailable: true));

        Assert.Contains("본문 200건", text, StringComparison.Ordinal);
        Assert.Contains("OCR 15쪽", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OCR 엔진 없음", text, StringComparison.Ordinal);
    }

    [Fact]
    public void empty_results_tell_the_user_body_is_being_read()
    {
        var text = SearchStatusFormatter.EmptyResults(documentCount: 276, bodyCount: 0, indexingNow: true);

        Assert.Contains("본문", text, StringComparison.Ordinal);
        Assert.DoesNotContain("폴더 변경", text, StringComparison.Ordinal);
    }

    [Fact]
    public void empty_results_stop_telling_users_to_reindex_when_body_exists()
    {
        var text = SearchStatusFormatter.EmptyResults(documentCount: 276, bodyCount: 10, indexingNow: false);

        Assert.Contains("본문", text, StringComparison.Ordinal);
        Assert.DoesNotContain("다시 인덱싱", text, StringComparison.Ordinal);
    }
}

public class SearchIdleCopyTests
{
    [Fact]
    public void idle_hint_names_the_indexed_document_count()
    {
        var hint = SearchIdleCopy.Hint(new IndexCoverage(276, 276, 707, true));

        Assert.Equal("파일명이나 본문 단어로 찾아 보세요", SearchIdleCopy.Headline);
        Assert.Contains("276개 문서", hint, StringComparison.Ordinal);
        Assert.Contains("버스 광고", SearchIdleCopy.Examples);
        Assert.Contains("부대", SearchIdleCopy.Examples);
    }

    [Fact]
    public void idle_hint_without_documents_is_plain_korean()
    {
        Assert.Equal("아직 인덱싱된 문서가 없습니다.", SearchIdleCopy.Hint(new IndexCoverage(0, 0, 0, false)));
    }
}

public class TessdataLocatorTests
{
    [Fact]
    public void finds_directory_with_english_and_korean_traineddata()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensTess", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "eng.traineddata"), "stub");
        File.WriteAllText(Path.Combine(dir, "kor.traineddata"), "stub");

        try
        {
            Assert.True(TessdataLocator.HasLanguageData(dir));
            Assert.Equal("kor+eng", TessdataLocator.ResolveLanguages(dir));
            Assert.Equal("kor", OcrLanguage.Primary(dir));
            Assert.Equal("eng", OcrLanguage.Fallback(dir, "kor"));
            Assert.True(OcrLanguage.ShouldTryFallback("Hi"));
            Assert.False(OcrLanguage.ShouldTryFallback("버스 광고 계약 조항은 을의 의무를 정한다"));
            Assert.Equal(dir, TessdataLocator.FindDirectory(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void missing_traineddata_is_not_a_language_pack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensTess", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            Assert.False(TessdataLocator.HasLanguageData(dir));
            Assert.Null(TessdataLocator.FindDirectory(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class TessdataInstallerTests
{
    [Fact]
    public async Task downloads_eng_and_kor_when_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensTessInstall", Guid.NewGuid().ToString("N"));
        using var handler = new StubTessdataHandler();
        var installer = new TessdataInstaller(handler);

        try
        {
            var ready = await installer.EnsureAsync(dir);

            Assert.Equal(dir, ready);
            Assert.True(TessdataLocator.HasLanguageData(dir));
            Assert.Equal(2, handler.Hits);
            Assert.Equal("fake-eng", File.ReadAllText(Path.Combine(dir, "eng.traineddata")));
            Assert.Equal("fake-kor", File.ReadAllText(Path.Combine(dir, "kor.traineddata")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task does_not_redownload_existing_language_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DocuLensTessInstall", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "eng.traineddata"), "keep-eng");
        File.WriteAllText(Path.Combine(dir, "kor.traineddata"), "keep-kor");
        using var handler = new StubTessdataHandler();
        var installer = new TessdataInstaller(handler);

        try
        {
            await installer.EnsureAsync(dir);

            Assert.Equal(0, handler.Hits);
            Assert.Equal("keep-eng", File.ReadAllText(Path.Combine(dir, "eng.traineddata")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class StubTessdataHandler : HttpMessageHandler
    {
        public int Hits { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Hits++;
            var body = request.RequestUri?.AbsoluteUri.Contains("kor", StringComparison.OrdinalIgnoreCase) == true
                ? "fake-kor"
                : "fake-eng";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }
}

public class CompositeOcrEngineTests
{
    [Fact]
    public void uses_first_available_engine()
    {
        var first = new StubOcrEngine(available: true, text: "from-first");
        var second = new StubOcrEngine(available: true, text: "from-second");
        var engine = new CompositeOcrEngine(first, second);

        Assert.True(engine.IsAvailable);
        Assert.Equal("from-first", engine.RecognizePng([1, 2, 3]));
        Assert.Equal(1, first.Calls);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public void skips_unavailable_engines()
    {
        var missing = new StubOcrEngine(available: false, text: "nope");
        var ready = new StubOcrEngine(available: true, text: "ready");
        var engine = new CompositeOcrEngine(missing, ready);

        Assert.Equal("ready", engine.RecognizePng([9]));
        Assert.Equal(0, missing.Calls);
        Assert.Equal(1, ready.Calls);
    }

    [Fact]
    public void does_not_run_a_second_engine_when_the_first_available_one_returns_empty()
    {
        var first = new StubOcrEngine(available: true, text: "   ");
        var second = new StubOcrEngine(available: true, text: "slow-fallback");
        var engine = new CompositeOcrEngine(first, second);

        Assert.Equal("   ", engine.RecognizePng([1]));
        Assert.Equal(1, first.Calls);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public void library_engine_without_tessdata_is_unavailable()
    {
        var missing = Path.Combine(Path.GetTempPath(), "DocuLensNoTess", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(missing);

        try
        {
            var engine = new TesseractLibraryOcrEngine(missing);
            Assert.False(engine.IsAvailable);
            Assert.Equal(string.Empty, engine.RecognizePng([1]));
        }
        finally
        {
            Directory.Delete(missing, recursive: true);
        }
    }

    private sealed class StubOcrEngine : IOcrEngine
    {
        private readonly string _text;

        public StubOcrEngine(bool available, string text)
        {
            IsAvailable = available;
            _text = text;
        }

        public bool IsAvailable { get; }
        public int Calls { get; private set; }

        public string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken = default)
        {
            Calls++;
            return _text;
        }
    }
}
