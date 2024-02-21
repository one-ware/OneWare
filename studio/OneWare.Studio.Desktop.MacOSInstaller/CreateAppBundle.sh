#!/bin/sh
rm -rf publish
mkdir publish

#x64
rm -rf source
mkdir -p source/OneWareStudio.app/Contents
cp -r Contents source/OneWareStudio.app
mkdir OneWareStudio.app/Contents/MacOS
dotnet publish -c Release -f net8.0 -r osx-x64 --self-contained ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj -o source/OneWareStudio.app/Contents/MacOS

VERSION=$(strings source/OneWareStudio.app/Contents/MacOS/OneWareStudio.dll | egrep '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$')
PACKAGENAME="publish/OneWareStudio-$VERSION-x64.dmg"

#codesign -f -s "Developer ID Application: hmennen@protop-solutions.com" -v "source/OneWareStudio.app"

create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWareStudio.app" 200 190 \
  --hide-extension "OneWareStudio.app" \
  --app-drop-link 600 185 \
  $PACKAGENAME \
  "source/"

#arm64
rm -rf source
mkdir -p source/OneWareStudio.app/Contents
cp -r Contents source/OneWareStudio.app
mkdir OneWareStudio.app/Contents/MacOS
dotnet publish -c Release -f net8.0 -r osx-arm64 --self-contained ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj -o source/OneWareStudio.app/Contents/MacOS

VERSION=$(strings source/OneWareStudio.app/Contents/MacOS/OneWareStudio.dll | egrep '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$')
PACKAGENAME="publish/OneWareStudio-$VERSION-arm64.dmg"

create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWareStudio.app" 200 190 \
  --hide-extension "OneWareStudio.app" \
  --app-drop-link 600 185 \
  $PACKAGENAME \
  "source/"
