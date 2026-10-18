# 128. Commits are attributed to people, not agents

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group

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
- Git hooks are client-side only. Nothing rejects a tainted commit server-side, so a push from an unconfigured clone
  can still reintroduce the trailer and the remedy is again a history rewrite. The recorded recipe:

  ```pwsh
  # Strip the trailer across the tainted range, then move the trunk.
  git filter-branch -f --msg-filter "sed '/^Co-authored-by: Copilot </d' | git stripspace" <first-bad>^..HEAD
  git push --force-with-lease origin main
  ```

  Verify with an empty `git diff --stat <backup-tag> HEAD` — the rewrite must change messages only, never content.
- GitHub recomputes the contributor list within a minute or two of the push, but the rendered repository page is cached
  separately and can keep showing the stale roster; the contributors API is the authoritative check.
