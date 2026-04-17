#!/usr/bin/env bash
set -euo pipefail

# Script goi tu keepalived notify hook.
# Example:
#   notify "/etc/keepalived/failover_event_logger.sh NORTH"

REGION="${1:-UNKNOWN}"
EVENT="${2:-unknown}"
STATE="${3:-unknown}"
LOG_FILE="${LOG_FILE:-/var/log/db-failover-events.log}"

echo "$(date '+%Y-%m-%d %H:%M:%S') [${REGION}] EVENT=${EVENT} STATE=${STATE}" >> "${LOG_FILE}"
