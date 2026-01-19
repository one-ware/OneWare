#!/bin/bash

if [ "$#" -ne 1 ]; then
    echo "Missing Argument: $0 <version>"
    exit 1
fi

version="$1"
plist_file="./Contents/Info.plist"

cat > "$plist_file" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>

    <key>CFBundleExecutable</key>
    <string>OneWareStudio</string>

    <key>CFBundleIconFile</key>
    <string>OneWare.icns</string>

    <key>CFBundleIdentifier</key>
    <string>oneware.onewarestudio</string>

    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>OneWare FPGA Project</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>fpgaproj</string>
            </array>
            <key>CFBundleTypeIconFile</key>
            <string>OneWare.icns</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
        </dict>
        <dict>
            <key>CFBundleTypeName</key>
            <string>OneWare AI File</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>oneai</string>
            </array>
            <key>CFBundleTypeIconFile</key>
            <string>OneWare.icns</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
        </dict>
    </array>

    <key>CFBundleURLTypes</key>
    <array>
        <dict>
            <key>CFBundleURLName</key>
            <string>oneware</string>
            <key>CFBundleURLSchemes</key>
            <array>
                <string>oneware</string>
            </array>
        </dict>
    </array>

    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>

    <key>CFBundleName</key>
    <string>OneWare Studio</string>

    <key>CFBundlePackageType</key>
    <string>APPL</string>

    <key>CFBundleShortVersionString</key>
    <string>$version</string>

    <key>CFBundleVersion</key>
    <string>$version</string>

    <key>NSCameraUsageDescription</key>
    <string>Camera access is required for optional video capture features such as the OneAI Extension.</string>
</dict>
</plist>
EOF

echo "Info.plist got created"