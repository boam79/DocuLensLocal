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
    }

    [Fact]
    public void pack_scripts_do_not_hardcode_windows_dotnet_path_only()
    {
        var packPs1 = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "pack.ps1"));
        var packSh = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "pack.sh"));

        Assert.DoesNotContain(@"$dotnet = ""C:\Program Files\dotnet\dotnet.exe""", packPs1, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", packPs1, StringComparison.Ordinal);
        Assert.Contains("osx-x64", packSh, StringComparison.Ordinal);
        Assert.Contains("dotnet", packSh, StringComparison.Ordinal);
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
