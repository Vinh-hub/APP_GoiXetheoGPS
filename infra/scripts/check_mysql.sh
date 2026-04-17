#!/usr/bin/env bash
set -euo pipefail

# Doi lai env cho dung may chu.
# export MYSQL_PWD="your_root_password"
MYSQL_USER="${MYSQL_USER:-root}"
MYSQL_HOST="${MYSQL_HOST:-127.0.0.1}"

mysqladmin ping -h "${MYSQL_HOST}" -u "${MYSQL_USER}" >/dev/null 2>&1
