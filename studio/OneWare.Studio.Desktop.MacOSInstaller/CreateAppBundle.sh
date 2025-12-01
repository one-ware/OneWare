#!/bin/sh
rm -rf publish
mkdir publish

VERSION=$(grep -o '<Version>[^<]*</Version>' ../../build/props/Base.props | sed -E 's/<\/?Version>//g')
#Create Info.plist
./CreateInfoPlist.sh $VERSION

#arm64
rm -rf source
mkdir -p "source/OneWare Studio.app/Contents"
cp -r Contents "source/OneWare Studio.app"
mkdir "OneWare Studio.app/Contents/MacOS"
dotnet publish -c Release -f net10.0 -r osx-arm64 --self-contained ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj -o "source/OneWare Studio.app/Contents/MacOS"

PACKAGENAME="publish/OneWareStudio-arm64.dmg"

create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWare Studio.app" 200 190 \
  --hide-extension "OneWare Studio.app" \
  --app-drop-link 600 185 \
  $PACKAGENAME \
  "source/"

#x64
rm -rf source
mkdir -p "source/OneWare Studio.app/Contents"
cp -r Contents "source/OneWare Studio.app"
mkdir "OneWare Studio.app/Contents/MacOS"
dotnet publish -c Release -f net10.0 -r osx-x64 --self-contained ../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj -o "source/OneWare Studio.app/Contents/MacOS"

PACKAGENAME="publish/OneWareStudio-x64.dmg"

#codesign -f -s "Developer ID Application: hmennen@protop-solutions.com" -v "source/OneWare Studio.app"

create-dmg \
  --volname "Install OneWare Studio" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "OneWare Studio.app" 200 190 \
  --hide-extension "OneWare Studio.app" \
  --app-drop-link 600 185 \
  $PACKAGENAME \
  "source/"
