#!/bin/sh
set -e

# Path to entitlements (relative to this script)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENTITLEMENTS="$SCRIPT_DIR/OneWareStudio.entitlements"

echo "Using signing identity: $MAC_CERT_ID"
echo "Using entitlements: $ENTITLEMENTS"

rm -rf publish
mkdir publish

VERSION=$(grep -o '<Version>[^<]*</Version>' ../../build/props/Base.props | sed -E 's/<\/?Version>//g')

# Create Info.plist
./CreateInfoPlist.sh "$VERSION"

#############################################
# FUNCTION TO SIGN AVALONIA APP
#############################################
sign_app_bundle() {
    APP="$1"
    ENT="$ENTITLEMENTS"

    echo "----------------------------------------"
    echo "  Signing binaries inside $APP"
    echo "----------------------------------------"

    # Sign .dylib, .so, .dll files
    find "$APP" -type f \( -name "*.dylib" -o -name "*.so" -o -name "*.dll" \) -print0 | xargs -0 -I {} codesign \
        --force \
        --timestamp \
        --options runtime \
        --entitlements "$ENT" \
        --sign "$MAC_CERT_ID" \
        "{}"

    echo "----------------------------------------"
    echo "  Signing main executable"
    echo "----------------------------------------"

    codesign \
        --force \
        --timestamp \
        --options runtime \
        --entitlements "$ENT" \
        --sign "$MAC_CERT_ID" \
        "$APP/Contents/MacOS/OneWareStudio"

    echo "----------------------------------------"
    echo "  Signing .app bundle"
    echo "----------------------------------------"

    codesign \
        --force \
        --timestamp \
        --options runtime \
        --entitlements "$ENT" \
        --sign "$MAC_CERT_ID" \
        "$APP"

    echo "----------------------------------------"
    echo "  Verifying .app bundle"
    echo "----------------------------------------"

    codesign --verify --deep --strict --verbose=4 "$APP"
}

#############################################
#               ARM64 BUILD
#############################################

echo "============================"
echo " Building ARM64 App Bundle "
echo "============================"

rm -rf source
mkdir -p "source/OneWare Studio.app/Contents"
cp -r Contents "source/OneWare Studio.app"
mkdir -p "source/OneWare Studio.app/Contents/MacOS"

dotnet publish -c Release -f net10.0 -r osx-arm64 --self-contained \
  ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj \
  -o "source/OneWare Studio.app/Contents/MacOS"

ARM_APP="source/OneWare Studio.app"
ARM_DMG="publish/OneWareStudio-arm64.dmg"

sign_app_bundle "$ARM_APP"

echo "Creating ARM64 DMG..."
create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWare Studio.app" 200 190 \
  --app-drop-link 600 185 \
  "$ARM_DMG" \
  "source/"


#############################################
#               X64 BUILD
#############################################

echo "============================"
echo " Building x64 App Bundle "
echo "============================"

rm -rf source
mkdir -p "source/OneWare Studio.app/Contents"
cp -r Contents "source/OneWare Studio.app"
mkdir -p "source/OneWare Studio.app/Contents/MacOS"

dotnet publish -c Release -f net10.0 -r osx-x64 --self-contained \
  ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj \
  -o "source/OneWare Studio.app/Contents/MacOS"

X64_APP="source/OneWare Studio.app"
X64_DMG="publish/OneWareStudio-x64.dmg"

sign_app_bundle "$X64_APP"

echo "Creating x64 DMG..."
create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWare Studio.app" 200 190 \
  --app-drop-link 600 185 \
  "$X64_DMG" \
  "source/"

echo "============================"
echo "   Build + Signing Done     "
echo "============================"
