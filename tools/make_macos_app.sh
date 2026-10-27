#!/usr/bin/env bash
# Usage: make_macos_app.sh <publish dir> <output .app path> [version]
set -euo pipefail
SRC="$1"
APP="$2"
VERSION="${3:-1.0.1}"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$SRC"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/wCalculator"

ICON_SRC="$(dirname "$0")/../src/Calculator.App/Assets/icon.png"
if command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1 && [ -f "$ICON_SRC" ]; then
  SET="$(mktemp -d)/AppIcon.iconset"
  mkdir -p "$SET"
  for s in 16 32 64 128 256 512; do
    sips -z $s $s "$ICON_SRC" --out "$SET/icon_${s}x${s}.png" >/dev/null
  done
  cp "$SET/icon_32x32.png"   "$SET/icon_16x16@2x.png"
  cp "$SET/icon_64x64.png"   "$SET/icon_32x32@2x.png"
  cp "$SET/icon_256x256.png" "$SET/icon_128x128@2x.png"
  cp "$SET/icon_512x512.png" "$SET/icon_256x256@2x.png"
  rm -f "$SET/icon_64x64.png"
  iconutil -c icns "$SET" -o "$APP/Contents/Resources/AppIcon.icns" || true
fi
if [ ! -f "$APP/Contents/Resources/AppIcon.icns" ] && [ -f "$ICON_SRC" ] && command -v python3 >/dev/null 2>&1; then
  # Not on a Mac: Pillow can write the .icns file (skipped when Pillow is missing).
  python3 - "$ICON_SRC" "$APP/Contents/Resources/AppIcon.icns" <<'PY' || true
import sys
from PIL import Image
Image.open(sys.argv[1]).convert("RGBA").resize((512, 512)).save(sys.argv[2], format="ICNS")
PY
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>wCalculator</string>
  <key>CFBundleDisplayName</key><string>wCalculator</string>
  <key>CFBundleIdentifier</key><string>com.hoferlukaslh.wcalculator</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key><string>wCalculator</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>10.15</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
echo "Created $APP"
