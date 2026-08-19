$ErrorActionPreference = "Stop"

function Resolve-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $win = Join-Path $env:ProgramFiles "dotnet/dotnet.exe"
    if ($win -and (Test-Path $win)) {
        return $win
    }

    throw "dotnet SDK not found. Install .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0"
}

function Get-PackRid {
    $windows = [System.Runtime.InteropServices.OSPlatform]::Windows
    $osx = [System.Runtime.InteropServices.OSPlatform]::OSX
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $isArm = $arch -eq [System.Runtime.InteropServices.Architecture]::Arm64

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($windows)) {
        return "win-x64"
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($osx)) {
        if ($isArm) { return "osx-arm64" }
        return "osx-x64"
    }

    if ($isArm) { return "linux-arm64" }
    return "linux-x64"
}

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Resolve-Dotnet
$publishDir = Join-Path $root "artifacts/publish"
$releaseDir = Join-Path $root "artifacts/Releases"
$appCsproj = Join-Path $root "src/DocuLensLocal.App/DocuLensLocal.App.csproj"
$version = "0.1.6"
$splash = Join-Path $root "assets/splash.png"
$iconIcns = Join-Path $root "assets/app.icns"
$rid = Get-PackRid
$mainExe = if ($rid.StartsWith("win-", [StringComparison]::Ordinal)) { "DocuLensLocal.exe" } else { "DocuLensLocal" }

Write-Host "dotnet: $dotnet"
Write-Host "rid: $rid"

& $dotnet publish $appCsproj -c Release --self-contained -r $rid -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

if ($rid.StartsWith("osx-", [StringComparison]::Ordinal) -and -not (Test-Path $iconIcns)) {
    Write-Host "Published: $publishDir"
    Write-Host "Mac installer packing needs assets/app.icns and must run on macOS. Use: dotnet run --project src/DocuLensLocal.App"
    Get-ChildItem $publishDir | Select-Object Name, Length
    exit 0
}

$packArgs = @(
    "tool", "run", "vpk", "--", "pack",
    "--packId", "DocuLensLocal",
    "--packVersion", $version,
    "--packDir", $publishDir,
    "--packTitle", "DocuLens Local",
    "--packAuthors", "DocuLens Local",
    "--mainExe", $mainExe,
    "--outputDir", $releaseDir
)

if ($rid.StartsWith("win-", [StringComparison]::Ordinal) -and (Test-Path $splash)) {
    $packArgs += @("--splashImage", $splash)
}

if ($rid.StartsWith("osx-", [StringComparison]::Ordinal)) {
    $packArgs += @("--icon", $iconIcns)
}

& $dotnet @packArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer: $releaseDir"
Get-ChildItem $releaseDir | Select-Object Name, Length
