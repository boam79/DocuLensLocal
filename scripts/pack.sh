#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app_csproj="$root/src/DocuLensLocal.App/DocuLensLocal.App.csproj"
publish_dir="$root/artifacts/publish"
release_dir="$root/artifacts/Releases"
version="0.1.6"
splash="$root/assets/splash.png"
icon_icns="$root/assets/app.icns"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found. Install .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0" >&2
  exit 1
fi

uname_s="$(uname -s)"
uname_m="$(uname -m)"
case "$uname_s" in
  Darwin)
    if [[ "$uname_m" == "arm64" ]]; then
      rid="osx-arm64"
    else
      rid="osx-x64"
    fi
    main_exe="DocuLensLocal"
    ;;
  Linux)
    if [[ "$uname_m" == "aarch64" || "$uname_m" == "arm64" ]]; then
      rid="linux-arm64"
    else
      rid="linux-x64"
    fi
    main_exe="DocuLensLocal"
    ;;
  MINGW*|MSYS*|CYGWIN*)
    rid="win-x64"
    main_exe="DocuLensLocal.exe"
    ;;
  *)
    echo "Unsupported OS: $uname_s" >&2
    exit 1
    ;;
esac

echo "dotnet: $(command -v dotnet)"
echo "rid: $rid"

dotnet publish "$app_csproj" -c Release --self-contained -r "$rid" -o "$publish_dir"
dotnet tool restore
mkdir -p "$release_dir"

if [[ "$rid" == osx-* && ! -f "$icon_icns" ]]; then
  echo "Published: $publish_dir"
  echo "Mac installer packing needs assets/app.icns and must run on macOS. Use: dotnet run --project src/DocuLensLocal.App"
  ls -l "$publish_dir"
  exit 0
fi

pack_args=(
  tool run vpk -- pack
  --packId DocuLensLocal
  --packVersion "$version"
  --packDir "$publish_dir"
  --packTitle "DocuLens Local"
  --packAuthors "DocuLens Local"
  --mainExe "$main_exe"
  --outputDir "$release_dir"
)

if [[ "$rid" == win-* && -f "$splash" ]]; then
  pack_args+=(--splashImage "$splash")
fi

if [[ "$rid" == osx-* ]]; then
  pack_args+=(--icon "$icon_icns")
fi

dotnet "${pack_args[@]}"
echo "Installer: $release_dir"
ls -l "$release_dir"
