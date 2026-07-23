#!/bin/bash
set -euo pipefail

deployment_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pid_file="$deployment_root/api.pid"

if [[ ! -s "$pid_file" ]]; then
    exit 0
fi

api_pid="$(cat "$pid_file")"
if kill -0 "$api_pid" 2>/dev/null; then
    kill "$api_pid"
fi
rm -f "$pid_file"
