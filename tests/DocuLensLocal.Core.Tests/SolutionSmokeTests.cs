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
        Assert.Contains("<Version>0.1.19</Version>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void search_screen_has_reset_button_and_idle_copy()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "MainWindow.axaml"));

        Assert.Contains("x:Name=\"ResetSearchButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"초기화\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdleHintPanel\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdleFormatBadges\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"PDF\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"WORD\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"HWP\"", axaml, StringComparison.Ordinal);
        Assert.Contains("PDF · Word · 한글(HWP) 파일을 찾을 수 있습니다", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"문서\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"버스 광고\"", axaml, StringComparison.Ordinal);
        Assert.Contains("파일명이나 본문 단어로 찾아 보세요", axaml, StringComparison.Ordinal);
        Assert.Contains("검색할 PDF, Word, 한글(HWP) 파일이 들어 있는 폴더를 선택하세요", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RebuildIndexButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"처음부터 다시 인덱싱\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SyncIndexButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"새 파일 인덱싱\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"폴더 변경\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void update_dialog_has_confirm_and_later_buttons()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "MessageDialog.axaml"));

        Assert.Contains("x:Name=\"PrimaryButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SecondaryButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"확인\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"나중에\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void indexing_resume_after_update_is_wired_in_the_app()
    {
        var window = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.App", "MainWindow.axaml.cs"));
        var settings = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.Core", "AppSettings.cs"));

        Assert.Contains("IndexingInProgress", settings, StringComparison.Ordinal);
        Assert.Contains("IndexResumePolicy.ShouldResume", window, StringComparison.Ordinal);
        Assert.Contains("CancelIndexingForUpdate", window, StringComparison.Ordinal);
        Assert.Contains("IndexPass.NewAndChanged", window, StringComparison.Ordinal);
        Assert.Contains("PlanSync", window, StringComparison.Ordinal);
    }

    [Fact]
    public void core_ships_tesseract_library_for_windows_ocr()
    {
        var csproj = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "DocuLensLocal.Core", "DocuLensLocal.Core.csproj"));

        Assert.Contains("Include=\"Tesseract\"", csproj, StringComparison.Ordinal);
        Assert.Contains("Include=\"HwpLibSharp\"", csproj, StringComparison.Ordinal);
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
