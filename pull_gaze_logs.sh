#!/bin/zsh

set -euo pipefail

LOCAL_LOG_DIR="./GazeLogs"
REMOTE_FIND_CMD="find /sdcard /storage/emulated/0 /data/local/tmp -type f -name '*.csv' 2>/dev/null | sort"

mkdir -p "${LOCAL_LOG_DIR}"

echo "Checking device connection..."
adb get-state >/dev/null

echo "Finding CSV logs on headset..."
REMOTE_CSV_FILES=$(adb shell "${REMOTE_FIND_CMD}" | tr -d '\r')

if [[ -z "${REMOTE_CSV_FILES}" ]]; then
  echo "No CSV files found on device."
  exit 0
fi

echo "Found CSV files:"
echo "${REMOTE_CSV_FILES}"

echo "Pulling CSV files to ${LOCAL_LOG_DIR}"
while IFS= read -r remote_file; do
  [[ -z "${remote_file}" ]] && continue
  adb pull "${remote_file}" "${LOCAL_LOG_DIR}/"
done <<< "${REMOTE_CSV_FILES}"

echo "Done. CSV logs copied to ${LOCAL_LOG_DIR}"
