#!/usr/bin/env pwsh
# Starts the ENTIRE frontend stack (Vite, via Turborepo) using the repo-pinned Node via NVS.
# Invoked by the ".vscode" compound launch profile ("API + UI"); also runnable standalone.
#
# This runs `pnpm dev` (= `turbo run dev`), which starts every app's own `dev` script and so
# picks up any future app automatically with no change here. Each app encodes its serving model
# in that script:
#   - backend-served SPAs (served by the .NET host from static `dist`, e.g. apps/auth) use
#     `vite build --watch`, which keeps `dist` fresh so a browser refresh shows edits;
#   - standalone SPAs (e.g. apps/mgmt) use `vite`, an HMR dev server on its own port.
# See src/frontend/README.md ("Running the full stack") and docs/adr/0064.

$ErrorActionPreference = 'Stop'

# Put the NVS-managed Node 22 on PATH for this process (Node is not on the global PATH).
$nodeDir = (Get-ChildItem "$env:LOCALAPPDATA\nvs\node\22*\x64" -Directory -ErrorAction SilentlyContinue |
    Select-Object -First 1).FullName
if ($nodeDir) {
    $env:Path = "$nodeDir;$env:Path"
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js 22 was not found. Install it with NVS: 'nvs add 22' (optionally 'nvs link 22' to add it to PATH)."
}

# Let corepack provision the repo-pinned pnpm non-interactively (an F5 launch has no TTY to
# answer corepack's "Do you want to continue?" download prompt).
$env:COREPACK_ENABLE_DOWNLOAD_PROMPT = '0'
corepack enable 2>$null | Out-Null

Set-Location (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'src' | Join-Path -ChildPath 'frontend')

# Ensure workspace dependencies (including Vite) are installed; a fresh clone has no node_modules,
# so running 'dev' directly fails with "'vite' is not recognized".
if (-not (Test-Path 'node_modules')) {
    pnpm install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) {
        throw "pnpm install failed with exit code $LASTEXITCODE."
    }
}

# `turbo run dev` across the workspace: starts every app's `dev` script (build-watch for
# backend-served bundles, dev server for standalone apps). Future apps are included automatically.
pnpm dev
