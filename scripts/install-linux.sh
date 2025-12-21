#!/bin/bash
set -e

SERVICE_NAME="kamiyomu"
INSTALL_DIR="/usr/local/bin/kamiyomu"
EXECUTABLE="KamiYomu"

PORT="${PORT:-8080}"
WORK_COUNT="${WORK_COUNT:-4}"
MAX_CONCURRENT_CRAWLER_INSTANCES="${MAX_CONCURRENT_CRAWLER_INSTANCES:-2}"
MAX_WAIT_PERIOD_IN_MILLISECONDS="${MAX_WAIT_PERIOD_IN_MILLISECONDS:-9001}"
MAX_RETRY_ATTEMPTS="${MAX_RETRY_ATTEMPTS:-10}"

SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

echo "Creating systemd service at $SERVICE_FILE..."

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
Environment="Worker__MaxWaitPeriodInMilliseconds=$MAX_WAIT_PERIOD_IN_MILLISECONDS"
Environment="Worker__MaxRetryAttempts=$MAX_RETRY_ATTEMPTS"
ExecStart=$INSTALL_DIR/$EXECUTABLE
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

echo "Reloading systemd..."
systemctl daemon-reload

echo "Enabling service..."
systemctl enable "$SERVICE_NAME"

echo "Starting service..."
systemctl start "$SERVICE_NAME"

echo "Kamiyomu installed and running."