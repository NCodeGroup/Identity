#!/usr/bin/env pwsh
#requires -version 7
<#
.SYNOPSIS
    Checks that Local prompts and Copilot skills expose the same workflows.
.PARAMETER Root
    Repository root. Defaults to the parent of this script's directory.
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Customization([string]$Path) {
    $text = [System.IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $match = [regex]::Match($text, '\A---\n(?<header>.*?)\n---\n(?<body>[\s\S]*)\z', 'Singleline')
    if (-not $match.Success) {
        throw "${Path}: expected a YAML header bounded by --- lines."
    }

    $metadata = @{}
    foreach ($line in $match.Groups['header'].Value.Split("`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $field = [regex]::Match($line, '^(?<key>[a-z][a-z0-9-]*):[ \t]*(?<value>.+?)\s*$')
        if (-not $field.Success) {
            throw "${Path}: invalid scalar metadata line '$line'."
        }
        $key = $field.Groups['key'].Value
        $value = $field.Groups['value'].Value
        if ($metadata.ContainsKey($key)) {
            throw "${Path}: duplicate metadata field '$key'."
        }
        if ($value.StartsWith('"')) {
            if ($value -notmatch '^"(?:[^"\\]|\\["\\/bfnrt]|\\u[0-9a-fA-F]{4})*"$') {
                throw "${Path}: invalid quoted metadata field '$key'."
            }
            $value = [System.Text.Json.JsonSerializer]::Deserialize[string]($value)
        }
        elseif ($value.StartsWith("'")) {
            if ($value -notmatch "^'(?:[^']|'')*'$") {
                throw "${Path}: invalid quoted metadata field '$key'."
            }
            $value = $value.Substring(1, $value.Length - 2).Replace("''", "'")
        }
        elseif ($value -match '[:#\[\]{}>|]' -or $value -in @('-', '?', '!', '&', '*')) {
            throw "${Path}: unsupported scalar metadata field '$key'; quote scalar values."
        }
        $metadata[$key] = $value
    }

    if (-not $metadata.ContainsKey('description') -or [string]::IsNullOrWhiteSpace($metadata['description'])) {
        throw "${Path}: description must not be blank."
    }
    $body = $match.Groups['body'].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($body)) {
        throw "${Path}: workflow body must not be blank."
    }
    return [pscustomobject]@{ Metadata = $metadata; Body = $body }
}

function Convert-PromptBody([string]$Body) {
    $bodyWithArguments = [regex]::Replace($Body, '\$\{input:details:[^}\r\n]+\}', 'the request given after the command name')
    $fenceCharacter = ''
    $fenceLength = 0
    $lines = foreach ($line in $bodyWithArguments.Split("`n")) {
        $fence = [regex]::Match($line, '^\s*(?<marker>`{3,}|~{3,})')
        if ($fence.Success) {
            $marker = $fence.Groups['marker'].Value
            if ($fenceCharacter -eq '') {
                $fenceCharacter = $marker.Substring(0, 1)
                $fenceLength = $marker.Length
            }
            elseif ($marker.StartsWith($fenceCharacter) -and $marker.Length -ge $fenceLength -and
                $line -match '^\s*(`+|~+)\s*$') {
                $fenceCharacter = ''
            }
            $line
        }
        elseif ($fenceCharacter -ne '') {
            $line
        }
        else {
            # Protect inline code; only real relative Markdown link targets change depth.
            [regex]::Replace($line, '(?<code>`+).*?\k<code>|\]\((?<target>\.\.?/[^\s)]+)\)', {
                param($link)
                if ($link.Groups['target'].Success) {
                    return '](../' + $link.Groups['target'].Value + ')'
                }
                return $link.Value
            })
        }
    }
    return $lines -join "`n"
}

$promptDirectory = Join-Path $Root '.github\prompts'
$skillDirectory = Join-Path $Root '.github\skills'
$problems = [System.Collections.Generic.List[string]]::new()
$prompts = @()
$skills = @()
if (Test-Path -LiteralPath $promptDirectory -PathType Container) {
    $prompts = @(Get-ChildItem -LiteralPath $promptDirectory -Filter '*.prompt.md' -File)
}
if (Test-Path -LiteralPath $skillDirectory -PathType Container) {
    $skills = @(Get-ChildItem -LiteralPath $skillDirectory -Directory)
}
if ($prompts.Count -eq 0) {
    $problems.Add("${promptDirectory}: expected at least one prompt/skill pair.")
}

$promptNames = @($prompts | ForEach-Object { $_.Name -creplace '\.prompt\.md$', '' })
foreach ($directory in $skills) {
    if ($directory.Name -cnotin $promptNames) {
        $problems.Add("$($directory.FullName): missing matching prompt '$($directory.Name).prompt.md'.")
    }
}

$pairCount = 0
foreach ($promptFile in $prompts) {
    $name = $promptFile.Name -creplace '\.prompt\.md$', ''
    $skillPath = Join-Path $skillDirectory "$name\SKILL.md"
    if (-not (Test-Path -LiteralPath $skillPath -PathType Leaf)) {
        $problems.Add("${skillPath}: missing matching skill for '$($promptFile.FullName)'.")
        continue
    }
    try {
        $prompt = Read-Customization $promptFile.FullName
        $skill = Read-Customization $skillPath
        if (-not $skill.Metadata.ContainsKey('name') -or $skill.Metadata['name'] -cne $name) {
            $problems.Add("${skillPath}: skill name must equal '$name'.")
        }
        foreach ($key in @('agent', 'model', 'tools', 'disable-model-invocation', 'user-invocable')) {
            if ($skill.Metadata.ContainsKey($key)) {
                $problems.Add("${skillPath}: unsupported skill field '$key' in the dual-runtime contract.")
            }
        }
        if ($prompt.Metadata['description'] -cne $skill.Metadata['description']) {
            $problems.Add("${skillPath}: description differs from '$($promptFile.FullName)'.")
        }
        $expectedBody = Convert-PromptBody $prompt.Body
        if ($expectedBody -cne $skill.Body) {
            $problems.Add("${skillPath}: workflow body differs from '$($promptFile.FullName)' after argument/link adaptation.")
        }
        $pairCount++
    }
    catch {
        $problems.Add($_.Exception.Message)
    }
}

if ($problems.Count -gt 0) {
    throw "Agent customization validation failed:`n$($problems -join "`n")"
}
Write-Host "Agent customizations: PASSED ($pairCount prompt/skill pairs)."
