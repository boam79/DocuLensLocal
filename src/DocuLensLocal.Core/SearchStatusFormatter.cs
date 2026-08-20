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

    public static string EmptyResults(int documentCount, int bodyCount, bool indexingNow)
    {
        if (documentCount == 0)
        {
            return "인덱싱된 PDF가 없습니다. 아래에서 폴더를 바꿔 다시 인덱싱할 수 있습니다.";
        }

        if (indexingNow && bodyCount == 0)
        {
            return "파일명에 그 단어가 없습니다. 지금 PDF 본문을 읽고 있으니 잠시 후 다시 검색하세요.";
        }

        if (bodyCount == 0)
        {
            return "파일명에 그 단어가 없습니다. 본문이 비어 있으면 앱이 자동으로 다시 읽습니다. 안 되면 「폴더 변경 / 다시 인덱싱」을 누르세요.";
        }

        return "조건에 맞는 파일이 없습니다. 파일명 또는 본문(OCR 포함)에 단어가 있어야 합니다.";
    }
}
