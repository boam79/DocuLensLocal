namespace DocuLensLocal.Core;

public static class InfoStatusCopy
{
    public const string Headline = "이 컴퓨터에서만 찾습니다";
    public const string UpdateCaption = "새 버전이 있으면 여기서 받습니다.";
    public const string OlderHistoryHeader = "이전 버전";
    public const string NoFolder = "아직 폴더를 고르지 않았습니다.";
    public const int RecentHistoryCount = 5;

    public static IReadOnlyList<string> Tips { get; } =
    [
        "종류 칸은 여러 개를 함께 고를 수 있습니다.",
        "검색 결과에서 열기로 파일을 엽니다.",
        "폴더가 다르면 파일이 안 나옵니다.",
    ];

    public static string FolderLine(string? folder) =>
        string.IsNullOrWhiteSpace(folder) ? NoFolder : folder.Trim();

    public static string DocumentCount(IndexCoverage coverage) =>
        coverage.DocumentCount <= 0 ? "문서 없음" : $"{coverage.DocumentCount}개";

    public static string BodyLabel(IndexCoverage coverage) =>
        coverage.BodyCount > 0 ? $"본문 {coverage.BodyCount}건" : "본문 없음";

    public static string OcrLabel(IndexCoverage coverage)
    {
        if (!coverage.OcrEngineAvailable)
        {
            return "OCR 엔진 없음";
        }

        return coverage.OcrPageCount > 0 ? $"OCR {coverage.OcrPageCount}쪽" : "OCR 없음";
    }
}
