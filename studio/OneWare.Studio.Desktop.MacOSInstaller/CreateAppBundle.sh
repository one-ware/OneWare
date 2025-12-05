#!/bin/sh
set -e

rm -rf publish
mkdir publish

VERSION=$(grep -o '<Version>[^<]*</Version>' ../../build/props/Base.props | sed -E 's/<\/?Version>//g')

# Create Info.plist
./CreateInfoPlist.sh "$VERSION"

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

PACKAGENAME="publish/OneWareStudio-arm64.dmg"

echo "Codesigning ARM64 app..."

# Sign inner binaries
find "source/OneWare Studio.app" -type f -perm +111 -print0 | while IFS= read -r -d '' file; do
    echo "Signing inner binary: $file"
    codesign --force --options runtime --timestamp --sign "$MAC_CERT_ID" "$file"
done

# Sign entire .app bundle
codesign --force --options runtime --timestamp --deep --sign "$MAC_CERT_ID" "source/OneWare Studio.app"

# Verify
codesign --verify --deep --strict --verbose=2 "source/OneWare Studio.app"

echo "Creating ARM64 DMG..."

create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWare Studio.app" 200 190 \
  --hide-extension "OneWare Studio.app" \
  --app-drop-link 600 185 \
  "$PACKAGENAME" \
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

PACKAGENAME="publish/OneWareStudio-x64.dmg"

echo "Codesigning x64 app..."

# Sign inner binaries
find "source/OneWare Studio.app" -type f -perm +111 -print0 | while IFS= read -r -d '' file; do
    echo "Signing inner binary: $file"
    codesign --force --options runtime --timestamp --sign "$MAC_CERT_ID" "$file"
done

# Sign entire .app bundle
codesign --force --options runtime --timestamp --deep --sign "$MAC_CERT_ID" "source/OneWare Studio.app"

# Verify
codesign --verify --deep --strict --verbose=2 "source/OneWare Studio.app"

echo "Creating x64 DMG..."

create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWare Studio.app" 200 190 \
  --hide-extension "OneWare Studio.app" \
  --app-drop-link 600 185 \
  "$PACKAGENAME" \
  "source/"

echo "============================"
echo "   Build + Signing Done     "
echo "============================"
