#!/usr/bin/env bash
# Restores the documented seed dataset (docs/SEED_DATA.md), with dates re-anchored to "now".
#   ./scripts/reset-db.sh           -> reset via local source (dotnet run -- --reset-db)
#   ./scripts/reset-db.sh docker    -> reset the database used by the docker compose stack
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [[ "${1:-}" == "docker" ]]; then
  docker compose -f "$ROOT/docker-compose.yml" exec api dotnet Northline.Api.dll --reset-db
else
  cd "$ROOT/backend/src/Northline.Api"
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile -- --reset-db
fi
