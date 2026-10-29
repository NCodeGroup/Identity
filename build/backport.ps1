#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Opens the backport pull requests that a merged pull request's `backport vX.Y` labels ask for.
.DESCRIPTION
    Servicing is upstream-first (GOVERNANCE.md "Releases, branches & tags", ADR-0263): a fix merges to `main`, and a
    backport pull request cherry-picks it with `git cherry-pick -x` onto `backport/<pr>-vX.Y`, cut from `release/vX.Y`,
    and targets that line. For each `backport vX.Y` label, this script cuts that branch, cherry-picks the commit the pull
    request merged as, pushes the branch and opens the pull request. When the release line does not exist, or the
    cherry-pick conflicts, it comments on the original pull request instead, with the commands that finish the backport
    by hand. A line whose backport branch already exists is skipped, so running it again is harmless.

    It runs in a clone of the repository whose `origin` it can push to, with `gh` authenticated (GH_TOKEN) and a git
    identity configured; the Backport workflow provides all three.
.PARAMETER PullRequest
    The number of the merged pull request.
.PARAMETER Commit
    The commit the pull request merged as on `main`.
.PARAMETER Labels
    The labels to act on; only those of the form `backport vX.Y` are used.
.PARAMETER ChecksNotTriggered
    Set when the pull requests are opened with the workflow's own token, which starts no workflows on them, so their
    description says how to start the checks.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$PullRequest,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$Commit,

    [Parameter(Mandatory)]
    [AllowEmptyCollection()]
    [string[]]$Labels,

    [switch]$ChecksNotTriggered
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    git @args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Add-Comment([string]$Body) {
    $Body | gh pr comment $PullRequest --body-file -
    if ($LASTEXITCODE -ne 0) {
        throw "Commenting on pull request #$PullRequest failed with exit code $LASTEXITCODE."
    }
}

$lines = @(
    @(
        foreach ($label in $Labels) {
            if ($label -cmatch '^backport v(?<line>[0-9]+\.[0-9]+)$') {
                $Matches['line']
            }
        }
    ) | Sort-Object -Unique
)

if ($lines.Count -eq 0) {
    Write-Host 'No backport label; nothing to do.'
    return
}

$title = gh pr view $PullRequest --json title --jq .title
if ($LASTEXITCODE -ne 0) {
    throw "Reading pull request #$PullRequest failed with exit code $LASTEXITCODE."
}

# A squash or rebase merge leaves one parent; a merge commit needs its first parent named as the mainline.
$parentCount = (git rev-list --parents -n 1 $Commit).Split(' ').Count - 1
$mainline = if ($parentCount -gt 1) { @('-m', '1') } else { @() }

foreach ($line in $lines) {
    $base = "release/v$line"
    $branch = "backport/$PullRequest-v$line"

    git ls-remote --exit-code --heads origin $branch *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "$branch already exists; skipping v$line."
        continue
    }

    git fetch --no-tags origin "refs/heads/${base}:refs/remotes/origin/$base" *> $null
    if ($LASTEXITCODE -ne 0) {
        Add-Comment "The ``backport v$line`` label names the release line ``$base``, which does not exist, so nothing was backported to it."
        continue
    }

    Invoke-Git switch --quiet --create $branch "origin/$base"
    git cherry-pick -x @mainline $Commit
    if ($LASTEXITCODE -ne 0) {
        git cherry-pick --abort
        Invoke-Git switch --quiet --detach "origin/$base"
        Invoke-Git branch --quiet --delete --force $branch
        $manual = @(
            "The cherry-pick of $Commit onto ``$base`` conflicts, so the backport to v$line needs a hand:",
            '',
            '```sh',
            "git fetch origin $base",
            "git switch --create $branch origin/$base",
            "git cherry-pick -x $($mainline -join ' ') $Commit".Replace('  ', ' '),
            '# resolve the conflicts, then: git cherry-pick --continue',
            "git push origin $branch",
            "gh pr create --base $base --head $branch --title `"$title (backport v$line)`"",
            '```'
        ) -join "`n"
        Add-Comment $manual
        continue
    }

    Invoke-Git push --quiet origin $branch

    $body = @(
        "Backport of #$PullRequest to ``$base``: ``git cherry-pick -x $Commit``.",
        ''
    )
    if ($ChecksNotTriggered) {
        $body += 'This pull request was opened with the workflow token, which starts no workflows on it: close and reopen it to run its checks.'
    }

    ($body -join "`n") | gh pr create --base $base --head $branch --title "$title (backport v$line)" --body-file -
    if ($LASTEXITCODE -ne 0) {
        throw "Opening the backport pull request for v$line failed with exit code $LASTEXITCODE."
    }

    Invoke-Git switch --quiet --detach "origin/$base"
}
