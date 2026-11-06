#!/usr/bin/env pwsh
#requires -version 7
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$buildFolder = Join-Path $PSScriptRoot '..'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-e2e-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function Write-FixtureFile([string]$Path, [string]$Text) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Write-Project([string]$Root, [string]$Path, [string[]]$References) {
    $items = ($References | ForEach-Object { "<ProjectReference Include=`"$_`" />" }) -join ''
    Write-FixtureFile (Join-Path $Root $Path) "<Project Sdk=`"Microsoft.NET.Sdk`"><ItemGroup>$items</ItemGroup></Project>`n"
}

function Invoke-Git([string]$Root, [string[]]$Arguments) {
    & git -C $Root @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$Root'."
    }
}

# A committed repository holding the scripts e2e.ps1 runs, so the scripts themselves are not a change.
function New-Fixture([string]$Name, [bool]$WithEndToEnd) {
    $root = Join-Path $fixtureRoot $Name
    foreach ($script in 'e2e.ps1', 'test-projects.ps1', 'affected-tests.ps1') {
        Write-FixtureFile (Join-Path $root 'build' $script) (Get-Content -LiteralPath (Join-Path $buildFolder $script) -Raw)
    }
    Write-Project $root 'src/A/A.csproj' @()
    Write-Project $root 'tests/A.Tests/A.Tests.csproj' @('..\..\src\A\A.csproj')
    if ($WithEndToEnd) {
        Write-Project $root 'tests/A.EndToEnd.Tests/A.EndToEnd.Tests.csproj' @('..\..\src\A\A.csproj')
    }
    Invoke-Git $root @('init', '-q')
    Invoke-Git $root @('config', 'user.name', 'E2E Tests')
    Invoke-Git $root @('config', 'user.email', 'e2e-tests@example.invalid')
    Invoke-Git $root @('config', 'core.autocrlf', 'false')
    Invoke-Git $root @('config', 'commit.gpgsign', 'false')
    Invoke-Git $root @('add', '-A')
    Invoke-Git $root @('commit', '-q', '-m', 'fixture')
    return $root
}

# A child process, so the script's strict mode, return and throw stay in it. A -Docker folder goes first on its PATH.
function Invoke-EndToEnd([string]$Root, [string[]]$Arguments, [string]$Docker) {
    $path = $env:PATH
    try {
        if ($Docker) {
            $env:PATH = "$Docker$([System.IO.Path]::PathSeparator)$path"
        }
        $output = & pwsh -NoProfile -File (Join-Path $Root 'build' 'e2e.ps1') @Arguments 2>&1 | Out-String
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
    }
    finally {
        $env:PATH = $path
    }
}

# A folder holding a docker command that always fails, as an engine that is not running does.
function New-FailingDocker {
    $folder = Join-Path $fixtureRoot 'docker'
    if ($IsWindows) {
        Write-FixtureFile (Join-Path $folder 'docker.cmd') "@exit /b 1`r`n"
    }
    else {
        $stub = Join-Path $folder 'docker'
        Write-FixtureFile $stub "#!/bin/sh`nexit 1`n"
        & chmod +x $stub
    }
    return $folder
}

function Assert-Run([string]$Name, [object]$Run, [bool]$Succeeds, [string[]]$Expected) {
    $succeeded = $Run.ExitCode -eq 0
    $missing = @($Expected | Where-Object { -not $Run.Output.Contains($_) })
    if ($succeeded -ne $Succeeds -or $missing.Count -gt 0) {
        throw "Test '$Name' expected success=$Succeeds and output containing '$($Expected -join "', '")', got exit code $($Run.ExitCode):$([Environment]::NewLine)$($Run.Output)"
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

try {
    $fixture = New-Fixture 'affected' $true
    $run = Invoke-EndToEnd $fixture @('-Affected', '-Base', 'HEAD')
    Assert-Run 'no change affects no suite and passes' $run $true @(
        'No end-to-end suite is affected; skipping.',
        'End-to-end: PASSED'
    )

    Write-FixtureFile (Join-Path $fixture '.github/workflows/ci.yml') "name: CI`n"
    $run = Invoke-EndToEnd $fixture @('-Affected', '-Base', 'HEAD')
    Assert-Run 'a change under no project affects no suite and passes' $run $true @(
        '.github/workflows/ci.yml -> nothing (no project)',
        'No end-to-end suite is affected; skipping.',
        'End-to-end: PASSED'
    )

    $fixture = New-Fixture 'none' $false
    $run = Invoke-EndToEnd $fixture @()
    Assert-Run 'a repository without a suite fails before it needs Docker' $run $false @(
        'No end-to-end suite exists under tests/.'
    )

    $docker = New-FailingDocker
    $dockerMissing = 'The end-to-end suites need a running Docker engine'

    $fixture = New-Fixture 'selected' $true
    Write-FixtureFile (Join-Path $fixture 'src/A/X.cs') "class X { }`n"
    $run = Invoke-EndToEnd $fixture @('-Affected', '-Base', 'HEAD') $docker
    Assert-Run 'a change that reaches a suite requires Docker' $run $false @(
        'src/A/X.cs -> src/A/A.csproj',
        $dockerMissing
    )

    $fixture = New-Fixture 'global' $true
    Write-FixtureFile (Join-Path $fixture 'build/x.ps1') "exit 0`n"
    $run = Invoke-EndToEnd $fixture @('-Affected', '-Base', 'HEAD') $docker
    Assert-Run 'a global build input selects every suite and requires Docker' $run $false @(
        'build/x.ps1 -> all tests (global trigger)',
        $dockerMissing
    )

    Write-Host "$testCount e2e checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
