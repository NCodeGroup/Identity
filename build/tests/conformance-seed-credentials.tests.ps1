#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Checks build/conformance/Get-ConformanceSeedCredentials.ps1 and the conformance scripts' shared helpers.

.DESCRIPTION
    The script and the LocalDev AppHost's ConformanceSeedSecrets generate the same per-machine files, so both are
    checked against one golden fixture, tests/NCode.Identity.OpenId.LocalDev.Tests/DevelopmentSecrets/
    conformance-seed-golden.json: given its files, the script must return exactly its environment, as the C# test
    requires of the AppHost. The remaining checks cover generating a missing file and expanding the committed suite
    configuration templates, which must leave no token unreplaced.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Join-Path $PSScriptRoot '..' '..'
$credentialScript = Join-Path $repoRoot 'build' 'conformance' 'Get-ConformanceSeedCredentials.ps1'
$goldenPath = Join-Path $repoRoot 'tests' 'NCode.Identity.OpenId.LocalDev.Tests' 'DevelopmentSecrets' 'conformance-seed-golden.json'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-conformance-seed-$([guid]::NewGuid().ToString('N'))"
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$testCount = 0

. (Join-Path $repoRoot 'build' 'conformance' 'ConformanceHelpers.ps1')

function New-FixtureDirectory([string] $Name) {
    $path = Join-Path $fixtureRoot $Name
    $null = [System.IO.Directory]::CreateDirectory($path)
    return $path
}

function Assert-True([string] $Name, [bool] $Condition, [string] $Detail) {
    if (-not $Condition) { throw "Test '$Name' failed: $Detail" }
}

function Complete-Test([string] $Name) {
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Assert-Throws([string] $Name, [scriptblock] $Action, [string] $ExpectedFragment) {
    try {
        & $Action
    }
    catch {
        $message = $_.Exception.Message
        Assert-True $Name $message.Contains($ExpectedFragment, [StringComparison]::Ordinal) "expected an error containing '$ExpectedFragment', got '$message'."
        Complete-Test $Name
        return
    }
    throw "Test '$Name' failed: expected an error containing '$ExpectedFragment', got none."
}

try {
    # --- The golden fixture: the contract shared with ConformanceSeedSecretsTests. ---------------------------------
    $golden = Get-Content -LiteralPath $goldenPath -Raw | ConvertFrom-Json -AsHashtable
    $directory = New-FixtureDirectory 'golden'
    foreach ($file in $golden.files.GetEnumerator()) {
        [System.IO.File]::WriteAllText((Join-Path $directory $file.Key), $file.Value, $strictUtf8)
    }
    $credentials = & $credentialScript -Directory $directory

    $name = 'the golden fixture files yield the golden fixture environment'
    $expected = @($golden.environment.Keys | Sort-Object -CaseSensitive)
    $actual = @($credentials.Environment.Keys | Sort-Object -CaseSensitive)
    Assert-True $name (($expected -join "`n") -ceq ($actual -join "`n")) "expected variables [$($expected -join ', ')], got [$($actual -join ', ')]."
    foreach ($key in $expected) {
        Assert-True $name ($golden.environment[$key] -ceq $credentials.Environment[$key]) "'$key' is '$($credentials.Environment[$key])', expected '$($golden.environment[$key])'."
    }
    Complete-Test $name

    $name = 'each client secret is the text of its file'
    foreach ($client in $credentials.ClientSecrets.GetEnumerator()) {
        $expectedText = $golden.files["conformance-secret-$($client.Key)"]
        Assert-True $name ($client.Value -ceq $expectedText) "client '$($client.Key)' got '$($client.Value)', expected '$expectedText'."
    }
    Assert-True $name ($credentials.ClientSecrets.Count -eq ($golden.files.Count - 1)) "got $($credentials.ClientSecrets.Count) client secrets."
    Complete-Test $name

    $name = 'the test user is the overlay''s, with the text of the password file'
    Assert-True $name ($credentials.TestUserName -ceq 'conformance-user') "got user '$($credentials.TestUserName)'."
    Assert-True $name ($credentials.TestUserPassword -ceq $golden.files['conformance-user-password']) "got password '$($credentials.TestUserPassword)'."
    Complete-Test $name

    $name = 'existing files are read, not replaced'
    Assert-True $name (@(Get-ChildItem -LiteralPath $directory -File).Count -eq $golden.files.Count) 'the script added or removed a file.'
    Complete-Test $name

    # --- Generating missing files. ------------------------------------------------------------------------------
    $directory = Join-Path $fixtureRoot 'generated' 'LocalDev'
    $first = & $credentialScript -Directory $directory
    $files = @(Get-ChildItem -LiteralPath $directory -File)

    $name = 'missing files are generated as base64url text of 32 random bytes'
    Assert-True $name ($files.Count -eq $golden.files.Count) "expected $($golden.files.Count) files, got [$($files.Name -join ', ')]."
    foreach ($file in $files) {
        Assert-True $name ($golden.files.Contains($file.Name)) "unexpected file '$($file.Name)'."
        $text = [System.IO.File]::ReadAllText($file.FullName, $strictUtf8)
        Assert-True $name ($text -cmatch '^[A-Za-z0-9_-]{43}$') "'$($file.Name)' holds '$text'."
    }
    Complete-Test $name

    if (-not $IsWindows) {
        $name = 'generated files are readable only by their owner'
        foreach ($file in $files) {
            $mode = [System.IO.File]::GetUnixFileMode($file.FullName)
            Assert-True $name ($mode -eq ([System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)) "'$($file.Name)' has mode $mode."
        }
        Complete-Test $name
    }

    $name = 'a second run returns the same credentials'
    $second = & $credentialScript -Directory $directory
    foreach ($key in $first.Environment.Keys) {
        Assert-True $name ($first.Environment[$key] -ceq $second.Environment[$key]) "'$key' changed."
    }
    Complete-Test $name

    $name = 'an empty file is replaced'
    $passwordPath = Join-Path $directory 'conformance-user-password'
    [System.IO.File]::WriteAllText($passwordPath, '')
    $third = & $credentialScript -Directory $directory
    Assert-True $name ($third.TestUserPassword -cmatch '^[A-Za-z0-9_-]{43}$') "got '$($third.TestUserPassword)'."
    Assert-True $name (@(Get-ChildItem -LiteralPath $directory -File).Count -eq $golden.files.Count) 'a temporary file was left behind.'
    Complete-Test $name

    $name = '-SetEnvironment sets each variable in this process'
    $saved = @{}
    foreach ($key in $third.Environment.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key) }
    try {
        $null = & $credentialScript -Directory $directory -SetEnvironment
        foreach ($key in $third.Environment.Keys) {
            Assert-True $name ([Environment]::GetEnvironmentVariable($key) -ceq $third.Environment[$key]) "'$key' is not set."
        }
    }
    finally {
        foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    }
    Complete-Test $name

    # --- Expanding the suite configuration templates. ---------------------------------------------------------------
    $name = 'a token is replaced with its JSON-escaped value'
    $text = Expand-ConfigTemplate '{"a":"{{x}}"}' ([ordered]@{ x = 'q"z' })
    Assert-True $name ($text -ceq '{"a":"q"z"}') "got '$text'."
    Complete-Test $name

    Assert-Throws 'a token that is not supplied fails the expansion' {
        Expand-ConfigTemplate '{"a":"{{x}}","b":"{{y}}"}' ([ordered]@{ x = '1' })
    } '{{y}}'

    $testUserTokens = [ordered]@{ test_user_name = 'conformance-user'; test_user_password = 'p' }
    $name = 'the committed Basic OP template expands with the test user''s tokens'
    $basic = Expand-ConfigTemplate (Get-Content -LiteralPath (Join-Path $repoRoot 'build' 'conformance' 'oidcc-basic.template.json') -Raw) $testUserTokens
    $null = $basic | ConvertFrom-Json -AsHashtable
    Complete-Test $name

    $name = 'the committed FAPI template expands with the runner''s tokens'
    $fapiTokens = [ordered]@{
        issuer = 'https://op.test'; alias = 'a'; client_auth_method = 'private_key_jwt'; client_id = 'c'; client2_id = 'c2'
    } + $testUserTokens
    $fapi = Expand-ConfigTemplate (Get-Content -LiteralPath (Join-Path $repoRoot 'build' 'conformance' 'fapi1-advanced.template.json') -Raw) $fapiTokens
    $null = $fapi | ConvertFrom-Json -AsHashtable
    Complete-Test $name

    Write-Host "$testCount conformance-seed-credentials checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
