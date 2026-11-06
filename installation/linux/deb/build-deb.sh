#!/usr/bin/env bash
# Builds a .deb for Debian / Ubuntu / Linux Mint from a self-contained linux publish folder.
#
# Usage: build-deb.sh <publish_dir> <tag> <deb_arch: amd64|arm64> <out_dir>
#   publish_dir  output of: dotnet publish KamiYomu.Web -r linux-x64|linux-arm64 --self-contained
#   tag          release tag, e.g. 4.0.0 or 4.0.0-beta1 (becomes 4.0.0~beta1 so pre-releases sort first)
# Output: <out_dir>/kamiyomu_<version>_<arch>.deb
set -euo pipefail

if [[ $# -ne 4 ]]; then
    echo "Usage: $0 <publish_dir> <tag> <amd64|arm64> <out_dir>" >&2
    exit 1
fi

PUBLISH_DIR="$(cd "$1" && pwd)"
TAG="$2"
ARCH="$3"
mkdir -p "$4"
OUT_DIR="$(cd "$4" && pwd)"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TILDE='~'
DEB_VERSION="${TAG//-/$TILDE}"

[[ -f "$PUBLISH_DIR/KamiYomu.Web" ]] || { echo "KamiYomu.Web not found in $PUBLISH_DIR" >&2; exit 1; }

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

install -d "$STAGE/DEBIAN" "$STAGE/opt/KamiYomu" "$STAGE/lib/systemd/system"
cp -a "$PUBLISH_DIR/." "$STAGE/opt/KamiYomu/"
find "$STAGE/opt/KamiYomu" -name '*.pdb' -delete
# Normalize modes: the service runs as the unprivileged "kamiyomu" user and must be able to read/execute everything.
chmod 0755 "$STAGE"
find "$STAGE/opt" -type d -exec chmod 0755 {} +
find "$STAGE/opt" -type f -exec chmod go+r,u+rw {} +
chmod 0755 "$STAGE/opt/KamiYomu/KamiYomu.Web"
install -m 0644 "$HERE/kamiyomu.service" "$STAGE/lib/systemd/system/kamiyomu.service"
install -m 0755 "$HERE/postinst" "$HERE/prerm" "$HERE/postrm" "$STAGE/DEBIAN/"

INSTALLED_SIZE="$(du -sk "$STAGE/opt" | cut -f1)"

# Runtime dependencies: .NET self-contained (ICU, OpenSSL) + libraries needed by the Chromium
# that KamiYomu downloads. "a | b" alternatives cover Ubuntu 24.04 t64 renames.
cat > "$STAGE/DEBIAN/control" <<CONTROL
Package: kamiyomu
Version: ${DEB_VERSION}
Architecture: ${ARCH}
Section: web
Priority: optional
Installed-Size: ${INSTALLED_SIZE}
Maintainer: KamiYomu contributors <noreply@kamiyomu.com>
Homepage: https://kamiyomu.com
Depends: ca-certificates, fontconfig, libfontconfig1, libfreetype6, libharfbuzz0b,
 libicu76 | libicu74 | libicu72 | libicu70 | libicu67,
 libssl3t64 | libssl3 | libssl1.1,
 libnss3, libdrm2, libgbm1, libxkbcommon0, libxcomposite1, libxdamage1, libxfixes3, libxrandr2,
 libx11-6, libx11-xcb1, libxshmfence1, libpango-1.0-0, libcairo2,
 libatk1.0-0t64 | libatk1.0-0, libatk-bridge2.0-0t64 | libatk-bridge2.0-0,
 libcups2t64 | libcups2, libasound2t64 | libasound2, libgtk-3-0t64 | libgtk-3-0,
 libpng16-16t64 | libpng16-16
Description: Self-hosted manga crawler and reader
 KamiYomu scans and downloads manga from supported sources, stores them locally and
 hosts a private reader. Runs as the "kamiyomu" systemd service on http://localhost:8080.
 Data is stored in /var/lib/kamiyomu; it is only removed by "apt purge kamiyomu".
CONTROL

OUT_FILE="$OUT_DIR/kamiyomu_${DEB_VERSION}_${ARCH}.deb"
dpkg-deb --root-owner-group --build "$STAGE" "$OUT_FILE"
echo "Built $OUT_FILE"
