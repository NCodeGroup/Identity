#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks that every web app's copy of a brand kit favicon file is byte for byte the kit's file.

.DESCRIPTION
    An image build's context holds no brand/, so each web app under frontend/apps carries its own copies of the kit's
    favicon files in its favicon folder (ADR-1492). A copy that drifts from brand/favicon/ shows a stale icon.

    Outputs one string per problem, and nothing when clean:
    - an app file that brand/favicon/ has no file of the same name for;
    - an app file whose SHA-256 differs from the kit's file of the same name.

    An app without a favicon folder is skipped.

.PARAMETER Kit
    The brand kit's favicon folder. Defaults to brand/favicon under the repository root.

.PARAMETER Apps
    The folder that holds the web apps. Defaults to frontend/apps under the repository root.

.EXAMPLE
    ./build/favicon-copies.ps1
#>
[CmdletBinding()]
param(
    [string]$Kit = (Join-Path (Split-Path -Parent $PSScriptRoot) 'brand' 'favicon'),
    [string]$Apps = (Join-Path (Split-Path -Parent $PSScriptRoot) 'frontend' 'apps')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$problems = [System.Collections.Generic.List[string]]::new()
$base = Split-Path -Parent $Apps

$appNames = @(Get-ChildItem -LiteralPath $Apps -Directory | ForEach-Object { $_.Name })
[Array]::Sort($appNames, [StringComparer]::Ordinal)

foreach ($appName in $appNames) {
    $folder = Join-Path $Apps $appName 'favicon'
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) {
        continue
    }

    $fileNames = @(Get-ChildItem -LiteralPath $folder -File | ForEach-Object { $_.Name })
    [Array]::Sort($fileNames, [StringComparer]::Ordinal)

    foreach ($fileName in $fileNames) {
        $file = Get-Item -LiteralPath (Join-Path $folder $fileName)
        $relative = [IO.Path]::GetRelativePath($base, $file.FullName).Replace('\', '/')
        $kitFile = Join-Path $Kit $file.Name

        if (-not (Test-Path -LiteralPath $kitFile -PathType Leaf)) {
            $problems.Add("${relative}: brand/favicon/ has no $($file.Name); an app carries only the kit's files.")
        }
        elseif ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $kitFile -Algorithm SHA256).Hash) {
            $problems.Add("${relative}: differs from brand/favicon/$($file.Name); copy the kit's file over it.")
        }
    }
}

$problems
