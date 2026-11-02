#requires -Version 7

<#
.SYNOPSIS
    Gets the per-machine credentials of the conformance seed, generating each one on first use (issue #514).

.DESCRIPTION
    No conformance credential is committed (project-template ADR-0119, ADR-1370): the client secrets of the conformance
    clients that authenticate with one, and the test user's password, are development secret files kept outside the
    repository, beside the development root key in `LocalApplicationData/NCode.Identity/LocalDev`. The LocalDev AppHost
    (`src/NCode.Identity.OpenId.LocalDev/DevelopmentSecrets/ConformanceSeedSecrets.cs`) reads and generates the same
    files, with the same names, so every launcher seeds the same credentials.

    Each file's text is the base64url encoding of 32 random bytes. A missing file is generated: it is written to a
    temporary file readable only by the current user and moved into place without overwriting, so a crash never leaves
    a partial secret and a concurrent generator's file wins. An empty file left by an interrupted write is replaced.

    The script returns the credentials the runner hands the suite and the pre-flight, and the environment variables
    (`ConformanceSeed__...`) that hand the same credentials to the OpenID host's conformance seed. A client secret is
    configured base64url-encoded, so the file's text is the secret a client presents.

.PARAMETER Directory
    The directory that holds the development secret files. Defaults to the one the LocalDev AppHost uses.

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

    [switch] $SetEnvironment
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The conformance clients that authenticate with a client secret, as appsettings.Conformance.json lists them, and the
# test user it seeds. Keep both in step with ConformanceSeedSecrets.cs.
$SecretClientIds = @('client', 'client2', 'client-secret-post')
$TestUserName = 'conformance-user'

function ConvertTo-Base64Url([byte[]] $Bytes) {
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-DevelopmentSecret([string] $FileName) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $path = Join-Path $Directory $FileName
    if (Test-Path -LiteralPath $path) {
        # An empty file can only be left by an interrupted in-place write of an older run.
        if ((Get-Item -LiteralPath $path).Length -gt 0) {
            return [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false, $true))
        }
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

    return [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false, $true))
}

$clientSecrets = [ordered]@{}
$environment = [ordered]@{}
foreach ($clientId in $SecretClientIds) {
    $secret = Get-DevelopmentSecret "conformance-secret-$clientId"
    $clientSecrets[$clientId] = $secret
    $environment["ConformanceSeed__Clients__${clientId}__ClientSecret"] =
    ConvertTo-Base64Url ([System.Text.UTF8Encoding]::new($false, $true).GetBytes($secret))
}
$testUserPassword = Get-DevelopmentSecret 'conformance-user-password'
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
