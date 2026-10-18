#!/usr/bin/env pwsh
#requires -version 7
<#
.SYNOPSIS
    Purges the OpenID host developer database on demand.

.DESCRIPTION
    The host dev database is Postgres in a container, orchestrated by the Aspire AppHost (ADR-0073). Its data
    lives in a STABLE named volume (ncode-identity-openiddb-data) so it survives restarts. This script removes that
    volume so the next AppHost run starts from an empty database: the migration worker re-applies the schema and the
    host re-seeds the admin user, SPA client, and developer signing key. Because the data, the seeded signing key, and
    the DataProtection key ring that protects it all live in the one database, this single action resets them together
    with nothing left on the filesystem.

    Stop the Aspire AppHost first — a volume cannot be removed while a container is using it. The container runtime
    (Docker or Podman) is auto-detected, matching Aspire's own behavior, so this works unchanged on either machine.

.EXAMPLE
    ./build/dev-db-reset.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$volume = 'ncode-identity-openiddb-data'

$runtime = @('docker', 'podman') |
    Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } |
    Select-Object -First 1

if (-not $runtime) {
    throw 'No container runtime found on PATH. Install Docker Desktop (or Podman) and try again.'
}

Write-Host "Using container runtime: $runtime" -ForegroundColor Cyan

# Remove any (stopped) containers still referencing the volume, otherwise the volume is 'in use' and cannot be removed.
$containers = & $runtime ps -aq --filter "volume=$volume"
if ($containers) {
    Write-Host "Removing containers using '$volume': $containers" -ForegroundColor Yellow
    & $runtime rm -f $containers | Out-Null
}

$existing = & $runtime volume ls -q --filter "name=^$volume$"
if (-not $existing) {
    Write-Host "Volume '$volume' does not exist; nothing to purge. The next AppHost run creates a fresh database." `
        -ForegroundColor Green
    return
}

& $runtime volume rm $volume | Out-Null
Write-Host "Purged developer database volume '$volume'." -ForegroundColor Green
Write-Host 'The next AppHost run rebuilds the schema (migration worker) and re-seeds the dev data.'
