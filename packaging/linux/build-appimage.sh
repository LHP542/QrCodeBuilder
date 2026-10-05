#!/usr/bin/env bash
# Baut das AppImage aus einem fertigen linux-x64-Publish-Ordner (Kroste-Standard).
# Aufruf: packaging/linux/build-appimage.sh <version> <publish-dir>
# Hinweis: --appimage-extract-and-run ist nötig, weil im CI kein FUSE verfügbar ist.
set -euo pipefail

VERSION="$1"
PUBLISH_DIR="$2"
APPDIR="AppDir"

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
cp -r "$PUBLISH_DIR"/* "$APPDIR/usr/bin/"
# Kleingeschrieben, weil die Dateien so heißen — auf Linux ist das nicht egal.
# Unter Windows lief dasselbe Skript jahrelang mit 'QrCodeBuilder.desktop' durch.
cp packaging/linux/qrcodebuilder.desktop "$APPDIR/"
cp QrCodeBuilder/Assets/qrcodebuilder.png "$APPDIR/"
cp packaging/linux/AppRun "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/QrCodeBuilder"

curl -sSL -o appimagetool \
  https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
chmod +x appimagetool
./appimagetool --appimage-extract-and-run "$APPDIR" "QrCodeBuilder-${VERSION}-x86_64.AppImage"
echo "AppImage gebaut: QrCodeBuilder-${VERSION}-x86_64.AppImage"
