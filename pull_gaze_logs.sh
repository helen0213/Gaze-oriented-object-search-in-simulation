#!/bin/zsh

set -euo pipefail

PACKAGE_NAME="com.UnityTechnologies.com.unity.template.urpblank"
DEVICE_LOG_DIR="/storage/emulated/0/Android/data/${PACKAGE_NAME}/files/GazeLogs"
LOCAL_LOG_DIR="./GazeLogs"

mkdir -p "${LOCAL_LOG_DIR}"

echo "Checking device connection..."
adb get-state >/dev/null

echo "Listing headset logs in ${DEVICE_LOG_DIR}"
adb shell ls "${DEVICE_LOG_DIR}"

echo "Pulling logs to ${LOCAL_LOG_DIR}"
adb pull "${DEVICE_LOG_DIR}" "${LOCAL_LOG_DIR}"

echo "Done. Logs copied to ${LOCAL_LOG_DIR}"
