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

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Resolve-Dotnet
$publishDir = Join-Path $root "artifacts/publish"
$releaseDir = Join-Path $root "artifacts/Releases"
$appCsproj = Join-Path $root "src/DocuLensLocal.App/DocuLensLocal.App.csproj"
$version = "0.1.9"
$splash = Join-Path $root "assets/splash.png"

Write-Host "dotnet: $dotnet"

& $dotnet publish $appCsproj -c Release --self-contained -r win-x64 -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
& $dotnet tool run vpk -- pack `
  --packId DocuLensLocal `
  --packVersion $version `
  --packDir $publishDir `
  --packTitle "DocuLens Local" `
  --packAuthors "DocuLens Local" `
  --mainExe DocuLensLocal.exe `
  --splashImage $splash `
  --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer: $releaseDir"
Get-ChildItem $releaseDir | Select-Object Name, Length
