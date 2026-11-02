#requires -Version 7

<#
.SYNOPSIS
    Gets the per-machine credentials of the conformance seed, generating each one on first use (issue #514).

.DESCRIPTION
    No conformance credential is committed (project-template ADR-0119, ADR-1370): the client secrets of the conformance
    clients that authenticate with one, and the test user's password, are development secret files kept outside the
    repository, beside the development root key in `LocalApplicationData/NCode.Identity/LocalDev`. The LocalDev AppHost
    (`src/NCode.Identity.OpenId.LocalDev/DevelopmentSecrets/ConformanceSeedSecrets.cs`) reads and generates the same
    files, with the same names, so every launcher seeds the same credentials. The clients are the ones the conformance
    overlay (`appsettings.Conformance.json`) registers with a `client_secret_*` token-endpoint authentication method,
    and the shared fixture `tests/NCode.Identity.OpenId.LocalDev.Tests/DevelopmentSecrets/conformance-seed-golden.json`
    pins the file names, the encoding and the variable names for both implementations.

    Each file's text is the base64url encoding of 32 random bytes. A missing file is generated: it is written to a
    temporary file readable only by the current user and moved into place without overwriting, so a crash never leaves
    a partial secret and a concurrent generator's file wins. An empty file left by an interrupted write is replaced.

    The script returns the credentials the runner hands the suite and the pre-flight, and the environment variables
    (`ConformanceSeed__...`) that hand the same credentials to the OpenID host's conformance seed. A client secret is
    configured as the base64url encoding of the file's bytes, so the file's text is the secret a client presents.

.PARAMETER Directory
    The directory that holds the development secret files. Defaults to the one the LocalDev AppHost uses.

.PARAMETER SettingsPath
    The conformance overlay that lists the seeded clients and the test user. Defaults to the reference host's
    `appsettings.Conformance.json`.

.PARAMETER SetEnvironment
    Set the `ConformanceSeed__...` environment variables in this PowerShell process instead of returning the
    credentials, so an OpenID host started from it afterwards seeds them; for an OP started by hand for
    `run-conformance.ps1 -UseRunningOp`.

.EXAMPLE
    ./build/conformance/Get-ConformanceSeedCredentials.ps1 -SetEnvironment
    dotnet run --project src/NCode.Identity.OpenId.Host --launch-profile Conformance
#>
[CmdletBinding()]
param(
    [string] $Directory = (Join-Path ([Environment]::GetFolderPath(
                [Environment+SpecialFolder]::LocalApplicationData,
                [Environment+SpecialFolderOption]::Create)) 'NCode.Identity' 'LocalDev'),

    [string] $SettingsPath = (Join-Path $PSScriptRoot '..' '..' 'src' 'NCode.Identity.OpenId.Host' 'appsettings.Conformance.json'),

    [switch] $SetEnvironment
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'ConformanceHelpers.ps1')

$StrictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)

# The seeded clients that authenticate with a client secret, and the test user, as the conformance overlay lists them.
# A client that names no method uses client_secret_basic, the seed's default.
$seed = (Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json -AsHashtable)['ConformanceSeed']
if ($null -eq $seed) { throw "$SettingsPath has no ConformanceSeed section." }
$SecretClientIds = @(
    foreach ($client in $seed['Clients'].GetEnumerator()) {
        $method = $client.Value['TokenEndpointAuthMethod']
        if ([string]::IsNullOrEmpty($method) -or $method.StartsWith('client_secret_', [StringComparison]::Ordinal)) {
            $client.Key
        }
    }
)
$TestUserName = $seed['TestUserName']
if ([string]::IsNullOrEmpty($TestUserName)) { throw "$SettingsPath names no ConformanceSeed:TestUserName." }

function Get-DevelopmentSecretPath([string] $FileName) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $path = Join-Path $Directory $FileName
    if (Test-Path -LiteralPath $path) {
        # An empty file can only be left by an interrupted in-place write of an older run.
        if ((Get-Item -LiteralPath $path).Length -gt 0) { return $path }
        Remove-Item -LiteralPath $path
    }

    $temporaryPath = Join-Path $Directory "$FileName.$([guid]::NewGuid().ToString('N')).tmp"
    $key = [byte[]]::new(32)
    $text = $null
    try {
        [System.Security.Cryptography.RandomNumberGenerator]::Fill($key)
        $text = [System.Text.Encoding]::ASCII.GetBytes((ConvertTo-Base64Url $key))

        $options = [System.IO.FileStreamOptions]::new()
        $options.Mode = [System.IO.FileMode]::CreateNew
        $options.Access = [System.IO.FileAccess]::Write
        $options.Share = [System.IO.FileShare]::None
        if (-not $IsWindows) {
            $options.UnixCreateMode = [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite
        }
        $stream = [System.IO.FileStream]::new($temporaryPath, $options)
        try {
            $stream.Write($text, 0, $text.Length)
            $stream.Flush($true)
        }
        finally { $stream.Dispose() }

        try {
            [System.IO.File]::Move($temporaryPath, $path, $false)
        }
        catch [System.IO.IOException] {
            # Another generator moved its file into place first; that one is the secret every run uses.
            if (-not (Test-Path -LiteralPath $path)) { throw }
        }
    }
    finally {
        [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($key)
        if ($null -ne $text) { [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($text) }
        Remove-Item -LiteralPath $temporaryPath -ErrorAction SilentlyContinue
    }

    return $path
}

$clientSecrets = [ordered]@{}
$environment = [ordered]@{}
foreach ($clientId in $SecretClientIds) {
    $bytes = [System.IO.File]::ReadAllBytes((Get-DevelopmentSecretPath "conformance-secret-$clientId"))
    $clientSecrets[$clientId] = $StrictUtf8.GetString($bytes)
    $environment["ConformanceSeed__Clients__${clientId}__ClientSecret"] = ConvertTo-Base64Url $bytes
}
$testUserPassword = $StrictUtf8.GetString([System.IO.File]::ReadAllBytes((Get-DevelopmentSecretPath 'conformance-user-password')))
$environment['ConformanceSeed__TestUserPassword'] = $testUserPassword

if ($SetEnvironment) {
    foreach ($entry in $environment.GetEnumerator()) { Set-Item -Path "Env:$($entry.Key)" -Value $entry.Value }
    Write-Host "Set the conformance seed's credentials ($($environment.Count) ConformanceSeed__ variables) from $Directory." -ForegroundColor Green
    return
}

[pscustomobject]@{
    ClientSecrets = $clientSecrets
    TestUserName = $TestUserName
    TestUserPassword = $testUserPassword
    Environment = $environment
}
