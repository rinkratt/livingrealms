#!/bin/bash
set -euo pipefail

deployment_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
set -a
source "$deployment_root/.env"
set +a

"$deployment_root/efbundle" --connection "$ConnectionStrings__GameDatabase"
