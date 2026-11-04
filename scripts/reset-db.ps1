# Restores the documented seed dataset (docs/SEED_DATA.md), with dates re-anchored to "now".
#   ./scripts/reset-db.ps1           -> reset via local source (dotnet run -- --reset-db)
#   ./scripts/reset-db.ps1 docker    -> reset the database used by the docker compose stack
param([string]$Target = "local")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ($Target -eq "docker") {
    docker compose -f "$root/docker-compose.yml" exec api dotnet Northline.Api.dll --reset-db
} else {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    Push-Location "$root/backend/src/Northline.Api"
    try { dotnet run --no-launch-profile -- --reset-db } finally { Pop-Location }
}
