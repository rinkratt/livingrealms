#!/bin/bash
set -euo pipefail

deployment_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pid_file="$deployment_root/api.pid"
log_file="$deployment_root/logs/api.log"

mkdir -p "$deployment_root/logs"

if [[ -s "$pid_file" ]]; then
    existing_pid="$(cat "$pid_file")"
    if kill -0 "$existing_pid" 2>/dev/null; then
        exit 0
    fi
    rm -f "$pid_file"
fi

set -a
source "$deployment_root/.env"
set +a

cd "$deployment_root/app"
./LivingRealms.Api >>"$log_file" 2>&1 </dev/null &
api_pid=$!
echo "$api_pid" >"$pid_file"

sleep 2
if ! kill -0 "$api_pid" 2>/dev/null; then
    tail -n 40 "$log_file"
    exit 1
fi
