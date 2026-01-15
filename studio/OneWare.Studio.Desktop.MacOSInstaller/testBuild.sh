#!/usr/bin/env bash
set -e

### CONFIG ###
APP_NAME="OneWare DEV"
EXECUTABLE_NAME="OneWareStudio"

BUNDLE_ID="dev.oneware.app"
SCHEME="oneware"

RUNTIME="osx-arm64"          # change to osx-x64 if needed
CONFIGURATION="Debug"
PROJECT_PATH="../OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj"

BUILD_DIR="$(pwd)/build-macos-dev"
PUBLISH_DIR="$BUILD_DIR/publish"
APP_DIR="$BUILD_DIR/${APP_NAME}.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
PLIST_PATH="$CONTENTS_DIR/Info.plist"

### CLEAN ###
rm -rf "$BUILD_DIR"
mkdir -p "$MACOS_DIR"

dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained \
  -o "$MACOS_DIR"

EXECUTABLE="$MACOS_DIR/$EXECUTABLE_NAME"

if [ ! -f "$EXECUTABLE" ]; then
  echo "Executable not found: $EXECUTABLE"
  exit 1
fi

chmod +x "$EXECUTABLE"

### INFO.PLIST ###
cat > "$PLIST_PATH" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
 "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$EXECUTABLE_NAME</string>

    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>

    <key>CFBundleName</key>
    <string>$APP_NAME</string>

    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>

    <key>CFBundlePackageType</key>
    <string>APPL</string>

    <key>CFBundleVersion</key>
    <string>1</string>

    <key>CFBundleShortVersionString</key>
    <string>1.0</string>

    <key>CFBundleURLTypes</key>
    <array>
        <dict>
            <key>CFBundleURLName</key>
            <string>$SCHEME</string>
            <key>CFBundleURLSchemes</key>
            <array>
                <string>$SCHEME</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
EOF

### REGISTER + OPEN ###
echo "Opening $APP_NAME"
open "$APP_DIR"

echo "Done"
echo "Test with: open ${SCHEME}://test/path"
