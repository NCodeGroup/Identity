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
#
# SELF-HEAL: a fast `--frozen-lockfile` install is a no-op when pnpm believes node_modules matches the lockfile — but
# it will NOT rebuild symlinks that were deleted/broken out-of-band (branch switches, antivirus, OneDrive, an aborted
# install). That leaves an app that fails at dev time with Vite "Failed to resolve import 'react-redux'". This is the
# single, SERIAL repair point (the AppHost runs the apps with `install: false` precisely because concurrent per-app
# repairs collide with ERR_PNPM_EBUSY), so after the frozen install we probe each workspace package's directly-declared
# dependencies and, only when a link is actually missing/broken, repair with a forced reinstall. Re-running this script
# is the one command that fixes a corrupted frontend node_modules.

$ErrorActionPreference = 'Stop'

# Returns $false when any workspace package is missing a directly-declared dependency under its own node_modules.
# pnpm's isolated layout (shamefully-hoist=false) symlinks every DIRECT dependency into the owning package's
# node_modules (transitives live in the shared .pnpm store), so a missing entry is a reliable corruption signal.
# Test-Path follows reparse points, so a broken junction (target gone) also reports missing — exactly what we want.
function Test-FrontendNodeModulesIntact {
    param([Parameter(Mandatory)][string] $Root)

    $packageDirs = @($Root) + @(
        Get-ChildItem -Path (Join-Path $Root 'apps'), (Join-Path $Root 'packages') -Directory -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    )

    foreach ($packageDir in $packageDirs) {
        $packageJsonPath = Join-Path $packageDir 'package.json'
        if (-not (Test-Path -LiteralPath $packageJsonPath)) {
            continue
        }

        $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
        $dependencyNames = @('dependencies', 'devDependencies') | ForEach-Object {
            if ($packageJson.PSObject.Properties.Name -contains $_ -and $packageJson.$_) {
                $packageJson.$_.PSObject.Properties.Name
            }
        }

        foreach ($dependencyName in $dependencyNames) {
            $expectedPath = Join-Path $packageDir (Join-Path 'node_modules' $dependencyName)
            if (-not (Test-Path -LiteralPath $expectedPath)) {
                Write-Host "  missing link: $($packageJson.name) -> $dependencyName" -ForegroundColor Yellow
                return $false
            }
        }
    }

    return $true
}

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

$frontendDir = (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'src' | Join-Path -ChildPath 'frontend')
Set-Location $frontendDir

pnpm install --frozen-lockfile
if ($LASTEXITCODE -ne 0) {
    throw "pnpm install failed with exit code $LASTEXITCODE."
}

# Self-heal: the frozen install above is a no-op when pnpm thinks node_modules is current, so it cannot rebuild links
# that were lost out-of-band. Probe the symlinks and, only when something is actually broken, force a reinstall (the
# one operation that rebuilds them). This keeps the common fresh-clone path fast while making a re-run the repair.
if (-not (Test-FrontendNodeModulesIntact -Root $frontendDir)) {
    Write-Host "node_modules is out of sync; repairing with a forced reinstall..." -ForegroundColor Yellow

    pnpm install --force
    if ($LASTEXITCODE -ne 0) {
        throw "pnpm install --force failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-FrontendNodeModulesIntact -Root $frontendDir)) {
        throw "Frontend node_modules is still incomplete after a forced reinstall; inspect src/frontend manually."
    }
}

Write-Host "Frontend workspace installed. Launch 'Aspire: Full stack (F5)' to run the whole stack." -ForegroundColor Green
