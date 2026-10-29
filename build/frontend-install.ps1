#!/usr/bin/env pwsh
# One-time frontend bootstrap: ensures the repo-pinned Node and pnpm are reachable and installs the pnpm workspace.
#
# Orchestration is owned by .NET Aspire now (see docs/decisions/0071): the `Aspire: Full stack (F5)` launch starts the
# AppHost, which runs each app's own `dev` script with deterministic, health-gated ordering. This script no longer
# runs `pnpm dev` — it only prepares the workspace so Aspire can spawn pnpm/node:
#   - The Node major is pinned once, in src/frontend/.node-version (CI and the AppHost read it too). With NVS, this
#     script adds that major when it is missing, and the AppHost prepends the same install to its PATH, so no
#     `nvs link` is needed. Any other version manager works as long as `node` on PATH is the pinned major.
#   - pnpm is the exact version the workspace's "packageManager" field pins. Node no longer bundles Corepack, so this
#     script installs that version with the pinned Node's npm, into its global prefix — for NVS, the Node directory
#     itself, so Aspire finds pnpm next to node.
#   - Installing the pnpm workspace once from the root avoids the per-app installs Aspire would otherwise run
#     concurrently against the shared, symlinked node_modules.
#   - The ASP.NET Core development certificate is exported as PEM to ~/.ncode/identity/dev-cert (or the directory in
#     NCODE_DEV_CERT_DIR) so the standalone Vite dev servers serve HTTPS outside Aspire.
# Run this once after a fresh clone, and again when dependencies or the pinned Node/pnpm change, then launch
# `Aspire: Full stack (F5)`.
#
# SELF-HEAL: a fast `--frozen-lockfile` install is a no-op when pnpm believes node_modules matches the lockfile — but
# it will NOT rebuild symlinks that were deleted/broken out-of-band (branch switches, antivirus, OneDrive, an aborted
# install). That leaves an app that fails at dev time with Vite "Failed to resolve import 'react-redux'". This is the
# single, SERIAL repair point (the AppHost runs the apps with `install: false` precisely because concurrent per-app
# repairs collide with ERR_PNPM_EBUSY), so after the frozen install we probe each workspace package's directly-declared
# dependencies and, only when a link is actually missing/broken, repair with a forced reinstall. Re-running this script
# is the one command that fixes a corrupted frontend node_modules.

$ErrorActionPreference = 'Stop'

# Returns the major of a Node version string ("26", "26.12.0", or "v26.12.0").
function Get-NodeMajor {
    param([Parameter(Mandatory)][string] $Version)

    return [int] ($Version.Trim().TrimStart('v') -split '\.')[0]
}

# Returns the x64 directory of the newest NVS-managed install of the given Node major, or $null when there is none.
# Versions compare numerically (22.9.0 < 22.22.2), never as strings — the AppHost selects the same way.
function Find-NvsNode {
    param([Parameter(Mandatory)][int] $Major)

    if (-not $env:LOCALAPPDATA) {
        return $null
    }

    $nvsNodeRoot = Join-Path $env:LOCALAPPDATA 'nvs/node'
    $installs = Get-ChildItem -Path $nvsNodeRoot -Directory -Filter "$Major.*" -ErrorAction SilentlyContinue |
        ForEach-Object {
            $version = $null
            if ([version]::TryParse($_.Name, [ref] $version) -and $version.Major -eq $Major) {
                [pscustomobject] @{ Version = $version; Directory = Join-Path $_.FullName 'x64' }
            }
        } |
        Where-Object { Test-Path -LiteralPath $_.Directory } |
        Sort-Object -Property Version -Descending

    return @($installs)[0].Directory
}

# Returns $false when any workspace package is missing a directly-declared dependency under its own node_modules.
# pnpm's isolated layout (`shamefullyHoist: false`) symlinks every DIRECT dependency into the owning package's
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

$frontendDir = (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'src' | Join-Path -ChildPath 'frontend')
$nodeMajor = Get-NodeMajor (Get-Content -LiteralPath (Join-Path $frontendDir '.node-version') -Raw)

# Put the NVS-managed Node of the pinned major on PATH for this process (Node is not on the global PATH unless
# `nvs link`ed), adding it through NVS first when it is missing.
$nodeDir = Find-NvsNode $nodeMajor
if (-not $nodeDir -and (Get-Command nvs -ErrorAction SilentlyContinue)) {
    Write-Host "Adding Node $nodeMajor (src/frontend/.node-version) with NVS..." -ForegroundColor Yellow
    nvs add $nodeMajor
    $nodeDir = Find-NvsNode $nodeMajor
}
if ($nodeDir) {
    $env:PATH = $nodeDir + [IO.Path]::PathSeparator + $env:PATH
}

$nodeVersion = if (Get-Command node -ErrorAction SilentlyContinue) { node --version }
if (-not $nodeVersion -or (Get-NodeMajor $nodeVersion) -ne $nodeMajor) {
    $found = if ($nodeVersion) { "'node' on PATH is $nodeVersion" } else { "'node' is not on PATH" }
    throw ("Node.js $nodeMajor (pinned in src/frontend/.node-version) was not found; $found. Install NVS and " +
        "re-run this script, which adds the pinned major ('nvs add $nodeMajor').")
}

# Install the exact pnpm the workspace pins into the selected Node's npm global prefix, unless it is already there, and
# put that prefix first on PATH so this script runs the same pnpm the AppHost will.
$packageJsonPath = Join-Path $frontendDir 'package.json'
$packageManager = (Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json).packageManager
if ($packageManager -notmatch '^pnpm@(?<version>[^+]+)') {
    throw "src/frontend/package.json must pin pnpm in its ""packageManager"" field (found '$packageManager')."
}
$pnpmVersion = $Matches.version
$installedPnpmManifest = Join-Path (npm root --global) 'pnpm/package.json'
$installedPnpmVersion = if (Test-Path -LiteralPath $installedPnpmManifest) {
    (Get-Content -LiteralPath $installedPnpmManifest -Raw | ConvertFrom-Json).version
}
if ($installedPnpmVersion -ne $pnpmVersion) {
    Write-Host "Installing pnpm $pnpmVersion for Node $nodeVersion..." -ForegroundColor Yellow
    npm install --global "pnpm@$pnpmVersion"
    if ($LASTEXITCODE -ne 0) {
        throw "npm install --global pnpm@$pnpmVersion failed with exit code $LASTEXITCODE."
    }
}
$npmGlobalBin = npm prefix --global
if (-not $IsWindows) {
    $npmGlobalBin = Join-Path $npmGlobalBin 'bin'
}
$env:PATH = $npmGlobalBin + [IO.Path]::PathSeparator + $env:PATH

Set-Location $frontendDir

# This script is the workspace's installer, so pnpm may replace node_modules wholesale (a pnpm major or a changed
# store layout requires it) without the interactive confirmation it otherwise asks for.
pnpm install --frozen-lockfile --config.confirm-modules-purge=false
if ($LASTEXITCODE -ne 0) {
    throw "pnpm install failed with exit code $LASTEXITCODE."
}

# Self-heal: the frozen install above is a no-op when pnpm thinks node_modules is current, so it cannot rebuild links
# that were lost out-of-band. Probe the symlinks and, only when something is actually broken, force a reinstall (the
# one operation that rebuilds them). This keeps the common fresh-clone path fast while making a re-run the repair.
if (-not (Test-FrontendNodeModulesIntact -Root $frontendDir)) {
    Write-Host "node_modules is out of sync; repairing with a forced reinstall..." -ForegroundColor Yellow

    pnpm install --force --config.confirm-modules-purge=false
    if ($LASTEXITCODE -ne 0) {
        throw "pnpm install --force failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-FrontendNodeModulesIntact -Root $frontendDir)) {
        throw "Frontend node_modules is still incomplete after a forced reinstall; inspect src/frontend manually."
    }
}

# Export the ASP.NET Core development certificate as PEM (localhost.pem and localhost.key) for the standalone Vite dev
# servers, which serve HTTPS from it outside Aspire. The export overwrites on every run, so a renewed certificate is
# picked up. Failures only warn: this script also runs where dotnet may be absent, and the dev server then fails fast
# with a message that names this script.
$certDirectory = if ($env:NCODE_DEV_CERT_DIR) { $env:NCODE_DEV_CERT_DIR } else { Join-Path $HOME '.ncode/identity/dev-cert' }
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    New-Item -ItemType Directory -Force -Path $certDirectory | Out-Null

    dotnet dev-certs https --export-path (Join-Path $certDirectory 'localhost.pem') --format Pem --no-password
    if ($LASTEXITCODE -ne 0) {
        Write-Warning ("The ASP.NET Core development certificate could not be exported to '$certDirectory'; the " +
            "standalone Vite surfaces will fail to start until it is.")
    }
    else {
        Write-Host "Exported the development certificate to $certDirectory." -ForegroundColor Green
    }

    dotnet dev-certs https --check --trust | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "The ASP.NET Core development certificate is not trusted; run 'dotnet dev-certs https --trust'."
    }
}
else {
    Write-Warning "'dotnet' is not on PATH, so the ASP.NET Core development certificate was not exported."
}
$global:LASTEXITCODE = 0

Write-Host "Frontend workspace installed. Launch 'Aspire: Full stack (F5)' to run the whole stack." -ForegroundColor Green
