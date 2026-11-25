#!/bin/bash
# Builds the macOS app bundle: a self-contained .NET app inside "PSS Collection.app".
#
#   tools/build_macos.sh [osx-arm64|osx-x64]
set -euo pipefail
cd "$(dirname "$0")/.."

RID=${1:-osx-arm64}
OUT="build/macos/$RID"
APP="build/macos/PSS Collection.app"

rm -rf "$OUT" "$APP"
mkdir -p "$OUT"

dotnet publish PSSCollection.DesktopGL -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -o "$OUT"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT"/. "$APP/Contents/MacOS/"

# A publish marks some managed assemblies executable. codesign --deep takes any
# executable file for code and then rejects those PE assemblies as unsigned ("code
# object is not signed at all"), so only the apphost and the native libraries stay
# executable. An App Store bundle wants exactly that anyway.
find "$APP/Contents/MacOS" -type f ! -name PSSCollection ! -name '*.dylib' ! -name '*.so' \
    -exec chmod 644 {} +

if [ -f PSSCollection.DesktopGL/PSSCollection.icns ]; then
    cp PSSCollection.DesktopGL/PSSCollection.icns "$APP/Contents/Resources/AppIcon.icns"
fi

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>PSS Collection</string>
    <key>CFBundleDisplayName</key>
    <string>The PSS Oric Collection</string>
    <key>CFBundleIdentifier</key>
    <string>uk.co.allthejohnsons.psscollection</string>
    <key>CFBundleExecutable</key>
    <string>PSSCollection</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>26.0</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.arcade-games</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
PLIST

codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || echo "note: ad-hoc signing failed; the app still runs locally"

echo "Built: $APP"
