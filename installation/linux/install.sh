#!/usr/bin/env bash
set -e

# ---------------------------------------------------------------
# KamiYomu Linux Service Installer
#
# IMPORTANT — READ BEFORE RUNNING:
#
# 1. Run this script from the SAME folder where you downloaded:
#
#        kamiyomu-x.x.x-linux-x64.tar.gz
#
# 2. The installer extracts the application into:
#
#        /opt/KamiYomu
#
# 3. After installation, the systemd service will start and the
#    application will be available at:
#
#        http://localhost:8080
#
# 4. To install:
#       chmod +x install.sh
#       sudo ./install.sh
#
# The installer will:
#    • Extract the application files
#    • Register a systemd service
#    • Configure automatic restart on failure
#    • Start the service immediately
# ---------------------------------------------------------------

SERVICE_NAME="kamiyomu"
DISPLAY_NAME="KamiYomu Service"
DESCRIPTION="KamiYomu background service"
INSTALL_DIR="/opt/KamiYomu"
PACKAGE="kamiyomu-x.x.x-linux-x64.tar.gz"   # Update this to match your actual file

echo "Installing $DISPLAY_NAME ..."
echo

# Must run as root
if [[ $EUID -ne 0 ]]; then
    echo "ERROR: This installer must be run with sudo or as root."
    exit 1
fi

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
if [[ ! -f "$PACKAGE" ]]; then
    echo "ERROR: Package '$PACKAGE' not found in current directory."
    exit 1
fi

echo "Extracting package $PACKAGE ..."
tar -xzf "$PACKAGE" -C "$INSTALL_DIR"

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

echo
echo "Installation complete!"
echo "Service '$SERVICE_NAME' is now running."
echo "Visit: http://localhost:8080"
