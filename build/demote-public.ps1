# Demotes top-level public type declarations to internal across a package,
# excluding files (by path relative to -Root) whose type must stay public,
# then removes the demoted types' entries from PublicAPI.Unshipped.txt.
#
# KeepFiles entries are paths relative to -Root (e.g. 'DefaultRegistration.cs'
# or 'Environments/OpenIdEnvironment.cs'); only exact relative-path matches are kept.
param(
    [Parameter(Mandatory)] [string] $Root,
    [string[]] $KeepFiles = @(),
    [string[]] $DemoteFiles = @()
)

$declRx = '(?m)^public (?=(sealed |abstract |static |partial |readonly )*(class|struct|record|interface|enum)\b)'
$rootFull = (Resolve-Path $Root).Path
$keepNorm = $KeepFiles | ForEach-Object { $_.Replace('\', '/') }
$demoteNorm = $DemoteFiles | ForEach-Object { $_.Replace('\', '/') }

function Get-RelPath($full) {
    return $full.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
}

$changed = @()
Get-ChildItem -Path $Root -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object {
        $rel = Get-RelPath $_.FullName
        # Explicit demote-list mode (safe): only flip the listed files.
        if ($demoteNorm.Count -gt 0) {
            if ($demoteNorm -notcontains $rel) { return }
        }
        elseif ($keepNorm -contains $rel) { return }
        $c = [System.IO.File]::ReadAllText($_.FullName)
        $n = [regex]::Replace($c, $declRx, 'internal ')
        if ($c -ne $n) {
            [System.IO.File]::WriteAllText($_.FullName, $n)
            $changed += $_.FullName
        }
    }
Write-Host "Changed $($changed.Count) files:"
$changed | ForEach-Object { Write-Host "  $(Get-RelPath $_)" }

# Build fully-qualified names of every top-level internal type in the package,
# then strip matching entries from PublicAPI.Unshipped.txt (idempotent, collision-safe).
$nsRx = '(?m)^namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;'
$typeRx = '(?m)^internal (?:sealed |abstract |static |partial |readonly )*(?:class|struct|record|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)'
$fqns = New-Object System.Collections.Generic.HashSet[string]
Get-ChildItem -Path $Root -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object {
        $c = [System.IO.File]::ReadAllText($_.FullName)
        $nsM = [regex]::Match($c, $nsRx)
        if (-not $nsM.Success) { return }
        $ns = $nsM.Groups[1].Value
        foreach ($m in [regex]::Matches($c, $typeRx)) {
            [void]$fqns.Add("$ns.$($m.Groups[1].Value)")
        }
    }

$api = Join-Path $Root 'PublicAPI.Unshipped.txt'
if ((Test-Path $api) -and $fqns.Count -gt 0) {
    $escaped = $fqns | ForEach-Object { [regex]::Escape($_) }
    $rx = '^(?:(?:const|static|readonly|override|virtual|abstract|sealed|extern|unsafe|volatile) )*(?:' + ($escaped -join '|') + ')(?:$|[.<])'
    $lines = [System.IO.File]::ReadAllLines($api)
    $kept = $lines | Where-Object { $_ -notmatch $rx }
    [System.IO.File]::WriteAllLines($api, $kept)
    Write-Host "Stripped $($lines.Count - $kept.Count) PublicAPI lines ($($fqns.Count) internal FQNs)."
}
