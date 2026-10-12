#!/usr/bin/env pwsh
# One-time frontend bootstrap: ensures the repo-pinned Node (via NVS) is reachable and installs the pnpm workspace.
#
# Orchestration is owned by .NET Aspire now (see docs/adr/0071): the `Aspire: Full stack (F5)` launch starts the
# AppHost, which runs each app's own `dev` script with deterministic, health-gated ordering. This script no longer
# runs `pnpm dev` — it only prepares the workspace so Aspire can spawn pnpm/node:
#   - The AppHost auto-prepends the newest NVS-managed Node 22 to its PATH, so `nvs add 22` is enough (no `nvs link`);
#     `corepack enable` below installs the `pnpm` shim into that same Node directory so Aspire finds pnpm there.
#   - Installing the pnpm workspace once from the root avoids the per-app installs Aspire would otherwise run
#     concurrently against the shared, symlinked node_modules.
# Run this once after a fresh clone (or when dependencies change), then launch `Aspire: Full stack (F5)`.

$ErrorActionPreference = 'Stop'

# Put the NVS-managed Node 22 on PATH for this process (Node is not on the global PATH unless `nvs link`ed).
$nodeDir = (Get-ChildItem "$env:LOCALAPPDATA\nvs\node\22*\x64" -Directory -ErrorAction SilentlyContinue |
    Select-Object -First 1).FullName
if ($nodeDir) {
    $env:Path = "$nodeDir;$env:Path"
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js 22 was not found. Install it with NVS: 'nvs add 22' (the AppHost adds it to PATH automatically)."
}

# Let corepack provision the repo-pinned pnpm non-interactively.
$env:COREPACK_ENABLE_DOWNLOAD_PROMPT = '0'
corepack enable 2>$null | Out-Null

Set-Location (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'src' | Join-Path -ChildPath 'frontend')

pnpm install --frozen-lockfile
if ($LASTEXITCODE -ne 0) {
    throw "pnpm install failed with exit code $LASTEXITCODE."
}

Write-Host "Frontend workspace installed. Launch 'Aspire: Full stack (F5)' to run the whole stack." -ForegroundColor Green
