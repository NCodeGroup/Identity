#!/usr/bin/env pwsh
#requires -version 7
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checker = Join-Path $PSScriptRoot '..' 'commit-attribution.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "identity-commit-attribution-$([guid]::NewGuid().ToString('N'))"
$repository = Join-Path $fixtureRoot 'repository'
$hooks = Join-Path $fixtureRoot 'hooks'
$messageFile = Join-Path $fixtureRoot 'message.txt'
$testCount = 0
$caseCount = 0

function Write-FixtureFile([string]$Path, [string]$Text) {
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-Git([string]$Root, [string[]]$Arguments) {
    $result = & git -C $Root @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$Root'."
    }
    return $result
}

function New-CaseCommit(
    [string]$Branch,
    [string]$BaseCommit,
    [string]$Message,
    [string]$Author,
    [string]$CommitterName,
    [string]$CommitterEmail
) {
    $script:caseCount++
    $null = Invoke-Git $repository @('switch', '-q', '-c', $Branch, $BaseCommit)
    Write-FixtureFile (Join-Path $repository "case-$script:caseCount.txt") "case $script:caseCount`n"
    $null = Invoke-Git $repository @('add', '-A')
    Write-FixtureFile $messageFile $Message
    $arguments = @('commit', '-q', '-F', $messageFile)
    if ($Author) {
        $arguments += @('--author', $Author)
    }
    try {
        if ($CommitterName) {
            $env:GIT_COMMITTER_NAME = $CommitterName
        }
        if ($CommitterEmail) {
            $env:GIT_COMMITTER_EMAIL = $CommitterEmail
        }
        $null = Invoke-Git $repository $arguments
    }
    finally {
        Remove-Item Env:GIT_COMMITTER_NAME -ErrorAction SilentlyContinue
        Remove-Item Env:GIT_COMMITTER_EMAIL -ErrorAction SilentlyContinue
    }
    return $Branch
}

function Add-CaseCommit([string]$Message, [string]$Author) {
    $script:caseCount++
    Write-FixtureFile (Join-Path $repository "case-$script:caseCount.txt") "case $script:caseCount`n"
    $null = Invoke-Git $repository @('add', '-A')
    Write-FixtureFile $messageFile $Message
    $arguments = @('commit', '-q', '-F', $messageFile)
    if ($Author) {
        $arguments += @('--author', $Author)
    }
    $null = Invoke-Git $repository $arguments
    return (Invoke-Git $repository @('rev-parse', 'HEAD') | Out-String).Trim()
}

function Assert-Attribution(
    [string]$Name,
    [string]$Base,
    [string]$Head,
    [int]$ExitCode,
    [string[]]$Contains,
    [string[]]$NotContains = @()
) {
    $output = & pwsh -NoProfile -File $checker -Root $repository -Base $Base -Head $Head 2>&1 | Out-String
    $code = $LASTEXITCODE
    if ($code -ne $ExitCode) {
        throw "Test '$Name' expected exit code $ExitCode, got $code. Output: $output"
    }
    foreach ($text in $Contains) {
        if (-not $output.Contains($text)) {
            throw "Test '$Name' expected output to contain '$text'. Output: $output"
        }
    }
    foreach ($text in $NotContains) {
        if ($output.Contains($text)) {
            throw "Test '$Name' expected output not to contain '$text'. Output: $output"
        }
    }
    $script:testCount++
    Write-Host "PASS: $Name"
}

try {
    $null = [System.IO.Directory]::CreateDirectory($hooks)
    $null = [System.IO.Directory]::CreateDirectory($repository)
    Invoke-Git $repository @('init', '-q') | Out-Null
    Invoke-Git $repository @('config', 'user.name', 'Commit Attribution') | Out-Null
    Invoke-Git $repository @('config', 'user.email', 'commit-attribution@example.invalid') | Out-Null
    Invoke-Git $repository @('config', 'commit.gpgsign', 'false') | Out-Null
    Invoke-Git $repository @('config', 'core.autocrlf', 'false') | Out-Null
    Invoke-Git $repository @('config', 'core.hooksPath', $hooks) | Out-Null
    Write-FixtureFile (Join-Path $repository 'base.txt') "base`n"
    Invoke-Git $repository @('add', '-A') | Out-Null
    Invoke-Git $repository @('commit', '-q', '-m', 'base') | Out-Null
    $base = (Invoke-Git $repository @('rev-parse', 'HEAD') | Out-String).Trim()

    $head = New-CaseCommit 'human' $base "Add a feature`n`nA plain message.`n"
    Assert-Attribution 'a human commit passes' $base $head 0 @('1 commit(s) checked')

    $head = New-CaseCommit 'agent-author' $base "Add a feature`n" 'copilot-swe-agent[bot] <198982749+Copilot@users.noreply.github.com>'
    Assert-Attribution 'an agent-authored commit fails' $base $head 1 @('is an AI agent account', '--reset-author')

    $head = New-CaseCommit 'claude-trailer' $base "Add a feature`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`n"
    Assert-Attribution 'a Claude co-author trailer fails' $base $head 1 @('message has agent attribution')

    $head = New-CaseCommit 'copilot-trailer' $base "Add a feature`n`nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`n"
    Assert-Attribution 'a Copilot co-author trailer fails' $base $head 1 @('message has agent attribution')

    $head = New-CaseCommit 'session-trailer' $base "Add a feature`n`nClaude-Session: 0f8fad5b-d9cb-469f-a165-70867728950e`n"
    Assert-Attribution 'a Claude-Session trailer fails' $base $head 1 @('message has agent attribution')

    $head = New-CaseCommit 'person-trailer' $base "Add a feature`n`nCo-authored-by: Jane Doe <jane@example.com>`n"
    Assert-Attribution "a person's co-author trailer passes" $base $head 0 @('1 commit(s) checked')

    $head = New-CaseCommit 'dependabot' $base "Bump a package`n" 'dependabot[bot] <49699333+dependabot[bot]@users.noreply.github.com>'
    Assert-Attribution 'a Dependabot commit passes' $base $head 0 @('1 commit(s) checked')

    $head = New-CaseCommit 'unknown-base' $base "Add a feature`n"
    Assert-Attribution 'an unknown base checks only the head' '0000000000000000000000000000000000000000' $head 0 @('checking')

    $head = New-CaseCommit 'agent-committer' $base "Add a feature`n" -CommitterName 'Copilot' -CommitterEmail '223556219+Copilot@users.noreply.github.com'
    Assert-Attribution 'an agent committer fails' $base $head 1 @('committer Copilot', 'is an AI agent account')

    $head = New-CaseCommit 'range' $base "First commit`n"
    $null = Add-CaseCommit "Middle commit`n" 'copilot-swe-agent[bot] <198982749+Copilot@users.noreply.github.com>'
    $null = Add-CaseCommit "Last commit`n"
    Assert-Attribution 'a range finds an agent commit in the middle' $base $head 1 @('Middle commit') @('First commit', 'Last commit')

    $head = New-CaseCommit 'github-copilot-trailer' $base "Add a feature`n`nCo-authored-by: GitHub Copilot <noreply@github.com>`n"
    Assert-Attribution 'a GitHub Copilot trailer fails' $base $head 1 @('message has agent attribution')

    $head = New-CaseCommit 'generated-with' $base "Add a feature`n`nGenerated with [Claude Code](https://claude.com/claude-code)`n"
    Assert-Attribution 'a Generated with Claude Code line fails' $base $head 1 @('message has agent attribution')

    $hooksSource = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..' '..' '.githooks')).Path
    $hookRepository = Join-Path $fixtureRoot 'hook-repository'
    $null = [System.IO.Directory]::CreateDirectory($hookRepository)
    Invoke-Git $hookRepository @('init', '-q') | Out-Null
    Invoke-Git $hookRepository @('config', 'user.name', 'Commit Attribution') | Out-Null
    Invoke-Git $hookRepository @('config', 'user.email', 'commit-attribution@example.invalid') | Out-Null
    Invoke-Git $hookRepository @('config', 'commit.gpgsign', 'false') | Out-Null
    Invoke-Git $hookRepository @('config', 'core.autocrlf', 'false') | Out-Null
    Invoke-Git $hookRepository @('config', 'core.hooksPath', $hooksSource) | Out-Null
    Write-FixtureFile (Join-Path $hookRepository 'hook.txt') "hook`n"
    Invoke-Git $hookRepository @('add', '-A') | Out-Null
    Write-FixtureFile $messageFile (@(
            'Hook check',
            '',
            'Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>',
            'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>',
            'Claude-Session: https://claude.ai/code/session_1',
            'Generated with [Claude Code](https://claude.com/claude-code)',
            'Co-authored-by: GitHub Copilot <noreply@github.com>',
            'Co-authored-by: Jane Doe <jane@example.com>'
        ) -join "`n")
    Invoke-Git $hookRepository @('commit', '-q', '-F', $messageFile) | Out-Null
    $hookMessage = (Invoke-Git $hookRepository @('log', '-1', '--format=%B') | Out-String)
    $hookName = 'the hook strips agent attribution and keeps people'
    if (-not $hookMessage.Contains('Jane Doe')) {
        throw "Test '$hookName' expected the message to contain 'Jane Doe'. Message: $hookMessage"
    }
    foreach ($text in @('anthropic.com', 'Copilot@', 'Claude-Session', 'Generated with', 'GitHub Copilot')) {
        if ($hookMessage.Contains($text)) {
            throw "Test '$hookName' expected the message not to contain '$text'. Message: $hookMessage"
        }
    }
    $testCount++
    Write-Host "PASS: $hookName"

    Write-Host "$testCount commit-attribution checks passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
