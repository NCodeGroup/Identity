#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Prints the version of the packages a full Definition of Done packed, after checking it is one prerelease version.
.DESCRIPTION
    Nerdbank.GitVersioning versions the family's packages in lockstep (version.json), so every package in
    artifacts/packages must carry the same version. The prerelease workflow publishes only a prerelease version: release
    packages are signed and published from a release tag (GOVERNANCE.md "Publishing"). A tag the workflow publishes from
    must name exactly the packed version, so the tag and the packages on nuget.org never disagree.
.PARAMETER PackagesPath
    The folder of the packed .nupkg files. Defaults to artifacts/packages.
.PARAMETER Tag
    The tag the workflow runs for, such as v0.1.0-alpha.3, or empty for a manual run from main.
#>
[CmdletBinding()]
param(
    [string]$PackagesPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/packages'),
    [string]$Tag = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-PackageVersion([string]$Path) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -notlike '*/*' -and $_.Name -like '*.nuspec' } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw "${Path}: the package has no .nuspec."
        }

        $reader = [System.IO.StreamReader]::new($entry.Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        return $nuspec.package.metadata.version
    }
    finally {
        $archive.Dispose()
    }
}

$packages = @(Get-ChildItem -Path $PackagesPath -Filter '*.nupkg' -File -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    throw "No packages in ${PackagesPath}: run the full ./build/dod.ps1, which packs them."
}

$versions = @($packages | ForEach-Object { Read-PackageVersion $_.FullName } | Sort-Object -Unique)
if ($versions.Count -ne 1) {
    throw "The packages carry more than one version ($($versions -join ', ')); they are versioned in lockstep."
}

$version = $versions[0]
if (-not $version.Contains('-')) {
    throw "The packages carry the release version $version; this workflow publishes prereleases only."
}

if ($Tag -and $Tag -cne "v$version") {
    throw "The tag $Tag does not name the packed version $version."
}

$version
