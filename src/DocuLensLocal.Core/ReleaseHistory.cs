namespace DocuLensLocal.Core;

public sealed record ReleaseNote(string Version, string SummaryKo);

public static class ReleaseHistory
{
    public static IReadOnlyList<ReleaseNote> Known { get; } =
    [
        new("0.1.8", "버튼·목록 글자 표시"),
        new("0.1.7", "본문 검색·OCR·근거 문장"),
        new("0.1.6", "Mac에서 개발·실행"),
        new("0.1.5", "자연어 파일명 검색"),
        new("0.1.4", "정보 탭·모던 UI"),
        new("0.1.3", "인덱싱 완료 후 검색 본화면"),
        new("0.1.2", "인덱싱 버튼"),
        new("0.1.1", "스플래시/폴더선택"),
        new("0.1.0", "splash 없음"),
    ];
}
