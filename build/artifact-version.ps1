#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Prints the version the artifact-publishing workflow gives the host image, the console image and the host chart.
.DESCRIPTION
    Nerdbank.GitVersioning gives a public-release build the same version for every commit (0.0.1-alpha on main), so
    the artifact version is not taken from it. The version is the prerelease in version.json followed by the number of
    first-parent commits of the built commit, such as 0.0.1-alpha.57. Every commit therefore publishes a distinct
    version that sorts after the versions of its ancestors. The version has no build metadata, because OCI tags cannot
    contain a plus sign. The workflow publishes prereleases only, so a version.json version without a prerelease label
    is refused.
.PARAMETER VersionJson
    The version.json to read the base version from. Defaults to the repository's version.json.
.PARAMETER Height
    The commit count to append, a positive integer. Defaults to the first-parent commit count of HEAD.
#>
[CmdletBinding()]
param(
    [string]$VersionJson = (Join-Path (Split-Path -Parent $PSScriptRoot) 'version.json'),
    [string]$Height = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$base = (Get-Content -LiteralPath $VersionJson -Raw | ConvertFrom-Json).version
if ($base -notmatch '^\d+\.\d+\.\d+-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*$') {
    throw "${VersionJson}: the version '$base' is not a prerelease version such as 0.0.1-alpha; this workflow publishes prereleases only."
}

if (-not $Height) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $Height = (git -C $repositoryRoot rev-list --count --first-parent HEAD) -join ''
    if ($LASTEXITCODE -ne 0) {
        throw "Could not count the first-parent commits of HEAD in ${repositoryRoot}."
    }
}

if ($Height -notmatch '^[1-9]\d*$') {
    throw "The height '$Height' is not a positive integer."
}

Write-Output "$base.$Height"
