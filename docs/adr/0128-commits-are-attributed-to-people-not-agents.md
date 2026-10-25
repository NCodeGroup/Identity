# 128. Commits are attributed to people, not agents

- **Status:** Accepted (amended 2026-10-24)
- **Date:** 2026-10-17
- **Deciders:** NCode Group

## Amendment (2026-10-24): agent-authored commits and a server-side check

The trailer was not the only way an agent reached the contributor graph. The Copilot cloud agent authors its own
commits as `copilot-swe-agent[bot]`, and 124 of them reached `main` through PR #9, so GitHub listed Copilot as a
contributor although no commit carried a trailer naming it. The hook could not see this: it reads the message, not the
author, and it runs only in a clone that enabled it. A squash merge adds to the problem, because GitHub appends a
`Co-authored-by:` trailer for every commit author on its own servers, where no client-side hook runs.

On 2026-10-24 the history was rewritten with `git filter-repo` and a commit callback that set the author of each agent
commit to the person (229 commits re-SHA'd, trees unchanged), and `main` was force-pushed with its ruleset briefly
disabled.

The decision gains a third level, and the first two now cover Claude as well:

- **Enforcement — server-side.** The required `Commit attribution` check runs
  [`build/commit-attribution.ps1`](../../build/commit-attribution.ps1) over every commit it is about to accept. It
  fails when the author or committer is an AI agent account, or when the message carries agent attribution: a
  `Co-authored-by:` line naming one, a `Claude-Session:` line, or a "Generated with [Claude Code" line. The workflow
  [`commit-attribution.yml`](../../.github/workflows/commit-attribution.yml) runs it on pull requests, in the merge
  queue (the commit that lands, including the trailers GitHub writes when it squashes) and on pushes to `main` and
  `release/**`; its tests run in the Definition of Done. After it merges, the maintainer adds `Commit attribution` to
  the `main` and `release-lines` rulesets' required status checks.
- **Prevention and the hook — Claude as well as Copilot.** The hook keeps stripping, never rejecting, and now also
  strips Claude attribution. Claude Code's `attribution` setting is off in `.claude/settings.json`, and VS Code's
  `git.addAICoAuthor` is `off` in `.vscode/settings.json`. The Copilot-only workflow of
  [ADR-0170](0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md) is
  unchanged.
- **The cloud agent's pull requests are re-authored before they merge**, because their commits fail the check until
  then:

  ```pwsh
  git rebase origin/main --exec "git commit --amend --no-edit --reset-author"
  git push --force-with-lease
  ```

## Context

Coding agents append a co-author trailer to the commits they help write:

```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

The repository host reads that trailer as authorship. GitHub derives the contributor list — the count and avatars on
the repository landing page, and the contributors API — from commit authors **and** their `Co-authored-by` trailers, so
the agent account was listed as one of this repository's two contributors.

Three properties make this worth a decision rather than a cleanup:

1. **The only remedy is rewriting published history.** The trailer is part of the immutable commit object, so removing
   it re-SHAs that commit and every descendant, and the corrected trunk has to be force-pushed. On 2026-10-17 the
   trailer had reached 10 commits; stripping it rewrote 183 commits.
2. **The failure mode is self-restoring.** Thirty minutes after that rewrite, a concurrently running agent session —
   one that had started before the preference was expressed — put the trailer back on 7 new commits, forcing a second
   rewrite and a second force-push. One unaware session re-taints the trunk, and each recurrence costs another rewrite.
3. **A per-session instruction cannot bind the next session.** Telling one agent, or storing the preference in an
   agent's volatile memory, does not reach a session that started earlier, a different tool, a different machine, or a
   human typing `git commit`. By [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) the guard has to be
   a versioned artifact in the repository.

## Decision

**A commit records the people who authored it. Agent accounts are never co-authors.**

This is not a claim that agents did not contribute — it is that the repository's contributor graph is a roster of
**accountable humans**, not a tooling manifest. Which tools produced a change is visible in the change itself, in the
pull request, and in this ADR; it does not belong in the commit's authorship record.

The rule is enforced at two levels, because prevention and enforcement fail differently:

- **Prevention — an instruction.** [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) states
  the rule, so agents working in this repository do not generate the trailer in the first place. This is the control
  that works on every clone, including ones that never ran the setup step below.
- **Enforcement — a versioned hook.** [`.githooks/commit-msg`](../../.githooks/commit-msg) removes any
  `Co-authored-by:` trailer naming the `Copilot` user or the `copilot-swe-agent[bot]` account. It is enabled per clone
  with `git config core.hooksPath .githooks` ([CONTRIBUTING.md](../../CONTRIBUTING.md)).

The hook **strips** the trailer rather than rejecting the commit. A rejected commit interrupts an automated flow over
an attribution detail, and an agent that is blocked will simply retry; a silent strip cannot be worked around, cannot
fail a batch, and leaves a message that is already correct untouched.

## Consequences

- The contributor graph lists only people, and stays that way without anyone remembering to check it.
- Enabling the hook is a one-time, per-clone step. A clone that skips it is unprotected, which is precisely why the
  instruction file — not the hook — is the primary control; the hook is the safety net for the case where an agent
  ignores or never loads the instruction.
- Git hooks are client-side only. Unless the `Commit attribution` check above stops it, a push from an unconfigured
  clone can still reintroduce the trailer and the remedy is again a history rewrite. The recorded recipe for a
  trailer follows; it removes trailers only and leaves authorship alone:

  ```pwsh
  # Strip the trailer across the tainted range, then move the trunk.
  git filter-branch -f --msg-filter "sed '/^Co-authored-by: Copilot </d' | git stripspace" <first-bad>^..HEAD
  git push --force-with-lease origin main
  ```

  Verify with an empty `git diff --stat <backup-tag> HEAD` — the rewrite must change messages only, never content.
- A commit authored by an agent account needs the author changed, which `filter-branch --msg-filter` cannot do. The
  recorded recipe, run in a fresh clone, is `git filter-repo` with a commit callback:

  ```pwsh
  git tag backup-main main
  $callback = @'
  import re
  if commit.author_email.endswith(b"+Copilot@users.noreply.github.com") or b"copilot-swe-agent" in commit.author_name:
      commit.author_name = b"<person>"
      commit.author_email = b"<person-email>"
      commit.message = re.sub(rb"(?mi)^co-authored-by:[ \t]*<person>[ \t]*<[^>]*>[ \t]*\n?", b"", commit.message)
      commit.message = commit.message.rstrip(b"\n") + b"\n"
  '@
  uvx --from git-filter-repo git-filter-repo --force --refs main --commit-callback $callback
  git remote add origin <url>   # filter-repo removes the remote
  git push --force-with-lease=main:<old tip> origin main   # <old tip>: the SHA of backup-main
  ```

  A `Co-authored-by:` trailer naming that same person is redundant after the rewrite and is removed in the same
  callback. Verify with an empty `git diff --stat backup-main main`, and that every rewritten commit keeps its tree. The
  push needs the `main` ruleset disabled for its duration; enable it again immediately afterwards.
- GitHub recomputes the contributor list within a minute or two of the push, but the rendered repository page is cached
  separately and can keep showing the stale roster; the contributors API is the authoritative check.
