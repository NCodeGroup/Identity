#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Regenerates the list of common passwords the password policy refuses.

.DESCRIPTION
    The password policy refuses a password that is on a bundled list of common passwords (ADR-0236). The list is
    derived from the UK National Cyber Security Centre's 100,000 most used passwords, as published in the SecLists
    repository at a pinned commit. The script downloads that file, verifies its SHA-256, and writes
    src/NCode.Identity.OpenId.Authentication/Passwords/common-passwords.txt: one entry per line, UTF-8 without a byte
    order mark, LF line endings, sorted by ordinal comparison.

    An entry is kept when it has at least eight Unicode code points and no control character. Entries are lower-cased
    with the invariant culture and de-duplicated, because the policy compares a password with the list ignoring case.
    The output is deterministic, so a change to the filter rules changes the committed list and must be made on
    purpose.

    The attribution and license of the source are recorded in THIRD-PARTY-NOTICES.md at the repository root.

.EXAMPLE
    ./build/Update-CommonPasswords.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commit = '1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56'
$expectedHash = 'C2E5696882C603B76BB67A47EE970897E5A76FC4C3F5547ABE3D0CA340C576E0'
$uri = "https://raw.githubusercontent.com/danielmiessler/SecLists/$commit/Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt"
$repoRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repoRoot 'src/NCode.Identity.OpenId.Authentication/Passwords/common-passwords.txt'
$source = Join-Path ([IO.Path]::GetTempPath()) 'ncsc-100k.txt'

Invoke-WebRequest -Uri $uri -OutFile $source
$hash = (Get-FileHash $source -Algorithm SHA256).Hash
if ($hash -ne $expectedHash) { throw "The downloaded list has SHA-256 $hash, expected $expectedHash." }

$text = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($source))
$set = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in $text.Split("`n")) {
    if ($line.Length -eq 0) { continue }
    if ($line.ToCharArray() | Where-Object { [char]::IsControl($_) }) { continue }
    $runes = 0
    foreach ($rune in $line.EnumerateRunes()) { $runes++ }
    if ($runes -lt 8) { continue }
    [void]$set.Add($line.ToLowerInvariant())
}

$sorted = [string[]]@($set)
[Array]::Sort($sorted, [StringComparer]::Ordinal)
[IO.File]::WriteAllText($target, ($sorted -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Remove-Item $source
Write-Host "Wrote $($sorted.Count) entries to $target."
