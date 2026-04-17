#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   MYSQL_PWD=... ./monitor_replication.sh NORTH
#   MYSQL_PWD=... ./monitor_replication.sh SOUTH

REGION="${1:-UNKNOWN}"
MYSQL_USER="${MYSQL_USER:-root}"
MYSQL_HOST="${MYSQL_HOST:-127.0.0.1}"
LOG_FILE="${LOG_FILE:-/var/log/db-repl-monitor.log}"
ALERT_FILE="${ALERT_FILE:-/var/log/db-repl-alert.log}"

ts() {
  date '+%Y-%m-%d %H:%M:%S'
}

status="$(mysql -h "${MYSQL_HOST}" -u "${MYSQL_USER}" -Nse "SHOW SLAVE STATUS\\G" 2>/dev/null || true)"

if [[ -z "${status}" ]]; then
  echo "$(ts) [${REGION}] [ERROR] Slave status unavailable" | tee -a "${LOG_FILE}" "${ALERT_FILE}"
  exit 1
fi

io_running="$(awk -F': ' '/Slave_IO_Running/ {print $2}' <<< "${status}" | head -n1)"
sql_running="$(awk -F': ' '/Slave_SQL_Running/ {print $2}' <<< "${status}" | head -n1)"
lag="$(awk -F': ' '/Seconds_Behind_Master/ {print $2}' <<< "${status}" | head -n1)"

if [[ "${io_running}" != "Yes" || "${sql_running}" != "Yes" ]]; then
  echo "$(ts) [${REGION}] [ERROR] Replication broken (IO=${io_running}, SQL=${sql_running})" | tee -a "${LOG_FILE}" "${ALERT_FILE}"
  exit 2
fi

if [[ "${lag}" == "NULL" ]]; then
  echo "$(ts) [${REGION}] [WARN] Lag is NULL" | tee -a "${LOG_FILE}" "${ALERT_FILE}"
  exit 3
fi

if [[ "${lag}" =~ ^[0-9]+$ ]] && (( lag > 1 )); then
  echo "$(ts) [${REGION}] [WARN] Replication lag=${lag}s (>1s)" | tee -a "${LOG_FILE}" "${ALERT_FILE}"
  exit 4
fi

echo "$(ts) [${REGION}] [OK] Replication healthy (lag=${lag}s)" | tee -a "${LOG_FILE}"
