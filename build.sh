#!/usr/bin/env bash
# Builds the installers of wCalculator into ./distribution as  NomApplication_version.ext
#
#   ./build.sh                 every target
#   ./build.sh linux windows   only the listed targets (linux | appimage | deb | arch | windows | android | macos)
#
#   Linux    wCalculator_<version>.flatpak   needs: dotnet, flatpak, flatpak-builder
#   Linux    wCalculator_<version>.AppImage  needs: dotnet, appimagetool (downloaded into build/ when missing; curl)
#   Debian   wCalculator_<version>.deb      needs: dotnet, ar, tar, xz
#   Arch     wCalculator_<version>.pkg.tar.zst  needs: dotnet, tar, zstd
#   Windows  wCalculator_<version>.exe       needs: dotnet, makensis (NSIS)
#   Android  wCalculator_<version>.apk       needs: dotnet + the android workload, a JDK
#   macOS    wCalculator_<version>.zip       needs: dotnet, zip (a .app bundle; MAC_ARCH=x64 for Intel -> _x64.zip)
#
# The version is the <Version> of Directory.Build.props.
#
# Environment: NATIVE_AOT=0 (no Native AOT: Linux and macOS builds made on that system are native by default), READY_TO_RUN=0 (no ReadyToRun on the desktop builds), READY_TO_RUN_COMPOSITE=0 (plain, smaller ReadyToRun), ANDROID_AOT=0 (no AOT on Android).
# To measure the startup of the Linux build: ./bench-startup.sh
set -euo pipefail

cd "$(dirname "$0")"
ROOT="$PWD"
APP_NAME="wCalculator"
APP_ID="com.hoferlukaslh.wcalculator"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
[ -n "$VERSION" ] || { echo "Version not found in Directory.Build.props" >&2; exit 1; }
IFS=. read -r V_MAJOR V_MINOR V_PATCH <<<"$VERSION"
VERSION_CODE=$(( V_MAJOR * 10000 + V_MINOR * 100 + ${V_PATCH:-0} ))

DIST="$ROOT/distribution"
WORK="$ROOT/build"
FLATPAK_RUNTIME_VERSION="${FLATPAK_RUNTIME_VERSION:-25.08}"
MAC_ARCH="${MAC_ARCH:-arm64}"

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "  missing tool: $1 ($2)" >&2; return 1; }
}

# Not a single file: the installers copy the whole folder.
# ReadyToRun (code precompiled to native code) makes the application start much faster. The default is the "composite"
# form: the framework and the application are precompiled into one image (measured with bench-startup.sh: 321 ms to a
# usable window instead of 445 ms for plain ReadyToRun and 866 ms without; the installers are bigger).
#   READY_TO_RUN_COMPOSITE=0   plain ReadyToRun (smaller)
#   READY_TO_RUN=0             no ReadyToRun at all
# If a publish fails (compiler pack not downloadable...), the script retries with the next simpler form.
# The runtime identifier of this machine, the only one Native AOT can build for (empty when unknown).
host_rid() {
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) echo linux-x64 ;;
    Linux-aarch64) echo linux-arm64 ;;
    Darwin-arm64) echo osx-arm64 ;;
    Darwin-x86_64) echo osx-x64 ;;
    *) echo "" ;;
  esac
}

# Native AOT: one native executable, no runtime to start (the fastest start-up). Only for the system this script runs
# on, and it needs a C compiler and linker (clang) with the zlib headers. NATIVE_AOT=0 turns it off.
publish_native_aot() { # <rid> <output folder>
  local rid="$1" out="$2"
  [ "${NATIVE_AOT:-1}" = "1" ] || return 1
  [ "$rid" = "$(host_rid)" ] || return 1
  if ! command -v clang >/dev/null 2>&1; then
    echo "  Native AOT needs clang and zlib (Arch: sudo pacman -S clang zlib; Debian: sudo apt install clang zlib1g-dev): using ReadyToRun" >&2
    return 1
  fi
  rm -rf "${out:?}"
  dotnet publish src/Calculator.Desktop -c Release -r "$rid" --self-contained true -p:PublishAot=true -p:PublishReadyToRun=false \
    -p:DebugType=None -p:DebugSymbols=false -o "$out" || return 1
  # Debug symbols of the native program are useless to the user.
  find "$out" -maxdepth 1 \( -name '*.dbg' -o -name '*.pdb' -o -name '*.dSYM' \) -exec rm -rf {} +
  [ -x "$out/$APP_NAME" ] || return 1
}

# The targets only copy the published folder, so a folder published during this run is not built again (Native AOT
# takes a minute or more, and four Linux packages use the same one).
declare -A PUBLISHED=()

publish_desktop() { # <rid> <output folder>
  if [ -n "${PUBLISHED["$1:$2"]:-}" ] && [ -d "$2" ]; then
    return 0
  fi
  publish_desktop_once "$@" && PUBLISHED["$1:$2"]=1
}

publish_desktop_once() { # <rid> <output folder>
  local common=(-c Release -r "$1" --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "$2")
  if publish_native_aot "$1" "$2"; then
    return 0
  fi
  if [ "${NATIVE_AOT:-1}" = "1" ] && [ "$1" = "$(host_rid)" ]; then
    echo "  Native AOT publish failed for $1: retrying with ReadyToRun (read the warnings and errors above)" >&2
  fi
  rm -rf "${2:?}"
  if [ "${READY_TO_RUN:-1}" = "1" ]; then
    if [ "${READY_TO_RUN_COMPOSITE:-1}" = "1" ]; then
      if dotnet publish src/Calculator.Desktop "${common[@]}" -p:PublishReadyToRun=true -p:PublishReadyToRunComposite=true; then
        return 0
      fi
      echo "  Composite ReadyToRun publish failed for $1: retrying with plain ReadyToRun" >&2
      rm -rf "${2:?}"
    fi
    if dotnet publish src/Calculator.Desktop "${common[@]}" -p:PublishReadyToRun=true -p:PublishReadyToRunComposite=false; then
      return 0
    fi
    echo "  ReadyToRun publish failed for $1: retrying without it (the application will start more slowly)" >&2
    rm -rf "${2:?}"
  fi
  dotnet publish src/Calculator.Desktop "${common[@]}" -p:PublishReadyToRun=false
}

# ---------------------------------------------------------------- Linux (Flatpak)
build_linux() {
  need dotnet "the .NET 10 SDK" && need flatpak "package flatpak" && need flatpak-builder "package flatpak-builder" || return 1

  local pub="$WORK/publish/linux-x64" stage="$WORK/flatpak"
  publish_desktop linux-x64 "$pub"

  rm -rf "${stage:?}"
  mkdir -p "$stage"
  cp -a "$pub" "$stage/app"
  cp "packaging/linux/$APP_ID.desktop" "$stage/"
  cp packaging/linux/icon.png "$stage/icon.png"
  sed -e "s/@VERSION@/$VERSION/" -e "s/@DATE@/$(date +%F)/" "packaging/linux/$APP_ID.metainfo.xml" > "$stage/$APP_ID.metainfo.xml"

  cat > "$stage/$APP_ID.yml" <<YAML
app-id: $APP_ID
runtime: org.freedesktop.Platform
runtime-version: '$FLATPAK_RUNTIME_VERSION'
sdk: org.freedesktop.Sdk
command: $APP_NAME
finish-args:
  - --share=ipc
  - --share=network
  - --socket=x11
  - --device=dri
modules:
  - name: $APP_NAME
    buildsystem: simple
    build-commands:
      - mkdir -p /app/lib/$APP_NAME /app/bin
      - cp -a app/. /app/lib/$APP_NAME/
      - ln -s ../lib/$APP_NAME/$APP_NAME /app/bin/$APP_NAME
      - install -Dm644 $APP_ID.desktop /app/share/applications/$APP_ID.desktop
      - install -Dm644 $APP_ID.metainfo.xml /app/share/metainfo/$APP_ID.metainfo.xml
      - install -Dm644 icon.png /app/share/icons/hicolor/512x512/apps/$APP_ID.png
    sources:
      - type: dir
        path: app
        dest: app
      - type: file
        path: $APP_ID.desktop
      - type: file
        path: $APP_ID.metainfo.xml
      - type: file
        path: icon.png
YAML

  (
    cd "$stage"
    flatpak-builder --user --force-clean --disable-rofiles-fuse \
      --install-deps-from=flathub --repo=repo builddir "$APP_ID.yml"
    flatpak build-bundle repo "$DIST/${APP_NAME}_${VERSION}.flatpak" "$APP_ID"
  )
  echo "  -> distribution/${APP_NAME}_${VERSION}.flatpak"
}

# ---------------------------------------------------------------- Linux (AppImage)
build_appimage() {
  need dotnet "the .NET 10 SDK" || return 1

  local pub="$WORK/publish/linux-x64" appdir="$WORK/appimage/$APP_NAME.AppDir" tool
  publish_desktop linux-x64 "$pub"

  tool="$(command -v appimagetool || true)"
  if [ -z "$tool" ]; then
    need curl "package curl, or install appimagetool yourself" || return 1
    tool="$WORK/appimagetool-x86_64.AppImage"
    if [ ! -x "$tool" ]; then
      echo "  downloading appimagetool"
      curl -fL -o "$tool" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
      chmod +x "$tool"
    fi
  fi

  rm -rf "${appdir:?}"
  mkdir -p "$appdir/usr/lib" "$appdir/usr/share/metainfo"
  cp -a "$pub" "$appdir/usr/lib/$APP_NAME"
  cp "packaging/linux/$APP_ID.desktop" "$appdir/$APP_ID.desktop"
  cp packaging/linux/icon.png "$appdir/$APP_ID.png"
  cp packaging/linux/icon.png "$appdir/.DirIcon"
  sed -e "s/@VERSION@/$VERSION/" -e "s/@DATE@/$(date +%F)/" "packaging/linux/$APP_ID.metainfo.xml" > "$appdir/usr/share/metainfo/$APP_ID.metainfo.xml"

  cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
# .NET needs ICU for the culture data; without it on this system, fall back to the invariant culture.
if ! ldconfig -p 2>/dev/null | grep -q 'libicuuc'; then
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
fi
exec "$HERE/usr/lib/wCalculator/wCalculator" "$@"
APPRUN
  chmod +x "$appdir/AppRun"

  # APPIMAGE_EXTRACT_AND_RUN lets the downloaded appimagetool run without FUSE.
  ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" "$appdir" "$DIST/${APP_NAME}_${VERSION}.AppImage"
  echo "  -> distribution/${APP_NAME}_${VERSION}.AppImage"
}

# ---------------------------------------------------------------- Linux packages (.deb, Arch .pkg.tar.zst)
MAINTAINER="${MAINTAINER:-wCalculator <wcalculator@localhost>}"
PKG_DESCRIPTION="Calculator with converters, date calculation and currencies"

# Lays out the files of the package in <root>: the application in /usr/lib/wcalculator, a launcher in /usr/bin,
# the menu entry, the icon and the AppStream data.
stage_linux_root() { # <published folder> <root>
  local pub="$1" root="$2"
  rm -rf "${root:?}"
  mkdir -p "$root/usr/lib" "$root/usr/bin" "$root/usr/share/applications" \
    "$root/usr/share/icons/hicolor/512x512/apps" "$root/usr/share/metainfo"
  cp -a "$pub" "$root/usr/lib/wcalculator"
  # Directories 755, files 644, programs keep their execute bit (whatever the umask of the machine).
  find "$root" -type d -exec chmod 755 {} +
  find "$root" -type f ! -perm /111 -exec chmod 644 {} +
  find "$root" -type f -perm /111 -exec chmod 755 {} +

  cat > "$root/usr/bin/$APP_NAME" <<'LAUNCHER'
#!/bin/sh
# .NET needs ICU for the culture data; without it on this system, fall back to the invariant culture.
if ! ldconfig -p 2>/dev/null | grep -q 'libicuuc'; then
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
fi
exec /usr/lib/wcalculator/wCalculator "$@"
LAUNCHER
  chmod 755 "$root/usr/bin/$APP_NAME"

  install -m 644 "packaging/linux/$APP_ID.desktop" "$root/usr/share/applications/$APP_ID.desktop"
  install -m 644 packaging/linux/icon.png "$root/usr/share/icons/hicolor/512x512/apps/$APP_ID.png"
  sed -e "s/@VERSION@/$VERSION/" -e "s/@DATE@/$(date +%F)/" "packaging/linux/$APP_ID.metainfo.xml" \
    > "$root/usr/share/metainfo/$APP_ID.metainfo.xml"
  chmod 644 "$root/usr/share/metainfo/$APP_ID.metainfo.xml"
}

build_deb() {
  need dotnet "the .NET 10 SDK" && need ar "package binutils" && need tar "package tar" && need xz "package xz" || return 1

  local pub="$WORK/publish/linux-x64" dir="$WORK/deb" root size
  root="$dir/root"
  publish_desktop linux-x64 "$pub"
  rm -rf "${dir:?}"
  mkdir -p "$dir/control"
  stage_linux_root "$pub" "$root"

  size="$(du -sk "$root" | cut -f1)"
  cat > "$dir/control/control" <<CONTROL
Package: wcalculator
Version: $VERSION
Architecture: amd64
Maintainer: $MAINTAINER
Installed-Size: $size
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libfontconfig1, libx11-6, libice6, libsm6, libssl3 | libssl3t64
Recommends: libicu76 | libicu74 | libicu72 | libicu70 | libicu67
Section: utils
Priority: optional
Description: $PKG_DESCRIPTION
 Standard, scientific and programmer calculator, date calculation and
 unit and currency converters. A port of the Windows Calculator to Avalonia UI.
CONTROL
  (cd "$root" && find . -type f ! -path './DEBIAN/*' -printf '%P\0' | sort -z | xargs -0 md5sum) > "$dir/control/md5sums"

  printf '2.0\n' > "$dir/debian-binary"
  tar --owner=0 --group=0 --numeric-owner -C "$dir/control" -cJf "$dir/control.tar.xz" .
  tar --owner=0 --group=0 --numeric-owner -C "$root" -cJf "$dir/data.tar.xz" .
  rm -f "${DIST:?}/${APP_NAME}_${VERSION}.deb"
  # The order of the members matters: debian-binary, control, data.
  (cd "$dir" && ar rcD "$DIST/${APP_NAME}_${VERSION}.deb" debian-binary control.tar.xz data.tar.xz)
  echo "  -> distribution/${APP_NAME}_${VERSION}.deb"
}

build_arch() {
  need dotnet "the .NET 10 SDK" && need tar "package tar" && need zstd "package zstd" || return 1

  local pub="$WORK/publish/linux-x64" dir="$WORK/arch" root size
  root="$dir/root"
  publish_desktop linux-x64 "$pub"
  rm -rf "${dir:?}"
  mkdir -p "$dir"
  stage_linux_root "$pub" "$root"

  size="$(du -sb "$root" | cut -f1)"
  cat > "$root/.PKGINFO" <<PKGINFO
pkgname = wcalculator
pkgbase = wcalculator
pkgver = $VERSION-1
pkgdesc = $PKG_DESCRIPTION
url = https://github.com/microsoft/calculator
builddate = $(date +%s)
packager = $MAINTAINER
size = $size
arch = x86_64
license = MIT
depend = glibc
depend = gcc-libs
depend = zlib
depend = icu
depend = openssl
depend = fontconfig
depend = libx11
depend = libice
depend = libsm
PKGINFO
  chmod 644 "$root/.PKGINFO"

  rm -f "${DIST:?}/${APP_NAME}_${VERSION}.pkg.tar.zst"
  # .PKGINFO first, then the files (pacman reads the package metadata from the start of the archive).
  tar --owner=0 --group=0 --numeric-owner -C "$root" -cf - .PKGINFO $(cd "$root" && ls -A | grep -v '^\.PKGINFO$') \
    | zstd -19 -T0 -q -o "$DIST/${APP_NAME}_${VERSION}.pkg.tar.zst"
  echo "  -> distribution/${APP_NAME}_${VERSION}.pkg.tar.zst"
}

# ---------------------------------------------------------------- Windows (NSIS)
build_windows() {
  need dotnet "the .NET 10 SDK" && need makensis "package nsis" || return 1

  local pub="$WORK/publish/win-x64"
  publish_desktop win-x64 "$pub"
  makensis -V2 \
    -DVERSION="$VERSION" \
    -DSRCDIR="$pub" \
    -DICON="$ROOT/src/Calculator.App/Assets/icon.ico" \
    -DOUTFILE="$DIST/${APP_NAME}_${VERSION}.exe" \
    packaging/windows/installer.nsi
  echo "  -> distribution/${APP_NAME}_${VERSION}.exe"
}

# ---------------------------------------------------------------- Android (APK)
build_android() {
  need dotnet "the .NET 10 SDK and 'dotnet workload install android'" || return 1

  # Full AOT compiles the whole application on the computer instead of on the phone: the fastest start-up, a bigger APK
  # (the SDK also trims the framework for it). ANDROID_AOT=0 turns it off, and the build is retried without it when the
  # AOT compiler of the workload is missing.
  local out="$WORK/android" apk
  local args=(-c Release -f net10.0-android -p:AndroidPackageFormat=apk
    -p:ApplicationDisplayVersion="$VERSION" -p:ApplicationVersion="$VERSION_CODE" -o "$out")
  rm -rf "${out:?}"
  if [ "${ANDROID_AOT:-1}" = "1" ] && dotnet publish src/Calculator.Android "${args[@]}" -p:AndroidAot=true; then
    :
  else
    [ "${ANDROID_AOT:-1}" = "1" ] && echo "  AOT build failed: retrying without it (slower startup on the phone)" >&2
    rm -rf "${out:?}"
    dotnet publish src/Calculator.Android "${args[@]}" -p:AndroidAot=false
  fi

  # The SDK signs the package with its debug key (no keystore of our own): that is what lets the APK install.
  apk="$(find "$out" -maxdepth 1 -name '*-Signed.apk' | head -n 1)"
  [ -n "$apk" ] || apk="$(find "$out" -maxdepth 1 -name '*.apk' | head -n 1)"
  [ -n "$apk" ] || { echo "  no APK produced in $out" >&2; return 1; }
  cp "$apk" "$DIST/${APP_NAME}_${VERSION}.apk"
  echo "  -> distribution/${APP_NAME}_${VERSION}.apk"
}

# ---------------------------------------------------------------- macOS (.app in a zip)
build_macos() {
  need dotnet "the .NET 10 SDK" && need zip "package zip" || return 1

  local rid="osx-$MAC_ARCH" suffix="" app="$WORK/macos/$APP_NAME.app" zipfile
  [ "$MAC_ARCH" = "arm64" ] || suffix="_$MAC_ARCH"
  zipfile="$DIST/${APP_NAME}_${VERSION}${suffix}.zip"
  publish_desktop "$rid" "$WORK/publish/$rid"
  mkdir -p "$WORK/macos"
  bash tools/make_macos_app.sh "$WORK/publish/$rid" "$app" "$VERSION"
  rm -f "${zipfile:?}"
  (cd "$WORK/macos" && zip -qry -X "$zipfile" "$APP_NAME.app")
  echo "  -> distribution/${APP_NAME}_${VERSION}${suffix}.zip"
}

# ---------------------------------------------------------------- main
targets=("$@")
[ ${#targets[@]} -gt 0 ] || targets=(linux appimage deb arch windows android macos)

mkdir -p "$DIST" "$WORK"
failed=()
for t in "${targets[@]}"; do
  case "$t" in
    linux|appimage|deb|arch|windows|android|macos) ;;
    *) echo "Unknown target: $t (linux | appimage | deb | arch | windows | android | macos)" >&2; exit 2 ;;
  esac
  echo "==> $t ($APP_NAME $VERSION)"
  # A subshell with its own "set -e": the first failing command stops this target, the next one still runs.
  # (Not tested with "if": set -e would be ignored inside the function.)
  set +e
  ( set -e; "build_$t" )
  rc=$?
  set -e
  if [ "$rc" -ne 0 ]; then
    failed+=("$t")
    echo "!! $t failed" >&2
  fi
done

echo
echo "Files in $DIST:"
ls -1 "$DIST"
if [ ${#failed[@]} -gt 0 ]; then
  echo "Failed: ${failed[*]}" >&2
  exit 1
fi
