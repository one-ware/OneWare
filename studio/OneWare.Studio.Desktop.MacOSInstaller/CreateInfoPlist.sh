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
</dict>
</plist>
EOF

echo "info.plist got created"
