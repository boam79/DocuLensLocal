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
$version = "0.1.10"
$splash = Join-Path $root "assets/splash.png"

Write-Host "dotnet: $dotnet"

& $dotnet publish $appCsproj -c Release --self-contained -r win-x64 -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Ensure-Tessdata([string]$dest) {
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $files = @{
        "eng.traineddata" = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata"
        "kor.traineddata" = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/kor.traineddata"
    }
    foreach ($name in $files.Keys) {
        $path = Join-Path $dest $name
        if (-not (Test-Path $path) -or ((Get-Item $path).Length -lt 1000)) {
            Write-Host "Downloading $name"
            Invoke-WebRequest -Uri $files[$name] -OutFile $path -UseBasicParsing
        }
    }
}

Ensure-Tessdata (Join-Path $publishDir "tessdata")

function Ensure-TesseractNatives([string]$outDir) {
    $dest = Join-Path $outDir "x64"
    $dll = Join-Path $dest "tesseract50.dll"
    if (Test-Path $dll) { return }
    $roots = @()
    if ($env:HOME) { $roots += (Join-Path $env:HOME ".nuget/packages/tesseract/5.2.0") }
    if ($env:USERPROFILE) { $roots += (Join-Path $env:USERPROFILE ".nuget/packages/tesseract/5.2.0") }
    $nuget = $env:NUGET_PACKAGES
    if ($nuget) { $roots += (Join-Path $nuget "tesseract/5.2.0") }
    foreach ($pkg in $roots) {
        foreach ($srcName in @("x64", "build/x64")) {
            $src = Join-Path $pkg $srcName
            if (Test-Path $src) {
                New-Item -ItemType Directory -Force -Path $dest | Out-Null
                Copy-Item (Join-Path $src "*") $dest
                Write-Host "Copied Tesseract natives from $src"
                return
            }
        }
    }
}

Ensure-TesseractNatives $publishDir

$tesseractDll = Join-Path $publishDir "x64/tesseract50.dll"
if (-not (Test-Path $tesseractDll)) {
    Write-Warning "Tesseract native DLL missing at $tesseractDll — OCR may fall back to PATH."
}

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
