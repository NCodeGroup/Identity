#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Writes the published version over the version in version.json for an image build, whose git engine is disabled.
.DESCRIPTION
    The image build stage disables the Nerdbank.GitVersioning git engine, so version.json is the only version input and
    an MSBuild property cannot set the version. The script refuses a version with a character outside [0-9A-Za-z.-],
    replaces the one "version" value of version.json, and fails unless the parsed version of the result equals the
    given version. It validates before it writes, so a refused version leaves the file untouched.
.PARAMETER Version
    The version to stamp, such as 0.0.1-alpha.57.
.PARAMETER VersionJson
    The version.json to stamp. Defaults to the repository's version.json.
#>
[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$VersionJson = (Join-Path (Split-Path -Parent $PSScriptRoot) 'version.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Version) { throw 'Give the version to stamp with -Version.' }
if ($Version -cnotmatch '^[0-9A-Za-z.-]+$') { throw "VERSION '$Version' is not a version." }

$text = [IO.File]::ReadAllText($VersionJson)
$pattern = '"version"\s*:\s*"[^"]*"'
$count = [regex]::Matches($text, $pattern).Count
if ($count -ne 1) {
    throw "${VersionJson}: expected one ""version"" value, found $count."
}

$stamped = [regex]::Replace($text, $pattern, "`"version`": `"$Version`"")
$written = (ConvertFrom-Json $stamped).PSObject.Properties['version']?.Value
if ($written -cne $Version) {
    throw "${VersionJson}: the version after stamping is '$written', not '$Version'."
}

[IO.File]::WriteAllText($VersionJson, $stamped)
