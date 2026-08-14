#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <publish-directory> <output-app> <version> <build-number>" >&2
  exit 64
fi

publish_directory="$1"
output_app="$2"
version="$3"
build_number="$4"
script_directory="$(cd "$(dirname "$0")" && pwd)"
repository_root="$(cd "$script_directory/../.." && pwd)"

if [[ ! -x "$publish_directory/NetworkDiagnosticsDesktop" ]]; then
  echo "Missing macOS desktop executable: $publish_directory/NetworkDiagnosticsDesktop" >&2
  exit 66
fi

if [[ ! -f "$publish_directory/wwwroot/index.html" ]]; then
  echo "Missing packaged desktop frontend: $publish_directory/wwwroot/index.html" >&2
  exit 66
fi

if [[ -e "$output_app" ]]; then
  echo "Refusing to overwrite existing application bundle: $output_app" >&2
  exit 73
fi

mkdir -p "$output_app/Contents/MacOS" "$output_app/Contents/Resources"
cp -R "$publish_directory/." "$output_app/Contents/MacOS/"
cp "$script_directory/Resources/AppIcon.icns" "$output_app/Contents/Resources/AppIcon.icns"
cp "$repository_root/LICENSE" "$output_app/Contents/Resources/LICENSE.txt"
cp "$script_directory/DISTRIBUTION.md" "$output_app/Contents/Resources/README.md"
chmod +x "$output_app/Contents/MacOS/NetworkDiagnosticsDesktop"

plist="$output_app/Contents/Info.plist"
plutil -create xml1 "$plist"
plutil -insert CFBundleDevelopmentRegion -string en "$plist"
plutil -insert CFBundleDisplayName -string "Network Diagnostics" "$plist"
plutil -insert CFBundleExecutable -string NetworkDiagnosticsDesktop "$plist"
plutil -insert CFBundleIdentifier -string dev.johnnyli.networkdiagnostics "$plist"
plutil -insert CFBundleInfoDictionaryVersion -string 6.0 "$plist"
plutil -insert CFBundleIconFile -string AppIcon "$plist"
plutil -insert CFBundleName -string "Network Diagnostics" "$plist"
plutil -insert CFBundlePackageType -string APPL "$plist"
plutil -insert CFBundleShortVersionString -string "$version" "$plist"
plutil -insert CFBundleVersion -string "$build_number" "$plist"
plutil -insert LSApplicationCategoryType -string public.app-category.utilities "$plist"
plutil -insert LSMinimumSystemVersion -string 12.0 "$plist"
plutil -insert NSHighResolutionCapable -bool true "$plist"
plutil -lint "$plist"
