using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class SolutionSmokeTests
{
    [Fact]
    public void Core_assembly_name_matches_product()
    {
        var name = typeof(AssemblyMarker).Assembly.GetName().Name;
        Assert.Equal(AssemblyMarker.Name, name);
    }

    [Fact]
    public void app_project_targets_cross_platform_net_not_wpf()
    {
        var csproj = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "DocuLensLocal.App.csproj"));

        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("net10.0-windows", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<UseWPF>true</UseWPF>", csproj, StringComparison.Ordinal);
        Assert.Contains("Avalonia", csproj, StringComparison.Ordinal);
        Assert.Contains("Avalonia.Desktop", csproj, StringComparison.Ordinal);
        Assert.Contains("<Version>0.1.29</Version>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void search_screen_has_reset_button_and_idle_copy()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "MainWindow.axaml"));

        Assert.Contains("x:Name=\"ResetSearchButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"초기화\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdleHintPanel\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdleFormatBadges\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PdfFormatButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WordFormatButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HangulFormatButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExcelFormatButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"FormatFilterButton_OnClick\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"PDF\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"WORD\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"HWP\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"EXCEL\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FormatFilterHintText\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ResultCountText\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IndexedFolderButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IndexedFolderPathText\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Classes=\"folder-link\"", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PDF · Word · 한글(HWP) · Excel 파일을 찾을 수 있습니다", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("검색하면 근거 문장과 함께 파일이 나타납니다", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"문서\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"버스 광고\"", axaml, StringComparison.Ordinal);
        Assert.Contains("파일명이나 본문 단어로 찾아 보세요", axaml, StringComparison.Ordinal);
        Assert.Contains("검색할 PDF, Word, 한글(HWP), Excel 파일이 들어 있는 폴더를 선택하세요", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FolderMenuButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"폴더\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RebuildIndexMenuItem\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"처음부터 다시 읽기\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SyncIndexMenuItem\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"새 파일만 읽기\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"폴더 변경\"", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"RebuildIndexButton\"", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"IndexedFolderText\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"열기\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"폴더에서 보기\"", axaml, StringComparison.Ordinal);
        Assert.Contains("HighlightedTextBlock", axaml, StringComparison.Ordinal);
        Assert.Contains("Classes=\"result-badge\"", axaml, StringComparison.Ordinal);
        Assert.Contains("이 컴퓨터에서만 찾습니다", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InfoFolderButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InfoDocumentCountText\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"이전 버전\"", axaml, StringComparison.Ordinal);
        Assert.Contains("새 버전이 있으면 여기서 받습니다.", axaml, StringComparison.Ordinal);
        Assert.Contains("종류 칸은 여러 개를 함께 고를 수 있습니다.", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("GitHub Releases에서 새 버전을 확인합니다.", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"DocuLens Local\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void update_dialog_has_confirm_and_later_buttons()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "MessageDialog.axaml"));

        Assert.Contains("x:Name=\"PrimaryButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SecondaryButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"확인\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"나중에\"", axaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"280\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void indexing_resume_after_update_is_wired_in_the_app()
    {
        var window = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "MainWindow.axaml.cs"));
        var settings = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.Core", "AppSettings.cs"));

        Assert.Contains("IndexingInProgress", settings, StringComparison.Ordinal);
        Assert.Contains("LastRunVersion", settings, StringComparison.Ordinal);
        Assert.Contains("IndexResumePolicy.ShouldResume", window, StringComparison.Ordinal);
        Assert.Contains("CancelIndexingForUpdate", window, StringComparison.Ordinal);
        Assert.Contains("IndexPass.NewAndChanged", window, StringComparison.Ordinal);
        Assert.Contains("PlanSync", window, StringComparison.Ordinal);
        Assert.Contains("TryWatchSyncAsync", window, StringComparison.Ordinal);
        Assert.Contains("FolderIndexWatch", window, StringComparison.Ordinal);
        Assert.Contains("_folderWatch.Ping()", window, StringComparison.Ordinal);
        Assert.Contains("FormatFilterButton_OnClick", window, StringComparison.Ordinal);
        Assert.Contains("_formatFilter", window, StringComparison.Ordinal);
        Assert.Contains("Search(query!, _formatFilter)", window, StringComparison.Ordinal);
        Assert.Contains("SearchFormatFilters.Includes", window, StringComparison.Ordinal);
        Assert.Contains("OpenResultButton_OnClick", window, StringComparison.Ordinal);
        Assert.Contains("RevealResultButton_OnClick", window, StringComparison.Ordinal);
        Assert.Contains("LocationLine", window, StringComparison.Ordinal);
        Assert.Contains("IndexedFolderButton_OnClick", window, StringComparison.Ordinal);
        Assert.Contains("CoverageChip", window, StringComparison.Ordinal);
        Assert.Contains("HitCount", window, StringComparison.Ordinal);
        Assert.Contains("다시 읽기", window, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesPolicy", window, StringComparison.Ordinal);
        Assert.Contains("AvailableUpdatePrompt", window, StringComparison.Ordinal);
        Assert.Contains("LastRunVersion", window, StringComparison.Ordinal);
        Assert.Contains("InfoStatusCopy", window, StringComparison.Ordinal);
        Assert.Contains("RefreshInfoPanel", window, StringComparison.Ordinal);
        Assert.Contains("ReleaseHistory.Recent", window, StringComparison.Ordinal);
        Assert.Contains("OlderHistoryExpander", window, StringComparison.Ordinal);
    }

    [Fact]
    public void core_ships_tesseract_library_for_windows_ocr()
    {
        var csproj = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.Core", "DocuLensLocal.Core.csproj"));

        Assert.Contains("Include=\"Tesseract\"", csproj, StringComparison.Ordinal);
        Assert.Contains("Include=\"HwpLibSharp\"", csproj, StringComparison.Ordinal);
        var access = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.Core", "OfficeFileAccess.cs"));
        var watch = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.Core", "FolderIndexWatch.cs"));
        Assert.Contains("FileShare.ReadWrite | FileShare.Delete", access, StringComparison.Ordinal);
        Assert.Contains("Filter = IndexWatchPolicy.FileWatcherFilter", watch, StringComparison.Ordinal);
    }

    [Fact]
    public void pack_script_finds_dotnet_on_path_and_still_builds_windows_installer()
    {
        var packPs1 = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "pack.ps1"));

        Assert.DoesNotContain(@"$dotnet = ""C:\Program Files\dotnet\dotnet.exe""", packPs1, StringComparison.Ordinal);
        Assert.Contains("Resolve-Dotnet", packPs1, StringComparison.Ordinal);
        Assert.Contains("win-x64", packPs1, StringComparison.Ordinal);
        Assert.Contains("[win] pack", packPs1, StringComparison.Ordinal);
        Assert.Contains("DocuLensLocal.exe", packPs1, StringComparison.Ordinal);
        Assert.Contains("tessdata", packPs1, StringComparison.Ordinal);
        Assert.Contains("eng.traineddata", packPs1, StringComparison.Ordinal);
        Assert.Contains("kor.traineddata", packPs1, StringComparison.Ordinal);
        Assert.Contains("tesseract50.dll", packPs1, StringComparison.Ordinal);
        Assert.DoesNotContain("osx-arm64", packPs1, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(FindRepoRoot(), "scripts", "pack.sh")));
    }

    [Fact]
    public void avalonia_control_templates_bind_contentpresenter()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "App.axaml"));
        var presenters = System.Text.RegularExpressions.Regex.Matches(axaml, "<ContentPresenter\\b");
        Assert.True(presenters.Count >= 4, $"expected button/tab/list presenters, found {presenters.Count}");
        Assert.Contains("Content=\"{TemplateBinding Content}\"", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ContentPresenter/>", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ContentPresenter HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\"/>", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void github_docs_folder_lists_product_topics()
    {
        var docs = Path.Combine(FindRepoRoot(), "docs");
        var index = File.ReadAllText(Path.Combine(docs, "README.md"));

        Assert.Contains("사용-안내.md", index, StringComparison.Ordinal);
        Assert.Contains("검색.md", index, StringComparison.Ordinal);
        Assert.Contains("인덱싱.md", index, StringComparison.Ordinal);
        Assert.Contains("설치와-업데이트.md", index, StringComparison.Ordinal);
        Assert.Contains("자주-묻는-질문.md", index, StringComparison.Ordinal);
        Assert.Contains("프로그램-구조.md", index, StringComparison.Ordinal);
        Assert.Contains("개발.md", index, StringComparison.Ordinal);
        Assert.Contains("고도화.md", index, StringComparison.Ordinal);
        Assert.Contains("ui-ux-제안.md", index, StringComparison.Ordinal);
        Assert.Contains("보안-제안.md", index, StringComparison.Ordinal);
        Assert.Contains("변경-이력.md", index, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(docs, "검색.md")));
        Assert.True(File.Exists(Path.Combine(docs, "보안-제안.md")));
        var search = File.ReadAllText(Path.Combine(docs, "검색.md"));
        Assert.Contains("여러 개를 함께", search, StringComparison.Ordinal);
        Assert.Contains("폴더에서 보기", search, StringComparison.Ordinal);
        Assert.Contains("제목 아래", search, StringComparison.Ordinal);
        Assert.Contains("폴더", search, StringComparison.Ordinal);
        var infoGuide = File.ReadAllText(Path.Combine(docs, "사용-안내.md"));
        Assert.Contains("지금 폴더", infoGuide, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DocuLensLocal.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "DocuLensLocal.slnx")))
        {
            return cwd;
        }

        throw new DirectoryNotFoundException("DocuLensLocal.slnx not found from test output or cwd.");
    }
}
