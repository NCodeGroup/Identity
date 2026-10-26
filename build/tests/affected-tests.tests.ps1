#!/usr/bin/env pwsh
#requires -version 7
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$selector = Join-Path $PSScriptRoot '..' 'affected-tests.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-affected-tests-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function Write-FixtureFile([string]$Path, [string]$Text) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Write-Project([string]$Root, [string]$Path, [string[]]$References) {
    $items = ($References | ForEach-Object { "<ProjectReference Include=`"$_`" />" }) -join ''
    Write-FixtureFile (Join-Path $Root $Path) "<Project Sdk=`"Microsoft.NET.Sdk`"><ItemGroup>$items</ItemGroup></Project>`n"
}

function New-Fixture([string]$Name) {
    $root = Join-Path $fixtureRoot $Name
    Write-Project $root 'src/A/A.csproj' @()
    Write-Project $root 'src/B/B.csproj' @('..\A\A.csproj')
    Write-Project $root 'tests/A.Tests/A.Tests.csproj' @('..\..\src\A\A.csproj')
    Write-Project $root 'tests/B.Tests/B.Tests.csproj' @('..\..\src\B\B.csproj')
    return $root
}

function Assert-Selection([string]$Name, [object]$Result, [bool]$All, [string]$Projects) {
    $actual = $Result.Projects -join ','
    if ($Result.All -ne $All -or $actual -ne $Projects) {
        throw "Test '$Name' expected All=$All Projects='$Projects', got All=$($Result.All) Projects='$actual'."
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Invoke-Git([string]$Root, [string[]]$Arguments) {
    & git -C $Root @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$Root'."
    }
}

try {
    $fixture = New-Fixture 'paths'
    $bothTests = 'tests/A.Tests/A.Tests.csproj,tests/B.Tests/B.Tests.csproj'

    $result = & $selector -Root $fixture -ChangedPath 'src/A/X.cs'
    Assert-Selection 'referenced project selects every dependent test' $result $false $bothTests
    $result = & $selector -Root $fixture -ChangedPath 'src/B/X.cs'
    Assert-Selection 'leaf project selects its own test' $result $false 'tests/B.Tests/B.Tests.csproj'
    $result = & $selector -Root $fixture -ChangedPath 'tests/A.Tests/T.cs'
    Assert-Selection 'test project selects itself' $result $false 'tests/A.Tests/A.Tests.csproj'
    $result = & $selector -Root $fixture -ChangedPath 'docs/x.md'
    Assert-Selection 'path under no project selects nothing' $result $false ''
    $result = & $selector -Root $fixture -ChangedPath 'src/Directory.Build.props'
    Assert-Selection 'nested Directory file selects all' $result $true ''
    $result = & $selector -Root $fixture -ChangedPath 'build/x.ps1'
    Assert-Selection 'build script selects all' $result $true ''
    $result = & $selector -Root $fixture -ChangedPath 'BannedSymbols.Encoding.txt'
    Assert-Selection 'banned-symbols file selects all' $result $true ''

    $gitFixture = New-Fixture 'git'
    Invoke-Git $gitFixture @('init', '-q')
    Invoke-Git $gitFixture @('config', 'user.name', 'Affected Tests')
    Invoke-Git $gitFixture @('config', 'user.email', 'affected-tests@example.invalid')
    Invoke-Git $gitFixture @('config', 'core.autocrlf', 'false')
    Invoke-Git $gitFixture @('config', 'commit.gpgsign', 'false')
    Invoke-Git $gitFixture @('add', '-A')
    Invoke-Git $gitFixture @('commit', '-q', '-m', 'fixture')
    Write-FixtureFile (Join-Path $gitFixture 'src/B/New.cs') "class New { }`n"
    $result = & $selector -Root $gitFixture -Base HEAD
    Assert-Selection 'untracked file in git selects its tests' $result $false 'tests/B.Tests/B.Tests.csproj'

    Write-Host "$testCount affected-tests checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
