#!/bin/bash
# Builds the store releases and drops them in Releases/<version>/.
#
#   tools/build_release.sh [macos] [ios] [android] [windows]
#
# With no arguments every platform is built. macOS becomes a signed-into-a-flat
# .pkg installer, iOS an App Store .ipa, Android a signed .aab (Play) plus a
# signed .apk (sideloading), and Windows one MSIX per architecture, packed in the
# Parallels Windows VM because MSIX can only be assembled there.
#
# Nothing here is ever committed: Releases/ is ignored by git.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)
BUILD=$(sed -n 's:.*<BuildNumber>\(.*\)</BuildNumber>.*:\1:p' Directory.Build.props | head -1)
VERSION=${VERSION:-1.0.0}
BUILD=${BUILD:-1}

OUT="Releases/$VERSION"
mkdir -p "$OUT"

say() { printf '\n== %s\n' "$*"; }

# ---------------------------------------------------------------- macOS (.pkg)
build_macos() {
    # Apple silicon only. A .NET publish carries an architecture-specific
    # System.Private.CoreLib.dll, so the two publishes cannot be lipo-merged into one
    # universal bundle: the arm64 apphost would refuse the x64 CoreLib.
    say "macOS app bundle (arm64)"
    tools/build_macos.sh osx-arm64 >/dev/null

    APP="build/macos/PSS Collection.app"

    # The self-contained runtime ships a crash-dump helper. It cannot be sandboxed or
    # signed for the store, and nothing needs it, so it is left out of the bundle.
    rm -f "$APP/Contents/MacOS/createdump"

    # The Mac App Store build carries its distribution profile inside the bundle and is
    # signed with the App Sandbox entitlements Transporter insists on.
    PROFILE="$HOME/Downloads/rel-pss-mac.provisionprofile"
    [ -f "$PROFILE" ] || { echo "missing $PROFILE" >&2; exit 1; }
    cp "$PROFILE" "$APP/Contents/embedded.provisionprofile"

    # Anything downloaded arrives with com.apple.quarantine, and App Store validation
    # rejects a package holding a single quarantined file (ITMS-91109). The profile is
    # the usual carrier, so every extended attribute in the bundle is cleared - not just
    # that file's - before the bundle is signed and packed.
    xattr -cr "$APP"
    if xattr -lr "$APP" | grep -q quarantine; then
        echo "quarantine attributes survived cleanup in $APP" >&2
        exit 1
    fi

    APP_ID="Apple Distribution: Paul Johnson (3UH7BE38T3)"
    INSTALLER_ID="3rd Party Mac Developer Installer: Paul Johnson (3UH7BE38T3)"

    # The sandbox needs the app identity from the profile, or it cannot reach
    # launchservicesd and the app dies the moment it starts.
    PROFILE_PLIST=$(mktemp -t pss-profile).plist
    security cms -D -i "$PROFILE" > "$PROFILE_PLIST"
    PROFILE_APP_ID=$(/usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.application-identifier" "$PROFILE_PLIST")
    PROFILE_TEAM=$(/usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.developer.team-identifier" "$PROFILE_PLIST")
    [ -n "$PROFILE_APP_ID" ] || { echo "profile has no application identifier" >&2; exit 1; }

    ENTITLEMENTS=$(mktemp -t pss-entitlements).plist
    cp PSSCollection.DesktopGL/macOS.entitlements "$ENTITLEMENTS"
    /usr/libexec/PlistBuddy -c "Add :com.apple.application-identifier string $PROFILE_APP_ID" "$ENTITLEMENTS" >/dev/null
    /usr/libexec/PlistBuddy -c "Add :com.apple.developer.team-identifier string $PROFILE_TEAM" "$ENTITLEMENTS" >/dev/null
    rm -f "$PROFILE_PLIST"

    say "signing the app"
    # Inside out, and with --deep: the runtime ships as PE assemblies, which codesign
    # signs with a detached signature kept in the file's extended attributes, and the
    # xattr cleanup above removed those along with the quarantine. One --deep pass signs
    # every nested object and the bundle that seals them; the bundle is then signed once
    # more because the sandbox entitlements belong to the app, not to the files inside it.
    codesign --force --deep --options runtime --timestamp --sign "$APP_ID" "$APP" >/dev/null
    codesign --force --options runtime --timestamp \
        --entitlements "$ENTITLEMENTS" --sign "$APP_ID" "$APP" >/dev/null
    codesign --verify --deep --strict "$APP"
    rm -f "$ENTITLEMENTS"

        say "macOS installer"
    # Transporter reads the minimum system version from the archive's product definition.
    # A --product definition is what it checks; a --distribution file's <os-version> is
    # reported back as "none", so the archive is assembled from the definition instead.
    STAGE=$(mktemp -d)
    cat > "$STAGE/product_definition.plist" <<DEF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>os</key>
    <array>
        <string>26.0</string>
    </array>
</dict>
</plist>
DEF
    productbuild --product "$STAGE/product_definition.plist" \
        --component "$APP" /Applications \
        --sign "$INSTALLER_ID" --timestamp \
        "$OUT/PSS-Collection-$VERSION-macOS.pkg" >/dev/null
    rm -rf "$STAGE"

    # The payload is what validation inspects, so check the assembled archive too.
    EXPANDED=$(mktemp -d)
    pkgutil --expand-full "$OUT/PSS-Collection-$VERSION-macOS.pkg" "$EXPANDED/pkg"
    if xattr -lr "$EXPANDED/pkg" | grep -q quarantine; then
        echo "the package payload carries com.apple.quarantine" >&2
        rm -rf "$EXPANDED"
        exit 1
    fi
    rm -rf "$EXPANDED"
    codesign -d --entitlements :- "$APP" 2>/dev/null | tr -d '\n' | sed 's/^/   entitlements: /;s/$/\n/'
    pkgutil --check-signature "$OUT/PSS-Collection-$VERSION-macOS.pkg" | head -3
    lipo -archs "$APP/Contents/MacOS/PSSCollection" | sed 's/^/   executable archs: /'
    for lib in "$APP"/Contents/MacOS/*.dylib; do lipo -archs "$lib"; done | sort -u |
        sed 's/^/   dylib archs: /'
}

# ------------------------------------------------------------------- iOS (.ipa)
build_ios() {
    say "iOS"
    # A release archive needs the App Store distribution profile installed.
    if [ ! -f "$HOME/Library/MobileDevice/Provisioning Profiles/rel-pss.mobileprovision" ]; then
        echo "installing rel-pss.mobileprovision from ~/Downloads"
        cp "$HOME/Downloads/rel-pss.mobileprovision" \
            "$HOME/Library/MobileDevice/Provisioning Profiles/rel-pss.mobileprovision"
    fi
    dotnet build PSSCollection.iOS -c Release -f net11.0-ios \
        -p:RuntimeIdentifier=ios-arm64 -v q
    cp PSSCollection.iOS/bin/Release/net11.0-ios/ios-arm64/PSSCollection.ipa \
        "$OUT/PSS-Collection-$VERSION.ipa"
}

# ------------------------------------------------------------- Android (.aab)
build_android() {
    say "Android"
    if [ ! -f "$HOME/keys/pss-upload.jks" ]; then
        echo "no upload key at ~/keys/pss-upload.jks - the packages will be unsigned" >&2
    fi
    local pass=""
    [ -f "$HOME/keys/pss-upload.properties" ] && \
        pass=$(sed -n 's/^storePassword=//p' "$HOME/keys/pss-upload.properties")

    PSS_KEYSTORE_PASS="$pass" dotnet build PSSCollection.Android -c Release -f net10.0-android \
        -p:AndroidPackageFormat=aab -v q
    cp PSSCollection.Android/bin/Release/net10.0-android/uk.co.allthejohnsons.psscollection-Signed.aab \
        "$OUT/PSS-Collection-$VERSION.aab"

    PSS_KEYSTORE_PASS="$pass" dotnet build PSSCollection.Android -c Release -f net10.0-android \
        -p:AndroidPackageFormat=apk -v q
    cp PSSCollection.Android/bin/Release/net10.0-android/uk.co.allthejohnsons.psscollection-Signed.apk \
        "$OUT/PSS-Collection-$VERSION.apk"
}

# ---------------------------------------------------------------- Windows MSIX
build_windows() {
    say "Windows MSIX (x64 and arm64, in the Parallels VM)"
    tools/build_windows.sh "$OUT"
}

[ $# -eq 0 ] && set -- macos ios android windows
for target in "$@"; do
    case "$target" in
        macos) build_macos ;;
        ios) build_ios ;;
        android) build_android ;;
        windows) build_windows ;;
        *) echo "unknown target: $target" >&2; exit 2 ;;
    esac
done

say "checksums"
# The manifest is written by the redirect, so it must not hash itself.
( cd "$OUT" && shasum -a 256 $(ls | grep -v '^SHA256SUMS.txt$') > SHA256SUMS.txt && cat SHA256SUMS.txt )
say "releases in $OUT"
