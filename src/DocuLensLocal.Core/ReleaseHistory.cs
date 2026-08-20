namespace DocuLensLocal.Core;

public sealed record ReleaseNote(string Version, string SummaryKo);

public static class ReleaseHistory
{
    public static IReadOnlyList<ReleaseNote> Known { get; } =
    [
        new("0.1.23", "엑셀·한글 다시 읽기"),
        new("0.1.22", "엑셀 넣으면 바로 인덱싱"),
        new("0.1.21", "Excel 본문 검색·OCR"),
        new("0.1.20", "폴더에 넣으면 자동 인덱싱"),
        new("0.1.19", "추가된 파일만 인덱싱"),
        new("0.1.18", "업데이트 후 인덱싱 이어서"),
        new("0.1.17", "검색 화면에 PDF·WORD·HWP 표시"),
        new("0.1.16", "Word·HWP 스캔 OCR·업데이트 안내"),
        new("0.1.15", "인덱스 초기화·재인덱싱"),
        new("0.1.14", "Word·HWP 본문 검색"),
        new("0.1.13", "검색 화면 다듬기"),
        new("0.1.12", "검색 초기 화면·초기화"),
        new("0.1.11", "OCR 속도 개선"),
        new("0.1.10", "본문 자동 인덱스·내장 OCR"),
        new("0.1.9", "버스광고 붙여 검색"),
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

    public static IReadOnlyList<ReleaseNote> Between(string? afterVersion, string throughVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(throughVersion);
        return Known.Where(note =>
            Compare(note.Version, afterVersion) > 0
            && Compare(note.Version, throughVersion) <= 0).ToList();
    }

    public static string FormatNotes(string? afterVersion, string throughVersion)
    {
        var notes = Between(afterVersion, throughVersion);
        if (notes.Count == 0)
        {
            return $"버전 {throughVersion}으로 업데이트했습니다.";
        }

        return string.Join("\n\n", notes.Select(note => $"{note.Version}\n{note.SummaryKo}"));
    }

    private static int Compare(string version, string? other)
    {
        if (string.IsNullOrWhiteSpace(other))
        {
            return 1;
        }

        if (Version.TryParse(Normalize(version), out var left) && Version.TryParse(Normalize(other), out var right))
        {
            return left.CompareTo(right);
        }

        return string.Compare(version, other, StringComparison.Ordinal);
    }

    private static string Normalize(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 3 ? version : version + ".0";
    }
}
