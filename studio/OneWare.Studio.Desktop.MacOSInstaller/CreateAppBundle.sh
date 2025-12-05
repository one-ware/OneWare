#!/bin/sh
set -e

ENTITLEMENTS="./OneWareStudio.entitlements"

# Ensure certificate is known
echo "Using signing identity: $MAC_CERT_ID"
echo "Using entitlements: $ENTITLEMENTS"

rm -rf publish
mkdir publish

VERSION=$(grep -o '<Version>[^<]*</Version>' ../../build/props/Base.props | sed -E 's/<\/?Version>//g')

# Create Info.plist
./CreateInfoPlist.sh "$VERSION"

#############################################
# FUNCTION TO SIGN AVALONIA APP (required)
#############################################
sign_app_bundle() {
    APP_PATH="$1"

    echo "----------------------------------------"
    echo "  Signing all components in $APP_PATH"
    echo "----------------------------------------"

    # Sign every file inside Contents/MacOS
    find "$APP_PATH/Contents/MacOS" -type f | while read file; do
        echo "Signing: $file"
        codesign --force --timestamp --options runtime \
            --entitlements "$ENTITLEMENTS" \
            --sign "$MAC_CERT_ID" "$file"
    done

    echo "Signing entire .app bundle"
    codesign --force --timestamp --options runtime \
        --entitlements "$ENTITLEMENTS" \
        --sign "$MAC_CERT_ID" "$APP_PATH"

    echo "Verifying signature"
    codesign --verify --verbose "$APP_PATH"
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

dotnet publish -c Release -f net10.0 -r osx-arm64 --self-contained ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj \
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

dotnet publish -c Release -f net10.0 -r osx-x64 --self-contained ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj \
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
