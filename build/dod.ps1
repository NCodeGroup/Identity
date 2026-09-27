#!/usr/bin/env pwsh
#requires -version 7
<#
.SYNOPSIS
    The NCode.Identity Definition of Done — the single entrypoint CI and developers both run.

.DESCRIPTION
    Restores, builds (net10, warnings and vulnerability advisories as errors in CI), and tests the whole family
    with coverage. Runs identically locally and in CI so "it passes on my machine" means "it passes in the pipeline".

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER SkipTests
    Build only; skip the test + coverage stage.

.EXAMPLE
    ./build/dod.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'NCode.Identity.slnx'

# Enforce the strict gate: warnings-as-errors and NuGet vulnerability advisories (NU1901-1904) fail the build.
$env:CI = 'true'

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot
try {
    Invoke-Step 'tool-restore' { dotnet tool restore }
    Invoke-Step 'format (csharpier check)' { dotnet csharpier check . }
    Invoke-Step 'restore' { dotnet restore $solution }
    # Build also PACKS: GeneratePackageOnBuild (Directory.Build.targets) emits each shippable .nupkg + .snupkg to
    # artifacts/packages as part of the build, so there is no separate pack step.
    Invoke-Step 'build' { dotnet build $solution --configuration $Configuration --no-restore }

    if (-not $SkipTests) {
        Invoke-Step 'test' {
            dotnet test $solution `
                --configuration $Configuration `
                --no-build `
                --collect:"XPlat Code Coverage" `
                --logger:"junit;LogFilePath={assembly}-{framework}.junit.xml"
        }

        Invoke-Step 'coverage report' {
            # Merge every per-project/per-TFM cobertura file into one report + a text summary. The summary line
            # ("Line coverage: NN%") is echoed so a CI system can scrape the overall percentage, and the merged
            # Cobertura.xml drives per-line diff coverage views.
            $coverageDir = Join-Path $repoRoot 'artifacts/coverage'
            dotnet reportgenerator `
                "-reports:$(Join-Path $repoRoot '**/coverage.cobertura.xml')" `
                "-targetdir:$coverageDir" `
                "-reporttypes:Cobertura;TextSummary"
            if ($LASTEXITCODE -ne 0) { return }

            # ReportGenerator writes absolute source paths; rewrite them relative to the repo root (forward slashes)
            # so coverage lines up against the diff in a code-review UI.
            $cobertura = Join-Path $coverageDir 'Cobertura.xml'
            $content = (Get-Content $cobertura -Raw).Replace($repoRoot + '\', '').Replace($repoRoot + '/', '')
            $content = [regex]::Replace($content, 'filename="([^"]*)"', { param($m) 'filename="' + ($m.Groups[1].Value -replace '\\', '/') + '"' })
            Set-Content -Path $cobertura -Value $content -NoNewline

            Get-Content (Join-Path $coverageDir 'Summary.txt') | Write-Host
        }
    }

    Write-Host "`nDefinition of Done: PASSED" -ForegroundColor Green
}
finally {
    Pop-Location
}
