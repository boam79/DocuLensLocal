$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$publishDir = Join-Path $root "artifacts\publish"
$releaseDir = Join-Path $root "artifacts\Releases"
$appCsproj = Join-Path $root "src\DocuLensLocal.App\DocuLensLocal.App.csproj"
$version = "0.1.0"

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
  --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer: $releaseDir"
Get-ChildItem $releaseDir | Select-Object Name, Length
