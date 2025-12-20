#!/bin/bash
set -e

# --------------------------
# Parameters (default values)
# --------------------------
REPO="kamiyomu/kamiyomu"        # GitHub repo
VERSION_TAG="${VERSION_TAG:-v1.0.12}"  # Release tag (can be overridden by env)
ARCH="${ARCH:-linux-x64}"      # Artifact architecture
INSTALL_DIR="${INSTALL_DIR:-/opt/kamiyomu}"  # Installation folder
SERVICE_NAME="${SERVICE_NAME:-kamiyomu}"     # systemd service name
EXECUTABLE="${EXECUTABLE:-KamiYomu}"        # Executable name
PORT="${PORT:-8080}"           # Default HTTP port
WORK_COUNT="${WORK_COUNT:-4}"    # Default worker count
MAX_CONCURRENT_CRAWLER_INSTANCES="${MAX_CONCURRENT_CRAWLER_INSTANCES:-2}" # Default max concurrent jobs
MAX_WAIT_PERIOD_IN_MILLISECONDS="${MAX_WAIT_PERIOD_IN_MILLISECONDS:-7001}" # Default max wait time
MAX_RETRY_ATTEMPTS="${MAX_RETRY_ATTEMPTS:-10}" # Default max retry attempts

# --------------------------
# Ensure running as root
# --------------------------
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root (sudo)"
   exit 1
fi

# --------------------------
# Download GitHub release artifact
# --------------------------
echo "Fetching release $VERSION_TAG for $ARCH from $REPO ..."
API_URL="https://api.github.com/repos/$REPO/releases/tags/$VERSION_TAG"

ASSET_URL=$(curl -s $API_URL | jq -r ".assets[] | select(.name | test(\"$ARCH\")) | .browser_download_url")

if [[ -z "$ASSET_URL" ]]; then
    echo "Error: Artifact for architecture '$ARCH' not found in release $VERSION_TAG!"
    exit 1
fi

TMP_DIR=$(mktemp -d)
ZIP_FILE="$TMP_DIR/kamiyomu-$ARCH.zip"

echo "Downloading artifact..."
curl -L -o "$ZIP_FILE" "$ASSET_URL"

# --------------------------
# Extract artifact
# --------------------------
mkdir -p "$INSTALL_DIR"
echo "Extracting artifact to $INSTALL_DIR ..."
unzip -o "$ZIP_FILE" -d "$INSTALL_DIR"

# Ensure executable has permissions
chmod +x "$INSTALL_DIR/$EXECUTABLE"

# --------------------------
# Create systemd service
# --------------------------
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

echo "Creating systemd service $SERVICE_FILE ..."
cat <<EOF > "$SERVICE_FILE"
[Unit]
Description=Kamiyomu Service
After=network.target

[Service]
Type=simple
WorkingDirectory=$INSTALL_DIR
Environment="Kestrel__Endpoints__Http__Url=http://*:$PORT"
Environment="Worker__WorkerCount=$WORK_COUNT"
Environment="Worker__MaxConcurrentCrawlerInstances=$MAX_CONCURRENT_CRAWLER_INSTANCES"
Environment="Worker__MaxWaitPeriodInMilliseconds=$MAX_CONCURRENT_CRAWLER_INSTANCES"
Environment="Worker__MaxRetryAttempts=MAX_RETRY_ATTEMPTS"
ExecStart=$INSTALL_DIR/$EXECUTABLE
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

# --------------------------
# Enable and start service
# --------------------------
echo "Reloading systemd daemon..."
systemctl daemon-reload

echo "Enabling and starting service $SERVICE_NAME ..."
systemctl enable "$SERVICE_NAME"
systemctl start "$SERVICE_NAME"

echo "Kamiyomu installed successfully! Service '$SERVICE_NAME' is running on port $PORT."
