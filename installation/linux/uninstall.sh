#!/usr/bin/env bash
set -e

# ---------------------------------------------------------------
# KamiYomu Linux Service Uninstaller
#
# This script removes:
#   • The systemd service (kamiyomu.service)
#   • The installation directory (/opt/KamiYomu)
#   • The systemd unit file
#
# Usage:
#   chmod +x uninstall.sh
#   sudo ./uninstall.sh
#
# ---------------------------------------------------------------

SERVICE_NAME="kamiyomu"
INSTALL_DIR="/opt/KamiYomu"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

echo "Uninstalling KamiYomu Service..."
echo

# Must run as root
if [[ $EUID -ne 0 ]]; then
    echo "ERROR: This uninstaller must be run with sudo or as root."
    exit 1
fi

# Stop service if running
if systemctl list-units --full -all | grep -q "${SERVICE_NAME}.service"; then
    echo "Stopping service..."
    systemctl stop "${SERVICE_NAME}.service" || true

    echo "Disabling service..."
    systemctl disable "${SERVICE_NAME}.service" || true
fi

# Remove systemd unit file
if [[ -f "$SERVICE_FILE" ]]; then
    echo "Removing systemd service file at $SERVICE_FILE"
    rm -f "$SERVICE_FILE"
else
    echo "Service file not found, skipping."
fi

# Reload systemd
echo "Reloading systemd..."
systemctl daemon-reload

# Remove installation directory
if [[ -d "$INSTALL_DIR" ]]; then
    echo "Removing installation directory at $INSTALL_DIR"
    rm -rf "$INSTALL_DIR"
else
    echo "Installation directory not found, skipping."
fi

echo
echo "Uninstallation complete!"
echo "KamiYomu has been fully removed from this system."
