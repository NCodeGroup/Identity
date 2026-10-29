# Resume: `/run-issues` "anything related to BFF" (paused 2026-10-29)

Work-in-progress snapshot. `PLAN.md` and this file are committed here only so the run can continue elsewhere;
neither belongs on `main`. Delete both before this work reaches a pull request.

## Queue and results

| Entry | Title | Result |
|---|---|---|
| #389 | Dynamic registration of back-channel logout metadata | Landed as 187621a0 (#397) |
| #393 | End-to-end DPoP test of the protocol client | Landed as 46dbc742 (#403) |
| #392 | mTLS client authentication in the protocol client | Landed as 7b159820 (#405) |
| #394 | BFF phase 2, the `NCode.Identity.Bff` core | **In progress: 63 of 72 steps done** (this branch) |

Not queued (from triage): #395, #396 and #158 wait on #394. #123 is the umbrella. Triage judged #342 (duplicate of
#347, done) and #344 (obsolete) ready to close.

## Where #394 stands

- Steps 1–62 and fix step 72 are done and committed: slices (a) session store and cookie, (b) scheme handler, login,
  callback and `/bff/user`, (c) token manager with single-flight refresh and DPoP, (d) `AsBffApi`, CSRF and `UseBff`,
  (e) logout and back-channel logout, and the first two end-to-end tests. All 333 `NCode.Identity.Bff.Tests` pass, and
  so do both BFF tests in `NCode.Identity.OpenId.IntegrationTests` (`FullyQualifiedName~.Bff.`).
- Remaining, in `PLAN.md` (`### [ ]` headings):
  - 63: prove back-channel logout ends the BFF session and that a misordered `UseBff` fails closed.
  - 64–68: write ADR-0289 to ADR-0293. They were renumbered from 0288–0292, because `main` took 0287 in #404 and 0288
    in #405.
  - 69: update `docs/backend-for-frontend.md`.
  - 70: catalogue the BFF audit events in `docs/audit-records.md`.
  - 71: update `AGENTS.md` and `CHANGELOG.md`.
- Then: `/review-plan-diff` (none has run yet), then `/ship-plan draft` (a draft pull request whose description ends
  with `Closes #394`).
- This branch was cut from `main` at 46dbc742, before #405 (mTLS) merged. Merge `origin/main` in before shipping, by
  merging and never by rebasing a pushed branch. Expect conflicts in:
  - `CHANGELOG.md` (Unreleased → Added)
  - `src/NCode.Identity.OpenId.Client.Abstractions/PublicAPI.Unshipped.txt`, which step 30 added
    `IdTokenValidationResult.Identity` to
  - `docs/decisions/README.md`
  - possibly `NCode.Identity.slnx`

## Points for the review of #394

- `BffLoginHandlerTests` uses `DateTimeOffset.UtcNow`, because the in-process value set expires entries by the real
  clock. Check it against the testing conventions.
- `DefaultRegistrationTests` asserts `ThrowsAny<Exception>` for an unknown configuration key. Narrow it to the actual
  exception type.
- `BffSessionRecord` and `BffStoreUnavailableException` docs use `<c>IBffSessionStore</c>`. Make them crefs now that
  the interface exists.
- These problem titles were written by the executor: 403 "Access to this resource is forbidden.", the anti-forgery
  title, and "An access token is unavailable."
- `BffCookies` suppresses CA1822 on three methods, because callers need them as instance methods.
- `DefaultIdTokenValidatorTests` uses the claim-type literal `"sub"`. `BffCallbackHandlerTests` uses the literal
  `"invalid_grant"`, which has the constant `OpenIdProtocol.ErrorCodes.InvalidGrant`.
- A refresh response without `access_token` maps to 502 `provider_error` and has no test. The
  `OpenIdProviderException` and `OperationCanceledException` refresh branches share a catch filter, and only the
  `HttpRequestException` case is tested.
- The `IIdTokenValidator` path doesn't catch `OpenIdProviderException`, and neither does the callback's metadata
  resolution. Decide whether either should map to a login failure.
- The plan's Files lines left out `BffSessionPrincipal.cs` (step 33) and the test helpers `StubHttpMessageHandler.cs`
  and `RecordedRequest.cs` (step 51). They were added anyway.

## To-dos outside the plan

- Issue #123: update its "Remaining" list to show DPoP and mTLS as done.
- Follow-ups filed so far: #398 and #399 (from #389), and #407 (from #392). #394's seven follow-ups are listed in
  `PLAN.md` and get filed when it lands.
- Project-template gap: once a pull request has merged, `Get-PlanState.ps1` reports `next: finish-plan` even when the
  plan's follow-ups are unfiled, so `ship-plan` never files them. This run filed #389's and #392's by hand.

## How to continue

1. Locally, the plan's worktree still exists: `.claude/worktrees/issue-394` on branch `worktree-issue-394`, at
   6eb19719 (this branch minus the snapshot commits). Run `/continue-plan issue-394` or `/orchestrate-plan issue-394`
   from a new session.
2. From another machine, create the worktree from this branch:
   `git worktree add .claude/worktrees/issue-394 -b worktree-issue-394 origin/dev`. Then move the snapshot back out of
   history: copy `PLAN.md` aside, run `git reset --hard HEAD~2` (this drops the two snapshot commits and deletes
   `RESUME.md`), copy `PLAN.md` back as an untracked file, and run `/orchestrate-plan issue-394`.
