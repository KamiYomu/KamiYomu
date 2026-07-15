#!/usr/bin/env bash
set -e

# ---------------------------------------------------------------
# KamiYomu Linux Service Installer (Top 5 Versions + Auto-Download)
#
# Features:
#   • Fetch the 5 most recent releases from GitHub
#   • Latest version is marked as "(recommended)"
#   • If user presses ENTER → latest version is auto-selected
#   • User selects Linux asset ("linux")
#   • Download to /tmp/KamiYomu
#   • Install systemd service
#   • Cleanup downloaded file
# ---------------------------------------------------------------

SERVICE_NAME="kamiyomu"
DISPLAY_NAME="KamiYomu Service"
DESCRIPTION="KamiYomu background service"
INSTALL_DIR="/opt/KamiYomu"
TMP_DIR="/tmp/KamiYomu"

GITHUB_API="https://api.github.com/repos/KamiYomu/KamiYomu/releases"

echo "Installing $DISPLAY_NAME ..."
echo

# Must run as root
if [[ $EUID -ne 0 ]]; then
    echo "ERROR: This installer must be run with sudo or as root."
    exit 1
fi

echo "Fetching releases from GitHub..."
RELEASES=$(curl -s -H "User-Agent: LinuxInstaller" "$GITHUB_API")

# Extract the 5 most recent releases
TAGS=($(echo "$RELEASES" | jq -r '.[].tag_name' | head -n 5))

if [[ ${#TAGS[@]} -eq 0 ]]; then
    echo "ERROR: No releases found."
    exit 1
fi

echo
echo "Most recent 5 versions:"
for i in "${!TAGS[@]}"; do
    if [[ $i -eq 0 ]]; then
        echo "[$i] ${TAGS[$i]}  (recommended)"
    else
        echo "[$i] ${TAGS[$i]}"
    fi
done

echo
read -p "Enter the number of the version you want to install (ENTER = recommended): " VERSION_INDEX

# Auto-select latest if user presses ENTER
if [[ -z "$VERSION_INDEX" ]]; then
    VERSION_INDEX=0
    echo "Using recommended version..."
fi

# Validate selection
if ! [[ "$VERSION_INDEX" =~ ^[0-9]+$ ]] || [[ "$VERSION_INDEX" -ge ${#TAGS[@]} ]]; then
    echo "ERROR: Invalid selection."
    exit 1
fi

VERSION="${TAGS[$VERSION_INDEX]}"
echo "Selected version: $VERSION"

# Extract assets for selected version
ASSETS=$(echo "$RELEASES" | jq -r ".[] | select(.tag_name==\"$VERSION\") | .assets[] | .name + \"|\" + .browser_download_url")

# Filter Linux assets
LINUX_ASSETS=($(echo "$ASSETS" | grep "linux"))

if [[ ${#LINUX_ASSETS[@]} -eq 0 ]]; then
    echo "ERROR: No Linux-compatible assets found for version $VERSION."
    exit 1
fi

echo
echo "Available Linux packages for version $VERSION:"
for i in "${!LINUX_ASSETS[@]}"; do
    NAME=$(echo "${LINUX_ASSETS[$i]}" | cut -d '|' -f 1)
    echo "[$i] $NAME"
done

echo
read -p "Enter the number of the Linux package you want to install: " ASSET_INDEX

if ! [[ "$ASSET_INDEX" =~ ^[0-9]+$ ]] || [[ "$ASSET_INDEX" -ge ${#LINUX_ASSETS[@]} ]]; then
    echo "ERROR: Invalid selection."
    exit 1
fi

PACKAGE_NAME=$(echo "${LINUX_ASSETS[$ASSET_INDEX]}" | cut -d '|' -f 1)
DOWNLOAD_URL=$(echo "${LINUX_ASSETS[$ASSET_INDEX]}" | cut -d '|' -f 2)

echo
echo "Selected package: $PACKAGE_NAME"

# Prepare temp directory
mkdir -p "$TMP_DIR"
TMP_FILE="$TMP_DIR/$PACKAGE_NAME"

echo "Downloading package to $TMP_FILE ..."
curl -L "$DOWNLOAD_URL" -o "$TMP_FILE"

echo "Download complete."

# Stop and remove existing service if present
if systemctl list-units --full -all | grep -q "$SERVICE_NAME.service"; then
    echo "Existing service found. Stopping..."
    systemctl stop "$SERVICE_NAME.service"
    systemctl disable "$SERVICE_NAME.service"
fi

# Create installation directory
echo "Creating installation directory at $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"

# Extract package
echo "Extracting package..."
tar -xzf "$TMP_FILE" -C "$INSTALL_DIR"

# Find the executable
EXECUTABLE=$(find "$INSTALL_DIR" -type f -executable -name "KamiYomu*" | head -n 1)

if [[ -z "$EXECUTABLE" ]]; then
    echo "ERROR: No executable found in extracted package."
    exit 1
fi

echo "Executable detected: $EXECUTABLE"

# Create systemd service file
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

echo "Registering systemd service at $SERVICE_FILE"

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=$DESCRIPTION
After=network.target

[Service]
Type=simple
ExecStart=$EXECUTABLE
Restart=always
RestartSec=5
WorkingDirectory=$INSTALL_DIR

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd
systemctl daemon-reload

# Enable service
systemctl enable "$SERVICE_NAME.service"

# Start service
echo "Starting service..."
systemctl start "$SERVICE_NAME.service"

# Cleanup
echo "Cleaning up downloaded package..."
rm -f "$TMP_FILE"

echo
echo "Installation complete!"
echo "Service '$SERVICE_NAME' is now running."
echo "Visit: http://localhost:8080"
