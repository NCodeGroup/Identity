#!/usr/bin/env pwsh
#requires -version 7
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path (Split-Path -Parent $PSScriptRoot) 'agent-customizations.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-agent-customizations-$([guid]::NewGuid().ToString('N'))"
$testCount = 0

function Write-FixtureFile([string]$Path, [string]$Text) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function New-Pair(
    [string]$Name,
    [string]$PromptBody = 'First step.',
    [string]$SkillBody = 'First step.',
    [string]$PromptHeader = 'description: "Example workflow"',
    [string]$SkillHeader = "name: example`ndescription: `"Example workflow`""
) {
    $root = Join-Path $fixtureRoot $Name
    Write-FixtureFile (Join-Path $root '.github\prompts\example.prompt.md') "---`n$PromptHeader`n---`n$PromptBody`n"
    Write-FixtureFile (Join-Path $root '.github\skills\example\SKILL.md') "---`n$SkillHeader`n---`n$SkillBody`n"
    return $root
}

function Assert-Valid([string]$Name, [string]$Root) {
    & $validator -Root $Root
    $script:testCount++
    Write-Host "PASS: $Name"
}

function Assert-Invalid([string]$Name, [string]$Root, [string]$Expected) {
    $failure = $null
    try {
        & $validator -Root $Root
    }
    catch {
        $failure = $_.Exception.Message
    }
    if ($null -eq $failure -or -not $failure.Contains($Expected)) {
        throw "Test '$Name' expected '$Expected', got '$failure'."
    }
    if (-not $failure.Contains($Root)) {
        throw "Test '$Name' did not identify the offending fixture path."
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

try {
    Assert-Valid 'repository pairs' (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $root = New-Pair 'arguments' '${input:details:What to do}' 'the request given after the command name'
    Assert-Valid 'argument adapter' $root

    $root = New-Pair 'links' "[rules](../instructions/rules.md)`n[root](../../AGENTS.md)" "[rules](../../instructions/rules.md)`r`n[root](../../../AGENTS.md)"
    Assert-Valid 'relative links and CRLF' $root
    $root = New-Pair 'single-quotes' -PromptHeader "description: 'Example workflow'" -SkillHeader "name: example`ndescription: Example workflow"
    Assert-Valid 'scalar quoting' $root

    $root = New-Pair 'missing-skill'
    Remove-Item -LiteralPath (Join-Path $root '.github\skills\example\SKILL.md')
    Assert-Invalid 'missing skill' $root 'missing matching skill'
    $root = New-Pair 'missing-prompt'
    Remove-Item -LiteralPath (Join-Path $root '.github\prompts\example.prompt.md')
    Assert-Invalid 'missing prompt' $root 'missing matching prompt'

    $root = New-Pair 'description' -SkillHeader "name: example`ndescription: Different"
    Assert-Invalid 'description drift' $root 'description differs'
    $root = New-Pair 'body' 'First step.' 'Different step.'
    Assert-Invalid 'body drift' $root 'workflow body differs'
    $root = New-Pair 'ordering' "First.`nSecond." "Second.`nFirst."
    Assert-Invalid 'body ordering' $root 'workflow body differs'
    $root = New-Pair 'case' 'First step.' 'first step.'
    Assert-Invalid 'body case' $root 'workflow body differs'
    $root = New-Pair 'code-path' 'Read ../src/file.cs.' 'Read ../../src/file.cs.'
    Assert-Invalid 'literal paths are not links' $root 'workflow body differs'
    $root = New-Pair 'inline-code' '`[rules](../rules.md)`' '`[rules](../../rules.md)`'
    Assert-Invalid 'inline code is not a link' $root 'workflow body differs'
    $root = New-Pair 'fenced-code' "``````md`n[rules](../rules.md)`n``````" "``````md`n[rules](../../rules.md)`n``````"
    Assert-Invalid 'fenced code is not a link' $root 'workflow body differs'

    $root = New-Pair 'missing-header'
    Write-FixtureFile (Join-Path $root '.github\skills\example\SKILL.md') 'No header.'
    Assert-Invalid 'missing header' $root 'expected a YAML header'
    $root = New-Pair 'unclosed-header'
    Write-FixtureFile (Join-Path $root '.github\skills\example\SKILL.md') "---`nname: example`ndescription: Example"
    Assert-Invalid 'unclosed header' $root 'expected a YAML header'
    $root = New-Pair 'malformed-header' -SkillHeader "name: example`ndescription missing colon"
    Assert-Invalid 'malformed header' $root 'invalid scalar metadata'
    $root = New-Pair 'invalid-quotes' -SkillHeader "name: example`ndescription: `"Unclosed"
    Assert-Invalid 'invalid quoted scalar' $root 'invalid quoted metadata'
    $root = New-Pair 'duplicate-header' -SkillHeader "name: example`nname: other`ndescription: Example"
    Assert-Invalid 'duplicate metadata' $root 'duplicate metadata'
    $root = New-Pair 'missing-description' -SkillHeader 'name: example'
    Assert-Invalid 'missing description' $root 'description must not be blank'
    $root = New-Pair 'blank-description' -SkillHeader "name: example`ndescription: `"`""
    Assert-Invalid 'blank description' $root 'description must not be blank'
    $root = New-Pair 'name' -SkillHeader "name: other`ndescription: Example"
    Assert-Invalid 'wrong skill name' $root "skill name must equal 'example'"
    $root = New-Pair 'missing-name' -SkillHeader 'description: Example'
    Assert-Invalid 'missing skill name' $root "skill name must equal 'example'"

    foreach ($key in @('agent', 'model', 'tools', 'disable-model-invocation', 'user-invocable')) {
        $root = New-Pair "forbidden-$key" -SkillHeader "name: example`ndescription: `"Example workflow`"`n${key}: true"
        Assert-Invalid "unsupported $key" $root "unsupported skill field '$key'"
    }
    $root = New-Pair 'blank-skill' 'First step.' ' '
    Assert-Invalid 'blank skill body' $root 'workflow body must not be blank'
    $root = New-Pair 'blank-prompt' ' ' 'First step.'
    Assert-Invalid 'blank prompt body' $root 'workflow body must not be blank'
    $root = Join-Path $fixtureRoot 'empty'
    $null = [System.IO.Directory]::CreateDirectory($root)
    Assert-Invalid 'empty inventory' $root 'expected at least one prompt/skill pair'

    Write-Host "Agent customization tests: PASSED ($testCount cases)."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
