namespace DocuLensLocal.Core;

public static class SearchStatusFormatter
{
    public static string Coverage(IndexCoverage coverage)
    {
        var ocr = coverage.OcrEngineAvailable
            ? $"OCR {coverage.OcrPageCount}쪽"
            : "OCR 엔진 없음";
        return $"인덱싱 완료 · {coverage.DocumentCount}건 · 본문 {coverage.BodyCount}건 · {ocr}";
    }

    public static string CoverageProgress(int processedCount, int foundCount) =>
        $"본문 읽는 중 · {processedCount} / {foundCount}";

    public static string ResumeProgress(int processedCount, int foundCount) =>
        $"이어서 읽는 중 · {processedCount} / {foundCount}";

    public static string NewFilesProgress(int processedCount, int foundCount) =>
        $"새 파일 읽는 중 · {processedCount} / {foundCount}";

    public static string EmptyResults(
        int documentCount,
        int bodyCount,
        bool indexingNow,
        SearchFormatFilter format = SearchFormatFilter.All)
    {
        if (format != SearchFormatFilter.All && documentCount > 0)
        {
            return $"{SearchFormatFilters.LabelKo(format)}에서 조건에 맞는 파일이 없습니다. 종류를 다시 누르면 전체를 찾습니다.";
        }

        if (documentCount == 0)
        {
            return "인덱싱된 문서가 없습니다. 아래에서 폴더를 바꾸거나 「처음부터 다시 인덱싱」을 누르세요.";
        }

        if (indexingNow && bodyCount == 0)
        {
            return "파일명에 그 단어가 없습니다. 지금 문서 본문을 읽고 있으니 잠시 후 다시 검색하세요.";
        }

        if (bodyCount == 0)
        {
            return "파일명에 그 단어가 없습니다. 본문이 비어 있으면 앱이 자동으로 다시 읽습니다. 안 되면 「처음부터 다시 인덱싱」을 누르세요.";
        }

        return "조건에 맞는 파일이 없습니다. 파일명 또는 본문(OCR 포함)에 단어가 있어야 합니다.";
    }
}

public static class SearchIdleCopy
{
    public const string Headline = "파일명이나 본문 단어로 찾아 보세요";
    public const string Subtitle = "검색하면 근거 문장과 함께 파일이 나타납니다.";

    public static IReadOnlyList<string> Examples { get; } = ["버스 광고", "부대", "계약"];

    public static string Hint(IndexCoverage coverage) =>
        coverage.DocumentCount <= 0
            ? "아직 인덱싱된 문서가 없습니다."
            : $"{coverage.DocumentCount}개 문서에서 파일명과 본문을 찾습니다. 폴더에 파일을 더 넣으면 자동으로 읽습니다.";
}
