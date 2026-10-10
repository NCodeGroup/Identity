#!/usr/bin/env pwsh
# Starts the Management UI (Vite) dev server using the repo-pinned Node via NVS.
# Invoked by the ".vscode" compound launch profile ("API + UI"); also runnable standalone.

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

corepack enable 2>$null | Out-Null

Set-Location (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'src' | Join-Path -ChildPath 'frontend')
pnpm --filter @ncode/app-mgmt dev
