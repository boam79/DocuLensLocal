namespace DocuLensLocal.Core;

public static class UpdatePromptCopy
{
    public const string AvailableTitle = "업데이트가 있습니다";
    public const string Confirm = "확인";
    public const string Later = "나중에";
    public const string NotesTitle = "업데이트 내역";
    public const string NotesOk = "확인";

    public static string AvailableBody(string version) =>
        $"새 버전 {version}을(를) 설치할까요? 확인을 누르면 업데이트하고 프로그램을 다시 시작합니다.";

    public static string InstallBuildOnly(string version) =>
        $"새 버전 {version}이(가) 있습니다. 설치본에서만 업데이트를 적용할 수 있습니다.";
}
